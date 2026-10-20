using UnityEngine;

namespace SneakHouse
{
    [RequireComponent(typeof(GameSession))]
    public class MissionFlowUI : MonoBehaviour
    {
        private GameSession game;
        private bool showGuide, showCredits, showRecords, speedBoard;
        private int boardLevel = 1;
        private LevelSelectionUI levelMenu;
        private GUIStyle heading, body, small, number, action, secondary, row, selectedRow, left;
        private GUIStyle tab, selectedTab, cell, cellRight, header, headerRight, badge;
        private readonly Color ink = LowPolyUI.Ink;
        private readonly Color muted = LowPolyUI.Muted;

        private void Awake()
        {
            game = GetComponent<GameSession>();
            boardLevel = game.Level.Number;
            levelMenu = new LevelSelectionUI(game);
        }
        private void OnDestroy() { levelMenu?.Dispose(); }
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
            action = LowPolyUI.ButtonStyle(game.bodyFont, 26, true);
            secondary = LowPolyUI.ButtonStyle(game.bodyFont, 25, false);
            left = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft };
            row = LowPolyUI.ButtonStyle(game.bodyFont, 21, false);
            selectedRow = LowPolyUI.ButtonStyle(game.bodyFont, 21, true);
            row.alignment = selectedRow.alignment = TextAnchor.MiddleLeft;
            row.wordWrap = selectedRow.wordWrap = true;
            tab = LowPolyUI.ButtonStyle(game.bodyFont, 22, false);
            selectedTab = LowPolyUI.ButtonStyle(game.bodyFont, 22, true);
            cell = Label(20, ink, game.bodyFont);
            cell.alignment = TextAnchor.MiddleLeft; cell.wordWrap = false; cell.clipping = TextClipping.Clip;
            cellRight = new GUIStyle(cell) { alignment = TextAnchor.MiddleRight };
            header = Label(16, muted, game.bodyFont);
            header.alignment = TextAnchor.MiddleLeft; header.wordWrap = false;
            headerRight = new GUIStyle(header) { alignment = TextAnchor.MiddleRight };
            badge = Label(17, ink, game.bodyFont);
            badge.alignment = TextAnchor.MiddleCenter; badge.wordWrap = false;
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
        public static string FormatSplit(float seconds)
        {
            int hundredths = Mathf.Max(0, Mathf.RoundToInt(seconds * 100));
            return (hundredths / 6000).ToString("00") + ":" + (hundredths / 100 % 60).ToString("00") + "." + (hundredths % 100).ToString("00");
        }
        private static string Points(int value) => value > 0 ? value.ToString("N0") : "-";
        private static string Split(float seconds) => seconds > 0 ? FormatSplit(seconds) : "-";
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
                case GameSession.ScreenState.LevelSelect: levelMenu.Draw(); break;
                case GameSession.ScreenState.Briefing: Briefing(); break;
                case GameSession.ScreenState.Results: Results(); break;
            }
            GUI.matrix = previous;
        }
        private void MainMenu()
        {
            if (showCredits) { Credits(); return; }
            if (showGuide) { Guide(); return; }
            if (showRecords) { Records(); return; }
            LowPolyUI.Plank(new Rect(470, 30, 500, 168));
            int logoSize = heading.fontSize; heading.fontSize = 48;
            LowPolyUI.Title(new Rect(490, 38, 460, 152), "SNEAK\nHOUSE", heading, true);
            heading.fontSize = logoSize;
            int done = game.Progress.HighestCompleted, next = Mathf.Clamp(game.Progress.HighestUnlocked, 1, CampaignLevels.Count);
            Card(new Rect(170, 236, 440, 584));
            Text(200, 256, 380, 30, "CAMPAIGN", small);
            Pips(new Rect(215, 294, 350, 20));
            Text(200, 322, 380, 32, done + " / " + CampaignLevels.Count + " levels complete", small);
            string play = done == 0 ? "Start Campaign" : done >= CampaignLevels.Count ? "Replay Level " + next : "Continue: Level " + next;
            if (Button(new Rect(200, 372, 380, 78), play)) game.StartLevel(next);
            if (Button(new Rect(200, 472, 380, 62), "Select Level", false)) OpenLevels();
            if (Button(new Rect(200, 552, 380, 62), "Leaderboard", false)) OpenRecords();
            if (Button(new Rect(200, 632, 380, 62), "How to Play", false)) showGuide = true;
            if (Button(new Rect(200, 712, 380, 62), "Quit", false)) Quit();
            RecordPreview(new Rect(650, 236, 620, 584));
            LowPolyUI.Ornament(new Rect(372, 52, 120, 120), 0, -14);
            LowPolyUI.Ornament(new Rect(950, 58, 124, 124), 1, 12);
            LowPolyUI.Ornament(new Rect(110, 742, 98, 98), 2, -18);
            LowPolyUI.Ornament(new Rect(1222, 744, 104, 104), 3, 11);
        }
        private void Pips(Rect area)
        {
            float gap = 8, width = (area.width - gap * (CampaignLevels.Count - 1)) / CampaignLevels.Count;
            for (int i = 1; i <= CampaignLevels.Count; i++)
            {
                Color colour = game.Progress.IsCompleted(i) ? LowPolyUI.Green : game.Progress.IsUnlocked(i) ? LowPolyUI.Gold : new Color(.24f, .21f, .16f, .22f);
                Fill(new Rect(area.x + (i - 1) * (width + gap), area.y, width, area.height), colour);
            }
        }
        private void RecordPreview(Rect area)
        {
            ScoreProfile scores = game.Scores;
            Card(area);
            int size = heading.fontSize; heading.fontSize = 30;
            Text(area.x + 30, area.y + 18, area.width - 60, 52, "BEST RECORDS", heading);
            heading.fontSize = size;
            float x = area.x + 34, w = area.width - 68;
            GUI.Label(new Rect(x + 14, area.y + 76, 200, 26), "LEVEL", header);
            GUI.Label(new Rect(x + w - 274, area.y + 76, 120, 26), "SCORE", headerRight);
            GUI.Label(new Rect(x + w - 136, area.y + 76, 122, 26), "FASTEST", headerRight);
            for (int i = 1; i <= CampaignLevels.Count; i++)
            {
                Rect line = new Rect(x, area.y + 106 + (i - 1) * 58, w, 50);
                LowPolyUI.Inset(line);
                string name = game.Progress.IsUnlocked(i) ? CampaignLevels.Get(i).Name : "Locked";
                GUI.Label(new Rect(line.x + 14, line.y, w - 290, line.height), i.ToString("00") + "  " + name, cell);
                GUI.Label(new Rect(line.x + w - 274, line.y, 120, line.height), Points(scores.BestScore(i)), cellRight);
                GUI.Label(new Rect(line.x + w - 136, line.y, 122, line.height), Split(scores.BestTime(i)), cellRight);
            }
            Text(area.x + 30, area.y + 460, area.width - 60, 34, "Total score  " + scores.TotalBestScore(CampaignLevels.Count).ToString("N0"), body);
            Text(area.x + 30, area.y + 496, area.width - 60, 30, SpeedTotal(scores), small);
            Text(area.x + 30, area.y + 530, area.width - 60, 30, "Local profile: " + scores.PlayerName, small);
        }
        private static string SpeedTotal(ScoreProfile scores)
        {
            int timed = scores.TimedLevels(CampaignLevels.Count);
            if (timed == 0) return "Speedrun total  -";
            string total = "Speedrun total  " + FormatSplit(scores.TotalBestTime(CampaignLevels.Count));
            return timed == CampaignLevels.Count ? total : total + "  (" + timed + " / " + CampaignLevels.Count + " levels)";
        }
        private void OpenRecords()
        {
            boardLevel = Mathf.Clamp(game.Level.Number, 1, CampaignLevels.Count);
            showRecords = true;
        }
        private void Records()
        {
            ScoreProfile scores = game.Scores;
            Card(new Rect(100, 70, 1240, 770));
            LowPolyUI.Plank(new Rect(76, 40, 1288, 115));
            LowPolyUI.Title(new Rect(145, 55, 1150, 70), "HALL OF RECORDS", heading, true);
            Text(150, 160, 1140, 34, speedBoard ? "Fastest escapes per level. Lower time ranks higher; only timed wins count." :
                "Highest scores per level. Only wins are ranked; faster time breaks ties.", small);
            if (LowPolyUI.Button(new Rect(150, 206, 200, 54), "SCORE", speedBoard ? tab : selectedTab)) speedBoard = false;
            if (LowPolyUI.Button(new Rect(364, 206, 200, 54), "SPEEDRUN", speedBoard ? selectedTab : tab)) speedBoard = true;
            for (int i = 1; i <= CampaignLevels.Count; i++)
                if (LowPolyUI.Button(new Rect(150, 280 + (i - 1) * 72, 414, 62), i.ToString("00") + "   " + CampaignLevels.Get(i).Name,
                    i == boardLevel ? selectedRow : row)) boardLevel = i;
            BoardTable(new Rect(600, 206, 690, 496), CampaignLevels.Get(boardLevel), scores.Find(boardLevel));
            Text(150, 714, 1140, 32, "Total best score  " + scores.TotalBestScore(CampaignLevels.Count).ToString("N0") + "      /      " + SpeedTotal(scores), small);
            if (Button(new Rect(150, 758, 250, 58), "Back", false)) showRecords = false;
            GUI.Label(new Rect(430, 760, 860, 54), "Local profile: " + scores.PlayerName + "  -  records are saved on this device only.", left);
        }
        private void BoardTable(Rect area, CampaignLevel level, LevelRecord record)
        {
            LowPolyUI.Inset(area);
            int size = heading.fontSize; heading.fontSize = 24;
            Text(area.x + 20, area.y + 12, area.width - 40, 46, level.Name.ToUpperInvariant(), heading);
            heading.fontSize = size;
            int best = record == null ? 0 : record.bestScore;
            float fastest = record == null ? 0 : record.bestTime;
            Text(area.x + 20, area.y + 58, area.width - 40, 30, level.MapName + "   /   Best " + Points(best) + "   /   Fastest " + Split(fastest), small);
            float x = area.x + 20, w = area.width - 40, top = area.y + 100;
            string first = speedBoard ? "TIME" : "SCORE", second = speedBoard ? "SCORE" : "TIME";
            GUI.Label(new Rect(x + 14, top, 70, 28), "RANK", header);
            GUI.Label(new Rect(x + 90, top, 150, 28), first, headerRight);
            GUI.Label(new Rect(x + 260, top, 150, 28), second, headerRight);
            GUI.Label(new Rect(x + 420, top, 90, 28), "ALERTS", headerRight);
            GUI.Label(new Rect(x + 530, top, 110, 28), "DATE", headerRight);
            System.Collections.Generic.List<ScoreEntry> entries = record == null ? null : speedBoard ? record.times : record.scores;
            for (int r = 0; r < LevelRecord.Keep; r++)
            {
                Rect line = new Rect(x, top + 34 + r * 66, w, 58);
                Fill(line, r == 0 ? new Color(LowPolyUI.Gold.r, LowPolyUI.Gold.g, LowPolyUI.Gold.b, .38f) : new Color(.24f, .21f, .16f, r % 2 == 1 ? .10f : .04f));
                GUI.Label(new Rect(line.x + 14, line.y, 70, line.height), "#" + (r + 1), cell);
                if (entries == null || r >= entries.Count)
                {
                    GUI.Label(new Rect(line.x + 90, line.y, 150, line.height), "-", cellRight);
                    continue;
                }
                ScoreEntry entry = entries[r];
                string score = entry.score.ToString("N0"), time = entry.Timed ? FormatSplit(entry.time) : "-";
                GUI.Label(new Rect(line.x + 90, line.y, 150, line.height), speedBoard ? time : score, cellRight);
                GUI.Label(new Rect(line.x + 260, line.y, 150, line.height), speedBoard ? score : time, cellRight);
                GUI.Label(new Rect(line.x + 420, line.y, 90, line.height), entry.date == ScoreEntry.LegacyDate ? "-" : entry.alerts.ToString(), cellRight);
                GUI.Label(new Rect(line.x + 530, line.y, 110, line.height), entry.date == ScoreEntry.LegacyDate ? "imported" : entry.date, cellRight);
            }
            if (entries == null || entries.Count == 0)
                Text(area.x + 20, area.y + area.height - 44, area.width - 40, 30, speedBoard ? "No timed escape yet. Win this level to set a time." : "No win recorded yet.", small);
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
            levelMenu.Open();
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
            Card(new Rect(370, 50, 700, 800));
            int size = heading.fontSize; heading.fontSize = 30;
            Text(408, 86, 624, 76, finalWin ? "CAMPAIGN COMPLETE" : game.Won ? "MISSION COMPLETE" : "CAUGHT", heading);
            heading.fontSize = size;
            Text(420, 162, 600, 36, "Level " + game.Level.Number + "  /  " + game.Level.Name, small);
            LowPolyUI.Inset(new Rect(430, 206, 580, 132));
            Text(450, 212, 540, 32, game.NewRecord ? "NEW LEVEL BEST" : "SCORE", small);
            Text(450, 242, 540, 86, game.ResultScore.ToString("N0"), number);
            LowPolyUI.Inset(new Rect(430, 350, 580, 150));
            Breakdown(372, "Objective", game.Won ? "" : game.Collected + " of " + game.RequiredTargetCount + " items", game.ObjectivePoints);
            Breakdown(412, "Stealth", game.AlertReports + (game.AlertReports == 1 ? " alert" : " alerts"), game.StealthPoints);
            Breakdown(452, "Time", game.Won ? FormatSplit(game.RecordedTime) : FormatSplit(game.Elapsed), game.TimePoints);
            Text(430, 510, 580, 32, "Best score " + Points(game.BestScore) + "   /   Fastest " + Split(game.BestTime), small);
            if (game.Won && game.NewTimeRecord)
            {
                Rect mark = new Rect(600, 548, 240, 34);
                Fill(mark, LowPolyUI.Gold);
                GUI.Label(mark, "NEW BEST TIME", badge);
            }
            string progress = finalWin ? "All " + CampaignLevels.Count + " contracts complete. Every level is replayable." :
                game.NewlyUnlocked ? "Level " + (game.Level.Number + 1) + " unlocked." :
                game.Won ? "Escape complete. Choose your next contract." : "Caught runs are not ranked. Try another route.";
            Text(430, 588, 580, 34, progress, small);
            bool next = game.Won && !finalWin;
            if (next && Button(new Rect(430, 630, 580, 62), "Next Level")) game.StartLevel(game.Level.Number + 1);
            if (Button(new Rect(430, next ? 704 : 640, 580, 56), "Replay Level", !next)) game.Restart();
            if (Button(new Rect(430, next ? 772 : 710, 580, 50), "Levels", false)) OpenLevels();
        }
        private void Breakdown(float y, string label, string detail, int points)
        {
            GUI.Label(new Rect(458, y, 160, 34), label, cell);
            GUI.Label(new Rect(610, y, 230, 34), detail, header);
            GUI.Label(new Rect(840, y, 146, 34), (points > 0 ? "+" : "") + points.ToString("N0"), cellRight);
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
