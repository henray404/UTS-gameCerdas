using System.Linq;
using UnityEditor;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class ChaseAtmosphereBuilder
    {
        private const string Root = "Assets/SneakHouse/";
        [MenuItem("Sneak House/Set up chase atmosphere")]
        public static void Apply() { ChaseCameraBuilder.Apply(); }

        private static GameObject Child(Transform parent, string name)
        {
            Transform found = parent.Cast<Transform>().FirstOrDefault(t => t.name == name);
            if (found != null) return found.gameObject;
            GameObject go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Add chase audio");
            go.transform.SetParent(parent, false); return go;
        }
        private static AudioClip Clip(string name)
        {
            string path = Root + "Audio/" + name + ".wav";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new System.IO.FileNotFoundException("Missing chase sound", path);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool ambience = clip.length > 15;
                settings.compressionFormat = ambience ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.loadType = ambience ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.quality = .7f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = !ambience;
                importer.defaultSampleSettings = settings; importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        private static AudioSource Source(Transform parent, string name, AudioClip clip, bool loop)
        {
            GameObject go = Child(parent, name);
            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(go);
            Undo.RecordObject(source, "Configure chase audio");
            source.clip = clip; source.loop = loop; source.playOnAwake = false;
            source.spatialBlend = 0; source.volume = 0; source.dopplerLevel = 0;
            EditorUtility.SetDirty(source); return source;
        }

        public static void Configure(Camera camera, GameSession game)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AudioClip heart = Clip("chase-heartbeat"), breath = Clip("chase-breath"),
                drone = Clip("chase-drone"), sting = Clip("chase-sting"), step = Clip("chase-footstep");
            AudioClip[] wood = new[] { Clip("Footsteps/wood-00"), Clip("Footsteps/wood-01"), Clip("Footsteps/wood-02") };
            AudioClip[] grass = new[] { Clip("Footsteps/grass-00"), Clip("Footsteps/grass-01"), Clip("Footsteps/grass-02") };
            RuntimeNavigation navigation = Object.FindFirstObjectByType<RuntimeNavigation>();
            if (navigation != null)
                foreach (string floor in new[] { "Courtyard ground", "Living floor", "Office floor", "Collection floor" })
                {
                    Transform ground = navigation.geometry.Find(floor);
                    if (ground == null) continue;
                    FootstepSurface surface = ground.GetComponent<FootstepSurface>();
                    if (surface == null) surface = Undo.AddComponent<FootstepSurface>(ground.gameObject);
                    Undo.RecordObject(surface, "Assign floor sounds");
                    surface.clips = floor == "Courtyard ground" ? grass : wood; EditorUtility.SetDirty(surface);
                }
            FootstepAudio footsteps = game.player.GetComponent<FootstepAudio>();
            if (footsteps == null) footsteps = Undo.AddComponent<FootstepAudio>(game.player.gameObject);
            Undo.RecordObject(footsteps, "Assign player footsteps");
            footsteps.steps = Source(game.player.transform, "AUDIO / Player steps", wood[0], false);
            EditorUtility.SetDirty(footsteps);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shaders/ChaseDread.shader");
            if (shader == null) throw new System.IO.FileNotFoundException("Missing chase shader");
            string materialPath = Root + "Materials/ChaseDread.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            ChaseAtmosphere atmosphere = camera.GetComponent<ChaseAtmosphere>();
            if (atmosphere == null) atmosphere = Undo.AddComponent<ChaseAtmosphere>(camera.gameObject);
            Undo.RecordObject(atmosphere, "Configure chase atmosphere");
            Transform sounds = Child(camera.transform, "CHASE / Sound layers").transform;
            atmosphere.heartbeat = Source(sounds, "Heartbeat", heart, true);
            atmosphere.breathing = Source(sounds, "Breathing", breath, true);
            atmosphere.drone = Source(sounds, "Dread drone", drone, true);
            atmosphere.sting = Source(sounds, "Chase accent", sting, false);
            atmosphere.screenMaterial = material; EditorUtility.SetDirty(atmosphere);
            foreach (GuardBrain guard in game.guards)
            {
                GuardPursuitAudio pursuit = guard.GetComponent<GuardPursuitAudio>();
                if (pursuit == null) pursuit = Undo.AddComponent<GuardPursuitAudio>(guard.gameObject);
                pursuit.guard = guard; pursuit.atmosphere = atmosphere;
                pursuit.strideDistance = .704f;
                pursuit.steps = Source(guard.transform, "AUDIO / Pursuit steps", step, false);
                pursuit.steps.transform.localPosition = Vector3.up * .15f;
                pursuit.steps.spatialBlend = 1; pursuit.steps.rolloffMode = AudioRolloffMode.Linear;
                pursuit.steps.minDistance = 1; pursuit.steps.maxDistance = 14; pursuit.steps.spread = 20;
                AudioLowPassFilter filter = pursuit.steps.GetComponent<AudioLowPassFilter>();
                if (filter == null) filter = Undo.AddComponent<AudioLowPassFilter>(pursuit.steps.gameObject);
                filter.cutoffFrequency = 4500; filter.lowpassResonanceQ = 1;
                pursuit.muffling = filter; EditorUtility.SetDirty(pursuit.steps); EditorUtility.SetDirty(pursuit);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
