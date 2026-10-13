using UnityEngine;

namespace SneakHouse
{
    public class ActorAnimation : MonoBehaviour
    {
        public Animator animator;
        private string current;
        public void Set(string state, float speed = 1)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.speed = speed;
            if (current == state) return;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return;
            animator.CrossFadeInFixedTime(hash, .16f); current = state;
        }
    }
}
