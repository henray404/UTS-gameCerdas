using System;
using System.IO;
using SneakHouse;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SneakHouseEditor
{
    // Batch smoke test. Run only in the isolated copy described by Run's safety checks.
    [InitializeOnLoad]
    public static class CampaignValidation
    {
        private const string Prefix = "SneakHouse.CampaignValidation.";
        private const string Scene = "Assets/SneakHouse/Scenes/PrototypeHouse.unity";
        private static bool exiting;

        static CampaignValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Application.logMessageReceived += LogReceived;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                Debug.LogError("Campaign validation requires an isolated batch-mode Unity process.");
                return;
            }
            try
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (project != "/tmp/sneakhouse-level-validation" ||
                    PlayerSettings.companyName != "SneakHouse Validation" ||
                    PlayerSettings.productName != "Campaign Validation")
                    throw new InvalidOperationException("Use /tmp/sneakhouse-level-validation with company " +
                        "SneakHouse Validation and product Campaign Validation. Source profile must remain untouched.");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Start validation from Edit Mode.");
                PlayerPrefs.DeleteKey(GameSession.ProgressKey);
                for (int i = 1; i <= CampaignLevels.Count; i++) PlayerPrefs.DeleteKey(ScoreKey(i));
                PlayerPrefs.Save();
                SessionState.SetBool(Prefix + "active", true);
                SessionState.SetInt(Prefix + "checks", 0);
                SessionState.SetFloat(Prefix + "deadline", (float)EditorApplication.timeSinceStartup + 300);
                SetStep(0);
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                Debug.Log("CAMPAIGN VALIDATION: physical pickup/navigation/progression checks; " +
                    "guards disabled after roster assertions to isolate collection from capture AI; " +
                    "synthetic alerts make each level's saved score distinct.");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!Active) return;
            if (state == PlayModeStateChange.EnteredPlayMode) SessionState.SetInt(Prefix + "settle", 3);
            if (state == PlayModeStateChange.EnteredEditMode)
                Fail(new Exception("Play Mode ended before all campaign checks passed."));
        }

        private static bool Active => SessionState.GetBool(Prefix + "active", false);
        private static string ScoreKey(int level) => "SneakHouse.Score.Campaign.v1.Level." + level;

        private static void Update()
        {
            if (!Active || exiting) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "deadline", 0))
                    throw new TimeoutException("Campaign validation exceeded 300 seconds.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                int settle = SessionState.GetInt(Prefix + "settle", 0);
                if (settle > 0) { SessionState.SetInt(Prefix + "settle", settle - 1); return; }
                GameSession game = GameSession.Instance;
                if (game == null) return;
                Tick(game, SessionState.GetInt(Prefix + "step", 0));
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Tick(GameSession game, int step)
        {
            switch (step)
            {
                case 0:
                    Check(game.Phase == GameSession.ScreenState.MainMenu, "Fresh launch opens main menu");
                    Check(game.Progress.HighestCompleted == 0, "Fresh completion count");
                    for (int i = 1; i <= CampaignLevels.Count; i++)
                        Check(game.Progress.IsUnlocked(i) == (i == 1), "Fresh lock level " + i);
                    Check(!game.StartLevel(2), "Locked level launch rejected");
                    game.ShowLevelSelect();
                    Check(game.Phase == GameSession.ScreenState.LevelSelect, "Level selector opens");
                    SetStep(1);
                    Check(game.StartLevel(1), "Level 1 selectable");
                    break;
                case 1:
                    VerifyScene(game, 1);
                    Begin(game);
                    Check(!Exit().Check(Exit().transform.position), "Incomplete extraction rejected");
                    game.End(true);
                    Check(!game.Finished, "Incomplete direct win rejected");
                    game.End(false);
                    Check(game.Finished && !game.Won, "Failure reaches results");
                    Check(game.Progress.HighestCompleted == 0 && !game.Progress.IsUnlocked(2), "Loss keeps next locked");
                    Check(game.BestScore == 0 && PlayerPrefs.GetInt(ScoreKey(1), 0) == 0, "Loss awards no best score");
                    SetStep(2); game.Restart();
                    break;
                case 2:
                    VerifyScene(game, 1);
                    Begin(game); CollectAndExtract(game);
                    VerifyWin(game, 1);
                    SetStep(3); game.Restart();
                    break;
                case 3:
                    VerifyScene(game, 1);
                    Check(game.Progress.HighestCompleted == 1, "Replay reload retains completion");
                    Check(game.BestScore == SavedScore(1), "Replay reload retains own score");
                    Begin(game); CollectAndExtract(game);
                    Check(game.Progress.HighestCompleted == 1 && !game.NewlyUnlocked, "Replay cannot advance completion");
                    SessionState.SetInt(Prefix + "score1", game.BestScore);
                    SessionState.SetInt(Prefix + "next", 2);
                    SetStep(4); game.ShowLevelSelect();
                    break;
                case 4:
                    int next = SessionState.GetInt(Prefix + "next", 2);
                    Check(game.Phase == GameSession.ScreenState.LevelSelect, "Results return to selector");
                    Check(game.Progress.HighestCompleted == next - 1, "Scene reload retains completion " + (next - 1));
                    VerifySavedScores(next - 1);
                    SetStep(5);
                    Check(game.StartLevel(next), "Next unlocked level selectable " + next);
                    break;
                case 5:
                    int number = SessionState.GetInt(Prefix + "next", 2);
                    VerifyScene(game, number);
                    Check(game.Progress.HighestCompleted == number - 1, "Selected level retains progress");
                    Check(game.BestScore == 0, "New level does not inherit another best score");
                    Begin(game); CollectAndExtract(game); VerifyWin(game, number);
                    if (number < CampaignLevels.Count)
                    {
                        SessionState.SetInt(Prefix + "next", number + 1);
                        SetStep(4); game.ShowLevelSelect();
                    }
                    else { SetStep(6); game.Restart(); }
                    break;
                case 6:
                    VerifyScene(game, CampaignLevels.Count);
                    Check(game.Progress.HighestCompleted == CampaignLevels.Count, "Final completion survives scene reload");
                    Check(game.Progress.HighestUnlocked == CampaignLevels.Count, "No seventh level created");
                    Check(game.BestScore == SavedScore(CampaignLevels.Count), "Final score reload uses correct level key");
                    VerifySavedScores(CampaignLevels.Count);
                    game.ShowLevelSelect();
                    Check(game.Phase == GameSession.ScreenState.LevelSelect, "Completed campaign selector opens");
                    Finish();
                    break;
                default: throw new Exception("Unknown validation step " + step);
            }
        }

        private static void VerifyScene(GameSession game, int number)
        {
            CampaignLevel level = CampaignLevels.Get(number);
            HouseShell shell = game.GetComponent<HouseShell>();
            InteriorLayout interior = game.GetComponent<InteriorLayout>();
            Check(game.Level.Number == number && game.Phase == GameSession.ScreenState.Briefing, "Selected briefing " + number);
            Check(game.MissionReady && interior.Validated, "Mission route validation " + number + ": " + interior.ValidationMessage);
            Check(shell.VariantIndex == level.ShellIndex && shell.Label == level.MapName, "Exact selected map " + number);
            Check(shell.CurrentSeed == level.LayoutSeed, "Deterministic map seed " + number);
            Check(shell.UsableArea > 0, "Measured walkable house area " + number);
            Check(game.ActiveGuards == level.Guards, "Configured guard count " + number);
            int activeGuards = 0, activeTargets = 0;
            foreach (GuardBrain guard in game.guards)
            {
                if (guard.gameObject.activeInHierarchy) activeGuards++;
                Check(Mathf.Approximately(guard.visionDistance, level.VisionDistance) &&
                    Mathf.Approximately(guard.fieldOfView, level.FieldOfView) &&
                    Mathf.Approximately(guard.searchDuration, level.SearchSeconds), "Guard senses match catalogue " + number);
            }
            foreach (LootTarget target in game.targets)
            {
                if (target.gameObject.activeInHierarchy) activeTargets++;
                Check(target.gameObject.activeSelf == game.IsRequiredTarget(target), "Exact required target activation " + number);
            }
            Check(activeGuards == level.Guards && activeTargets == level.TargetCount, "Active roster/objective count " + number);
            Check(game.player.Distractions.Remaining == level.Cans, "Starting cans " + number);
            Check(Mathf.Approximately(game.targetTime, level.ReferenceSeconds), "Reference duration " + number);
            Debug.Log("CAMPAIGN MEASURE level=" + number + " map=" + shell.Label + " shellSeed=" + shell.CurrentSeed +
                " interiorSeed=" + interior.CurrentSeed + " usableArea=" + shell.UsableArea.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " guards=" + activeGuards + " targets=" + activeTargets + " cans=" + game.player.Distractions.Remaining);
        }

        private static void Begin(GameSession game)
        {
            // Capture AI is deliberately outside this deterministic progression smoke test.
            foreach (GuardBrain guard in game.guards) guard.gameObject.SetActive(false);
            game.BeginMission();
            Check(game.Phase == GameSession.ScreenState.Playing, "Briefing starts validated mission");
            for (int i = 1; i < game.Level.Number; i++)
                game.Broadcast(game.guards[0], game.player.transform.position, Time.time);
        }

        private static void CollectAndExtract(GameSession game)
        {
            InteriorLayout interior = game.GetComponent<InteriorLayout>();
            ExtractionZone exit = Exit();
            Check(!exit.Check(exit.transform.position), "Empty extraction rejected level " + game.Level.Number);
            foreach (LootTarget target in game.targets)
            {
                if (game.IsRequiredTarget(target)) continue;
                game.AddLoot(target);
                Check(game.Collected == 0, "Non-objective cannot be collected");
            }
            int collected = 0;
            foreach (int index in game.Level.TargetIndices)
            {
                LootTarget target = game.targets[index];
                int room = Array.FindIndex(interior.rooms, candidate => candidate.target == target);
                Check(room >= 0, "Objective has authored room approach");
                game.player.Teleport(interior.Approach(room));
                game.player.RefreshLoot();
                Check(game.player.NearestLoot == target, "Target physically reachable " + target.Label);
                Check(game.player.TryInteract(), "Interact succeeds " + target.Label);
                collected++;
                Check(target.Collected && game.Collected == collected, "Pickup records once " + target.Label);
                game.AddLoot(target);
                Check(game.Collected == collected, "Duplicate pickup ignored " + target.Label);
            }
            Check(!exit.Check(game.player.transform.position), "Items alone do not win outside exit");
            Check(!exit.Check(exit.transform.position + Vector3.up * 3), "Wrong floor cannot extract");
            game.player.Teleport(exit.transform.position);
            Check(exit.Check(game.player.transform.position), "Actual extraction succeeds with all required items");
        }

        private static ExtractionZone Exit()
        {
            ExtractionZone exit = UnityEngine.Object.FindFirstObjectByType<ExtractionZone>();
            Check(exit != null, "Authored extraction zone exists");
            return exit;
        }

        private static void VerifyWin(GameSession game, int number)
        {
            Check(game.Finished && game.Won && game.Phase == GameSession.ScreenState.Results, "Win reaches results " + number);
            Check(game.Progress.HighestCompleted == number, "Win completes exactly current level " + number);
            Check(game.Progress.HighestUnlocked == Math.Min(number + 1, CampaignLevels.Count), "Win unlocks only next " + number);
            Check(game.NewlyUnlocked == (number < CampaignLevels.Count), "New unlock indicator " + number);
            Check(game.ResultScore == 10000 - 500 * (number - 1) && game.BestScore == game.ResultScore,
                "Distinct per-level win score stored " + number);
            SessionState.SetInt(Prefix + "score" + number, game.BestScore);
            Check(PlayerPrefs.GetInt(GameSession.ProgressKey, -1) == number, "Completion persisted " + number);
            VerifySavedScores(number);
            if (number + 2 <= CampaignLevels.Count)
                Check(!game.StartLevel(number + 2), "Cannot skip next level after win " + number);
        }

        private static int SavedScore(int number) => SessionState.GetInt(Prefix + "score" + number, -1);
        private static void VerifySavedScores(int completed)
        {
            for (int i = 1; i <= CampaignLevels.Count; i++)
                Check(PlayerPrefs.GetInt(ScoreKey(i), 0) == (i <= completed ? SavedScore(i) : 0), "Independent score key " + i);
        }

        private static void SetStep(int step)
        {
            SessionState.SetInt(Prefix + "step", step);
            SessionState.SetInt(Prefix + "settle", 3);
        }

        private static void Check(bool condition, string message)
        {
            SessionState.SetInt(Prefix + "checks", SessionState.GetInt(Prefix + "checks", 0) + 1);
            if (!condition) throw new Exception("Campaign assertion failed: " + message);
        }

        private static void LogReceived(string condition, string stackTrace, LogType type)
        {
            // Unity 6000.6 can throw in its editor search index during Play Mode startup.
            // This is outside the game; all other runtime errors still fail validation.
            if (Active && stackTrace.Contains("UnityEditor.Search.SearchDatabase") &&
                stackTrace.Contains("UnityEditor.Search.SearchInit"))
            {
                Debug.LogWarning("Campaign validation: unrelated Unity Editor search-index startup exception ignored.");
                return;
            }
            if (Active && EditorApplication.isPlaying && !exiting &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Fail(new Exception("Runtime error during validation: " + condition + "\n" + stackTrace));
        }

        private static void Fail(Exception exception)
        {
            if (exiting) return;
            exiting = true; SessionState.SetBool(Prefix + "active", false);
            Debug.LogError("CAMPAIGN VALIDATION FAILED: " + exception);
            EditorApplication.Exit(1);
        }

        private static void Finish()
        {
            exiting = true; SessionState.SetBool(Prefix + "active", false);
            Debug.Log("CAMPAIGN VALIDATION PASS: " + SessionState.GetInt(Prefix + "checks", 0) +
                " assertions across all " + CampaignLevels.Count + " levels.");
            EditorApplication.Exit(0);
        }
    }
}
