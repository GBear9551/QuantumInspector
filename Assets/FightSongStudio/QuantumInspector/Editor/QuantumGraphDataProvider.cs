using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    internal sealed class QuantumProjectIndex
    {
        private string[] paths;
        private int cursor;
        private readonly Dictionary<string,List<string>> children=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        public bool Ready=>paths!=null&&cursor==paths.Length;
        public string Status=>paths==null?"Preparing project index":$"Indexing project paths {cursor:N0}/{paths.Length:N0}";
        public void Invalidate(){paths=null;cursor=0;children.Clear();}
        public bool Tick(int budget)
        {
            if(paths==null)paths=AssetDatabase.GetAllAssetPaths();
            int end=Math.Min(paths.Length,cursor+budget);
            for(;cursor<end;cursor++)
            {
                string path=paths[cursor];
                if(!path.StartsWith("Assets/",StringComparison.Ordinal)&&!path.StartsWith("Packages/",StringComparison.Ordinal))continue;
                string parent=Path.GetDirectoryName(path)?.Replace('\\','/');if(parent==null)continue;
                if(!children.TryGetValue(parent,out var list))children[parent]=list=new List<string>();list.Add(path);
            }
            return Ready;
        }
        public IEnumerable<string> Children(string path)=>children.TryGetValue(path,out var list)?list:Enumerable.Empty<string>();
    }

    public sealed class QuantumGraphDataProvider:IDisposable
    {
        private sealed class Job
        {
            public QuantumGraphNode Node;
            public IEnumerator<bool> Iterator;
            public bool Overlay;
        }
        private readonly QuantumProjectIndex index=new QuantumProjectIndex();
        private readonly Queue<Job> jobs=new Queue<Job>();
        private readonly Dictionary<string,int> dirtyCounts=new Dictionary<string,int>();
        private readonly Dictionary<string,int> referenceVersions=new Dictionary<string,int>();
        private readonly Dictionary<string,QuantumGraphOverlays> referenceRequests=new Dictionary<string,QuantumGraphOverlays>();
        private readonly HashSet<string> overlaysQueued=new HashSet<string>();
        private readonly HashSet<string> hierarchyRevealed=new HashSet<string>();
        private Dictionary<string,QuantumSavedNode> saved=new Dictionary<string,QuantumSavedNode>();
        private InspectorWheelSettings settings;
        private QuantumGraphOverlays overlays;
        private QuantumProjectGrouping grouping;
        private string failure="";
        private static int nextGeneration;
        private readonly Dictionary<string,string> missingSignatures=new Dictionary<string,string>();
        private readonly Dictionary<string,string> missingEpochs=new Dictionary<string,string>();
        private readonly HashSet<string> pending=new HashSet<string>();
        private bool pendingProject;
        public int PendingChanges=>pending.Count;
        public string ChangeSummary {get;private set;}="";
        public QuantumGraphDocument Document {get;private set;}
        public bool Busy=>jobs.Count>0;
        public string Status=>!string.IsNullOrEmpty(failure)?failure:Document!=null&&Document.EntireProject&&!index.Ready?index.Status:jobs.Count>0?$"Expanding graph · {jobs.Count} pending branches":$"{Document?.Nodes.Count??0:N0} discovered nodes · expand a node to inspect deeper";

        public static string KeyFor(WheelScope scope,bool entire,InspectorWheelSettings configuration)=>
            scope.Kind==WheelScopeKind.ProjectFolder?(entire?"project:entire:"+configuration.GraphIncludePackages:"project:"+QuantumGraphIdentity.AssetId(scope.FolderPath)):
                scope.Object!=null?"hierarchy:"+QuantumGraphIdentity.ObjectId(scope.Object):"hierarchy:scene-roots";
        public QuantumGraphNode Investigate(Object target,QuantumGraphNode source)
        {
            if(Document.Project&&target is GameObject prefab&&EditorUtility.IsPersistent(prefab)&&source.Kind==QuantumGraphNodeKind.Asset)
            {
                var contents=GameObjectNode(prefab,null);
                if(contents.ParentId==null){contents.ParentId=Document.RootId;if(!Document.Children.TryGetValue(Document.RootId,out var children))Document.Children[Document.RootId]=children=new HashSet<string>();children.Add(contents.Id);}
                Document.Link(source.Id,contents.Id,"Prefab contents",settings.OrbitColor);Expand(contents);return contents;
            }
            return EnsureReference(target,source,"Explored reference",false);
        }
        public QuantumGraphDocument Open(WheelScope scope,bool entire,InspectorWheelSettings configuration,bool restoreState=true)
        {
            settings=configuration;grouping=settings.GraphProjectGrouping;overlays=settings.GraphOverlays;
            bool project=scope.Kind==WheelScopeKind.ProjectFolder;
            string key=KeyFor(scope,entire,settings);
            if(Document!=null&&Document.Key==key)return Document;
            DisposeJobs();dirtyCounts.Clear();referenceVersions.Clear();referenceRequests.Clear();overlaysQueued.Clear();hierarchyRevealed.Clear();missingSignatures.Clear();missingEpochs.Clear();pending.Clear();pendingProject=false;failure="";
            var state=restoreState?QuantumGraphStatePersistence.instance.Get(key):null;
            saved=state?.Nodes.GroupBy(n=>n.Id).ToDictionary(g=>g.Key,g=>g.Last())??new Dictionary<string,QuantumSavedNode>();
            Document=new QuantumGraphDocument{Key=key,Project=project,EntireProject=entire,Generation=++nextGeneration};
            QuantumGraphNode root;
            if(project)
            {
                if(entire&&settings.GraphIncludePackages)
                {
                    root=Add(new QuantumGraphNode{Id="project:roots",Kind=QuantumGraphNodeKind.World,Label="Unity Project",Subtitle="Assets + Packages",Color=settings.OrbitColor,Expanded=true,Loaded=true});
                    var assets=Asset("Assets",root.Id);var packages=Asset("Packages",root.Id);assets.Expanded=true;packages.Expanded=false;
                    QueueExpansion(assets);
                }
                else {root=Asset(entire?"Assets":scope.FolderPath,null);root.Expanded=true;QueueExpansion(root);}
            }
            else if(scope.Object!=null)
            {root=GameObjectNode(scope.Object,null);root.Expanded=true;PopulateComponents(root);}
            else
            {
                root=Add(new QuantumGraphNode{Id="hierarchy:scene-roots",Kind=QuantumGraphNodeKind.World,Label="Loaded scenes",Subtitle="Select or expand a GameObject",Expanded=true,Loaded=true,Color=settings.OrbitColor});
                foreach(var entry in new HierarchyOrbitalProvider().Discover(new WheelScope{Kind=WheelScopeKind.SceneRoots},null,settings))GameObjectNode((GameObject)entry.Target,root.Id);
            }
            Document.RootId=root.Id;root.Expanded=true;
            if(!root.Positioned){root.Position=Vector2.zero;root.Positioned=true;}
            RestoreExpanded();return Document;
        }
        private QuantumGraphNode Add(QuantumGraphNode node)
        {
            bool exists=Document.Nodes.ContainsKey(node.Id);
            QuantumGraphNodeSizing.Apply(node,settings);
            node.SearchText=ComponentDiscoveryService.Normalize(node.Label+" "+node.Subtitle+" "+node.AssetPath+" "+node.PropertyPath+" "+node.SearchText);
            var result=Document.Upsert(node);
            if(!exists&&saved.TryGetValue(result.Id,out var state))
            {
                result.Position=state.Position;result.OverviewOffset=state.OverviewOffset;result.Positioned=state.Positioned;result.Manual=state.Manual;result.Pinned=state.Pinned;result.Expanded=state.Expanded;result.Slot=state.Slot;
                result.CustomSize=state.CustomSize||(state.SizeVersion==0&&state.Size.y>64.1f);
                if(result.CustomSize)result.Size=state.Size;
            }
            QuantumGraphNodeSizing.Apply(result,settings);
            return result;
        }
        private QuantumGraphNode GameObjectNode(GameObject target,string parent)
        {
            string id="object:"+QuantumGraphIdentity.ObjectId(target);
            if(Document.Nodes.TryGetValue(id,out var existing))
            {existing.Label=target.name;existing.SearchText=ComponentDiscoveryService.Normalize(existing.Label+" "+existing.Subtitle);if(parent!=null){Document.Reparent(existing,parent);Document.Link(parent,id,"Hierarchy",settings.OrbitColor);}return existing;}
            var node=Add(new QuantumGraphNode{Id=id,ParentId=parent,Target=target,Kind=QuantumGraphNodeKind.GameObject,Label=target.name,
                Subtitle="GameObject · "+target.GetComponents<Component>().Length+" components",Glyph="◇",CanExpand=true,Color=settings.OrbitColor});
            if(parent!=null)Document.Link(parent,node.Id,"Hierarchy",settings.OrbitColor);
            return node;
        }
        private void PopulateComponents(QuantumGraphNode owner)
        {
            var go=owner.Target as GameObject;if(go==null)return;
            var present=new HashSet<string>();
            string signature=string.Join(",",go.GetComponents<Component>().Select(c=>c!=null?c.GetEntityId().ToString():"missing"));
            if(!missingSignatures.TryGetValue(owner.Id,out var previous)||previous!=signature)
            {missingSignatures[owner.Id]=signature;missingEpochs[owner.Id]=QuantumGraphIdentity.SessionPrefix+Guid.NewGuid().ToString("N");}
            foreach(var entry in new ComponentDiscoveryService().Discover(go,null,false))
            {
                var category=QuantumClusterClassifier.Classify(entry.Component!=null?entry.Component.GetType():null,settings,out string tags);
                string clusterId=owner.Id+"/cluster/"+category;
                var cluster=Add(new QuantumGraphNode{Id=clusterId,ParentId=owner.Id,Kind=QuantumGraphNodeKind.Cluster,FamilyKey=category.ToString(),Label=QuantumClusterClassifier.Label(category),Subtitle="Components",
                    Glyph="◌",Expanded=true,Loaded=true,CanExpand=true,Color=QuantumClusterClassifier.Tint(category)});
                Document.Link(owner.Id,clusterId,"Category",cluster.Color);
                string id=entry.Component!=null?"component:"+QuantumGraphIdentity.ObjectId(entry.Component):missingEpochs[owner.Id]+"/missing/"+entry.Ordinal;
                var component=Add(new QuantumGraphNode{Id=id,ParentId=clusterId,ClusterId=clusterId,Kind=entry.Component!=null?QuantumGraphNodeKind.Component:QuantumGraphNodeKind.Missing,Target=entry.Component,
                    Label=entry.Name,Subtitle=entry.TypeName,Glyph=entry.Glyph??"</>",Icon=entry.Icon,Color=entry.CustomColor??cluster.Color,
                    CanExpand=entry.Component!=null,SearchText=tags+" "+category+" "+(entry.Component!=null?entry.Component.GetType().FullName:"")});
                present.Add(id);Document.Link(owner.Id,id,"Attached component",cluster.Color);
                Document.Link(clusterId,id,"Cluster member",cluster.Color);
                if(entry.Component!=null)dirtyCounts[id]=EditorUtility.GetDirtyCount(entry.Component);
                QueueOverlays(component);
            }
            foreach(var cluster in Document.DirectChildren(owner.Id).Where(n=>n.Kind==QuantumGraphNodeKind.Cluster).ToArray())
            {
                foreach(var node in Document.DirectChildren(cluster.Id).Where(n=>(n.Kind==QuantumGraphNodeKind.Component||n.Kind==QuantumGraphNodeKind.Missing)&&!present.Contains(n.Id)).ToArray())Document.RemoveBranch(node.Id);
                cluster.MemberCount=Document.DirectChildren(cluster.Id).Count(n=>n.Kind==QuantumGraphNodeKind.Component||n.Kind==QuantumGraphNodeKind.Missing);
                if(cluster.MemberCount==0)Document.RemoveBranch(cluster.Id);
                else cluster.Subtitle=cluster.MemberCount+" components";
            }
            owner.Loaded=true;owner.Label=go.name;owner.MemberCount=present.Count;owner.Subtitle="GameObject · "+present.Count+" components";
            owner.SearchText=ComponentDiscoveryService.Normalize(owner.Label+" "+owner.Subtitle);
            dirtyCounts[owner.Id]=EditorUtility.GetDirtyCount(go);
        }
        private QuantumGraphNode Asset(string path,string parent)
        {
            string id=QuantumGraphIdentity.AssetId(path);if(Document.Nodes.TryGetValue(id,out var old))
            {if(parent!=null){Document.Reparent(old,parent);Document.Link(parent,id,"Folder child",AssetInformation.Tint("Folder"));}return old;}
            bool folder=path=="Packages"||path=="Assets"||AssetDatabase.IsValidFolder(path);
            Type type=folder?null:AssetDatabase.GetMainAssetTypeAtPath(path);string kind=folder?"Folder":AssetInformation.Kind(type,path);
            var node=Add(new QuantumGraphNode{Id=id,ParentId=parent,Kind=folder?QuantumGraphNodeKind.Folder:QuantumGraphNodeKind.Asset,
                AssetPath=path,Label=folder?Path.GetFileName(path):Path.GetFileNameWithoutExtension(path),Subtitle=kind,Glyph=AssetInformation.Glyph(kind),Icon=AssetDatabase.GetCachedIcon(path) as Texture2D,Color=AssetInformation.Tint(kind),CanExpand=true});
            if(parent!=null)Document.Link(parent,id,"Folder child",AssetInformation.Tint("Folder"));
            if(!folder)QueueOverlays(node);
            return node;
        }
        private QuantumGraphNode EnsureReference(Object target,QuantumGraphNode source,string label,bool overlay)
        {
            string path=AssetDatabase.GetAssetPath(target);QuantumGraphNode node;
            if(Document.Project&&!string.IsNullOrEmpty(path)&&AssetDatabase.IsMainAsset(target))node=EnsureAssetPath(path);
            else if(target is GameObject go)
            {node=GameObjectNode(go,null);}
            else
            {
                string id=target is Component?"component:"+QuantumGraphIdentity.ObjectId(target):!string.IsNullOrEmpty(path)&&AssetDatabase.IsMainAsset(target)?QuantumGraphIdentity.AssetId(path):"object:"+QuantumGraphIdentity.ObjectId(target);
                if(!Document.Nodes.TryGetValue(id,out node))
                    node=Add(new QuantumGraphNode{Id=id,ParentId=Document.RootId,Target=target,Kind=target is Component?QuantumGraphNodeKind.Component:QuantumGraphNodeKind.Asset,
                        AssetPath=path,Label=target is Component c?ComponentAliasDatabase.DisplayName(c):target.name,Subtitle=target.GetType().Name,Color=new Color(.5f,.7f,1),Glyph="↗",CanExpand=true,ReferenceOnly=overlay});
            }
            if(node.ParentId==null&&node.Id!=Document.RootId&&node.Kind==QuantumGraphNodeKind.GameObject)
            {node.ParentId=Document.RootId;if(!Document.Children.TryGetValue(Document.RootId,out var children))Document.Children[Document.RootId]=children=new HashSet<string>();children.Add(node.Id);}
            Document.Link(source.Id,node.Id,label,new Color(.7f,.55f,1),overlay);return node;
        }
        private QuantumGraphNode EnsureAssetPath(string path)
        {
            string id=QuantumGraphIdentity.AssetId(path);if(Document.Nodes.TryGetValue(id,out var found))return found;
            string parent=Path.GetDirectoryName(path)?.Replace('\\','/');
            if(string.IsNullOrEmpty(parent))return Asset(path,Document.Root.Kind==QuantumGraphNodeKind.World?Document.RootId:null);
            if(Document.Root.AssetPath!=null&&(path.StartsWith(Document.Root.AssetPath+"/",StringComparison.Ordinal)||Document.Root.AssetPath==path))
            {var folder=parent==Document.Root.AssetPath?Document.Root:EnsureAssetPath(parent);folder.Expanded=true;return Asset(path,folder.Id);}
            if(Document.EntireProject&&(path.StartsWith("Assets/",StringComparison.Ordinal)||settings.GraphIncludePackages))
            {
                if(parent=="Assets"||parent=="Packages")
                {var root=Document.Nodes.Values.FirstOrDefault(n=>n.AssetPath==parent)??Asset(parent,Document.RootId);root.Expanded=true;return Asset(path,root.Id);}
                var folder=EnsureAssetPath(parent);folder.Expanded=true;return Asset(path,folder.Id);
            }
            var external=Add(new QuantumGraphNode{Id=Document.RootId+"/external",ParentId=Document.RootId,Kind=QuantumGraphNodeKind.Cluster,Label="External references",Subtitle="Outside this folder",Expanded=true,Loaded=true,CanExpand=true,Color=new Color(.7f,.55f,1)});
            Document.Link(Document.RootId,external.Id,"External reference group",external.Color);
            var node=Asset(path,null);node.ParentId=external.Id;node.ClusterId=external.Id;
            if(!Document.Children.TryGetValue(external.Id,out var items))Document.Children[external.Id]=items=new HashSet<string>();items.Add(node.Id);
            return node;
        }
        public Object Resolve(QuantumGraphNode node)
        {
            if(node==null||!node.Active||node.Kind==QuantumGraphNodeKind.Missing||node.Kind==QuantumGraphNodeKind.Cluster||node.Kind==QuantumGraphNodeKind.World)return null;
            if(node.Target!=null)return node.Target;
            if(node.Id.Contains("session:"))return null;
            int assetIdentity=node.Id.IndexOf("guid:",StringComparison.Ordinal);
            if(assetIdentity>=0)
            {
                var parts=node.Id.Substring(assetIdentity+5).Split(':');
                if(parts.Length==2&&long.TryParse(parts[1],out var local))
                    foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(parts[0])))
                        if(asset!=null&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string guid,out long candidate)&&candidate==local){node.Target=asset;return asset;}
                return null;
            }
            int marker=node.Id.IndexOf("GlobalObjectId_",StringComparison.Ordinal);
            if(marker>=0)
            {
                int end=node.Id.IndexOf(':',marker);string identity=end<0?node.Id.Substring(marker):node.Id.Substring(marker,end-marker);
                if(GlobalObjectId.TryParse(identity,out var id)){node.Target=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);return node.Target;}
            }
            if(!string.IsNullOrEmpty(node.AssetPath))node.Target=AssetDatabase.LoadMainAssetAtPath(node.AssetPath);
            return node.Target;
        }
        public void Expand(QuantumGraphNode node)
        {
            if(node==null||!node.CanExpand)return;
            node.Expanded=true;QueueExpansion(node);Document.Revision++;
        }
        public void Collapse(QuantumGraphNode node){if(node==null||node.Id==Document.RootId)return;node.Expanded=false;Document.Revision++;}
        private void QueueExpansion(QuantumGraphNode node,bool refresh=false)
        {
            if(node.Loading||(!refresh&&node.Loaded))return;
            if(node.Kind==QuantumGraphNodeKind.Cluster)return;
            node.Loading=true;jobs.Enqueue(new Job{Node=node,Iterator=ExpandRoutine(node).GetEnumerator()});
        }
        private IEnumerable<bool> ExpandRoutine(QuantumGraphNode node)
        {
            var seen=new HashSet<string>();
            if(node.Kind==QuantumGraphNodeKind.Folder)
            {
                if(Document.EntireProject)while(!index.Ready)yield return false;
                var paths=Document.EntireProject?index.Children(node.AssetPath):ImmediatePaths(node.AssetPath);
                foreach(string path in paths)
                {
                    var child=Asset(path,node.Id);seen.Add(child.Id);
                    if(child.Kind==QuantumGraphNodeKind.Asset&&settings.GraphProjectGrouping!=QuantumProjectGrouping.FolderHierarchy)
                    {
                        string group=child.Subtitle;
                        if(settings.GraphProjectGrouping==QuantumProjectGrouping.ScriptCategory&&child.Subtitle=="Script")
                        {
                            var script=AssetDatabase.LoadAssetAtPath<MonoScript>(path);var type=script!=null?script.GetClass():null;
                            group="Scripts · "+QuantumClusterClassifier.Classify(type,settings,out _);
                        }
                        string groupId=node.Id+"/type/"+group;
                        Add(new QuantumGraphNode{Id=groupId,ParentId=node.Id,Kind=QuantumGraphNodeKind.Cluster,FamilyKey=group,Label=group,Subtitle="Assets",Expanded=true,Loaded=true,CanExpand=true,Color=child.Color});child.ClusterId=groupId;
                        Document.Link(node.Id,groupId,"Asset classification",child.Color);
                        Document.Link(groupId,child.Id,"Cluster member",child.Color);
                    }
                    else child.ClusterId=null;
                    yield return true;
                }
                foreach(var child in Document.DirectChildren(node.Id).Where(n=>n.Kind!=QuantumGraphNodeKind.Cluster&&!seen.Contains(n.Id)).ToArray())Document.RemoveBranch(child.Id);
                foreach(var cluster in Document.DirectChildren(node.Id).Where(n=>n.Kind==QuantumGraphNodeKind.Cluster).ToArray())
                {
                    cluster.MemberCount=Document.DirectChildren(node.Id).Count(n=>n.ClusterId==cluster.Id);
                    if(cluster.MemberCount==0)Document.RemoveBranch(cluster.Id);else cluster.Subtitle=cluster.MemberCount+" assets";
                }
                node.MemberCount=seen.Count;node.Subtitle="Folder · "+seen.Count+" direct children";
            }
            else if(node.Kind==QuantumGraphNodeKind.GameObject){PopulateComponents(node);yield return true;}
            else
            {
                var owner=Resolve(node);if(owner==null)yield break;
                string path=node.Kind==QuantumGraphNodeKind.Parameter?node.PropertyPath:"";
                using(var serialized=new SerializedObject(owner))
                {
                    var property=string.IsNullOrEmpty(path)?null:serialized.FindProperty(path);
                    if(property!=null&&property.propertyType==SerializedPropertyType.ObjectReference)
                    {
                        if(property.objectReferenceValue!=null)EnsureReference(property.objectReferenceValue,node,path,false);
                        yield break;
                    }
                }
                foreach(var entry in SerializedParameterService.Enumerate(owner,path))
                {
                    string id="parameter:"+QuantumGraphIdentity.ObjectId(owner)+":"+entry.PropertyPath;
                    var child=Add(new QuantumGraphNode{Id=id,ParentId=node.Id,ClusterId=node.ClusterId,Kind=QuantumGraphNodeKind.Parameter,Target=owner,PropertyPath=entry.PropertyPath,
                        Label=entry.Name,Subtitle=entry.TypeName,Glyph=entry.Glyph,Color=entry.CustomColor??node.Color,CanExpand=entry.CanExpand||entry.TypeName=="ObjectReference"});
                    Document.Link(node.Id,id,"Serialized field",node.Color);seen.Add(child.Id);yield return true;
                }
                foreach(var child in Document.DirectChildren(node.Id).Where(n=>n.Kind==QuantumGraphNodeKind.Parameter&&!seen.Contains(n.Id)).ToArray())Document.RemoveBranch(child.Id);
                dirtyCounts[node.Id]=EditorUtility.GetDirtyCount(owner);
            }
        }
        private void QueueOverlays(QuantumGraphNode node)
        {
            if(settings.GraphOverlays==QuantumGraphOverlays.None||!overlaysQueued.Add(node.Id))return;
            RevealReferences(node,settings.GraphOverlays);
        }
        public void RevealReferences(QuantumGraphNode node,QuantumGraphOverlays kinds)
        {
            if(node==null)return;
            if(referenceRequests.TryGetValue(node.Id,out var previous))kinds|=previous;
            referenceRequests[node.Id]=kinds;
            var remaining=jobs.ToArray();jobs.Clear();
            foreach(var job in remaining){if(job.Overlay&&job.Node.Id==node.Id)job.Iterator.Dispose();else jobs.Enqueue(job);}
            foreach(var key in Document.Edges.Where(e=>e.Value.Overlay&&e.Value.From==node.Id).Select(e=>e.Key).ToArray())Document.Edges.Remove(key);
            jobs.Enqueue(new Job{Node=node,Iterator=References(node,kinds).GetEnumerator(),Overlay=true});
        }
        private IEnumerable<bool> References(QuantumGraphNode node,QuantumGraphOverlays kinds)
        {
            var owner=Resolve(node);if(owner==null)yield break;
            referenceVersions[node.Id]=EditorUtility.GetDirtyCount(owner);
            if((kinds&QuantumGraphOverlays.SerializedReferences)!=0)
            {
                using(var serialized=new SerializedObject(owner))
                {
                    var property=serialized.GetIterator();
                    while(property.Next(true))
                    {
                        if(property.propertyType==SerializedPropertyType.ObjectReference&&property.propertyPath!="m_Script"&&property.propertyPath!="m_GameObject")
                        {
                            if(property.objectReferenceValue!=null)EnsureReference(property.objectReferenceValue,node,property.propertyPath,true);
                            else if(property.objectReferenceInstanceIDValue!=0)
                            {
                                string missingId=QuantumGraphIdentity.SessionPrefix+node.Id+"/unavailable/"+property.propertyPath;
                                var missing=Add(new QuantumGraphNode{Id=missingId,ParentId=Document.RootId,Kind=QuantumGraphNodeKind.Missing,Label="Unavailable reference",Subtitle=property.propertyPath,PropertyPath=property.propertyPath,Color=new Color(1,.5f,.45f),Glyph="?",ReferenceOnly=true});
                                Document.Link(node.Id,missing.Id,property.propertyPath,missing.Color,true,QuantumGraphRelation.SerializedReference);
                            }
                        }
                        yield return true;
                    }
                }
                if(owner is Component component)
                    foreach(var required in ComponentCatalogService.Requirements(component.GetType()))
                    {var target=component.GetComponent(required);if(target!=null)EnsureReference(target,node,"RequireComponent("+required.Name+")",true);yield return true;}
            }
            if((kinds&QuantumGraphOverlays.PrefabSources)!=0)
            {
                var prefab=PrefabUtility.GetCorrespondingObjectFromSource(owner);if(prefab!=null)EnsureReference(prefab,node,"Prefab source",true);yield return true;
            }
            string path=AssetDatabase.GetAssetPath(owner);
            if((kinds&QuantumGraphOverlays.AssetDependencies)!=0&&!string.IsNullOrEmpty(path))
                foreach(var dependency in AssetDatabase.GetDependencies(path,false))
                {
                    if(dependency==path)continue;
                    var target=Document.Project?EnsureAssetPath(dependency):Asset(dependency,null);
                    Document.Link(node.Id,target.Id,"Asset dependency",new Color(.4f,.7f,1),true);yield return true;
                }
        }
        public void RevealHierarchy(QuantumGraphNode node)
        {
            if(!(node.Target is GameObject go))return;
            hierarchyRevealed.Add(node.Id);
            foreach(var key in Document.Edges.Where(e=>e.Value.From==node.Id&&e.Value.Label=="Hierarchy").Select(e=>e.Key).ToArray())Document.Edges.Remove(key);
            var seen=new HashSet<string>();foreach(Transform child in go.transform)seen.Add(GameObjectNode(child.gameObject,node.Id).Id);
            foreach(var previous in Document.DirectChildren(node.Id).Where(n=>n.Kind==QuantumGraphNodeKind.GameObject&&!seen.Contains(n.Id)).ToArray())Document.Reparent(previous,null);
            node.Expanded=true;Document.Revision++;
        }
        private void RestoreExpanded()
        {foreach(var node in Document.Nodes.Values.Where(n=>n.Expanded&&!n.Loaded&&!n.Loading).ToArray())QueueExpansion(node);}
        public bool Tick()
        {
            if(Document==null||jobs.Count==0)return false;
            bool changed=false;
            if(Document.EntireProject&&!index.Ready)index.Tick(Math.Max(256,settings.GraphItemsPerTick*16));
            var watch=System.Diagnostics.Stopwatch.StartNew();int count=0;
            while(jobs.Count>0&&count++<settings.GraphItemsPerTick&&watch.Elapsed.TotalMilliseconds<settings.GraphWorkMilliseconds)
            {
                var job=jobs.Peek();
                try
                {
                    if(!Document.Nodes.ContainsKey(job.Node.Id)||!job.Iterator.MoveNext())
                    {
                        job.Iterator.Dispose();jobs.Dequeue();if(!job.Overlay){job.Node.Loaded=true;job.Node.Loading=false;}
                        changed=true;RestoreExpanded();continue;
                    }
                    changed|=job.Iterator.Current;
                }
                catch(Exception ex) when(ex is MissingReferenceException||ex is ArgumentException||ex is InvalidOperationException)
                {failure="Branch changed while expanding; Refresh to retry. "+ex.Message;job.Iterator.Dispose();jobs.Dequeue();job.Node.Loading=false;job.Node.Loaded=true;}
            }
            if(changed)Document.Revision++;return changed;
        }
        public void Refresh(bool projectChanged=false)
        {
            if(Document==null)return;failure="";
            if(Document.RootId=="hierarchy:scene-roots")
            {
                var roots=new HierarchyOrbitalProvider().Discover(new WheelScope{Kind=WheelScopeKind.SceneRoots},null,settings);
                var present=new HashSet<string>();foreach(var entry in roots)present.Add(GameObjectNode((GameObject)entry.Target,Document.RootId).Id);
                foreach(var previous in Document.DirectChildren(Document.RootId).Where(n=>n.Kind==QuantumGraphNodeKind.GameObject&&!present.Contains(n.Id)).ToArray())Document.RemoveBranch(previous.Id);
            }
            if(projectChanged)
            {
                index.Invalidate();
                foreach(var asset in Document.Nodes.Values.Where(n=>!string.IsNullOrEmpty(n.AssetPath)&&n.Id.StartsWith("asset:")).ToArray())
                {
                    string moved=AssetDatabase.GUIDToAssetPath(asset.Id.Substring(6));
                    if(string.IsNullOrEmpty(moved)){if(asset.AssetPath!="Assets"&&asset.AssetPath!="Packages"&&asset.Id!=Document.RootId)Document.RemoveBranch(asset.Id);continue;}
                    asset.AssetPath=moved;asset.Label=asset.Kind==QuantumGraphNodeKind.Folder?Path.GetFileName(moved):Path.GetFileNameWithoutExtension(moved);
                    asset.SearchText=ComponentDiscoveryService.Normalize(asset.Label+" "+asset.Subtitle+" "+moved);
                    string parentPath=Path.GetDirectoryName(moved)?.Replace('\\','/');
                    if(parentPath!=null&&Document.Nodes.TryGetValue(QuantumGraphIdentity.AssetId(parentPath),out var parent))
                    {Document.Reparent(asset,parent.Id);if(asset.Id!=Document.RootId)Document.Link(parent.Id,asset.Id,"Folder child",AssetInformation.Tint("Folder"));}
                }
            }
            foreach(var node in Document.Nodes.Values.ToArray())
            {
                if(!Document.Nodes.ContainsKey(node.Id))continue;
                if(node.Kind==QuantumGraphNodeKind.GameObject&&node.Loaded&&node.Target!=null)PopulateComponents(node);
                else if(node.Kind==QuantumGraphNodeKind.Folder&&node.Loaded&&(projectChanged||grouping!=settings.GraphProjectGrouping))QueueExpansion(node,true);
                else if(node.Loaded&&node.Expanded&&node.Target!=null&&node.Kind!=QuantumGraphNodeKind.Cluster)
                {
                    int count=EditorUtility.GetDirtyCount(node.Target);
                    if(!dirtyCounts.TryGetValue(node.Id,out int last)||last!=count)QueueExpansion(node,true);
                }
            }
            if(overlays!=settings.GraphOverlays)
            {
                foreach(var key in Document.Edges.Where(e=>e.Value.Overlay).Select(e=>e.Key).ToArray())Document.Edges.Remove(key);
                overlaysQueued.Clear();referenceRequests.Clear();referenceVersions.Clear();
                var remaining=jobs.ToArray();jobs.Clear();foreach(var job in remaining){if(job.Overlay)job.Iterator.Dispose();else jobs.Enqueue(job);}
                overlays=settings.GraphOverlays;
                foreach(var node in Document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Component||n.Kind==QuantumGraphNodeKind.Asset).ToArray())QueueOverlays(node);
            }
            foreach(var id in hierarchyRevealed.ToArray())if(Document.Nodes.TryGetValue(id,out var owner)&&owner.Target!=null)
            {bool expanded=owner.Expanded;RevealHierarchy(owner);owner.Expanded=expanded;}
            foreach(var dead in Document.Nodes.Values.Where(n=>n.Id!=Document.RootId&&n.Kind==QuantumGraphNodeKind.GameObject&&n.Target==null).ToArray())Document.RemoveBranch(dead.Id);
            foreach(var request in referenceRequests.ToArray())
                if(Document.Nodes.TryGetValue(request.Key,out var source)&&source.Target!=null&&referenceVersions.TryGetValue(source.Id,out int version)&&version!=EditorUtility.GetDirtyCount(source.Target))
                    RevealReferences(source,request.Value);
            grouping=settings.GraphProjectGrouping;Document.Revision++;RestoreExpanded();
        }
        public static IEnumerable<string> ImmediatePaths(string folder)
        {
            string directory=folder;
            if(folder.StartsWith("Packages/",StringComparison.Ordinal))
            {
                var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(folder);
                if(package!=null)directory=package.resolvedPath+folder.Substring(package.assetPath.Length);
            }
            if(!Directory.Exists(directory))yield break;
            foreach(var path in Directory.EnumerateFileSystemEntries(directory))
            {
                if(path.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))continue;
                string asset=folder+"/"+Path.GetFileName(path);
                if(!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(asset)))yield return asset;
            }
        }
        public void CheckPending(bool projectChanged=false)
        {
            if(Document==null)return;
            pendingProject|=projectChanged;pending.Clear();
            foreach(var node in Document.Nodes.Values.ToArray())
            {
                if((node.Kind==QuantumGraphNodeKind.Component||node.Kind==QuantumGraphNodeKind.GameObject||node.Kind==QuantumGraphNodeKind.Parameter)&&node.Target==null)
                {node.Active=false;node.Opacity=0;pending.Add("removed:"+node.Id);continue;}
                if(node.Kind==QuantumGraphNodeKind.GameObject&&node.Loaded&&node.Target is GameObject owner)
                {
                    var components=owner.GetComponents<Component>();
                    var current=components.Where(c=>c!=null).Select(c=>"component:"+QuantumGraphIdentity.ObjectId(c)).ToHashSet();
                    foreach(var id in current)if(!Document.Nodes.ContainsKey(id))pending.Add("added:"+id);
                    if(node.Label!=owner.name)pending.Add("renamed:"+node.Id);
                    int missing=components.Count(c=>c==null),known=Document.Descendants(node.Id).Count(n=>n.Kind==QuantumGraphNodeKind.Missing);
                    for(int i=0;i<Math.Abs(missing-known);i++)pending.Add("missing:"+node.Id+":"+i);
                }
                if(hierarchyRevealed.Contains(node.Id)&&node.Target is GameObject parentObject)
                {
                    var current=parentObject.transform.Cast<Transform>().Select(t=>"object:"+QuantumGraphIdentity.ObjectId(t.gameObject)).ToHashSet();
                    var known=Document.DirectChildren(node.Id).Where(n=>n.Kind==QuantumGraphNodeKind.GameObject).Select(n=>n.Id).ToHashSet();
                    foreach(var id in current)if(!known.Contains(id))pending.Add("added:"+id);
                    foreach(var id in known)if(!current.Contains(id)){pending.Add("removed:"+id);Document.Nodes[id].Active=false;}
                }
                if(pendingProject&&node.Id.StartsWith("asset:",StringComparison.Ordinal)&&node.AssetPath!="Assets"&&node.AssetPath!="Packages")
                {
                    string path=AssetDatabase.GUIDToAssetPath(node.Id.Substring(6));
                    if(string.IsNullOrEmpty(path)){node.Active=false;pending.Add("removed:"+node.Id);}
                    else if(path!=node.AssetPath)pending.Add("renamed:"+node.Id);
                }
                if(pendingProject&&node.Kind==QuantumGraphNodeKind.Folder&&node.Loaded)
                {
                    var current=ImmediatePaths(node.AssetPath).Select(QuantumGraphIdentity.AssetId).ToHashSet();
                    var previous=Document.DirectChildren(node.Id).Where(n=>n.Kind!=QuantumGraphNodeKind.Cluster).Select(n=>n.Id).ToHashSet();
                    foreach(var id in current)if(!previous.Contains(id))pending.Add("added:"+id);
                    foreach(var id in previous)if(!current.Contains(id)){pending.Add("removed:"+id);if(Document.Nodes.TryGetValue(id,out var removed)){removed.Active=false;removed.Opacity=0;}}
                }
                if(node.Target!=null&&dirtyCounts.TryGetValue(node.Id,out int count)&&count!=EditorUtility.GetDirtyCount(node.Target))pending.Add("edited:"+node.Id);
                if(node.Target!=null&&referenceVersions.TryGetValue(node.Id,out int referenceVersion)&&referenceVersion!=EditorUtility.GetDirtyCount(node.Target))pending.Add("edited:"+node.Id);
            }
            foreach(var edge in Document.Edges.Values)
                if(Document.Nodes.TryGetValue(edge.From,out var from)&&!from.Active||Document.Nodes.TryGetValue(edge.To,out var to)&&!to.Active)Document.Revision++;
            ChangeSummary=pending.Count==0?"":pending.Count+" pending object changes (added / removed / edited). Apply Updates to accept.";
        }
        public void ApplyPending()
        {
            int count=pending.Count;Refresh(pendingProject);pending.Clear();pendingProject=false;
            ChangeSummary=count==0?"Snapshot refreshed":count+" object changes applied";
        }
        public void Configure(InspectorWheelSettings value){settings=value;}
        private void DisposeJobs(){while(jobs.Count>0)jobs.Dequeue().Iterator.Dispose();}
        public void Dispose(){DisposeJobs();}
    }
}
