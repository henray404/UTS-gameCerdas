using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SneakHouse
{
    // Rule-based thief that plays a mission through PlayerMotor: the opponent for Hunter training and automated playtests.
    // F6 toggles it during play; the -thiefbot command-line flag starts it switched on.
    [RequireComponent(typeof(PlayerMotor))]
    public class ThiefBot : MonoBehaviour
    {
        public static bool Enabled;
        // -thiefbot-skill 0..1; -1 keeps the original ThiefStyle.Random behaviour.
        public static float Skill = -1;
        public const float HearRadius = 6, ThreatMemory = 6, RouteClearance = 3;
        public ThiefStyle style = ThiefStyle.Default;
        public bool randomizeStyle = true;
        [Min(1)] public float hidingSearchRadius = 8;
        public ThiefIntent Intent => intent;

        private PlayerMotor motor;
        private CharacterController controller;
        private NavMeshPath path;
        private Vector3[] corners = new Vector3[0];
        private Vector3 pathGoal;
        private int corner;
        private float repathAt, sinceThreat = 99;
        private bool wasSpotted, guardClose, prefersHiding, wantsCan, wasOnRoute, wantsWait;
        private Vector3 threatAt;
        private float threatSeen = -100, waited;
        private ThiefIntent intent;
        private System.Random rng;
        private ExtractionZone exit;
        private HidingSpot spot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Hunter training needs the bot too; read both flags here because BeforeSceneLoad order between classes is undefined.
            string[] args = System.Environment.GetCommandLineArgs();
            Enabled = HunterArgs.Has(args, "-thiefbot") || System.Array.Exists(HunterDecision.LoopFlags, flag => HunterArgs.Has(args, flag));
            float skill = HunterArgs.Float(args, "-thiefbot-skill", -1);
            Skill = skill < 0 ? -1 : Mathf.Clamp01(skill);
            SceneManager.sceneLoaded -= Attach; SceneManager.sceneLoaded += Attach;
        }
        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            PlayerMotor player = FindFirstObjectByType<PlayerMotor>();
            if (player != null && player.GetComponent<ThiefBot>() == null) player.gameObject.AddComponent<ThiefBot>();
        }
        private void Awake()
        {
            motor = GetComponent<PlayerMotor>(); controller = GetComponent<CharacterController>(); path = new NavMeshPath();
            rng = new System.Random(System.Environment.TickCount);
            if (randomizeStyle) style = Skill >= 0 ? ThiefStyle.ForSkill(Skill, rng) : ThiefStyle.Random(rng);
        }
        private void OnDisable() { if (motor != null) motor.externalControl = false; }
        private void Update()
        {
            GameSession game = GameSession.Instance;
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Enabled = !Enabled;
                if (game != null) game.Notify(Enabled ? "Thief bot ON" : "Thief bot OFF");
            }
            motor.externalControl = Enabled;
            if (!Enabled || game == null || game.Phase != GameSession.ScreenState.Playing) return;
            // Uncapped batch-mode frames make each step smaller than the default 0.001 m minimum, so the controller would not move.
            controller.minMoveDistance = 0;
            intent = ThiefPolicy.Decide(Sense(game), style);
            Act(game);
        }
        private ThiefSense Sense(GameSession game)
        {
            Vector3 eye = transform.position + Vector3.up * 1.1f;
            bool spotted = false; float nearest = float.PositiveInfinity;
            foreach (GuardBrain guard in game.guards)
            {
                if (guard == null || !guard.isActiveAndEnabled) continue;
                if (guard.State == GuardBrain.Mode.Chase || (guard.CanSeePlayer && guard.Suspicion > .5f)) spotted = true;
                float distance = Vector3.Distance(transform.position, guard.transform.position);
                // Skill only: remember where a guard was last seen or heard (footsteps within HearRadius).
                if (style.Caution > 0 && (distance < HearRadius ||
                    !Physics.Linecast(eye, guard.transform.position + Vector3.up * 1.1f, 1 << 8, QueryTriggerInteraction.Ignore)))
                { threatAt = guard.transform.position; threatSeen = Time.time; }
                if (distance >= nearest || Physics.Linecast(eye, guard.transform.position + Vector3.up * 1.1f, 1 << 8, QueryTriggerInteraction.Ignore)) continue;
                nearest = distance;
                // Walking into a patrolling guard (< .72 m) is an instant catch, so a visible guard this close is a threat too.
                if (distance < 2.5f) spotted = true;
            }
            sinceThreat = spotted ? 0 : sinceThreat + Time.deltaTime;
            if (spotted && !wasSpotted) prefersHiding = rng.NextDouble() < style.HideChance;
            bool close = nearest < ThiefPolicy.CanRange;
            if (close && !guardClose) wantsCan = rng.NextDouble() < style.CanChance;
            wasSpotted = spotted; guardClose = close;
            spot = NearestFreeSpot();
            bool onRoute = ThreatFresh && RouteNear(threatAt);
            if (onRoute && !wasOnRoute) wantsWait = rng.NextDouble() < style.Caution;
            waited = onRoute ? waited + Time.deltaTime : 0;
            wasOnRoute = onRoute;
            return new ThiefSense
            {
                Hidden = motor.IsHidden, Spotted = spotted, NearestGuard = nearest, SinceThreat = sinceThreat,
                TargetsLeft = NearestTarget(game) != null, HidingNearby = spot != null, Cans = motor.Distractions.Remaining,
                PrefersHiding = prefersHiding, WantsCan = wantsCan,
                ThreatOnRoute = onRoute, WantsWait = wantsWait, WaitedSeconds = waited
            };
        }
        private bool ThreatFresh => style.Caution > 0 && Time.time - threatSeen < ThreatMemory;
        // The remaining NavMesh corners (or the goal) pass within RouteClearance of the remembered guard.
        private bool RouteNear(Vector3 point)
        {
            if (Flat(pathGoal - point) < RouteClearance) return true;
            for (int i = corner; i < corners.Length; i++)
                if (Flat(corners[i] - point) < RouteClearance) return true;
            return false;
        }
        // Skill only: prefer a target away from the remembered guard; otherwise the nearest one.
        private LootTarget ChooseTarget(GameSession game)
        {
            if (!ThreatFresh) return NearestTarget(game);
            LootTarget best = null; float bestScore = float.PositiveInfinity;
            foreach (LootTarget loot in game.targets)
            {
                if (loot == null || loot.Collected || !game.IsRequiredTarget(loot)) continue;
                float score = Vector3.Distance(loot.transform.position, transform.position) +
                              (Flat(loot.transform.position - threatAt) < 5 ? 15 * style.Caution : 0);
                if (score < bestScore) { bestScore = score; best = loot; }
            }
            return best;
        }
        private void Act(GameSession game)
        {
            switch (intent.Goal)
            {
                case ThiefGoal.StayHidden: return;
                case ThiefGoal.LeaveHiding: motor.Hiding.TryExit(); return;
                case ThiefGoal.Hide:
                    if (Flat(spot.Approach - transform.position) < spot.interactionRadius * .8f && motor.Hiding.TryEnter()) return;
                    Steer(spot.Approach); break;
                case ThiefGoal.Flee: Steer(FleePoint(game)); break;
                case ThiefGoal.Wait: motor.Move(Vector3.zero, false, true, Time.deltaTime); break;
                case ThiefGoal.Exit:
                    ExtractionZone zone = Exit();
                    Steer(zone != null ? zone.transform.position : transform.position); break;
                default:
                    motor.RefreshLoot();
                    if (motor.NearestLoot != null && motor.TryInteract()) return;
                    Steer(ApproachOf(ChooseTarget(game), game)); break;
            }
            if (intent.DropCan && motor.Distractions.TryDrop()) wantsCan = false;
        }
        // Follows NavMesh corners with PlayerMotor.Move so speed, noise and stamina match a human player.
        private void Steer(Vector3 goal)
        {
            if (Time.time >= repathAt || (goal - pathGoal).sqrMagnitude > .25f)
            {
                repathAt = Time.time + .5f; pathGoal = goal; corner = 1;
                bool found = NavMesh.SamplePosition(transform.position, out NavMeshHit from, 1.5f, NavMesh.AllAreas) &&
                             NavMesh.SamplePosition(goal, out NavMeshHit to, 2.2f, NavMesh.AllAreas) &&
                             NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) &&
                             path.status != NavMeshPathStatus.PathInvalid;
                corners = found ? path.corners : new[] { transform.position, goal };
            }
            while (corner < corners.Length - 1 && Flat(corners[corner] - transform.position) < .3f) corner++;
            Vector3 direction = corner < corners.Length ? corners[corner] - transform.position : Vector3.zero;
            direction.y = 0;
            motor.Move(direction.sqrMagnitude > .01f ? direction.normalized : Vector3.zero, intent.Run, intent.Sneak, Time.deltaTime);
        }
        private Vector3 FleePoint(GameSession game)
        {
            GuardBrain threat = null; float best = float.PositiveInfinity;
            foreach (GuardBrain guard in game.guards)
            {
                if (guard == null || !guard.isActiveAndEnabled) continue;
                float distance = (guard.transform.position - transform.position).sqrMagnitude;
                if (distance < best) { best = distance; threat = guard; }
            }
            if (threat == null) return transform.position;
            Vector3 away = transform.position - threat.transform.position; away.y = 0;
            Vector3 point = transform.position + (away.sqrMagnitude > .01f ? away.normalized : transform.forward) * 6;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3, NavMesh.AllAreas)) return hit.position;
            // Cornered: running for the exit beats running into a wall.
            ExtractionZone zone = Exit();
            return zone != null ? zone.transform.position : transform.position;
        }
        private LootTarget NearestTarget(GameSession game)
        {
            LootTarget best = null; float bestDistance = float.PositiveInfinity;
            foreach (LootTarget loot in game.targets)
            {
                if (loot == null || loot.Collected || !game.IsRequiredTarget(loot)) continue;
                float distance = (loot.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = loot; }
            }
            return best;
        }
        // Targets sit on furniture; the layout's validated approach point is where pickup (1.55 m, clear line) works.
        internal static Vector3 ApproachOf(LootTarget loot, GameSession game)
        {
            InteriorLayout interior = game.GetComponent<InteriorLayout>();
            if (interior != null)
                for (int i = 0; i < interior.rooms.Length; i++)
                    if (interior.rooms[i].target == loot) return interior.Approach(i);
            return loot.transform.position;
        }
        private HidingSpot NearestFreeSpot()
        {
            HidingSpot best = null; float bestDistance = hidingSearchRadius;
            foreach (HidingSpot candidate in motor.Hiding.spots)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.Occupant != null) continue;
                Vector3 offset = candidate.Approach - transform.position;
                if (Mathf.Abs(offset.y) > .75f) continue;
                float distance = Flat(offset);
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }
        private ExtractionZone Exit()
        {
            if (exit != null && exit.isActiveAndEnabled) return exit;
            foreach (ExtractionZone zone in FindObjectsByType<ExtractionZone>(FindObjectsSortMode.None))
                if (zone.isActiveAndEnabled) return exit = zone;
            return null;
        }
        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        private void OnGUI()
        {
            if (!Enabled || GameSession.Instance == null || GameSession.Instance.Phase != GameSession.ScreenState.Playing) return;
            GUI.Label(new Rect(12, 12, 360, 22), "BOT " + intent.Goal + (intent.Run ? " run" : intent.Sneak ? " sneak" : "") + "  [F6]");
        }
    }
}
