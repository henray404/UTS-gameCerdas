using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class StaminaMusicBuilder
    {
        [MenuItem("Sneak House/Set up stamina and music")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            GameSession game = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if (game == null) throw new InvalidOperationException("Open PrototypeHouse first.");
            if (game.player.GetComponent<PlayerStamina>() == null) Undo.AddComponent<PlayerStamina>(game.player.gameObject);
            MissionMusic music = game.GetComponent<MissionMusic>();
            if (music == null) music = Undo.AddComponent<MissionMusic>(game.gameObject);
            Undo.RecordObject(music, "Assign mission soundtrack");
            music.exploration = Source(game.transform, "MUSIC / Sneaky exploration", "exploration");
            music.pursuit = Source(game.transform, "MUSIC / Tense pursuit", "pursuit");
            music.atmosphere = Camera.main.GetComponent<ChaseAtmosphere>();
            EditorUtility.SetDirty(music);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        private static AudioSource Source(Transform parent, string name, string filename)
        {
            string path = "Assets/SneakHouse/Audio/Music/" + filename + ".ogg";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing music: " + path);
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming; settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .75f; settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings; importer.forceToMono = false; importer.SaveAndReimport();
            Transform child = parent.Cast<Transform>().FirstOrDefault(t => t.name == name);
            if (child == null)
            {
                GameObject go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Add music source");
                go.transform.SetParent(parent, false); child = go.transform;
            }
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(child.gameObject);
            Undo.RecordObject(source, "Configure music source");
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); source.loop = true;
            source.playOnAwake = false; source.volume = 0; source.spatialBlend = 0; source.dopplerLevel = 0;
            EditorUtility.SetDirty(source); return source;
        }
    }
}
