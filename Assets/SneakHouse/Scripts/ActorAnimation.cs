using UnityEngine;

namespace SneakHouse
{
    public class ActorAnimation : MonoBehaviour
    {
        public Animator animator;
        [Min(.1f)] public float walkReferenceSpeed = 1.4f, runReferenceSpeed = 3.2f;
        public float PlaybackRate { get; private set; } = 1;
        public string CurrentState => current;
        private string current;
        public void Set(string state, float speed = 1)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.speed = speed;
            PlaybackRate = speed;
            if (current == state) return;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return;
            animator.CrossFadeInFixedTime(hash, .16f); current = state;
        }
        public void SetLocomotion(float actualSpeed, bool running, string stoppedState = "Idle")
        {
            if (actualSpeed < .08f) { Set(stoppedState); return; }
            float reference = running ? runReferenceSpeed : walkReferenceSpeed;
            Set(running ? "Run" : "Walk", Mathf.Clamp(actualSpeed / Mathf.Max(.1f, reference), .08f, 4));
        }
    }
}
