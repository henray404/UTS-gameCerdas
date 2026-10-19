using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class ChaseCameraBuilder
    {
        [MenuItem("Sneak House/Set up chase camera")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before setting up the camera.");
            GameSession game = Object.FindFirstObjectByType<GameSession>();
            RuntimeNavigation nav = Object.FindFirstObjectByType<RuntimeNavigation>();
            if (game == null || nav == null || Camera.main == null) throw new System.InvalidOperationException("Open PrototypeHouse first.");
            Configure(Camera.main, game, nav.geometry);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
        }

        public static void Configure(Camera camera, GameSession game, Transform geometry)
        {
            ChaseCamera view = camera.GetComponent<ChaseCamera>();
            if (view == null) view = Undo.AddComponent<ChaseCamera>(camera.gameObject);
            Undo.RecordObject(view, "Configure chase camera");
            view.player = game.player;
            FirstPersonRoomShell shell = geometry.GetComponentInChildren<FirstPersonRoomShell>(true);
            if (shell == null)
            {
                GameObject group = new GameObject("CAMERA / First person room shell");
                Undo.RegisterCreatedObjectUndo(group, "Add first person room shell");
                group.transform.SetParent(geometry, false);
                shell = Undo.AddComponent<FirstPersonRoomShell>(group);
            }
            shell.geometry = geometry; view.roomShell = shell;
            EditorUtility.SetDirty(shell); EditorUtility.SetDirty(view);
            ChaseAtmosphereBuilder.Configure(camera, game);
        }
    }
}
