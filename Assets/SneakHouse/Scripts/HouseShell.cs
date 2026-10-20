using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SneakHouse
{
    [DefaultExecutionOrder(-450), RequireComponent(typeof(InteriorLayout))]
    public class HouseShell : MonoBehaviour
    {
        public HouseShellVariant[] variants;
        public int CurrentSeed { get; private set; }
        public int VariantIndex { get; private set; }
        public HouseShellVariant Current { get; private set; }
        public float UsableArea { get; private set; }
        public bool HasUpperFloor => Current != null && Current.upperHeight > 0;
        public string Label => Current == null ? "Collector's House" : Current.label;
        public Rect[] Footprint => Current == null ? new[] { new Rect(-8,-6,16,12) } : Current.footprint;
        private struct Original { public Transform node; public Vector3 position, scale; public Quaternion rotation; public bool active; }
        private readonly List<Original> originals = new List<Original>();
        private Vector3[][] routes;
        private string[] zoneNames;
        private Transform geometry;
        private Quaternion cameraRotation;
        private float cameraSize;
        private void Capture(Transform node)
        {
            originals.Add(new Original { node = node, position = node.position, rotation = node.rotation,
                scale = node.localScale, active = node.gameObject.activeSelf });
        }
        private void Awake()
        {
            GetComponent<GameSession>().ApplyLevelConfiguration();
            geometry = GetComponent<RuntimeNavigation>().geometry;
            foreach (Transform child in geometry)
            {
                if (child.name.StartsWith("Back /") || child.name == "Courtyard ground" || child.name == "Garden north" ||
                    child.name == "West border" || child.name == "East border") Capture(child);
                if (child.name == "EXTERIOR / Living garden")
                    foreach (Transform item in child) Capture(item);
            }
            GuardEncounter encounter = GetComponent<GuardEncounter>();
            routes = new Vector3[encounter.zones.Length][]; zoneNames = new string[routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                routes[i] = new Vector3[encounter.zones[i].route.Length]; zoneNames[i] = encounter.zones[i].guard.patrolZone;
                for (int j = 0; j < routes[i].Length; j++) routes[i][j] = encounter.zones[i].route[j].position;
            }
            Camera view = Camera.main; cameraRotation = view.transform.rotation; cameraSize = view.orthographicSize;
            CurrentSeed = GameSession.SelectedLevel.LayoutSeed;
            Generate(CurrentSeed);
        }
        public void Generate(int seed)
        {
            if (Current != null) { Current.gameObject.SetActive(false); Destroy(Current.gameObject); }
            foreach (Original original in originals)
            {
                original.node.SetPositionAndRotation(original.position,original.rotation);
                original.node.localScale = original.scale; original.node.gameObject.SetActive(original.active);
            }
            CurrentSeed = seed; VariantIndex = GameSession.SelectedLevel.ShellIndex;
            Current = Instantiate(variants[VariantIndex],geometry,false); Current.name = "HOUSE / " + Current.label;
            bool expanded = Current.officeWing;
            if (expanded)
            {
                foreach (Original original in originals)
                {
                    Transform node = original.node;
                    if (node.name.StartsWith("Back /")) node.gameObject.SetActive(false);
                    else if (node.name == "Courtyard ground" || node.name == "Neighborhood verge" || node.name == "West border" || node.name == "East border")
                    { node.position += Vector3.forward*6; node.localScale += Vector3.forward*12; }
                    else if (node.name == "Garden north" || (original.position.z > 6 && !(Mathf.Abs(original.position.x) > 11 && original.position.z < 7))) node.position += Vector3.forward*12;
                }
            }
            GuardEncounter encounter = GetComponent<GuardEncounter>();
            for (int i = 0; i < routes.Length; i++)
            {
                for (int j = 0; j < routes[i].Length; j++) encounter.zones[i].route[j].position = PlaceRoomPoint(i,routes[i][j]);
                encounter.zones[i].guard.patrolZone = i == 1 && HasUpperFloor ? "Upper office" : i == 1 && expanded ? "Library wing" :
                    i == 2 && Current.collectionWing ? "Gallery wing" : zoneNames[i];
            }
            RuntimeNavigation navigation = GetComponent<RuntimeNavigation>();
            navigation.bounds = expanded ? new Bounds(new Vector3(0,HasUpperFloor ? 3 : 1,6),new Vector3(24,HasUpperFloor ? 14 : 7,30)) : new Bounds(new Vector3(0,1,0),new Vector3(24,7,18));
            Camera camera = Camera.main;
            camera.transform.rotation = expanded ? Quaternion.LookRotation(new Vector3(0,0,6)-camera.transform.position) : cameraRotation;
            camera.orthographicSize = HasUpperFloor ? 15.2f : expanded ? 14.2f : cameraSize;
            Physics.SyncTransforms();
        }
        public Vector3 PlaceRoomPoint(int room, Vector3 point)
        {
            bool moved = Current != null && (room == 1 && Current.officeWing || room == 2 && Current.collectionWing);
            if (!moved) return point;
            Vector3 centre = new Vector3(room == 1 ? -4 : 4,0,3.5f);
            Vector3 destination = new Vector3(room == 1 ? (VariantIndex == 1 ? -4 : -5) : 5,0,12);
            if (room == 1 && HasUpperFloor) destination.y = Current.upperHeight;
            return destination + Quaternion.Euler(0,90,0)*(point-centre);
        }
        public void PlaceRoom(int index, Transform room)
        {
            Vector3 origin = PlaceRoomPoint(index,Vector3.zero);
            room.SetPositionAndRotation(origin,
                index == 1 && Current.officeWing || index == 2 && Current.collectionWing ? Quaternion.Euler(0,90,0) : Quaternion.identity);
        }
        public bool Contains(Vector3 point)
        {
            foreach (Rect rect in Footprint) if (rect.Contains(new Vector2(point.x,point.z))) return true;
            return false;
        }
        // Clip each walkable triangle against non-overlapping floor rectangles. Courtyard is excluded.
        public void MeasureNavigation()
        {
            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation(); float area = 0;
            for (int i = 0; i < mesh.indices.Length; i += 3)
            {
                Vector3 a = mesh.vertices[mesh.indices[i]], b = mesh.vertices[mesh.indices[i+1]], c = mesh.vertices[mesh.indices[i+2]];
                float low = Mathf.Min(a.y,Mathf.Min(b.y,c.y)), high = Mathf.Max(a.y,Mathf.Max(b.y,c.y));
                Rect[] regions = low >= -.2f && high <= .3f ? Footprint :
                    HasUpperFloor && low >= Current.upperHeight-.2f && high <= Current.upperHeight+.3f ? Current.upperFootprint : new Rect[0];
                foreach (Rect rect in regions)
                {
                    var polygon = new List<Vector2> { new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z) };
                    polygon = Clip(polygon,0,rect.xMin,true); polygon = Clip(polygon,0,rect.xMax,false);
                    polygon = Clip(polygon,1,rect.yMin,true); polygon = Clip(polygon,1,rect.yMax,false);
                    float signed = 0;
                    for (int j = 0; j < polygon.Count; j++) { Vector2 p = polygon[j], q = polygon[(j+1)%polygon.Count]; signed += p.x*q.y-q.x*p.y; }
                    area += Mathf.Abs(signed)*.5f;
                }
            }
            UsableArea = area; GameSession game = GetComponent<GameSession>();
            game.activeGuardCount = game.Level.Guards;
            game.targetTime = game.Level.ReferenceSeconds;
            for (int i = 0; i < game.guards.Length; i++) game.guards[i].gameObject.SetActive(i < game.activeGuardCount);
        }
        private static List<Vector2> Clip(List<Vector2> input, int axis, float edge, bool above)
        {
            var output = new List<Vector2>(); if (input.Count == 0) return output;
            Vector2 previous = input[input.Count-1]; bool previousInside = above ? previous[axis] >= edge : previous[axis] <= edge;
            foreach (Vector2 point in input)
            {
                bool inside = above ? point[axis] >= edge : point[axis] <= edge;
                if (inside != previousInside) output.Add(Vector2.Lerp(previous,point,(edge-previous[axis])/(point[axis]-previous[axis])));
                if (inside) output.Add(point); previous = point; previousInside = inside;
            }
            return output;
        }
    }
}
