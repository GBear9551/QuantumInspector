using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    /// <summary>Shared, editor-only metadata. Exact GlobalObjectIds distinguish scene and prefab instances.</summary>
    public sealed class ComponentAliasDatabase : ScriptableObject
    {
        public const string AssetPath = "Assets/Scripts/FightSong/InspectorTool/Editor/ComponentAliases.asset";
        [Serializable]
        public sealed class Alias
        {
            public string Id;
            public string Name;
            public Texture2D Icon;
            public bool UseColor;
            public Color Color = Color.cyan;
        }
        [SerializeField] private List<Alias> aliases = new List<Alias>();
        private Dictionary<string, Alias> lookup;
        private static ComponentAliasDatabase shared;
        public static event Action Changed;
        public static ComponentAliasDatabase Shared
        {
            get
            {
                if (shared != null) return shared;
                shared = AssetDatabase.LoadAssetAtPath<ComponentAliasDatabase>(AssetPath);
                if (shared != null) return shared;
                shared = CreateInstance<ComponentAliasDatabase>();
                AssetDatabase.CreateAsset(shared, AssetPath);
                AssetDatabase.SaveAssetIfDirty(shared);
                return shared;
            }
        }
        private void OnValidate() { lookup = null; Changed?.Invoke(); }
        private void OnEnable() { lookup = null; Undo.undoRedoPerformed += Invalidate; }
        private void OnDisable() { Undo.undoRedoPerformed -= Invalidate; }
        private void Invalidate() { lookup = null; Changed?.Invoke(); }

        public static bool TryIdentity(Component component, out string key)
        {
            key = null;
            if (!InspectorObjectIdentity.TryGet(component, out var id)) return false;
            key = id.ToString();
            return true;
        }
        public static bool CanEdit(Component component, out string reason)
        {
            reason = null;
            if (component == null) reason = "This component no longer exists.";
            else if (EditorApplication.isPlaying && !EditorUtility.IsPersistent(component))
                reason = "Edit scene component aliases outside Play Mode so they have a stable saved identity.";
            else if (!TryIdentity(component, out _)) reason = "Save this scene or prefab before assigning a persistent alias.";
            return reason == null;
        }
        public Alias Get(Component component)
        {
            if (!TryIdentity(component, out var key)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, Alias>();
                foreach (var item in aliases) if (!string.IsNullOrEmpty(item.Id)) lookup[item.Id] = item;
            }
            return lookup.TryGetValue(key, out var alias) ? alias : null;
        }
        public bool Set(Component component, string nickname, Texture2D icon, bool useColor, Color color, out string reason)
        {
            if (!CanEdit(component, out reason) || !TryIdentity(component, out var key)) return false;
            if (icon != null && !EditorUtility.IsPersistent(icon)) { reason = "Choose a saved texture asset for the icon."; return false; }
            nickname = (nickname ?? "").Trim();
            if (nickname.Length > 80) nickname = nickname.Substring(0, 80);
            Undo.RecordObject(this, "Edit component alias");
            aliases.RemoveAll(item => item.Id == key);
            if (nickname.Length > 0 || icon != null || useColor)
                aliases.Add(new Alias { Id = key, Name = nickname, Icon = icon, UseColor = useColor, Color = color });
            lookup = null;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            Changed?.Invoke();
            return true;
        }
        public static string DisplayName(Component component)
        {
            if (component == null) return "Missing component";
            var alias = Shared.Get(component);
            return string.IsNullOrEmpty(alias?.Name) ? ObjectNames.NicifyVariableName(component.GetType().Name) : alias.Name;
        }
    }
}
