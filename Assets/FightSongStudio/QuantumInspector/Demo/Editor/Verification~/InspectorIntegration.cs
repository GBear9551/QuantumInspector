var settings=UnityEngine.ScriptableObject.CreateInstance<FightSong.InspectorTool.InspectorWheelSettings>();
try
{
    settings.GraphOverlays=FightSong.InspectorTool.QuantumGraphOverlays.None;
    var root=UnityEngine.GameObject.Find("QuantumInspector_LivingLaboratory");
    var wheel=new FightSong.InspectorTool.HierarchyOrbitalProvider().Discover(new FightSong.InspectorTool.WheelScope{Kind=FightSong.InspectorTool.WheelScopeKind.Children,Object=root},null,settings);
    if(wheel.Count!=7) throw new System.Exception("Wheel did not discover seven subsystems.");
    var cross=root.transform.Find("06_DemoDocumentation/NavigationExamples/SHOWCASE_CrossReferencedSystems").gameObject;
    using(var provider=new FightSong.InspectorTool.QuantumGraphDataProvider())
    {
        var document=provider.Open(new FightSong.InspectorTool.WheelScope{Kind=FightSong.InspectorTool.WheelScopeKind.Components,Object=cross},false,settings);
        var guide=cross.GetComponent<FightSongStudio.QuantumInspector.Demo.DemoGuide>();
        var node=System.Linq.Enumerable.First(document.Nodes.Values,n=>n.Target==guide);
        provider.RevealReferences(node,FightSong.InspectorTool.QuantumGraphOverlays.SerializedReferences);
        for(int i=0;i<1000 && provider.Busy;i++)provider.Tick();
        int overlays=System.Linq.Enumerable.Count(document.Edges.Values,e=>e.Overlay&&e.From==node.Id);
        if(provider.Busy || overlays!=5)throw new System.Exception("Expected five actual serialized graph references: "+overlays);
        var project=new FightSong.InspectorTool.ProjectOrbitalProvider().Discover(new FightSong.InspectorTool.WheelScope{Kind=FightSong.InspectorTool.WheelScopeKind.ProjectFolder,Folder="Assets/FightSongStudio/QuantumInspector/Demo/Scenes"},null,settings);
        bool sceneFound=System.Linq.Enumerable.Any(project,e=>e.AssetPath.EndsWith("/Demo.unity"));
        if(!sceneFound)throw new System.Exception("Project provider did not find Demo.");
        return new {wheelSubsystems=wheel.Count, graphSerializedReferenceEdges=overlays, graphNodes=document.Nodes.Count, graphComplete=!provider.Busy, projectSceneFound=sceneFound, status=provider.Status};
    }
}
finally {UnityEngine.Object.DestroyImmediate(settings);}
