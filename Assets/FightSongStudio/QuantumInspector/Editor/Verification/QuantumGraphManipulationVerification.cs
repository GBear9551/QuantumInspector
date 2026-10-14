using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public static class QuantumGraphManipulationVerification
    {
        private static InspectorWheelWindow window;
        private static InspectorWheelSettings settings;
        private static Object selection;
        private static Dictionary<InspectorWheelWindow,bool> locks;
        private static QuantumSavedGraph previous;
        private static string key;
        private static readonly Dictionary<string,bool?> boolPrefs=new Dictionary<string,bool?>();
        private static readonly Dictionary<string,int?> intPrefs=new Dictionary<string,int?>();
        private static float? dotPref;
        private const string Prefix="FightSong.QuantumInspector.Cosmic.";
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static Dictionary<string,Vector2> Anchors()=> (Dictionary<string,Vector2>)typeof(QuantumGraphView).GetField("screenAnchors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window.GraphWorkspace);
        private static List<QuantumGraphDot> Dots()=> (List<QuantumGraphDot>)typeof(QuantumGraphView).GetField("pointMarks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window.GraphWorkspace);
        public static object Begin()
        {
            Check(window==null&&!EditorApplication.isPlaying,"Run one verification at a time in Edit Mode.");
            selection=Selection.activeObject;locks=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>w.Pinned);
            foreach(var pair in locks)pair.Key.Pinned=true;
            foreach(string name in new[]{"Lock","Ambient","LockedAmbient","AuxiliaryUI"})boolPrefs[Prefix+name]=EditorPrefs.HasKey(Prefix+name)?EditorPrefs.GetBool(Prefix+name):(bool?)null;
            foreach(string name in new[]{"Motion","Palette","Quality"})intPrefs[Prefix+name]=EditorPrefs.HasKey(Prefix+name)?EditorPrefs.GetInt(Prefix+name):(int?)null;
            dotPref=EditorPrefs.HasKey(Prefix+"DotSize")?EditorPrefs.GetFloat(Prefix+"DotSize"):(float?)null;
            try
            {
                settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();settings.GraphMotion=QuantumGraphMotionMode.Off;settings.GraphInvestigationLock=true;
                settings.GraphShowAuxiliary=true;
                var fixture=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightSongStudio/QuantumInspector/Demo/GraphFixtures/DenseComponentFixture.prefab");
                Check(fixture!=null,"DenseComponentFixture prefab is required.");
                var scope=new WheelScope{Kind=WheelScopeKind.Components,Object=fixture};key=QuantumGraphDataProvider.KeyFor(scope,false,settings);previous=QuantumGraphStatePersistence.instance.Get(key);
                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;window.minSize=new Vector2(860,600);
                typeof(InspectorWheelWindow).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,settings);
                window.position=new Rect(120,100,1280,820);window.ShowUtility();window.CreateGUI();window.Inspect(fixture);window.SetGraphMode(true);
                window.GraphWorkspace.Fit();return new{ready=true,nodes=window.GraphWorkspace.Document.Nodes.Count,dotSize=settings.GraphDotSize};
            }
            catch{Close();throw;}
        }
        private static void Drag(VisualElement target,Vector2 start,Vector2 delta)
        {
            using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=start})){e.target=target;target.SendEvent(e);}
            using(var e=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseDrag,button=0,mousePosition=start+delta}))
            {typeof(PointerMoveEvent).GetProperty("pressedButtons").SetValue(e,1);e.target=target;target.SendEvent(e);}
            using(var e=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=start+delta})){e.target=target;target.SendEvent(e);}
        }
        public static object GraphControls()
        {
            Check(window!=null,"Call Begin and allow a UI frame first.");
            var graph=window.GraphWorkspace;graph.Fit();graph.Camera.Zoom=Mathf.Min(.08f,graph.Camera.Zoom);graph.Moved(false);
            var document=graph.Document;var family=document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Cluster).OrderByDescending(n=>n.MemberCount).First();
            var visual=graph.Viewport.Query<QuantumGraphNodeView>().ToList().Single(v=>v.Node==family);
            var nodes=graph.DragSet(family);var positions=document.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);
            var anchors=Anchors().ToDictionary(p=>p.Key,p=>p.Value);var pan=graph.Camera.Pan;float zoom=graph.Camera.Zoom;var offset=family.OverviewOffset;
            var delta=new Vector2(26,17);var start=graph.Viewport.LocalToWorld(anchors[family.Id]);
            Drag(visual,start,delta);
            Check(graph.Selected==family,"Dragging the category did not select it.");
            Check(Vector2.Distance(Anchors()[family.Id],anchors[family.Id]+delta)<.05f,"Category header did not follow the pointer: "+anchors[family.Id]+" -> "+Anchors()[family.Id]+" expected "+(anchors[family.Id]+delta)+"; offset "+family.OverviewOffset+"; world "+family.Position+"; camera "+graph.Camera.Pan+" / "+graph.Camera.Zoom);
            foreach(var node in nodes)
            {
                Check(Vector2.Distance(node.Position,positions[node.Id]+delta/zoom)<.05f,"Category drag omitted a member.");
                if(anchors.ContainsKey(node.Id))Check(Vector2.Distance(Anchors()[node.Id],anchors[node.Id]+delta)<.05f,"A dot did not travel with its category.");
            }
            Check(positions.Where(p=>!nodes.Any(n=>n.Id==p.Key)).All(p=>document.Nodes[p.Key].Position==p.Value),"Category drag moved an unrelated node.");
            Check(graph.Camera.Pan==pan&&graph.Camera.Zoom==zoom,"Category drag moved the camera.");
            Check(Vector2.Distance(family.OverviewOffset,offset+delta/zoom)<.01f,"Category presentation offset was not retained.");
            graph.SaveState();var saved=QuantumGraphStatePersistence.instance.Get(document.Key).Nodes.Single(n=>n.Id==family.Id);
            Check(saved.Position==family.Position&&saved.OverviewOffset==family.OverviewOffset,"Category drag did not persist.");
            using(var provider=new QuantumGraphDataProvider())
            {
                var restored=provider.Open(new WheelScope{Kind=WheelScopeKind.Components,Object=document.Root.Target as GameObject},false,settings).Nodes[family.Id];
                Check(restored.Position==family.Position&&restored.OverviewOffset==family.OverviewOffset,"Reopening discarded category movement.");
            }
            var moved=Anchors()[family.Id];graph.Camera.Zoom=1;graph.Moved(false);graph.Camera.Zoom=zoom;graph.Camera.Pan=pan;graph.Moved(false);
            Check(Vector2.Distance(Anchors()[family.Id],moved)<.05f,"Zooming back discarded the category placement.");
            var overview=(QuantumGraphOverview)typeof(QuantumGraphView).GetField("zoomOverview",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(graph);
            var group=overview.Groups[family.Id];var empty=new Vector2(group.Bounds.xMin+4,group.Bounds.yMax-4);
            Check(graph.Hit(empty)==family,"Empty category enclosure could not select the category.");
            var world=document.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);var field=graph.Q<Slider>("graphDotSize");
            Check(field!=null&&settings.GraphDotSize==2,"The default dot size is not 200%.");
            field.value=1;var original=Dots().ToDictionary(d=>d.Node.Id,d=>d.Radius);field.value=2;
            Check(Dots().All(d=>Mathf.Abs(d.Radius-original[d.Node.Id]*2)<.001f),"Default dot diameter was not doubled.");
            field.value=3;Check(Dots().All(d=>Mathf.Abs(d.Radius-original[d.Node.Id]*3)<.001f),"Dot size control did not enlarge the dots.");
            Check(EditorPrefs.GetFloat(Prefix+"DotSize")==3,"Personal dot size did not persist.");
            Check(world.All(p=>document.Nodes[p.Key].Position==p.Value),"Dot size changed layout.");field.value=2;
            return new{passed=true,category=family.Label,movedNodes=nodes.Count,headerAndDotsFollowPointer=true,unrelatedPositionsPreserved=true,categoryPersisted=true,zoomRoundTrip=true,enclosureHit=true,doubleDiameter=true,adjustableAndPersisted=true};
        }
        public static object FloatingInspector()
        {
            Check(window!=null,"Call Begin and allow a UI frame first.");
            var graph=window.GraphWorkspace;
            var components=graph.Document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Component&&n.Target is Component).Take(2).ToArray();
            Check(components.Length==2,"Fixture requires two components.");
            graph.Camera.Zoom=1;graph.Camera.Pan=new Vector2(210,170)-graph.Viewport.contentRect.size*.5f-components[0].DrawPosition;
            graph.Moved(false);graph.Select(components[0]);
            var inspector=graph.ComponentInspector;var editor=inspector.ComponentEditor;
            Check(inspector.Visible&&editor!=null&&editor.target==components[0].Target,"Selected component did not open its exact native editor.");
            Check(Vector2.Distance(inspector.Anchor,Anchors()[components[0].Id])<.01f,"Inspector was not anchored to the displayed node.");
            var oldAnchor=inspector.Anchor;var delta=new Vector2(25,18);graph.Camera.Pan+=delta;graph.Moved(false);
            Check(Vector2.Distance(inspector.Anchor,oldAnchor+delta)<.01f,"Inspector anchor did not follow graph pan.");
            Check(inspector.ComponentEditor==editor,"Panning recreated the native editor.");
            var pointer=inspector.Anchor;graph.Camera.ZoomAt(pointer,graph.Viewport.contentRect.size,-1);graph.Moved(false);
            Check(Vector2.Distance(inspector.Anchor,Anchors()[components[0].Id])<.01f,"Inspector anchor did not follow zoom.");
            var pan=graph.Camera.Pan;float zoom=graph.Camera.Zoom;
            using(var e=WheelEvent.GetPooled(new Event{type=EventType.ScrollWheel,delta=new Vector2(0,2)}))
            {e.target=inspector;inspector.SendEvent(e);}
            using(var e=KeyDownEvent.GetPooled(new Event{type=EventType.KeyDown,keyCode=KeyCode.F}))
            {e.target=inspector;inspector.SendEvent(e);}
            Check(graph.Camera.Pan==pan&&graph.Camera.Zoom==zoom,"Inspector input triggered graph navigation.");
            graph.Camera.Pan=new Vector2(210,170)-graph.Viewport.contentRect.size*.5f-components[1].DrawPosition*graph.Camera.Zoom;
            graph.Moved(false);graph.Select(components[1]);
            Check(inspector.Visible&&inspector.ComponentEditor.target==components[1].Target&&editor==null,"Component switch retained the previous editor.");
            var viewport=graph.Viewport.contentRect.size;var bounds=inspector.PanelBounds;
            Check(bounds.xMin>=0&&bounds.yMin>=0&&bounds.xMax<=viewport.x&&bounds.yMax<=viewport.y,"Floating inspector escaped the viewport.");
            graph.Select(null);Check(!inspector.Visible&&inspector.ComponentEditor==null,"Clearing selection leaked the native editor.");
            graph.Fit();
            return new{passed=true,exactComponent=true,followsPanAndZoom=true,cachedEditor=true,inputIsolated=true,selectionCleanup=true,clampedToViewport=true};
        }
        public static object ResizeControls()
        {
            Check(window!=null&&!window.docked,"Call Begin for a floating verification window.");
            var graph=window.GraphWorkspace;var selected=graph.Selected;var pan=graph.Camera.Pan;float zoom=graph.Camera.Zoom;
            var border=window.rootVisualElement.Q("quantumWindowResizeBorder");Check(border!=null&&border.childCount==8,"Window edges and corner handles were not installed.");
            var handle=border.Q("windowResizeRightBottom");Check(handle!=null&&handle.panel!=null,"Corner resize handle is not attached.");
            var original=window.position;Drag(handle,handle.worldBound.center,new Vector2(-80,-40));
            Check(Mathf.Abs(window.position.width-original.width+80)<1&&Mathf.Abs(window.position.height-original.height+40)<1,"Border dragging did not shrink the window: "+original+" -> "+window.position+"; handle "+handle.worldBound);
            Drag(handle,handle.worldBound.center,new Vector2(80,40));
            Check(Mathf.Abs(window.position.width-original.width)<1&&Mathf.Abs(window.position.height-original.height)<1,"Border dragging did not restore the window size.");
            Check(graph.Selected==selected&&graph.Camera.Pan==pan&&graph.Camera.Zoom==zoom,"Window resizing changed graph selection or camera.");
            var left=QuantumInspectorWindowResize.Bounds(new Rect(100,100,1000,700),new Vector2(900,900),new Vector2(860,600),new Vector2(4000,4000),QuantumWindowEdge.Left|QuantumWindowEdge.Top);
            Check(left.size==new Vector2(860,600)&&left.xMax==1100&&left.yMax==800,"Top/left minimum size did not preserve the opposite borders.");
            var divider=window.rootVisualElement.Q("unity-dragline-anchor");Check(divider!=null&&divider.resolvedStyle.width>=15,"Graph/details border hit area was not widened.");
            window.position=original;
            return new{passed=true,allEightHandles=true,cornerDrag=true,minimumSize=true,selectionAndCameraPreserved=true,dividerHitWidth=divider.resolvedStyle.width};
        }
        public static object Close()
        {
            if(window!=null){window.Close();window=null;}if(settings!=null){Object.DestroyImmediate(settings);settings=null;}
            if(key!=null){if(previous!=null)QuantumGraphStatePersistence.instance.Store(previous);else QuantumGraphStatePersistence.instance.Forget(key);}key=null;previous=null;
            foreach(var pair in boolPrefs){if(pair.Value.HasValue)EditorPrefs.SetBool(pair.Key,pair.Value.Value);else EditorPrefs.DeleteKey(pair.Key);}boolPrefs.Clear();
            foreach(var pair in intPrefs){if(pair.Value.HasValue)EditorPrefs.SetInt(pair.Key,pair.Value.Value);else EditorPrefs.DeleteKey(pair.Key);}intPrefs.Clear();
            if(dotPref.HasValue)EditorPrefs.SetFloat(Prefix+"DotSize",dotPref.Value);else EditorPrefs.DeleteKey(Prefix+"DotSize");
            if(locks!=null){Selection.activeObject=selection;foreach(var pair in locks)if(pair.Key!=null)pair.Key.Pinned=pair.Value;locks=null;}
            return new{closed=true};
        }
    }
}
