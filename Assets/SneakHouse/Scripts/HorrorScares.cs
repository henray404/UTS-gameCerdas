using UnityEngine;

namespace SneakHouse
{
    // Distant unexplained noises during a mission, plus the jumpscare when the player is caught.
    [DisallowMultipleComponent, RequireComponent(typeof(GameSession))]
    public class HorrorScares : MonoBehaviour
    {
        public Vector2 scareInterval = new Vector2(16, 38);
        public Vector2 scareDistance = new Vector2(5, 11);
        [Min(.2f)] public float jumpscareSeconds = 1.6f;

        private GameSession game;
        private HorrorDirector director;
        private ChaseAtmosphere atmosphere;
        private AudioClip[] scares = new AudioClip[0];
        private AudioClip jumpscare;
        private AudioSource distant, scream;
        private Texture2D eyeTexture;
        private float nextScare, caughtAt = -100;
        private int lastScare = -1;
        private bool caughtShown;

        private void Start()
        {
            game = GetComponent<GameSession>();
            director = GetComponent<HorrorDirector>();
            if (Camera.main != null) atmosphere = Camera.main.GetComponent<ChaseAtmosphere>();
            scares = Resources.LoadAll<AudioClip>("Horror/Audio/Scares");
            jumpscare = Resources.Load<AudioClip>("Horror/Audio/jumpscare");
            distant = new GameObject("Distant scare voice").AddComponent<AudioSource>();
            distant.playOnAwake = false; distant.spatialBlend = 1; distant.dopplerLevel = 0;
            distant.rolloffMode = AudioRolloffMode.Linear; distant.minDistance = 3; distant.maxDistance = 30;
            scream = gameObject.AddComponent<AudioSource>();
            scream.playOnAwake = false; scream.spatialBlend = 0; scream.ignoreListenerPause = true;
            eyeTexture = GlowTexture();
            Schedule(1.4f);
        }

        private void Schedule(float scale) { nextScare = Time.time + Random.Range(scareInterval.x, scareInterval.y) * scale; }

        private void Update()
        {
            if (game.Phase == GameSession.ScreenState.Playing && Time.time >= nextScare)
            {
                // Chases already carry their own soundtrack; keep scares for the quiet moments.
                if (atmosphere != null && atmosphere.BeingChased) Schedule(.4f);
                else { PlayScare(); Schedule(1); }
            }
            if (game.Phase == GameSession.ScreenState.Results && !game.Won && !caughtShown)
            {
                caughtShown = true; caughtAt = Time.unscaledTime;
                if (jumpscare != null) scream.PlayOneShot(jumpscare, 1);
                if (director != null) director.Flicker(jumpscareSeconds);
            }
        }

        public void PlayScare()
        {
            if (scares.Length == 0 || game.player == null) return;
            int pick = Random.Range(0, scares.Length);
            if (scares.Length > 1 && pick == lastScare) pick = (pick + 1) % scares.Length;
            lastScare = pick;
            Vector2 around = Random.insideUnitCircle.normalized * Random.Range(scareDistance.x, scareDistance.y);
            distant.transform.position = game.player.transform.position + new Vector3(around.x, 1.2f, around.y);
            distant.pitch = Random.Range(.88f, 1.05f);
            distant.PlayOneShot(scares[pick], Random.Range(.55f, .9f));
            if (director != null && Random.value < .35f) director.Flicker(Random.Range(.4f, 1));
        }

        private static Texture2D GlowTexture()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Jumpscare eye", wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2 - 1, dy = ((y + .5f) / size * 2 - 1) * 1.9f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01(1 - d * 1.6f), halo = Mathf.Clamp01(1 - d) * .55f;
                    pixels[y * size + x] = new Color(1, .25f + core * .6f, .15f + core * .5f, Mathf.Clamp01(core + halo));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            float t = Time.unscaledTime - caughtAt;
            if (t < 0 || t > jumpscareSeconds || Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            float fade = Mathf.Clamp01((jumpscareSeconds - t) / .35f);
            GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (t < .12f)
            {
                GUI.color = new Color(.75f, .02f, .01f, (1 - t / .12f) * .9f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            // Two eyes rushing at the camera, jittering like a bad signal.
            float grow = Mathf.Lerp(.16f, .5f, Mathf.Clamp01(t / .5f)) * Screen.height;
            Vector2 shake = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * grow * .05f;
            Vector2 centre = new Vector2(Screen.width * .5f, Screen.height * .46f) + shake;
            GUI.color = new Color(1, 1, 1, fade);
            foreach (float side in new[] { -1f, 1f })
                GUI.DrawTexture(new Rect(centre.x + side * grow * .55f - grow * .4f, centre.y - grow * .21f, grow * .8f, grow * .42f), eyeTexture);
            GUI.color = previous;
        }

        private void OnDestroy() { if (eyeTexture != null) Destroy(eyeTexture); }
    }
}
