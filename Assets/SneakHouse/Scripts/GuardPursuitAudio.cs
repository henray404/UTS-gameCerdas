using UnityEngine;

namespace SneakHouse
{
    public class GuardPursuitAudio : MonoBehaviour
    {
        public GuardBrain guard;
        public AudioSource steps;
        public AudioLowPassFilter muffling;
        public ChaseAtmosphere atmosphere;
        [Min(.2f)] public float strideDistance = .704f;
        public int StepCount { get; private set; }
        private float distance;

        private void Update()
        {
            GameSession game = GameSession.Instance;
            bool active = game != null && game.Phase == GameSession.ScreenState.Playing &&
                atmosphere != null && atmosphere.isActiveAndEnabled && atmosphere.horrorEnabled &&
                guard != null && guard.isActiveAndEnabled && guard.State == GuardBrain.Mode.Chase;
            if (!active) { distance = 0; if (steps != null) steps.Stop(); return; }
            if (steps == null || steps.clip == null || !guard.Agent.isOnNavMesh) return;
            float speed = guard.Agent.velocity.magnitude;
            if (speed < .15f) { distance = 0; return; }
            if (muffling != null)
            {
                Camera view = Camera.main;
                Vector3 toward = guard.transform.position - view.transform.position; toward.y = 0;
                muffling.cutoffFrequency = Vector3.Dot(view.transform.forward, toward.normalized) < 0 ? 1800 : 4500;
            }
            distance += speed * Time.deltaTime;
            if (distance < strideDistance) return;
            distance %= strideDistance; StepCount++;
            steps.volume = .45f * atmosphere.soundVolume * atmosphere.effectIntensity;
            steps.pitch = .92f + Mathf.Sin(StepCount * 1.7f) * .07f;
            steps.PlayOneShot(FootstepSurface.ClipAt(guard.transform.position, StepCount, steps.clip));
        }
        private void OnDisable() { distance = 0; if (steps != null) steps.Stop(); }
    }
}
