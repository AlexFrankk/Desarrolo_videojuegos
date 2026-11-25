using UnityEngine;

namespace MageHero
{
    /// Jefe del nivel 1 (Otoño): Gólem de piedra con brazos y piernas articulados.
    ///  - Camina hacia el mago balanceando brazos y piernas.
    ///  - A media distancia levanta una roca y se la lanza en parábola (marca roja donde caerá).
    ///  - Si el mago está pegado, golpea el suelo con ambos puños (daño en área).
    [RequireComponent(typeof(HealthSystem))]
    public class GolemBoss : MonoBehaviour
    {
        public float rockDamage = 18f, slamDamage = 20f;
        public float rockCooldown = 4f, slamCooldown = 3f;
        public float rockMinRange = 6f, rockMaxRange = 24f, slamRange = 4.2f;

        Transform hips, torso, head, shL, shR, elbowL, elbowR, legL, legR, fistR;
        FinalBoss boss; EnemyAI3D ai; HealthSystem hs; Transform player;
        enum Act { None, Throw, Slam }
        Act act; float actT; bool released;
        float nextRock, nextSlam, phase;
        Vector3 lastPos;
        Transform heldRock;
        Material rockMat;

        // ------------------------------------------------------------------ construcción del modelo
        public static void Build(GameObject root, Color rock, Color crack)
        {
            var g = root.AddComponent<GolemBoss>();
            var stone = RuntimeMaterials.Toon(rock);
            var dark = RuntimeMaterials.Toon(rock * 0.7f);
            var glow = RuntimeMaterials.Glow(crack);
            g.rockMat = stone;
            var model = BossKit.Pivot(root.transform, "Modelo", Vector3.zero);

            g.hips = BossKit.Pivot(model, "Cadera", new Vector3(0f, 1.5f, 0f));
            BossKit.Part(g.hips, "Pelvis", PrimitiveType.Cube, Vector3.zero, new Vector3(1.3f, 0.6f, 0.8f), dark);

            g.torso = BossKit.Pivot(g.hips, "Torso", new Vector3(0f, 0.3f, 0f));
            BossKit.Part(g.torso, "Pecho", PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(2f, 1.4f, 1.2f), stone);
            BossKit.Part(g.torso, "RocaEspalda1", PrimitiveType.Cube, new Vector3(0.5f, 1.35f, -0.5f), Vector3.one * 0.6f, dark, new Vector3(20, 30, 10));
            BossKit.Part(g.torso, "RocaEspalda2", PrimitiveType.Cube, new Vector3(-0.6f, 1.45f, -0.45f), Vector3.one * 0.5f, dark, new Vector3(-15, 10, 25));
            BossKit.Part(g.torso, "Grieta1", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0.61f), new Vector3(0.9f, 0.1f, 0.04f), glow, new Vector3(0, 0, 12));
            BossKit.Part(g.torso, "Grieta2", PrimitiveType.Cube, new Vector3(0.2f, 0.95f, 0.61f), new Vector3(0.08f, 0.6f, 0.04f), glow, new Vector3(0, 0, -20));
            BossKit.Part(g.torso, "Nucleo", PrimitiveType.Sphere, new Vector3(0f, 0.85f, 0.55f), Vector3.one * 0.35f, glow);

            g.head = BossKit.Pivot(g.torso, "Cabeza", new Vector3(0f, 1.6f, 0.1f));
            BossKit.Part(g.head, "Craneo", PrimitiveType.Cube, Vector3.zero, new Vector3(0.8f, 0.7f, 0.75f), stone);
            BossKit.Part(g.head, "Ceja", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0.3f), new Vector3(0.9f, 0.16f, 0.3f), dark);
            BossKit.Part(g.head, "OjoI", PrimitiveType.Cube, new Vector3(-0.18f, 0.06f, 0.38f), new Vector3(0.16f, 0.08f, 0.04f), glow);
            BossKit.Part(g.head, "OjoD", PrimitiveType.Cube, new Vector3(0.18f, 0.06f, 0.38f), new Vector3(0.16f, 0.08f, 0.04f), glow);

