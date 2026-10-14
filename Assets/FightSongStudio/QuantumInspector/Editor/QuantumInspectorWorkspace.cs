using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    /// <summary>Opens the tool with supported APIs; users control their native dock layout.</summary>
    public static class QuantumInspectorWorkspace
    {
        [MenuItem("Tools/Quantum Inspector/Layouts/Workspace Setup", false, 40)]
        public static void ShowSetup() => QuantumInspectorWorkspaceGuide.Open();

        public static InspectorWheelWindow OpenDefault()
        {
            var window=EditorWindow.GetWindow<InspectorWheelWindow>();
            window.minSize=new Vector2(400,400);
            window.Show();window.SetGraphMode(true);
            ShowSetup();
            return window;
        }
    }
}
