using UnityEngine;

namespace SneakHouse
{
    public class VisionCone : MonoBehaviour
    {
        public GuardBrain guard;
        private Mesh mesh;
        private Material material;
        private float nextUpdate;
        private void Start()
        {
            GameObject display = new GameObject("Vision footprint"); display.transform.SetParent(transform, false);
            display.layer = 2;
            MeshFilter filter = display.AddComponent<MeshFilter>();
            mesh = new Mesh { name = "Guard vision clipped by walls" }; filter.sharedMesh = mesh;
            material = new Material(Shader.Find("Sprites/Default"));
            display.AddComponent<MeshRenderer>().sharedMaterial = material;
            SetAlert(0, false);
        }
        public void SetAlert(float suspicion, bool chase)
        {
            if (material != null) material.color = chase ? new Color(.8f, .22f, .10f, .22f) : Color.Lerp(new Color(.7f, .65f, .35f, .16f), new Color(.9f, .6f, .12f, .26f), suspicion);
        }
        private void LateUpdate()
        {
            if (mesh == null || Time.time < nextUpdate) return; nextUpdate = Time.time + .08f;
            const int segments = 28;
            Vector3[] vertices = new Vector3[segments + 2]; int[] triangles = new int[segments * 3];
            Vector3 origin = transform.position + Vector3.up * guard.eyeHeight;
            float floor = transform.position.y + .045f;
            vertices[0] = transform.InverseTransformPoint(new Vector3(origin.x, floor, origin.z));
            for (int i = 0; i <= segments; i++)
            {
                float angle = -guard.fieldOfView / 2 + guard.fieldOfView * i / segments;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
                float distance = Physics.Raycast(origin, direction, out RaycastHit hit, guard.visionDistance, 1 << 8, QueryTriggerInteraction.Ignore) ? hit.distance : guard.visionDistance;
                Vector3 end = origin + direction * distance; end.y = floor;
                vertices[i + 1] = transform.InverseTransformPoint(end);
                if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
    public class NoisePulse : MonoBehaviour
    {
        private LineRenderer line;
        private float radius, since;
        private void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>(); line.positionCount = 48; line.loop = true;
            line.widthMultiplier = .035f; line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
        }
        public static void Show(Vector3 point, float radius)
        {
            NoisePulse pulse = new GameObject("Footstep noise cue").AddComponent<NoisePulse>();
            pulse.transform.position = point; pulse.radius = Mathf.Min(radius, 2.3f); pulse.since = Time.time;
        }
        private void Update()
        {
            float t = (Time.time - since) / .55f;
            if (t >= 1) { Destroy(gameObject); return; }
            line.startColor = line.endColor = new Color(.8f, .6f, .30f, .36f * (1 - t));
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * t; p.y = .055f; line.SetPosition(i, p);
            }
        }
        private void OnDestroy() { if (line != null && line.material != null) Destroy(line.material); }
    }
}
