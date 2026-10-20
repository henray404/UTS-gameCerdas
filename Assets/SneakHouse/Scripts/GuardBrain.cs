using UnityEngine;
using UnityEngine.AI;

namespace SneakHouse
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardBrain : MonoBehaviour
    {
        public enum Mode { Patrol, Investigate, Chase, Search, Return }
        public Transform[] patrol;
        public ActorAnimation acting;
        public string patrolZone = "House";
        public string characterName = "Bogo";
        [Min(.5f)] public float eyeHeight = 1.2f;
        [Min(0)] public float patrolPause = .65f;
        [Min(1)] public float visionDistance = 9;
        [Range(30, 180)] public float fieldOfView = 120;
        [Range(0, 1)] public float hearingSuspicion = .35f;
        [Min(.05f)] public float suspicionRate = .9f;
        [Min(0)] public float suspicionDecay = .65f;
        [Min(1)] public float chaseSpeed = 3.05f;
        [Min(.1f)] public float loseSightSeconds = .75f;
        public float searchDuration = 7;
        [Header("Level tuning")]
        public float idleMin, idleMax, idleChance;
        public float hearingScale = 1, hearingError, alertRadius = 999;
        // Waypoints visited only every peekEvery laps of patrol; they sit closer to the objectives than the home loop.
        public Transform[] peek;
        [Min(0)] public int peekEvery;
        public bool Idling { get; private set; }
        // A resting guard sees a shorter, narrower cone and builds suspicion slowly.
        public float EffectiveVision => Idling ? visionDistance * .75f : visionDistance;
        public float EffectiveFieldOfView => Idling ? fieldOfView * .6f : fieldOfView;
        public Mode State { get; private set; } = Mode.Patrol;
        public bool CanSeePlayer { get; private set; }
        public float Suspicion { get; private set; }
        public bool HasMemory { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public float LastSeenTime { get; private set; }
        public float LastEvidenceTime { get; private set; }
        public string MemorySource { get; private set; } = "-";
        public NavMeshAgent Agent { get; private set; }
        public Vector3 Destination => destination;
        private int waypoint, searchPoint, lap, peekIndex = -1;
        private float stateSince, dwellUntil, lastVisible = -100;
        private float travelBudget = 10, searchSince = -1;
        private Vector3 destination;
        private HidingSpot witnessedSpot;
        private HidingSpot checkingSpot;
        private float checkSince;
        private VisionCone cone;
        private LineRenderer pathLine;
        private bool assistingSearch;
        private NavMeshPath candidatePath;
        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>(); cone = GetComponent<VisionCone>();
            candidatePath = new NavMeshPath();
            pathLine = gameObject.AddComponent<LineRenderer>();
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            pathLine.startColor = pathLine.endColor = new Color(.1f, .62f, .43f, .8f);
            pathLine.widthMultiplier = .045f; pathLine.useWorldSpace = true; pathLine.positionCount = 0;
        }
        private void OnEnable()
        {
            GameSession.Noise += Hear; GameSession.SharedAlert += ReceiveAlert;
            if (Agent != null && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1, NavMesh.AllAreas))
            {
                if (!Agent.enabled) { transform.position = hit.position; Agent.enabled = true; }
            }
        }
        private void OnDisable() { GameSession.Noise -= Hear; GameSession.SharedAlert -= ReceiveAlert; }
        private void Start() { SetMode(Mode.Patrol, Time.time); }
        private void Update()
        {
            GameSession game = GameSession.Instance;
            if (game == null || game.Finished || game.Paused || !Agent.isOnNavMesh) return;
            Step(Time.deltaTime, Time.time);
            DrawPath(game.DebugMode);
        }
        public bool Sees(Vector3 position)
        {
            Vector3 direction = position + Vector3.up * 1.05f - (transform.position + Vector3.up * eyeHeight);
            Vector3 flat = new Vector3(direction.x, 0, direction.z);
            if (flat.magnitude > EffectiveVision || Vector3.Angle(transform.forward, flat) > EffectiveFieldOfView * .5f) return false;
            return !Physics.Raycast(transform.position + Vector3.up * eyeHeight, direction.normalized, direction.magnitude, 1 << 8, QueryTriggerInteraction.Ignore);
        }
        public void Step(float delta, float now)
        {
            PlayerMotor player = GameSession.Instance.player;
            CanSeePlayer = !player.IsHidden && Sees(player.transform.position);
            if (CanSeePlayer)
            {
                Remember(player.transform.position, now, "Vision");
                lastVisible = now;
                float flatDistance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(player.transform.position.x, 0, player.transform.position.z));
                Suspicion = Mathf.Min(1, Suspicion + delta * CampaignLevel.SuspicionGain(
                    suspicionRate * (Idling ? .6f : 1f), player.Sneaking, flatDistance, visionDistance));
                if (Suspicion >= 1 && State != Mode.Chase)
                {
                    SetMode(Mode.Chase, now);
                    GameSession.Instance.Broadcast(this, LastKnownPosition, now);
                }
            }
            else Suspicion = Mathf.Max(0, Suspicion - delta * suspicionDecay);
            if (!player.IsHidden && Vector3.Distance(transform.position, player.transform.position) < .72f &&
                !Physics.Linecast(transform.position + Vector3.up, player.transform.position + Vector3.up, 1 << 8, QueryTriggerInteraction.Ignore))
            { GameSession.Instance.End(false); return; }
            switch (State)
            {
                case Mode.Patrol:
                    if (CanSeePlayer && Suspicion > .08f)
                    {
                        Idling = false; Agent.isStopped = true; Face(player.transform.position, delta);
                    }
                    else
                    {
                        Agent.isStopped = false;
                        if (HasMemory && Suspicion > .15f && !CanSeePlayer) { SetMode(Mode.Investigate, now); break; }
                        if (Reached())
                        {
                            if (dwellUntil == 0) BeginDwell(now);
                            if (now >= dwellUntil) { Idling = false; dwellUntil = 0; AdvanceWaypoint(); }
                        }
                    }
                    break;
                case Mode.Investigate:
                    if (Reached())
                    {
                        if(CheckHiding())return;
                        SetMode(Mode.Search, now);
                    }
                    else if (now - stateSince > travelBudget) SetMode(Mode.Return, now);
                    break;
                case Mode.Chase:
                    if (CanSeePlayer)
                    {
                        // Prediction uses observed velocity only while the target is visible.
                        Go(LastKnownPosition + player.Velocity * .35f);
                    }
                    else if (now - lastVisible > loseSightSeconds) SetMode(Mode.Search, now);
                    break;
                case Mode.Search:
                    if (searchSince >= 0 ? now-searchSince > searchDuration : now-stateSince > travelBudget)
                    { SetMode(Mode.Return, now); break; }
                    if (Reached())
                    {
                        if (searchSince < 0) searchSince = now;
                        if(CheckHiding())return;
                        Agent.isStopped = true;
                        transform.Rotate(0, 50 * delta, 0);
                        if (dwellUntil == 0) dwellUntil = now + .9f;
                        if (now >= dwellUntil)
                        {
                            searchPoint++; Agent.isStopped = false; dwellUntil = 0;
                            Go(LastKnownPosition + SearchOffset(searchPoint + SearchSector()));
                        }
                    }
                    break;
                case Mode.Return:
                    if (Reached()) { HasMemory = false; MemorySource = "-"; SetMode(Mode.Patrol, now); }
                    break;
            }
            float speed = Agent.velocity.magnitude;
            acting?.SetLocomotion(speed, State == Mode.Chase, State == Mode.Search || Idling ? "LookAround" : Suspicion > .1f ? "FaceActing" : "Idle");
            if (cone != null) cone.SetAlert(Suspicion, State == Mode.Chase);
        }
        public void Hear(Vector3 point, float radius, string source)
        {
            if (GameSession.Instance == null || GameSession.Instance.Finished || CanSeePlayer) return;
            // Thrown cans are loud and exact; footsteps are scaled by level and their source is only roughly located.
            bool decoy = source == "Dropped can";
            float attenuation = Physics.Linecast(transform.position + Vector3.up, point + Vector3.up, 1 << 8, QueryTriggerInteraction.Ignore) ? .65f : 1;
            if (Vector3.Distance(transform.position, point) > radius * (decoy ? 1f : hearingScale) * attenuation) return;
            if (!decoy && hearingError > 0)
            {
                Vector2 jitter = Random.insideUnitCircle * hearingError;
                point += new Vector3(jitter.x, 0, jitter.y);
            }
            Remember(point, Time.time, source); Suspicion = Mathf.Max(hearingSuspicion, Suspicion); SetMode(Mode.Investigate, Time.time);
        }
        public void NoticeHidingEntry(HidingSpot spot)
        {
            witnessedSpot=spot;Remember(spot.Approach,Time.time,"Saw hiding entry");Suspicion=Mathf.Max(.55f,Suspicion);SetMode(Mode.Investigate,Time.time);
        }
        private bool CheckHiding()
        {
            if(witnessedSpot!=null)
            {
                if(TryInspect(witnessedSpot))return true;
                if(witnessedSpot.Occupant==null)witnessedSpot=null;
            }
            // Investigate evidence near a hiding spot; casual patrols do not reveal occupants.
            foreach(HidingSpot spot in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
                if(HasMemory && Vector3.Distance(spot.Approach,LastKnownPosition)<1.1f && TryInspect(spot))return true;
            checkingSpot=null;
            return false;
        }
        private bool TryInspect(HidingSpot spot)
        {
            if(spot.Occupant==null || Vector3.Distance(transform.position,spot.Approach)>1.15f)return false;
            if(checkingSpot!=spot){checkingSpot=spot;checkSince=Time.time;GameSession.Instance.Notify("Guard is checking the hiding spot.");}
            Agent.isStopped=true;acting?.Set("LookAround");Face(spot.anchor.position,Time.deltaTime);
            if(Time.time-checkSince>=.8f)spot.Inspect(this);
            return true;
        }
        private void ReceiveAlert(GuardBrain sender, Vector3 point, float timestamp)
        {
            if (sender == this || State == Mode.Chase || CanSeePlayer || Time.time - timestamp > searchDuration ||
                (HasMemory && LastEvidenceTime > timestamp)) return;
            if (Vector3.Distance(sender.transform.position, transform.position) > alertRadius) return;
            Remember(point, timestamp, "Shared alert");
            Suspicion = Mathf.Max(.3f, Suspicion);
            assistingSearch = true;
            SetMode(Mode.Search, Time.time);
        }
        private int SearchSector()
        {
            GuardBrain[] guards = GameSession.Instance.guards;
            int index = System.Array.IndexOf(guards, this);
            return Mathf.Max(0, index);
        }
        private void Remember(Vector3 point, float now, string source)
        {
            assistingSearch = false;
            HasMemory = true; LastKnownPosition = point; LastEvidenceTime = now; MemorySource = source;
            if (source == "Vision") LastSeenTime = now;
        }
        private void SetMode(Mode mode, float now)
        {
            State = mode; stateSince = now; dwellUntil = 0; searchSince = -1; Agent.isStopped = false;
            Idling = false; peekIndex = -1;
            Agent.speed = mode == Mode.Chase ? chaseSpeed : mode == Mode.Patrol ? 1.15f : 1.65f;
            if (mode == Mode.Patrol || mode == Mode.Return)
            {
                if (mode == Mode.Return)
                {
                    float best = float.PositiveInfinity;
                    for (int i = 0; i < patrol.Length; i++)
                    {
                        float distance = (patrol[i].position - transform.position).sqrMagnitude;
                        if (distance < best) { best = distance; waypoint = i; }
                    }
                }
                Go(patrol[waypoint].position);
            }
            else
            {
                searchPoint = 0;
                Vector3 point = LastKnownPosition;
                if (mode == Mode.Search && assistingSearch) point += SearchOffset(SearchSector());
                if (!Go(point)) Go(LastKnownPosition);
                // Crossing wings/floors can exceed a fixed timeout before the guard reaches evidence.
                float distance = 0; Vector3[] corners = candidatePath.corners;
                for (int i = 1; i < corners.Length; i++) distance += Vector3.Distance(corners[i-1],corners[i]);
                travelBudget = Mathf.Clamp(distance/Mathf.Max(.1f,Agent.speed)+5,10,60);
            }
        }
        private void BeginDwell(float now)
        {
            Idling = idleMax > 0 && Random.value < idleChance;
            dwellUntil = now + (Idling ? Random.Range(idleMin, idleMax) : patrolPause);
        }
        // Home loop; every peekEvery laps the guard also walks the peek waypoints once.
        private void AdvanceWaypoint()
        {
            if (peekIndex >= 0)
            {
                peekIndex++;
                Go(peekIndex < peek.Length ? peek[peekIndex].position : patrol[waypoint].position);
                if (peekIndex >= peek.Length) peekIndex = -1;
                return;
            }
            waypoint = (waypoint + 1) % patrol.Length;
            if (waypoint == 0 && peekEvery > 0 && peek != null && peek.Length > 0 && ++lap % peekEvery == 0)
            { peekIndex = 0; Go(peek[0].position); return; }
            Go(patrol[waypoint].position);
        }
        private static Vector3 SearchOffset(int index)
        {
            Vector3[] offsets = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            return offsets[index % 4] * 1.7f;
        }
        private bool Go(Vector3 point)
        {
            if (!Agent.isOnNavMesh) return false;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2.2f, Agent.areaMask) ||
                !Agent.CalculatePath(hit.position, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete) return false;
            destination = hit.position;
            return Agent.SetPath(candidatePath);
        }
        private bool Reached()
        {
            Vector3 flat = transform.position - destination; flat.y = 0;
            return !Agent.pathPending && flat.sqrMagnitude < .40f * .40f;
        }
        private void Face(Vector3 point, float delta)
        {
            Vector3 flat = point - transform.position; flat.y = 0;
            if (flat.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 150 * delta);
        }
        private void DrawPath(bool visible)
        {
            pathLine.enabled = visible;
            if (!visible || !Agent.hasPath) { pathLine.positionCount = 0; return; }
            Vector3[] corners = Agent.path.corners; pathLine.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++) pathLine.SetPosition(i, corners[i] + Vector3.up * .10f);
        }
    }
}
