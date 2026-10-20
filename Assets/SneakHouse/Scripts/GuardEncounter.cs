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
        private static string previousSignature;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHistory() { previousSignature = null; }
        private void Awake()
        {
            int seed = useFixedSeed ? fixedSeed : Guid.NewGuid().GetHashCode() & int.MaxValue;
            for (int attempt = 0; attempt < 8; attempt++, seed = unchecked(seed + 1))
            {
                if (!TryBuildPlan(seed, out Plan plan)) break;
                if (!useFixedSeed && plan.signature == previousSignature) continue;
                Apply(plan); previousSignature = plan.signature;
                return;
            }
            Debug.LogWarning("Encounter validation failed; keeping authored patrols.", this);
        }
        public bool TryBuildPlan(int seed, out Plan plan)
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
            var assignments = new Assignment[game.ActiveGuards];
            string signature = "";
            for (int i = 0; i < assignments.Length; i++)
            {
                Zone zone = zones[i];
                if (zone.guard == null || zone.route == null || zone.route.Length < 2) return false;
                foreach (Transform point in zone.route) if (point == null) return false;
                if (!RouteConnected(zone.route)) return false;
                int start = random.Next(zone.route.Length);
                bool reversed = random.Next(2) == 1;
                Character character = roster[valid[i]];
                Assignment assignment = null;
                for (int candidate = 0; candidate < zone.route.Length; candidate++)
                {
                    int index = (start + candidate) % zone.route.Length;
                    int next = (index + (reversed ? zone.route.Length - 1 : 1)) % zone.route.Length;
                    if (!NavMesh.SamplePosition(zone.route[index].position, out NavMeshHit hit, .35f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(hit.position.y - zone.route[index].position.y) > .35f) continue;
                    Vector3 forward = zone.route[next].position - hit.position; forward.y = 0;
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
        private static bool RouteConnected(Transform[] points)
        {
            NavMeshPath path = new NavMeshPath();
            for (int i = 0; i < points.Length; i++)
                if (!NavMesh.SamplePosition(points[i].position, out NavMeshHit a, .35f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(points[(i + 1) % points.Length].position, out NavMeshHit b, .35f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            return true;
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
        private void Apply(Plan plan)
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
                guard.patrol = new Transform[zone.route.Length];
                for (int j = 0; j < zone.route.Length; j++)
                {
                    int index = (assignment.startIndex + (assignment.reversed ? zone.route.Length - j : j)) % zone.route.Length;
                    guard.patrol[j] = zone.route[index];
                }
                NavMeshAgent agent = guard.GetComponent<NavMeshAgent>();
                agent.height = character.bodyHeight;
                agent.Warp(assignment.position); guard.transform.rotation = assignment.rotation;
            }
            CurrentPlan = plan; Validated = true; ValidationMessage = "Valid / " + plan.assignments.Length + " patrol zones";
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
