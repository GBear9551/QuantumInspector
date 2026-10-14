using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public sealed class ResearchLayoutManager
    {
        public readonly ScrollView Root=new ScrollView();
        private readonly Dictionary<ResearchLayouts,VisualElement> panels=new Dictionary<ResearchLayouts,VisualElement>();
        private readonly Action<Object> navigate;
        private readonly Action<Object> bookmark;
        private readonly Action scanIncoming;
        private readonly Action<InspectorWheelSession.Bookmark> loadBookmark;
        private readonly Action<int> selectHistory;
        private InspectorWheelSettings settings;
        private ResearchLayouts visibleLayouts;
        private GameObject target;
        private Component selected;
        private ReferenceGraphService graph;
        private WheelNavigationHistory history;
        private WheelScope scope;
        private RecursiveDetailsView recursiveDetails;
        private string detailsKey;
        public readonly ComponentActionContext Actions=new ComponentActionContext();
        public System.Action<Type> AddComponent;
        public System.Action ConfirmRemoval,CancelAction;
        public ResearchLayoutManager(Action<Object> navigate,Action<Object> bookmark,Action scanIncoming,Action<InspectorWheelSession.Bookmark> loadBookmark,Action<int> selectHistory)
        {
            this.navigate=navigate;this.bookmark=bookmark;this.scanIncoming=scanIncoming;this.loadBookmark=loadBookmark;this.selectHistory=selectHistory;
            Root.AddToClassList("quantum-panels");
            Root.horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            foreach(ResearchLayouts layout in Enum.GetValues(typeof(ResearchLayouts)))
            {
                if(layout==ResearchLayouts.None)continue;
                var panel=new VisualElement();panel.AddToClassList("research-panel");panels.Add(layout,panel);Root.Add(panel);
            }
        }
        public void Configure(InspectorWheelSettings value,ResearchLayouts? layoutOverride=null)
        {
            settings=value;
            visibleLayouts=layoutOverride??settings.Layouts;
            Root.EnableInClassList("quantum-hidden",visibleLayouts==ResearchLayouts.None);
            Root.EnableInClassList("wide",settings.WideResearchPanel);
            foreach(var pair in panels)pair.Value.EnableInClassList("quantum-hidden",(visibleLayouts&pair.Key)==0);
        }
        public void Present(WheelScope scope,ReferenceGraphService graph,WheelNavigationHistory history)
        {
            this.scope=scope;target=scope.Object;selected=scope.Component;this.graph=graph;this.history=history;
            Overview();Details();Relationships();Runtime();History();
        }
        private VisualElement Begin(ResearchLayouts layout,string title)
        {
            var panel=panels[layout];panel.Clear();
            var label=new Label(title.ToUpperInvariant());label.AddToClassList("research-title");panel.Add(label);
            return panel;
        }
        private static Label Text(VisualElement parent,string text,string className="research-line")
        {var label=new Label(text);label.AddToClassList(className);parent.Add(label);return label;}
        private static Button Action(VisualElement parent,string text,System.Action action)
        {var button=new Button(action){text=text};parent.Add(button);return button;}
        private void Overview()
        {
            if((visibleLayouts&ResearchLayouts.Overview)==0)return;
            var panel=Begin(ResearchLayouts.Overview,"Object overview");
            if(scope.Kind==WheelScopeKind.ProjectFolder){Text(panel,scope.Breadcrumb);Text(panel,"Choose a folder to enter it or an asset to see its information.");return;}
            if(target==null){Text(panel,"Select a GameObject to begin.");return;}
            Text(panel,target.name,"research-value");
            Text(panel,"Tag: "+target.tag+"   Layer: "+LayerMask.LayerToName(target.layer)+" ("+target.layer+")");
            Text(panel,"Active self: "+target.activeSelf+"   In hierarchy: "+target.activeInHierarchy);
            Text(panel,"Scene: "+(string.IsNullOrEmpty(target.scene.path)?"Unsaved / prefab asset":target.scene.path));
            Text(panel,"Prefab: "+PrefabUtility.GetPrefabAssetType(target));
            Text(panel,"Entity ID: "+target.GetEntityId());
            var row=new VisualElement();row.AddToClassList("research-actions");panel.Add(row);
            Action(row,"Ping",()=>EditorGUIUtility.PingObject(target));
            Action(row,"Frame",()=>{if(SceneView.lastActiveSceneView!=null){Selection.activeGameObject=target;SceneView.lastActiveSceneView.FrameSelected();}});
            Action(row,"Bookmark",()=>bookmark(target));
            Action(row,"Copy ID",()=>EditorGUIUtility.systemCopyBuffer=GlobalObjectId.GetGlobalObjectIdSlow(target).ToString());
        }
        private void Details()
        {
            string key=scope.Kind+"|"+(selected!=null?selected.GetEntityId().ToString():"")+"|"+scope.PropertyPath+"|"+scope.SelectedProperty+"|"+
                (scope.Asset!=null?scope.Asset.GetEntityId().ToString():"")+"|"+(target!=null?target.GetEntityId().ToString():"")+"|"+Actions.Revision+"|"+visibleLayouts;
            if(detailsKey==key)return;
            detailsKey=key;recursiveDetails?.Dispose();recursiveDetails=null;
            var panel=Begin(ResearchLayouts.Details,scope.Kind==WheelScopeKind.ProjectFolder?"Asset details":"Component details");
            if((visibleLayouts&ResearchLayouts.Details)==0)return;
            if(scope.Kind==WheelScopeKind.ProjectFolder){AssetDetails(panel);return;}
            recursiveDetails=new RecursiveDetailsView(panel);
            recursiveDetails.Build(scope,Actions,AddComponent,ConfirmRemoval,CancelAction,navigate,bookmark);
        }
        private void AssetDetails(VisualElement panel)
        {
            var asset=scope.Asset;
            if(asset==null){Text(panel,"Select an asset node for its type, preview and properties. Folder nodes enter the folder.");return;}
            Text(panel,asset.name,"research-value");
            foreach(string line in AssetInformation.Describe(asset))Text(panel,line);
            Texture preview=asset is Texture texture?texture:AssetPreview.GetAssetPreview(asset);
            if(preview!=null){var image=new Image{image=preview,scaleMode=ScaleMode.ScaleToFit};image.AddToClassList("asset-preview");panel.Add(image);}
            var row=new VisualElement();row.AddToClassList("research-actions");panel.Add(row);
            Action(row,"Unity Inspector",()=>{Selection.activeObject=asset;EditorGUIUtility.PingObject(asset);EditorApplication.ExecuteMenuItem("Window/General/Inspector");});
            Action(row,"Ping in Project",()=>EditorGUIUtility.PingObject(asset));
            Action(row,"Bookmark asset",()=>bookmark(asset));
            if(asset is GameObject prefab)Action(panel,"Inspect prefab components",()=>navigate(prefab));
            if(asset is SceneAsset)
            {
                var open=Action(panel,"Open scene",()=>
                {
                    if(!InspectorSceneNavigation.TryOpen(AssetDatabase.GetAssetPath(asset),out string failure)&&failure!=null)
                        EditorUtility.DisplayDialog("Open scene",failure,"OK");
                });
                open.tooltip="Switch to this scene, prompting to save modified scenes first.";
                open.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            }
            else Action(panel,"Open asset in Unity",()=>AssetDatabase.OpenAsset(asset));
            Text(panel,"GUID: "+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)));
        }
        public void Relationships()
        {
            if(graph==null||(visibleLayouts&(ResearchLayouts.Graph|ResearchLayouts.References))==0)return;
            bool projectScope=scope.Kind==WheelScopeKind.ProjectFolder;
            var discovered=projectScope?(scope.Asset!=null?graph.Outgoing(scope.Asset):new List<ResearchRelationship>()):graph.ForObject(target,selected);
            var all=discovered.Where(e=>(settings.Relationships&e.Kind)!=0).ToList();
            var graphPanel=Begin(ResearchLayouts.Graph,"Relationship preview");
            Text(graphPanel,"→ outgoing direction · cyan hierarchy · violet serialized · blue assets · green requirements · amber events · pink instrumented");
            var drawing=new RelationshipGraphView(target,selected,all,projectScope&&scope.Asset!=null?scope.Asset.name:null);graphPanel.Add(drawing);
            Text(graphPanel,"This compact preview shows up to 8 destinations. Use Clustered Graph above the wheel for unlimited exploration, or Reference Explorer for exact property paths.");
            var references=Begin(ResearchLayouts.References,"Reference explorer");
            Text(references,projectScope?"Source: "+(scope.Asset!=null?scope.Asset.name:"select an asset"):selected!=null?"Source: "+ComponentAliasDatabase.DisplayName(selected):"Source: all attached components");
            Action(references,"Scan incoming · loaded scenes",scanIncoming).SetEnabled(!projectScope&&(scope.Kind==WheelScopeKind.Components||scope.IsComponentFocus));
            Text(references,graph.ScanStatus);
            if(graph.Truncated)Text(references,"Some objects exceeded the property scan limit. Increase Max Properties Per Object in settings.","quantum-empty");
            Text(references,$"OUTGOING · {all.Count}");
            AddLinks(references,all,false);
            Text(references,$"INCOMING · {graph.Incoming.Count}");
            AddLinks(references,graph.Incoming,true);
            Text(references,"Serialized fields and persistent UnityEvent targets are discoverable. Nonserialized runtime links require an explicit provider. Incoming scans cover loaded scene objects, not the entire project.");
        }
        private void AddLinks(VisualElement parent,IReadOnlyList<ResearchRelationship> links,bool incoming)
        {
            if(links.Count==0){Text(parent,"No discovered links.");return;}
            foreach(var link in links.Take(100))
            {
                var row=new VisualElement();row.AddToClassList("research-link");row.AddToClassList("relationship-"+link.Kind.ToString().ToLowerInvariant());parent.Add(row);
                Text(row,link.Kind+" · "+ObjectLabel(link.Source)+" → "+ObjectLabel(link.Target));
                Text(row,link.Path);
                Object destination=incoming?link.Source:link.Target;
                var action=Action(row,"Ping · double-click to explore",()=>{if(destination!=null)EditorGUIUtility.PingObject(destination);});
                action.RegisterCallback<ClickEvent>(e=>{if(e.clickCount==2&&destination!=null)navigate(destination);});
                Action(row,"Explore",()=>navigate(destination));
            }
            if(links.Count>100)Text(parent,$"Showing first 100 of {links.Count} links. Select a component to narrow the source.");
        }
        public void Runtime()
        {
            if((visibleLayouts&ResearchLayouts.Runtime)==0)return;
            var panel=Begin(ResearchLayouts.Runtime,"Runtime research");
            if(target==null){Text(panel,"No inspected object.");return;}
            Text(panel,EditorApplication.isPlaying?"LIVE · PLAY MODE":"EDIT MODE · runtime sampling paused","research-value");
            Text(panel,"Editor uptime: "+EditorApplication.timeSinceStartup.ToString("F1")+" s");
            if(!EditorApplication.isPlaying)return;
            Text(panel,"Time scale: "+Time.timeScale+"   Frame: "+Time.frameCount);
            Text(panel,"World position: "+target.transform.position.ToString("F3"));
            if(InspectorWheelExtensions.RuntimeProviders.Count==0)
                Text(panel,"No instrumentation providers registered. Add an IInspectorWheelRuntimeProvider in an Editor assembly to expose measurements and proven runtime links.");
            foreach(var provider in InspectorWheelExtensions.RuntimeProviders)
            {
                Text(panel,provider.Name,"research-value");
                try {foreach(var value in (provider.Sample(target)??Array.Empty<ResearchMeasurement>()).Take(100))Text(panel,value.Name+": "+value.Value);}
                catch(Exception ex){Text(panel,"Provider unavailable: "+ex.Message,"quantum-empty");}
            }
        }
        public void History()
        {
            if((visibleLayouts&ResearchLayouts.History)==0)return;
            var panel=Begin(ResearchLayouts.History,"Bookmarks & history");
            Text(panel,"BOOKMARKS · load into wheel. Unopened scenes replace the current scene after a save prompt. Use ← to return to available inspection locations.");
            foreach(var saved in InspectorWheelSession.instance.Bookmarks.ToArray())
            {
                var row=new VisualElement();row.AddToClassList("research-actions");panel.Add(row);
                Action(row,InspectorWheelSession.Label(saved),()=>loadBookmark(saved));
                Action(row,"×",()=>{InspectorWheelSession.instance.Remove(saved);History();});
            }
            Text(panel,"TAB HISTORY · exploration locations; rotation stays independent");
            if(history==null)return;
            for(int i=history.Entries.Count-1;i>=0;i--)
            {var item=history.Entries[i];int index=i;if(item.Available)Action(panel,(i==history.Cursor?"● ":"")+item.Title+" · "+item.EntryLabel,()=>selectHistory(index));}
        }
        public void Dispose() {recursiveDetails?.Dispose();recursiveDetails=null;detailsKey=null;Root.Clear();}
        public static string ObjectLabel(Object item)=>item==null?"<missing>":(item is Component c?ComponentAliasDatabase.DisplayName(c)+" · "+c.gameObject.name:item.name)+" ["+item.GetType().Name+"]";
        public static Color LinkColor(RelationshipKinds kind)=>kind switch {
            RelationshipKinds.Hierarchy=>new Color(.25f,.85f,.95f),RelationshipKinds.Dependency=>new Color(.5f,.9f,.6f),
            RelationshipKinds.Event=>new Color(1,.73f,.3f),RelationshipKinds.Instrumented=>new Color(1,.4f,.8f),
            RelationshipKinds.ScriptableObject=>new Color(.3f,.6f,1),
            _=>new Color(.65f,.5f,1)};
    }

    internal sealed class RelationshipGraphView:VisualElement
    {
        private readonly string centerName;
        private readonly List<ResearchRelationship> edges;
        public RelationshipGraphView(GameObject target,Component selected,List<ResearchRelationship> edges,string contextName=null)
        {
            centerName=contextName??(selected!=null?ComponentAliasDatabase.DisplayName(selected):target!=null?target.name:"No object");
            this.edges=edges;AddToClassList("research-graph");generateVisualContent+=Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            if(contentRect.width<1)return;
            var p=context.painter2D;Vector2 center=new Vector2(contentRect.width*.5f,105);
            int count=Math.Min(8,edges.Count);
            for(int i=0;i<count;i++)
            {
                float angle=(i*360f/Mathf.Max(1,count)-90)*Mathf.Deg2Rad;
                Vector2 end=center+new Vector2(Mathf.Cos(angle)*contentRect.width*.34f,Mathf.Sin(angle)*75);
                var tint=ResearchLayoutManager.LinkColor(edges[i].Kind);
                p.strokeColor=tint;p.lineWidth=1.3f;p.BeginPath();p.MoveTo(center);p.LineTo(end);p.Stroke();
                Vector2 d=(end-center).normalized,tip=end-d*12;
                p.BeginPath();p.MoveTo(tip-d*7+new Vector2(-d.y,d.x)*4);p.LineTo(tip);p.LineTo(tip-d*7-new Vector2(-d.y,d.x)*4);p.Stroke();
                p.fillColor=new Color(.08f,.14f,.24f);p.BeginPath();p.Arc(end,10,Angle.Degrees(0),Angle.Degrees(360));p.Fill();p.Stroke();
                string label=edges[i].Target is Component c?ComponentAliasDatabase.DisplayName(c):edges[i].Target!=null?edges[i].Target.name:"missing";if(label.Length>12)label=label.Substring(0,11)+"…";
                context.DrawText(label,end+new Vector2(-30,12),9,tint,null);
            }
            p.fillColor=new Color(.05f,.18f,.26f);p.strokeColor=new Color(.3f,.9f,1);p.BeginPath();p.Arc(center,30,Angle.Degrees(0),Angle.Degrees(360));p.Fill();p.Stroke();
            string name=centerName.Length>15?centerName.Substring(0,14)+"…":centerName;
            context.DrawText(name,center+new Vector2(-40,-5),10,Color.white,null);
        }
    }
}
