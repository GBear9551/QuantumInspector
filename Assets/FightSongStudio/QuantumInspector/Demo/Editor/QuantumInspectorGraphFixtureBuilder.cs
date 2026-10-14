using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSongStudio.QuantumInspector.Demo.Editor
{
    public static class QuantumInspectorGraphFixtureBuilder
    {
        public const string Folder=QuantumInspectorDemoSceneBuilder.Root+"/GraphFixtures";
        public const string PrefabPath=Folder+"/DenseComponentFixture.prefab";
        [MenuItem("Tools/Quantum Inspector/Demo/Create Graph Trial Fixtures",false,110)]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder+"/Leaf");AssetDatabase.Refresh();
            if(!File.Exists(PrefabPath))
            {
                var scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    var go=ObjectFactory.CreateGameObject(scene,HideFlags.None,"SHOWCASE_DenseComponentFixture");
                    go.AddComponent<MeshFilter>();go.AddComponent<MeshRenderer>();go.AddComponent<BoxCollider>();go.AddComponent<SphereCollider>();
                    go.AddComponent<Rigidbody>().isKinematic=true;
                    var audio=go.AddComponent<AudioSource>();audio.playOnAwake=false;audio.enabled=false;
                    go.AddComponent<AudioLowPassFilter>();go.AddComponent<AudioHighPassFilter>();
                    go.AddComponent<Light>().enabled=false;
                    var sample=ObjectFactory.CreateGameObject(scene,HideFlags.None,"CalibrationTarget");sample.transform.SetParent(go.transform);
                    var probes=new DemoProbe[32];for(int i=0;i<probes.Length;i++)probes[i]=go.AddComponent<DemoProbe>();
                    for(int i=0;i<probes.Length;i++){probes[i].Configure("Channel_"+i.ToString("D2"),1+i*.01f,sample.transform,probes[(i+probes.Length-1)%probes.Length]);probes[i].enabled=i%4!=0;}
                    PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
            string texturePath=Folder+"/CalibrationTexture.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)
            {
                texture=new Texture2D(32,32){name="CalibrationTexture"};var pixels=new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)pixels[y*32+x]=(x/4+y/4)%2==0?new Color(.1f,.85f,.9f):new Color(.2f,.05f,.35f);
                texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);
            }
            string materialPath=Folder+"/CalibrationMaterial.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(materialPath)==null)
            {var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="CalibrationMaterial",mainTexture=texture};AssetDatabase.CreateAsset(material,materialPath);}
            var contents=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                bool changed=false;var filter=contents.GetComponent<MeshFilter>();var renderer=contents.GetComponent<MeshRenderer>();
                if(filter.sharedMesh==null){filter.sharedMesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");changed=true;}
                if(renderer.sharedMaterial==null){renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);changed=true;}
                if(changed)PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            string presetPath=Folder+"/Experiment.asset";
            if(AssetDatabase.LoadAssetAtPath<DemoExperimentPreset>(presetPath)==null)
            {var preset=ScriptableObject.CreateInstance<DemoExperimentPreset>();preset.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),texture);AssetDatabase.CreateAsset(preset,presetPath);}
            if(!File.Exists(Folder+"/FixtureUsage.cs"))File.WriteAllText(Folder+"/FixtureUsage.cs","// Quantum Inspector graph fixture: this source asset intentionally contains no runtime type.\n// Repeated runtime components are implemented by DemoProbe in ../Runtime.\n");
            if(!File.Exists(Folder+"/Leaf/README.txt"))File.WriteAllText(Folder+"/Leaf/README.txt","Leaf folders show files even when there are no child folders. This text file uses a fallback icon.\n");
            if(!File.Exists(Folder+"/UnsupportedPreview.bytes"))File.WriteAllText(Folder+"/UnsupportedPreview.bytes","Opaque fixture data: use an honest fallback icon.\n");
            AssetDatabase.Refresh();
            foreach(var guid in AssetDatabase.FindAssets("",new[]{Folder}))AssetDatabase.SaveAssetIfDirty(new GUID(guid));
            Debug.Log("Graph trial fixtures ready: 42 components on one owner (including 32 distinct DemoProbe instances), plus a mixed asset folder and leaf folder.");
        }
        [MenuItem("Tools/Quantum Inspector/Demo/Select Dense Component Fixture",false,111)]
        public static void SelectFixture(){Generate();Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);EditorGUIUtility.PingObject(Selection.activeObject);}
    }
}
