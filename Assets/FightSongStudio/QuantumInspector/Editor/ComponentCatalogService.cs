using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    public sealed class CatalogComponent
    {
        public Type Type;
        public string Name,Category,Search;
    }

    /// <summary>Domain-cached, installed types only. Unity remains the final authority on native component constraints.</summary>
    public static class ComponentCatalogService
    {
        private static List<CatalogComponent> catalog;
        public static IReadOnlyList<CatalogComponent> All
        {
            get
            {
                if(catalog!=null)return catalog;
                var scripts=new HashSet<Type>(MonoImporter.GetAllRuntimeMonoScripts().Select(s=>s.GetClass()).Where(t=>t!=null));
                catalog=TypeCache.GetTypesDerivedFrom<Component>().Where(t=>!t.IsAbstract&&!t.IsGenericType&&!t.IsNested&&(t.IsPublic||scripts.Contains(t))&&
                    !typeof(Transform).IsAssignableFrom(t)&&!Attribute.IsDefined(t,typeof(ObsoleteAttribute))&&
                    (typeof(MonoBehaviour).IsAssignableFrom(t)?scripts.Contains(t):t.Assembly.GetName().Name.StartsWith("UnityEngine",StringComparison.Ordinal))&&
                    t!=typeof(ParticleSystemRenderer)).Select(t=>
                    {
                        string name=ObjectNames.NicifyVariableName(t.Name),category=Category(t);
                        return new CatalogComponent{Type=t,Name=name,Category=category,Search=ComponentDiscoveryService.Normalize(name+" "+t.FullName+" "+category)};
                    }).OrderBy(c=>c.Category).ThenBy(c=>c.Name).ToList();
                return catalog;
            }
        }
        private static string Category(Type type)
        {
            string name=(type.FullName??type.Name).ToLowerInvariant();
            string space=(type.Namespace??"").ToLowerInvariant();
            var menu=type.GetCustomAttributes(typeof(AddComponentMenu),false).Cast<AddComponentMenu>().FirstOrDefault();
            if(space.StartsWith("unity.netcode",StringComparison.Ordinal)||space.StartsWith("unity.networking",StringComparison.Ordinal)||
                space.StartsWith("mirror",StringComparison.Ordinal)||space.StartsWith("photon",StringComparison.Ordinal)||space.StartsWith("fishnet",StringComparison.Ordinal)||
                (menu!=null&&menu.componentMenu.StartsWith("Networking/",StringComparison.OrdinalIgnoreCase)))return "Networking / Netcode";
            if(name.Contains("audio"))return "Audio";
            if(name.Contains("unityengine.ui")||name.Contains("tmpro")||name.Contains("canvas")||name.Contains("eventsystem")||name.Contains("uidocument"))return "UI";
            if(name.Contains("animat"))return "Animation";
            if(typeof(Collider).IsAssignableFrom(type)||typeof(Collider2D).IsAssignableFrom(type)||typeof(Joint).IsAssignableFrom(type)||typeof(Joint2D).IsAssignableFrom(type)||name.Contains("rigidbody")||name.Contains("charactercontroller")||name.Contains("cloth")||name.Contains("effector"))return "Physics";
            if(typeof(Renderer).IsAssignableFrom(type)||name.Contains("render")||name.Contains("camera")||name.Contains("light")||name.Contains("mesh")||name.Contains("particle")||name.Contains("volume"))return "Rendering";
            return typeof(MonoBehaviour).IsAssignableFrom(type)?"Scripts":"Other";
        }
        public static string CanModify(GameObject target)
        {
            if(target==null)return "The GameObject no longer exists.";
            if(EditorUtility.IsPersistent(target))return "Open this prefab in Prefab Mode to change its components.";
            if((target.hideFlags&HideFlags.NotEditable)!=0||PrefabUtility.IsPartOfImmutablePrefab(target))return "This object is read only. Edit an editable prefab or scene instance.";
            var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(target);
            if(stage!=null&&!AssetDatabase.IsOpenForEdit(stage.assetPath))return "The prefab asset is not open for editing.";
            return null;
        }
        public static IEnumerable<Type> Requirements(Type type)
        {
            foreach(RequireComponent requirement in type.GetCustomAttributes(typeof(RequireComponent),true))
            {
                if(requirement.m_Type0!=null)yield return requirement.m_Type0;
                if(requirement.m_Type1!=null)yield return requirement.m_Type1;
                if(requirement.m_Type2!=null)yield return requirement.m_Type2;
            }
        }
        public static string CanAdd(GameObject target,Type type)
        {
            string reason=CanModify(target);if(reason!=null)return reason;
            if(type==null||!All.Any(c=>c.Type==type))return "This type is not an installed, attachable component.";
            return CheckType(target,type,new HashSet<Type>());
        }
        private static string CheckType(GameObject target,Type type,HashSet<Type> visited)
        {
            if(!visited.Add(type))return null;
            foreach(var present in target.GetComponents<Component>())
            {
                if(present==null)continue;
                for(Type parent=type;parent!=null&&typeof(Component).IsAssignableFrom(parent);parent=parent.BaseType)
                    if(Attribute.IsDefined(parent,typeof(DisallowMultipleComponent),false)&&parent.IsAssignableFrom(present.GetType()))
                        return parent.Name+" disallows multiple instances ("+present.GetType().Name+" is already attached).";
            }
            foreach(var required in Requirements(type))
            {
                if(target.GetComponent(required)!=null)continue;
                var failure=CheckType(target,required,visited);if(failure!=null)return failure;
            }
            return null;
        }
        public static Component Add(GameObject target,Type type,out string failure)
        {
            failure=CanAdd(target,type);if(failure!=null)return null;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add "+type.Name);
            try
            {
                var added=Undo.AddComponent(target,type);
                if(added==null){failure="Unity rejected this component for this GameObject. Check the native Inspector for its compatibility requirements.";Undo.RevertAllDownToGroup(group);return null;}
                Undo.CollapseUndoOperations(group);return added;
            }
            catch(Exception exception){Undo.RevertAllDownToGroup(group);failure=exception.Message;return null;}
        }
        public static string CanRemove(Component component)
        {
            if(component==null)return "The component no longer exists.";
            if(component is Transform)return "Transform and RectTransform are required by every GameObject.";
            var failure=CanModify(component.gameObject);if(failure!=null)return failure;
            if((component.hideFlags&HideFlags.NotEditable)!=0)return "This component is read only.";
            if(component is ParticleSystemRenderer)return "The Particle System owns its renderer. Remove the Particle System instead.";
            var components=component.gameObject.GetComponents<Component>().Where(c=>c!=null&&c!=component).ToArray();
            var dependents=components.Where(c=>Requirements(c.GetType()).Any(r=>r.IsAssignableFrom(component.GetType())&&!components.Any(other=>r.IsInstanceOfType(other)))).ToArray();
            if(dependents.Length>0)return "Required by "+string.Join(", ",dependents.Select(ComponentAliasDatabase.DisplayName))+". Remove dependent components first.";
            return null;
        }
        public static bool Remove(Component component,out string failure)
        {
            failure=CanRemove(component);if(failure!=null)return false;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Remove "+component.GetType().Name);
            try
            {
                Undo.DestroyObjectImmediate(component);
                if(component!=null){failure="Unity prevented removal because of a native dependency or prefab restriction.";return false;}
                Undo.CollapseUndoOperations(group);return true;
            }
            catch(Exception exception){Undo.RevertAllDownToGroup(group);failure=exception.Message;return false;}
        }
    }
}
