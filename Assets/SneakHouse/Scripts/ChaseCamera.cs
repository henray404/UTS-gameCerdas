using UnityEngine;

namespace SneakHouse
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]
    public class ChaseCamera : MonoBehaviour
    {
        public PlayerMotor player;
        [Header("Chase transition")]
        [Min(.15f)] public float enterDuration = .3f;
        [Min(.15f)] public float exitDuration = 1.15f;
        [Min(0)] public float calmDelay = 4;
        [Range(50, 100)] public float firstPersonFov = 72;
        [Header("Mouse look")]
        [Range(.1f, 8)] public float mouseSensitivity = 2.2f;
        [Range(0, 75)] public float lookUpLimit = 55;
        [Range(0, 80)] public float lookDownLimit = 65;
        public float eyeHeight = 1.48f;
        public float sneakEyeHeight = 1.13f;
        [Range(0, .2f)] public float eyeForwardOffset = .08f;
        public FirstPersonRoomShell roomShell;

        public bool IsFirstPerson { get; private set; }
        public float Transition { get; private set; }
        public float ViewBlend => Mathf.SmoothStep(0, 1, Transition);
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool FirstPersonControls => isActiveAndEnabled && player != null && !player.IsHidden &&
            GameSession.Instance != null && GameSession.Instance.Phase == GameSession.ScreenState.Playing &&
            (IsFirstPerson || Transition > .5f);
        public Vector3 PlanarForward => Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
        public Vector3 PlanarRight => Quaternion.Euler(0, Yaw, 0) * Vector3.right;
        public Vector3 OverheadPosition => overheadPosition;

        private Camera view;
        private Vector3 overheadPosition;
        private Quaternion overheadRotation;
        private float overheadSize, overheadFov, overheadNear, calmTime;
        private bool overheadOrthographic;
        private Renderer[] actorRenderers;
        private bool[] originalForcedOff;
        private bool actorHidden, captured;
        private Vector3 lastVisibleEye;
        private bool eyeCached;

        private void Awake()
        {
            view = GetComponent<Camera>();
            overheadPosition = transform.position; overheadRotation = transform.rotation;
            overheadSize = view.orthographicSize; overheadFov = view.fieldOfView;
            overheadNear = view.nearClipPlane; overheadOrthographic = view.orthographic;
            captured = true;
            if (player != null && player.acting != null && player.acting.animator != null)
            {
                actorRenderers = player.acting.animator.GetComponentsInChildren<Renderer>(true);
                originalForcedOff = new bool[actorRenderers.Length];
                for (int i = 0; i < actorRenderers.Length; i++) originalForcedOff[i] = actorRenderers[i].forceRenderingOff;
            }
        }

        private void Update()
        {
            GameSession game = GameSession.Instance;
            if (game == null || player == null) { ReleaseCursor(); return; }
            if (game.Phase == GameSession.ScreenState.Playing)
            {
                TickThreat(Time.deltaTime);
                if (FirstPersonControls && Application.isFocused && Cursor.lockState == CursorLockMode.Locked)
                    ApplyLook(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
            }
            else if (game.Phase != GameSession.ScreenState.Paused) SetFirstPerson(false);
            SyncCursor();
        }

        public void TickThreat(float delta)
        {
            GameSession game = GameSession.Instance;
            if (game == null || game.Phase != GameSession.ScreenState.Playing || player == null) return;
            if (player.IsHidden) { calmTime = 0; SetFirstPerson(false); return; }
            bool chasing = false;
            foreach (GuardBrain guard in game.guards)
                if (guard != null && guard.isActiveAndEnabled && guard.State == GuardBrain.Mode.Chase)
                { chasing = true; break; }
            if (chasing) { calmTime = 0; SetFirstPerson(true); }
            else
            {
                calmTime += Mathf.Max(0, delta);
                if (calmTime >= calmDelay) SetFirstPerson(false);
            }
        }

        private void SetFirstPerson(bool enabled)
        {
            if (IsFirstPerson == enabled) return;
            // Preserve the look direction when a chase resumes during the return flight.
            if (enabled && Transition <= .001f)
            {
                Yaw = player.transform.eulerAngles.y; Pitch = 0;
                GameSession.Instance.Notify("Chased! Mouse to look. WASD to move.");
            }
            IsFirstPerson = enabled;
        }

        public void ApplyLook(Vector2 mouseDelta)
        {
            if (!FirstPersonControls) return;
            Yaw = Mathf.Repeat(Yaw + mouseDelta.x * mouseSensitivity, 360);
            Pitch = Mathf.Clamp(Pitch - mouseDelta.y * mouseSensitivity, -lookUpLimit, lookDownLimit);
        }

        public Vector3 MovementDirection(Vector3 input)
        {
            return FirstPersonControls ? PlanarRight * input.x + PlanarForward * input.z : input;
        }

        private void LateUpdate()
        {
            GameSession game = GameSession.Instance;
            if (game == null || player == null) return;
            if (game.Phase == GameSession.ScreenState.Paused) { SyncCursor(); return; }
            AdvanceView(Time.unscaledDeltaTime);
            SyncCursor();
        }

        public void AdvanceView(float delta)
        {
            if (view == null || player == null) return;
            float duration = IsFirstPerson ? enterDuration : exitDuration;
            Transition = Mathf.MoveTowards(Transition, IsFirstPerson ? 1 : 0, Mathf.Max(0, delta) / Mathf.Max(.15f, duration));
            float weight = ViewBlend;
            if (weight <= 0)
            {
                RestoreOverhead(); SetActorHidden(false);
                if (roomShell != null) roomShell.SetView(0, false);
                return;
            }

            Quaternion eyeRotation = Quaternion.Euler(Pitch, Yaw, 0);
            Vector3 eye = player.transform.position + Vector3.up * (player.Sneaking ? sneakEyeHeight : eyeHeight)
                + PlanarForward * eyeForwardOffset;
            // Hiding teleports the avatar into a prop; the return flight must not jump with it.
            if (!player.IsHidden) { lastVisibleEye = eye; eyeCached = true; }
            else if (eyeCached) eye = lastVisibleEye;
            // Blend pitch and yaw separately so turning 180 degrees never rolls the horizon.
            Vector3 topAngles = overheadRotation.eulerAngles;
            Quaternion flightRotation = Quaternion.Euler(Mathf.LerpAngle(topAngles.x, Pitch, weight),
                Mathf.LerpAngle(topAngles.y, Yaw, weight), Mathf.LerpAngle(topAngles.z, 0, weight));
            transform.SetPositionAndRotation(Vector3.Lerp(overheadPosition, eye, weight), flightRotation);
            view.orthographic = false;
            view.nearClipPlane = Mathf.Lerp(overheadNear, .035f, weight);
            view.fieldOfView = firstPersonFov;

            if (weight >= 1) view.ResetProjectionMatrix();
            else
            {
                float aspect = view.aspect;
                Matrix4x4 top = overheadOrthographic
                    ? Matrix4x4.Ortho(-overheadSize * aspect, overheadSize * aspect, -overheadSize, overheadSize, view.nearClipPlane, view.farClipPlane)
                    : Matrix4x4.Perspective(overheadFov, aspect, view.nearClipPlane, view.farClipPlane);
                Matrix4x4 eyeProjection = Matrix4x4.Perspective(firstPersonFov, aspect, view.nearClipPlane, view.farClipPlane);
                Matrix4x4 projection = new Matrix4x4();
                for (int i = 0; i < 16; i++) projection[i] = Mathf.Lerp(top[i], eyeProjection[i], weight);
                if (overheadOrthographic)
                {
                    // Keep the focal plane zoom monotonic while changing projection type.
                    float depth = Mathf.Max(.1f, Vector3.Dot(transform.forward, eye + eyeRotation * Vector3.forward * 1.5f - transform.position));
                    float eyeHalfHeight = Mathf.Tan(firstPersonFov * .5f * Mathf.Deg2Rad) * 1.5f;
                    float halfHeight = Mathf.Lerp(overheadSize, eyeHalfHeight, weight);
                    projection[1, 1] = ((1 - weight) + weight * depth) / halfHeight;
                    projection[0, 0] = projection[1, 1] / aspect;
                }
                view.projectionMatrix = projection;
            }
            // Rendering visibility is independent of PlayerHiding's renderer.enabled snapshots.
            SetActorHidden(weight > .82f);
            if (roomShell != null) roomShell.SetView(weight, IsFirstPerson && weight > .995f);
        }

        private void SetActorHidden(bool hide)
        {
            if (actorHidden == hide || actorRenderers == null) return;
            actorHidden = hide;
            for (int i = 0; i < actorRenderers.Length; i++)
                if (actorRenderers[i] != null) actorRenderers[i].forceRenderingOff = hide || originalForcedOff[i];
        }

        private void RestoreOverhead()
        {
            if (!captured || view == null) return;
            transform.SetPositionAndRotation(overheadPosition, overheadRotation);
            view.orthographic = overheadOrthographic; view.orthographicSize = overheadSize;
            view.fieldOfView = overheadFov; view.nearClipPlane = overheadNear; view.ResetProjectionMatrix();
        }

        private void SyncCursor()
        {
            if (FirstPersonControls && Application.isFocused)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            else ReleaseCursor();
        }
        private static void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseCursor(); }
        private void OnDisable()
        {
            if (!captured) return;
            IsFirstPerson = false; Transition = 0; calmTime = 0;
            SetActorHidden(false); RestoreOverhead(); ReleaseCursor();
            if (roomShell != null) roomShell.SetView(0, false);
        }

        private void OnGUI()
        {
            if (!FirstPersonControls || ViewBlend < .7f) return;
            Color previous = GUI.color;
            GUI.color = new Color(.18f, .14f, .10f, .85f);
            GUI.DrawTexture(new Rect(Screen.width * .5f - 3, Screen.height * .5f - 3, 6, 6), Texture2D.whiteTexture);
            GUI.color = new Color(.96f, .89f, .70f);
            GUI.DrawTexture(new Rect(Screen.width * .5f - 1, Screen.height * .5f - 1, 2, 2), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
