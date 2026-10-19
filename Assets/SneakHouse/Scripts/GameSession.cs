using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SneakHouse
{
    [DefaultExecutionOrder(-200)]
    public class GameSession : MonoBehaviour
    {
        public enum ScreenState { MainMenu, Briefing, Playing, Paused, Results }
        public ScreenState Phase { get; private set; } = ScreenState.MainMenu;
        public static GameSession Instance { get; private set; }
        public static event Action<Vector3, float, string> Noise;
        public static event Action<GuardBrain, Vector3, float> SharedAlert;
        public PlayerMotor player;
        public LootTarget[] targets;
        public GuardBrain[] guards;
        [Range(1, 3)] public int activeGuardCount = 1;
        public Font bodyFont, titleFont;
        [Min(1)] public float targetTime = 240;
        public bool DebugMode { get; private set; }
        public bool Finished { get; private set; }
        public bool Won { get; private set; }
        public bool Paused { get; private set; }
        public int Collected { get; private set; }
        public int AlertReports { get; private set; }
        public string Message { get; private set; } = "Collect all 3 items. Return to the entrance.";
        public float Elapsed { get; private set; }
        public float LastNoiseTime { get; private set; } = -10;
        public int ResultScore { get; private set; }
        public int BestScore { get; private set; }
        public int ObjectivePoints { get; private set; }
        public int StealthPoints { get; private set; }
        public int TimePoints { get; private set; }
        public bool NewRecord { get; private set; }
        public bool PersistRecords { get; set; } = true;
        public int ActiveGuards => Mathf.Clamp(activeGuardCount, 1, guards.Length);
        private string RecordKey => "SneakHouse.Score.v1." + SceneManager.GetActiveScene().name;
        private static bool reopenBriefing;
        private float messageUntil;
        private GUIStyle title, text, small, button, secondaryButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; Noise = null; SharedAlert = null; reopenBriefing = false; }
        private void Awake()
        {
            Instance = this;
            Font.textureRebuilt += OnFontTextureRebuilt;
            BestScore = PlayerPrefs.GetInt(RecordKey, 0);
            for (int i = 0; i < guards.Length; i++) guards[i].gameObject.SetActive(i < activeGuardCount);
            gameObject.AddComponent<MissionFlowUI>();
            SetScreen(reopenBriefing ? ScreenState.Briefing : ScreenState.MainMenu);
            reopenBriefing = false;
        }
        private void SetScreen(ScreenState screen)
        {
            Phase = screen;
            Paused = screen != ScreenState.Playing;
            Time.timeScale = Paused ? 0 : 1;
        }
        public void ShowBriefing()
        {
            if (Phase == ScreenState.MainMenu) SetScreen(ScreenState.Briefing);
        }
        public void ShowMainMenu()
        {
            if (Phase == ScreenState.Briefing) SetScreen(ScreenState.MainMenu);
            else if (Phase == ScreenState.Playing || Phase == ScreenState.Paused || Phase == ScreenState.Results)
            {
                reopenBriefing = false; Time.timeScale = 1;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
        public void BeginMission()
        {
            if (Phase == ScreenState.Briefing) SetScreen(ScreenState.Playing);
        }
        public void TogglePause()
        {
            if (Phase == ScreenState.Playing) SetScreen(ScreenState.Paused);
            else if (Phase == ScreenState.Paused) SetScreen(ScreenState.Playing);
        }
        private void Update()
        {
            if ((Phase == ScreenState.Playing || Phase == ScreenState.Paused) && Input.GetKeyDown(KeyCode.F1)) DebugMode = !DebugMode;
            if ((Phase == ScreenState.Playing || Phase == ScreenState.Paused || Finished) && Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Phase == ScreenState.Playing) Elapsed += Time.deltaTime;
            if (messageUntil > 0 && Time.time > messageUntil)
            {
                Message = Collected == targets.Length ? "All items collected. Return to the entrance." : "Watch the patrol. Running makes noise.";
                messageUntil = 0;
            }
        }
        public void Notify(string message) { Message = message; messageUntil = Time.time + 3; }
        public void AddLoot(LootTarget target)
        {
            if (Phase != ScreenState.Playing || target.Collected) return;
            target.MarkCollected(); Collected++;
            Notify("Collected: " + target.Label);
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
            if (Phase != ScreenState.Playing) return;
            Finished = true; Won = win;
            ObjectivePoints = win ? 6000 : 0;
            StealthPoints = win ? Mathf.Max(0, 2500 - AlertReports * 500) : 0;
            TimePoints = win ? Mathf.RoundToInt(1500 * Mathf.Clamp01(1 - Elapsed / Mathf.Max(1, targetTime))) : 0;
            ResultScore = ObjectivePoints + StealthPoints + TimePoints;
            NewRecord = win && ResultScore > BestScore;
            if (NewRecord)
            {
                BestScore = ResultScore;
                if (PersistRecords) { PlayerPrefs.SetInt(RecordKey, BestScore); PlayerPrefs.Save(); }
            }
            SetScreen(ScreenState.Results);
            Message = win ? "All items secured. You made it out." : "Caught by a guard. Try another route.";
        }
        public void Restart()
        {
            reopenBriefing = true;
            Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        private void OnFontTextureRebuilt(Font font)
        {
            if ((font == bodyFont || font == titleFont) && font != null && font.material != null && font.material.mainTexture != null)
                font.material.mainTexture.filterMode = FilterMode.Point;
        }
        private void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            if (Instance == this) { Instance = null; Time.timeScale = 1; }
        }

        private void EnsureStyles()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { font = bodyFont, fontSize = 22, wordWrap = true };
            foreach (GUIStyleState state in new[] { text.normal, text.hover, text.active, text.focused })
                state.textColor = LowPolyUI.Ink;
            small = new GUIStyle(text) { fontSize = 17 };
            title = new GUIStyle(text) { font = titleFont, fontSize = 37 };
            button = LowPolyUI.ButtonStyle(bodyFont, 23, true);
            secondaryButton = LowPolyUI.ButtonStyle(bodyFont, 23, false);
        }
        private static void Rect(Rect r, Color colour)
        {
            Color old = GUI.color; GUI.color = colour; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }
        private void OnGUI()
        {
            if (Phase != ScreenState.Playing && Phase != ScreenState.Paused) return;
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            LowPolyUI.Board(new Rect(24, 22, 300, 106));
            for (int i = 0; i < targets.Length; i++)
            {
                LowPolyUI.Portrait(new Rect(46 + i * 58, 42, 44, 44), i);
                if (targets[i].Collected) GUI.Label(new Rect(51 + i * 58, 86, 44, 24), "OK", small);
            }
            GUI.Label(new Rect(232, 55, 72, 36), Collected + " / " + targets.Length, text);
            if(player!=null && player.Distractions!=null)
            {
                LowPolyUI.Inset(new Rect(24,144,222,42));
                GUI.Label(new Rect(42,151,190,30),"[Q] Can  x "+player.Distractions.Remaining,small);
            }
            float suspicion = 0;
            foreach (GuardBrain g in guards) if (g.gameObject.activeInHierarchy) suspicion = Mathf.Max(suspicion, g.Suspicion);
            LowPolyUI.Board(new Rect(w - 262, 22, 238, 106));
            GUI.Label(new Rect(w - 238, 40, 190, 28), player.IsHidden ? player.Hiding.Compromised ? "Compromised" : "Hidden" : suspicion >= 1 ? "Detected" : suspicion > .05f ? "Suspicious" : "Undetected", small);
            LowPolyUI.Inset(new Rect(w - 240, 70, 194, 14));
            Rect(new Rect(w - 235, 74, 184 * suspicion, 6), suspicion >= 1 ? LowPolyUI.Red : LowPolyUI.Green);
            GUI.Label(new Rect(w - 238, 89, 190, 24), MissionFlowUI.FormatTime(Elapsed), small);
            LowPolyUI.Board(new Rect(w / 2 - 310, h - 86, 620, 62));
            string hint=player.IsHidden?(player.Hiding.Compromised?"Spotted entering! Leave this hiding spot.":player.Hiding.CurrentSpot.HiddenText+". [F] Leave"):Message;
            GUI.Label(new Rect(w / 2 - 286, h - 70, 572, 33), hint, text);
            if(!player.IsHidden && player.Hiding!=null && player.Hiding.NearestSpot!=null)
            {
                HidingSpot spot=player.Hiding.NearestSpot;Vector3 p=Camera.main.WorldToScreenPoint(spot.Approach+Vector3.up*.45f);
                GUI.Label(new Rect(p.x/scale-115,(Screen.height-p.y)/scale-30,300,45),"[F] Hide: "+spot.displayName,small);
            }
            if (player != null && !player.IsHidden && !Finished && !Paused && player.NearestLoot != null)
            {
                Vector3 p = Camera.main.WorldToScreenPoint(player.NearestLoot.transform.position + Vector3.up * .45f);
                GUI.Label(new Rect(p.x / scale - 115, (Screen.height - p.y) / scale - 45, 260, 60), "[E] " + player.NearestLoot.Label, small);
            }
            if (DebugMode)
            {
                float y = 270;
                foreach (GuardBrain g in guards)
                {
                    if (!g.gameObject.activeInHierarchy) continue;
                    LowPolyUI.Board(new Rect(24, y + 18, 430, 178));
                    GUI.Label(new Rect(48, y + 40, 382, 138),
                        g.name + " / " + g.State + "\nCan see: " + g.CanSeePlayer + " | suspicion: " + g.Suspicion.ToString("0.00") +
                        "\nMemory: " + g.MemorySource + " " + g.LastKnownPosition.ToString("F1") +
                        "\nLast evidence: " + (g.HasMemory ? (Time.time - g.LastEvidenceTime).ToString("0.0") + " s ago" : "-") +
                        "\nPath: " + g.Agent.pathStatus + " | reports: " + AlertReports, small);
                    y += 192;
                }
            }
            if (Phase == ScreenState.Paused)
            {
                LowPolyUI.Board(new Rect(w / 2 - 270, h / 2 - 185, 540, 370));
                GUIStyle centeredTitle = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
                LowPolyUI.Title(new Rect(w / 2 - 230, h / 2 - 151, 460, 64), "PAUSED", centeredTitle);
                if (LowPolyUI.Button(new Rect(w / 2 - 220, h / 2 - 32, 440, 70), "Resume", button)) TogglePause();
                if (LowPolyUI.Button(new Rect(w / 2 - 220, h / 2 + 72, 440, 64), "Main Menu", secondaryButton)) ShowMainMenu();
            }
            GUI.matrix = old;
        }
    }
}
