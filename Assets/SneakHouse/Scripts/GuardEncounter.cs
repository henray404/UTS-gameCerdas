using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SneakHouse
{
    // Navigation is ready before this runs; guard Awake/Start run after assignment.
    [DefaultExecutionOrder(-250), RequireComponent(typeof(GameSession))]
    public class GuardEncounter : MonoBehaviour
    {
        [Serializable] public class Character
        {
            public string id, displayName;
            public GameObject visualPrefab;
            public float eyeHeight = 1.2f, bodyHeight = 1.8f;
        }
        [Serializable] public class Zone
        {
            public GuardBrain guard;
            public Transform[] route;
        }
        [Serializable] public class Assignment
        {
            public int characterIndex, startIndex;
            public bool reversed;
            public float pause;
            public Vector3 position;
            public Quaternion rotation;
        }
        [Serializable] public class Plan
        {
            public int seed;
            public Assignment[] assignments;
            public string signature;
        }
        public Character[] roster = new Character[0];
        public Zone[] zones = new Zone[0];
        public bool useFixedSeed;
        public int fixedSeed = 1042;
        public Plan CurrentPlan { get; private set; }
        public bool Validated { get; private set; }
        public string ValidationMessage { get; private set; } = "Authored patrol fallback";
        // True when guards walk the level's PatrolPlans beats rather than the authored room routes.
        public bool UsingBeats { get; private set; }
        private static string previousSignature;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHistory() { previousSignature = null; }
        private void Awake()
        {
            GameSession game = GetComponent<GameSession>();
            IReadOnlyList<GuardBeat> beats = PatrolPlans.For(GameSession.SelectedLevel.Number);
            // Level beats first; if they fail validation (stale layout, blocked node) fall back to the authored room routes.
            for (int pass = 0; pass < 2; pass++)
            {
                bool useBeats = pass == 0;
                if (useBeats && (beats == null || beats.Count < game.ActiveGuards)) continue;
                int seed = useFixedSeed ? fixedSeed : Guid.NewGuid().GetHashCode() & int.MaxValue;
                for (int attempt = 0; attempt < 8; attempt++, seed = unchecked(seed + 1))
                {
                    if (!TryBuildPlan(seed, useBeats ? beats : null, out Plan plan)) break;
                    if (!useFixedSeed && plan.signature == previousSignature) continue;
                    Apply(plan, useBeats ? beats : null); previousSignature = plan.signature;
                    return;
                }
                if (useBeats) Debug.LogWarning("Level patrol beats failed validation; using authored room routes.", this);
            }
            Debug.LogWarning("Encounter validation failed; keeping authored patrols.", this);
        }
        public bool TryBuildPlan(int seed, out Plan plan) => TryBuildPlan(seed, null, out plan);
        public bool TryBuildPlan(int seed, IReadOnlyList<GuardBeat> beats, out Plan plan)
        {
            plan = null;
            GameSession game = GetComponent<GameSession>();
            if (zones.Length < game.ActiveGuards || game.player == null) return false;
            var valid = new List<int>();
            var ids = new HashSet<string>();
            for (int i = 0; i < roster.Length; i++)
            {
                Character character = roster[i];
                if (character == null || character.visualPrefab == null || string.IsNullOrEmpty(character.id)) continue;
                Animator animator = character.visualPrefab.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null && ids.Add(character.id)) valid.Add(i);
            }
            if (valid.Count < game.ActiveGuards) return false;
            var random = new System.Random(seed);
            for (int i = valid.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); int temp = valid[i]; valid[i] = valid[j]; valid[j] = temp; }
            var approaches = new List<Vector3>();
            if (beats != null && !CollectApproaches(game, approaches)) return false;
            var assignments = new Assignment[game.ActiveGuards];
            string signature = "";
            for (int i = 0; i < assignments.Length; i++)
            {
                Zone zone = zones[i];
                if (zone.guard == null) return false;
                Vector3[] route = beats != null ? Points(beats[i].Home, beats[i].Floor) : Points(zone.route);
                if (route == null || route.Length < 2 || !RouteConnected(route, true)) return false;
                if (beats != null && !BeatValid(beats[i], route, approaches, game.Level.PatrolClearance)) return false;
                int start = random.Next(route.Length);
                bool reversed = random.Next(2) == 1;
                Character character = roster[valid[i]];
                Assignment assignment = null;
                for (int candidate = 0; candidate < route.Length; candidate++)
                {
                    int index = (start + candidate) % route.Length;
                    int next = (index + (reversed ? route.Length - 1 : 1)) % route.Length;
                    if (!NavMesh.SamplePosition(route[index], out NavMeshHit hit, .35f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(hit.position.y - route[index].y) > .35f) continue;
                    Vector3 forward = route[next] - hit.position; forward.y = 0;
                    if (forward.sqrMagnitude < .01f) continue;
                    Quaternion rotation = Quaternion.LookRotation(forward);
                    if (!SafeSpawn(game.player.transform.position, zone.guard, character, hit.position, rotation)) continue;
                    bool separated = true;
                    for (int j = 0; j < i; j++) separated &= Vector3.Distance(assignments[j].position, hit.position) >= 2;
                    if (!separated) continue;
                    assignment = new Assignment { characterIndex = valid[i], startIndex = index, reversed = reversed,
                        pause = .5f + (float)random.NextDouble() * .6f, position = hit.position, rotation = rotation };
                    break;
                }
                if (assignment == null) return false;
                assignments[i] = assignment;
                signature += character.id + ":" + assignment.startIndex + ":" + assignment.reversed + ";";
            }
            plan = new Plan { seed = seed, assignments = assignments, signature = signature };
            return true;
        }
        private static Vector3[] Points(Transform[] route)
        {
            if (route == null) return null;
            var points = new Vector3[route.Length];
            for (int i = 0; i < points.Length; i++)
            {
                if (route[i] == null) return null;
                points[i] = route[i].position;
            }
            return points;
        }
        private static Vector3[] Points(IReadOnlyList<PatrolPoint> route, float floor)
        {
            var points = new Vector3[route.Count];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector3(route[i].X, floor, route[i].Z);
            return points;
        }
        // Closed routes loop back to the first point; open ones end at the last.
        private static bool RouteConnected(Vector3[] points, bool closed)
        {
            NavMeshPath path = new NavMeshPath();
            int pairs = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < pairs; i++)
                if (!NavMesh.SamplePosition(points[i], out NavMeshHit a, .35f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(points[(i + 1) % points.Length], out NavMeshHit b, .35f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            return true;
        }
        // Pickup spots of the level's required targets, as the player would stand to collect them.
        private bool CollectApproaches(GameSession game, List<Vector3> approaches)
        {
            InteriorLayout interior = GetComponent<InteriorLayout>();
            if (interior == null || interior.UsingFallback) return false;
            foreach (int index in game.Level.TargetIndices)
            {
                int room = Array.FindIndex(interior.rooms, candidate => candidate.target == game.targets[index]);
                if (room < 0) return false;
                approaches.Add(interior.Approach(room));
            }
            return true;
        }
        // Home loop keeps its clearance from every pickup on the same floor; the peek trip must be walkable.
        private static bool BeatValid(GuardBeat beat, Vector3[] home, List<Vector3> approaches, float clearance)
        {
            if (beat.Peek.Count == 0 || beat.PeekEvery < 1) return false;
            foreach (Vector3 point in home)
                foreach (Vector3 approach in approaches)
                    if (Mathf.Abs(point.y - approach.y) < 1.5f &&
                        Vector3.ProjectOnPlane(point - approach, Vector3.up).magnitude < clearance) return false;
            var trip = new List<Vector3> { home[0] };
            trip.AddRange(Points(beat.Peek, beat.Floor)); trip.Add(home[0]);
            return RouteConnected(trip.ToArray(), false);
        }
        private Transform[] Nodes(string label, Vector3[] points)
        {
            var nodes = new Transform[points.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new GameObject(label + " " + (i + 1)).transform;
                nodes[i].SetParent(transform, false); nodes[i].position = points[i];
            }
            return nodes;
        }
        private static bool SafeSpawn(Vector3 player, GuardBrain guard, Character character, Vector3 position, Quaternion rotation)
        {
            if (Physics.CheckCapsule(position + Vector3.up * .48f, position + Vector3.up * (character.bodyHeight - .4f),
                .4f, 1 << 8, QueryTriggerInteraction.Ignore)) return false;
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.SamplePosition(player, out NavMeshHit playerHit, 1, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(position, playerHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            float distance = 0;
            for (int i = 1; i < path.corners.Length; i++) distance += Vector3.Distance(path.corners[i-1],path.corners[i]);
            if (distance < 8) return false;
            Vector3 eye = position + Vector3.up * character.eyeHeight;
            Vector3 direction = player + Vector3.up * 1.05f - eye;
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            bool inView = flat.magnitude <= guard.visionDistance && Vector3.Angle(rotation * Vector3.forward, flat) <= guard.fieldOfView * .5f;
            return !inView || Physics.Raycast(eye, direction.normalized, direction.magnitude, 1 << 8, QueryTriggerInteraction.Ignore);
        }
        private void Apply(Plan plan, IReadOnlyList<GuardBeat> beats)
        {
            for (int i = 0; i < plan.assignments.Length; i++)
            {
                Zone zone = zones[i]; GuardBrain guard = zone.guard; Assignment assignment = plan.assignments[i];
                Character character = roster[assignment.characterIndex];
                GameObject old = guard.acting.animator.gameObject;
                old.SetActive(false); Destroy(old);
                GameObject visual = Instantiate(character.visualPrefab, guard.transform, false);
                visual.name = "Visual / " + character.displayName;
                guard.acting.animator = visual.GetComponent<Animator>();
                guard.acting.walkReferenceSpeed = 1.4f; guard.acting.runReferenceSpeed = 3.2f;
                guard.characterName = character.displayName; guard.eyeHeight = character.eyeHeight;
                guard.name = "Guard 0" + (i + 1) + " / " + character.displayName + " / " + guard.patrolZone;
                guard.patrolPause = assignment.pause;
                Transform[] route = zone.route;
                guard.peek = null; guard.peekEvery = 0;
                if (beats != null)
                {
                    GuardBeat beat = beats[i];
                    route = Nodes("Guard 0" + (i + 1) + " home", Points(beat.Home, beat.Floor));
                    guard.peek = Nodes("Guard 0" + (i + 1) + " peek", Points(beat.Peek, beat.Floor));
                    guard.peekEvery = beat.PeekEvery;
                    guard.patrolZone = beat.Zone;
                    guard.name = "Guard 0" + (i + 1) + " / " + character.displayName + " / " + guard.patrolZone;
                }
                guard.patrol = new Transform[route.Length];
                for (int j = 0; j < route.Length; j++)
                {
                    int index = (assignment.startIndex + (assignment.reversed ? route.Length - j : j)) % route.Length;
                    guard.patrol[j] = route[index];
                }
                NavMeshAgent agent = guard.GetComponent<NavMeshAgent>();
                agent.height = character.bodyHeight;
                agent.Warp(assignment.position); guard.transform.rotation = assignment.rotation;
            }
            CurrentPlan = plan; Validated = true; UsingBeats = beats != null;
            ValidationMessage = "Valid / " + plan.assignments.Length + (UsingBeats ? " patrol beats" : " patrol zones");
        }
        public string RosterLabel()
        {
            if (CurrentPlan == null) return "";
            var names = new string[CurrentPlan.assignments.Length];
            for (int i = 0; i < names.Length; i++) names[i] = roster[CurrentPlan.assignments[i].characterIndex].displayName;
            return string.Join(" / ", names);
        }
    }
}
