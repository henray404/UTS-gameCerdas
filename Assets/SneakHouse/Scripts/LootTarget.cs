using UnityEngine;
using UnityEngine.Rendering;

namespace SneakHouse
{
    public class LootTarget : MonoBehaviour
    {
        public string displayName;
        public string Label => displayName == "Drive rahasia" ? "Data Drive" :
            displayName == "Jam tangan" ? "Watch" : displayName == "Konsol game" ? "Game Console" : displayName;
        public Transform visual;
        public bool Collected { get; private set; }
        private Vector3 rest;
        private Transform marker;
        private Light glow;
        private LineRenderer ring;
        private float phase;
        private static Material outlineMaterial, markerMaterial;
        private void Awake()
        {
            if (visual != null) rest = visual.localPosition;
            phase = Mathf.Abs(GetHashCode() % 100) * .37f;
            BuildHighlight();
        }
        private void BuildHighlight()
        {
            BuildOutline();
            marker = new GameObject("Objective marker").transform;
            marker.gameObject.layer = 2;
            marker.SetParent(transform, false);
            marker.localPosition = Vector3.up * .62f;
            GameObject diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diamond.name = "Objective diamond";
            diamond.layer = 2;
            Destroy(diamond.GetComponent<Collider>());
            diamond.transform.SetParent(marker, false);
            diamond.transform.localScale = Vector3.one * .17f;
            diamond.transform.localRotation = Quaternion.Euler(45, 45, 0);
            MeshRenderer renderer = diamond.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MarkerMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            GameObject lightObject = new GameObject("Objective glow");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * .40f;
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, .82f, .43f);
            glow.range = 1.7f;
            glow.intensity = 1.1f;
            glow.shadows = LightShadows.None;

            foreach (LineRenderer line in GetComponentsInChildren<LineRenderer>(true))
                if (line.gameObject.name == "Interaction marker") { ring = line; break; }
        }
        private void BuildOutline()
        {
            if (visual == null) return;
            if (outlineMaterial == null) outlineMaterial = OutlineMaterial();
            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                GameObject shell = new GameObject("Target outline");
                shell.layer = 2;
                shell.transform.SetParent(filter.transform, false);
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localRotation = Quaternion.identity;
                shell.transform.localScale = Vector3.one * 1.12f;
                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer renderer = shell.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = outlineMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
        private static Material MarkerMaterial()
        {
            if (markerMaterial != null) return markerMaterial;
            markerMaterial = new Material(Shader.Find("Standard"));
            markerMaterial.color = new Color(.82f, .64f, .28f);
            markerMaterial.SetFloat("_Glossiness", .05f);
            markerMaterial.EnableKeyword("_EMISSION");
            markerMaterial.SetColor("_EmissionColor", new Color(.55f, .38f, .12f));
            return markerMaterial;
        }
        private static Material OutlineMaterial()
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(.82f, .64f, .28f);
            material.SetFloat("_Glossiness", 0);
            material.SetFloat("_Cull", 1);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(.55f, .38f, .12f));
            return material;
        }
        private void Update()
        {
            if (Collected) return;
            GameSession game = GameSession.Instance;
            bool show = game == null || game.IsRequiredTarget(this);
            float time = Time.time + phase;
            float pulse = .5f + .5f * Mathf.Sin(time * 2.6f);
            if (marker != null)
            {
                marker.gameObject.SetActive(show);
                if (show)
                {
                    marker.localPosition = new Vector3(0, .62f + Mathf.Sin(time * 2f) * .04f, 0);
                    marker.Rotate(0, 45f * Time.deltaTime, 0, Space.Self);
                }
            }
            if (glow != null)
            {
                glow.enabled = show;
                if (show) glow.intensity = .95f + .45f * pulse;
            }
            if (ring != null)
            {
                ring.enabled = show;
                if (show)
                {
                    ring.transform.localScale = Vector3.one * (1f + .22f * pulse);
                    ring.startColor = ring.endColor = new Color(.82f, .64f, .28f, .35f + .35f * pulse);
                }
            }
            if (visual != null && show) visual.localPosition = rest + Vector3.up * Mathf.Sin(time * 2.4f) * .025f;
        }
        public void MarkCollected()
        {
            Collected = true;
            if (visual != null) visual.gameObject.SetActive(false);
            if (marker != null) marker.gameObject.SetActive(false);
            if (glow != null) glow.enabled = false;
            if (ring != null) ring.enabled = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
    }
}
