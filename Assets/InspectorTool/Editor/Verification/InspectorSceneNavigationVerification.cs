using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

namespace FightSong.InspectorTool
{
    public static class InspectorSceneNavigationVerification
    {
        public static object Run()
        {
            const string path = "Assets/Verification.unity";
            var calls = new List<string>();
            bool opened = InspectorSceneNavigation.ConfirmAndOpen(path,
                () => { calls.Add("save"); return true; },
                (destination, mode) =>
                {
                    Check(destination == path && mode == OpenSceneMode.Single, "Scene navigation must use Single mode.");
                    calls.Add("open");
                }, out string failure);
            Check(opened && failure == null && string.Join(",", calls) == "save,open", "Save prompt must precede scene switching.");

            calls.Clear();
            opened = InspectorSceneNavigation.ConfirmAndOpen(path, () => false,
                (_, __) => calls.Add("open"), out failure);
            Check(!opened && failure == null && calls.Count == 0, "Cancelling must leave the open scenes unchanged.");

            opened = InspectorSceneNavigation.ConfirmAndOpen(path, () => true,
                (_, __) => throw new InvalidOperationException("Fixture failure"), out failure);
            Check(!opened && failure.Contains("Fixture failure"), "Scene opening failures must be reported.");
            Check(!InspectorSceneNavigation.TryOpen("", out failure) && !string.IsNullOrEmpty(failure), "Invalid scene paths must be rejected.");
            return new { passed = true, singleSceneMode = true, saveBeforeSwitch = true, cancellation = true, failureReporting = true };
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
