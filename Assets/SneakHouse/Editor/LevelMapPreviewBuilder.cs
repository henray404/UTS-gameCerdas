using System;
using System.IO;
using SneakHouse;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SneakHouseEditor
{
    // Render real seeded campaign scenes. No UI overlays, player saves, or scene assets are changed.
    [InitializeOnLoad]
    public static class LevelMapPreviewBuilder
    {
        private const string Key = "SneakHouse.LevelMapPreview.";
        private const int Width = 768, Height = 576;
        private static bool exiting;
        private static bool Active => SessionState.GetBool(Key + "active", false);

        static LevelMapPreviewBuilder()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += LogReceived;
            EditorApplication.playModeStateChanged += state =>
            {
                if (Active && state == PlayModeStateChange.EnteredEditMode)
                    Fail(new Exception("Play Mode ended before every preview was rendered."));
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                Debug.LogError("Level map previews require an isolated batch editor; interactive editor was left open.");
                return;
            }
            try
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (project != "/tmp/sneakhouse-level-validation" ||
                    PlayerSettings.companyName != "SneakHouse Validation" ||
                    PlayerSettings.productName != "Campaign Validation")
                    throw new InvalidOperationException("Run in batch mode from /tmp/sneakhouse-level-validation " +
                        "with isolated company SneakHouse Validation and product Campaign Validation.");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Preview generation must start in Edit Mode.");
                string output = ReadOutputArgument();
                Directory.CreateDirectory(output);
                SessionState.SetString(Key + "output", output);
                SessionState.SetInt(Key + "level", 1);
                SessionState.SetInt(Key + "stage", 0);
                SessionState.SetInt(Key + "settle", 12);
                SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 420);
                SessionState.SetBool(Key + "active", true);
                // These keys belong only to the isolated validation identity checked above.
                PlayerPrefs.SetInt(GameSession.ProgressKey, CampaignLevels.Count);
                PlayerPrefs.Save();
                EditorSceneManager.OpenScene("Assets/SneakHouse/Scenes/PrototypeHouse.unity", OpenSceneMode.Single);
                Debug.Log("LEVEL PREVIEW: render all seeded campaign scenes at " + Width + "x" + Height + ". Output: " + output);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static string ReadOutputArgument()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-levelPreviewOutput");
            if (index < 0 || index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-"))
                throw new ArgumentException("Provide -levelPreviewOutput with the destination Levels directory.");
            string output = Path.GetFullPath(arguments[index + 1]).TrimEnd(Path.DirectorySeparatorChar);
            if (!output.EndsWith(Path.DirectorySeparatorChar + "Resources" + Path.DirectorySeparatorChar +
                "SneakHouseUI" + Path.DirectorySeparatorChar + "Levels", StringComparison.Ordinal))
                throw new ArgumentException("Output must end in Resources/SneakHouseUI/Levels.");
            return output;
        }

        private static void Update()
        {
            if (!Active || exiting) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0))
                    throw new TimeoutException("Preview generation exceeded 420 seconds.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                int settle = SessionState.GetInt(Key + "settle", 0);
                if (settle > 0) { SessionState.SetInt(Key + "settle", settle - 1); return; }
                GameSession game = GameSession.Instance;
                if (game == null) return;
                int level = SessionState.GetInt(Key + "level", 1);
                if (SessionState.GetInt(Key + "stage", 0) == 0)
                {
                    SessionState.SetInt(Key + "stage", 1);
                    SessionState.SetInt(Key + "settle", 16);
                    if (!game.StartLevel(level)) throw new Exception("Unable to select level " + level);
                    return;
                }
                if (game.Level.Number != level || !game.MissionReady)
                    throw new Exception("Selected level is not ready: " + level + " / " + game.MissionIssue);
                Render(game, SessionState.GetString(Key + "output", ""));
                if (level == CampaignLevels.Count)
                {
                    SessionState.SetBool(Key + "active", false); exiting = true;
                    Debug.Log("LEVEL PREVIEW PASS: all " + CampaignLevels.Count + " actual campaign maps rendered.");
                    EditorApplication.Exit(0);
                }
                else
                {
                    SessionState.SetInt(Key + "level", level + 1);
                    SessionState.SetInt(Key + "stage", 0);
                    SessionState.SetInt(Key + "settle", 2);
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Render(GameSession game, string output)
        {
            HouseShell shell = game.GetComponent<HouseShell>();
            FloorCutaway cutaway = game.GetComponent<FloorCutaway>();
            if (cutaway != null) cutaway.enabled = false; // Restore upper architecture for the complete overview.
            Camera source = Camera.main;
            if (source == null) throw new Exception("Campaign scene has no main camera.");
            ChaseCamera chase = source.GetComponent<ChaseCamera>();
            if (chase != null)
            {
                chase.enabled = false;
                if (chase.roomShell != null) chase.roomShell.SetView(0, false);
            }
            foreach (GuardBrain guard in game.guards) guard.gameObject.SetActive(false);
            game.player.gameObject.SetActive(false);
            foreach (LineRenderer marker in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
                marker.enabled = false;
            GameObject cameraObject = new GameObject("Level map preview camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            RenderTexture render = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.CopyFrom(source); camera.enabled = false;
                camera.orthographic = true; camera.aspect = Width / (float)Height;
                camera.nearClipPlane = .1f; camera.farClipPlane = 150;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.48f, .55f, .36f);
                camera.allowHDR = false;
                FitCamera(camera, shell);
                render = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                { antiAliasing = 4, name = "Campaign map preview" };
                render.Create(); camera.targetTexture = render;
                Physics.SyncTransforms(); camera.Render();
                RenderTexture.active = render;
                image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                string file = Path.Combine(output, "level-" + game.Level.Number.ToString("00") + ".png");
                File.WriteAllBytes(file, image.EncodeToPNG());
                Debug.Log("LEVEL PREVIEW RENDERED level=" + game.Level.Number + " map=" + shell.Label +
                    " shellSeed=" + shell.CurrentSeed + " interiorSeed=" + game.GetComponent<InteriorLayout>().CurrentSeed +
                    " upperFloor=" + shell.HasUpperFloor + " output=" + file);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (render != null) { render.Release(); UnityEngine.Object.DestroyImmediate(render); }
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void FitCamera(Camera camera, HouseShell shell)
        {
            Rect[] footprint = shell.Footprint;
            if (footprint.Length == 0) throw new Exception("Selected map has no footprint.");
            float left = footprint[0].xMin, right = footprint[0].xMax;
            float south = footprint[0].yMin, north = footprint[0].yMax;
            foreach (Rect region in footprint)
            {
                left = Mathf.Min(left, region.xMin); right = Mathf.Max(right, region.xMax);
                south = Mathf.Min(south, region.yMin); north = Mathf.Max(north, region.yMax);
            }
            left -= 1.5f; right += 1.5f; south -= 1.5f; north += 1.5f;
            float top = shell.HasUpperFloor ? shell.Current.upperHeight + 1.8f : 1.8f;
            Vector3 center = new Vector3((left + right) * .5f, top * .5f, (south + north) * .5f);
            camera.transform.SetPositionAndRotation(center + new Vector3(0, 38, -23),
                Quaternion.LookRotation(new Vector3(0, -38, 23)));
            float size = 0;
            foreach (float x in new[] { left, right })
                foreach (float y in new[] { 0f, top })
                    foreach (float z in new[] { south, north })
                    {
                        Vector3 point = camera.transform.InverseTransformPoint(new Vector3(x, y, z));
                        size = Mathf.Max(size, Mathf.Abs(point.y), Mathf.Abs(point.x) / camera.aspect);
                    }
            camera.orthographicSize = size * 1.025f;
        }

        private static void LogReceived(string condition, string stackTrace, LogType type)
        {
            // Unity 6000.6's editor startup index can throw before Play Mode; unrelated to scene rendering.
            if (Active && type != LogType.Warning && type != LogType.Log &&
                stackTrace.Contains("UnityEditor.Search.SearchDatabase") &&
                stackTrace.Contains("UnityEditor.Search.SearchInit"))
            {
                Debug.LogWarning("LEVEL PREVIEW: ignored known Unity editor QuickSearch startup exception: " + condition);
                return;
            }
            if (Active && !exiting && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Fail(new Exception(condition + "\n" + stackTrace));
        }

        private static void Fail(Exception exception)
        {
            if (exiting) return;
            exiting = true; SessionState.SetBool(Key + "active", false);
            Debug.LogError("LEVEL PREVIEW FAILED: " + exception);
            EditorApplication.Exit(1);
        }
    }
}
