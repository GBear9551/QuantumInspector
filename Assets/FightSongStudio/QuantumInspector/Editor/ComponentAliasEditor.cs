using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    /// <summary>The same alias fields are used in component details and the node's context action.</summary>
    public sealed class ComponentAliasFields : VisualElement
    {
        public ComponentAliasFields(Component component, Action applied = null)
        {
            AddToClassList("alias-fields");
            var value = ComponentAliasDatabase.Shared.Get(component);
            var nickname = new TextField("Alias") { value = value?.Name ?? "", maxLength = 80, name = "component-alias" };
            var icon = new ObjectField("Icon") { objectType = typeof(Texture2D), allowSceneObjects = false, value = value?.Icon };
            var useColor = new Toggle("Custom color") { value = value?.UseColor ?? false };
            var color = new ColorField("Color") { value = value?.Color ?? Color.cyan, showAlpha = false, hdr = false };
            color.SetEnabled(useColor.value);
            useColor.RegisterValueChangedCallback(e => color.SetEnabled(e.newValue));
            Add(nickname); Add(icon); Add(useColor); Add(color);
            var help = new Label("Shared editor metadata for this component instance. Its C# type stays unchanged.");
            help.AddToClassList("research-line"); Add(help);
            var actions = new VisualElement(); actions.AddToClassList("research-actions"); Add(actions);
            void Save(bool clear)
            {
                if (ComponentAliasDatabase.Shared.Set(component, clear ? "" : nickname.value, clear ? null : icon.value as Texture2D,
                    !clear && useColor.value, color.value, out var failure))
                {
                    if (clear) { nickname.SetValueWithoutNotify(""); icon.SetValueWithoutNotify(null); useColor.SetValueWithoutNotify(false); color.SetEnabled(false); }
                    help.text = clear ? "Alias and appearance overrides cleared." : "Alias saved to the project's editor metadata.";
                    applied?.Invoke();
                }
                else help.text = failure;
            }
            actions.Add(new Button(() => Save(false)) { text = "Save alias" });
            actions.Add(new Button(() => Save(true)) { text = "Reset alias" });
            if (!ComponentAliasDatabase.CanEdit(component, out var reason))
            { nickname.SetEnabled(false); icon.SetEnabled(false); useColor.SetEnabled(false); color.SetEnabled(false); actions.SetEnabled(false); help.text = reason; }
        }
    }

    public sealed class ComponentAliasEditor : EditorWindow
    {
        [SerializeField] private Component component;
        public static void Open(Component target)
        {
            var window = CreateInstance<ComponentAliasEditor>();
            window.component = target; window.titleContent = new GUIContent("Component Alias");
            window.minSize = new Vector2(380, 300); window.maxSize = new Vector2(600, 420);
            window.ShowUtility();
        }
        public void CreateGUI()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(InspectorWheelWindow.StylePath);
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);
            rootVisualElement.AddToClassList("quantum-root");
            rootVisualElement.AddToClassList("alias-editor");
            if (component == null) { rootVisualElement.Add(new Label("This component no longer exists.")); return; }
            var type = new Label(component.GetType().FullName + " · " + component.gameObject.name);
            type.AddToClassList("research-value"); rootVisualElement.Add(type);
            rootVisualElement.Add(new ComponentAliasFields(component));
        }
    }
}
