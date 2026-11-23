using UnityEngine;

namespace MageHero
{
    /// Ataque a distancia para enemigos: cuando el mago está en rango y a la vista, carga un orbe (aviso visual)
    /// y lo lanza hacia donde estará el mago. Mantiene la distancia: si el mago se acerca demasiado, retrocede.
    /// Funciona junto a EnemyAI3D (que persigue y para a distancia); desactiva su golpe cuerpo a cuerpo.
    [RequireComponent(typeof(EnemyAI3D))]
    public class EnemyCaster3D : MonoBehaviour
    {
        [Header("Disparo")]
        public float castRange = 13f;
        public float cooldown = 2.4f;
        public float chargeTime = 0.55f;
        public float projectileSpeed = 11f;
        public float damage = 9f;
        public Color orbColor = new Color(0.75f, 0.2f, 1f);
        [Tooltip("Punto de salida (bastón, mano...). Vacío = pecho.")]
        public Transform castPoint;
        public float castHeight = 1.3f;

        [Header("Distancia")]
        public float retreatDistance = 5f;
        public float retreatSpeed = 2.2f;

        EnemyAI3D ai;
        HealthSystem self, playerHealth;
        Transform player;
        float nextCast, chargeEnd = -1f;
        GameObject chargeOrb;
        int obstacleMask, groundMask;

        void Awake()
        {
            ai = GetComponent<EnemyAI3D>();
            self = GetComponent<HealthSystem>();
            ai.meleeEnabled = false;
            ai.attackRange = castRange * 0.85f;          // EnemyAI3D se detiene a esta distancia en vez de pegarse al mago
            nextCast = Time.time + Random.Range(0.8f, 2f);
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) { player = p.transform; playerHealth = p.GetComponentInParent<HealthSystem>(); }
            obstacleMask = GameLayers.Exists(GameLayers.Obstacles) ? GameLayers.Mask(GameLayers.Obstacles) : 0;
            groundMask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
        }

        void Update()
        {
            if (self.IsDead || player == null || playerHealth == null || playerHealth.IsDead || !ai.enabled) { CancelCharge(); return; }
            Vector3 origin = Origin();
            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            bool engaged = ai.CurrentState == EnemyAI3D.State.Attack || ai.CurrentState == EnemyAI3D.State.Chase;

            if (engaged && dist < retreatDistance) Retreat(-to.normalized);

            if (chargeEnd > 0f)
            {
                float k = 1f - Mathf.Clamp01((chargeEnd - Time.time) / chargeTime);
                if (chargeOrb != null) { chargeOrb.transform.position = origin; chargeOrb.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.5f, k); }
                if (Time.time >= chargeEnd) Fire(origin);
                return;
            }
            if (!engaged || dist > castRange || Time.time < nextCast) return;
            if (obstacleMask != 0 && Physics.Linecast(origin, player.position + Vector3.up, obstacleMask, QueryTriggerInteraction.Ignore)) return;
            BeginCharge(origin);
        }

        Vector3 Origin() => castPoint != null ? castPoint.position : transform.position + Vector3.up * castHeight + transform.forward * 0.5f;

        void BeginCharge(Vector3 origin)
        {
            chargeEnd = Time.time + chargeTime;
            chargeOrb = RuntimeMaterials.Prim(PrimitiveType.Sphere, "CargaHechizo", null, origin, Vector3.one * 0.1f, RuntimeMaterials.Glow(orbColor), false);
            var l = chargeOrb.AddComponent<Light>(); l.type = LightType.Point; l.color = orbColor; l.range = 3f; l.intensity = 2f;
        }

        void CancelCharge()
        {
            chargeEnd = -1f;
            if (chargeOrb != null) Destroy(chargeOrb);
        }

        void Fire(Vector3 origin)
        {
            CancelCharge();
            nextCast = Time.time + cooldown * Random.Range(0.85f, 1.15f);
            // Apunta un poco por delante del mago (si se mueve, hay que esquivar).
            Vector3 target = player.position + Vector3.up * 0.9f;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) target += new Vector3(cc.velocity.x, 0f, cc.velocity.z) * (Vector3.Distance(origin, target) / projectileSpeed) * 0.6f;
            Vector3 dir = (target - origin).normalized;

            var go = RuntimeMaterials.Prim(PrimitiveType.Sphere, "OrbeOscuro", null, origin, Vector3.one * 0.45f, RuntimeMaterials.Glow(orbColor), false);
            var col = go.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.6f;
            go.AddComponent<Rigidbody>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f; trail.startWidth = 0.35f; trail.endWidth = 0f; trail.sharedMaterial = RuntimeMaterials.Glow(orbColor);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = orbColor; l.range = 3.5f; l.intensity = 1.8f;
            var p = go.AddComponent<Projectile3D>();
            p.speed = projectileSpeed; p.damage = damage; p.lifetime = castRange / projectileSpeed + 1.5f;
            p.Launch(dir, Team.Enemy);
            Face(dir);
        }

        void Retreat(Vector3 dir)
        {
            Vector3 next = transform.position + dir * retreatSpeed * Time.deltaTime;
            if (obstacleMask != 0 && Physics.SphereCast(transform.position + Vector3.up, 0.5f, dir, out _, 1f, obstacleMask, QueryTriggerInteraction.Ignore)) return;
            if (!Physics.Raycast(next + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, groundMask, QueryTriggerInteraction.Ignore)) return;
            if (hit.normal.y < 0.6f) return;
            next.y = hit.point.y;
            transform.position = next;
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        void OnDestroy() => CancelCharge();
    }
}
