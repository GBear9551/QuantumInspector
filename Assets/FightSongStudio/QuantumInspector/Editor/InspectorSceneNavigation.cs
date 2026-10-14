using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FightSong.InspectorTool
{
    internal static class InspectorSceneNavigation
    {
        public static bool TryOpen(string path, out string failure)
        {
            failure = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                failure = "Leave Play Mode to open a scene.";
                return false;
            }
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                failure = "The scene asset is missing.";
                return false;
            }
            var current = SceneManager.GetSceneByPath(path);
            if (SceneManager.sceneCount == 1 && current.IsValid() && current.isLoaded)
                return true;
            return ConfirmAndOpen(path, EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo,
                (scenePath, mode) => EditorSceneManager.OpenScene(scenePath, mode), out failure);
        }

        internal static bool ConfirmAndOpen(string path, Func<bool> confirmSave,
            Action<string, OpenSceneMode> openScene, out string failure)
        {
            failure = null;
            try
            {
                if (!confirmSave()) return false;
                openScene(path, OpenSceneMode.Single);
                return true;
            }
            catch (Exception exception)
            {
                failure = "Could not open scene: " + exception.Message;
                return false;
            }
        }
    }
}
