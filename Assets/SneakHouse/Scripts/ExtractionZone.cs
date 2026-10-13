using UnityEngine;
namespace SneakHouse
{
    public class ExtractionZone : MonoBehaviour
    {
        public float radius = 1.0f;
        private void Update()
        {
            if (GameSession.Instance != null && GameSession.Instance.player != null) Check(GameSession.Instance.player.transform.position);
        }
        public bool Check(Vector3 position)
        {
            GameSession game = GameSession.Instance;
            Vector3 offset = position - transform.position; offset.y = 0;
            if (offset.sqrMagnitude > radius * radius || game.Finished) return false;
            if (game.Collected == game.targets.Length) { game.End(true); return true; }
            return false;
        }
    }
}
