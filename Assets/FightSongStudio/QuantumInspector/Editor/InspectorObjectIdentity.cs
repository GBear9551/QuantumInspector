using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightSong.InspectorTool
{
    /// <summary>Persistent object identity shared by aliases and bookmarks, including Prefab Mode.</summary>
    public static class InspectorObjectIdentity
    {
        public static bool TryGet(Object target, out GlobalObjectId id)
        {
            id = default;
            if (target == null) return false;
            var owner = target as GameObject ?? (target as Component)?.gameObject;
            if (owner != null && !EditorUtility.IsPersistent(target) && string.IsNullOrEmpty(owner.scene.path)) return false;
            id = GlobalObjectId.GetGlobalObjectIdSlow(target);
            if (id.identifierType == 0 || id.targetObjectId == 0 || id.assetGUID.ToString() == new string('0', 32)) return false;
            var stage = owner != null ? PrefabStageUtility.GetPrefabStage(owner) : null;
            if (stage == null) return true;
            // Prefab Mode reports scene IDs. Variants/nested prefabs also encode a prefab instance
            // ID; Unity's imported asset uses the combined local file ID. Resolve and validate the
            // actual asset object before storing its canonical ID. Never remap ordinary scene instances.
            ulong localId = id.targetPrefabId == 0 ? id.targetObjectId : (id.targetObjectId ^ id.targetPrefabId) & 0x7fffffffffffffff;
            string assetId = $"GlobalObjectId_V1-1-{id.assetGUID}-{localId}-0";
            if (!GlobalObjectId.TryParse(assetId, out var candidateId)) return false;
            var candidate = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(candidateId);
            if (candidate == null || candidate.GetType() != target.GetType()) return false;
            id = GlobalObjectId.GetGlobalObjectIdSlow(candidate);
            return true;
        }
    }
}