            g.shL = Arm(g.torso, "BrazoI", -1.2f, stone, dark, out g.elbowL, out _);
            g.shR = Arm(g.torso, "BrazoD", 1.2f, stone, dark, out g.elbowR, out g.fistR);
            g.legL = Leg(g.hips, "PiernaI", -0.45f, stone, dark);
            g.legR = Leg(g.hips, "PiernaD", 0.45f, stone, dark);
        }

        static Transform Arm(Transform torso, string name, float x, Material stone, Material dark, out Transform elbow, out Transform fist)
        {
            var sh = BossKit.Pivot(torso, name, new Vector3(x, 1.3f, 0f));
            BossKit.Part(sh, "Hombro", PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.85f, dark, new Vector3(0, 0, x > 0 ? -15 : 15));
            BossKit.Part(sh, "Brazo", PrimitiveType.Cube, new Vector3(0f, -0.65f, 0f), new Vector3(0.55f, 1f, 0.55f), stone);
            elbow = BossKit.Pivot(sh, "Codo", new Vector3(0f, -1.15f, 0f));
            BossKit.Part(elbow, "Antebrazo", PrimitiveType.Cube, new Vector3(0f, -0.45f, 0f), new Vector3(0.62f, 0.9f, 0.62f), stone);
            fist = BossKit.Part(elbow, "Puño", PrimitiveType.Cube, new Vector3(0f, -1.05f, 0f), new Vector3(0.95f, 0.8f, 0.95f), dark);
            return sh;
        }

        static Transform Leg(Transform hips, string name, float x, Material stone, Material dark)
        {
            var leg = BossKit.Pivot(hips, name, new Vector3(x, -0.2f, 0f));
            BossKit.Part(leg, "Muslo", PrimitiveType.Cube, new Vector3(0f, -0.45f, 0f), new Vector3(0.62f, 0.8f, 0.62f), stone);
            BossKit.Part(leg, "Pierna", PrimitiveType.Cube, new Vector3(0f, -1f, 0.05f), new Vector3(0.68f, 0.7f, 0.68f), stone);
            BossKit.Part(leg, "Pie", PrimitiveType.Cube, new Vector3(0f, -1.3f, 0.15f), new Vector3(0.82f, 0.3f, 1f), dark);
            return leg;
        }

        // ------------------------------------------------------------------ comportamiento
        void Start()
        {
            boss = GetComponent<FinalBoss>(); ai = GetComponent<EnemyAI3D>(); hs = GetComponent<HealthSystem>();
            var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
            if (ai != null) { ai.meleeEnabled = false; ai.attackRange = 3f; ai.moveSpeed = 1.8f; }
            lastPos = transform.position;
            nextRock = Time.time + 2f;
        }

        void Update()
        {
            if (hs.IsDead) return;
            AnimateWalk();
            if (boss == null || !boss.IsAwake || player == null) return;
            if (act != Act.None) { RunAction(); return; }

            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist <= slamRange && Time.time >= nextSlam) Begin(Act.Slam);
            else if (dist >= rockMinRange && dist <= rockMaxRange && Time.time >= nextRock &&
                     BossKit.LineOfSight(transform.position + Vector3.up * 3f, player.position + Vector3.up)) Begin(Act.Throw);
        }

        void Begin(Act a)
        {
            act = a; actT = 0f; released = false;
            if (ai != null) ai.enabled = false;                       // se planta para atacar
            if (a == Act.Throw)
            {
                heldRock = BossKit.Part(fistR, "RocaEnMano", PrimitiveType.Cube, new Vector3(0f, -0.7f, 0f), Vector3.one * 0.01f, rockMat, Random.rotation.eulerAngles);
            }
        }

        void RunAction()
        {
            actT += Time.deltaTime;
            FacePlayer(6f);
            if (act == Act.Throw)
            {
                // 0-0.75 s: brazo derecho hacia atrás con la roca creciendo; 0.75 s: lanza; luego recupera.
                float up = Mathf.Clamp01(actT / 0.75f);
                if (!released)
                {
                    shR.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -165f, Ease(up)), 0f, 0f);
                    if (heldRock != null) heldRock.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.1f, up);
                    if (actT >= 0.75f) ThrowRock();
                }
                else shR.localRotation = Quaternion.Slerp(shR.localRotation, Quaternion.Euler(-20f, 0f, 0f), Time.deltaTime * 10f);
                if (actT >= 1.4f) End(rockCooldown);
            }
            else // Slam
            {
                float up = Mathf.Clamp01(actT / 0.6f);
                if (!released)
                {
                    float a = Mathf.Lerp(0f, -155f, Ease(up));
                    shL.localRotation = shR.localRotation = Quaternion.Euler(a, 0f, 0f);
                    if (actT >= 0.6f) Slam();
                }
                else
                {
                    shL.localRotation = shR.localRotation = Quaternion.Slerp(shR.localRotation, Quaternion.Euler(-15f, 0f, 0f), Time.deltaTime * 12f);
                    torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(18f, 0f, 0f), Time.deltaTime * 12f);
                }
                if (actT >= 1.3f) End(slamCooldown);
            }
        }

        void ThrowRock()
        {
            released = true;
            Vector3 from = heldRock != null ? heldRock.position : fistR.position;
            if (heldRock != null) { heldRock.SetParent(null, true); }
            float dist = Vector3.Distance(from, player.position);
            float flight = Mathf.Clamp(dist / 14f, 0.9f, 1.7f);
            Vector3 target = BossKit.Predict(player, flight * 0.7f);
            if (RuntimeMaterials.Ground(target, out var g)) target = g;
            var visual = heldRock != null ? heldRock.gameObject : BossKit.Part(null, "Roca", PrimitiveType.Cube, from, Vector3.one, rockMat).gameObject;
            visual.transform.localScale = Vector3.one * 1.1f;
            ArcProjectile.Launch(visual, from, target, flight, 3f + dist * 0.15f, rockDamage, 2.1f, new Color(0.75f, 0.5f, 0.25f));
            heldRock = null;
        }

        void Slam()
        {
            released = true;
            Vector3 c = transform.position + transform.forward * 1.8f;
            if (RuntimeMaterials.Ground(c, out var g)) c = g;
            HitFX.Explosion(c + Vector3.up * 0.3f, 1.8f, new Color(0.8f, 0.55f, 0.3f));
            float d = Vector3.Distance(player.position, c);
            if (d <= slamRange) player.GetComponentInParent<HealthSystem>()?.TakeDamage(slamDamage);
            CameraShake3D.Shake(d < 10f ? 0.45f : 0.2f, 0.4f);
        }

        void End(float cooldown)
        {
            if (act == Act.Throw) nextRock = Time.time + cooldown; else nextSlam = Time.time + cooldown;
            act = Act.None;
            if (heldRock != null) Destroy(heldRock.gameObject);
            if (ai != null && boss != null && boss.IsAwake) ai.enabled = true;
        }

        void AnimateWalk()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 d = transform.position - lastPos; d.y = 0f; lastPos = transform.position;
            float w = Mathf.Clamp01(d.magnitude / dt / 1.5f);
            phase += dt * (2f + w * 3f);
            float s = Mathf.Sin(phase);
            legL.localRotation = Quaternion.Euler(s * 30f * w, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-s * 30f * w, 0f, 0f);
            hips.localPosition = new Vector3(0f, 1.5f + Mathf.Abs(s) * 0.08f * w + Mathf.Sin(Time.time * 1.6f) * 0.02f, 0f);
            if (act == Act.None)
            {
                shL.localRotation = Quaternion.Slerp(shL.localRotation, Quaternion.Euler(-s * 22f * w, 0f, 0f), dt * 8f);
                shR.localRotation = Quaternion.Slerp(shR.localRotation, Quaternion.Euler(s * 22f * w, 0f, 0f), dt * 8f);
                torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(4f * w, s * 4f * w, 0f), dt * 6f);
                elbowL.localRotation = elbowR.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            }
            if (player != null && boss != null && boss.IsAwake)
            {
                Vector3 look = player.position - head.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(look), dt * 4f);
            }
        }

        void FacePlayer(float speed)
        {
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * speed);
        }

        static float Ease(float t) => t * t * (3f - 2f * t);
    }
}
