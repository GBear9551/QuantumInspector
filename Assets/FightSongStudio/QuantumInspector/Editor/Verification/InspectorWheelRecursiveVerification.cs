using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    [Serializable] public sealed class RecursiveFixtureData
    {
        public float Damage=25;
        public List<Vector3> Points=new List<Vector3>{Vector3.one,Vector3.zero};
    }
    public sealed class RecursiveParameterFixture:ScriptableObject
    {
        [Range(0,100)] public int Number=10;
        public bool Enabled=true;
        public LightType Mode;
        public Vector3 Direction;
        public Color Tint=Color.cyan;
        public Gradient Gradient=new Gradient();
        public Object Reference;
        public RecursiveFixtureData Data=new RecursiveFixtureData();
        public List<RecursiveFixtureData> Items=new List<RecursiveFixtureData>{new RecursiveFixtureData(),new RecursiveFixtureData()};
        [NonSerialized] public int RuntimeField;
        public int RuntimeProperty=>42;
    }

    public static class InspectorWheelRecursiveVerification
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static object Typography()
        {
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            var restored=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            InspectorWheelView wheel=null;
            try
            {
                var small=QuantumGraphNodeSizing.Fit("Component information",settings);
                float originalText=settings.GraphFontSize*QuantumGraphNodeSizing.DisplayScale(.2f,settings.GraphFontSize);
                settings.GraphFontSize=24;settings.ButtonFontSize=18;settings.OrbitalFontScale=2;
                var large=QuantumGraphNodeSizing.Fit("Component information",settings);
                Check(large.x>small.x&&large.y>small.y,"Graph nodes failed to grow with text.");
                Check(settings.GraphFontSize*QuantumGraphNodeSizing.DisplayScale(.2f,settings.GraphFontSize)>originalText,"Overview scaling cancelled the font increase.");
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(settings),restored);
                Check(restored.GraphFontSize==24&&restored.ButtonFontSize==18&&restored.OrbitalFontScale==2,"Independent font preferences did not serialize.");
                var controller=new InspectorWheelController();
                wheel=new InspectorWheelView(controller,settings,_=>{},(_,__)=>{},_=>{},_=>{},_=>{});
                var entries=Enumerable.Range(0,8).Select(i=>new OrbitalEntry{Name="Node "+i,TypeName="Component information",Ordinal=i,Kind=OrbitalEntryKind.Parameter}).ToList();
                for(int capacity=5;capacity<=8;capacity++)
                {
                    settings.MaximumVisibleNodes=capacity;wheel.Configure(settings);controller.SetCount(entries.Count);
                    wheel.Present(new WheelScope{Kind=WheelScopeKind.SceneRoots},entries,entries.Count,new List<ResearchRelationship>());
                    for(int tick=0;tick<180;tick++)wheel.Tick(1f/60);
                    Check(wheel.Nodes[0].style.width.value.value>112,"Wheel nodes failed to grow with text.");
                    Check(wheel.Nodes[0].Q<Label>(className:"node-name").style.fontSize.value.value==26&&wheel.Nodes[0].Q<Label>(className:"node-meta").style.fontSize.value.value==20,"Wheel did not preserve the preset title/detail proportions.");
                    Check(2*wheel.Radius*Mathf.Sin(Mathf.PI/capacity)>wheel.NodeDiameter,"Large-font wheel nodes overlapped their neighbours.");
                }
                settings.OrbitalFontScale=3;wheel.Configure(settings);
                QuantumInspectorTypography.ApplyButtons(wheel,settings.ButtonFontSize);
                Check(wheel.Nodes[0].style.width.value.value==336&&wheel.Nodes[0].Q<Label>(className:"node-name").style.fontSize.value.value==39,"Wheel did not support 3x scaling.");
                Check(wheel.Back.style.fontSize.value.value==settings.ButtonFontSize*3,"Hub button font did not scale or was overwritten by global typography.");
                Check(wheel.Search.style.height.value.value==78&&wheel.Search.Q<TextField>().style.fontSize.value.value==36,"Search field did not scale with the hub.");
                Check(wheel.Search.Q<Button>().style.fontSize.value.value==42,"Search clear button did not scale.");
                settings.OrbitalFontScale=1;wheel.Configure(settings);
                Check(wheel.Nodes[0].Q<Label>(className:"node-name").style.fontSize.value.value==13&&wheel.Nodes[0].Q<Label>(className:"node-meta").style.fontSize.value.value==10,"Returning to 1x did not restore the preset fonts.");
                Check(wheel.Nodes[0].style.width.value.value==112,"Returning to 1x did not restore preset node dimensions.");
                var controls=new VisualElement();var button=new Button();controls.Add(button);
                var native=new VisualElement();native.AddToClassList("orbital-native-body");controls.Add(native);
                var nativeButton=new Button();nativeButton.style.fontSize=11;native.Add(nativeButton);
                QuantumInspectorTypography.ApplyButtons(controls,settings.ButtonFontSize);
                Check(button.style.fontSize.value.value==18&&nativeButton.style.fontSize.value.value==11,"Button typography leaked into native Inspector controls.");
                Check(settings.GraphFontSize==24,"Button typography changed node information size.");
                return new{passed=true,graphGrows=true,wheelGrows=true,capacities="5–8",independentSavedFonts=true,nativeInspectorPreserved=true};
            }
            finally{wheel?.Dispose();Object.DestroyImmediate(settings);Object.DestroyImmediate(restored);}
        }
        public static object Run()
        {
            var originalSelection=Selection.activeObject;
            var existing=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>();
            var snapshots=existing.Select(EditorJsonUtility.ToJson).ToArray();
            foreach(var existingWindow in existing)existingWindow.Pinned=true;
            var scene=EditorSceneManager.NewPreviewScene();
            var fixture=ScriptableObject.CreateInstance<RecursiveParameterFixture>();
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            InspectorWheelWindow window=null;
            try
            {
                var entries=SerializedParameterService.Children(fixture,"");
                Check(entries.Any(e=>e.PropertyPath=="Number")&&entries.Any(e=>e.PropertyPath=="Gradient"),"Typed fields missing.");
                Check(!entries.Any(e=>e.PropertyPath.Contains("Runtime")),"Nonserialized telemetry leaked into editable fields.");
                Check(entries.Single(e=>e.PropertyPath=="Data").CanExpand,"Nested structure not expandable.");
                var nested=SerializedParameterService.Children(fixture,"Data");
                Check(nested.Count==2&&nested.Any(e=>e.PropertyPath=="Data.Damage"),"Nested discovery leaked descendant fields.");
                var list=SerializedParameterService.Children(fixture,"Items");
                Check(list.Count==3&&list[0].PropertyPath=="Items.Array.size"&&list[2].PropertyPath=="Items.Array.data[1]","Collection discovery failed.");
                Check(SerializedParameterService.ParentPath("Items.Array.data[0].Points.Array.data[1]")=="Items.Array.data[0].Points","Array parent failed.");
                Check(SerializedParameterService.ParentPath("Items.Array.data[0].Damage")=="Items.Array.data[0]","Struct parent failed.");
                using(var serialized=new SerializedObject(fixture))
                {serialized.FindProperty("Data.Damage").floatValue=71;serialized.ApplyModifiedProperties();}
                Undo.FlushUndoRecordObjects();Check(fixture.Data.Damage==71,"Serialized editing failed.");
                Undo.PerformUndo();Check(fixture.Data.Damage==25,"Serialized Undo failed.");
                Undo.PerformRedo();Check(fixture.Data.Damage==71,"Serialized Redo failed.");

                var go=ObjectFactory.CreateGameObject(scene,HideFlags.None,"Quantum recursive verification");
                var a=ComponentCatalogService.Add(go,typeof(AudioSource),out string failure);
                var b=ComponentCatalogService.Add(go,typeof(AudioSource),out failure);
                Check(a!=null&&b!=null&&a!=b,"Multiple component instances were confused: "+failure);
                Check(ComponentCatalogService.CanRemove(go.transform)!=null,"Transform removal allowed.");
                Check(ComponentCatalogService.Remove(b,out failure)&&a!=null&&go.GetComponents<AudioSource>().Length==1,"Exact instance removal failed: "+failure);
                Undo.PerformUndo();Check(go.GetComponents<AudioSource>().Length==2,"Component removal Undo failed.");
                Undo.PerformRedo();Check(go.GetComponents<AudioSource>().Length==1,"Component removal Redo failed.");
                var joint=ComponentCatalogService.Add(go,typeof(HingeJoint),out failure);
                Check(joint!=null&&go.GetComponent<Rigidbody>()!=null,"Required component not installed: "+failure);
                Check(ComponentCatalogService.CanRemove(go.GetComponent<Rigidbody>())!=null,"Required Rigidbody removal allowed.");
                Undo.PerformUndo();Check(go.GetComponent<HingeJoint>()==null&&go.GetComponent<Rigidbody>()==null,"Dependency installation was not one Undo operation.");
                var scriptTypes=ComponentCatalogService.All.Where(c=>typeof(MonoBehaviour).IsAssignableFrom(c.Type)).ToArray();
                Check(scriptTypes.Length>0&&scriptTypes.All(c=>!c.Type.Assembly.GetName().Name.EndsWith(".Editor",StringComparison.Ordinal)),"Catalog script discovery is not runtime-safe.");

                window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;
                typeof(InspectorWheelWindow).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(window,settings);
                window.Show();window.CreateGUI();window.Inspect(go);window.SetQuery("audio");window.Controller.RestorePosition(-1.25);
                window.ActivateEntry(window.Entries.Single(e=>e.Component==a));
                Check(window.CurrentScope.Kind==WheelScopeKind.ComponentFocus&&window.SelectedComponent==a&&window.Entries.All(e=>e.Kind==OrbitalEntryKind.Parameter),"Component focus did not create a parameter orbit.");
                Check(window.Wheel.AddComponentButton.parent.ClassListContains("quantum-hidden"),"GameObject actions leaked into component hub.");
                var rotation=window.Controller.Position;var focusQuery=window.CurrentScope.Query;
                window.Wheel.SetComponentInspector(true);
                for(int tick=0;tick<100;tick++)window.Wheel.Tick(1f/60);
                Check(window.Wheel.ComponentInspectorOpen&&!window.Wheel.ComponentInspectorTransitioning,"Component inspector failed to settle.");
                var native=window.Wheel.ComponentInspector.ComponentEditor;
                Check(native!=null&&native.target==a&&window.rootVisualElement.Q("orbitalNativeComponentEditor")!=null,"Center did not host the exact component's native editor.");
                Check(window.Wheel.Nodes.All(n=>n.style.opacity.value==0),"Absorbed parameter nodes stayed visible.");
                Check(window.Wheel.ComponentInspector.Back.enabledSelf==window.Wheel.Back.enabledSelf,"Expanded center lost Back navigation.");
                float oldVolume=((AudioSource)a).volume;
                native.serializedObject.Update();native.serializedObject.FindProperty("m_Volume").floatValue=.42f;native.serializedObject.ApplyModifiedProperties();
                Undo.FlushUndoRecordObjects();Check(Mathf.Abs(((AudioSource)a).volume-.42f)<.001f,"Embedded editor did not edit its component.");
                Undo.PerformUndo();Check(Mathf.Abs(((AudioSource)a).volume-oldVolume)<.001f,"Embedded edit lost Undo.");
                window.Wheel.SetComponentInspector(false);
                for(int tick=0;tick<100;tick++)window.Wheel.Tick(1f/60);
                Check(window.Wheel.ComponentInspector.ComponentEditor==null&&!window.Wheel.OrbitInputBlocked,"Returning to orbit leaked the editor or blocked input.");
                Check(window.Controller.Position==rotation&&window.CurrentScope.Query==focusQuery,"Mode switch changed wheel location.");
                window.Wheel.SetComponentInspector(true);window.Wheel.Tick(.1f);
                var midFlight=window.Wheel.Nodes.Select(n=>n.Center).ToArray();window.Wheel.SetComponentInspector(false);
                Check(window.Wheel.Nodes.Select((n,i)=>Vector2.Distance(n.Center,midFlight[i])).All(d=>d<.01f),"Reversing absorption jumped node positions.");
                for(int tick=0;tick<100;tick++)window.Wheel.Tick(1f/60);
                Check(!window.Wheel.ComponentInspectorOpen&&!window.Wheel.ComponentInspectorTransitioning,"Interrupted absorption failed to return.");
                bool reduced=settings.ReducedMotion;settings.ReducedMotion=true;
                window.Wheel.SetComponentInspector(true);
                Check(window.Wheel.ComponentInspector.ComponentEditor!=null&&!window.Wheel.ComponentInspectorTransitioning,"Reduced motion failed to open immediately.");
                window.Wheel.SetComponentInspector(false);settings.ReducedMotion=reduced;
                var volume=window.Entries.First(e=>e.PropertyPath=="m_Volume");window.ActivateEntry(volume);
                Check(window.CurrentScope.SelectedProperty=="m_Volume"&&window.rootVisualElement.Q("parameterEditor")!=null,"Parameter editor did not appear.");
                for(int capacity=5;capacity<=8;capacity++)
                {
                    settings.MaximumVisibleNodes=capacity;window.Wheel.Configure(settings);window.Controller.SetCount(window.FilteredCount);
                    for(int tick=0;tick<180;tick++)window.Wheel.Tick(1f/60);
                    Check(window.Wheel.Nodes.Count(n=>n.Entry!=null)<=capacity,"Parameter capacity exceeded.");
                    Check(Mathf.Abs(window.Wheel.NodeDiameter-112*(1-.05f*(capacity-5)))<.001f,"Parameter scale rule failed.");
                }
                window.Wheel.SetComponentInspector(true);for(int tick=0;tick<100;tick++)window.Wheel.Tick(1f/60);
                window.MoveHistory(-1);Check(window.Wheel.ComponentInspector.ComponentEditor==null&&!window.Wheel.ComponentInspectorOpen,"Back retained the previous component editor.");
                Check(window.CurrentScope.Kind==WheelScopeKind.Components&&window.Wheel.Search.value=="audio"&&Math.Abs(window.Controller.Position-(-1.25*8/5))<.0001,"Back lost object scope/filter/rotation.");
                window.MoveHistory(1);Check(window.CurrentScope.IsComponentFocus&&window.CurrentScope.SelectedProperty=="m_Volume","Forward lost parameter selection.");
                window.GoToParent();Check(window.CurrentScope.Kind==WheelScopeKind.Components,"Hub Parent failed.");
                window.BeginRemoveComponent();window.SetQuery("");window.ActivateEntry(window.Entries.Single(e=>e.Component==go.transform));
                window.ConfirmRemoval();Check(go.transform!=null,"Blocked removal modified GameObject.");
                window.CancelComponentAction();window.BeginAddComponent();window.AddComponent(typeof(BoxCollider));
                Check(go.GetComponent<BoxCollider>()!=null&&window.CurrentScope.Kind==WheelScopeKind.Components,"Add catalog changed the hub or failed.");
                window.CancelComponentAction();window.Navigate(a);Object.DestroyImmediate(a);
                typeof(InspectorWheelWindow).GetMethod("RefreshData",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(window,null);
                Check(window.CurrentScope.Kind==WheelScopeKind.Components&&window.InspectedObject==go,"Destroyed focused component did not recover to its GameObject.");
                return new{serializedTypes=true,nestedCollections=true,undoRedo=true,exactInstanceRemoval=true,dependencyProtection=true,componentFocus=true,centerNativeInspector=true,absorptionReversible=true,embeddedEditingUndo=true,reducedMotion=true,parameterEditing=true,capacities="5–8",history=true,destroyedFocusRecovery=true,catalogTypes=ComponentCatalogService.All.Count};
            }
            finally
            {
                if(window!=null)window.Close();Undo.ClearUndo(fixture);Object.DestroyImmediate(fixture);Object.DestroyImmediate(settings);
                EditorSceneManager.ClosePreviewScene(scene);
                for(int i=0;i<existing.Length;i++)if(existing[i]!=null){EditorJsonUtility.FromJsonOverwrite(snapshots[i],existing[i]);existing[i].CreateGUI();}
                Selection.activeObject=originalSelection;
            }
        }
    }
}
