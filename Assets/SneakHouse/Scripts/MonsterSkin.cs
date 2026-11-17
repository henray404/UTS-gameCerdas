using UnityEngine;
using UnityEngine.Rendering;

namespace SneakHouse
{
    // Turns a patrol guard into a shadow creature: wet black skin, glowing eyes and an idle growl.
    // The guard's AI is untouched; only its look and sound change.
    [DisallowMultipleComponent, RequireComponent(typeof(GuardBrain))]
    public class MonsterSkin : MonoBehaviour
    {
        private static Material skin, eyeMaterial;
        private GuardBrain guard;
        private Animator skinned;
        private AudioSource growl;
        private Light glow;
        private float seed;
        private bool growlStarted;

        public static string NameFor(string original)
        {
            switch (original)
            {
                case "Bogo": return "The Warden";
                case "Judy": return "The Hare";
                case "Clawhauser": return "The Glutton";
                case "Spike": return "The Crawler";
                case "Stan": return "The Hollow Man";
                default: return string.IsNullOrEmpty(original) ? "The Thing" : original;
            }
        }

        private void Awake()
        {
            guard = GetComponent<GuardBrain>();
            seed = Random.value * 50;
            guard.characterName = NameFor(guard.characterName);
            growl = gameObject.AddComponent<AudioSource>();
            growl.clip = Resources.Load<AudioClip>("Horror/Audio/monster-loop");
            growl.loop = true; growl.playOnAwake = false; growl.dopplerLevel = 0;
            growl.spatialBlend = 1; growl.rolloffMode = AudioRolloffMode.Linear;
            growl.minDistance = 1; growl.maxDistance = 13;
            growl.pitch = Random.Range(.82f, 1.08f);
        }

        private void LateUpdate()
        {
            // Encounters swap the visual prefab per mission, so re-skin whenever the animator changes.
            if (guard.acting != null && guard.acting.animator != null && guard.acting.animator != skinned) Apply(guard.acting.animator);
            bool chasing = guard.State == GuardBrain.Mode.Chase;
            if (glow != null)
                glow.intensity = (chasing ? 1.5f : .75f) * (.8f + .2f * Mathf.PerlinNoise(Time.time * 3, seed));
            UpdateGrowl(chasing);
        }

        private void UpdateGrowl(bool chasing)
        {
            if (growl.clip == null) return;
            GameSession game = GameSession.Instance;
            if (game == null || game.Phase != GameSession.ScreenState.Playing) { if (growl.isPlaying) growl.Pause(); return; }
            if (!growl.isPlaying)
            {
                if (growlStarted) growl.UnPause();
                else { growl.time = Random.Range(0, growl.clip.length * .9f); growl.Play(); growlStarted = true; }
            }
            growl.volume = Mathf.MoveTowards(growl.volume, chasing ? .85f : .45f, Time.deltaTime);
        }

        private void Apply(Animator animator)
        {
            skinned = animator;
            Material body = Skin();
            bool face = false;
            foreach (Renderer renderer in animator.GetComponentsInChildren<Renderer>(true))
            {
                // Each cast model draws its pupils, nose and mouth with a "*_FaceDark" material; those slots glow red.
                Material[] originals = renderer.sharedMaterials;
                Material[] shadows = new Material[Mathf.Max(1, originals.Length)];
                for (int i = 0; i < shadows.Length; i++)
                {
                    bool features = i < originals.Length && originals[i] != null && originals[i].name.Contains("FaceDark");
                    shadows[i] = features ? EyeMaterial() : body;
                    face |= features;
                }
                renderer.sharedMaterials = shadows;
            }
            if (face) AddGlow(animator, HeadPoint(animator));
            else AddEyes(animator);
        }

        private Vector3 HeadPoint(Animator animator)
        {
            Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return transform.position + Vector3.up * guard.eyeHeight;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return new Vector3(transform.position.x, bounds.max.y - bounds.size.y * .24f, transform.position.z);
        }

        private void AddGlow(Animator animator, Vector3 eyes)
        {
            glow = new GameObject("Eye glow").AddComponent<Light>();
            glow.transform.position = eyes + transform.forward * .12f;
            Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : FindByName(animator.transform, "head");
            glow.transform.SetParent(head != null ? head : animator.transform, true);
            glow.type = LightType.Point; glow.color = new Color(1, .12f, .05f);
            glow.range = 2.4f; glow.shadows = LightShadows.None;
        }

        private void AddEyes(Animator animator)
        {
            Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) head = FindByName(animator.transform, "head");
            Transform eyes = new GameObject("Monster eyes").transform;
            eyes.SetPositionAndRotation(transform.position + Vector3.up * guard.eyeHeight * .97f + transform.forward * .16f, transform.rotation);
            eyes.SetParent(head != null ? head : animator.transform, true);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "Eye"; eye.layer = 2;
                Destroy(eye.GetComponent<Collider>());
                eye.transform.SetParent(eyes, false);
                eye.transform.localPosition = new Vector3(side * .055f, 0, 0);
                eye.transform.localScale = new Vector3(.05f, .028f, .03f);
                Renderer renderer = eye.GetComponent<Renderer>();
                renderer.sharedMaterial = EyeMaterial(); renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            glow = new GameObject("Eye glow").AddComponent<Light>();
            glow.transform.SetParent(eyes, false);
            glow.transform.localPosition = Vector3.forward * .08f;
            glow.type = LightType.Point; glow.color = new Color(1, .12f, .05f);
            glow.range = 2.4f; glow.shadows = LightShadows.None;
        }

        private static Transform FindByName(Transform root, string part)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.ToLowerInvariant().Contains(part)) return child;
            return null;
        }

        private static Material Skin()
        {
            if (skin != null) return skin;
            skin = new Material(Shader.Find("Standard")) { name = "Monster shadow skin" };
            skin.color = new Color(.03f, .027f, .034f);
            skin.SetFloat("_Glossiness", .38f);
            skin.SetFloat("_Metallic", 0);
            return skin;
        }

        private static Material EyeMaterial()
        {
            if (eyeMaterial != null) return eyeMaterial;
            // Sprites/Default is always included in builds and ignores scene lighting.
            eyeMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Monster eyes", color = new Color(1, .16f, .08f) };
            return eyeMaterial;
        }

        private void OnDisable() { if (growl != null) growl.Pause(); }
    }
}
