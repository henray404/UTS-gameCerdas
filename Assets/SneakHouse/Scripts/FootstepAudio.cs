using UnityEngine;

namespace SneakHouse
{
    [DisallowMultipleComponent]
    public class FootstepAudio : MonoBehaviour
    {
        public AudioSource steps;
        public int StepCount { get; private set; }

        public void PlayStep(bool running, bool sneaking)
        {
            GameSession game = GameSession.Instance;
            if (game == null || game.Phase != GameSession.ScreenState.Playing || game.player.IsHidden ||
                steps == null || steps.clip == null) return;
            AudioClip clip = FootstepSurface.ClipAt(transform.position, StepCount++, steps.clip);
            steps.volume = sneaking ? .07f : running ? .3f : .19f;
            steps.pitch = (running ? 1.03f : .97f) + Mathf.Sin(StepCount * 1.7f) * .035f;
            // Audible player feedback is separate from the hearing evidence emitted by PlayerMotor.
            steps.PlayOneShot(clip);
        }

        private void Update()
        {
            GameSession game = GameSession.Instance;
            if (steps != null && (game == null || game.Phase != GameSession.ScreenState.Playing || game.player.IsHidden)) steps.Stop();
        }
        private void OnDisable() { if (steps != null) steps.Stop(); }
    }
}
