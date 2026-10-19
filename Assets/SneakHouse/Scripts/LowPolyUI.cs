using UnityEngine;

namespace SneakHouse
{
    // Small, cached raster shapes; all screens share the same materials and cut corners.
    public static class LowPolyUI
    {
        public static readonly Color Cream = Hex("F3E5BF"), Ink = Hex("3E3528"), Muted = Hex("5D6045");
        public static readonly Color Green = Hex("405832"), Gold = Hex("C49A4D"), Red = Hex("994C36");
        private static GUIStyle board, shadow, plank, inset;
        private static Texture2D drive, watch, console;
        private static Texture2D[] ornaments;
        private static bool portraitsLoaded;

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color colour); return colour; }
        private static void Paint(Color[] pixels, int size, Color colour, params Vector2[] points)
        {
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    Vector2 a = points[i], b = points[j];
                    if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                }
                if (inside) pixels[(size - 1 - y) * size + x] = colour;
            }
        }
        private static void Cut(Color[] p, int s, int edge, int corner, Color colour)
        {
            Paint(p, s, colour, new Vector2(edge + corner, edge), new Vector2(s - edge - corner, edge),
                new Vector2(s - edge, edge + corner), new Vector2(s - edge, s - edge - corner),
                new Vector2(s - edge - corner, s - edge), new Vector2(edge + corner, s - edge),
                new Vector2(edge, s - edge - corner), new Vector2(edge, edge + corner));
        }
        private static Texture2D Texture(int kind, Color face, bool pressed = false)
        {
            const int s = 128;
            Color[] p = new Color[s * s];
            if (kind == 3) Cut(p, s, 0, 10, new Color(.10f, .08f, .04f, .28f));
            else
            {
                Cut(p, s, 0, 10, Hex("493324"));
                Cut(p, s, 2, 9, kind == 0 ? Hex("9D6A3D") : Color.Lerp(face, Color.black, .40f));
                Paint(p, s, kind == 0 ? Hex("C39358") : Color.Lerp(face, Color.white, .26f),
                    new Vector2(11, 2), new Vector2(117, 2), new Vector2(126, 11), new Vector2(115, 18), new Vector2(13, 18), new Vector2(2, 11));
                Paint(p, s, kind == 0 ? Hex("70492F") : Color.Lerp(face, Color.black, .25f),
                    new Vector2(126, 11), new Vector2(126, 117), new Vector2(117, 126), new Vector2(110, 114), new Vector2(111, 16));
                int edge = kind == 0 ? 15 : 7;
                Cut(p, s, edge, kind == 0 ? 4 : 7, kind == 0 ? Hex("CFBA8B") : face);
                Cut(p, s, edge + 2, 4, kind == 0 ? Hex("E8D7AA") : face);
                if (kind == 0)
                {
                    Paint(p, s, Hex("E0CD9F"), new Vector2(17, 80), new Vector2(73, 111), new Vector2(17, 111));
                    Paint(p, s, Hex("EDDEB8"), new Vector2(75, 17), new Vector2(111, 17), new Vector2(111, 54));
                    foreach (Vector2 bolt in new[] { new Vector2(9, 9), new Vector2(119, 9), new Vector2(9, 119), new Vector2(119, 119) })
                    {
                        Paint(p, s, Hex("67462D"), bolt + new Vector2(-3, -2), bolt + new Vector2(2, -3), bolt + new Vector2(3, 2), bolt + new Vector2(-2, 3));
                        Paint(p, s, Hex("CEA45F"), bolt + new Vector2(-2, -2), bolt + new Vector2(1, -2), bolt + new Vector2(1, 0), bolt + new Vector2(-2, 0));
                    }
                }
                else
                {
                    Paint(p, s, Color.Lerp(face, Color.white, .12f), new Vector2(12, 8), new Vector2(116, 8), new Vector2(121, 14), new Vector2(7, 14));
                    Paint(p, s, Color.Lerp(face, Color.black, .14f), new Vector2(7, 93), new Vector2(121, 111), new Vector2(115, 117), new Vector2(13, 117));
                    if (pressed) Cut(p, s, 7, 7, Color.Lerp(face, Color.black, .13f));
                }
            }
            Texture2D texture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            texture.name = "Sneak House / faceted UI"; texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels(p); texture.Apply(); texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        private static GUIStyle Surface(Texture2D texture, int border)
        {
            return new GUIStyle { normal = { background = texture }, border = new RectOffset(border, border, border, border) };
        }
        private static void Initialize()
        {
            if (board != null) return;
            board = Surface(Texture(0, Cream), 23);
            shadow = Surface(Texture(3, Cream), 20);
            plank = Surface(Texture(1, Hex("775033")), 18);
            inset = Surface(Texture(1, Hex("D8C69A")), 15);
        }
        public static void Fill(Rect rect, Color colour)
        {
            Color previous = GUI.color; GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        private static void Draw(GUIStyle style, Rect rect) { if (Event.current.type == EventType.Repaint) style.Draw(rect, GUIContent.none, false, false, false, false); }
        public static void Board(Rect rect)
        {
            Initialize(); Draw(shadow, new Rect(rect.x + 5, rect.y + 9, rect.width, rect.height)); Draw(board, rect);
        }
        public static void Plank(Rect rect) { Initialize(); Draw(shadow, new Rect(rect.x + 3, rect.y + 6, rect.width, rect.height)); Draw(plank, rect); }
        public static void Inset(Rect rect) { Initialize(); Draw(inset, rect); }
        public static GUIStyle ButtonStyle(Font font, int size, bool primary)
        {
            Color face = primary ? Green : Hex("D1AF73");
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                font = font, fontSize = size, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(18, 18, 18, 18), padding = new RectOffset(20, 20, 10, 14)
            };
            style.normal.background = Texture(1, face);
            style.hover.background = Texture(1, Color.Lerp(face, Color.white, .09f));
            style.active.background = Texture(1, face, true);
            style.focused.background = Texture(1, Color.Lerp(face, Gold, .24f));
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused }) state.textColor = primary ? Cream : Ink;
            return style;
        }
        public static bool Button(Rect rect, string label, GUIStyle style)
        {
            Initialize(); Draw(shadow, new Rect(rect.x + 2, rect.y + 5, rect.width, rect.height));
            return GUI.Button(rect, label, style);
        }
        public static void Title(Rect rect, string label, GUIStyle style, bool light = false)
        {
            Color saved = style.normal.textColor;
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused }) state.textColor = Hex("5A3D28");
            GUI.Label(new Rect(rect.x + 2, rect.y + 4, rect.width, rect.height), label, style);
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused }) state.textColor = light ? Cream : Green;
            GUI.Label(rect, label, style);
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused }) state.textColor = saved;
        }
        public static Texture2D LootPortrait(int index)
        {
            if (!portraitsLoaded)
            {
                drive = Resources.Load<Texture2D>("SneakHouseUI/drive"); watch = Resources.Load<Texture2D>("SneakHouseUI/watch");
                console = Resources.Load<Texture2D>("SneakHouseUI/console"); portraitsLoaded = true;
            }
            return index == 0 ? drive : index == 1 ? watch : console;
        }
        public static void Portrait(Rect rect, int index)
        {
            Inset(rect); Texture2D texture = LootPortrait(index);
            if (texture != null) GUI.DrawTexture(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, rect.height - 10), texture, ScaleMode.ScaleToFit, true);
        }
        private static void Box(Color[] p, Color c, float x, float y, float w, float h)
        {
            Paint(p, 128, c, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));
        }
        private static void Disc(Color[] p, Color c, Vector2 center, float radius)
        {
            Vector2[] points = new Vector2[12];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / points.Length;
                points[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            Paint(p, 128, c, points);
        }
        private static Texture2D OrnamentTexture(int index)
        {
            Color[] p = new Color[128 * 128];
            Color outline = Hex("493324"), brass = Hex("C49A4D"), light = Hex("E8CD8C");
            if (index == 0)
            {
                // Faceted magnifier: opaque blue glass keeps it readable over the scene.
                Paint(p, 128, outline, new Vector2(65, 70), new Vector2(79, 60), new Vector2(119, 103), new Vector2(105, 119));
                Paint(p, 128, Hex("775033"), new Vector2(72, 74), new Vector2(80, 69), new Vector2(112, 104), new Vector2(106, 111));
                Disc(p, outline, new Vector2(48, 46), 36);
                Disc(p, brass, new Vector2(48, 44), 33);
                Disc(p, Hex("526D70"), new Vector2(48, 44), 26);
                Disc(p, Hex("91ADB0"), new Vector2(47, 41), 22);
                Paint(p, 128, Hex("DAE5D6"), new Vector2(29, 33), new Vector2(38, 24), new Vector2(59, 24), new Vector2(40, 43));
            }
            else if (index == 1)
            {
                Box(p, outline, 15, 25, 100, 91);
                Box(p, Hex("A77B45"), 18, 28, 94, 84);
                Box(p, brass, 21, 20, 39, 13);
                Box(p, outline, 28, 12, 71, 83);
                Box(p, Cream, 31, 15, 65, 77);
                Box(p, Muted, 42, 28, 31, 5);
                Box(p, Hex("B6A982"), 42, 42, 43, 3);
                Box(p, Hex("B6A982"), 42, 52, 34, 3);
                Box(p, Hex("B6A982"), 42, 62, 40, 3);
                Paint(p, 128, Hex("D1AF73"), new Vector2(18, 65), new Vector2(110, 65), new Vector2(105, 111), new Vector2(23, 111));
                Box(p, Red, 42, 78, 43, 18);
                Box(p, light, 49, 84, 29, 3);
            }
            else if (index == 2)
            {
                // Two shoe prints, staggered like a quiet approach.
                for (int i = 0; i < 2; i++)
                {
                    Vector2 o = new Vector2(i * 49, -i * 31);
                    Paint(p, 128, Green, o + new Vector2(18, 56), o + new Vector2(24, 43), o + new Vector2(38, 42),
                        o + new Vector2(45, 53), o + new Vector2(43, 82), o + new Vector2(19, 82));
                    Paint(p, 128, Green, o + new Vector2(20, 89), o + new Vector2(42, 89), o + new Vector2(40, 105), o + new Vector2(23, 108));
                    Box(p, Hex("687C4A"), 23 + o.x, 58 + o.y, 5, 18);
                }
            }
            else
            {
                Disc(p, outline, new Vector2(45, 39), 29);
                Disc(p, brass, new Vector2(45, 37), 26);
                Disc(p, Color.clear, new Vector2(45, 37), 17);
                Paint(p, 128, outline, new Vector2(49, 57), new Vector2(63, 49), new Vector2(104, 99), new Vector2(91, 111));
                Paint(p, 128, brass, new Vector2(54, 60), new Vector2(61, 56), new Vector2(99, 101), new Vector2(92, 105));
                Paint(p, 128, brass, new Vector2(78, 80), new Vector2(94, 67), new Vector2(101, 76), new Vector2(86, 88));
                Paint(p, 128, brass, new Vector2(89, 94), new Vector2(105, 81), new Vector2(112, 90), new Vector2(98, 103));
                Paint(p, 128, light, new Vector2(24, 24), new Vector2(36, 14), new Vector2(53, 13), new Vector2(48, 19), new Vector2(32, 22));
            }
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            texture.name = "Sneak House / stealth ornament"; texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels(p); texture.Apply(); texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        public static void Ornament(Rect rect, int index, float angle)
        {
            if (ornaments == null) ornaments = new[] { OrnamentTexture(0), OrnamentTexture(1), OrnamentTexture(2), OrnamentTexture(3) };
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            Color previousColour = GUI.color;
            GUI.color = new Color(0.15f, 0.12f, 0.07f, .3f);
            GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 7, rect.width, rect.height), ornaments[index]);
            GUI.color = previousColour;
            GUI.DrawTexture(rect, ornaments[index]);
            GUI.matrix = previous;
        }
    }
}
