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
    public static class QuantumGraphCleanViewVerification
    {
        private static InspectorWheelWindow window;
        private static InspectorWheelSettings settings;
        private static Dictionary<InspectorWheelWindow,bool> locks;
        private static Object originalSelection;
        private static string sessionKey;
        private static Vector2 cleanSize,pan;
        private static float zoom;
        private static Dictionary<string,Vector2> positions;
        private static readonly Dictionary<string,bool?> boolPrefs=new Dictionary<string,bool?>();
        private static readonly Dictionary<string,int?> intPrefs=new Dictionary<string,int?>();
        private static float? dotPref;
        private const string Prefix="FightSong.QuantumInspector.Cosmic.";
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static object Begin()
        {
            Check(window==null&&!EditorApplication.isPlaying,"Finish the prior review first; run in Edit Mode.");
            originalSelection=Selection.activeObject;locks=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>w.Pinned);
            foreach(var pair in locks)pair.Key.Pinned=true;
            foreach(string key in new[]{"Lock","Ambient","LockedAmbient","AuxiliaryUI"})boolPrefs[Prefix+key]=EditorPrefs.HasKey(Prefix+key)?EditorPrefs.GetBool(Prefix+key):(bool?)null;
            foreach(string key in new[]{"Motion","Palette","Quality"})intPrefs[Prefix+key]=EditorPrefs.HasKey(Prefix+key)?EditorPrefs.GetInt(Prefix+key):(int?)null;
            dotPref=EditorPrefs.HasKey(Prefix+"DotSize")?EditorPrefs.GetFloat(Prefix+"DotSize"):(float?)null;
            try
            {
                settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
                var fixture=GameObject.Find("SHOWCASE_DenseComponentFixture")??AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightSongStudio/QuantumInspector/Demo/GraphFixtures/DenseComponentFixture.prefab");
                Check(fixture!=null,"The real Dense component fixture is required.");
                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;window.minSize=new Vector2(860,600);
                typeof(InspectorWheelWindow).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,settings);
                window.position=new Rect(20,40,1180,620);window.ShowUtility();window.CreateGUI();window.Inspect(fixture);window.SetGraphMode(true);
                var graph=window.GraphWorkspace;
                // Read real graph data without importing the user's saved manual arrangement into the review.
                graph.Provider.Dispose();typeof(QuantumGraphDataProvider).GetProperty("Document").SetValue(graph.Provider,null);
                var document=graph.Provider.Open(new WheelScope{Kind=WheelScopeKind.Components,Object=fixture},false,settings,false);
                document.Key=sessionKey=QuantumGraphIdentity.SessionPrefix+"clean-view-review:"+Guid.NewGuid().ToString("N");
                graph.Layout.Arrange(document,settings);graph.Motion.Direct(document);graph.Select(document.Root);graph.Fit();
                window.titleContent=new GUIContent("Quantum Inspector — Clean Defaults");window.Focus();
                return new{ready=true,realRoot=document.Root.Label,nodes=document.Nodes.Count,components=fixture.GetComponents<Component>().Length,clean=!settings.GraphShowAuxiliary};
            }
            catch{Close();throw;}
        }
        public static object ShowAuxiliary()
        {
            Check(window!=null,"Call Begin and allow an Editor frame.");var graph=window.GraphWorkspace;
            cleanSize=graph.Viewport.contentRect.size;pan=graph.Camera.Pan;zoom=graph.Camera.Zoom;
            positions=graph.Document.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);
            Check(window.rootVisualElement.ClassListContains("graph-clean"),"Clean view is not the default.");
            Check(window.rootVisualElement.Q(className:"quantum-navigation").resolvedStyle.display==DisplayStyle.None,"Auxiliary navigation is visible.");
            Check(window.rootVisualElement.Q(className:"quantum-footer").resolvedStyle.display==DisplayStyle.None,"Footer is visible in the clean view.");
            Check(graph.Q("graphAuxiliaryInfo").resolvedStyle.display==DisplayStyle.None,"Graph legends and relationship text are visible.");
            Check(window.rootVisualElement.Q("graphSettingsButton")!=null,"Settings is inaccessible in clean view.");
            if(graph.Q("graphSettingsPanel").style.display.value!=DisplayStyle.Flex)graph.ToggleSettings();Check(graph.Q("graphSettingsPanel").style.display.value==DisplayStyle.Flex,"Settings drawer did not open.");
            graph.Q<Toggle>("graphShowAuxiliary").value=true;
            Check(EditorPrefs.GetBool(Prefix+"AuxiliaryUI"),"Auxiliary UI preference did not persist.");
            return new{ready=true,cleanViewport=cleanSize.ToString(),settingsDrawer=true,auxiliaryPreferenceSaved=true};
        }
        public static object VerifyAuxiliaryAndReset()
        {
            var graph=window.GraphWorkspace;var fullSize=graph.Viewport.contentRect.size;
            Check(!window.rootVisualElement.ClassListContains("graph-clean"),"Auxiliary toggle did not restore the UI.");
            Check(graph.Q("graphAuxiliaryInfo").resolvedStyle.display!=DisplayStyle.None,"Auxiliary information is still hidden.");
            Check(cleanSize.x>fullSize.x+100&&cleanSize.y>fullSize.y+100,"Clean view did not substantially enlarge the graph viewport.");
            Check(graph.Camera.Pan==pan&&Mathf.Abs(graph.Camera.Zoom-zoom)<.0001f,"Toggling auxiliary UI changed the camera.");
            Check(positions.All(p=>graph.Document.Nodes[p.Key].Position==p.Value),"Toggling auxiliary UI changed graph positions.");
            var pin=graph.Document.Root;pin.Pinned=pin.Manual=true;var pinnedPosition=pin.Position;
            settings.MaximumVisibleNodes=8;settings.ClickReaction=.99f;settings.GraphPalette=QuantumGraphPalette.Light;settings.GraphDotSize=5;settings.GraphAmbient=true;
            graph.RestoreDefaults();
            Check(!settings.GraphShowAuxiliary&&settings.GraphDotSize==2&&settings.GraphPalette==QuantumGraphPalette.CosmicNebula&&settings.GraphQuality==QuantumGraphQuality.Balanced,"Restore did not return to clean presentation defaults.");
            Check(settings.MaximumVisibleNodes==5&&Mathf.Abs(settings.ClickReaction-.45f)<.001f,"Restore did not reset the settings preset.");
            Check(!settings.GraphAmbient&&!settings.GraphAmbientWhileLocked&&settings.GraphMotion==QuantumGraphMotionMode.Full,"Restore did not reset motion defaults.");
            Check(graph.Document.Root==pin&&pin.Pinned&&pin.Position==pinnedPosition,"Restore discarded the graph root or its pinned position.");
            Check(graph.Camera.Pan==pan&&Mathf.Abs(graph.Camera.Zoom-zoom)<.0001f,"Restoring settings changed the camera.");
            Check(!EditorPrefs.GetBool(Prefix+"AuxiliaryUI"),"Restored clean defaults were not saved.");
            return new{passed=true,cleanWidth=cleanSize.x,cleanHeight=cleanSize.y,auxiliaryWidth=fullSize.x,auxiliaryHeight=fullSize.y,areaMultiplier=cleanSize.x*cleanSize.y/(fullSize.x*fullSize.y),cameraAndPositionsPreserved=true,fullPresetDefaultsRestored=true,pinPreserved=true};
        }
        public static object PrepareCapture()
        {
            Check(window!=null,"Call Begin first.");var graph=window.GraphWorkspace;
            Check(!settings.GraphShowAuxiliary,"Capture expects the clean default view.");
            graph.ToggleSettings();graph.ToggleSettings();graph.Select(graph.Document.Root);graph.Fit();window.Focus();window.Repaint();
            return new{ready=true,nodes=graph.Document.Nodes.Count,components=graph.Document.Nodes.Values.Count(n=>n.Kind==QuantumGraphNodeKind.Component),viewport=graph.Viewport.contentRect.ToString(),theme=settings.GraphPalette.ToString()};
        }
        public static object Close()
        {
            if(window!=null){window.Close();window=null;}if(settings!=null){Object.DestroyImmediate(settings);settings=null;}
            if(sessionKey!=null)QuantumGraphStatePersistence.instance.Forget(sessionKey);sessionKey=null;
            foreach(var pair in boolPrefs){if(pair.Value.HasValue)EditorPrefs.SetBool(pair.Key,pair.Value.Value);else EditorPrefs.DeleteKey(pair.Key);}boolPrefs.Clear();
            foreach(var pair in intPrefs){if(pair.Value.HasValue)EditorPrefs.SetInt(pair.Key,pair.Value.Value);else EditorPrefs.DeleteKey(pair.Key);}intPrefs.Clear();
            if(dotPref.HasValue)EditorPrefs.SetFloat(Prefix+"DotSize",dotPref.Value);else EditorPrefs.DeleteKey(Prefix+"DotSize");
            if(locks!=null){Selection.activeObject=originalSelection;foreach(var pair in locks)if(pair.Key!=null)pair.Key.Pinned=pair.Value;locks=null;}
            return new{closed=true};
        }
    }
}
