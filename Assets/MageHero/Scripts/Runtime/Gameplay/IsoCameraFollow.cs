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

        // Encuadre de combate (jefes): aleja la cámara y la centra entre el mago y el enemigo.
        static IsoCameraFollow active;
        Transform encounter;
        float encounterExtra, encounterBias, encounterPitch, encounterWeight, encounterTarget;

        /// Aleja la cámara <paramref name="extraDistance"/> y encuadra al mago junto a <paramref name="other"/>.
        /// <paramref name="bias"/>: 0 = centrado en el mago, 0.5 = punto medio. <paramref name="pitchOffset"/>: grados que
        /// se baja la cámara (negativo = más de frente, para ver al enemigo y el escenario detrás).
        public static void SetEncounter(Transform other, float extraDistance = 7f, float bias = 0.45f, float pitchOffset = -12f)
        {
            var c = active != null ? active : FindAnyObjectByType<IsoCameraFollow>();
            if (c == null) return;
            c.encounter = other; c.encounterExtra = extraDistance; c.encounterBias = bias; c.encounterPitch = pitchOffset; c.encounterTarget = 1f;
        }

        /// Vuelve suavemente al encuadre normal.
        public static void ClearEncounter()
        {
            var c = active != null ? active : FindAnyObjectByType<IsoCameraFollow>();
            if (c != null) c.encounterTarget = 0f;
        }

        /// Encuadre de juego que tendría la cámara con el mago en su posición actual (sin suavizado).
        /// Lo usa la cinemática de apertura para terminar exactamente donde empieza el gameplay.
        public void GoalPose(out Vector3 pos, out Quaternion rot)
        {
            Vector3 f = target != null ? target.position : transform.position;
            rot = Quaternion.Euler(Mathf.Clamp(pitch, minPitch, maxPitch), yaw, 0f);
            Vector3 look = f + Vector3.up * lookHeight;
            pos = look - rot * Vector3.forward * distance;
            rot = Quaternion.LookRotation(look - pos, Vector3.up);
        }

        /// Coloca el foco sobre el mago al instante (tras teletransportarlo) para que no haya barrido de cámara.
        public void Snap() { if (target != null) focus = target.position; }

        void OnEnable() { active = this; }
        void OnDisable() { if (active == this) active = null; }

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

            // Encuadre de combate: transición suave (≈1 s) de entrada y salida.
            encounterWeight = Mathf.MoveTowards(encounterWeight, encounter != null ? encounterTarget : 0f, Time.deltaTime * 0.9f);
            float ew = Mathf.SmoothStep(0f, 1f, encounterWeight);
            Vector3 goal = target.position;
            if (encounter != null && ew > 0f)
            {
                Vector3 toOther = encounter.position - target.position; toOther.y = 0f;
                float far = Mathf.Clamp01(1f - (toOther.magnitude - 18f) / 14f);   // si el enemigo está muy lejos, no lo persigue
                goal += toOther * encounterBias * far * ew;
            }
            if (encounter == null && encounterWeight <= 0f) encounterTarget = 0f;

            focus = Vector3.Lerp(focus, goal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            Quaternion rot = Quaternion.Euler(Mathf.Clamp(pitch + encounterPitch * ew, minPitch, maxPitch), yaw, 0f);
            Vector3 look = focus + Vector3.up * (lookHeight + 1.8f * ew);
            Vector3 pos = look - rot * Vector3.forward * (distance + encounterExtra * ew);

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
