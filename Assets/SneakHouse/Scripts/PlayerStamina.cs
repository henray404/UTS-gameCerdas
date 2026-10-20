using UnityEngine;

namespace SneakHouse
{
    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        [Min(1)] public float capacity = 120;
        [Min(0)] public float sprintDrain = 18;
        [Min(0)] public float recoveryRate = 22;
        [Min(0)] public float recoveryDelay = 1;
        [Range(.05f, 1)] public float resumeThreshold = .3f;
        public float Current { get; private set; }
        public bool Exhausted { get; private set; }
        public float Normalized => Mathf.Clamp01(Current / Mathf.Max(1, capacity));
        public bool CanSprint => !Exhausted && Current > .001f;
        private float recoveryWait;

        private void Awake() { Current = Mathf.Max(1, capacity); }

        public void Tick(float delta, float sprintFraction)
        {
            GameSession game = GameSession.Instance;
            if (game == null || game.Phase != GameSession.ScreenState.Playing || delta <= 0) return;
            if (CanSprint && sprintFraction > .02f)
            {
                // Actual travel controls spending; holding Shift against a wall costs nothing.
                Current = Mathf.Max(0, Current - sprintDrain * delta * Mathf.Clamp01(sprintFraction));
                recoveryWait = recoveryDelay;
                if (Current <= .001f) { Current = 0; Exhausted = true; }
                return;
            }
            float waiting = Mathf.Min(recoveryWait, delta);
            recoveryWait -= waiting;
            Current = Mathf.Min(Mathf.Max(1, capacity), Current + recoveryRate * (delta - waiting));
            if (Exhausted && Normalized >= resumeThreshold) Exhausted = false;
        }
    }
}
