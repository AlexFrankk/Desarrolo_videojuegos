using UnityEngine;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Movimiento del mago con CharacterController. Camina por defecto; Shift = correr.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] float runSpeed = 4.2f;
        [SerializeField] float acceleration = 9f;
        [SerializeField] float turnSpeed = 540f;

        [Header("Gravedad / Pendientes")]
        [SerializeField] float gravity = -25f;
        [SerializeField] float stickToGroundForce = -4f;
        [SerializeField] float slideSpeed = 6f;

        [Header("Salto (Espacio)")]
        [SerializeField] float jumpHeight = 1.2f;
        [Tooltip("Margen tras dejar el suelo en el que aún se puede saltar.")]
        [SerializeField] float coyoteTime = 0.12f;
        [Tooltip("Si pulsas saltar un poco antes de aterrizar, el salto se guarda este tiempo.")]
        [SerializeField] float jumpBuffer = 0.12f;

        [Header("Referencias")]
        [SerializeField] GroundSensor sensor;
        [SerializeField] Transform cameraTransform;

        InputAction move, runAction, jumpAction;
        CharacterController cc;
        Vector3 planarVelocity;
        float verticalVelocity;
        float lastGroundedTime = -10f, lastJumpPressTime = -10f;
        bool jumping;

        public bool IsJumping { get { return jumping; } }

        public float NormalizedSpeed { get; private set; }   // 0..1 (caminar ~0.57, correr 1)
        public bool IsGrounded { get { return cc.isGrounded || (sensor != null && sensor.IsGrounded); } }
        public float SlopeAngle { get { return sensor != null ? sensor.SlopeAngle : 0f; } }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (GetComponent<MageFootsteps>() == null) gameObject.AddComponent<MageFootsteps>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            runAction = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            runAction.AddBinding("<Gamepad>/buttonSouth");
            jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable() { move.Enable(); runAction.Enable(); jumpAction.Enable(); }
        void OnDisable() { move.Disable(); runAction.Disable(); jumpAction.Disable(); }

        void Update()
        {
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            float speed = input.sqrMagnitude < 0.01f ? 0f : (runAction.IsPressed() ? runSpeed : walkSpeed) * Mathf.Max(0.4f, input.magnitude);

            Vector3 wish = CameraRelative(input);
            if (sensor != null && sensor.IsGrounded) wish = Vector3.ProjectOnPlane(wish, sensor.Normal).normalized;

            planarVelocity = Vector3.MoveTowards(planarVelocity, wish * speed, acceleration * Time.deltaTime);

            bool grounded = IsGrounded && verticalVelocity <= 0f;
            if (grounded) { lastGroundedTime = Time.time; jumping = false; }

            // No saltar con un panel abierto (quiz, pista) o durante la cinemática del puente.
            var ui = UIManager.Instance;
            bool uiBlocks = (ui != null && (ui.IsModalOpen || ui.InCinematic)) || GameOverScreen.IsOpen;
            if (jumpAction.WasPressedThisFrame() && !uiBlocks) lastJumpPressTime = Time.time;

            bool onSteepSlope = sensor != null && sensor.IsGrounded && sensor.SlopeAngle > cc.slopeLimit;
            if (!jumping && !onSteepSlope
                && Time.time - lastJumpPressTime <= jumpBuffer
                && Time.time - lastGroundedTime <= coyoteTime)
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                jumping = true;
                lastJumpPressTime = lastGroundedTime = -10f;
            }
            else if (grounded) verticalVelocity = stickToGroundForce;
            else verticalVelocity += gravity * Time.deltaTime;

            // Al chocar con un techo, empieza a caer.
            if (verticalVelocity > 0f && (cc.collisionFlags & CollisionFlags.Above) != 0) verticalVelocity = 0f;

            Vector3 velocity = planarVelocity;
            velocity.y += verticalVelocity;

            if (sensor != null && sensor.IsGrounded && sensor.SlopeAngle > cc.slopeLimit)
                velocity += Vector3.ProjectOnPlane(Vector3.down, sensor.Normal).normalized * slideSpeed;

            cc.Move(velocity * Time.deltaTime);

            Vector3 flat = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
            if (flat.sqrMagnitude > 0.05f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);

            NormalizedSpeed = Mathf.Clamp01(flat.magnitude / runSpeed);
        }

        /// Llamado por PlayerSafety tras un respawn.
        public void ResetMotion() { planarVelocity = Vector3.zero; verticalVelocity = 0f; jumping = false; lastJumpPressTime = -10f; }

        Vector3 CameraRelative(Vector2 input)
        {
            if (cameraTransform == null) return new Vector3(input.x, 0f, input.y);
            Vector3 f = cameraTransform.forward; f.y = 0f; f.Normalize();
            Vector3 r = cameraTransform.right; r.y = 0f; r.Normalize();
            return f * input.y + r * input.x;
        }
    }
}
