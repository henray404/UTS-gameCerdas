using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class SecurityTuningBuilder
    {
        [MenuItem("Sneak House/Set up alert security")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            GameSession game = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if (game == null) throw new InvalidOperationException("Open PrototypeHouse first.");
            foreach (GuardBrain guard in game.guards)
            {
                Undo.RecordObject(guard, "Increase guard perception");
                guard.visionDistance = 9; guard.fieldOfView = 120; guard.hearingSuspicion = .35f;
                EditorUtility.SetDirty(guard);
            }
            Undo.RecordObject(game.player, "Increase footstep hearing radius");
            game.player.walkingNoiseRadius = 3.5f; game.player.runningNoiseRadius = 7;
            EditorUtility.SetDirty(game.player);
            PlayerDistraction distraction = game.player.GetComponent<PlayerDistraction>();
            Undo.RecordObject(distraction, "Increase distraction hearing radius");
            distraction.hearingRadius = 10; EditorUtility.SetDirty(distraction);
            const string path = "Assets/SneakHouse/Resources/SneakHouseGameplay/DistractionCan.prefab";
            GameObject can = PrefabUtility.LoadPrefabContents(path);
            try { can.GetComponent<DroppedCan>().hearingRadius = 10; PrefabUtility.SaveAsPrefabAsset(can, path); }
            finally { PrefabUtility.UnloadPrefabContents(can); }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
    }
}
