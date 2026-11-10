using UnityEngine;
using UnityEngine.InputSystem;

namespace SantasLittleHelpers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public const float DefaultMouseSensitivity = 0.12f;

        [SerializeField] private Transform viewPivot;
        [SerializeField] private LayerMask ceilingLayers = ~(1 << 8);

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4f;
        [SerializeField, Min(0f)] private float sprintSpeed = 6f;
        [SerializeField, Min(0f)] private float crouchSpeed = 2f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.1f;
        [SerializeField, Min(0.01f)] private float gravity = 20f;

        [Header("View")]
        [SerializeField, Min(0f)] private float mouseSensitivity = DefaultMouseSensitivity;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;
        [SerializeField, Min(0f)] private float crouchHeight = 1.1f;
        [SerializeField, Min(0.01f)] private float stanceSpeed = 5f;

        private static bool hasGlobalSensitivity;
        private static float globalMouseSensitivity = DefaultMouseSensitivity;

        private CharacterController controller;
        private InputActionMap input;
        private InputAction move;
        private InputAction look;
        private InputAction sprint;
        private InputAction crouch;
        private InputAction jump;
        private InputAction captureCursor;
        private InputAction releaseCursor;
        private Vector3 standingEyePosition;
        private float standingHeight;
        private float standingStepOffset;
        private float pitch;
        private float verticalSpeed;
        private bool grounded;
        private bool crouched;

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Max(0f, value);
        }

        public static float ActiveMouseSensitivity => globalMouseSensitivity;

        public static void ApplyGlobalMouseSensitivity(float sensitivity)
        {
            globalMouseSensitivity = Mathf.Max(0f, sensitivity);
            hasGlobalSensitivity = true;
            foreach (FirstPersonController controller in
                     FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude))
            {
                if (controller != null && controller.isActiveAndEnabled)
                    controller.mouseSensitivity = globalMouseSensitivity;
            }
        }

        private void Awake()
        {
            if (viewPivot == null)
            {
                Debug.LogError("Assign the local camera pivot.", this);
                enabled = false;
                return;
            }

            controller = GetComponent<CharacterController>();
            standingHeight = controller.height;
            standingStepOffset = controller.stepOffset;
            standingEyePosition = viewPivot.localPosition;
            grounded = controller.isGrounded;

            input = new InputActionMap("LocalPlayer");
            move = input.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            look = input.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            sprint = input.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            crouch = input.AddAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            crouch.AddBinding("<Keyboard>/c");
            jump = input.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            captureCursor = input.AddAction("CaptureCursor", InputActionType.Button, "<Mouse>/leftButton");
            releaseCursor = input.AddAction("ReleaseCursor", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            if (hasGlobalSensitivity)
                mouseSensitivity = globalMouseSensitivity;

            input?.Enable();
        }

        private void OnDisable()
        {
            input?.Disable();
            if (input != null)
                UnlockCursor();
        }

        private void OnDestroy() => input?.Dispose();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isActiveAndEnabled)
                UnlockCursor();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            bool acceptsInput = UpdateCursor();
            if (acceptsInput)
                UpdateLook();

            UpdateStance(acceptsInput && crouch.IsPressed(), deltaTime);

            Vector2 direction = acceptsInput ? move.ReadValue<Vector2>() : Vector2.zero;
            float speed = crouched ? crouchSpeed : walkSpeed;
            if (!crouched && acceptsInput && sprint.IsPressed())
                speed = sprintSpeed;

            Vector3 planar = (transform.right * direction.x + transform.forward * direction.y) * speed;
            bool jumping = acceptsInput && grounded && !crouched && jump.WasPressedThisFrame();
            if (grounded)
                verticalSpeed = jumping ? Mathf.Sqrt(2f * gravity * jumpHeight) : -2f;

            controller.stepOffset = grounded && !jumping ? standingStepOffset : 0f;

            float verticalDistance = (verticalSpeed - 0.5f * gravity * deltaTime) * deltaTime;
            verticalSpeed -= gravity * deltaTime;
            CollisionFlags contacts = controller.Move(planar * deltaTime + Vector3.up * verticalDistance);
            grounded = (contacts & CollisionFlags.Below) != 0;
            if ((contacts & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
        }

        private void UpdateLook()
        {
            Vector2 delta = look.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            viewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateStance(bool wantsCrouch, float deltaTime)
        {
            float lowHeight = Mathf.Clamp(crouchHeight, 2f * controller.radius, standingHeight);
            float currentHeight = controller.height;
            crouched = wantsCrouch || (currentHeight < standingHeight && !CanStand());
            float targetHeight = crouched ? lowHeight : standingHeight;
            float newHeight = Mathf.MoveTowards(currentHeight, targetHeight, stanceSpeed * deltaTime);
            float heightChange = newHeight - currentHeight;
            if (heightChange != 0f)
            {
                controller.height = newHeight;
                controller.center += Vector3.up * (heightChange * 0.5f);
                viewPivot.localPosition = standingEyePosition + Vector3.up * (newHeight - standingHeight);
            }

            crouched = newHeight < standingHeight || wantsCrouch;
        }

        private bool CanStand()
        {
            float radius = controller.radius;
            Vector3 currentTop = transform.TransformPoint(
                controller.center + Vector3.up * (controller.height * 0.5f - radius));
            Vector3 standingTop = currentTop + Vector3.up * (standingHeight - controller.height);

            return !Physics.CheckCapsule(currentTop, standingTop, radius - controller.skinWidth * 0.5f,
                ceilingLayers, QueryTriggerInteraction.Ignore);
        }

        private bool UpdateCursor()
        {
            if (!Application.isFocused || releaseCursor.WasPressedThisFrame())
            {
                UnlockCursor();
                return false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
                return true;

            if (captureCursor.WasPressedThisFrame())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            return false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
