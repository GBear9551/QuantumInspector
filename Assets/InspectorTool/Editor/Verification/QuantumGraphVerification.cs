using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    /// <summary>Explicit integration verification. All editable fixtures belong to a preview scene or a unique temporary asset folder.</summary>
    public static class QuantumGraphVerification
    {
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Drain(QuantumGraphDataProvider provider)
        {
            for(int i=0;i<30000&&provider.Busy;i++)provider.Tick();
            Check(!provider.Busy,"Progressive expansion failed to finish: "+provider.Status);
            Check(!provider.Status.Contains("retry"),provider.Status);
        }
        public static object Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Run in Edit Mode.");
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            settings.GraphItemsPerTick=32;settings.GraphWorkMilliseconds=12;
            var scene=EditorSceneManager.NewPreviewScene();
            var folder="Assets/Scripts/FightSong/InspectorTool/Editor/Verification/Graph_"+Guid.NewGuid().ToString("N");
            var providers=new List<QuantumGraphDataProvider>();
            InspectorWheelWindow window=null;QuantumGraphVerificationFixture fixture=null;
            var originalSelection=Selection.activeObject;
            var windows=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>w.Pinned);
            foreach(var pair in windows)pair.Key.Pinned=true;
            string persistentKey=null;
            try
            {
                var go=ObjectFactory.CreateGameObject(scene,HideFlags.None,"Graph root");
                for(int i=0;i<27;i++)go.AddComponent<AudioSource>();
                go.AddComponent<Rigidbody>();go.AddComponent<BoxCollider>();go.AddComponent<MeshRenderer>();
                var child=ObjectFactory.CreateGameObject(scene,HideFlags.None,"Referenced child");child.transform.SetParent(go.transform);
                var linkedBody=child.AddComponent<Rigidbody>();var joint=go.AddComponent<FixedJoint>();joint.connectedBody=linkedBody;
                var provider=new QuantumGraphDataProvider();providers.Add(provider);
                var scope=new WheelScope{Kind=WheelScopeKind.Components,Object=go};
                var doc=provider.Open(scope,false,settings);
                Check(doc.Nodes.Values.Count(n=>n.Kind==QuantumGraphNodeKind.Component)==go.GetComponents<Component>().Length,"Graph lost directly attached components.");
                Check(doc.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Component).All(n=>n.ClusterId!=null),"Missing primary cluster.");
                var layout=new QuantumGraphLayoutEngine();layout.Arrange(doc,settings);
                var clusters=doc.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Cluster).ToArray();
                Check(clusters.Single(n=>n.Label=="Audio").Radius>clusters.Single(n=>n.Label=="Physics").Radius,"Larger cluster did not receive more space.");
                for(int i=0;i<clusters.Length;i++)for(int j=i+1;j<clusters.Length;j++)
                    Check(Vector2.Distance(clusters[i].Position,clusters[j].Position)+1>=clusters[i].Radius+clusters[j].Radius,"Automatic clusters overlap.");
                var positions=doc.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);
                var moved=doc.Nodes.Values.First(n=>n.Target is AudioSource);moved.Position+=new Vector2(37,29);moved.Manual=true;positions[moved.Id]=moved.Position;
                go.AddComponent<Light>();provider.Refresh();layout.Arrange(doc,settings);
                Check(positions.All(p=>doc.Nodes[p.Key].Position==p.Value),"Refresh moved an existing node under Investigation Lock.");
                var audioCluster=clusters.Single(n=>n.Label=="Audio");provider.Collapse(audioCluster);
                Check(!doc.Visible(moved)&&doc.Visible(audioCluster),"Collapsed cluster did not replace members.");
                provider.Expand(audioCluster);Check(doc.Visible(moved),"Cluster expansion failed.");
                var jointNode=doc.Nodes.Values.Single(n=>n.Target==joint);
                provider.Expand(jointNode);Drain(provider);layout.Arrange(doc,settings);
                Check(doc.DirectChildren(jointNode.Id).Any(n=>n.Kind==QuantumGraphNodeKind.Parameter),"Component parameters missing.");
                provider.RevealReferences(jointNode,QuantumGraphOverlays.SerializedReferences);Drain(provider);
                Check(doc.Edges.Values.Any(e=>e.From==jointNode.Id&&doc.Nodes[e.To].Target==linkedBody&&e.Label.Contains("ConnectedBody")),"Serialized reference lost its precise source/target.");
                Check(doc.Nodes.Values.Where(n=>n.Target==linkedBody).All(doc.Visible),"Reference target hidden behind collapsed source.");
                settings.GraphCategoryRules.Add(new QuantumClusterRule{Match=QuantumRuleMatch.TypeName,Pattern=typeof(AudioSource).FullName,Category=QuantumClusterCategory.Scripting,AdditionalTags="Lesson"});
                provider.Configure(settings);
                Check(doc.Nodes[moved.Id].ClusterId.EndsWith("/Scripting"),"Custom category rule failed.");
                settings.GraphCategoryRules.Clear();

                var camera=new QuantumGraphViewportController{Pan=new Vector2(40,20),Zoom=.7f};
                var viewport=new Vector2(1000,600);var mouse=new Vector2(157,291);var world=camera.ToWorld(mouse,viewport);
                camera.ZoomAt(mouse,viewport,-4);
                Check(Vector2.Distance(world,camera.ToWorld(mouse,viewport))<.001f,"Zoom is not mouse centered.");
                layout.Reindex(doc);Check(layout.Spatial.Query(doc.Root.Bounds).Contains(doc.Root),"Spatial index missed visible root.");

                AssetDatabase.CreateFolder("Assets/Scripts/FightSong/InspectorTool/Editor/Verification",System.IO.Path.GetFileName(folder));
                AssetDatabase.CreateFolder(folder,"Nested");AssetDatabase.CreateAsset(new Texture2D(2,2),folder+"/Image.asset");
                AssetDatabase.CreateAsset(new Texture2D(2,2),folder+"/Nested/Hidden.asset");
                fixture=ScriptableObject.CreateInstance<QuantumGraphVerificationFixture>();fixture.Samples=new int[10025];fixture.Reference=AssetDatabase.LoadMainAssetAtPath(folder+"/Image.asset");
                AssetDatabase.CreateAsset(fixture,folder+"/Data.asset");
                var project=new QuantumGraphDataProvider();providers.Add(project);
                var projectScope=new WheelScope{Kind=WheelScopeKind.ProjectFolder,Folder=folder};
                var projectDoc=project.Open(projectScope,false,settings);Drain(project);
                Check(projectDoc.DirectChildren(projectDoc.RootId).Count()==3,"Local directory is not limited to immediate children.");
                Check(!projectDoc.Nodes.Values.Any(n=>n.AssetPath==folder+"/Nested/Hidden.asset"),"Project eagerly expanded descendants.");
                var nested=projectDoc.Nodes.Values.Single(n=>n.AssetPath==folder+"/Nested");project.Expand(nested);Drain(project);
                Check(projectDoc.Nodes.Values.Any(n=>n.AssetPath==folder+"/Nested/Hidden.asset"&&n.ParentId==nested.Id),"Folder expansion lost real hierarchy.");
                var data=projectDoc.Nodes.Values.Single(n=>n.AssetPath==folder+"/Data.asset");project.Expand(data);Drain(project);
                Check(!projectDoc.DirectChildren(data.Id).Any(n=>n.Label.Contains("RuntimeOnly")),"Runtime-only value exposed as editable parameter.");
                var samples=projectDoc.DirectChildren(data.Id).Single(n=>n.PropertyPath=="Samples");project.Expand(samples);project.Tick();
                Check(project.Busy&&projectDoc.DirectChildren(samples.Id).Count()<=settings.GraphItemsPerTick,"Array expansion ignored processing budget.");
                Drain(project);
                Check(projectDoc.DirectChildren(samples.Id).Count()==10026,"A fixed logical parameter limit was applied.");
                project.RevealReferences(data,QuantumGraphOverlays.SerializedReferences);Drain(project);
                Check(projectDoc.Edges.Values.Any(e=>e.From==data.Id&&e.Label=="Reference"&&projectDoc.Nodes[e.To].AssetPath==folder+"/Image.asset"),"Asset serialized relationship missing.");
                var global=new QuantumGraphDataProvider();providers.Add(global);
                var globalDoc=global.Open(projectScope,true,settings);Drain(global);
                Check(globalDoc.Root.AssetPath=="Assets"&&globalDoc.Nodes.Values.All(n=>n.Id==globalDoc.RootId||System.IO.Path.GetDirectoryName(n.AssetPath)?.Replace('\\','/')=="Assets"),"Entire Project loaded nested descendants eagerly.");
                settings.GraphIncludePackages=true;globalDoc=global.Open(projectScope,true,settings);Drain(global);
                Check(globalDoc.Root.Kind==QuantumGraphNodeKind.World&&globalDoc.DirectChildren(globalDoc.RootId).Any(n=>n.AssetPath=="Packages"),"Packages is not a separate root.");
                settings.GraphIncludePackages=false;

                persistentKey=projectDoc.Key;
                layout.Arrange(projectDoc,settings);
                data.Position=new Vector2(812,-427);data.Manual=data.Pinned=true;
                QuantumGraphStatePersistence.instance.Store(new QuantumSavedGraph{Key=persistentKey,Pan=new Vector2(67,-29),Zoom=.8f,HasViewport=true,Selected=data.Id,Search="Data",
                    Nodes=projectDoc.Nodes.Values.Select(n=>new QuantumSavedNode{Id=n.Id,Position=n.Position,Positioned=n.Positioned,Size=n.Size,Manual=n.Manual,Pinned=n.Pinned,Expanded=n.Expanded}).ToList()});
                var reload=new QuantumGraphDataProvider();providers.Add(reload);var restored=reload.Open(projectScope,false,settings);Drain(reload);
                Check(restored.Nodes[data.Id].Position==data.Position&&restored.Nodes[data.Id].Pinned,"Manual node layout did not reload.");
                Check(restored.Nodes[samples.Id].Expanded&&restored.DirectChildren(samples.Id).Count()==10026,"Expanded recursive state did not reload.");

                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;
                typeof(InspectorWheelWindow).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,settings);
                window.ShowUtility();window.CreateGUI();window.Inspect(go);window.SetGraphMode(true);
                var workspace=window.GraphWorkspace;var rootId=workspace.Document.RootId;
                var componentNode=workspace.Document.Nodes.Values.First(n=>n.Target is AudioSource);
                workspace.Select(componentNode);workspace.Provider.Expand(componentNode);Drain(workspace.Provider);workspace.Layout.Arrange(workspace.Document,settings);
                var volume=workspace.Document.DirectChildren(componentNode.Id).Single(n=>n.PropertyPath=="m_Volume");
                workspace.Select(volume);
                Check(workspace.Document.RootId==rootId&&window.CurrentScope.Kind==WheelScopeKind.Components&&window.rootVisualElement.Q("parameterEditor")!=null,"Parameter editor navigated away from graph root.");
                window.Pinned=false;Selection.activeObject=child;
                typeof(InspectorWheelWindow).GetMethod("SelectionChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                Check(workspace.Document.RootId==rootId,"Investigation Lock allowed Selection to replace the root.");
                window.Pinned=true;
                var group=workspace.Document.Nodes[componentNode.ClusterId];
                Check(workspace.DragSet(group).Contains(volume),"Cluster dragging omitted expanded parameters.");
                window.OpenProjectFolder(folder);Drain(workspace.Provider);
                window.OpenProjectFolder(folder+"/Nested");Drain(workspace.Provider);window.MoveHistory(-1);
                Check(window.CurrentScope.FolderPath==folder,"Project Back lost the directory scope.");
                window.SwitchTab(InspectorNavigationTab.Hierarchy);
                Check(workspace.Document.RootId==rootId,"Tab switch lost independent GameObject state.");
                window.SetGraphMode(false);
                for(int capacity=5;capacity<=8;capacity++)
                {
                    settings.MaximumVisibleNodes=capacity;settings.ReducedMotion=true;window.Wheel.Configure(settings);window.Wheel.Tick(.02f);
                    Check(window.Wheel.Nodes.Count(n=>n.Entry!=null)==capacity,"Graph changes broke wheel capacity.");
                }
                return new{passed=true,components=go.GetComponents<Component>().Length,expandedArrayNodes=10026,clusters=true,customRules=true,
                    stableLayout=true,preciseReferences=true,localAndEntireProject=true,progressiveExpansion=true,persistentLayout=true,mouseCenteredZoom=true,
                    parameterEditor=true,investigationLock=true,groupDragging=true,independentHistory=true,wheelCapacities="5–8"};
            }
            finally
            {
                if(window!=null)window.Close();
                foreach(var provider in providers)provider.Dispose();
                if(persistentKey!=null)QuantumGraphStatePersistence.instance.Forget(persistentKey);
                Selection.activeObject=originalSelection;
                foreach(var pair in windows)if(pair.Key!=null)pair.Key.Pinned=pair.Value;
                if(fixture!=null)Undo.ClearUndo(fixture);
                EditorSceneManager.ClosePreviewScene(scene);Object.DestroyImmediate(settings);
                if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
