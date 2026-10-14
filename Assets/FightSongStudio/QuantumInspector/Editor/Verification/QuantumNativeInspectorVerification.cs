using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public static class QuantumNativeInspectorVerification
    {
        private static Scene scene;
        private static Object original;
        private static bool locked;
        private static GameObject target,other;
        private static AudioSource[] audio;
        private static BoxCollider collider;
        private static InspectorWheelWindow quantum;
        private static Dictionary<InspectorWheelWindow,string> windows;
        private static EditorWindow inspector;
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        public static object Begin()
        {
            Check(target==null,"Close the previous verification.");
            original=Selection.activeObject;locked=ActiveEditorTracker.sharedTracker.isLocked;
            windows=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>EditorJsonUtility.ToJson(w));
            foreach(var window in windows.Keys)window.Pinned=true;
            scene=EditorSceneManager.NewPreviewScene();
            target=new GameObject("Quantum native Inspector verification");SceneManager.MoveGameObjectToScene(target,scene);
            other=new GameObject("Other inspected object");SceneManager.MoveGameObjectToScene(other,scene);other.AddComponent<Light>();
            audio=Enumerable.Range(0,16).Select(_=>target.AddComponent<AudioSource>()).ToArray();collider=target.AddComponent<BoxCollider>();
            ActiveEditorTracker.sharedTracker.isLocked=false;Selection.activeGameObject=target;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            quantum=ScriptableObject.CreateInstance<InspectorWheelWindow>();quantum.ShowUtility();quantum.CreateGUI();quantum.Inspect(target);quantum.SetGraphMode(false);
            return new{ready=true,components=18};
        }
        private static VisualElement Rows()
        {
            inspector=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>QuantumNativeInspectorBridge.ResolveRow(w.rootVisualElement.Q(className:"game-object-inspector")??new VisualElement())==target);
            Check(inspector!=null,"Native Inspector did not display the fixture.");
            return inspector.rootVisualElement.Q(className:"game-object-inspector").parent;
        }
        private static ToolbarSearchField Search()=>inspector.rootVisualElement.Q<ToolbarSearchField>("quantumInspectorComponentSearch");
        private static int Visible()=>Rows().Children().Count(e=>QuantumNativeInspectorBridge.ResolveRow(e) is Component&&e.resolvedStyle.display!=DisplayStyle.None);
        public static object SearchFromWheel()
        {
            Rows();Check(Search()!=null,"Native component search bar missing.");
            quantum.SetQuery("box collider");return new{ready=true};
        }
        public static object VerifyWheelAndSearchNative()
        {
            Check(Visible()==1,"Wheel search did not filter to the BoxCollider.");Check(Search().value=="box collider","Search text did not synchronize.");
            Search().value="audio";return new{wheelPassed=true};
        }
        public static object VerifyNativeAndNoMatches()
        {
            Check(Visible()==16,"Native search did not preserve repeated AudioSource components.");
            Search().value="no-such-component";return new{nativePassed=true};
        }
        public static object VerifyEmptyAndReveal()
        {
            Check(Visible()==0,"No-match filter left components visible.");
            quantum.FocusComponent(audio[12]);return new{noMatchesPassed=true};
        }
        public static object VerifyRevealAndLock()
        {
            Check(Visible()==18,"Selecting a component did not clear the conflicting filter.");
            Check(Search().value=="","Conflicting search was not cleared.");
            var rows=Rows();var row=rows.Children().Single(e=>QuantumNativeInspectorBridge.ResolveRow(e)==audio[12]);
            var scroll=rows.GetFirstAncestorOfType<ScrollView>();
            Check(scroll.scrollOffset.y>0,"Inspector did not scroll to the selected component.");
            Check(row.worldBound.yMin>=scroll.contentViewport.worldBound.yMin-3&&row.worldBound.yMin<scroll.contentViewport.worldBound.yMax,"Selected component header is not visible.");
            ActiveEditorTracker.sharedTracker.isLocked=true;
            Selection.activeGameObject=other;QuantumNativeInspectorBridge.Search(other,"light");
            return new{revealPassed=true,scrollOffset=scroll.scrollOffset.y};
        }
        public static object VerifyLockAndGraph()
        {
            Check(Visible()==18&&Search().value=="","Search on another object changed a locked Inspector.");
            Check(ActiveEditorTracker.sharedTracker.isLocked,"Inspector was unexpectedly unlocked.");
            ActiveEditorTracker.sharedTracker.isLocked=false;Selection.activeGameObject=target;
            quantum.Inspect(target);quantum.SetGraphMode(true);
            quantum.rootVisualElement.Q<WheelSearchField>("graphSearch").value="box";
            return new{lockPassed=true};
        }
        public static object VerifyGraphAndClear()
        {
            Check(Visible()==1&&Search().value=="box","Graph search did not synchronize.");
            Search().value="";return new{graphPassed=true};
        }
        public static object VerifyClear()
        {
            Check(Visible()==18,"Clearing native search did not restore every component.");
            return new{passed=true,wheelSearch=true,graphSearch=true,nativeSearch=true,noMatch=true,duplicateIdentity=true,autoScroll=true,lockedInspectorPreserved=true,clearRestoresAll=true};
        }
        public static object Close()
        {
            if(quantum!=null)quantum.Close();quantum=null;
            ActiveEditorTracker.sharedTracker.isLocked=false;Selection.activeObject=original;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
            ActiveEditorTracker.sharedTracker.isLocked=locked;
            if(windows!=null)foreach(var pair in windows)if(pair.Key!=null){EditorJsonUtility.FromJsonOverwrite(pair.Value,pair.Key);pair.Key.CreateGUI();}
            windows=null;target=null;other=null;return new{closed=true};
        }
    }
}
