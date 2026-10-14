using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public static class QuantumGraphConstellationVerification
    {
        private static int assertions;
        public static bool AllocationCounterSupported()
        {long before=GC.GetAllocatedBytesForCurrentThread();var control=new byte[32768];long after=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(control);return after-before>=32768;}
        private static void Check(bool value,string message){assertions++;if(!value)throw new InvalidOperationException(message);}
        private static QuantumGraphDocument Synthetic(int count,bool reverse=false)
        {
            var graph=new QuantumGraphDocument{Key=QuantumGraphIdentity.SessionPrefix+"synthetic",RootId="owner",Generation=17};
            graph.Upsert(new QuantumGraphNode{Id="owner",Label="Owner",Kind=QuantumGraphNodeKind.GameObject,Expanded=true,Positioned=true});
            for(int i=0;i<7;i++)graph.Upsert(new QuantumGraphNode{Id="family"+i,FamilyKey=((QuantumClusterCategory)i).ToString(),Label="Family "+i,ParentId="owner",Kind=QuantumGraphNodeKind.Cluster,Expanded=true,Loaded=true});
            IEnumerable<int> order=Enumerable.Range(0,count);if(reverse)order=order.Reverse();
            foreach(int i in order)
            {
                string id="member"+i.ToString("D5"),family="family"+(i%7);
                graph.Upsert(new QuantumGraphNode{Id=id,Label="Component instance "+i,ParentId=family,ClusterId=family,Kind=QuantumGraphNodeKind.Component});
                graph.Link("owner",id,"Attached component",Color.cyan);graph.Link(family,id,"Cluster member",Color.cyan);
                if(i%4==0&&i>0)graph.Link(id,"member"+(i-1).ToString("D5"),"fixture.reference",Color.magenta,true);
            }
            return graph;
        }
        private static void LayoutAndMotion(InspectorWheelSettings settings)
        {
            settings.GraphInvestigationLock=false;
            var a=Synthetic(42);var b=Synthetic(42,true);var solver=new QuantumGraphLayoutEngine();
            solver.Arrange(a,settings);new QuantumGraphLayoutEngine().Arrange(b,settings);
            Check(a.Nodes.All(p=>p.Value.Position==b.Nodes[p.Key].Position),"Equal input produced different targets when enumeration order changed.");
            var prior=a.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);var family=a.Nodes["family0"];float radius=family.Radius,distance=family.Position.magnitude;
            for(int i=42;i<70;i++)a.Upsert(new QuantumGraphNode{Id="growth"+i,Label="Added instance "+i,Kind=QuantumGraphNodeKind.Component,ParentId=family.Id,ClusterId=family.Id});
            solver.Arrange(a,settings);
            Check(family.Radius>radius&&family.Position.magnitude>distance,"Growing family failed to increase footprint and owner spacing.");
            Check(a.Nodes.Where(p=>p.Value.Kind==QuantumGraphNodeKind.Cluster&&p.Key!=family.Id).All(p=>p.Value.Position==prior[p.Key]),"Growing one family shuffled an unaffected anchor.");
            var pinned=a.Nodes["member00000"];pinned.Pinned=true;Vector2 pin=pinned.Position;
            solver.Arrange(a,settings,true);Check(pinned.Position==pin,"Arrange moved a pinned component.");
            settings.GraphInvestigationLock=true;prior=a.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);
            a.Upsert(new QuantumGraphNode{Id="locked-addition",Label="New local overflow",Kind=QuantumGraphNodeKind.Component,ParentId=family.Id,ClusterId=family.Id});solver.Arrange(a,settings);
            Check(prior.All(p=>a.Nodes[p.Key].Position==p.Value),"Locked update moved existing positions.");
            var motion=new QuantumGraphMotion();motion.Commit(a,1,QuantumGraphMotionMode.Off);
            var member=a.Nodes["member00007"];Vector2 original=member.Position;
            member.Position+=new Vector2(170,90);motion.Commit(a,2,QuantumGraphMotionMode.Full);motion.Sample(2.073,a.Generation);
            Vector2 displayed=member.DrawPosition;member.Position+=new Vector2(-40,150);motion.Commit(a,2.073,QuantumGraphMotionMode.Full);
            Check(member.DrawPosition==displayed,"Interrupted transition restarted from an obsolete origin.");
            var target=member.Position;float last=Vector2.Distance(displayed,target);
            foreach(double time in new[]{2.079,2.113,2.2,2.319,2.8})
            {motion.Sample(time,a.Generation);float next=Vector2.Distance(member.DrawPosition,target);Check(next<=last+.001f,"Irregular elapsed time caused overshoot.");last=next;}
            Check(member.DrawPosition==target&&!motion.Active,"Motion failed to finish exactly at target.");
            family.Expanded=false;motion.Commit(a,3,QuantumGraphMotionMode.Full);motion.Sample(3.4,a.Generation);
            Check(member.Position==target&&member.Opacity==0,"Collapse lost saved member position or visibility.");
            Check(pinned.Position==pin&&pinned.DrawPosition==pin,"Pinned member moved during collapse.");
            family.Expanded=true;motion.Commit(a,4,QuantumGraphMotionMode.Full);motion.Sample(4.4,a.Generation);
            Check(member.DrawPosition==target&&member.Opacity==1,"Reopen lost expanded position.");
            foreach(var mode in new[]{QuantumGraphMotionMode.Reduced,QuantumGraphMotionMode.Off})
            {member.Position+=Vector2.one;motion.Commit(a,5,mode);Check(!motion.Active&&member.DrawPosition==member.Position,"Non-spatial motion preference animated layout.");}
            member.Position+=Vector2.one;motion.Commit(a,6,QuantumGraphMotionMode.Full);displayed=member.DrawPosition;a.Generation++;
            motion.Sample(7,a.Generation);Check(!motion.Active&&member.DrawPosition==displayed,"Stale generation overwrote displayed state.");
        }
        private static void IdentityAndLock(InspectorWheelSettings settings)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var provider=new QuantumGraphDataProvider();
            try
            {
                var go=ObjectFactory.CreateGameObject(scene,HideFlags.None,"Duplicate fixture");
                var first=go.AddComponent<AudioSource>();var second=go.AddComponent<AudioSource>();
                var doc=provider.Open(new WheelScope{Kind=WheelScopeKind.Components,Object=go},false,settings);
                var original=doc.Nodes.Values.Single(n=>n.Target==second);string id=original.Id;original.Pinned=true;original.Position=new Vector2(23,71);
                Check(doc.Nodes.Values.Count(n=>n.Target is AudioSource)==2,"Repeated component instances were merged.");
                go.name="Renamed duplicate fixture";var added=go.AddComponent<AudioSource>();UnityEditorInternal.ComponentUtility.MoveComponentUp(added);
                Object.DestroyImmediate(first);provider.CheckPending();
                Check(provider.PendingChanges>=2,"Locked topology failed to report additions/removals.");
                Check(!doc.Nodes.Values.Any(n=>n.Target==added),"Locked discovery accepted a new member automatically.");
                Check(doc.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Component&&n.Target==null).All(n=>!n.Active),"Deleted targets remained interactive.");
                provider.ApplyPending();Check(doc.Nodes[id].Target==second&&doc.Nodes[id].Position==new Vector2(23,71)&&doc.Nodes[id].Pinned,"Rename, insertion or removal transferred presentation to another instance.");
                Check(doc.Nodes.Values.Single(n=>n.Target==added).Id!=id,"New component inherited an existing identity.");
                Check(doc.Edges.Values.Where(e=>e.Relation==QuantumGraphRelation.Ownership).All(e=>doc.Nodes[e.From].Kind==QuantumGraphNodeKind.GameObject),"Presentation clusters claimed actual ownership.");
                Check(QuantumClusterClassifier.Classify(typeof(Transform),settings,out _)==QuantumClusterCategory.Transform,"Transform family is missing.");
                Check(QuantumClusterClassifier.Classify(typeof(UnityEngine.EventSystems.EventSystem),settings,out _)==QuantumClusterCategory.Scripting,"MonoBehaviour UI did not default to Scripts.");
                WindowLock(go,added,settings);
            }
            finally{provider.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
        }
        private static void WindowLock(GameObject go,Component member,InspectorWheelSettings settings)
        {
            var oldSelection=Selection.activeObject;var others=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>w.Pinned);
            foreach(var pair in others)pair.Key.Pinned=true;
            InspectorWheelWindow window=null;
            try
            {
                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;
                typeof(InspectorWheelWindow).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,settings);
                window.ShowUtility();window.CreateGUI();window.Inspect(go);window.SetGraphMode(true);window.Pinned=false;
                var graph=window.GraphWorkspace;graph.Camera.Pan=new Vector2(137,-94);graph.Camera.Zoom=.63f;
                var oldPositions=graph.Document.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);string root=graph.Document.RootId;
                Selection.activeObject=settings;typeof(InspectorWheelWindow).GetMethod("SelectionChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                Check(graph.Document.RootId==root&&graph.Camera.Pan==new Vector2(137,-94)&&graph.Camera.Zoom==.63f,"External selection violated Investigation Lock.");
                var node=graph.Document.Nodes.Values.Single(n=>n.Target==member);graph.Select(node);
                Check(Selection.activeObject==member&&graph.Document.RootId==root,"Internal selection changed root instead of synchronizing selection.");
                int revision=graph.Document.Revision;settings.OrbitColor=Color.red;settings.AccentColor=Color.green;settings.GraphLinkThickness=4;graph.Configure(settings);
                Check(graph.Document.Revision==revision&&oldPositions.All(p=>graph.Document.Nodes[p.Key].Position==p.Value)&&graph.Camera.Pan==new Vector2(137,-94),"Appearance change altered graph topology, layout or camera.");
                settings.GraphInvestigationLock=false;graph.Configure(settings);
                Check(oldPositions.All(p=>graph.Document.Nodes[p.Key].Position==p.Value),"Unlocking rearranged the investigation.");
                settings.GraphInvestigationLock=true;
                int updates=graph.ViewUpdates;graph.Motion.Direct(graph.Document);
                for(int i=0;i<100;i++)graph.Tick(.033f);
                Check(graph.ViewUpdates<=updates+1,"Idle graph continuously rebuilt presentation.");
                window.CreateGUI();Check(window.GraphWorkspace.Document.RootId==root,"Window rebuild lost stable root.");
            }
            finally{if(window!=null)window.Close();Selection.activeObject=oldSelection;foreach(var pair in others)if(pair.Key!=null)pair.Key.Pinned=pair.Value;}
        }
        private static object Benchmark(int size,InspectorWheelSettings settings)
        {
            var graph=Synthetic(size);var solver=new QuantumGraphLayoutEngine();var watch=Stopwatch.StartNew();solver.Arrange(graph,settings);watch.Stop();double initial=watch.Elapsed.TotalMilliseconds;
            var motion=new QuantumGraphMotion();motion.Commit(graph,0,QuantumGraphMotionMode.Off);
            long bytes=GC.GetAllocatedBytesForCurrentThread();watch.Restart();for(int i=0;i<10000;i++)motion.Sample(1,graph.Generation);watch.Stop();long idle=GC.GetAllocatedBytesForCurrentThread()-bytes;
            Check(graph.Nodes.Count==size+8,"Synthetic stress graph truncated members.");
            bool allocations=AllocationCounterSupported();
            return new{members=size,nodes=graph.Nodes.Count,edges=graph.Edges.Count,initialLayoutMs=initial,measurementMs=solver.MeasureMilliseconds,placementMs=solver.PlacementMilliseconds,indexMs=solver.IndexMilliseconds,idleMotionCalls=10000,idleMotionMs=watch.Elapsed.TotalMilliseconds,allocationCounterSupported=allocations,idleMotionAllocatedBytes=allocations?(long?)idle:null,motion="Off",visible="not attached to a viewport; math benchmark"};
        }
        private static void ProjectAndPreviews(InspectorWheelSettings settings)
        {
            const string folder="Assets/FightSongStudio/QuantumInspector/Demo/GraphFixtures";
            Check(AssetDatabase.IsValidFolder(folder),"Generate the graph trial fixtures before verification.");
            var provider=new QuantumGraphDataProvider();
            try
            {
                var graph=provider.Open(new WheelScope{Kind=WheelScopeKind.ProjectFolder,Folder=folder},false,settings);
                for(int i=0;i<1000&&provider.Busy;i++)provider.Tick();
                Check(!provider.Busy,"Scoped folder discovery did not complete.");
                var children=graph.DirectChildren(graph.RootId).ToArray();
                Check(children.Any(n=>n.Kind==QuantumGraphNodeKind.Folder)&&children.Count(n=>n.Kind==QuantumGraphNodeKind.Asset)>=6,"Immediate folders and mixed assets were not discovered together.");
                Check(!graph.Nodes.Values.Any(n=>n.AssetPath==folder+"/Leaf/README.txt"),"Folder discovery eagerly loaded descendants.");
                var leaf=children.Single(n=>n.AssetPath==folder+"/Leaf");provider.Expand(leaf);for(int i=0;i<1000&&provider.Busy;i++)provider.Tick();
                Check(graph.DirectChildren(leaf.Id).Single().AssetPath==folder+"/Leaf/README.txt","Leaf folder omitted its immediate file.");
                new QuantumGraphLayoutEngine().Arrange(graph,settings);var prior=graph.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);
                var previews=new QuantumGraphPreviewCache();previews.BeginVisible();foreach(var asset in children)previews.RequestVisible(asset);
                for(int i=0;i<60;i++)previews.Tick(i*.25,provider);
                Check(previews.Pending==0&&previews.Requests<=children.Length*12,"Preview retries did not terminate within their budget.");
                Check(prior.All(p=>graph.Nodes[p.Key].Position==p.Value),"Preview arrival changed geometry.");
                Check(children.Where(n=>n.Kind==QuantumGraphNodeKind.Asset).All(n=>n.Icon!=null),"Unsupported preview lacked a fallback icon.");
                previews.Clear();Check(previews.Pending==0,"Root change did not release stale preview requests.");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/DenseComponentFixture.prefab");
                graph=provider.Open(new WheelScope{Kind=WheelScopeKind.Components,Object=prefab},false,settings);
                Check(graph.Nodes.Values.Count(n=>n.Kind==QuantumGraphNodeKind.Component)==42,"Dense fixture did not expose all 42 components.");
                var scripts=graph.Nodes.Values.Single(n=>n.Kind==QuantumGraphNodeKind.Cluster&&n.Label=="Scripts");
                Check(scripts.MemberCount==32,"Repeated script instances did not remain distinct in Scripts.");
                var probe=graph.DirectChildren(scripts.Id).First();provider.RevealReferences(probe,QuantumGraphOverlays.SerializedReferences);
                for(int i=0;i<1000&&provider.Busy;i++)provider.Tick();
                Check(graph.Edges.Values.Any(e=>e.From==probe.Id&&e.Relation==QuantumGraphRelation.SerializedReference&&e.Label=="upstream"),"Actual serialized property path was not preserved.");
                Check(graph.Nodes.Values.Where(n=>n.Target!=null&&EditorUtility.IsPersistent(n.Target)&&n.Kind==QuantumGraphNodeKind.Component).All(n=>n.Id.Contains("guid:")),"Asset component identity omitted GUID/local ID.");
            }
            finally{provider.Dispose();}
        }
        public static object Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Run in Edit Mode.");
            assertions=0;var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            try
            {
                settings.GraphMotion=QuantumGraphMotionMode.Off;LayoutAndMotion(settings);IdentityAndLock(settings);ProjectAndPreviews(settings);
                Benchmark(100,settings);
                var benchmarks=new[]{Benchmark(100,settings),Benchmark(500,settings),Benchmark(2000,settings)};
                return new{passed=true,assertions,unity=Application.unityVersion,cpu=SystemInfo.processorType,memoryMB=SystemInfo.systemMemorySize,gpu=SystemInfo.graphicsDeviceName,benchmarks};
            }
            finally{Object.DestroyImmediate(settings);}
        }
    }
}
