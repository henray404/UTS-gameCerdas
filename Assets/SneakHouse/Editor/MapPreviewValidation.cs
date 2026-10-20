using System;
using System.IO;
using System.Reflection;
using SneakHouse;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SneakHouseEditor
{
    // Regression: real map assets must recover when a menu instance cached missing imports.
    [InitializeOnLoad]
    public static class MapPreviewValidation
    {
        private const string Key = "SneakHouse.MapPreviewValidation.";
        private static bool exiting;
        private static bool Active => SessionState.GetBool(Key + "active", false);

        static MapPreviewValidation()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += LogReceived;
            EditorApplication.playModeStateChanged += state =>
            {
                if (Active && state == PlayModeStateChange.EnteredEditMode)
                    Fail(new Exception("Play Mode ended before map-preview validation finished."));
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                Debug.LogError("Map preview validation requires an isolated batch editor. Interactive editor was left open.");
                return;
            }
            try
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (project != "/tmp/sneakhouse-level-validation" ||
                    PlayerSettings.companyName != "SneakHouse Validation" ||
                    PlayerSettings.productName != "Campaign Validation")
                    throw new InvalidOperationException("Use /tmp/sneakhouse-level-validation with company " +
                        "SneakHouse Validation and product Campaign Validation. Source saves must remain untouched.");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Start map-preview validation from Edit Mode.");
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Timed Draw recovery requires a graphics device. Omit -nographics.");
                // Only the isolated profile checked above is reset.
                PlayerPrefs.SetInt(GameSession.ProgressKey, 0);
                PlayerPrefs.Save();
                SessionState.SetInt(Key + "checks", 0);
                SessionState.SetInt(Key + "settle", 12);
                SessionState.SetInt(Key + "stage", 0);
                SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 120);
                SessionState.SetBool(Key + "active", true);
                EditorSceneManager.OpenScene("Assets/SneakHouse/Scenes/PrototypeHouse.unity", OpenSceneMode.Single);
                RepaintGameView();
                Debug.Log("MAP PREVIEW VALIDATION: load real six Resources textures, clear only UI cache references, " +
                    "exercise Open, and verify timed Draw recovery while the menu pauses game time.");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Update()
        {
            if (!Active || exiting) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0))
                    throw new TimeoutException("Map-preview validation exceeded 120 seconds.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                int settle = SessionState.GetInt(Key + "settle", 0);
                if (settle > 0) { SessionState.SetInt(Key + "settle", settle - 1); return; }
                GameSession game = GameSession.Instance;
                if (game == null) return;
                if (SessionState.GetInt(Key + "stage", 0) == 0)
                {
                    Validate(game);
                    BeginDrawRecovery(game);
                    return;
                }
                // Drive the real Game View OnGUI path; never call menu.Open/Draw/Refresh directly here.
                RepaintGameView();
                EditorApplication.QueuePlayerLoopUpdate();
                if (Time.realtimeSinceStartup - SessionState.GetFloat(Key + "drawStarted", 0) < 1.5f) return;
                ValidateDrawRecovery(game);
                exiting = true; SessionState.SetBool(Key + "active", false);
                Debug.Log("MAP PREVIEW VALIDATION PASS: " + SessionState.GetInt(Key + "checks", 0) + " assertions.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Validate(GameSession game)
        {
            Check(game.Phase == GameSession.ScreenState.MainMenu, "Fixture starts at main menu");
            Check(game.Progress.HighestCompleted == 0 && game.Progress.HighestUnlocked == 1, "Fresh fixture locks levels 2-6");
            var originals = new Texture2D[CampaignLevels.Count];
            for (int i = 0; i < originals.Length; i++)
            {
                string path = "SneakHouseUI/Levels/level-" + (i + 1).ToString("00");
                originals[i] = Resources.Load<Texture2D>(path);
                CheckTexture(originals[i], i + 1, "Resources");
                Check(LevelSelectionUI.LoadPreview(i + 1) == originals[i],
                    "Menu loader retains valid Resources asset for level " + (i + 1));
                Debug.Log("MAP PREVIEW RESOURCE level=" + (i + 1) + " path=" + path +
                    " size=" + originals[i].width + "x" + originals[i].height);
            }
            MissionFlowUI flow = game.GetComponent<MissionFlowUI>();
            Check(flow != null, "Current scene owns MissionFlowUI");
            FieldInfo menuField = typeof(MissionFlowUI).GetField("levelMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(menuField != null, "Mission UI exposes expected internal menu field");
            var menu = menuField.GetValue(flow) as LevelSelectionUI;
            Check(menu != null, "Validation uses the actual current mission menu instance");
            FieldInfo cacheField = typeof(LevelSelectionUI).GetField("previews", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(cacheField != null, "Menu exposes expected internal preview cache");
            var cache = cacheField.GetValue(menu) as Texture2D[];
            Check(cache != null && cache.Length == CampaignLevels.Count, "Menu has six cached preview slots");

            // Simulate assets unavailable during construction, then imported before the menu opens.
            // Keep Resources assets alive and unchanged; only stale cache references become null.
            for (int i = 0; i < cache.Length; i++) cache[i] = null;
            menu.Open();
            Check(game.Phase == GameSession.ScreenState.LevelSelect, "Open reaches level selection");
            CheckRecovered(cacheField, menu, originals, "Open must reload missing preview");

            menu.Open();
            CheckRecovered(cacheField, menu, originals, "Repeated Open must retain preview");
            cache = (Texture2D[])cacheField.GetValue(menu);
            cache[3] = null;
            menu.Open();
            CheckRecovered(cacheField, menu, originals, "Retry must recover a single missing preview");

            game.ShowMainMenu();
            Check(game.Phase == GameSession.ScreenState.MainMenu, "Return to main menu succeeds");
            cache = (Texture2D[])cacheField.GetValue(menu);
            cache[5] = null;
            menu.Open();
            CheckRecovered(cacheField, menu, originals, "Reopening menu must recover preview");
            Check(!game.StartLevel(2), "Preview recovery preserves locked level rejection");
            Check(!game.StartLevel(0) && !game.StartLevel(CampaignLevels.Count + 1), "Invalid levels remain rejected");
            Check(game.Level.Number == 1 && game.Progress.HighestCompleted == 0 &&
                game.Phase == GameSession.ScreenState.LevelSelect, "Recovery never changes mission or completion state");
        }

        private static void CheckRecovered(FieldInfo field, LevelSelectionUI menu, Texture2D[] originals, string context)
        {
            var cache = field.GetValue(menu) as Texture2D[];
            Check(cache != null && cache.Length == originals.Length, context + ": cache shape");
            for (int i = 0; i < originals.Length; i++)
            {
                Check(cache[i] != null, context + " for level " + (i + 1));
                Check(cache[i] == originals[i], context + ": correct Resources asset for level " + (i + 1));
                CheckTexture(cache[i], i + 1, context);
            }
        }

        private static void BeginDrawRecovery(GameSession game)
        {
            LevelSelectionUI menu = CurrentMenu(game);
            FieldInfo field = typeof(LevelSelectionUI).GetField("previews", BindingFlags.Instance | BindingFlags.NonPublic);
            var cache = (Texture2D[])field.GetValue(menu);
            for (int i = 0; i < cache.Length; i++)
            {
                cache[i] = null;
                Check(cache[i] == null, "Timed Draw test clears cached preview " + (i + 1));
            }
            Check(Time.timeScale == 0, "Timed Draw fixture pauses scaled game time");
            SessionState.SetFloat(Key + "drawStarted", Time.realtimeSinceStartup);
            SessionState.SetInt(Key + "stage", 1);
            Debug.Log("MAP PREVIEW DRAW WAIT: cleared six UI cache references; waiting 1.5 real seconds " +
                "without reopening the menu or calling refresh.");
        }

        private static void ValidateDrawRecovery(GameSession game)
        {
            LevelSelectionUI menu = CurrentMenu(game);
            FieldInfo field = typeof(LevelSelectionUI).GetField("previews", BindingFlags.Instance | BindingFlags.NonPublic);
            var originals = new Texture2D[CampaignLevels.Count];
            for (int i = 0; i < originals.Length; i++)
                originals[i] = Resources.Load<Texture2D>("SneakHouseUI/Levels/level-" + (i + 1).ToString("00"));
            CheckRecovered(field, menu, originals, "Timed Game View Draw must reload missing preview");
            Check(Time.timeScale == 0, "Timed Draw recovery works with scaled game time paused");
            Check(game.Phase == GameSession.ScreenState.LevelSelect, "Timed recovery keeps level selection open");
            Check(!game.StartLevel(2) && game.Progress.HighestCompleted == 0, "Timed recovery preserves level locks");
            Debug.Log("MAP PREVIEW DRAW PASS: all six previews recovered through actual Game View drawing at timeScale=0.");
        }

        private static LevelSelectionUI CurrentMenu(GameSession game)
        {
            FieldInfo field = typeof(MissionFlowUI).GetField("levelMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            var menu = field.GetValue(game.GetComponent<MissionFlowUI>()) as LevelSelectionUI;
            Check(menu != null, "Timed validation uses current mission menu instance");
            return menu;
        }

        private static void RepaintGameView()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) throw new Exception("Unity editor Game View type is unavailable.");
            EditorWindow view = EditorWindow.GetWindow(type);
            view.Show(); view.Repaint();
        }

        private static void CheckTexture(Texture2D texture, int level, string context)
        {
            Check(texture != null, context + " texture exists for level " + level);
            Check(texture.width == 768 && texture.height == 576, context + " preserves 768x576 preview for level " + level);
        }

        private static void Check(bool condition, string message)
        {
            SessionState.SetInt(Key + "checks", SessionState.GetInt(Key + "checks", 0) + 1);
            if (!condition) throw new Exception("Map-preview assertion failed: " + message);
        }

        private static void LogReceived(string condition, string stackTrace, LogType type)
        {
            if (!Active || exiting || type == LogType.Log || type == LogType.Warning) return;
            // Known Unity 6000.6 editor startup search index failure is outside the game and Resources checks.
            if (stackTrace.Contains("UnityEditor.Search.SearchDatabase") && stackTrace.Contains("UnityEditor.Search.SearchInit"))
            {
                Debug.LogWarning("MAP PREVIEW VALIDATION: ignored known Unity QuickSearch startup exception: " + condition);
                return;
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Fail(new Exception("Runtime error during map-preview validation: " + condition + "\n" + stackTrace));
        }

        private static void Fail(Exception exception)
        {
            if (exiting) return;
            exiting = true; SessionState.SetBool(Key + "active", false);
            Debug.LogError("MAP PREVIEW VALIDATION FAILED: " + exception);
            EditorApplication.Exit(1);
        }
    }
}
