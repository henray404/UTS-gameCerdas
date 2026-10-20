using UnityEngine;

namespace SneakHouse
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public float walkSpeed = 2.5f, runSpeed = 4.0f, sneakSpeed = 1.35f;
        [Header("Footstep hearing")]
        [Min(0)] public float walkingNoiseRadius = 3.5f;
        [Min(0)] public float runningNoiseRadius = 7;
        public Transform visual;
        public ActorAnimation acting;
        public Vector3 Velocity { get; private set; }
        public bool Sneaking { get; private set; }
        public LootTarget NearestLoot { get; private set; }
        public PlayerDistraction Distractions { get; private set; }
        public PlayerHiding Hiding { get; private set; }
        public ChaseCamera View { get; private set; }
        public PlayerStamina Stamina { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsHidden => Hiding!=null && Hiding.IsHidden;
        private CharacterController controller;
        private FootstepAudio footsteps;
        private float gravity, stepDistance;
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            footsteps = GetComponent<FootstepAudio>();
            Stamina = GetComponent<PlayerStamina>();
            if (Stamina == null) Stamina = gameObject.AddComponent<PlayerStamina>();
            View = Camera.main != null ? Camera.main.GetComponent<ChaseCamera>() : null;
            Distractions = GetComponent<PlayerDistraction>();
            if (Distractions == null) Distractions = gameObject.AddComponent<PlayerDistraction>();
            Hiding=GetComponent<PlayerHiding>();if(Hiding==null)Hiding=gameObject.AddComponent<PlayerHiding>();
        }
        private void Update()
        {
            GameSession game = GameSession.Instance;
            if (game == null || game.Finished || game.Paused) { Velocity = Vector3.zero; IsSprinting = false; return; }
            if(IsHidden){Move(Vector3.zero,false,false,Time.deltaTime);return;}
            Vector3 input = new Vector3(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0), 0,
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            bool sneak = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            Move(View != null ? View.MovementDirection(input) : input, Input.GetKey(KeyCode.LeftShift), sneak, Time.deltaTime);
            RefreshLoot();
            if (Input.GetKeyDown(KeyCode.E)) TryInteract();
            if (Input.GetKeyDown(KeyCode.Q)) Distractions.TryDrop();
        }
        public void Move(Vector3 input, bool running, bool sneak, float delta)
        {
            GameSession game = GameSession.Instance;
            IsSprinting = false;
            if(game==null || game.Phase!=GameSession.ScreenState.Playing || delta<=0){Velocity=Vector3.zero;return;}
            if(IsHidden){Velocity=Vector3.zero;Stamina?.Tick(delta,0);return;}
            input.y=0; input=Vector3.ClampMagnitude(input,1);Sneaking=sneak;running &= !Sneaking;
            running &= Stamina == null || Stamina.CanSprint;
            float speed = Sneaking ? sneakSpeed : running ? runSpeed : walkSpeed;
            Vector3 before = transform.position;
            if (controller.isGrounded && gravity < 0) gravity = -2;
            gravity += Physics.gravity.y * delta;
            controller.Move((input * speed + Vector3.up * gravity) * delta);
            Velocity = (transform.position - before) / Mathf.Max(.0001f, delta); Velocity = new Vector3(Velocity.x, 0, Velocity.z);
            IsSprinting = running && Velocity.magnitude > .08f;
            Stamina?.Tick(delta, IsSprinting ? Velocity.magnitude / Mathf.Max(.01f, runSpeed) : 0);
            if (View != null && View.FirstPersonControls)
                transform.rotation = Quaternion.Euler(0, View.Yaw, 0);
            else if (input.sqrMagnitude > .01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(input), 650 * delta);
            }
            // Distance-based steps stop sounds and foot motion when pushing into a wall.
            if (Velocity.magnitude > .08f)
            {
                stepDistance += Vector3.Distance(new Vector3(before.x,0,before.z),new Vector3(transform.position.x,0,transform.position.z));
                float spacing = running ? .704f : .455f;
                if (stepDistance >= spacing)
                {
                    stepDistance %= spacing;
                    footsteps?.PlayStep(running, Sneaking);
                    if (!Sneaking) game.EmitNoise(transform.position, running ? runningNoiseRadius : walkingNoiseRadius, running ? "Running footsteps" : "Footsteps");
                }
            }
            else stepDistance = 0;
            acting?.SetLocomotion(Velocity.magnitude, running);
        }
        public void RefreshLoot()
        {
            NearestLoot = null; float best = 1.35f * 1.35f;
            if(IsHidden)return;
            foreach (LootTarget loot in GameSession.Instance.targets)
            {
                if (!GameSession.Instance.IsRequiredTarget(loot) || loot.Collected) continue;
                Vector3 flat = loot.transform.position - transform.position; flat.y = 0;
                if (Mathf.Abs(loot.transform.position.y-transform.position.y) > 1.6f) continue;
                if (flat.sqrMagnitude >= best) continue;
                // The same walls that block vision also block reaching through partitions.
                if (Physics.Linecast(transform.position + Vector3.up * 1.1f, loot.transform.position + Vector3.up * .12f, 1 << 8, QueryTriggerInteraction.Ignore)) continue;
                best = flat.sqrMagnitude; NearestLoot = loot;
            }
        }
        public bool TryInteract()
        {
            if(IsHidden)return false;
            RefreshLoot(); if (NearestLoot == null) return false;
            GameSession.Instance.AddLoot(NearestLoot); return true;
        }
        public void Teleport(Vector3 point)
        {
            controller.enabled = false; transform.position = point; controller.enabled = true;
            gravity = 0; stepDistance = 0; Velocity = Vector3.zero; IsSprinting = false; Physics.SyncTransforms();
        }
    }
}
