using UnityEngine;

namespace SneakHouse
{
    [RequireComponent(typeof(GameSession))]
    public class MissionFlowUI : MonoBehaviour
    {
        private GameSession game;
        private bool showGuide, showCredits;
        private int selectedLevel = 1;
        private GUIStyle heading, body, small, number, action, secondary, row, selectedRow, left;
        private readonly Color ink = LowPolyUI.Ink;
        private readonly Color muted = LowPolyUI.Muted;

        private void Awake() { game = GetComponent<GameSession>(); selectedLevel = game.Level.Number; }
        private GUIStyle Label(int size, Color colour, Font font)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, wordWrap = true };
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused })
                state.textColor = colour;
            return style;
        }
        private void Styles()
        {
            if (body != null) return;
            heading = Label(36, ink, game.titleFont);
            body = Label(25, ink, game.bodyFont);
            small = Label(20, muted, game.bodyFont);
            number = Label(60, ink, game.titleFont);
            heading.alignment = number.alignment = TextAnchor.MiddleCenter;
            body.alignment = small.alignment = TextAnchor.MiddleCenter;
            left = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft };
            action = LowPolyUI.ButtonStyle(game.bodyFont, 26, true);
            secondary = LowPolyUI.ButtonStyle(game.bodyFont, 25, false);
            row = LowPolyUI.ButtonStyle(game.bodyFont, 21, false);
            selectedRow = LowPolyUI.ButtonStyle(game.bodyFont, 21, true);
            row.alignment = selectedRow.alignment = TextAnchor.MiddleLeft;
            row.wordWrap = selectedRow.wordWrap = true;
        }
        private static void Fill(Rect rect, Color colour) { LowPolyUI.Fill(rect, colour); }
        private static void Card(Rect rect) { LowPolyUI.Board(rect); }
        private bool Button(Rect rect, string label, bool primary = true)
        {
            return LowPolyUI.Button(rect, label, primary ? action : secondary);
        }
        private void Text(float x, float y, float w, float h, string value, GUIStyle style)
        {
            if (style == heading || style == number) LowPolyUI.Title(new Rect(x, y, w, h), value, style);
            else GUI.Label(new Rect(x, y, w, h), value, style);
        }
        public static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
        private void OnGUI()
        {
            if (game.Phase == GameSession.ScreenState.Playing || game.Phase == GameSession.ScreenState.Paused) return;
            Styles();
            Fill(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), new Color(.14f, .18f, .09f, .29f));
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(UnityEngine.Screen.width / 1440f, UnityEngine.Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((UnityEngine.Screen.width - 1440 * scale) / 2,
                (UnityEngine.Screen.height - 900 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            switch (game.Phase)
            {
                case GameSession.ScreenState.MainMenu: MainMenu(); break;
                case GameSession.ScreenState.LevelSelect: LevelSelect(); break;
                case GameSession.ScreenState.Briefing: Briefing(); break;
                case GameSession.ScreenState.Results: Results(); break;
            }
            GUI.matrix = previous;
        }
        private void MainMenu()
        {
            if (showCredits) { Credits(); return; }
            if (showGuide) { Guide(); return; }
            Card(new Rect(460, 170, 520, 560));
            LowPolyUI.Plank(new Rect(424, 132, 592, 180));
            int logoSize = heading.fontSize; heading.fontSize = 48;
            LowPolyUI.Title(new Rect(454, 142, 532, 160), "SNEAK\nHOUSE", heading, true);
            heading.fontSize = logoSize;
            Text(500, 320, 440, 36, game.Progress.HighestCompleted + " / " + CampaignLevels.Count + " levels complete", small);
            if (Button(new Rect(510, 370, 420, 76), "Select Level")) OpenLevels();
            if (Button(new Rect(510, 474, 420, 68), "How to Play", false)) showGuide = true;
            if (Button(new Rect(510, 570, 420, 68), "Quit", false)) Quit();
            LowPolyUI.Ornament(new Rect(407, 239, 133, 133), 0, -14);
            LowPolyUI.Ornament(new Rect(912, 242, 138, 138), 1, 12);
            LowPolyUI.Ornament(new Rect(463, 651, 98, 98), 2, -18);
            LowPolyUI.Ornament(new Rect(892, 649, 116, 116), 3, 11);
        }
        private void Guide()
        {
            Card(new Rect(400, 80, 640, 740));
            Text(442, 120, 556, 80, "HOW TO PLAY", heading);
            Text(460, 215, 520, 330, "WASD / Arrow   -   Move\nShift   -   Sprint (stamina)\nCtrl / C   -   Sneak\nE   -   Collect\nQ   -   Drop can\nF   -   Hide / leave\nEsc   -   Pause\nF1   -   AI Debug", body);
            Text(450, 552, 540, 36, "Chased: first person / Mouse: look", small);
            Text(450, 598, 540, 48, "Collect marked items and escape to unlock the next level.", small);
            if (Button(new Rect(490, 654, 460, 52), "Music Credits", false)) showCredits = true;
            if (Button(new Rect(490, 726, 460, 60), "Back", false)) showGuide = false;
        }
        private void Credits()
        {
            Card(new Rect(330, 130, 780, 640));
            Text(380, 166, 680, 80, "MUSIC CREDITS", heading);
            Text(380, 276, 680, 132, "Sneaky Snitch\nKevin MacLeod (incompetech.com)\nCC BY 4.0\ncreativecommons.org/licenses/by/4.0/", small);
            Text(380, 440, 680, 114, "Pursuit / Vicegrip of Pursuit\nSudocolon - OpenGameArt.org\nCC0 / Public domain", small);
            Text(380, 566, 680, 52, "Playback edits and full asset credits: Audio-Credits.txt", small);
            if (Button(new Rect(490, 646, 460, 72), "Back", false)) showCredits = false;
        }
        private void OpenLevels()
        {
            selectedLevel = Mathf.Clamp(game.Progress.HighestUnlocked, 1, CampaignLevels.Count);
            game.ShowLevelSelect();
        }
        private string Status(int level)
        {
            if (game.Progress.IsCompleted(level)) return "COMPLETE";
            return game.Progress.IsUnlocked(level) ? "AVAILABLE" : "LOCKED";
        }
        private void LevelSelect()
        {
            Card(new Rect(100, 70, 1240, 770));
            LowPolyUI.Plank(new Rect(76, 40, 1288, 115));
            LowPolyUI.Title(new Rect(145, 55, 1150, 70), "THE MISSION BOARD", heading, true);
            Text(150, 160, 1140, 36, "Complete each escape to unlock the next contract. Completed levels stay replayable.", small);
            for (int i = 1; i <= CampaignLevels.Count; i++)
            {
                CampaignLevel level = CampaignLevels.Get(i);
                string label = i.ToString("00") + "   " + level.Name + "\n       " + Status(i) + "  /  " + level.MapName;
                if (LowPolyUI.Button(new Rect(150, 218 + (i - 1) * 84, 560, 74), label,
                    i == selectedLevel ? selectedRow : row)) selectedLevel = i;
            }
            LevelDetail(CampaignLevels.Get(selectedLevel));
            if (Button(new Rect(150, 750, 250, 58), "Main Menu", false)) game.ShowMainMenu();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && game.Progress.IsUnlocked(selectedLevel);
            if (Button(new Rect(790, 744, 480, 66), "Play Level " + selectedLevel)) game.StartLevel(selectedLevel);
            GUI.enabled = enabled;
        }
        private void LevelDetail(CampaignLevel level)
        {
            LowPolyUI.Inset(new Rect(760, 216, 530, 500));
            int titleSize = heading.fontSize;
            heading.fontSize = Mathf.Min(28, Mathf.FloorToInt(440f / level.Name.Length));
            Text(790, 238, 470, 60, level.Name.ToUpperInvariant(), heading);
            heading.fontSize = titleSize;
            Text(790, 304, 470, 48, level.MapName, body);
            Text(800, 368, 450, 32, level.Guards + (level.Guards == 1 ? " guard" : " guards") + "   /   " +
                level.TargetCount + (level.TargetCount == 1 ? " item" : " items") + "   /   " + level.Cans + " cans", left);
            Text(800, 408, 450, 32, "Difficulty: " + level.DifficultyScore.ToString("0") + " / 100", left);
            Text(800, 448, 450, 32, "Sight: " + level.VisionDistance.ToString("0.#") + " m / " + level.FieldOfView.ToString("0") + " deg   Search: " + level.SearchSeconds.ToString("0") + " s", left);
            Text(800, 488, 450, 32, "Pace: " + FormatTime(level.ReferenceSeconds) + " (score only, no time limit)", left);
            Text(800, 534, 450, 100, level.TacticalHint, left);
            string status = game.Progress.IsUnlocked(level.Number) ? Status(level.Number) : "Finish Level " + (level.Number - 1) + " to unlock.";
            Text(800, 650, 450, 40, status, small);
        }
        private void Briefing()
        {
            CampaignLevel level = game.Level;
            Card(new Rect(330, 85, 780, 730));
            int titleSize = heading.fontSize;
            string title = "LEVEL " + level.Number + "  /  " + level.Name.ToUpperInvariant();
            heading.fontSize = Mathf.Min(28, Mathf.FloorToInt(680f / title.Length));
            Text(368, 118, 704, 76, title, heading);
            heading.fontSize = titleSize;
            Text(380, 194, 680, 46, level.MapName, body);
            Text(380, 248, 680, 60, "Collect " + game.RequiredTargetCount + (game.RequiredTargetCount == 1 ? " marked item." : " marked items.") + " Return to the entrance.", body);
            RequiredPortraits();
            Text(380, 504, 680, 38, level.Guards + " guards   /   " + level.Cans + " cans   /   " + FormatTime(level.ReferenceSeconds) + " pace reference", small);
            Text(390, 552, 660, 76, level.TacticalHint, small);
            if (!game.MissionReady) Text(380, 624, 680, 42, game.MissionIssue, small);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && game.MissionReady;
            if (Button(new Rect(380, 688, 330, 76), "Enter House")) game.BeginMission();
            GUI.enabled = enabled;
            if (Button(new Rect(730, 688, 330, 76), "Levels", false)) OpenLevels();
        }
        private void RequiredPortraits()
        {
            float width = 660f / Mathf.Max(1, game.RequiredTargetCount);
            int slot = 0;
            foreach (int index in game.Level.TargetIndices)
            {
                if (index < 0 || index >= game.targets.Length) continue;
                LootTarget target = game.targets[index];
                if (target == null) continue;
                if (!game.IsRequiredTarget(target)) continue;
                float x = 390 + slot++ * width;
                LowPolyUI.Portrait(new Rect(x + (width - 104) / 2, 326, 104, 104), game.TargetIndex(target));
                Text(x + 5, 440, width - 10, 52, target.Label, small);
            }
        }
        private void Results()
        {
            bool finalWin = game.Won && game.Level.Number == CampaignLevels.Count;
            Card(new Rect(370, 65, 700, 770));
            int size = heading.fontSize; heading.fontSize = 30;
            Text(408, 108, 624, 82, finalWin ? "CAMPAIGN COMPLETE" : game.Won ? "MISSION COMPLETE" : "CAUGHT", heading);
            heading.fontSize = size;
            Text(420, 195, 600, 44, "Level " + game.Level.Number + "  /  " + game.Level.Name, small);
            LowPolyUI.Inset(new Rect(430, 256, 580, 148));
            Text(450, 264, 540, 36, game.NewRecord ? "NEW LEVEL BEST" : "SCORE", small);
            Text(450, 302, 540, 90, game.ResultScore.ToString("N0"), number);
            Text(430, 420, 580, 52, FormatTime(game.Elapsed) + "   /   " + game.Collected + " of " + game.RequiredTargetCount + " items", body);
            Text(430, 476, 580, 34, "Level best: " + game.BestScore.ToString("N0"), small);
            string progress = finalWin ? "All " + CampaignLevels.Count + " contracts complete. Every level is replayable." :
                game.NewlyUnlocked ? "Level " + (game.Level.Number + 1) + " unlocked." :
                game.Won ? "Escape complete. Choose your next contract." : "Watch the patrol. Try another route.";
            Text(430, 522, 580, 52, progress, small);
            bool next = game.Won && !finalWin;
            if (next && Button(new Rect(430, 588, 580, 68), "Next Level")) game.StartLevel(game.Level.Number + 1);
            if (Button(new Rect(430, next ? 674 : 602, 580, 64), "Replay Level", !next)) game.Restart();
            if (Button(new Rect(430, next ? 754 : 686, 580, 58), "Levels", false)) OpenLevels();
        }
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
