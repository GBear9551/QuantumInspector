using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FightSong.InspectorTool
{
    /// <summary>Explicit integration checks using disposable scene/prefab/metadata assets.</summary>
    public static class InspectorWheelEnhancementVerification
    {
        public static object Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            string folder = "Assets/Scripts/FightSong/InspectorTool/Editor/Verification/Temporary_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Scripts/FightSong/InspectorTool/Editor/Verification", System.IO.Path.GetFileName(folder));
            var originalScene = SceneManager.GetActiveScene();
            var originalSelection = Selection.activeObject;
            var windows = Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().ToDictionary(w => w, w => EditorJsonUtility.ToJson(w));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var settings = ScriptableObject.CreateInstance<InspectorWheelSettings>();
            ComponentAliasDatabase database = null;
            InspectorWheelWindow window = null;
            bool openedStage = false;
            try
            {
                VerifyMotion(settings);
                InspectorSceneNavigationVerification.Run();
                var root = new GameObject("Alias identity fixture");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<AudioSource>(); root.AddComponent<AudioSource>(); root.AddComponent<AudioSource>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Base.prefab");
                Object.DestroyImmediate(root);
                var first = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var second = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var variantSource = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var variant = PrefabUtility.SaveAsPrefabAsset(variantSource, folder + "/Variant.prefab");
                Object.DestroyImmediate(variantSource);
                EditorSceneManager.SaveScene(scene, folder + "/Identity.unity");
                var components = new[] { prefab, first, second, variant }.SelectMany(go => go.GetComponents<AudioSource>()).ToArray();
                var keys = new HashSet<string>();
                foreach (var component in components)
                {
                    Check(ComponentAliasDatabase.TryIdentity(component, out var key), "Saved component has no persistent ID.");
                    Check(keys.Add(key), "Prefab, variant or duplicate component identities collided.");
                }
                database = ScriptableObject.CreateInstance<ComponentAliasDatabase>();
                string metadataPath = folder + "/Aliases.asset";
                AssetDatabase.CreateAsset(database, metadataPath);
                var icon = new Texture2D(2, 2); AssetDatabase.CreateAsset(icon, folder + "/Icon.asset");
                var audio = first.GetComponents<AudioSource>();
                Check(database.Set(audio[0], "Identity alpha", icon, true, Color.magenta, out var error), error);
                Check(database.Set(audio[1], "Identity beta", null, false, Color.cyan, out error), error);
                Check(database.Set(prefab.GetComponents<AudioSource>()[0], "Prefab alias", null, false, Color.cyan, out error), error);
                Check(database.Get(audio[0]).Name == "Identity alpha" && database.Get(audio[1]).Name == "Identity beta", "Per-instance aliases collided.");
                Check(database.Get(second.GetComponent<AudioSource>()) == null && database.Get(variant.GetComponent<AudioSource>()) == null, "Alias leaked to another instance or variant.");
                Check(!scene.isDirty, "Alias editing changed the inspected scene.");
                Undo.PerformUndo();
                Check(database.Get(prefab.GetComponent<AudioSource>()) == null, "Alias Undo failed.");
                Undo.PerformRedo();
                Check(database.Get(prefab.GetComponent<AudioSource>())?.Name == "Prefab alias", "Alias Redo failed.");
                AssetDatabase.SaveAssetIfDirty(database);
                Undo.ClearUndo(database);
                Resources.UnloadAsset(database);
                database = AssetDatabase.LoadAssetAtPath<ComponentAliasDatabase>(metadataPath);
                Check(database.Get(audio[0])?.Name == "Identity alpha", "Alias did not survive metadata asset unload/reload.");
                Check(database.Set(variant.GetComponent<AudioSource>(), "Variant alias", null, false, Color.cyan, out error), error);
                var discovery = new ComponentDiscoveryService();
                var entries = discovery.Discover(first, new ReferenceGraphService(), false, database);
                Check(ComponentDiscoveryService.Filter(entries, "alpha").Single().Component == audio[0], "Alias search failed.");
                Check(ComponentDiscoveryService.Filter(entries, "AudioSource").Count == 3, "Type search failed with aliases.");
                Check(entries.Single(e => e.Component == audio[0]).CustomColor == (Color?)Color.magenta, "Alias color missing.");
                var node = new ComponentNodeView(_ => { }, (_, __) => { }, _ => { });
                node.Bind(entries.Single(e => e.Component == audio[0]), null, WheelNodeSize.Standard);
                Check(node.Q<Label>(className: "node-name").text == "Identity alpha" && node.Q<Label>(className: "node-meta").text == "AudioSource", "Alias/type presentation failed.");
                Check(node.Q<Image>().image == icon, "Alias icon did not survive reload or appear on the node.");

                if (PrefabStageUtility.GetCurrentPrefabStage() == null)
                {
                    var stage = PrefabStageUtility.OpenPrefab(folder + "/Base.prefab"); openedStage = true;
                    Check(database.Get(stage.prefabContentsRoot.GetComponent<AudioSource>())?.Name == "Prefab alias", "Prefab Stage identity does not match prefab asset.");
                    Check(InspectorObjectIdentity.TryGet(stage.prefabContentsRoot.GetComponent<AudioSource>(), out var prefabBookmark) &&
                        InspectorWheelSession.Resolve(prefabBookmark.ToString()) == prefab.GetComponent<AudioSource>(), "Prefab Stage bookmark identity failed.");
                    stage = PrefabStageUtility.OpenPrefab(folder + "/Variant.prefab");
                    Check(database.Get(stage.prefabContentsRoot.GetComponent<AudioSource>())?.Name == "Variant alias", "Variant Stage identity does not match variant asset.");
                    StageUtility.GoToMainStage(); openedStage = false;
                }
                window = ScriptableObject.CreateInstance<InspectorWheelWindow>(); window.Pinned = true;
                window.Show(); window.CreateGUI();
                window.Navigate(audio[0]); window.SetQuery("audio"); window.Controller.RestorePosition(-2);
                var bookmark = new InspectorWheelSession.Bookmark { Id = GlobalObjectId.GetGlobalObjectIdSlow(audio[1]).ToString(), Label = "Fixture bookmark" };
                window.LoadBookmark(bookmark);
                Check(window.InspectedObject == first && window.SelectedComponent == audio[1] && window.Wheel.Search.value == "", "Bookmark failed to select its component or clear filter.");
                window.MoveHistory(-1);
                Check(window.SelectedComponent == audio[0] && window.Wheel.Search.value == "audio" && window.Controller.Position == -2, "Back failed to restore same-object component, query and rotation.");
                window.MoveHistory(1);
                Check(window.SelectedComponent == audio[1], "Forward failed to restore bookmark selection.");
                window.Navigate(second.transform); window.MoveHistory(-1);
                Check(window.InspectedObject == first && window.SelectedComponent == audio[1], "Cross-object history failed.");

                window.Inspect(null, false);
                var resolved = InspectorWheelSession.Load(bookmark.Id, out error) as Component;
                Check(resolved != null && error == null && resolved.GetType() == typeof(AudioSource), "Loaded scene bookmark did not resolve.");
                Check(originalScene.isLoaded, "Resolving an already loaded bookmark changed the scene setup.");
                Check(database.Get(resolved)?.Name == "Identity beta", "Bookmark alias changed.");
                Check(database.Set(resolved, "", null, false, Color.white, out error) && database.Get(resolved) == null, "Reset alias failed.");
                window.Navigate(prefab.transform); window.SetQuery("transform"); window.Pinned = false;
                window.LoadBookmark(bookmark);
                Check(window.SelectedComponent is AudioSource, "Selection sync interrupted bookmark loading.");
                window.MoveHistory(-1);
                Check(window.SelectedComponent == prefab.transform && window.Wheel.Search.value == "transform", "Unpinned bookmark loading lost the previous inspection.");
                return new { passed = true, uniquePrefabAndComponentIds = keys.Count, aliasSearch = true, metadataReload = true,
                    prefabStage = true, undoRedo = true, sceneUnchanged = true, bookmarkBackForward = true, loadedSceneBookmark = true, singleScenePolicy = true,
                    springSettle = true, reducedMotion = true, deterministicBobbing = true };
            }
            finally
            {
                if (window != null) window.Close();
                if (openedStage) StageUtility.GoToMainStage();
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                Selection.activeObject = originalSelection;
                foreach (var saved in windows) if (saved.Key != null) { EditorJsonUtility.FromJsonOverwrite(saved.Value, saved.Key); saved.Key.CreateGUI(); }
                if (database != null) Undo.ClearUndo(database);
                Object.DestroyImmediate(settings);
                // Only the uniquely named fixture folder created by this run is removed.
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void VerifyMotion(InspectorWheelSettings settings)
        {
            settings.BobAmplitude = settings.MicroDrift = 0;
            var motion = new OrbitalNodeMotion();
            motion.Tick(.02f, 0, "node", 0, Vector2.up, false, false, 0, settings);
            for (int i = 0; i < 200; i++) motion.Tick(.02f, 0, "node", 0, Vector2.up, false, false, 0, settings);
            motion.Click(settings);
            Check(motion.Scale < 1, "Click did not compress the node.");
            bool rebound = false;
            for (int i = 0; i < 250; i++)
            {
                motion.Tick(.02f, 1, "node", 0, Vector2.up, false, false, 0, settings);
                rebound |= motion.Scale > 1;
                Check(motion.Offset.magnitude < 20 && motion.Scale >= .9f && motion.Scale <= 1.07f, "Spring exceeded readability bounds.");
            }
            Check(rebound && Mathf.Abs(motion.Scale - 1) < .001f && motion.Offset.magnitude < .01f, "Spring failed to rebound and settle.");
            settings.BobAmplitude = 3; settings.MicroDrift = 1;
            var duplicate = new OrbitalNodeMotion(); var twin = new OrbitalNodeMotion(); var otherSlot = new OrbitalNodeMotion();
            for (int i = 0; i < 120; i++)
            {
                duplicate.Tick(1f / 15, 0, "a", 0, Vector2.up, false, false, i / 15.0, settings);
                twin.Tick(1f / 15, 0, "a", 0, Vector2.up, false, false, i / 15.0, settings);
                otherSlot.Tick(1f / 15, 0, "b", 1, Vector2.up, false, false, i / 15.0, settings);
            }
            Check(duplicate.Offset == twin.Offset && Vector2.Distance(duplicate.Offset, otherSlot.Offset) > .1f, "Bobbing is not deterministic/asynchronous.");
            settings.ReducedMotion = true;
            duplicate.Click(settings); duplicate.Tick(.1f, 10, "b", 2, Vector2.up, true, true, 30, settings);
            Check(duplicate.Offset == Vector2.zero && duplicate.Scale == 1 && duplicate.Ripple == 0, "Reduced Motion left decorative motion enabled.");
            settings.ReducedMotion = false; settings.AnimationIntensity = 0;
            duplicate.Tick(.1f, 20, "c", 2, Vector2.up, true, true, 30, settings);
            Check(duplicate.Offset == Vector2.zero && duplicate.Scale == 1, "Zero intensity did not suppress motion.");
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
