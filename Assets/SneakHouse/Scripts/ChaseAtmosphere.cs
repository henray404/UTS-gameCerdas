using UnityEngine;

namespace SneakHouse
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera), typeof(ChaseCamera))]
    [DefaultExecutionOrder(-40)]
    public class ChaseAtmosphere : MonoBehaviour
    {
        [Header("Chase tension")]
        public bool horrorEnabled = true;
        [Range(0, 1)] public float effectIntensity = .9f;
        [Range(0, 1)] public float soundVolume = .85f;
        [Range(0, 1)] public float cameraShake = .7f;
        [Range(0, 1)] public float screenEffects = .9f;
        [Header("Sound layers")]
        public AudioSource heartbeat, breathing, drone, sting;
        public Material screenMaterial;

        public float Intensity { get; private set; }
        public float TargetIntensity { get; private set; }
        public float NearestChaserDistance { get; private set; } = float.PositiveInfinity;
        public bool BeingChased { get; private set; }
        public float VisualStrength => IsPlaying && horrorEnabled && chaseView != null ? Intensity * screenEffects * chaseView.ViewBlend : 0;
        public float ShakeDegrees => Quaternion.Angle(Quaternion.identity, lastTwist);
        public float HeartPulse { get; private set; }
        private bool IsPlaying => GameSession.Instance != null && GameSession.Instance.Phase == GameSession.ScreenState.Playing;
        private ChaseCamera chaseView;
        private Camera view;
        private Material screenInstance;
        private Vector3 lastShift;
        private Vector3 appliedPosition;
        private Quaternion lastTwist = Quaternion.identity;
        private bool shifted, lensChanged, audioPaused, previousChase;
        private float baseFov, appliedFov, clock, accentSince = -100, nextSting;

        private void Awake()
        {
            view = GetComponent<Camera>(); chaseView = GetComponent<ChaseCamera>();
            if (screenMaterial != null) screenInstance = new Material(screenMaterial);
        }

        private void Update() { RemoveCameraMotion(); }
        private void LateUpdate()
        {
            Tick(Time.deltaTime);
            ApplyCameraMotion();
        }

        public void Tick(float delta)
        {
            GameSession game = GameSession.Instance;
            if (!horrorEnabled || effectIntensity <= 0 || game == null)
            { ResetEffects(); return; }
            if (game.Phase == GameSession.ScreenState.Paused)
            { PauseAudio(true); return; }
            if (game.Phase != GameSession.ScreenState.Playing || chaseView == null || !chaseView.isActiveAndEnabled)
            { ResetEffects(); return; }
            PauseAudio(false);
            delta = Mathf.Max(0, delta); clock += delta;
            BeingChased = false; NearestChaserDistance = float.PositiveInfinity;
            GuardBrain nearest = null;
            foreach (GuardBrain guard in game.guards)
            {
                if (guard == null || !guard.isActiveAndEnabled || guard.State != GuardBrain.Mode.Chase) continue;
                float distance = Vector3.Distance(game.player.transform.position, guard.transform.position);
                BeingChased = true;
                if (distance < NearestChaserDistance) { NearestChaserDistance = distance; nearest = guard; }
            }
            float danger = 0;
            if (BeingChased)
            {
                danger = Mathf.Lerp(.46f, 1, 1 - Mathf.InverseLerp(1.1f, 9, NearestChaserDistance));
                Vector3 toward = nearest.transform.position - game.player.transform.position; toward.y = 0;
                if (Vector3.Dot(chaseView.PlanarForward, toward.normalized) < -.3f) danger = Mathf.Min(1, danger + .06f);
            }
            else if (chaseView.IsFirstPerson) danger = .22f;
            if (game.player.IsHidden) danger = game.player.Hiding.Compromised ? .3f : 0;
            TargetIntensity = danger * effectIntensity;
            Intensity = Mathf.MoveTowards(Intensity, TargetIntensity, delta * (TargetIntensity > Intensity ? 1.8f : .32f));

            if (BeingChased && !previousChase && clock >= nextSting)
            {
                if (sting != null && sting.clip != null) { sting.volume = soundVolume * .52f; sting.Play(); }
                accentSince = clock; nextSting = clock + 10;
            }
            previousChase = BeingChased;
            UpdateAudio();
            float phase = heartbeat != null && heartbeat.clip != null ? heartbeat.time / heartbeat.clip.length : 0;
            HeartPulse = Mathf.Exp(-Mathf.Pow((phase - .04f) * 22, 2)) + .65f * Mathf.Exp(-Mathf.Pow((phase - .28f) * 24, 2));
        }

        private void Loop(AudioSource source, float volume, float pitch)
        {
            if (source == null || source.clip == null) return;
            source.volume = volume * soundVolume; source.pitch = pitch;
            if (Intensity > .008f) { if (!source.isPlaying && !audioPaused) source.Play(); }
            else if (source.isPlaying) source.Stop();
        }
        private void UpdateAudio()
        {
            Loop(heartbeat, Intensity * .62f, Mathf.Lerp(.95f, 1.9f, Intensity));
            Loop(breathing, Intensity * .4f, Mathf.Lerp(.9f, 1.3f, Intensity));
            Loop(drone, Intensity * .42f, .98f + Intensity * .025f);
        }
        private AudioSource[] Sources => new[] { heartbeat, breathing, drone, sting };
        private void PauseAudio(bool paused)
        {
            if (audioPaused == paused) return;
            foreach (AudioSource source in Sources)
                if (source != null) { if (paused) source.Pause(); else source.UnPause(); }
            audioPaused = paused;
        }

        private void ApplyCameraMotion()
        {
            if (!IsPlaying || !horrorEnabled || chaseView == null || !chaseView.isActiveAndEnabled) return;
            float amount = Intensity * chaseView.ViewBlend * cameraShake;
            if (amount > .001f)
            {
                float x = (Mathf.PerlinNoise(clock * 11, 1.3f) - .5f) * 2;
                float y = (Mathf.PerlinNoise(5.7f, clock * 9) - .5f) * 2;
                float roll = (Mathf.PerlinNoise(clock * 5, 12.4f) - .5f) * 2;
                float accent = Mathf.Exp(-Mathf.Max(0, clock - accentSince) * 8) * Mathf.Sin((clock - accentSince) * 34);
                lastShift = (transform.right * x + transform.up * y) * (.007f * amount);
                lastTwist = Quaternion.Euler((x * .85f + accent * .8f) * amount,
                    y * .6f * amount, roll * .16f * amount);
                transform.position += lastShift; transform.rotation *= lastTwist;
                appliedPosition = transform.position; shifted = true;
            }
            if (chaseView.ViewBlend > .999f && !view.orthographic && cameraShake > 0)
            {
                baseFov = view.fieldOfView;
                view.fieldOfView = baseFov - Intensity * cameraShake * 2.6f + HeartPulse * .22f * Intensity * cameraShake;
                appliedFov = view.fieldOfView;
                lensChanged = true;
            }
        }
        private void RemoveCameraMotion()
        {
            if (shifted)
            {
                // The base camera may already have restored its pose when disabled.
                if (Vector3.Distance(transform.position, appliedPosition) < .0001f)
                { transform.position -= lastShift; transform.rotation *= Quaternion.Inverse(lastTwist); }
                shifted = false; lastShift = Vector3.zero; lastTwist = Quaternion.identity;
            }
            if (lensChanged && view != null)
            {
                if (Mathf.Abs(view.fieldOfView - appliedFov) < .001f) view.fieldOfView = baseFov;
                lensChanged = false;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (screenInstance == null || !screenInstance.shader.isSupported || VisualStrength < .001f)
            { Graphics.Blit(source, destination); return; }
            screenInstance.SetFloat("_Intensity", VisualStrength);
            screenInstance.SetFloat("_Pulse", HeartPulse);
            screenInstance.SetFloat("_Clock", clock);
            Graphics.Blit(source, destination, screenInstance);
        }

        private void ResetEffects()
        {
            RemoveCameraMotion(); Intensity = TargetIntensity = HeartPulse = 0;
            BeingChased = previousChase = audioPaused = false;
            NearestChaserDistance = float.PositiveInfinity;
            foreach (AudioSource source in Sources) if (source != null) source.Stop();
        }
        private void OnDisable() { ResetEffects(); }
        private void OnDestroy() { if (screenInstance != null) Destroy(screenInstance); }
    }
}
