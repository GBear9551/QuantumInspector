using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightSong.InspectorTool
{
    public interface IOrbitalDataProvider
    {
        bool Supports(WheelScopeKind kind);
        List<OrbitalEntry> Discover(WheelScope scope,ReferenceGraphService graph,InspectorWheelSettings settings);
        void Invalidate();
    }

    public sealed class ComponentOrbitalProvider:IOrbitalDataProvider
    {
        private readonly ComponentDiscoveryService discovery=new ComponentDiscoveryService();
        public bool Supports(WheelScopeKind kind)=>kind==WheelScopeKind.Components;
        public List<OrbitalEntry> Discover(WheelScope scope,ReferenceGraphService graph,InspectorWheelSettings settings)
            =>discovery.Discover(scope.Object,graph,settings.SearchReferences).Cast<OrbitalEntry>().ToList();
        public void Invalidate() { }
    }

    public sealed class HierarchyOrbitalProvider:IOrbitalDataProvider
    {
        public bool Supports(WheelScopeKind kind)=>kind==WheelScopeKind.SceneRoots||kind==WheelScopeKind.Children;
        public void Invalidate() { }
        public List<OrbitalEntry> Discover(WheelScope scope,ReferenceGraphService graph,InspectorWheelSettings settings)
        {
            var objects=new List<GameObject>();
            if(scope.Kind==WheelScopeKind.Children&&scope.Object!=null)
                foreach(Transform child in scope.Object.transform)objects.Add(child.gameObject);
            else if(scope.Kind==WheelScopeKind.SceneRoots)
            {
                var stage=PrefabStageUtility.GetCurrentPrefabStage();
                if(stage!=null)objects.Add(stage.prefabContentsRoot);
                else for(int i=0;i<SceneManager.sceneCount;i++)
                {
                    var scene=SceneManager.GetSceneAt(i);
                    if(scene.isLoaded&&!EditorSceneManager.IsPreviewScene(scene))objects.AddRange(scene.GetRootGameObjects());
                }
            }
            return objects.Where(go=>go!=null&&(go.hideFlags&HideFlags.HideInHierarchy)==0).Select((go,i)=>new OrbitalEntry
            {
                Kind=OrbitalEntryKind.GameObject,Target=go,Name=go.name,TypeName="GameObject",CategoryName="GameObject",
                Category=ComponentCategory.System,CustomColor=new Color(.2f,.85f,.82f),Glyph="◇",Ordinal=i,
                Description=go.scene.name+" · "+go.GetComponents<Component>().Length+" components · "+go.transform.childCount+" children",
                SearchText=ComponentDiscoveryService.Normalize(go.name+" GameObject "+go.tag+" "+go.scene.name)
            }).ToList();
        }
    }

    public sealed class ProjectOrbitalProvider:IOrbitalDataProvider
    {
        private Dictionary<string,List<string>> folders;
        private readonly Dictionary<string,List<OrbitalEntry>> cache=new Dictionary<string,List<OrbitalEntry>>();
        public bool Supports(WheelScopeKind kind)=>kind==WheelScopeKind.ProjectFolder;
        public void Invalidate(){folders=null;cache.Clear();}
        public List<OrbitalEntry> Discover(WheelScope scope,ReferenceGraphService graph,InspectorWheelSettings settings)
        {
            string folder=scope.FolderPath;
            if(cache.TryGetValue(folder,out var entries))return entries;
            if(folders==null)
            {
                folders=new Dictionary<string,List<string>>();
                foreach(var path in AssetDatabase.GetAllAssetPaths())
                {
                    if(!path.StartsWith("Assets/",StringComparison.Ordinal))continue;
                    var parent=Path.GetDirectoryName(path)?.Replace('\\','/');if(parent==null)continue;
                    if(!folders.TryGetValue(parent,out var children))folders[parent]=children=new List<string>();
                    children.Add(path);
                }
            }
            entries=new List<OrbitalEntry>();
            if(folders.TryGetValue(folder,out var paths))
                foreach(var path in paths.OrderBy(p=>AssetDatabase.IsValidFolder(p)?0:1).ThenBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase))
                {
                    bool isFolder=AssetDatabase.IsValidFolder(path);
                    var type=AssetDatabase.GetMainAssetTypeAtPath(path);
                    string kind=isFolder?"Folder":AssetInformation.Kind(type,path);
                    string name=isFolder?Path.GetFileName(path):Path.GetFileNameWithoutExtension(path);
                    entries.Add(new OrbitalEntry{Kind=isFolder?OrbitalEntryKind.Folder:OrbitalEntryKind.Asset,AssetPath=path,
                        Name=name,TypeName=kind,CategoryName=kind,Category=ComponentCategory.System,Ordinal=entries.Count,
                        Glyph=AssetInformation.Glyph(kind),CustomColor=AssetInformation.Tint(kind),Description=path+"\n"+kind,
                        SearchText=ComponentDiscoveryService.Normalize(name+" "+Path.GetFileName(path)+" "+kind+" "+type?.FullName)});
                }
            cache[folder]=entries;return entries;
        }
    }

    public static class AssetInformation
    {
        public static string Kind(Type type,string path)
        {
            if(path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))return "Prefab";
            if(type==typeof(MonoScript))return "Script";
            if(type==typeof(Material))return "Material";
            if(type!=null&&typeof(Texture).IsAssignableFrom(type))return "Texture";
            if(type==typeof(SceneAsset))return "Scene";
            if(type!=null&&typeof(ScriptableObject).IsAssignableFrom(type))return "ScriptableObject";
            return type!=null?ObjectNames.NicifyVariableName(type.Name):"Asset";
        }
        public static string Glyph(string kind)=>kind switch {"Folder"=>"▱","Prefab"=>"◇","Script"=>"</>","Material"=>"◉","Texture"=>"▧","Scene"=>"◎","ScriptableObject"=>"{ }",_=>"◈"};
        public static Color Tint(string kind)=>kind switch {"Folder"=>new Color(1,.75f,.3f),"Prefab"=>new Color(.3f,.65f,1),"Script"=>new Color(.7f,.5f,1),"Texture"=>new Color(.5f,.95f,.7f),_=>new Color(.4f,.85f,.95f)};
        public static IEnumerable<string> Describe(UnityEngine.Object asset)
        {
            if(asset==null)yield break;
            string path=AssetDatabase.GetAssetPath(asset);
            yield return Kind(asset.GetType(),path)+" · "+asset.GetType().Name;
            yield return path;
            switch(asset)
            {
                case GameObject prefab:
                    yield return PrefabUtility.GetPrefabAssetType(prefab)+" · "+prefab.GetComponentsInChildren<Transform>(true).Length+" GameObjects";
                    yield return prefab.GetComponents<Component>().Length+" components on the root";break;
                case MonoScript script: yield return "Class: "+(script.GetClass()?.FullName??"No compiled Unity object class");break;
                case Material material: yield return "Shader: "+(material.shader!=null?material.shader.name:"Missing");yield return "Render queue: "+material.renderQueue;break;
                case Texture texture: yield return texture.width+" × "+texture.height+" · "+texture.dimension;break;
                case SceneAsset _: yield return "Scene asset · Open scene switches to it, prompting to save modified scenes first.";break;
                case ScriptableObject _: yield return "Shared serialized data asset · inspect its fields in Unity's Inspector.";break;
            }
        }
    }
}
