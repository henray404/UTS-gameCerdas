using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SneakHouse
{
    // Night-time horror pass applied at runtime, so the generated scene asset stays untouched.
    [DisallowMultipleComponent, RequireComponent(typeof(GameSession))]
    public class HorrorDirector : MonoBehaviour
    {
        public static readonly Color FogColour = new Color(.012f, .014f, .02f);
        public static bool Night { get; private set; }
        public static Font TitleFont { get; private set; }

        [Header("Darkness")]
        [Range(0, .3f)] public float indoorFog = .095f;
        [Range(0, .3f)] public float overheadFog = .018f;
        public Color indoorAmbient = new Color(.028f, .03f, .045f);
        public Color overheadAmbient = new Color(.10f, .11f, .17f);
        [Header("Flashlight")]
        public KeyCode flashlightKey = KeyCode.T;
        [Min(0)] public float flashlightIntensity = 2.4f;

        public bool FlashlightOn { get; private set; } = true;
        public Light Flashlight => torch;

        private GameSession game;
        private ChaseCamera view;
        private ChaseAtmosphere atmosphere;
        private Light torch;
        private AudioSource click;
        private Light[] lamps = new Light[0];
        private float[] lampBase = new float[0];
        private float flickerUntil, nextFlicker;

        private void Awake()
        {
            game = GetComponent<GameSession>();
            Night = true;
            TitleFont = Resources.Load<Font>("Horror/Fonts/Creepster-Regular");
        }

        private void Start()
        {
            Camera main = Camera.main;
            if (main != null) { view = main.GetComponent<ChaseCamera>(); atmosphere = main.GetComponent<ChaseAtmosphere>(); }
            SetupNight(main);
            HorrorDecor.Place(game);
            CollectLamps();
            torch = CreateFlashlight(main);
            foreach (GuardBrain guard in game.guards)
                if (guard != null && guard.GetComponent<MonsterSkin>() == null) guard.gameObject.AddComponent<MonsterSkin>();
            if (GetComponent<HorrorScares>() == null) gameObject.AddComponent<HorrorScares>();
            nextFlicker = Time.time + Random.Range(15f, 30f);
        }

        private static void SetupNight(Camera main)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColour;
            RenderSettings.reflectionIntensity = .08f;
            // Flashlight, lamps, objective glows and monster eyes all need per-pixel lighting.
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            if (main != null) { main.clearFlags = CameraClearFlags.SolidColor; main.backgroundColor = FogColour; }
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = new Color(.42f, .5f, .75f);
                light.intensity = .16f;
                light.shadowStrength = .9f;
            }
        }

        private void CollectLamps()
        {
            List<Light> keep = new List<Light>();
            foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type != LightType.Directional && light.name != "Objective glow") keep.Add(light);
            lamps = keep.ToArray(); lampBase = new float[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                lampBase[i] = lamps[i].intensity * .7f;
                lamps[i].color = Color.Lerp(lamps[i].color, new Color(1, .55f, .3f), .6f);
            }
        }

        private Light CreateFlashlight(Camera main)
        {
            if (main == null) return null;
            Light spot = new GameObject("Flashlight").AddComponent<Light>();
            spot.transform.SetParent(main.transform, false);
            spot.transform.localPosition = new Vector3(.18f, -.16f, .05f);
            spot.type = LightType.Spot; spot.range = 15; spot.spotAngle = 58;
            spot.color = new Color(1, .92f, .78f); spot.intensity = flashlightIntensity;
            spot.shadows = LightShadows.Soft; spot.shadowStrength = .85f; spot.shadowNearPlane = .2f;
            spot.renderMode = LightRenderMode.ForcePixel;
            spot.cookie = BeamCookie();
            click = gameObject.AddComponent<AudioSource>();
            click.playOnAwake = false; click.spatialBlend = 0; click.volume = .55f;
            click.clip = Resources.Load<AudioClip>("Horror/Audio/flashlight-click");
            return spot;
        }

        // Soft hotspot plus a faint outer ring, like a cheap incandescent torch.
        private static Texture2D BeamCookie()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.Alpha8, false)
            { name = "Flashlight beam", wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[size * size];
            Vector2 centre = new Vector2(size * .5f, size * .5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), centre) / (size * .5f);
                    float hot = Mathf.Exp(-d * d * 9), spill = Mathf.Clamp01(1 - d) * .45f;
                    float ring = Mathf.Exp(-Mathf.Pow((d - .62f) * 14, 2)) * .12f;
                    float alpha = d < .97f ? Mathf.Clamp01(hot + spill + ring) : 0;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        public void Flicker(float seconds) { flickerUntil = Mathf.Max(flickerUntil, Time.unscaledTime + seconds); }

        private void Update()
        {
            float blend = view != null ? view.ViewBlend : 1;
            RenderSettings.fogDensity = Mathf.Lerp(overheadFog, indoorFog, blend);
            RenderSettings.ambientLight = Color.Lerp(overheadAmbient, indoorAmbient, blend);
            bool live = game.Phase == GameSession.ScreenState.Playing;
            if (live && Input.GetKeyDown(flashlightKey))
            {
                FlashlightOn = !FlashlightOn;
                if (click != null && click.clip != null) click.PlayOneShot(click.clip);
            }
            if (live && Time.time >= nextFlicker) { Flicker(Random.Range(.5f, 1.3f)); nextFlicker = Time.time + Random.Range(20f, 45f); }
            float now = Time.unscaledTime;
            bool flickering = now < flickerUntil;
            // Monsters close behind make the bulb stutter.
            bool haunted = atmosphere != null && atmosphere.BeingChased && atmosphere.NearestChaserDistance < 4.5f;
            float stutter = flickering || haunted ? (Mathf.PerlinNoise(now * 23, 4.1f) > .45f ? 1 : .08f) : 1;
            if (torch != null)
            {
                torch.enabled = FlashlightOn && blend > .5f && (live || game.Phase == GameSession.ScreenState.Paused);
                torch.intensity = flashlightIntensity * stutter * (.96f + .04f * Mathf.PerlinNoise(now * 4, 1.7f));
            }
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;
                float hum = .78f + .22f * Mathf.PerlinNoise(now * 1.7f + i * 3.1f, i * .7f);
                float drop = Mathf.PerlinNoise(now * 9 + i * 11, 2.3f) > .82f ? .25f : 1;
                lamps[i].intensity = lampBase[i] * hum * drop * (flickering ? stutter : 1);
            }
        }

        private void OnDestroy() { Night = false; }
    }
}
