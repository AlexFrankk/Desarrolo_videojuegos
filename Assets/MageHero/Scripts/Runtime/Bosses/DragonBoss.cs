using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Jefes dragón (nivel 2: Dragón Blanco, nivel 3: Dragón Oscuro). Construidos con piezas low-poly:
    /// cuerpo, cuello, cabeza con mandíbula, cuernos, alas que aletean, cola que ondula y patas.
    ///  - Vuela bajo y mantiene la distancia (retrocede si el mago se acerca).
    ///  - Abre la boca, carga fuego y lanza una ráfaga de bolas de fuego en abanico hacia donde estará el mago.
    [RequireComponent(typeof(HealthSystem))]
    public class DragonBoss : MonoBehaviour
    {
        public float fireballDamage = 14f, cooldown = 3.2f, fireballSpeed = 14f;
        public int fireballsPerVolley = 3;
        public float spreadDegrees = 10f;
        public float minRange = 5f, maxRange = 26f, keepAway = 6f;
        public Color fireCore = new Color(1f, 0.85f, 0.4f), fireGlow = new Color(1f, 0.45f, 0.1f);
        public const float HoverHeight = 2.2f;

        Transform model, neck, head, jaw, mouth, wingL, wingR;
        readonly List<Transform> tail = new List<Transform>();
        FinalBoss boss; EnemyAI3D ai; HealthSystem hs; Transform player;
        float nextFire, chargeT = -1f;
        int shotsLeft; float nextShot;
        GameObject mouthGlow;
        Vector3 lastPos;
        int groundMask, obstacleMask;

        // ------------------------------------------------------------------ construcción
        public static DragonBoss Build(GameObject root, Color body, Color belly, Color wing, Color eye, Color fireCore, Color fireGlow)
        {
            var d = root.AddComponent<DragonBoss>();
            d.fireCore = fireCore; d.fireGlow = fireGlow;
            var mBody = RuntimeMaterials.Toon(body);
            var mBelly = RuntimeMaterials.Toon(belly);
            var mWing = RuntimeMaterials.Toon(wing);
            var mHorn = RuntimeMaterials.Toon(Color.Lerp(belly, Color.white, 0.4f));
            var mEye = RuntimeMaterials.Glow(eye);

            d.model = BossKit.Pivot(root.transform, "Modelo", new Vector3(0f, HoverHeight, 0f));
            BossKit.Part(d.model, "Cuerpo", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.6f, 1.3f, 2.8f), mBody);
            BossKit.Part(d.model, "Vientre", PrimitiveType.Sphere, new Vector3(0f, -0.25f, 0.1f), new Vector3(1.3f, 1f, 2.3f), mBelly);
            for (int i = 0; i < 5; i++)
                BossKit.Part(d.model, "Pua" + i, PrimitiveType.Cube, new Vector3(0f, 0.62f - Mathf.Abs(i - 1.5f) * 0.04f, 1f - i * 0.5f), new Vector3(0.18f, 0.3f, 0.18f), mWing, new Vector3(45f, 0f, 45f));

            // Cuello y cabeza
            d.neck = BossKit.Pivot(d.model, "Cuello", new Vector3(0f, 0.35f, 1.2f));
            for (int i = 1; i <= 3; i++)
                BossKit.Part(d.neck, "Vertebra" + i, PrimitiveType.Sphere, new Vector3(0f, 0.32f * i, 0.4f * i), Vector3.one * (0.85f - i * 0.07f), mBody);
            d.head = BossKit.Pivot(d.neck, "Cabeza", new Vector3(0f, 1.25f, 1.45f));
            BossKit.Part(d.head, "Craneo", PrimitiveType.Cube, new Vector3(0f, 0f, 0.15f), new Vector3(0.85f, 0.62f, 1f), mBody);
            BossKit.Part(d.head, "Hocico", PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.82f), new Vector3(0.6f, 0.38f, 0.65f), mBody);
            BossKit.Part(d.head, "CuernoI", PrimitiveType.Cylinder, new Vector3(-0.25f, 0.42f, -0.2f), new Vector3(0.12f, 0.42f, 0.12f), mHorn, new Vector3(-45f, 0f, 15f));
            BossKit.Part(d.head, "CuernoD", PrimitiveType.Cylinder, new Vector3(0.25f, 0.42f, -0.2f), new Vector3(0.12f, 0.42f, 0.12f), mHorn, new Vector3(-45f, 0f, -15f));
            BossKit.Part(d.head, "OjoI", PrimitiveType.Cube, new Vector3(-0.3f, 0.12f, 0.55f), new Vector3(0.06f, 0.1f, 0.16f), mEye);
            BossKit.Part(d.head, "OjoD", PrimitiveType.Cube, new Vector3(0.3f, 0.12f, 0.55f), new Vector3(0.06f, 0.1f, 0.16f), mEye);
            d.jaw = BossKit.Pivot(d.head, "Mandibula", new Vector3(0f, -0.26f, 0.35f));
            BossKit.Part(d.jaw, "Quijada", PrimitiveType.Cube, new Vector3(0f, 0f, 0.42f), new Vector3(0.55f, 0.14f, 0.85f), mBelly);
            d.mouth = BossKit.Pivot(d.head, "Boca", new Vector3(0f, -0.15f, 1.2f));

            // Alas
            d.wingL = Wing(d.model, -1f, mBody, mWing);
            d.wingR = Wing(d.model, 1f, mBody, mWing);

            // Cola
            Transform parent = BossKit.Pivot(d.model, "Cola", new Vector3(0f, 0f, -1.3f));
            for (int i = 0; i < 6; i++)
            {
                var seg = BossKit.Pivot(parent, "Cola" + i, new Vector3(0f, -0.04f, i == 0 ? 0f : -0.55f));
                BossKit.Part(seg, "Seg", PrimitiveType.Sphere, Vector3.zero, Vector3.one * (0.75f - i * 0.1f), mBody);
                d.tail.Add(seg);
                parent = seg;
            }
            BossKit.Part(parent, "Punta", PrimitiveType.Cube, new Vector3(0f, 0f, -0.35f), new Vector3(0.05f, 0.4f, 0.5f), mWing, new Vector3(0f, 0f, 0f));

            // Patas
            foreach (var p in new[] { new Vector3(-0.55f, -0.7f, 0.8f), new Vector3(0.55f, -0.7f, 0.8f), new Vector3(-0.6f, -0.7f, -0.7f), new Vector3(0.6f, -0.7f, -0.7f) })
            {
                BossKit.Part(d.model, "Pata", PrimitiveType.Cube, p, new Vector3(0.32f, 0.6f, 0.38f), mBody, new Vector3(20f, 0f, 0f));
                BossKit.Part(d.model, "Garra", PrimitiveType.Cube, p + new Vector3(0f, -0.35f, 0.12f), new Vector3(0.36f, 0.12f, 0.45f), mHorn);
            }
            return d;
        }

        static Transform Wing(Transform model, float side, Material bone, Material membrane)
        {
            var w = BossKit.Pivot(model, side < 0 ? "AlaI" : "AlaD", new Vector3(0.7f * side, 0.45f, 0.3f));
            BossKit.Part(w, "Hueso", PrimitiveType.Cube, new Vector3(1.4f * side, 0f, 0f), new Vector3(2.8f, 0.14f, 0.18f), bone);
            BossKit.Part(w, "Membrana", PrimitiveType.Cube, new Vector3(1.45f * side, -0.03f, -0.85f), new Vector3(2.9f, 0.05f, 1.9f), membrane);
            BossKit.Part(w, "Dedo1", PrimitiveType.Cube, new Vector3(2.2f * side, 0f, -0.9f), new Vector3(0.08f, 0.08f, 1.9f), bone, new Vector3(0f, 25f * side, 0f));
            BossKit.Part(w, "Dedo2", PrimitiveType.Cube, new Vector3(1.2f * side, 0f, -0.95f), new Vector3(0.08f, 0.08f, 1.9f), bone, new Vector3(0f, 8f * side, 0f));
            return w;
        }

        // ------------------------------------------------------------------ comportamiento
        void Start()
        {
            boss = GetComponent<FinalBoss>(); ai = GetComponent<EnemyAI3D>(); hs = GetComponent<HealthSystem>();
            var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
            if (ai != null) { ai.meleeEnabled = false; ai.attackRange = 11f; ai.moveSpeed = 2.4f; }
            groundMask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            obstacleMask = GameLayers.Exists(GameLayers.Obstacles) ? GameLayers.Mask(GameLayers.Obstacles) : 0;
            nextFire = Time.time + 2.5f;
            lastPos = transform.position;
        }

        void Update()
        {
            if (hs.IsDead) return;
            Animate();
            if (boss == null || !boss.IsAwake || player == null) return;

            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < keepAway) Retreat(-to.normalized);

            if (shotsLeft > 0) { Volley(); return; }
            if (chargeT >= 0f) { Charge(); return; }
            if (dist >= minRange && dist <= maxRange && Time.time >= nextFire &&
                BossKit.LineOfSight(mouth.position, player.position + Vector3.up)) { chargeT = 0f; StartGlow(); }
        }

        void Charge()
        {
            chargeT += Time.deltaTime;
            FacePlayer(8f);
            float k = Mathf.Clamp01(chargeT / 0.65f);
            jaw.localRotation = Quaternion.Euler(35f * k, 0f, 0f);
            if (mouthGlow != null) { mouthGlow.transform.position = mouth.position; mouthGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.7f, k); }
            if (chargeT >= 0.65f) { chargeT = -1f; shotsLeft = fireballsPerVolley; nextShot = Time.time; }
        }

        void Volley()
        {
            FacePlayer(8f);
            if (Time.time < nextShot) return;
            int i = fireballsPerVolley - shotsLeft;
            float angle = fireballsPerVolley == 1 ? 0f : Mathf.Lerp(-spreadDegrees, spreadDegrees, i / (float)(fireballsPerVolley - 1));
            Vector3 from = mouth.position;
            float t = Vector3.Distance(from, player.position) / fireballSpeed;
            Vector3 target = BossKit.Predict(player, t * 0.6f) + Vector3.up * 0.9f;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * (target - from).normalized;
            BossKit.Fireball(from, dir, fireballSpeed, fireballDamage, fireCore, fireGlow);
            HitFX.Spark(from, fireGlow, 0.6f);
            shotsLeft--;
            nextShot = Time.time + 0.14f;
            if (shotsLeft <= 0)
            {
                nextFire = Time.time + cooldown * Random.Range(0.85f, 1.15f);
                if (mouthGlow != null) Destroy(mouthGlow);
            }
        }

        void StartGlow()
        {
            mouthGlow = RuntimeMaterials.Prim(PrimitiveType.Sphere, "FuegoEnBoca", null, mouth.position, Vector3.one * 0.1f, RuntimeMaterials.Glow(fireCore), false);
            var l = mouthGlow.AddComponent<Light>(); l.type = LightType.Point; l.color = fireGlow; l.range = 4f; l.intensity = 3f;
        }

        void Animate()
        {
            float t = Time.time;
            model.localPosition = new Vector3(0f, HoverHeight + Mathf.Sin(t * 1.5f) * 0.25f, 0f);
            float flap = Mathf.Sin(t * 4f) * 35f;
            wingL.localRotation = Quaternion.Euler(0f, 0f, -10f - flap);
            wingR.localRotation = Quaternion.Euler(0f, 0f, 10f + flap);
            for (int i = 0; i < tail.Count; i++)
                tail[i].localRotation = Quaternion.Euler(4f, Mathf.Sin(t * 2f - i * 0.6f) * 14f, 0f);
            if (chargeT < 0f && shotsLeft <= 0) jaw.localRotation = Quaternion.Slerp(jaw.localRotation, Quaternion.identity, Time.deltaTime * 6f);

            // Inclinación al moverse
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = (transform.position - lastPos) / dt; lastPos = transform.position;
            float fwd = Vector3.Dot(v, transform.forward);
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(Mathf.Clamp(fwd * 4f, -12f, 12f), 0f, 0f), dt * 4f);

            if (player != null && boss != null && boss.IsAwake)
            {
                Vector3 look = player.position + Vector3.up - head.position;
                if (look.sqrMagnitude > 0.01f) head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(look), dt * 5f);
            }
        }

        void Retreat(Vector3 dir)
        {
            Vector3 next = transform.position + dir * 2.4f * Time.deltaTime;
            if (obstacleMask != 0 && Physics.SphereCast(transform.position + Vector3.up * 1.5f, 0.8f, dir, out _, 1.5f, obstacleMask, QueryTriggerInteraction.Ignore)) return;
            if (!Physics.Raycast(next + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, groundMask, QueryTriggerInteraction.Ignore)) return;
            next.y = hit.point.y;
            transform.position = next;
        }

        void FacePlayer(float speed)
        {
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * speed);
        }

        void OnDestroy() { if (mouthGlow != null) Destroy(mouthGlow); }
    }
}
