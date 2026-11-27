using SneakHouse;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SneakHouseEditor
{
    // Batch Q-learning for The Hunter: Unity -batchmode -nographics -projectPath <copy> -executeMethod
    // SneakHouseEditor.HunterTrainingRunner.Run -hunter-train -hunter-episodes 200
    // Evaluation: -hunter-eval (shipped table, greedy) or -hunter-baseline (plain FSM) with -hunter-episodes 100.
    // HunterDecision drives the missions (ThiefBot plays, records are not saved); this only enters Play Mode and exits when done.
    [InitializeOnLoad]
    public static class HunterTrainingRunner
    {
        private const string Active = "SneakHouse.HunterTraining.active", Deadline = "SneakHouse.HunterTraining.deadline";
        private const string Scene = "Assets/SneakHouse/Scenes/PrototypeHouse.unity";
        private const float SecondsPerEpisode = 90;

        static HunterTrainingRunner() { EditorApplication.update += Update; }

        public static void Run()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool mode = System.Array.Exists(HunterDecision.LoopFlags, flag => HunterArgs.Has(args, flag));
            if (!Application.isBatchMode || !mode || !HunterArgs.Has(args, "-hunter-episodes"))
            {
                Debug.LogError("HUNTER: run in batch mode with one of " + string.Join(", ", HunterDecision.LoopFlags) + ", plus -hunter-episodes <n>.");
                EditorApplication.Exit(1);
                return;
            }
            int at = System.Array.IndexOf(args, "-hunter-episodes");
            int episodes = at + 1 < args.Length && int.TryParse(args[at + 1], out int n) && n > 0 ? n : 0;
            if (episodes == 0) { Debug.LogError("HUNTER: -hunter-episodes needs a positive number."); EditorApplication.Exit(1); return; }
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 120 + episodes * SecondsPerEpisode);
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            Debug.Log("HUNTER training " + episodes + " episodes; table: " + HunterDecision.TablePath);
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0))
            {
                Debug.LogError("HUNTER: training timed out; the table saved after the last finished mission is kept.");
                Finish(1);
                return;
            }
            if (EditorApplication.isPlaying && HunterDecision.Table != null && HunterDecision.TargetEpisodes > 0 &&
                HunterDecision.CompletedEpisodes >= HunterDecision.TargetEpisodes)
            {
                Debug.Log("HUNTER run done: " + HunterDecision.CompletedEpisodes + " episodes" +
                          (HunterDecision.Training ? ", saved to " + HunterDecision.TablePath : ""));
                Finish(0);
            }
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(code);
        }
    }
}
