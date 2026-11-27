using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SneakHouse
{
    // Tahap A of plans/rl-enemy-design.md: guards[0] becomes The Hunter and a Q-table picks where it goes after losing the player.
    // Off unless the game starts with one of (all but -hunter loop missions against ThiefBot, never save records, and unlock all levels):
    //   -hunter            play the shipped Q-table
    //   -hunter-train      Q-learning, saving the table after every mission
    //   -hunter-eval       Q-table greedy, no learning
    //   -hunter-baseline   plain FSM guard; only the mission loop and log
    //   -hunter-ppo-train  ML-Agents policy trained by mlagents-learn (HunterAgent)
    //   -hunter-ppo-eval   ML-Agents policy from Resources/Adaptive/HunterGuard.onnx
    // Options: -hunter-episodes N (players quit after N), -hunter-level 1..6|random, -hunter-table <path>.
    public class HunterDecision : MonoBehaviour
    {
        public const string TableFile = "hunter_qtable.json", TableResource = "Adaptive/hunter_qtable";
        public const float TrainSpeed = 4;
        public static readonly string[] LoopFlags = { "-hunter-train", "-hunter-eval", "-hunter-baseline", "-hunter-ppo-train", "-hunter-ppo-eval" };
        // Greedy play once gave up on every footstep (11 decisions/s for 95 minutes); after a GiveUp, new evidence is investigated.
        public const float GiveUpCooldown = 5;
        // Training/eval only: the longest FSM baseline mission was 58 s, so this only cuts stuck episodes.
        public const float MaxMissionSeconds = 180;
        public static bool Enabled, Training, Evaluating, Baseline, Ppo, PpoTrain;
        public static bool Looping => Training || Evaluating || PpoTrain;
        public static int TargetEpisodes;
        public static QLearningHunter Table { get; private set; }
        public static int CompletedEpisodes => Training ? Table.Episodes : evalEpisodes;
        private static int evalEpisodes;
        private static string tableOverride, levelSpec, seedOffsetSpec, seedSpec;
        public const float ExitRadius = 4;
        private static System.Random levelRng;
        public static string TablePath => tableOverride ?? Path.Combine(Application.persistentDataPath, TableFile);

        public HidingSpot CheckingSpot { get; private set; }
        public HunterAction LastAction { get; private set; }
        public int Decisions { get; private set; }

        private static readonly Vector3[] SweepOffsets = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
        private GuardBrain guard;
        private HidingSpot[] spots;
        private ExtractionZone exit;
        private System.Random rng;
        private bool pending, hasPrevious, ended;
        private int pendingState, sweep;
        private HunterAction pendingAction;
        private float pendingReward, lastReacquire = -100, lastGiveUp = -100, restartAt;
        private bool timedOut;
        private float exitTime; // game seconds this guard spent within ExitRadius of the extraction zone (camping check)
        private LootTarget nearestTarget;
        private HidingSpot nearestSpot;
        private readonly int[] histogram = new int[HunterContract.ActionCount];
        private NavMeshPath path;
        private HunterAgent agent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            Training = HunterArgs.Has(args, "-hunter-train");
            PpoTrain = !Training && HunterArgs.Has(args, "-hunter-ppo-train");
            Ppo = PpoTrain || HunterArgs.Has(args, "-hunter-ppo-eval");
            Baseline = HunterArgs.Has(args, "-hunter-baseline");
            Evaluating = !Training && !PpoTrain && (Baseline || HunterArgs.Has(args, "-hunter-eval") || HunterArgs.Has(args, "-hunter-ppo-eval"));
            Enabled = Looping || HunterArgs.Has(args, "-hunter");
            GameSession.UnlockAll = Looping;
            evalEpisodes = 0;
            TargetEpisodes = HunterArgs.Int(args, "-hunter-episodes", 0);
            levelSpec = HunterArgs.Value(args, "-hunter-level");
            levelRng = new System.Random(System.Environment.TickCount);
            GameSession.SoloHunter = Looping && HunterArgs.Has(args, "-hunter-solo");
            seedOffsetSpec = HunterArgs.Value(args, "-hunter-seed-offset");
            seedSpec = HunterArgs.Value(args, "-hunter-seed");
            GameSession.SeedOffset = Looping ? HunterArgs.SeedOffset(seedOffsetSpec, seedSpec, levelRng) : 0;
            string table = HunterArgs.Value(args, "-hunter-table");
            tableOverride = table == null ? null : Path.GetFullPath(table);
            if (!Enabled) return;
            Table = LoadTable();
            Table.EpsilonDecayEpisodes = Mathf.Max(1, HunterArgs.Int(args, "-hunter-eps-decay", QLearningHunter.DefaultEpsilonDecayEpisodes));
            if (Ppo) HunterAgent.PrepareAcademy();
            SceneManager.sceneLoaded -= Attach; SceneManager.sceneLoaded += Attach;
        }
        // -hunter-table wins; otherwise training resumes from the local save and play prefers the table shipped in Resources.
        private static QLearningHunter LoadTable()
        {
            TextAsset shipped = Training || tableOverride != null ? null : Resources.Load<TextAsset>(TableResource);
            string json = shipped != null ? shipped.text : File.Exists(TablePath) ? File.ReadAllText(TablePath) : null;
            if (QLearningHunter.TryParse(json, out QLearningHunter table)) return table;
            if (json != null) Debug.LogWarning("HUNTER: Q-table does not match the 48x6 contract; starting from zeros.");
            return new QLearningHunter();
        }
        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            GameSession game = FindFirstObjectByType<GameSession>();
            if (game == null || game.guards == null || game.guards.Length == 0 || game.guards[0] == null) return;
            GuardBrain first = game.guards[0];
            if (first.GetComponent<HunterDecision>() != null) return;
            HunterDecision decision = first.gameObject.AddComponent<HunterDecision>();
            // Baseline keeps the component for the mission loop and log but leaves the FSM in charge.
            if (!Baseline) first.hunter = decision;
            if (Ppo) decision.agent = HunterAgent.AddTo(first.gameObject, inference: !PpoTrain);
        }
        private void Awake()
        {
            guard = GetComponent<GuardBrain>(); path = new NavMeshPath();
            rng = new System.Random(System.Environment.TickCount);
            spots = FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
        }

        // Called by GuardBrain whenever the Hunter needs a destination. False means GiveUp (return to patrol).
        public bool Decide(float now, out Vector3 point)
        {
            HunterSituation s = Situation(now);
            int state = HunterContract.StateIndex(s);
            bool[] allowed = QLearningHunter.Allowed(s);
            if (now - lastGiveUp < GiveUpCooldown) allowed[(int)HunterAction.GiveUp] = false;
            if (pending && Training) Table.Update(pendingState, pendingAction, pendingReward, state, allowed, false);
            HunterAction action = agent != null ? agent.Decide(s, allowed, pending ? pendingReward : 0)
                                                : Table.Choose(state, allowed, Training ? Table.Epsilon : 0, rng);
            pending = true; pendingState = state; pendingAction = action; pendingReward = HunterContract.StepReward;
            LastAction = action; hasPrevious = true; Decisions++; histogram[(int)action]++;
            CheckingSpot = null;
            point = guard.LastKnownPosition;
            switch (action)
            {
                case HunterAction.SweepSector: point += SweepOffsets[sweep++ % SweepOffsets.Length] * 1.7f; break;
                case HunterAction.InterceptExit: point = InterceptPoint(guard.LastKnownPosition); break;
                case HunterAction.AmbushTarget: point = ThiefBot.ApproachOf(nearestTarget, GameSession.Instance); break;
                case HunterAction.CheckHidingSpot: CheckingSpot = nearestSpot; point = nearestSpot.Approach; break;
                case HunterAction.GiveUp: lastGiveUp = now; return false;
            }
            return true;
        }
        // The Hunter learns whether a spot is occupied only after standing at it (GuardBrain waits 0.8 s first).
        public bool ResolveCheck()
        {
            HidingSpot spot = CheckingSpot; CheckingSpot = null;
            if (spot == null) return false;
            if (spot.Occupant != null && spot.Inspect(guard))
            {
                pendingReward += HunterContract.FoundHiddenReward; guard.CaughtPlayer = true; return true;
            }
            pendingReward += HunterContract.EmptyHidingCheckReward;
            return false;
        }
        public void OnChase(float now)
        {
            if (!pending || now - lastReacquire < HunterContract.ReacquireCooldown) return;
            pendingReward += HunterContract.ReacquireReward; lastReacquire = now;
        }

        private HunterSituation Situation(float now)
        {
            GameSession game = GameSession.Instance;
            Vector3 self = guard.transform.position, last = guard.LastKnownPosition;
            var s = new HunterSituation
            {
                MemoryAge = now - guard.LastEvidenceTime, Memory = HunterContract.KindFromSource(guard.MemorySource),
                Suspicion = guard.Suspicion, CollectedRatio = game.Collected / (float)Mathf.Max(1, game.RequiredTargetCount),
                HasPrevious = hasPrevious, Previous = LastAction
            };
            Flat(last - self, out s.ToLastKnownX, out s.ToLastKnownZ);
            if (s.Memory == MemoryKind.Vision) Flat(guard.LastSeenVelocity, out s.LastSeenVelocityX, out s.LastSeenVelocityZ);
            ExtractionZone zone = Exit();
            if (zone != null) Flat(zone.transform.position - last, out s.LastKnownToExitX, out s.LastKnownToExitZ);
            nearestTarget = null; float best = float.PositiveInfinity;
            foreach (LootTarget loot in game.targets)
            {
                if (loot == null || loot.Collected || !game.IsRequiredTarget(loot)) continue;
                float distance = (loot.transform.position - self).sqrMagnitude;
                if (distance < best) { best = distance; nearestTarget = loot; }
            }
            s.HasTarget = nearestTarget != null;
            if (s.HasTarget) Flat(nearestTarget.transform.position - self, out s.ToTargetX, out s.ToTargetZ);
            nearestSpot = null; best = HunterContract.HidingScale;
            foreach (HidingSpot spot in spots)
            {
                if (spot == null || !spot.isActiveAndEnabled) continue;
                float distance = Vector3.Distance(spot.Approach, last);
                if (distance < best) { best = distance; nearestSpot = spot; }
            }
            s.HasHidingSpot = nearestSpot != null;
            if (s.HasHidingSpot) Flat(nearestSpot.Approach - last, out s.LastKnownToHidingX, out s.LastKnownToHidingZ);
            foreach (GuardBrain other in game.guards)
                if (other != null && other != guard && other.isActiveAndEnabled &&
                    (other.State == GuardBrain.Mode.Search || other.State == GuardBrain.Mode.Chase)) s.OtherGuardsHunting++;
            return s;
        }
        // About 60% along the NavMesh route from the evidence to the exit: where a fleeing player must pass.
        private Vector3 InterceptPoint(Vector3 from)
        {
            ExtractionZone zone = Exit();
            if (zone == null) return from;
            Vector3 to = zone.transform.position;
            if (!NavMesh.SamplePosition(from, out NavMeshHit a, 2.2f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out NavMeshHit b, 2.2f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.corners.Length < 2)
                return Vector3.Lerp(from, to, .6f);
            Vector3[] corners = path.corners;
            float total = 0;
            for (int i = 1; i < corners.Length; i++) total += Vector3.Distance(corners[i - 1], corners[i]);
            float remaining = total * .6f;
            for (int i = 1; i < corners.Length; i++)
            {
                float leg = Vector3.Distance(corners[i - 1], corners[i]);
                if (remaining <= leg) return Vector3.Lerp(corners[i - 1], corners[i], leg > 0 ? remaining / leg : 0);
                remaining -= leg;
            }
            return corners[corners.Length - 1];
        }
        private ExtractionZone Exit()
        {
            if (exit != null && exit.isActiveAndEnabled) return exit;
            foreach (ExtractionZone zone in FindObjectsByType<ExtractionZone>(FindObjectsSortMode.None))
                if (zone.isActiveAndEnabled) return exit = zone;
            return null;
        }
        private static void Flat(Vector3 v, out float x, out float z) { x = v.x; z = v.z; }

        private void Update()
        {
            GameSession game = GameSession.Instance;
            if (game == null) return;
            if (game.Phase == GameSession.ScreenState.Playing && !ended)
            {
                ExtractionZone zone = Exit();
                Vector3 offset = zone != null ? guard.transform.position - zone.transform.position : Vector3.positiveInfinity;
                offset.y = 0;
                if (offset.magnitude < ExitRadius) exitTime += Time.deltaTime;
            }
            if (game.Finished && !ended) EndMission(game);
            if (!Looping) return;
            if (game.Phase == GameSession.ScreenState.Playing && !ended && game.Elapsed > MaxMissionSeconds)
            {
                timedOut = true; EndMission(game); game.Restart(); return;
            }
            bool done = TargetEpisodes > 0 && CompletedEpisodes >= TargetEpisodes;
            if (game.Phase == GameSession.ScreenState.MainMenu) game.StartLevel(NextLevel());
            else if (game.Phase == GameSession.ScreenState.Briefing && game.MissionReady)
            {
                game.PersistRecords = false; game.BeginMission(); Time.timeScale = TrainSpeed;
            }
            else if (game.Phase == GameSession.ScreenState.Results && ended && Time.unscaledTime >= restartAt)
            {
                // The editor runner exits itself; a player build has nothing else to stop it.
                if (done) { if (!Application.isEditor) Application.Quit(); }
                else game.StartLevel(NextLevel());
            }
        }
        private static int NextLevel()
        {
            int level = HunterArgs.Level(levelSpec, 1, levelRng);
            GameSession.SeedOffset = HunterArgs.SeedOffset(seedOffsetSpec, seedSpec, levelRng);
            return PpoTrain ? HunterAgent.CurriculumLevel(level) : level;
        }
        private void EndMission(GameSession game)
        {
            ended = true; restartAt = Time.unscaledTime + .5f;
            float terminal = guard.CaughtPlayer ? HunterContract.CatchReward : game.Won ? HunterContract.EscapeReward : 0;
            if (pending && Training)
                Table.Update(pendingState, pendingAction, pendingReward + terminal, 0, QLearningHunter.Allowed(default), true);
            // An episode without any decision has nothing to credit; the agent's removal on scene reload closes it.
            if (pending && agent != null) agent.Finish(pendingReward + terminal);
            pending = false;
            if (Training)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TablePath));
                Table.Episodes++; File.WriteAllText(TablePath, Table.ToJson());
            }
            else if (Looping) evalEpisodes++;
            else return;
            Debug.Log($"HUNTER episode={CompletedEpisodes} eps={(Training ? Table.Epsilon : 0):F2} level={game.Level.Number} won={game.Won} " +
                      $"hunterCaught={guard.CaughtPlayer} time={game.Elapsed:F1} decisions={Decisions} actions=[{string.Join(",", histogram)}]" +
                      (timedOut ? " timeout=True" : "") +
                      // New fields go last: ml/ parsers match the older fields in order.
                      $" seed={GameSession.LayoutSeed} solo={GameSession.SoloHunter} skill={ThiefBot.Skill:F2} exitTime={exitTime:F1}");
        }
    }
}
