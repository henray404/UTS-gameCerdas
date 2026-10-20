using System;
using System.IO;
using System.Text.RegularExpressions;
using SneakHouse;
using UnityEditor;
using UnityEngine;

namespace SneakHouseEditor
{
    // Reimport only the six preview assets in the actual editor, without resetting game progress.
    [InitializeOnLoad]
    public static class MapPreviewImportRepair
    {
        private const string Folder = "Assets/SneakHouse/Resources/SneakHouseUI/Levels";
        private const string SessionKey = "SneakHouse.MapPreviewImportRepair.v1";
        private static bool running;
        private static double captureAfter;

        [Serializable] private sealed class Preview
        {
            public string path, guid, importer, mainAssetType;
            public bool fileExists, assetDatabaseLoaded, resourcesLoaded;
            public int width, height;
        }
        [Serializable] private sealed class Report
        {
            public string projectPath, utc, folderGuid, error;
            public bool success;
            public Preview[] before, after;
        }

        static MapPreviewImportRepair()
        {
            if (!Application.isBatchMode && !AssetDatabase.IsAssetImportWorkerProcess())
                EditorApplication.delayCall += RepairOnce;
        }

        private static void RepairOnce()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            Repair();
        }

        [MenuItem("Sneak House/Repair Map Previews")]
        public static void Repair()
        {
            if (running || AssetDatabase.IsAssetImportWorkerProcess()) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Repair;
                return;
            }
            running = true;
            var report = new Report
            {
                projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                utc = DateTime.UtcNow.ToString("o")
            };
            try
            {
                string metadata = File.ReadAllText(Folder + ".meta");
                Match match = Regex.Match(metadata, @"(?m)^guid: ([0-9a-fA-F]{32})\s*$");
                if (!match.Success) throw new InvalidOperationException("Preview folder metadata has no valid GUID.");
                report.folderGuid = match.Groups[1].Value;
                report.before = Inspect();
                Debug.Log("MAP PREVIEW REPAIR: actual project=" + report.projectPath +
                    "; before=" + CountLoaded(report.before) + "/" + CampaignLevels.Count);
                var force = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
                AssetDatabase.ImportAsset(Folder, force | ImportAssetOptions.ImportRecursive);
                for (int i = 1; i <= CampaignLevels.Count; i++)
                {
                    string path = AssetPath(i);
                    if (!File.Exists(path)) throw new FileNotFoundException("Preview PNG missing", path);
                    AssetDatabase.ImportAsset(path, force);
                }
                if (Application.isBatchMode) Finish(report);
                else EditorApplication.delayCall += () => Finish(report);
            }
            catch (Exception exception)
            {
                running = false;
                report.error = exception.Message;
                Debug.LogError("MAP PREVIEW REPAIR FAILED: " + exception.Message);
                SaveReport(report);
            }
        }

        private static Preview[] Inspect()
        {
            var result = new Preview[CampaignLevels.Count];
            for (int i = 1; i <= CampaignLevels.Count; i++)
            {
                string path = AssetPath(i);
                Texture2D texture = Resources.Load<Texture2D>("SneakHouseUI/Levels/level-" + i.ToString("00"));
                Texture2D direct = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                AssetImporter importer = AssetImporter.GetAtPath(path);
                Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                result[i - 1] = new Preview
                {
                    path = path, guid = AssetDatabase.AssetPathToGUID(path), fileExists = File.Exists(path),
                    importer = importer == null ? "none" : importer.GetType().Name,
                    mainAssetType = mainType == null ? "none" : mainType.FullName,
                    assetDatabaseLoaded = direct != null,
                    resourcesLoaded = texture != null,
                    width = texture == null ? 0 : texture.width, height = texture == null ? 0 : texture.height
                };
            }
            return result;
        }

        private static void Finish(Report report)
        {
            try
            {
                report.after = Inspect();
                report.success = CountLoaded(report.after) == CampaignLevels.Count;
                SaveReport(report);
                foreach (Preview preview in report.after)
                    Debug.Log("MAP PREVIEW IMPORT: " + preview.path + " importer=" + preview.importer +
                        " main=" + preview.mainAssetType + " resource=" + preview.resourcesLoaded);
                if (!report.success)
                {
                    Debug.LogError("MAP PREVIEW REPAIR: images still unavailable. See Logs/MapPreviewImportReport.json.");
                    return;
                }
                Debug.Log("MAP PREVIEW REPAIR PASS: all " + CampaignLevels.Count + " previews load in the actual project.");
                if (!Application.isBatchMode && EditorApplication.isPlaying)
                {
                    captureAfter = EditorApplication.timeSinceStartup + 1.5;
                    EditorApplication.update -= CaptureMenu;
                    EditorApplication.update += CaptureMenu;
                }
            }
            catch (Exception exception) { Debug.LogError("MAP PREVIEW REPAIR report failed: " + exception.Message); }
            finally { running = false; }
        }

        private static void CaptureMenu()
        {
            if (EditorApplication.timeSinceStartup < captureAfter) return;
            EditorApplication.update -= CaptureMenu;
            GameSession game = GameSession.Instance;
            if (game != null && game.Phase == GameSession.ScreenState.LevelSelect)
                ScreenCapture.CaptureScreenshot("Logs/MapPreviewRepair.png");
        }

        private static int CountLoaded(Preview[] previews)
        {
            int count = 0;
            foreach (Preview preview in previews) if (preview.resourcesLoaded) count++;
            return count;
        }
        private static string AssetPath(int level) => Folder + "/level-" + level.ToString("00") + ".png";
        private static void SaveReport(Report report)
        {
            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/MapPreviewImportReport.json", JsonUtility.ToJson(report, true));
            }
            catch (IOException exception) { Debug.LogWarning("Could not save preview import report: " + exception.Message); }
            catch (UnauthorizedAccessException exception) { Debug.LogWarning("Could not save preview import report: " + exception.Message); }
        }
    }
}
