using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public static class QuantumNebulaVerification
    {
        private static int assertions;
        private static void Check(bool condition,string message){assertions++;if(!condition)throw new InvalidOperationException(message);}
        private static void Drain(QuantumGraphView graph)
        {for(int i=0;i<1000&&graph.Provider.Busy;i++)graph.Tick(.033f);Check(!graph.Provider.Busy,"Discovery did not settle.");}
        public static object Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Run in Edit Mode.");
            assertions=0;var original=Selection.activeObject;
            var others=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>w.Pinned);
            foreach(var pair in others)pair.Key.Pinned=true;
            var scene=EditorSceneManager.NewPreviewScene();var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();InspectorWheelWindow window=null;
            try
            {
                settings.GraphInvestigationLock=false;settings.GraphPalette=QuantumGraphPalette.CosmicNebula;settings.GraphMotion=QuantumGraphMotionMode.Full;
                var a=ObjectFactory.CreateGameObject(scene,HideFlags.None,"Cosmic selection A");var b=ObjectFactory.CreateGameObject(scene,HideFlags.None,"Cosmic selection B");
                var audio=a.AddComponent<AudioSource>();audio.enabled=false;var repeated=a.AddComponent<AudioSource>();
                string presetBefore=EditorJsonUtility.ToJson(settings);bool dirtyBefore=scene.isDirty;
                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();
                typeof(InspectorWheelWindow).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,settings);
                window.Pinned=true;window.ShowUtility();window.CreateGUI();window.Inspect(a);window.SetGraphMode(true);window.Pinned=false;
                Check(!settings.GraphInvestigationLock,"Entering Graph Mode enabled a lock without user intent.");
                Selection.activeObject=b;window.ObserveSelection();var graph=window.GraphWorkspace;
                Check(graph.Document.Root.Target==b,"Hierarchy selection did not replace root.");
                Check(graph.Selected?.Target==b,"Hierarchy selection did not identify the owner.");
                Selection.activeObject=audio;window.ObserveSelection();Drain(graph);
                Check(graph.Document.Root.Target==a&&graph.Selected?.Target==audio,"Component selection failed to select its owner and exact instance.");
                Check(Selection.activeObject==audio,"Component selection identity was replaced by its GameObject.");
                var ownerPreviews=new QuantumGraphPreviewCache();ownerPreviews.RequestVisible(graph.Document.Root);
                Check(ownerPreviews.Pending==0&&graph.Document.Root.PreviewState==QuantumGraphPreviewState.Fallback,"Scene owner entered the potentially blocking asset-preview renderer.");ownerPreviews.Clear();
                var id=graph.Selected.Id;
                Selection.activeObject=a;window.ObserveSelection();Check(graph.Selected?.Target==a,"Selecting the owner after a component retained component focus.");
                graph.Select(graph.Document.Nodes.Values.Single(n=>n.Target==repeated));window.ObserveSelection();
                Check(graph.Document.Root.Target==a&&graph.Selected.Target==repeated&&graph.Selected.Id!=id,"Internal selection rerooted or merged repeated components.");
                var positions=graph.Document.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);var pan=graph.Camera.Pan;float zoom=graph.Camera.Zoom;int revision=graph.Document.Revision;
                foreach(QuantumGraphPalette palette in Enum.GetValues(typeof(QuantumGraphPalette)))
                {settings.GraphPalette=palette;graph.Configure(settings);Check(graph.Document.Revision==revision&&graph.Camera.Pan==pan&&graph.Camera.Zoom==zoom&&positions.All(p=>graph.Document.Nodes[p.Key].Position==p.Value),"Theme changed graph geometry or data.");}
                foreach(QuantumGraphQuality quality in Enum.GetValues(typeof(QuantumGraphQuality)))
                {settings.GraphQuality=quality;graph.Configure(settings);Check(graph.Document.Revision==revision&&positions.All(p=>graph.Document.Nodes[p.Key].Position==p.Value),"Quality changed graph data or positions.");}
                settings.GraphPalette=QuantumGraphPalette.CosmicNebula;settings.GraphAmbient=true;settings.GraphInvestigationLock=true;
                Check(!settings.AmbientEnabled,"Ambient started automatically in a locked investigation.");
                settings.GraphAmbientWhileLocked=true;Check(settings.AmbientEnabled,"Explicit locked-ambient opt-in was ignored.");
                var pinned=graph.Selected;pinned.Pinned=true;var pin=pinned.Position;
                Selection.activeObject=b;window.ObserveSelection();Check(graph.Document.Root.Target==a&&pinned.Position==pin,"Lock failed to preserve root and pin.");
                foreach(var mode in new[]{QuantumGraphMotionMode.Reduced,QuantumGraphMotionMode.Off})
                {
                    settings.GraphMotion=QuantumGraphMotionMode.Full;pinned.Position+=Vector2.one*30;graph.Motion.Commit(graph.Document,EditorApplication.timeSinceStartup,QuantumGraphMotionMode.Full);
                    settings.GraphMotion=mode;graph.Configure(settings);
                    Check(!settings.AmbientEnabled&&!graph.Motion.Active&&pinned.DrawPosition==pinned.Position,"Motion preference did not settle immediately.");
                }
                graph.SuspendPresentation(true);int updates=graph.ViewUpdates;for(int i=0;i<100;i++)graph.Tick(.1f);Check(graph.ViewUpdates==updates,"Hidden graph performed presentation updates.");graph.SuspendPresentation(false);
                settings.GraphInvestigationLock=false;settings.GraphAmbient=false;
                const string folder="Assets/FightSongStudio/QuantumInspector/Demo/GraphFixtures/Leaf";
                Selection.activeObject=AssetDatabase.LoadMainAssetAtPath(folder);window.ObserveSelection();Drain(graph);
                Check(graph.Document.Project&&graph.Document.Root.AssetPath==folder,"Folder selection did not replace Hierarchy context.");
                var asset=AssetDatabase.LoadMainAssetAtPath(folder+"/README.txt");Selection.activeObject=asset;window.ObserveSelection();Drain(graph);graph.Tick(.033f);
                Check(graph.Selected?.AssetPath==folder+"/README.txt","Leaf asset was not revealed and selected.");
                var bounds=graph.Selected.Size;for(int i=0;i<60;i++)graph.Previews.Tick(i*.25+EditorApplication.timeSinceStartup,graph.Provider);
                Check(graph.Selected.Size==bounds,"Preview loading changed reserved bounds.");
                Check(EditorJsonUtility.ToJson(settings)==presetBefore,"Personal graph configuration changed serialized preset values.");
                Check(scene.isDirty==dirtyBefore,"Presentation changed scene dirtiness.");
                return new{passed=true,assertions,unity=Application.unityVersion,selection="Hierarchy, component identity, internal selection, folder, leaf file",motion="Full / Reduced / Off, interrupted commit, hidden suspension",theme="All palettes preserve topology, camera and positions"};
            }
            finally
            {
                if(window!=null)window.Close();Object.DestroyImmediate(settings);EditorSceneManager.ClosePreviewScene(scene);
                Selection.activeObject=original;foreach(var pair in others)if(pair.Key!=null)pair.Key.Pinned=pair.Value;
            }
        }
    }
}
