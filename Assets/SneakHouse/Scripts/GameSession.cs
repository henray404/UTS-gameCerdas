using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SneakHouse
{
    [DefaultExecutionOrder(-200)]
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public static event Action<Vector3, float, string> Noise;
        public static event Action<GuardBrain, Vector3, float> SharedAlert;
        public PlayerMotor player;
        public LootTarget[] targets;
        public GuardBrain[] guards;
        [Range(1, 3)] public int activeGuardCount = 1;
        public Font bodyFont, titleFont;
        public bool DebugMode { get; private set; }
        public bool Finished { get; private set; }
        public bool Won { get; private set; }
        public bool Paused { get; private set; }
        public int Collected { get; private set; }
        public int AlertReports { get; private set; }
        public string Message { get; private set; } = "Ambil tiga target, lalu kembali ke pintu masuk.";
        public float Elapsed { get; private set; }
        public float LastNoiseTime { get; private set; } = -10;
        private float messageUntil;
        private GUIStyle title, text, small, button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; Noise = null; SharedAlert = null; }
        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1;
            for (int i = 0; i < guards.Length; i++) guards[i].gameObject.SetActive(i < activeGuardCount);
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) DebugMode = !DebugMode;
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (!Finished && Input.GetKeyDown(KeyCode.Escape))
            {
                Paused = !Paused; Time.timeScale = Paused ? 0 : 1;
            }
            if (!Finished && !Paused) Elapsed += Time.deltaTime;
            if (messageUntil > 0 && Time.time > messageUntil)
            {
                Message = Collected == targets.Length ? "Semua target didapat. Kembali ke pintu masuk." : "Amati patroli. Lari menimbulkan suara.";
                messageUntil = 0;
            }
        }
        public void Notify(string message) { Message = message; messageUntil = Time.time + 3; }
        public void AddLoot(LootTarget target)
        {
            if (Finished || target.Collected) return;
            target.MarkCollected(); Collected++;
            Notify("Didapat: " + target.displayName);
        }
        public void EmitNoise(Vector3 point, float radius, string kind)
        {
            LastNoiseTime = Time.time; Noise?.Invoke(point, radius, kind);
            NoisePulse.Show(point, radius);
        }
        public void Broadcast(GuardBrain sender, Vector3 point, float timestamp)
        {
            AlertReports++; SharedAlert?.Invoke(sender, point, timestamp);
        }
        public void End(bool win)
        {
            if (Finished) return;
            Finished = true; Won = win; Paused = false; Time.timeScale = 0;
            Message = win ? "Tiga target aman. Kamu berhasil keluar." : "Penjaga menangkapmu. Coba rute lain.";
        }
        public void Restart()
        {
            Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void EnsureStyles()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { font = bodyFont, fontSize = 22, wordWrap = true };
            text.normal.textColor = new Color(.16f, .24f, .19f);
            small = new GUIStyle(text) { fontSize = 17 };
            title = new GUIStyle(text) { font = titleFont, fontSize = 37 };
            button = new GUIStyle(GUI.skin.button) { font = bodyFont, fontSize = 23, padding = new RectOffset(15, 15, 8, 8) };
        }
        private static void Rect(Rect r, Color colour)
        {
            Color old = GUI.color; GUI.color = colour; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }
        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.width / 1440f, .6f, 1.6f);
            Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            Color paper = new Color(.98f, .96f, .90f, .94f);
            Rect(new Rect(24, 22, 342, 228), paper);
            GUI.Label(new Rect(40, 30, 310, 48), "SNEAK HOUSE", title);
            GUI.Label(new Rect(40, 79, 310, 28), "VILA / PROTOTYPE 01", small);
            for (int i = 0; i < targets.Length; i++)
                GUI.Label(new Rect(40, 119 + i * 31, 307, 31), (targets[i].Collected ? "[OK]  " : "[  ]  ") + targets[i].displayName, text);
            GUI.Label(new Rect(40, 217, 310, 30), Collected + " / " + targets.Length + " target", small);
            float suspicion = 0;
            foreach (GuardBrain g in guards) if (g.gameObject.activeInHierarchy) suspicion = Mathf.Max(suspicion, g.Suspicion);
            Rect(new Rect(w - 284, 22, 260, 116), paper);
            GUI.Label(new Rect(w - 267, 35, 224, 32), suspicion >= 1 ? "TERDETEKSI" : suspicion > .05f ? "PENJAGA CURIGA" : "BELUM TERDETEKSI", small);
            Rect(new Rect(w - 266, 80, 220, 13), new Color(.78f, .80f, .72f));
            Rect(new Rect(w - 266, 80, 220 * suspicion, 13), suspicion >= 1 ? new Color(.74f, .26f, .17f) : new Color(.75f, .55f, .21f));
            GUI.Label(new Rect(w - 267, 102, 224, 25), Mathf.FloorToInt(Elapsed / 60).ToString("00") + ":" + Mathf.FloorToInt(Elapsed % 60).ToString("00"), small);
            Rect(new Rect(24, h - 91, w - 48, 67), paper);
            GUI.Label(new Rect(40, h - 83, w - 80, 29), Message, text);
            GUI.Label(new Rect(40, h - 51, w - 80, 26), "WASD gerak  /  Shift lari  /  Ctrl mengendap  /  E ambil  /  F1 debug  /  R ulang  /  Esc jeda", small);
            if (player != null && !Finished && !Paused && player.NearestLoot != null)
            {
                Vector3 p = Camera.main.WorldToScreenPoint(player.NearestLoot.transform.position + Vector3.up * .45f);
                GUI.Label(new Rect(p.x / scale - 115, (Screen.height - p.y) / scale - 45, 260, 60), "[E] " + player.NearestLoot.displayName, small);
            }
            if (DebugMode)
            {
                float y = 270;
                foreach (GuardBrain g in guards)
                {
                    if (!g.gameObject.activeInHierarchy) continue;
                    Rect(new Rect(24, y, 397, 142), paper);
                    GUI.Label(new Rect(38, y + 8, 370, 132),
                        g.name + " / " + g.State + "\nCan see: " + g.CanSeePlayer + " | suspicion: " + g.Suspicion.ToString("0.00") +
                        "\nMemory: " + g.MemorySource + " " + g.LastKnownPosition.ToString("F1") +
                        "\nLast evidence: " + (g.HasMemory ? (Time.time - g.LastEvidenceTime).ToString("0.0") + " s lalu" : "-") +
                        "\nPath: " + g.Agent.pathStatus + " | reports: " + AlertReports, small);
                    y += 154;
                }
            }
            if (Finished || Paused)
            {
                Rect(new Rect(w / 2 - 260, h / 2 - 110, 520, 232), paper);
                GUI.Label(new Rect(w / 2 - 238, h / 2 - 92, 476, 54), Finished ? Won ? "BERHASIL KELUAR" : "TERTANGKAP" : "JEDA", title);
                GUI.Label(new Rect(w / 2 - 238, h / 2 - 31, 476, 59), Message, text);
                if (GUI.Button(new Rect(w / 2 - 238, h / 2 + 55, 225, 47), "Ulangi misi", button)) Restart();
                if (Paused && GUI.Button(new Rect(w / 2 + 12, h / 2 + 55, 225, 47), "Lanjut", button))
                { Paused = false; Time.timeScale = 1; }
            }
            GUI.matrix = old;
        }
    }
}
