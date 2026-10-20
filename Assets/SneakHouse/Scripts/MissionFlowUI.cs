using UnityEngine;

namespace SneakHouse
{
    [RequireComponent(typeof(GameSession))]
    public class MissionFlowUI : MonoBehaviour
    {
        private GameSession game;
        private bool showGuide;
        private bool showCredits;
        private GUIStyle heading, body, small, number, action, secondary;
        private readonly Color ink = LowPolyUI.Ink;
        private readonly Color muted = LowPolyUI.Muted;

        private void Awake() { game = GetComponent<GameSession>(); }
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
        }
        private static void Fill(Rect rect, Color colour)
        {
            Color previous = GUI.color; GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        private void Card(Rect rect)
        {
            LowPolyUI.Board(rect);
        }
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
                case GameSession.ScreenState.Briefing: Briefing(); break;
                case GameSession.ScreenState.Results: Results(); break;
            }
            GUI.matrix = previous;
        }
        private void MainMenu()
        {
            if (showCredits)
            {
                Card(new Rect(330, 130, 780, 640));
                Text(380, 166, 680, 80, "MUSIC CREDITS", heading);
                Text(380, 276, 680, 132, "Sneaky Snitch\nKevin MacLeod (incompetech.com)\nCC BY 4.0\ncreativecommons.org/licenses/by/4.0/", small);
                Text(380, 440, 680, 114, "Pursuit / Vicegrip of Pursuit\nSudocolon - OpenGameArt.org\nCC0 / Public domain", small);
                Text(380, 566, 680, 52, "Playback edits and full asset credits: Audio-Credits.txt", small);
                if (Button(new Rect(490, 646, 460, 72), "Back", false)) showCredits = false;
                return;
            }
            if (showGuide)
            {
                Card(new Rect(400, 100, 640, 700));
                Text(442, 166, 556, 80, "HOW TO PLAY", heading);
                Text(460, 264, 520, 330, "WASD / Arrow   -   Move\nShift   -   Sprint (stamina)\nCtrl / C   -   Sneak\nE   -   Collect\nQ   -   Drop can\nF   -   Hide / leave\nEsc   -   Pause\nF1   -   AI Debug", body);
                Text(450, 598, 540, 36, "Chased: first person / Mouse: look", small);
                if (Button(new Rect(490, 640, 460, 52), "Music Credits", false)) showCredits = true;
                if (Button(new Rect(490, 710, 460, 60), "Back", false)) showGuide = false;
                return;
            }
            Card(new Rect(460, 170, 520, 560));
            LowPolyUI.Plank(new Rect(424, 132, 592, 180));
            int logoSize = heading.fontSize; heading.fontSize = 48;
            LowPolyUI.Title(new Rect(454, 142, 532, 160), "SNEAK\nHOUSE", heading, true);
            heading.fontSize = logoSize;
            if (Button(new Rect(510, 370, 420, 76), "Start Mission")) game.NewContract();
            if (Button(new Rect(510, 474, 420, 68), "How to Play", false)) showGuide = true;
            if (Button(new Rect(510, 570, 420, 68), "Quit", false)) Quit();
            LowPolyUI.Ornament(new Rect(407, 239, 133, 133), 0, -14);
            LowPolyUI.Ornament(new Rect(912, 242, 138, 138), 1, 12);
            LowPolyUI.Ornament(new Rect(463, 651, 98, 98), 2, -18);
            LowPolyUI.Ornament(new Rect(892, 649, 116, 116), 3, 11);
        }
        private void Briefing()
        {
            Card(new Rect(360, 125, 720, 650));
            int size = heading.fontSize; heading.fontSize = 26;
            HouseShell shell = game.GetComponent<HouseShell>();
            Text(398, 166, 644, 80, shell == null ? "COLLECTOR'S HOUSE" : shell.Label.ToUpperInvariant(), heading);
            heading.fontSize = size;
            Text(410, 266, 620, 70, "Collect all 3 items. Return to the entrance.", body);
            for (int i = 0; i < game.targets.Length; i++)
            {
                float x = 420 + i * 200;
                LowPolyUI.Portrait(new Rect(x + 48, 370, 104, 104), i);
                Text(x, 490, 200, 46, game.targets[i].Label, small);
            }
            Text(410, 560, 620, 35, game.ActiveGuards + (game.ActiveGuards == 1 ? " guard on patrol" : " guards on patrol"), small);
            GuardEncounter encounter = game.GetComponent<GuardEncounter>();
            if (encounter != null) Text(410, 600, 620, 32, encounter.RosterLabel(), small);
            if (Button(new Rect(410, 650, 300, 74), "Enter House")) game.BeginMission();
            if (Button(new Rect(730, 650, 300, 74), "Back", false)) game.ShowMainMenu();
        }
        private void Results()
        {
            Card(new Rect(420, 110, 600, 680));
            int size = heading.fontSize; heading.fontSize = 28;
            Text(458, 154, 524, 90, game.Won ? "MISSION COMPLETE" : "CAUGHT", heading);
            heading.fontSize = size;
            LowPolyUI.Inset(new Rect(478, 286, 484, 158));
            Text(490, 294, 460, 40, game.NewRecord ? "NEW BEST" : "SCORE", small);
            Text(490, 332, 460, 100, game.ResultScore.ToString("N0"), number);
            Text(478, 478, 484, 64, FormatTime(game.Elapsed) + "    /    " + game.Collected + " of " + game.targets.Length + " items", body);
            if (Button(new Rect(478, 586, 484, 74), "Try Again")) game.Restart();
            if (Button(new Rect(478, 686, 484, 64), "Main Menu", false)) game.ShowMainMenu();
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
