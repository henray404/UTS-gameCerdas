using UnityEngine;

namespace SneakHouse
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(AudioSource))]
    public class DroppedCan : MonoBehaviour
    {
        public float hearingRadius=10;
        public bool Impacted { get; private set; }
        public Vector3 ImpactPoint { get; private set; }
        private bool audioPaused;
        private void Start() { Destroy(gameObject,25); }
        private void Update()
        {
            GameSession game = GameSession.Instance;
            AudioSource sound = GetComponent<AudioSource>();
            if (game == null || game.Finished) { sound.Stop(); return; }
            if (game.Paused && !audioPaused) { sound.Pause(); audioPaused = true; }
            else if (!game.Paused && audioPaused) { sound.UnPause(); audioPaused = false; }
        }
        private void OnCollisionEnter(Collision collision)
        {
            if(Impacted || collision.gameObject.layer!=8 || collision.relativeVelocity.magnitude<.6f)return;
            GameSession game=GameSession.Instance;
            if(game==null || game.Phase!=GameSession.ScreenState.Playing)return;
            Impacted=true;ImpactPoint=collision.GetContact(0).point;
            // One clang per can; bouncing cannot repeatedly redirect guards.
            GetComponent<AudioSource>().Play();game.EmitNoise(ImpactPoint,hearingRadius,"Dropped can");
        }
    }
}
