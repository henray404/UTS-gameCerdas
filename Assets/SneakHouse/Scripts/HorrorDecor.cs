using System.Collections.Generic;
using UnityEngine;

namespace SneakHouse
{
    // Scatters Kenney graveyard props (CC0) over the lawns. Decoration only: no colliders, so routes,
    // NavMesh and AI validation are unaffected. Seeded per level so replays keep the same yard.
    public static class HorrorDecor
    {
        private static readonly Dictionary<Material, Material> Weathered = new Dictionary<Material, Material>();
        private struct Prop { public string model; public float height; public int count; public bool lit; }
        private static readonly Prop[] Props =
        {
            new Prop { model = "gravestone-round", height = .95f, count = 3 },
            new Prop { model = "gravestone-cross", height = 1.1f, count = 2 },
            new Prop { model = "gravestone-broken", height = .7f, count = 2 },
            new Prop { model = "gravestone-bevel", height = .9f, count = 2 },
            new Prop { model = "gravestone-decorative", height = 1f, count = 1 },
            new Prop { model = "grave", height = .35f, count = 2 },
            new Prop { model = "cross-wood", height = 1.2f, count = 2 },
            new Prop { model = "coffin-old", height = .45f, count = 1 },
            new Prop { model = "debris", height = .3f, count = 2 },
            new Prop { model = "pine-crooked", height = 3.6f, count = 2 },
            new Prop { model = "candle-multiple", height = .3f, count = 2, lit = true },
        };

        public static void Place(GameSession game)
        {
            List<Bounds> lawns = new List<Bounds>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (renderer.name.StartsWith("Lawn")) lawns.Add(renderer.bounds);
            if (lawns.Count == 0) return;
            HouseShell shell = game.GetComponent<HouseShell>();
            Rect[] house = shell != null ? shell.Footprint : new Rect[0];
            ExtractionZone[] exits = Object.FindObjectsByType<ExtractionZone>(FindObjectsSortMode.None);
            HidingSpot[] spots = Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
            System.Random random = new System.Random(7919 * game.Level.Number);
            Transform root = new GameObject("Horror graveyard decor").transform;
            List<Vector3> placed = new List<Vector3>();
            foreach (Prop prop in Props)
            {
                GameObject model = Resources.Load<GameObject>("Horror/Graveyard/" + prop.model);
                if (model == null) continue;
                for (int i = 0; i < prop.count; i++)
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        Bounds lawn = lawns[random.Next(lawns.Count)];
                        Vector3 point = new Vector3(Mathf.Lerp(lawn.min.x, lawn.max.x, (float)random.NextDouble()), lawn.max.y,
                            Mathf.Lerp(lawn.min.z, lawn.max.z, (float)random.NextDouble()));
                        if (!Free(point, house, exits, spots, placed)) continue;
                        Spawn(model, prop, point, (float)random.NextDouble() * 360, root);
                        placed.Add(point); break;
                    }
            }
        }

        private static bool Free(Vector3 point, Rect[] house, ExtractionZone[] exits, HidingSpot[] spots, List<Vector3> placed)
        {
            Vector2 flat = new Vector2(point.x, point.z);
            foreach (Rect rect in house)
                if (new Rect(rect.x - 1.6f, rect.y - 1.6f, rect.width + 3.2f, rect.height + 3.2f).Contains(flat)) return false;
            foreach (ExtractionZone exit in exits) if (Vector3.Distance(exit.transform.position, point) < 3.2f) return false;
            foreach (HidingSpot spot in spots) if (Vector3.Distance(spot.Approach, point) < 1.8f) return false;
            foreach (Vector3 other in placed) if (Vector3.Distance(other, point) < 1.3f) return false;
            // Keep clear of fences, props and anything else solid already in the yard.
            return !Physics.CheckSphere(point + Vector3.up * .6f, .5f, ~0, QueryTriggerInteraction.Ignore);
        }

        // The kit palette is cheerful; dim and cool it so props sit in the night instead of glowing.
        private static Material Weather(Material source)
        {
            if (source == null) return null;
            if (!Weathered.TryGetValue(source, out Material dim))
            {
                dim = new Material(source) { name = source.name + " (weathered)" };
                dim.color = source.color * new Color(.42f, .42f, .48f);
                if (dim.HasProperty("_Glossiness")) dim.SetFloat("_Glossiness", .05f);
                Weathered[source] = dim;
            }
            return dim;
        }

        private static Bounds RenderedBounds(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void Spawn(GameObject model, Prop prop, Vector3 point, float yaw, Transform root)
        {
            GameObject instance = Object.Instantiate(model, point, Quaternion.Euler(0, yaw, 0), root);
            instance.name = "Decor / " + prop.model;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = Weather(materials[i]);
                renderer.sharedMaterials = materials;
            }
            // Kit units vary by exporter, so size every prop from its rendered height, then stand it on the lawn.
            Bounds bounds = RenderedBounds(instance);
            instance.transform.localScale *= prop.height / Mathf.Max(.01f, bounds.size.y);
            instance.transform.position += Vector3.up * (point.y - RenderedBounds(instance).min.y);
            if (!prop.lit) return;
            Light flame = new GameObject("Candle light").AddComponent<Light>();
            flame.transform.SetParent(instance.transform, true);
            flame.transform.position = point + Vector3.up * (prop.height + .15f);
            flame.type = LightType.Point; flame.color = new Color(1, .55f, .25f);
            flame.range = 3; flame.intensity = 1.1f; flame.shadows = LightShadows.None;
        }
    }
}
