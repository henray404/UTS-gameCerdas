using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SneakHouseEditor
{
    public static class UIArtBuilder
    {
        private static Texture2D TransparentPreview(PreviewRenderUtility preview, int size)
        {
            preview.BeginStaticPreview(new Rect(0, 0, size, size));
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.clear;
            preview.Render(true, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture output = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            bool srgb = GL.sRGBWrite;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(preview.camera.targetTexture, output);
            GL.sRGBWrite = srgb;
            RenderTexture.active = output;
            Texture2D image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, size, size), 0, 0); image.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(output);
            // Unity's thumbnail exporter discards alpha; read RGBA before it finishes the preview.
            Texture2D thumbnail = preview.EndStaticPreview(); Object.DestroyImmediate(thumbnail);
            return image;
        }
        [MenuItem("Sneak House/Apply pixel fonts")]
        public static void ApplyPixelFonts()
        {
            string root = "Assets/SneakHouse/Fonts/";
            string[] files = { "PressStart2P-Regular.ttf", "PixelifySans-Variable.ttf" };
            foreach (string file in files)
            {
                TrueTypeFontImporter importer = AssetImporter.GetAtPath(root + file) as TrueTypeFontImporter;
                if (importer == null) throw new FileNotFoundException("Missing pixel font", file);
                importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                importer.SaveAndReimport();
            }
            SneakHouse.GameSession session = Object.FindFirstObjectByType<SneakHouse.GameSession>();
            if (session == null) throw new System.InvalidOperationException("Open the prototype scene first.");
            Undo.RecordObject(session, "Apply pixel UI fonts");
            session.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(root + files[1]);
            session.titleFont = AssetDatabase.LoadAssetAtPath<Font>(root + files[0]);
            EditorUtility.SetDirty(session);
            EditorSceneManager.SaveScene(session.gameObject.scene);
        }

        [MenuItem("Sneak House/Render cast portraits")]
        public static void RenderCastPortraits()
        {
            string folder = "Assets/SneakHouse/Resources/SneakHouseUI";
            Directory.CreateDirectory(folder);
            string[] models = { "03_judy_guard", "07_carl_guard", "10_baymax_guard", "06_incredible_guard" };
            string[] names = { "judy", "carl", "baymax", "incredible" };
            for (int i = 0; i < models.Length; i++)
            {
                PreviewRenderUtility preview = new PreviewRenderUtility();
                Texture2D image = null;
                try
                {
                    string path = "Assets/SneakHouse/Models/PortraitCast/" + models[i] + ".fbx";
                    GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (source == null) throw new FileNotFoundException("Missing existing character model", path);
                    GameObject model = Object.Instantiate(source);
                    preview.AddSingleGO(model);
                    AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                        .FirstOrDefault(c => c.name.EndsWith("Idle") || c.name.Contains("|Idle"));
                    if (idle != null) idle.SampleAnimation(model, .5f);
                    Transform eye = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("face_eye"));
                    Transform head = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "head");
                    if (eye != null && head != null)
                    {
                        Vector3 facing = eye.position - head.position; facing.y = 0;
                        if (facing.sqrMagnitude > .0001f) model.transform.Rotate(0, Vector3.SignedAngle(facing, Vector3.forward, Vector3.up), 0);
                    }
                    Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    float radius = bounds.size.y * .39f;
                    Vector3 focus = bounds.center + Vector3.up * bounds.size.y * .15f;
                    Camera camera = preview.camera;
                    camera.orthographic = true; camera.orthographicSize = radius;
                    camera.nearClipPlane = .001f; camera.farClipPlane = radius * 20 + 5;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                    camera.transform.position = focus + new Vector3(.55f, .12f, 3).normalized * (radius * 5 + 1);
                    camera.transform.LookAt(focus);
                    preview.lights[0].intensity = 1.4f; preview.lights[0].color = new Color(1, .92f, .79f);
                    preview.lights[0].transform.rotation = Quaternion.Euler(35, 25, 0);
                    preview.lights[1].intensity = .65f; preview.lights[1].transform.rotation = Quaternion.Euler(345, 190, 0);
                    preview.ambientColor = new Color(.62f, .60f, .53f);
                    image = TransparentPreview(preview, 320);
                    File.WriteAllBytes(folder + "/cast-" + names[i] + ".png", image.EncodeToPNG());
                }
                finally { if (image != null) Object.DestroyImmediate(image); preview.Cleanup(); }
            }
            AssetDatabase.Refresh();
            foreach (string name in names)
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/cast-" + name + ".png");
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Debug.Log("SNEAK_HOUSE_CAST_PORTRAITS_READY: 4 existing character models");
        }

        [MenuItem("Sneak House/Render UI portraits")]
        public static void RenderPortraits()
        {
            string folder = "Assets/SneakHouse/Resources/SneakHouseUI";
            Directory.CreateDirectory(folder);
            string[] models = { "confidential_drive", "premium_watch", "nextgen_console" };
            string[] names = { "drive", "watch", "console" };
            for (int i = 0; i < models.Length; i++)
            {
                PreviewRenderUtility preview = new PreviewRenderUtility();
                Texture2D image = null;
                try
                {
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SneakHouse/Models/Loot/" + models[i] + ".fbx");
                    if (asset == null) throw new FileNotFoundException("Missing UI portrait model", models[i]);
                    GameObject model = Object.Instantiate(asset);
                    preview.AddSingleGO(model);
                    Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    float radius = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * .70f;
                    Camera camera = preview.camera;
                    camera.orthographic = true; camera.orthographicSize = radius;
                    camera.nearClipPlane = .001f; camera.farClipPlane = radius * 20 + 5;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                    camera.transform.position = bounds.center + new Vector3(1.5f, 1.5f, 2.1f).normalized * (radius * 5 + 1);
                    camera.transform.LookAt(bounds.center);
                    preview.lights[0].intensity = 1.35f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(40, 25, 0);
                    preview.lights[1].intensity = .65f;
                    preview.lights[1].transform.rotation = Quaternion.Euler(340, 190, 0);
                    preview.ambientColor = new Color(.6f, .6f, .5f);
                    image = TransparentPreview(preview, 192);
                    File.WriteAllBytes(folder + "/" + names[i] + ".png", image.EncodeToPNG());
                }
                finally
                {
                    if (image != null) Object.DestroyImmediate(image);
                    preview.Cleanup();
                }
            }
            AssetDatabase.Refresh();
            foreach (string name in names)
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/" + name + ".png");
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Debug.Log("SNEAK_HOUSE_UI_PORTRAITS_READY: 3 FBX portraits");
        }
    }
}
