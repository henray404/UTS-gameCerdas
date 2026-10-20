using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SneakHouse
{
    // Visual-only extensions for the existing full-height wall colliders.
    public class FirstPersonRoomShell : MonoBehaviour
    {
        public Transform geometry;
        private struct Cover { public Transform mesh; public float bottom, height; public Vector3 size, center; }
        private readonly List<Cover> covers = new List<Cover>();
        private readonly List<GameObject> ceilings = new List<GameObject>();
        private bool built;

        private GameObject VisualBox(string label, Transform parent, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label; box.transform.SetParent(parent, false);
            Collider collision = box.GetComponent<Collider>(); collision.enabled = false; Destroy(collision);
            Renderer art = box.GetComponent<Renderer>(); art.sharedMaterial = material;
            art.shadowCastingMode = ShadowCastingMode.Off;
            box.SetActive(false); return box;
        }

        private void Build()
        {
            built = true;
            if (geometry == null) return;
            Bounds room = new Bounds(); bool hasBounds = false; Material plaster = null;
            foreach (Transform child in geometry.GetComponentsInChildren<Transform>())
            {
                const string suffix = " / Full wall collider";
                if (!child.gameObject.activeInHierarchy || !child.name.EndsWith(suffix)) continue;
                BoxCollider volume = child.GetComponent<BoxCollider>(); if (volume == null) continue;
                string artName = child.name.Substring(0, child.name.Length - suffix.Length) + " / Cutaway art";
                Renderer cutaway = null;
                foreach (Transform sibling in child.parent)
                    if (sibling.name == artName) { cutaway = sibling.GetComponent<Renderer>(); break; }
                if (cutaway == null) continue;
                Bounds wall = volume.bounds;
                if (!hasBounds) { room = wall; hasBounds = true; } else room.Encapsulate(wall);
                plaster = cutaway.sharedMaterial;
                float bottom = cutaway.bounds.max.y;
                GameObject cap = VisualBox(child.name + " / First person art", transform, plaster);
                covers.Add(new Cover { mesh = cap.transform, bottom = bottom, height = Mathf.Max(0, wall.max.y - bottom), size = wall.size, center = wall.center });
            }
            if (hasBounds)
            {
                HouseShell shell = FindFirstObjectByType<HouseShell>();
                Rect[] panels = shell == null ? new[] {new Rect(room.min.x,room.min.z,room.size.x,room.size.z)} : shell.Footprint;
                foreach (Rect panel in panels)
                {
                    GameObject ceiling = VisualBox("First person ceiling",transform,plaster);
                    ceiling.transform.position = new Vector3(panel.center.x,shell == null ? room.max.y+.06f : 2.96f,panel.center.y);
                    ceiling.transform.localScale = new Vector3(panel.width,.12f,panel.height);
                    ceilings.Add(ceiling);
                }
                if (shell != null && shell.HasUpperFloor)
                    foreach (Rect panel in shell.Current.upperFootprint)
                    {
                        GameObject ceiling = VisualBox("Upper floor ceiling",transform,plaster);
                        ceiling.transform.position = new Vector3(panel.center.x,shell.Current.upperHeight+2.96f,panel.center.y);
                        ceiling.transform.localScale = new Vector3(panel.width,.12f,panel.height);
                        ceilings.Add(ceiling);
                    }
            }
        }

        public void SetView(float blend, bool enclosed)
        {
            if (!built) Build();
            float extension = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .8f, blend));
            foreach (Cover cover in covers)
            {
                if (cover.mesh == null) continue;
                float height = cover.height * extension;
                cover.mesh.gameObject.SetActive(height > .001f);
                cover.mesh.position = new Vector3(cover.center.x, cover.bottom + height * .5f, cover.center.z);
                cover.mesh.localScale = new Vector3(cover.size.x, height, cover.size.z);
            }
            foreach (GameObject ceiling in ceilings) if (ceiling != null) ceiling.SetActive(enclosed);
        }
    }
}
