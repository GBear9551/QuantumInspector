using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    [FilePath("Library/FightSong/QuantumInspectorBookmarks.asset",FilePathAttribute.Location.ProjectFolder)]
    public sealed class InspectorWheelSession : ScriptableSingleton<InspectorWheelSession>
    {
        [Serializable] public sealed class Bookmark { public string Id, Label; }
        public List<Bookmark> Bookmarks=new List<Bookmark>();
        public bool Add(Object target)
        {
            if(target==null)return false;
            var owner=target as GameObject ?? (target as Component)?.gameObject;
            if(owner!=null&&!EditorUtility.IsPersistent(owner)&&string.IsNullOrEmpty(owner.scene.path))return false;
            if(!InspectorObjectIdentity.TryGet(target,out var id) || (!EditorUtility.IsPersistent(target) && EditorApplication.isPlaying))return false;
            string value=id.ToString();
            if(Bookmarks.Exists(b=>b.Id==value))return true;
            Bookmarks.Add(new Bookmark{Id=value,Label=target.name+" · "+target.GetType().Name});Save(true);return true;
        }
        public void Remove(Bookmark bookmark) { Bookmarks.Remove(bookmark);Save(true); }
        public static Object Resolve(string value) => GlobalObjectId.TryParse(value,out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null;
        public static Object Load(string value,out string failure)
        {
            failure=null;
            var target=Resolve(value);
            if(target!=null)return target;
            if(!GlobalObjectId.TryParse(value,out var id)){failure="This bookmark has an invalid object identity.";return null;}
            string path=AssetDatabase.GUIDToAssetPath(id.assetGUID.ToString());
            if(string.IsNullOrEmpty(path)){failure="The bookmarked scene or asset is missing.";return null;}
            if(path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase))
            {
                var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                if(!scene.isLoaded&&!InspectorSceneNavigation.TryOpen(path,out failure))
                {failure=failure??"Scene change cancelled.";return null;}
            }
            else AssetDatabase.LoadMainAssetAtPath(path);
            target=Resolve(value);
            if(target==null)failure="The bookmarked object no longer exists in its scene or asset.";
            return target;
        }
        public static string Label(Bookmark bookmark)
        {
            var target=Resolve(bookmark.Id);
            return target is Component component?component.gameObject.name+" · "+ComponentAliasDatabase.DisplayName(component):target!=null?target.name:bookmark.Label;
        }
    }
}
