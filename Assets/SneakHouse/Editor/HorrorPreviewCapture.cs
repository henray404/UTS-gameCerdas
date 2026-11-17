using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SneakHouse.EditorTools
{
    // Plays level 1, then saves first-person frames to Tests/screenshots for a quick look at the horror pass.
    // Batch: Unity -batchmode -projectPath . -executeMethod SneakHouse.EditorTools.HorrorPreviewCapture.RunBatch
    [InitializeOnLoad]
    public static class HorrorPreviewCapture
    {
        private const string Active = "SneakHouse.HorrorCapture.Active", Batch = "SneakHouse.HorrorCapture.Batch";
        private const string Folder = "Tests/screenshots";
        private static double started;
        private static int step;

        static HorrorPreviewCapture()
        {
            if (!SessionState.GetBool(Active, false)) return;
            started = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update += Tick;
        }

        [MenuItem("Sneak House/Capture horror preview")]
        public static void Run() { Begin(false); }
        public static void RunBatch() { Begin(true); }

        private static void Begin(bool batch)
        {
            EditorSceneManager.OpenScene("Assets/SneakHouse/Scenes/PrototypeHouse.unity");
            SessionState.SetBool(Active, true); SessionState.SetBool(Batch, batch);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed > 120) { Finish("timed out at step " + step, 1); return; }
            GameSession game = GameSession.Instance;
            if (game == null) return;
            switch (step)
            {
                case 0:
                    if (game.Phase == GameSession.ScreenState.MainMenu && game.StartLevel(1)) step = 1;
                    break;
                case 1:
                    if (game.Phase != GameSession.ScreenState.Briefing || !game.MissionReady) return;
                    game.BeginMission(); started = EditorApplication.timeSinceStartup; step = 2;
                    break;
                case 2:
                    if (elapsed < 3) return;
                    Save("horror-fps-entrance.png"); step = 3;
                    break;
                case 3:
                    FaceNearestMonster(game); started = EditorApplication.timeSinceStartup; step = 4;
                    break;
                case 4:
                    if (elapsed < .4) return;
                    Save("horror-fps-monster.png");
                    Finish("captured", 0);
                    break;
            }
        }

        private static void FaceNearestMonster(GameSession game)
        {
            GuardBrain nearest = null; float best = float.PositiveInfinity;
            foreach (GuardBrain guard in game.guards)
            {
                if (guard == null || !guard.isActiveAndEnabled) continue;
                float distance = Vector3.Distance(guard.transform.position, game.player.transform.position);
                if (distance < best) { best = distance; nearest = guard; }
            }
            if (nearest == null) return;
            Vector3 front = nearest.transform.position + nearest.transform.forward * 2.6f;
            game.player.Teleport(new Vector3(front.x, game.player.transform.position.y, front.z));
            ChaseCamera view = game.player.View;
            Vector3 toward = nearest.transform.position - game.player.transform.position;
            float yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            view.ApplyLook(new Vector2(Mathf.DeltaAngle(view.Yaw, yaw) / view.mouseSensitivity, 0));
        }

        private static void Save(string name)
        {
            Camera view = Camera.main;
            RenderTexture target = new RenderTexture(1280, 720, 24);
            RenderTexture previous = view.targetTexture;
            view.targetTexture = target; view.Render(); view.targetTexture = previous;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, name), image.EncodeToPNG());
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
            Debug.Log("HORROR_CAPTURE_SAVED: " + name);
        }

        private static void Finish(string message, int code)
        {
            EditorApplication.update -= Tick;
            bool batch = SessionState.GetBool(Batch, false);
            SessionState.EraseBool(Active); SessionState.EraseBool(Batch);
            Debug.Log("HORROR_CAPTURE_DONE: " + message);
            EditorApplication.ExitPlaymode();
            if (batch) EditorApplication.Exit(code);
        }
    }
}
