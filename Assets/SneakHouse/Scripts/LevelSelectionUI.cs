using UnityEngine;

namespace SneakHouse
{
    // A single campaign strip keeps every contract and its guard count visible together.
    public sealed class LevelSelectionUI
    {
        private const float CardWidth = 202, CardGap = 12, CardLeft = 84, CardTop = 224;
        private readonly GameSession game;
        private readonly Texture2D[] previews = new Texture2D[CampaignLevels.Count];
        private readonly Texture2D[] symbols = new Texture2D[4];
        private int selectedLevel;
        private GUIStyle title, name, map, count, detail, hint, introduction, card, selectedCard, action, secondary;

        public LevelSelectionUI(GameSession session)
        {
            game = session; selectedLevel = game.Level.Number;
            for (int i = 0; i < previews.Length; i++)
            {
                string path = "SneakHouseUI/Levels/level-" + (i + 1).ToString("00");
                previews[i] = Resources.Load<Texture2D>(path);
                if (previews[i] == null) Debug.LogWarning("Missing campaign map preview: " + path);
            }
            for (int i = 0; i < symbols.Length; i++) symbols[i] = CreateSymbol(i);
        }

        public void Open()
        {
            selectedLevel = game.Progress.HighestUnlocked;
            game.ShowLevelSelect();
        }

