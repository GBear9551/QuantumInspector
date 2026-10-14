using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public static class InspectorWheelNavigationVerification
    {
        public static object Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Run in Edit Mode.");
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            var originalSelection=Selection.activeObject;
            var windows=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w=>w,w=>EditorJsonUtility.ToJson(w));
            var scene=EditorSceneManager.NewPreviewScene();
            InspectorWheelWindow window=null;
            string verificationRoot=System.IO.Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath("fe2560ffaebf82843b4e76c491d97012")).Replace('\\','/');
            string folder=verificationRoot+"/Navigation_"+Guid.NewGuid().ToString("N");
            try
            {
                int samples=VerifyCapacities(settings);
                var root=new GameObject("Wheel root");SceneManager.MoveGameObjectToScene(root,scene);
                var child=new GameObject("Child alpha");child.transform.SetParent(root.transform);
                var leaf=new GameObject("Child beta");leaf.transform.SetParent(root.transform);
                var grandchild=new GameObject("Grandchild");grandchild.transform.SetParent(child.transform);
                root.AddComponent<AudioSource>();root.AddComponent<AudioSource>();
                var hierarchy=new HierarchyOrbitalProvider();var graph=new ReferenceGraphService();
                var children=hierarchy.Discover(new WheelScope{Kind=WheelScopeKind.Children,Object=root},graph,settings);
                Check(children.Count==2&&children.All(e=>e.Kind==OrbitalEntryKind.GameObject&&e.Component==null),"Child provider exposed components or grandchildren.");
                Check(ComponentDiscoveryService.Filter(children,"ALPHA").Single().Target==child,"Child name filtering failed.");
                AssetDatabase.CreateFolder(verificationRoot,System.IO.Path.GetFileName(folder));
                AssetDatabase.CreateFolder(folder,"Nested");
                var texture=new Texture2D(4,8);AssetDatabase.CreateAsset(texture,folder+"/Texture.asset");
                var data=ScriptableObject.CreateInstance<InspectorWheelSettings>();AssetDatabase.CreateAsset(data,folder+"/Data.asset");
                AssetDatabase.CreateAsset(new Texture2D(2,2),folder+"/Nested/Deeper.asset");
                var project=new ProjectOrbitalProvider();
                var scope=new WheelScope{Kind=WheelScopeKind.ProjectFolder,Folder=folder,FolderGuid=AssetDatabase.AssetPathToGUID(folder)};
                var assets=project.Discover(scope,graph,settings);
                Check(assets.Count==3&&assets.All(e=>e.Kind!=OrbitalEntryKind.Component&&e.Component==null),"Project scope leaked recursive assets or represented assets as components.");
                Check(assets[0].Kind==OrbitalEntryKind.Folder,"Project folders should precede assets.");
                Check(ComponentDiscoveryService.Filter(assets,"texture").Single().TypeName=="Texture","Asset name/type filter failed.");
                Check(assets.Single(e=>e.Name=="Data").TypeName=="ScriptableObject","ScriptableObject asset type missing.");
                Check(AssetInformation.Describe(texture).Any(s=>s.Contains("4 × 8")),"Texture-specific information missing.");
                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;
                typeof(InspectorWheelWindow).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(window,settings);
                window.Show();window.CreateGUI();
                window.Inspect(root);window.SetQuery("audio");window.Controller.RestorePosition(-1);
                window.ToggleChildren();
                Check(window.CurrentScope.Kind==WheelScopeKind.Children&&window.FilteredCount==2,"Children switch failed.");
                window.ActivateEntry(window.Entries.Single(e=>e.Target==child));
                Check(window.CurrentScope.Kind==WheelScopeKind.Components&&window.InspectedObject==child,"GameObject selection did not expose components.");
                window.GoToParent();Check(window.CurrentScope.Kind==WheelScopeKind.Children&&window.InspectedObject==root,"Parent scope failed.");
                window.Inspect(leaf);
                Check(window.Wheel.ChildrenButton.ClassListContains("quantum-hidden"),"Leaf object displays a children button.");
                window.Inspect(root);Check(!window.Wheel.ChildrenButton.ClassListContains("quantum-hidden"),"Parent lacks a children button.");
                window.SetQuery("audio");window.Controller.RestorePosition(-1.25);
                window.OpenProjectFolder(folder);window.SetQuery("texture");window.Controller.RestorePosition(-.75);
                window.ActivateEntry(window.Entries.Single());
                Check(window.CurrentScope.Asset==texture&&window.InspectedObject==null&&window.SelectedComponent==null,"Asset selection contaminated component context.");
                window.SwitchTab(InspectorNavigationTab.Hierarchy);
                Check(window.InspectedObject==root&&window.Wheel.Search.value=="audio"&&window.Controller.Position== -1.25,"Hierarchy tab state was not retained.");
                window.SwitchTab(InspectorNavigationTab.Project);
                Check(window.CurrentScope.FolderPath==folder&&window.CurrentScope.Asset==texture&&window.Wheel.Search.value=="texture"&&window.Controller.Position== -.75,"Project tab state was not retained.");
                window.SetQuery("");window.ActivateEntry(window.Entries.Single(e=>e.Kind==OrbitalEntryKind.Folder));
                Check(window.CurrentScope.FolderPath==folder+"/Nested"&&window.FilteredCount==1,"Folder entry failed.");
                window.MoveHistory(-1);Check(window.CurrentScope.FolderPath==folder&&window.CurrentScope.Asset==texture,"Project Back failed.");
                window.MoveHistory(1);Check(window.CurrentScope.FolderPath==folder+"/Nested","Project Forward failed.");
                window.GoToParent();Check(window.CurrentScope.FolderPath==folder,"Project parent failed.");
                window.SetQuery("texture");
                string serialized=EditorJsonUtility.ToJson(window);window.OpenProjectFolder("Assets");
                EditorJsonUtility.FromJsonOverwrite(serialized,window);
                typeof(InspectorWheelWindow).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(window,settings);
                window.CreateGUI();
                Check(window.CurrentScope.FolderPath==folder&&window.Wheel.Search.value=="texture","Serialized tab state was not restored.");
                string renamed=folder+"_Moved";
                Check(System.IO.Path.GetFullPath(renamed).StartsWith(System.IO.Path.GetFullPath(verificationRoot)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Fixture move escaped its verification folder.");
                Check(string.IsNullOrEmpty(AssetDatabase.MoveAsset(folder,renamed)),"Fixture move failed.");folder=renamed;project.Invalidate();
                Check(window.CurrentScope.FolderPath==renamed,"Folder identity did not survive a move.");
                window.Pinned=false;Selection.activeGameObject=child;DispatchSelection(window);
                Check(window.ActiveTab==InspectorNavigationTab.Hierarchy&&window.InspectedObject==child,"Scene selection did not switch to Hierarchy.");
                window.SwitchTab(InspectorNavigationTab.Project);
                Check(window.CurrentScope.FolderPath==renamed&&window.CurrentScope.Query=="texture","Automatic tab switching lost Project state.");
                window.SwitchTab(InspectorNavigationTab.Hierarchy);Selection.activeGameObject=leaf;DispatchSelection(window);
                Check(window.InspectedObject==leaf,"Hierarchy selection synchronization failed: inspected="+window.InspectedObject?.name+", selection="+Selection.activeGameObject?.name+", pinned="+window.Pinned+", tab="+window.ActiveTab);
                window.Pinned=true;Selection.activeGameObject=child;DispatchSelection(window);
                Check(window.InspectedObject==leaf,"Pin selection failed.");
                VerifyProjectSelection(window,settings,root,child,texture,data,folder);
                window.Pinned=true;window.Inspect(leaf);
                Object.DestroyImmediate(leaf);window.MoveHistory(-1);
                Check(window.CurrentScope.Available,"Destroyed history entry was not skipped.");
                return new{passed=true,rotationSamples=samples,capacities="5,6,7,8",nodeReuse=true,collisionFreeTransitions=true,
                    exactScaleFormula=true,childrenAndParents=true,projectScope=true,independentTabHistory=true,serializedState=true,
                    movedFolderIdentity=true,selectionSync=true,projectSelectionSync=true,selectionLock=true,projectGraphSelection=true};
            }
            finally
            {
                if(window!=null)window.Close();EditorSceneManager.ClosePreviewScene(scene);Selection.activeObject=originalSelection;
                foreach(var pair in windows)if(pair.Key!=null){EditorJsonUtility.FromJsonOverwrite(pair.Value,pair.Key);pair.Key.CreateGUI();}
                Object.DestroyImmediate(settings);
                if(AssetDatabase.IsValidFolder(folder)&&System.IO.Path.GetFullPath(folder).StartsWith(System.IO.Path.GetFullPath(verificationRoot)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))AssetDatabase.DeleteAsset(folder);
            }
        }
        private static void VerifyProjectSelection(InspectorWheelWindow window,InspectorWheelSettings settings,GameObject root,GameObject child,Texture2D texture,Object data,string folder)
        {
            var nested=AssetDatabase.LoadMainAssetAtPath(folder+"/Nested");
            window.Pinned=false;Selection.activeObject=nested;DispatchSelection(window);
            Check(window.ActiveTab==InspectorNavigationTab.Project&&window.CurrentScope.FolderPath==folder+"/Nested","Folder selection did not switch to its Project scope.");
            Selection.activeObject=data;DispatchSelection(window);
            Check(window.CurrentScope.FolderPath==folder&&window.CurrentScope.Asset==data&&window.InspectedObject==null,"Asset selection did not navigate to its containing folder.");
            window.SetQuery("does not match any asset");Selection.activeObject=texture;DispatchSelection(window);
            window.Wheel.Tick(.02f);
            Check(window.CurrentScope.Asset==texture&&window.Wheel.Search.value==""&&window.Wheel.Nodes.Any(n=>n.IsSelected&&n.Entry?.AssetPath==AssetDatabase.GetAssetPath(texture)),"Selected asset was hidden by filtering or not highlighted.");
            Selection.activeObject=nested;DispatchSelection(window);window.MoveHistory(-1);
            Check(window.CurrentScope.Asset==texture&&window.CurrentScope.FolderPath==folder,"Selection history did not return to the selected asset.");
            window.MoveHistory(1);Check(window.CurrentScope.FolderPath==folder+"/Nested","Selection feedback broke Forward history.");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/Prefab.prefab");
            Selection.activeObject=prefab;DispatchSelection(window);
            Check(window.ActiveTab==InspectorNavigationTab.Project&&window.CurrentScope.Asset==prefab&&window.InspectedObject==null,"Prefab asset selection was treated as a Hierarchy object.");
            var selectionLock=window.rootVisualElement.Q<Toggle>("selectionLock");
            Check(selectionLock!=null&&selectionLock.label=="Lock selection","Selection lock control is missing.");
            selectionLock.value=true;Selection.activeObject=texture;DispatchSelection(window);
            Check(window.CurrentScope.Asset==prefab,"Selection lock allowed an asset to replace the view.");
            Selection.activeGameObject=child;DispatchSelection(window);
            Check(window.ActiveTab==InspectorNavigationTab.Project&&window.CurrentScope.Asset==prefab,"Selection lock allowed a tab switch.");
            selectionLock.value=false;
            Check(window.ActiveTab==InspectorNavigationTab.Hierarchy&&window.InspectedObject==child,"Unlocking did not follow the current Unity selection.");
            Selection.activeObject=texture;DispatchSelection(window);
            window.SetGraphMode(true);settings.GraphInvestigationLock=false;window.GraphWorkspace.Configure(settings);
            Check(ReferenceEquals(typeof(InspectorWheelWindow).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(window),settings),"Verification window lost its isolated settings.");
            Selection.activeObject=data;DispatchSelection(window);DrainGraph(window.GraphWorkspace);
            Check(window.GraphWorkspace.Selected?.AssetPath==AssetDatabase.GetAssetPath(data),"Graph did not select the externally selected asset.");
            Selection.activeObject=nested;DispatchSelection(window);DrainGraph(window.GraphWorkspace);
            Check(window.GraphWorkspace.Document.Root.AssetPath==folder+"/Nested"&&window.GraphWorkspace.Selected==window.GraphWorkspace.Document.Root,"Graph did not follow the selected folder.");
            settings.GraphInvestigationLock=true;Selection.activeObject=texture;DispatchSelection(window);
            DrainGraph(window.GraphWorkspace);
            Check(window.CurrentScope.Asset==texture&&window.GraphWorkspace.Selected?.AssetPath==AssetDatabase.GetAssetPath(texture),"Investigation Lock blocked the selected Project texture.");
            Selection.activeObject=nested;window.ObserveSelection();DrainGraph(window.GraphWorkspace);
            Check(window.CurrentScope.FolderPath==folder+"/Nested"&&window.GraphWorkspace.Document.Root.AssetPath==folder+"/Nested","Investigation Lock blocked the selected Project folder.");
            Selection.activeObject=data;window.ObserveSelection();DrainGraph(window.GraphWorkspace);
            Check(window.CurrentScope.Asset==data&&window.GraphWorkspace.Selected?.AssetPath==AssetDatabase.GetAssetPath(data),"Investigation Lock blocked the selected ScriptableObject.");
            Selection.activeObject=prefab;window.ObserveSelection();DrainGraph(window.GraphWorkspace);
            Check(window.ActiveTab==InspectorNavigationTab.Project&&window.GraphWorkspace.Selected?.AssetPath==AssetDatabase.GetAssetPath(prefab),"Investigation Lock blocked the selected Project prefab.");
            window.Pinned=true;Selection.activeObject=texture;DispatchSelection(window);DrainGraph(window.GraphWorkspace);
            Check(window.CurrentScope.Asset==prefab,"Explicit selection lock allowed a Project selection under Investigation Lock.");
            selectionLock.value=false;DrainGraph(window.GraphWorkspace);
            Check(window.CurrentScope.Asset==texture&&window.GraphWorkspace.Selected?.AssetPath==AssetDatabase.GetAssetPath(texture),"Unlocking did not follow the latest Project asset under Investigation Lock.");
            Selection.activeGameObject=child;DispatchSelection(window);DrainGraph(window.GraphWorkspace);
            Check(window.ActiveTab==InspectorNavigationTab.Hierarchy&&window.GraphWorkspace.Document.Root.Target==child,"Clustered graph did not follow a Hierarchy selection under Investigation Lock.");
            Selection.activeGameObject=root;DispatchSelection(window);DrainGraph(window.GraphWorkspace);
            Check(window.GraphWorkspace.Document.Root.Target==root,"Clustered graph did not switch between selected GameObjects.");
            window.Pinned=true;Selection.activeGameObject=child;DispatchSelection(window);
            Check(window.GraphWorkspace.Document.Root.Target==root,"Selection lock allowed a GameObject selection to replace the graph.");
            selectionLock.value=false;DrainGraph(window.GraphWorkspace);
            Check(window.GraphWorkspace.Document.Root.Target==child,"Unlocking did not follow the selected GameObject under Investigation Lock.");
            settings.GraphInvestigationLock=false;window.GraphWorkspace.Configure(settings);
            window.Pinned=true;Selection.activeObject=nested;DispatchSelection(window);
            Check(window.GraphWorkspace.Document.Root.Target==child,"Selection lock allowed a graph root or tab change.");
            selectionLock.value=false;DrainGraph(window.GraphWorkspace);
            Check(window.ActiveTab==InspectorNavigationTab.Project&&window.CurrentScope.FolderPath==folder+"/Nested","Unlocking did not follow the latest Project selection in graph mode.");
            window.SetGraphMode(false);Selection.activeObject=null;DispatchSelection(window);
            Check(window.ActiveTab==InspectorNavigationTab.Project&&window.CurrentScope.FolderPath==folder+"/Nested","Clearing Unity selection discarded the Project view.");
            Selection.activeObject=texture;DispatchSelection(window);
            ClickProjectItem(window,folder+"/Nested");
            Check(Selection.activeObject==texture&&window.CurrentScope.FolderPath==folder+"/Nested","Left folder-tree click did not navigate independently of the active asset.");
            window.ObserveSelection();
            Check(window.CurrentScope.FolderPath==folder+"/Nested","Selection polling bounced back to the stale right-pane selection.");
            ClickProjectItem(window,AssetDatabase.GetAssetPath(texture));
            Check(window.CurrentScope.Asset==texture,"Clicking the already-selected right-pane asset did not restore its view.");
            window.Pinned=true;ClickProjectItem(window,folder+"/Nested");
            Check(window.CurrentScope.Asset==texture,"Folder-tree click bypassed the explicit selection lock.");
            selectionLock.value=false;
            Check(window.CurrentScope.FolderPath==folder+"/Nested","Unlocking ignored the most recent folder-tree click.");
            window.SetGraphMode(true);settings.GraphInvestigationLock=true;
            ClickProjectItem(window,AssetDatabase.GetAssetPath(data));DrainGraph(window.GraphWorkspace);
            Check(window.GraphWorkspace.Selected?.AssetPath==AssetDatabase.GetAssetPath(data),"Right-pane callback did not reveal an asset under Investigation Lock.");
            ClickProjectItem(window,folder+"/Nested");DrainGraph(window.GraphWorkspace);
            Check(window.GraphWorkspace.Document.Root.AssetPath==folder+"/Nested","Left folder-tree callback did not change the graph root under Investigation Lock.");
        }
        private static void ClickProjectItem(InspectorWheelWindow window,string path)
        {
            var e=new Event{type=EventType.MouseDown,button=0,mousePosition=new Vector2(30,10)};
            window.HandleProjectItemEvent(AssetDatabase.AssetPathToGUID(path),new Rect(0,0,100,20),e);
            window.FlushProjectItem();
        }
        private static void DrainGraph(QuantumGraphView graph)
        {
            int ticks=0;
            do{graph.Tick(.02f);}while(graph.Provider.Busy&&++ticks<10000);
            Check(!graph.Provider.Busy,"Graph expansion did not finish.");
        }
        private static int VerifyCapacities(InspectorWheelSettings settings)
        {
            int samples=0;
            for(int capacity=5;capacity<=8;capacity++)
            {
                settings.MaximumVisibleNodes=capacity;
                Check(Mathf.Approximately(settings.OrbitalNodeScale,1-(capacity-5)*.05f),"Incorrect capacity scale formula.");
                for(int count=0;count<=41;count++)
                {
                    var controller=new InspectorWheelController();controller.SetCapacity(capacity);controller.Reset(count);
                    var visited=new HashSet<int>();
                    for(int i=0;i<500;i++)
                    {
                        controller.Rotate(i<250?-.37:.61,true);
                        var visible=Enumerable.Range(0,capacity).Select(controller.IndexForSlot).Where(n=>n>=0).ToArray();
                        Check(visible.Length==Math.Min(count,capacity)&&visible.Distinct().Count()==visible.Length,"Adaptive wheel duplicated or omitted visible entries.");
                        Check(visible.All(n=>n<count),"Adaptive index out of range.");foreach(int n in visible)visited.Add(n);samples++;
                    }
                    Check(visited.Count==count,"Adaptive wheel failed to reach every entry.");
                }
            }
            settings.MaximumVisibleNodes=5;settings.NodeSize=WheelNodeSize.Large;settings.AnimationIntensity=1;settings.BobAmplitude=6;settings.MicroDrift=3;
            var rotation=new InspectorWheelController();rotation.Reset(30);
            var view=new InspectorWheelView(rotation,settings,_=>{},(_,__)=>{},_=>{},_=>{},_=>{});
            var entries=Enumerable.Range(0,30).Select(i=>new OrbitalEntry{Kind=OrbitalEntryKind.Asset,Name="Item "+i,TypeName="Asset",CategoryName="Asset",AssetPath="fixture/"+i}).ToList();
            view.Present(new WheelScope{Kind=WheelScopeKind.ProjectFolder},entries,entries.Count,new List<ResearchRelationship>());
            var firstNode=view.Nodes[0];
            foreach(int capacity in new[]{8,6,7,5,8})
            {
                settings.MaximumVisibleNodes=capacity;view.Configure(settings);
                Check(view.Nodes[0]==firstNode,"Capacity change recreated existing slots.");
                for(int tick=0;tick<180;tick++)
                {
                    view.Tick(1f/60);
                    var visible=view.Nodes.Where(n=>n.Entry!=null).ToArray();
                    Check(visible.Length<=capacity&&visible.Select(n=>n.Entry.Key).Distinct().Count()==visible.Length,"Transition duplicated entries.");
                    for(int i=0;i<visible.Length;i++)for(int j=i+1;j<visible.Length;j++)
                        Check(Vector2.Distance(visible[i].Center,visible[j].Center)>=view.NodeDiameter*(visible[i].Motion.Scale+visible[j].Motion.Scale)*.5f+2,"Nodes collided during capacity transition.");
                }
                Check(view.Nodes.Count(n=>n.Entry!=null)==capacity,"Capacity transition did not finish with all slots visible.");
                Check(Mathf.Abs(view.NodeDiameter-128*settings.OrbitalNodeScale)<.001f,"Node diameter did not settle to the requested scale.");
            }
            settings.ReducedMotion=true;settings.MaximumVisibleNodes=5;view.Configure(settings);view.Tick(.02f);
            Check(view.Nodes.Length==5&&view.Nodes.All(n=>n.Motion.Offset==Vector2.zero&&n.Motion.Scale==1),"Reduced motion broke adaptive layout.");
            return samples;
        }
        private static void DispatchSelection(InspectorWheelWindow window)=>typeof(InspectorWheelWindow).GetMethod("SelectionChanged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(window,null);
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    }
}
