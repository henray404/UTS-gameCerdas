using UnityEngine;

namespace SneakHouse
{
    [DisallowMultipleComponent]
    public class MissionMusic : MonoBehaviour
    {
        public AudioSource exploration, pursuit;
        public ChaseAtmosphere atmosphere;
        public bool musicEnabled = true;
        [Range(0, 1)] public float musicVolume = .65f;
        [Min(.1f)] public float crossfadeSeconds = .8f;
        [Min(0)] public float calmDelay = 4;
        public float ChaseBlend { get; private set; }
        private float threatHold;
        private bool audioPaused;

        private void Update() { Tick(Time.unscaledDeltaTime); }
        public void Tick(float delta)
        {
            GameSession game = GameSession.Instance;
            if (!musicEnabled || game == null || game.Phase == GameSession.ScreenState.Results)
            { StopMusic(); return; }
            if (game.Phase == GameSession.ScreenState.Paused) { PauseMusic(true); return; }
            PauseMusic(false);
            delta = Mathf.Max(0, delta);
            bool threat = false;
            if (game.Phase == GameSession.ScreenState.Playing && !game.player.IsHidden)
                foreach (GuardBrain guard in game.guards)
                    if (guard != null && guard.isActiveAndEnabled && guard.State == GuardBrain.Mode.Chase)
                    { threat = true; break; }
            if (threat) threatHold = calmDelay;
            else if (game.player.IsHidden || game.Phase != GameSession.ScreenState.Playing) threatHold = 0;
            else threatHold = Mathf.Max(0, threatHold - delta);
            ChaseBlend = Mathf.MoveTowards(ChaseBlend, threat || threatHold > 0 ? 1 : 0,
                delta / Mathf.Max(.1f, crossfadeSeconds));
            // Keep both transports running silently so a short chase cannot restart the song.
            float duck = atmosphere != null ? Mathf.Lerp(1, .65f, atmosphere.Intensity) : 1;
            Layer(exploration, (1 - ChaseBlend) * .22f * musicVolume);
            Layer(pursuit, ChaseBlend * .25f * musicVolume * duck);
        }
        private void Layer(AudioSource source, float volume)
        {
            if (source == null || source.clip == null) return;
            source.volume = Mathf.Clamp01(volume);
            if (!source.isPlaying) source.Play();
        }
        private void PauseMusic(bool pause)
        {
            if (audioPaused == pause) return;
            foreach (AudioSource source in new[] { exploration, pursuit })
                if (source != null) { if (pause) source.Pause(); else source.UnPause(); }
            audioPaused = pause;
        }
        private void StopMusic()
        {
            ChaseBlend = threatHold = 0; audioPaused = false;
            foreach (AudioSource source in new[] { exploration, pursuit })
                if (source != null) { source.Stop(); source.volume = 0; }
        }
        private void OnDisable() { StopMusic(); }
    }
}
