using UnityEngine;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Cámara en perspectiva, tercera persona, cercana y cenital (~54°).
    /// Q/E o arrastrar con clic derecho / rueda pulsada: girar e inclinar. Z/X o rueda: zoom.
    [RequireComponent(typeof(Camera))]
    public class IsoCameraFollow : MonoBehaviour
    {
        public Transform target;
        public float yaw = 0f, pitch = 54.4f, distance = 17f, minDistance = 9f, maxDistance = 48f;
        public float fieldOfView = 46f, followSharpness = 5f, lookHeight = 0.8f, groundClearance = 3f;

        [Header("Ratón (arrastrar con clic derecho o rueda pulsada)")]
        public float mouseYawSpeed = 0.25f;
        public float mousePitchSpeed = 0.18f;
        public bool invertY = false;
        public float minPitch = 20f, maxPitch = 80f;
        Vector3 focus;
        Camera cam;

        void Start()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            if (target != null) focus = target.position;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                yaw += ((kb.qKey.isPressed ? 1f : 0f) - (kb.eKey.isPressed ? 1f : 0f)) * 69f * Time.deltaTime;
                distance += ((kb.xKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f)) * 14f * Time.deltaTime;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                distance -= mouse.scroll.ReadValue().y * 0.01f;
                if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    yaw += d.x * mouseYawSpeed;
                    pitch += (invertY ? d.y : -d.y) * mousePitchSpeed;
                }
            }
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            focus = Vector3.Lerp(focus, target.position, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 look = focus + Vector3.up * lookHeight;
            Vector3 pos = look - rot * Vector3.forward * distance;

            // Evita meterse dentro de montañas: mantiene la cámara por encima del suelo.
            RaycastHit hit;
            int mask = GameLayers.Mask(GameLayers.Environment, GameLayers.Obstacles);
            if (mask != 0 && Physics.Raycast(pos + Vector3.up * 80f, Vector3.down, out hit, 200f, mask, QueryTriggerInteraction.Ignore))
                if (pos.y < hit.point.y + groundClearance) pos.y = hit.point.y + groundClearance;

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        }
    }
}
