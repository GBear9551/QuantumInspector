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
                var volume=window.Entries.First(e=>e.PropertyPath=="m_Volume");window.ActivateEntry(volume);
                Check(window.CurrentScope.SelectedProperty=="m_Volume"&&window.rootVisualElement.Q("parameterEditor")!=null,"Parameter editor did not appear.");
                for(int capacity=5;capacity<=8;capacity++)
                {
                    settings.MaximumVisibleNodes=capacity;window.Wheel.Configure(settings);window.Controller.SetCount(window.FilteredCount);
                    for(int tick=0;tick<180;tick++)window.Wheel.Tick(1f/60);
                    Check(window.Wheel.Nodes.Count(n=>n.Entry!=null)<=capacity,"Parameter capacity exceeded.");
                    Check(Mathf.Abs(window.Wheel.NodeDiameter-112*(1-.05f*(capacity-5)))<.001f,"Parameter scale rule failed.");
                }
                window.MoveHistory(-1);Check(window.CurrentScope.Kind==WheelScopeKind.Components&&window.Wheel.Search.value=="audio"&&Math.Abs(window.Controller.Position-(-1.25*8/5))<.0001,"Back lost object scope/filter/rotation.");
                window.MoveHistory(1);Check(window.CurrentScope.IsComponentFocus&&window.CurrentScope.SelectedProperty=="m_Volume","Forward lost parameter selection.");
                window.GoToParent();Check(window.CurrentScope.Kind==WheelScopeKind.Components,"Hub Parent failed.");
                window.BeginRemoveComponent();window.SetQuery("");window.ActivateEntry(window.Entries.Single(e=>e.Component==go.transform));
                window.ConfirmRemoval();Check(go.transform!=null,"Blocked removal modified GameObject.");
                window.CancelComponentAction();window.BeginAddComponent();window.AddComponent(typeof(BoxCollider));
                Check(go.GetComponent<BoxCollider>()!=null&&window.CurrentScope.Kind==WheelScopeKind.Components,"Add catalog changed the hub or failed.");
                window.CancelComponentAction();window.Navigate(a);Object.DestroyImmediate(a);
                typeof(InspectorWheelWindow).GetMethod("RefreshData",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(window,null);
                Check(window.CurrentScope.Kind==WheelScopeKind.Components&&window.InspectedObject==go,"Destroyed focused component did not recover to its GameObject.");
                return new{serializedTypes=true,nestedCollections=true,undoRedo=true,exactInstanceRemoval=true,dependencyProtection=true,componentFocus=true,parameterEditing=true,capacities="5–8",history=true,destroyedFocusRecovery=true,catalogTypes=ComponentCatalogService.All.Count};
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
