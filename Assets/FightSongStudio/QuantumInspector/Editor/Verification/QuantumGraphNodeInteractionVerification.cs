using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public static class QuantumGraphNodeInteractionVerification
    {
        private static InspectorWheelWindow window;
        private static InspectorWheelSettings settings;
        private static Scene scene;
        private static GameObject fixture;
        private static Dictionary<InspectorWheelWindow,bool> locks;
        private static Object selection;
        private static Vector2 originalSize,grownSize,shrunkSize;
        private static string key;
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static object Begin()
        {
            Check(window==null,"Finish the previous verification first.");
            Check(!EditorApplication.isPlaying,"Run in Edit Mode.");
            selection=Selection.activeObject;
            locks=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>w.Pinned);
            foreach(var pair in locks)pair.Key.Pinned=true;
            try
            {
                settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();settings.GraphInvestigationLock=true;settings.ReducedMotion=true;
                scene=EditorSceneManager.NewPreviewScene();
                fixture=ObjectFactory.CreateGameObject(scene,HideFlags.None,"QuantumInspector_Calibration_Station_With_A_Long_Object_Title_That_Must_Remain_Fully_Readable_And_Resize_Without_Losing_Its_Text");
                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;
                typeof(InspectorWheelWindow).GetField("settings",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,settings);
                window.position=new Rect(120,100,1100,760);window.ShowUtility();window.CreateGUI();window.Inspect(fixture);window.SetGraphMode(true);
                var graph=window.GraphWorkspace;key=graph.Document.Key;
                graph.Camera.Zoom=1;graph.Camera.Center(graph.Document.Root.Position);graph.Moved(false);
                originalSize=graph.Document.Root.Size;
                return new{ready=true,width=originalSize.x,height=originalSize.y};
            }
            catch{Finish();throw;}
        }
        private static QuantumGraphNodeView RootView()=>window.GraphWorkspace.Viewport.Query<QuantumGraphNodeView>().ToList().Single(v=>v.Node==window.GraphWorkspace.Document.Root);
        private static object CheckTitle()
        {
            var visual=RootView();var label=visual.Q<Label>(className:"graph-node-title");
            var measured=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
            Check(label.resolvedStyle.whiteSpace==WhiteSpace.Normal,"Title wrapping is disabled.");
            Check(label.contentRect.height+.5f>=measured.y,"Title is clipped: needs "+measured.y+", has "+label.contentRect.height);
            Check(label.worldBound.yMin>=visual.worldBound.yMin&&label.worldBound.yMax<=visual.worldBound.yMax,"Title extends outside its node.");
            return new{titleHeight=measured.y,availableHeight=label.contentRect.height,nodeWidth=visual.Node.Size.x,nodeHeight=visual.Node.Size.y};
        }
        private static void Drag(Vector2 delta)
        {
            var visual=RootView();var handle=visual.Q("nodeResizeHandle");var graph=window.GraphWorkspace;
            Check(handle!=null&&handle.panel!=null,"Node resize handle is not attached.");
            var start=handle.worldBound.center;var pan=graph.Camera.Pan;
            using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=start}))
            {e.target=handle;handle.SendEvent(e);}
            Check(handle.HasPointerCapture(PointerId.mousePointerId),"Resize handle did not capture PointerDown.");
            using(var e=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseDrag,button=0,mousePosition=start+delta}))
            {
                typeof(PointerMoveEvent).GetProperty("pressedButtons").SetValue(e,1);
                e.target=handle;handle.SendEvent(e);
            }
            using(var e=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=start+delta}))
            {e.target=handle;handle.SendEvent(e);}
            Check(!handle.HasPointerCapture(PointerId.mousePointerId),"Resize left the mouse captured.");
            Check(graph.Camera.Pan==pan,"Resizing panned the graph.");
        }
        public static object Grow()
        {
            try
            {
                var title=CheckTitle();Check(originalSize.x>360,"Long title did not expand past the previous width cap.");
                Drag(new Vector2(150,80));grownSize=window.GraphWorkspace.Document.Root.Size;
                Check(grownSize.x>originalSize.x+100&&grownSize.y>originalSize.y,"Pointer drag did not enlarge the node: "+originalSize+" -> "+grownSize);
                return new{passed=true,automaticTitle=title,width=grownSize.x,height=grownSize.y};
            }
            catch{Finish();throw;}
        }
        public static object Shrink()
        {
            try
            {
                CheckTitle();Drag(new Vector2(190-grownSize.x,-2000));shrunkSize=window.GraphWorkspace.Document.Root.Size;
                Check(shrunkSize.x<grownSize.x&&shrunkSize.y>=64,"Pointer drag failed to shrink within the readable limits.");
                window.GraphWorkspace.SaveState();
                using(var provider=new QuantumGraphDataProvider())
                {
                    var restored=provider.Open(new WheelScope{Kind=WheelScopeKind.Components,Object=fixture},false,settings).Root;
                    Check(restored.CustomSize&&restored.Size==shrunkSize,"Resized node did not survive reopening its graph.");
                }
                return new{passed=true,width=shrunkSize.x,height=shrunkSize.y,persisted=true};
            }
            catch{Finish();throw;}
        }
        public static object Complete()
        {
            try
            {
                var title=CheckTitle();
                window.GraphWorkspace.Refresh();
                Check(window.GraphWorkspace.Document.Root.Size==shrunkSize,"Refresh discarded custom node dimensions.");
                window.GraphWorkspace.RestoreNodeSize(window.GraphWorkspace.Document.Root);
                Check(!window.GraphWorkspace.Document.Root.CustomSize&&window.GraphWorkspace.Document.Root.Size==originalSize,"Restore size did not return to title-fitting dimensions.");
                return new{passed=true,wrappedTitle=title,pointerEnlarge=true,pointerShrink=true,sizePersistence=true,refreshPreservesSize=true,restoreAutomaticSize=true};
            }
            finally{Finish();}
        }
        public static void Finish()
        {
            if(window!=null)window.Close();window=null;
            if(key!=null)QuantumGraphStatePersistence.instance.Forget(key);key=null;
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);scene=default;
            if(settings!=null)Object.DestroyImmediate(settings);settings=null;fixture=null;
            Selection.activeObject=selection;
            if(locks!=null)foreach(var pair in locks)if(pair.Key!=null)pair.Key.Pinned=pair.Value;
            locks=null;
        }
    }
}
