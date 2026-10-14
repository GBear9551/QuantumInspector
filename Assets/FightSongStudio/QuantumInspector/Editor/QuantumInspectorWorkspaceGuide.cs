using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class QuantumInspectorWorkspaceGuide : EditorWindow
    {
        public static void Open()
        {
            var window=GetWindow<QuantumInspectorWorkspaceGuide>(true,"Quantum Workspace Setup",true);
            window.minSize=new Vector2(380,390);window.ShowUtility();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var scroll=new ScrollView();scroll.style.flexGrow=1;
            scroll.style.paddingLeft=16;scroll.style.paddingRight=16;
            scroll.style.paddingTop=12;scroll.style.paddingBottom=12;rootVisualElement.Add(scroll);
            var title=new Label("Set up your Quantum workspace");
            title.style.fontSize=18;title.style.unityFontStyleAndWeight=FontStyle.Bold;
            title.style.whiteSpace=WhiteSpace.Normal;scroll.Add(title);
            AddText(scroll,"Arrange your workspace once, then save it using Unity's Layouts menu. Opening this guide does not move or replace existing docks.");
            AddText(scroll,"1. Left: drag Hierarchy and Project into the same tab group.");
            AddText(scroll,"2. Middle: dock Quantum Inspector beside the left group.");
            AddText(scroll,"3. Right: dock Unity Inspector beside Quantum Inspector.");
            AddText(scroll,"4. Adjust the dividers, then choose Layouts > Save Layout.");
            AddWindowButton(scroll,"Open Hierarchy","Window/General/Hierarchy");
            AddWindowButton(scroll,"Open Project","Window/General/Project");
            AddWindowButton(scroll,"Open Unity Inspector","Window/General/Inspector");
            scroll.Add(new Button(()=>InspectorWheelWindow.Open()){text="Open Quantum Inspector"});
            AddText(scroll,"Return here from Tools > Quantum Inspector > Layouts > Workspace Setup. The offline user guide is in the package Documentation folder.");
            scroll.Add(new Button(Close){text="Done"});
        }
        private static void AddText(VisualElement parent,string text)
        {
            var label=new Label(text);label.style.whiteSpace=WhiteSpace.Normal;
            label.style.marginTop=8;label.style.marginBottom=4;parent.Add(label);
        }
        private static void AddWindowButton(VisualElement parent,string text,string menu)
            =>parent.Add(new Button(()=>EditorApplication.ExecuteMenuItem(menu)){text=text});
    }
}
