using UnityEngine;

namespace SneakHouse
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public float walkSpeed = 2.5f, runSpeed = 4.0f, sneakSpeed = 1.35f;
        public Transform visual;
        public ActorAnimation acting;
        public Vector3 Velocity { get; private set; }
        public bool Sneaking { get; private set; }
        public LootTarget NearestLoot { get; private set; }
        private CharacterController controller;
        private float gravity, nextStep;
        private void Awake() { controller = GetComponent<CharacterController>(); }
        private void Update()
        {
            GameSession game = GameSession.Instance;
            if (game == null || game.Finished || game.Paused) { Velocity = Vector3.zero; return; }
            Vector3 input = new Vector3(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0), 0,
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            input = Vector3.ClampMagnitude(input, 1);
            Sneaking = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            bool running = Input.GetKey(KeyCode.LeftShift) && !Sneaking;
            float speed = Sneaking ? sneakSpeed : running ? runSpeed : walkSpeed;
            Vector3 before = transform.position;
            if (controller.isGrounded && gravity < 0) gravity = -2;
            gravity += Physics.gravity.y * Time.deltaTime;
            controller.Move((input * speed + Vector3.up * gravity) * Time.deltaTime);
            Velocity = (transform.position - before) / Mathf.Max(.0001f, Time.deltaTime); Velocity = new Vector3(Velocity.x, 0, Velocity.z);
            if (input.sqrMagnitude > .01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(input), 650 * Time.deltaTime);
                if (Time.time >= nextStep)
                {
                    nextStep = Time.time + (running ? .32f : .50f);
                    if (!Sneaking) game.EmitNoise(transform.position, running ? 5.3f : 1.25f, running ? "Langkah lari" : "Langkah");
                }
            }
            acting?.Set(Velocity.magnitude < .08f ? "Idle" : running ? "Run" : "Walk", Sneaking ? .7f : 1);
            RefreshLoot();
            if (Input.GetKeyDown(KeyCode.E)) TryInteract();
        }
        public void RefreshLoot()
        {
            NearestLoot = null; float best = 1.35f * 1.35f;
            foreach (LootTarget loot in GameSession.Instance.targets)
            {
                if (loot.Collected) continue;
                Vector3 flat = loot.transform.position - transform.position; flat.y = 0;
                if (flat.sqrMagnitude >= best) continue;
                // The same walls that block vision also block reaching through partitions.
                if (Physics.Linecast(transform.position + Vector3.up * 1.1f, loot.transform.position + Vector3.up * .12f, 1 << 8, QueryTriggerInteraction.Ignore)) continue;
                best = flat.sqrMagnitude; NearestLoot = loot;
            }
        }
        public bool TryInteract()
        {
            RefreshLoot(); if (NearestLoot == null) return false;
            GameSession.Instance.AddLoot(NearestLoot); return true;
        }
        public void Teleport(Vector3 point)
        {
            controller.enabled = false; transform.position = point; controller.enabled = true;
            gravity = 0; Velocity = Vector3.zero; Physics.SyncTransforms();
        }
    }
}
