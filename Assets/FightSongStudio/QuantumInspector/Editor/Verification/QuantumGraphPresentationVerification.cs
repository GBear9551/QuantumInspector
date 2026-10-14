using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphVerificationWindow:EditorWindow
    {
        internal QuantumGraphView Graph;
        internal InspectorWheelSettings Settings;
        private void OnDisable(){Graph?.Dispose();Graph=null;if(Settings!=null)Object.DestroyImmediate(Settings);}
    }
    public static class QuantumGraphPresentationVerification
    {
        private static QuantumGraphVerificationWindow window;
        private static double initial;
        private static QuantumSavedGraph previous;
        private static string stateKey;
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static object Begin(int members=2000,QuantumGraphQuality quality=QuantumGraphQuality.Balanced)
        {
            if(window!=null)window.Close();
            window=ScriptableObject.CreateInstance<QuantumGraphVerificationWindow>();window.titleContent=new GUIContent("Graph verification");window.position=new Rect(20,35,1180,650);
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();settings.GraphMotion=QuantumGraphMotionMode.Off;settings.GraphPalette=QuantumGraphPalette.CosmicNebula;settings.GraphQuality=quality;settings.GraphWorkMilliseconds=4;window.Settings=settings;
            var root=window.rootVisualElement;root.AddToClassList("quantum-root");root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath("802c743417758e14caea778717cb3bf3")));
            var graph=new QuantumGraphView(settings,_=>{},_=>{},_=>{},_=>{},_=>{},a=>a(),_=>{});window.Graph=graph;root.Add(graph);window.ShowUtility();
            var watch=Stopwatch.StartNew();
            if(members==42||members==0)
            {
                var prefab=members==0?GameObject.Find("SHOWCASE_MultiComponentObject"):AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightSongStudio/QuantumInspector/Demo/GraphFixtures/DenseComponentFixture.prefab");
                if(prefab==null)throw new InvalidOperationException("Open LivingLaboratory with SHOWCASE_MultiComponentObject for the Small fixture.");
                var scope=new WheelScope{Kind=WheelScopeKind.Components,Object=prefab};stateKey=QuantumGraphDataProvider.KeyFor(scope,false,settings);previous=QuantumGraphStatePersistence.instance.Get(stateKey);graph.Bind(scope,false,settings);
            }
            else
            {
                var document=(QuantumGraphDocument)typeof(QuantumGraphConstellationVerification).GetMethod("Synthetic",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{members,false});
                // New title strings exercise cold text measurement rather than reusing a warmed benchmark cache.
                string run=Guid.NewGuid().ToString("N").Substring(0,6);
                foreach(var node in document.Nodes.Values)node.Label+=" "+run;
                typeof(QuantumGraphDataProvider).GetProperty("Document").SetValue(graph.Provider,document);graph.Provider.Configure(settings);
                stateKey=document.Key;previous=null;
                graph.Configure(settings);typeof(QuantumGraphView).GetMethod("CommitLayout",Private).Invoke(graph,new object[]{true,false});
            }
            initial=watch.Elapsed.TotalMilliseconds;
            return new{ready=true,nodes=graph.Document.Nodes.Count,edges=graph.Document.Edges.Count,setupMs=initial};
        }
        public static object Complete()
        {
            if(window==null)throw new InvalidOperationException("Call Begin, allow an Editor UI frame, then Complete.");
            var graph=window.Graph;
            try
            {
                double maximumTick=0;int slices=0;
                var preparing=typeof(QuantumGraphView).GetField("preparing",Private);
                for(int i=0;i<1000;i++)
                {
                    var watch=Stopwatch.StartNew();graph.Tick(.016f);maximumTick=Math.Max(maximumTick,watch.Elapsed.TotalMilliseconds);slices++;
                    if(preparing.GetValue(graph)==null&&!graph.Provider.Busy)break;
                }
                if(preparing.GetValue(graph)!=null)throw new InvalidOperationException("Preparation failed to finish.");
                graph.Fit();int overviewNodes=graph.VisibleCount,overviewEdges=graph.VisibleEdgeCount;
                var member=graph.Document.Nodes.Values.FirstOrDefault(n=>n.Kind==QuantumGraphNodeKind.Component&&n.ClusterId!=null&&graph.Document.Nodes[n.ClusterId].FamilyKey=="Scripting")??graph.Document.Root;
                graph.Camera.Zoom=1;graph.Camera.Center(member.Position);graph.Moved(false);
                graph.Moved(false);double maximumPan=0,totalPan=0;long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<30;i++)
                {var watch=Stopwatch.StartNew();graph.Camera.Pan+=new Vector2(2,1);graph.Moved(false);double ms=watch.Elapsed.TotalMilliseconds;maximumPan=Math.Max(maximumPan,ms);totalPan+=ms;}
                long allocated=GC.GetAllocatedBytesForCurrentThread()-before;graph.SaveState();
                int views=graph.ViewUpdates;before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)graph.Tick(.033f);long idle=GC.GetAllocatedBytesForCurrentThread()-before;
                if(graph.ViewUpdates!=views)throw new InvalidOperationException("Idle presentation kept updating.");
                bool allocations=QuantumGraphConstellationVerification.AllocationCounterSupported();
                return new{passed=true,unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,memoryMB=SystemInfo.systemMemorySize,nodes=graph.Document.Nodes.Count,edges=graph.Document.Edges.Count,motion="Off",quality=window.Settings.GraphQuality.ToString(),viewportWidth=graph.Viewport.contentRect.width,viewportHeight=graph.Viewport.contentRect.height,setupMs=initial,slices,maximumScheduledTickMs=maximumTick,maximumTextPreparationSliceMs=graph.MaximumPreparationSliceMs,overviewNodes,overviewEdges,inspectionNodes=graph.VisibleCount,inspectionEdges=graph.VisibleEdgeCount,panAverageMs=totalPan/30,panMaximumMs=maximumPan,maximumArtCallbackMs=graph.MaximumDrawMilliseconds,drawBreakdownMs=graph.DrawMetrics,backdropGenerationMs=graph.BackdropGenerationMilliseconds,allocationCounterSupported=allocations,panAllocatedBytesPerAction=allocations?(long?)(allocated/30):null,idleCalls=1000,idleAllocatedBytes=allocations?(long?)idle:null,idleViewUpdates=graph.ViewUpdates-views,measurementScope="Main-thread graph preparation / UI binding and art submission callback only; excludes asynchronous native tessellation and GPU frame time"};
            }
            finally{window.Close();window=null;if(previous!=null)QuantumGraphStatePersistence.instance.Store(previous);else QuantumGraphStatePersistence.instance.Forget(stateKey);previous=null;stateKey=null;}
        }
    }
}