        public void Dispose()
        {
            foreach (Texture2D symbol in symbols) if (symbol != null) Object.Destroy(symbol);
            // ButtonStyle creates private texture backgrounds; Resources map assets remain shared.
            foreach (GUIStyle style in new[] { card, selectedCard, action, secondary })
            {
                if (style == null) continue;
                foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused })
                    if (state.background != null) Object.Destroy(state.background);
            }
        }

        private GUIStyle Label(int size, Font font, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = size, alignment = alignment, wordWrap = true
            };
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused })
                state.textColor = LowPolyUI.Ink;
            return style;
        }

        private void Styles()
        {
            if (title != null) return;
            title = Label(36, game.titleFont, TextAnchor.MiddleCenter);
            name = Label(21, game.bodyFont, TextAnchor.MiddleCenter);
            map = Label(17, game.bodyFont, TextAnchor.MiddleCenter);
            count = Label(22, game.bodyFont, TextAnchor.MiddleLeft);
            detail = Label(22, game.bodyFont, TextAnchor.MiddleLeft);
            hint = Label(20, game.bodyFont, TextAnchor.MiddleLeft);
            introduction = Label(20, game.bodyFont, TextAnchor.MiddleCenter);
            card = LowPolyUI.ButtonStyle(game.bodyFont, 20, false);
            selectedCard = LowPolyUI.ButtonStyle(game.bodyFont, 20, true);
            action = LowPolyUI.ButtonStyle(game.bodyFont, 25, true);
            secondary = LowPolyUI.ButtonStyle(game.bodyFont, 23, false);
        }

        public void Draw()
        {
            Styles(); HandleNavigation();
            LowPolyUI.Board(new Rect(48, 70, 1344, 770));
            LowPolyUI.Plank(new Rect(24, 40, 1392, 115));
            LowPolyUI.Title(new Rect(80, 55, 1280, 70), "THE MISSION BOARD", title, true);
            GUI.Label(new Rect(84, 164, 1272, 36), "Choose your next escape. Complete each contract to open the next.",
                introduction);
            for (int i = 1; i <= CampaignLevels.Count; i++) DrawCard(CampaignLevels.Get(i));
            DrawDetail(CampaignLevels.Get(selectedLevel));
            DrawActions();
        }

        private void HandleNavigation()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown) return;
            if (current.keyCode != KeyCode.LeftArrow && current.keyCode != KeyCode.RightArrow) return;
            int direction = current.keyCode == KeyCode.RightArrow ? 1 : -1;
            selectedLevel = Mathf.Clamp(selectedLevel + direction, 1, CampaignLevels.Count);
            current.Use();
        }

        private int Status(int level) => game.Progress.IsCompleted(level) ? 2 : game.Progress.IsUnlocked(level) ? 0 : 1;

        private void DrawCard(CampaignLevel level)
        {
            bool selected = level.Number == selectedLevel;
            int status = Status(level.Number);
            float x = CardLeft + (level.Number - 1) * (CardWidth + CardGap);
            Rect rect = new Rect(x, selected ? CardTop - 10 : CardTop, CardWidth, selected ? 354 : 344);
            GUI.SetNextControlName("CampaignLevel" + level.Number);
            if (LowPolyUI.Button(rect, "", selected ? selectedCard : card)) selectedLevel = level.Number;
            Color ink = selected ? LowPolyUI.Cream : LowPolyUI.Ink;
            DrawText(new Rect(x + 18, rect.y + 16, 75, 42), level.Number.ToString("00"), count, ink);
            LowPolyUI.Inset(new Rect(x + 148, rect.y + 15, 38, 38));
            DrawSymbol(new Rect(x + 154, rect.y + 21, 26, 26), status,
                status == 1 ? LowPolyUI.Red : LowPolyUI.Green);
            DrawMap(new Rect(x + 12, rect.y + 66, 178, 134), level.Number, status == 1);
            DrawText(new Rect(x + 12, rect.y + 210, 178, 58), level.Name, name, ink);
            DrawSymbol(new Rect(x + 22, rect.y + 274, 25, 25), 3, ink);
            DrawText(new Rect(x + 56, rect.y + 269, 134, 36),
                level.Guards + (level.Guards == 1 ? " GUARD" : " GUARDS"), count, ink);
            DrawText(new Rect(x + 12, rect.y + 308, 178, 30), level.MapName, map, ink);
        }

        private void DrawMap(Rect rect, int number, bool locked)
        {
            LowPolyUI.Fill(rect, new Color(.20f, .17f, .12f));
            Texture2D preview = previews[number - 1];
            Rect image = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
            if (preview == null)
            {
                DrawText(image, "Map preview unavailable", map, LowPolyUI.Cream);
                return;
            }
            Color previous = GUI.color;
            GUI.color = locked ? new Color(.78f, .78f, .78f) : Color.white;
            GUI.DrawTexture(image, preview, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }

        private void DrawDetail(CampaignLevel level)
        {
            LowPolyUI.Inset(new Rect(84, 596, 1272, 128));
            int status = Status(level.Number);
            DrawSymbol(new Rect(106, 618, 32, 32), status, status == 1 ? LowPolyUI.Red : LowPolyUI.Green);
            DrawText(new Rect(156, 606, 656, 38), "LEVEL " + level.Number + "  /  " + level.Name, detail, LowPolyUI.Ink);
            string floors = level.ShellIndex == 3 ? "2 floors" : "1 floor";
            DrawText(new Rect(156, 648, 656, 28), level.TargetCount + (level.TargetCount == 1 ? " item" : " items") +
                "   /   " + level.Cans + (level.Cans == 1 ? " can" : " cans") + "   /   " + floors, hint, LowPolyUI.Ink);
            DrawText(new Rect(156, 682, 656, 26), "Difficulty: " + level.DifficultyScore.ToString("0") +
                " / 100   |   Pace: " + MissionFlowUI.FormatTime(level.ReferenceSeconds) + " (bonus only)", map, LowPolyUI.Muted);
            DrawText(new Rect(838, 610, 490, 98), level.TacticalHint, hint, LowPolyUI.Ink);
        }

        private void DrawActions()
        {
            if (LowPolyUI.Button(new Rect(84, 750, 240, 58), "Main Menu", secondary)) game.ShowMainMenu();
            bool unlocked = game.Progress.IsUnlocked(selectedLevel);
            string guidance = unlocked ? "Collect every marked item and escape." :
                "Complete Level " + (selectedLevel - 1) + " to unlock this contract.";
            GUI.Label(new Rect(360, 750, 616, 58), guidance, hint);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && unlocked;
            if (LowPolyUI.Button(new Rect(1030, 746, 326, 64), "Play Level " + selectedLevel, action))
                game.StartLevel(selectedLevel);
            GUI.enabled = enabled;
        }

        private static void DrawText(Rect rect, string value, GUIStyle style, Color colour)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = colour;
            GUI.Label(rect, value, style);
            style.normal.textColor = previous;
        }

        private void DrawSymbol(Rect rect, int symbol, Color colour)
        {
            Color previous = GUI.color; GUI.color = colour;
            GUI.DrawTexture(rect, symbols[symbol]); GUI.color = previous;
        }

        private static Texture2D CreateSymbol(int symbol)
        {
            const int size = 48;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                bool filled;
                if (symbol == 0) filled = x >= 15 && x <= 35 - Mathf.Abs(y - 24) && y >= 10 && y <= 38;
                else if (symbol == 1)
                {
                    bool body = x >= 10 && x <= 38 && y >= 22 && y <= 42;
                    bool shackle = x >= 16 && x <= 32 && y >= 6 && y <= 24 &&
                        !(x >= 21 && x <= 27 && y >= 11);
                    filled = (body || shackle) && !(x >= 22 && x <= 26 && y >= 28 && y <= 36);
                }
                else if (symbol == 2)
                    filled = SegmentDistance(new Vector2(x, y), new Vector2(9, 25), new Vector2(20, 36)) < 4 ||
                        SegmentDistance(new Vector2(x, y), new Vector2(20, 36), new Vector2(40, 12)) < 4;
                else filled = (new Vector2(x - 24, y - 13).sqrMagnitude <= 49) ||
                    (y >= 24 && y <= 42 && x >= 10 + Mathf.Max(0, 29 - y) && x <= 38 - Mathf.Max(0, 29 - y));
                pixels[(size - 1 - y) * size + x] = filled ? Color.white : Color.clear;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Campaign symbol " + symbol, hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }

        private static float SegmentDistance(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float along = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return (point - start - segment * along).magnitude;
        }
    }
}
