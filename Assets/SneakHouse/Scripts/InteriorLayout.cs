using System;
using UnityEngine;
using UnityEngine.AI;

namespace SneakHouse
{
    [DefaultExecutionOrder(-400), RequireComponent(typeof(GameSession))]
    public class InteriorLayout : MonoBehaviour
    {
        [Serializable] public class Room
        {
            public string label;
            public LootTarget target;
            public RoomLayoutVariant[] variants;
            public Transform fallbackApproach;
        }
        public Transform geometry;
        public GameObject authoredFurniture;
        public HidingSpot deskHiding;
        public Room[] rooms = new Room[0];
        public int CurrentSeed { get; private set; }
        public RoomLayoutVariant[] CurrentRooms { get; private set; } = new RoomLayoutVariant[0];
        public bool Validated { get; private set; }
        public bool UsingFallback { get; private set; }
        public string ValidationMessage { get; private set; } = "Pending navigation";
        private Vector3[] originalTargets;
        private Quaternion[] originalRotations;
        private Vector3 originalHideRoot, originalHideAnchor, originalHideApproach;
        private Quaternion originalHideRotation;
        private void Awake()
        {
            originalTargets = new Vector3[rooms.Length]; originalRotations = new Quaternion[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            { originalTargets[i] = rooms[i].target.transform.position; originalRotations[i] = rooms[i].target.transform.rotation; }
            originalHideRoot = deskHiding.transform.position; originalHideRotation = deskHiding.transform.rotation;
            originalHideAnchor = deskHiding.anchor.position; originalHideApproach = deskHiding.Approach;
            CurrentSeed = GameSession.SelectedLevel.LayoutSeed;
            if (!Generate(CurrentSeed)) UseFallback("Invalid room catalogue");
        }
        public int[] ChoicesForSeed(int seed)
        {
            var random = new System.Random(seed); int[] choices = new int[rooms.Length];
            for (int i = 0; i < rooms.Length; i++) choices[i] = rooms[i].variants.Length == 0 ? -1 : random.Next(rooms[i].variants.Length);
            return choices;
        }
        public bool Generate(int seed)
        {
            int[] choices = ChoicesForSeed(seed);
            for (int i = 0; i < rooms.Length; i++)
            {
                if (choices[i] < 0) return false;
                RoomLayoutVariant prefab = rooms[i].variants[choices[i]];
                if (prefab == null || prefab.targetSocket == null || prefab.targetApproach == null ||
                    (i == 1 && (prefab.hidingAnchor == null || prefab.hidingApproach == null))) return false;
            }
            ClearRooms(); authoredFurniture.SetActive(false);
            CurrentRooms = new RoomLayoutVariant[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                RoomLayoutVariant room = Instantiate(rooms[i].variants[choices[i]],geometry,false);
                room.name = "INTERIOR / " + rooms[i].label + " / " + room.label;
                GetComponent<HouseShell>()?.PlaceRoom(i,room.transform);
                CurrentRooms[i] = room;
                rooms[i].target.transform.SetPositionAndRotation(room.targetSocket.position,room.targetSocket.rotation);
                if (room.hidingAnchor != null)
                {
                    deskHiding.transform.SetPositionAndRotation(room.hidingAnchor.position,room.hidingAnchor.rotation);
                    deskHiding.anchor.position = room.hidingAnchor.position;
                    deskHiding.inspectionPoint.position = room.hidingApproach.position;
                }
            }
            CurrentSeed = seed; UsingFallback = false; Validated = false;
            ValidationMessage = "Pending navigation"; Physics.SyncTransforms(); return true;
        }
        private void ClearRooms()
        {
            foreach (RoomLayoutVariant room in CurrentRooms)
                if (room != null) { room.gameObject.SetActive(false); Destroy(room.gameObject); }
            CurrentRooms = new RoomLayoutVariant[0];
        }
        public void UseFallback(string reason)
        {
            ClearRooms(); authoredFurniture.SetActive(true);
            for (int i = 0; i < rooms.Length; i++) rooms[i].target.transform.SetPositionAndRotation(originalTargets[i],originalRotations[i]);
            deskHiding.transform.SetPositionAndRotation(originalHideRoot,originalHideRotation);
            deskHiding.anchor.position = originalHideAnchor; deskHiding.inspectionPoint.position = originalHideApproach;
            UsingFallback = true; Validated = false; ValidationMessage = reason; Physics.SyncTransforms();
        }
        public Vector3 Approach(int room) => UsingFallback ? rooms[room].fallbackApproach.position : CurrentRooms[room].targetApproach.position;
        public bool ValidatePaths()
        {
            GameSession game = GetComponent<GameSession>();
            if (!NavMesh.SamplePosition(game.player.transform.position,out NavMeshHit start,.5f,NavMesh.AllAreas)) return Fail("Player spawn is off NavMesh");
            for (int i = 0; i < rooms.Length; i++)
            {
                Vector3 approach = Approach(i), target = rooms[i].target.transform.position;
                if (!Reachable(start.position,approach) || !ClearPlayer(approach)) return Fail("Blocked target approach: " + rooms[i].label);
                if (Vector3.ProjectOnPlane(target-approach,Vector3.up).magnitude >= 1.35f ||
                    Physics.Linecast(approach+Vector3.up*1.1f,target+Vector3.up*.12f,1<<8,QueryTriggerInteraction.Ignore)) return Fail("Target cannot be collected: " + rooms[i].label);
            }
            GuardEncounter encounter = GetComponent<GuardEncounter>();
            for (int i = 0; i < game.ActiveGuards; i++)
                foreach (Transform point in encounter.zones[i].route)
                    if (!Reachable(start.position,point.position)) return Fail("Blocked patrol: " + encounter.zones[i].guard.patrolZone);
            foreach (HidingSpot spot in game.player.GetComponent<PlayerHiding>().spots)
                if (!Reachable(start.position,spot.Approach) || !ClearPlayer(spot.Approach)) return Fail("Blocked hiding approach: " + spot.displayName);
            ExtractionZone exit = FindFirstObjectByType<ExtractionZone>();
            if (exit == null || !Reachable(start.position,exit.transform.position)) return Fail("Extraction is unreachable");
            Validated = true; ValidationMessage = UsingFallback ? "Authored fallback validated" : "Targets, patrol and hiding reachable"; return true;
        }
        private bool Fail(string message) { Validated = false; ValidationMessage = message; return false; }
        private static bool Reachable(Vector3 start, Vector3 point)
        {
            if (!NavMesh.SamplePosition(point,out NavMeshHit hit,.3f,NavMesh.AllAreas) || Mathf.Abs(hit.position.y-point.y) > .25f) return false;
            NavMeshPath path = new NavMeshPath();
            return NavMesh.CalculatePath(start,hit.position,NavMesh.AllAreas,path) && path.status == NavMeshPathStatus.PathComplete;
        }
        private static bool ClearPlayer(Vector3 point)
        {
            return !Physics.CheckCapsule(point+Vector3.up*.38f,point+Vector3.up*1.31f,.29f,1<<8,QueryTriggerInteraction.Ignore);
        }
    }
}
