using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MageHero
{
    /// Jefe del nivel volcánico: el Gran Cero, elemental alienígena de obsidiana (torso de obsidiana sobre una columna de humo).
    /// Duerme en su altar hasta que el mago se acerca; luego flota hacia él y alterna tres ataques:
    ///  - Zarpazo (cuerpo a cuerpo): echa el brazo atrás y golpea en látigo.
    ///  - Bolas de fuego: carga una bola en la mano y la lanza (abanico de 3 en la fase 2).
    ///  - Erupciones: levanta los brazos y golpea el suelo; brotan columnas de fuego tras un aviso.
    /// El anillo rúnico de la espalda AVISA cada ataque: gira más rápido, crece y cambia de color según la fuerza
    /// (naranja = zarpazo, rojo = bolas de fuego, guindo fosforescente = erupción).
    /// Animación procedural: cada articulación (hombro, codo, mano, torso) sigue su pose con un resorte, lo que da
    /// anticipación, latigazo y rebote sin necesitar esqueleto ni clips.
    [RequireComponent(typeof(HealthSystem))]
    public class ObsidianBoss : MonoBehaviour
    {
        public enum State { Dormant, Waking, Fight, Dead }
        enum Attack { None = 0, Swipe = 1, Fireball = 2, Eruption = 3 }

        [Header("Identidad")]
        public string bossName = "Gran Cero";

        [Header("Estadísticas")]
        public float maxHealth = 900f;
        public float wakeRadius = 15f;
        public float moveSpeed = 1.7f, turnSpeed = 4f, hoverHeight = 0.25f;
        public float meleeRange = 3.4f, meleeDamage = 18f, meleeCooldown = 2.4f;
        public float fireballDamage = 12f, fireballSpeed = 9f, fireballCooldown = 4.5f;
        public float eruptionDamage = 22f, eruptionRadius = 2.3f, eruptionCooldown = 8f, eruptionWarning = 1.3f;

        [Header("Arena")]
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 20f;

        [Header("Esqueleto procedural (lo asigna el generador del nivel)")]
        public Transform body;
        public Transform head, flames;
        [Tooltip("Hombros (pivote del brazo).")] public Transform armL, armR;
        public Transform elbowL, elbowR, handL, handR;
        public Transform smokeColumn;

        [Header("Anillo rúnico (aviso de ataques)")]
        public Transform runeRing;
        public Transform ringOuter, ringInner;
        public Renderer[] ringRenderers;
        public Light ringLight;

        [Header("Núcleo")]
        public Renderer[] coreRenderers;
        public Light coreLight;

        [Header("Humo y estelas")]
        public ParticleSystem[] smokeFx;

        [Header("Efectos de ataque")]
        public Mesh fireballMesh, discMesh, pillarMesh;
        public Material fireMaterial, hotMaterial, warningMaterial;

        [Header("Cámara")]
        public float cameraExtraDistance = 7f;

        public State CurrentState { get; private set; }
        public bool PhaseTwo { get; private set; }

        HealthSystem health;
        Transform player;
        HealthSystem playerHealth;
        int groundMask;
        float nextMelee, nextFireball, nextEruption, flash;
        bool busy;
        GameObject chargeOrb;
        readonly List<GameObject> liveFx = new List<GameObject>();
        Vector3 lastPos, smoothVel;
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color CoreCol = new Color(1f, 0.42f, 0.05f), CoreHot = new Color(1f, 0.85f, 0.35f);

        // ------------------------------------------------------------------ resortes de las articulaciones
        /// Articulación que persigue un ángulo objetivo (grados, relativo a su pose de reposo) con un resorte amortiguado.
        class Spring
        {
            public Transform t; public Quaternion rest; public Vector3 cur, vel, target; public float k, d;
            public Spring(Transform tr, float stiffness, float damping)
            {
                t = tr; k = stiffness; d = damping;
                if (t != null) rest = t.localRotation;
            }
            public void Step(float dt, float stiffMul)
            {
                if (t == null) return;
                float kk = k * stiffMul, dd = d * Mathf.Sqrt(stiffMul);
                // Subpasos: estable incluso con resortes muy rígidos durante los golpes.
                int n = 3; float h = dt / n;
                for (int i = 0; i < n; i++) { vel += (kk * (target - cur) - dd * vel) * h; cur += vel * h; }
                t.localRotation = rest * Quaternion.Euler(cur);
            }
        }

        Spring shL, elL, haL, shR, elR, haR, torso, neck;
        float stiffL = 1f, stiffR = 1f, stiffBody = 1f;
        bool poseL, poseR, poseBody;                     // true = un ataque controla esa parte
        Vector3 bodyRest; float bodyDrop, bodyDropVel, bodyDropTarget;

        // ------------------------------------------------------------------ anillo rúnico
        Attack ringLevel;
        float ringCharge, ringChargeTarget, ringPop, ringSpinO, ringSpinI;
        Color ringColor;
        static readonly Color RingIdle = new Color(0.9f, 0.2f, 0.05f) * 1.3f;
        static readonly Color RingIdle2 = new Color(1f, 0.07f, 0.04f) * 1.8f;
        static readonly Color RingSwipe = new Color(1f, 0.48f, 0.04f) * 3.2f;      // naranja
        static readonly Color RingFire = new Color(1f, 0.1f, 0.02f) * 3.6f;         // rojo intenso
        static readonly Color RingErupt = new Color(0.82f, 0f, 0.11f) * 5.2f;       // guindo fosforescente (rojo vino brillante)

        // Barra de vida
        GameObject barRoot;
        RectTransform barFill, barTrail;
        Text barLabel;

        // ------------------------------------------------------------------ ciclo de vida
        void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.team = Team.Enemy;
            health.destroyOnDeath = false;
            health.SetMaxHealth(maxHealth);
            // ProgressionManager asigna niveles a los enemigos que no tengan EnemyLevel: el jefe lleva uno
            // (desactivado) para que no le cambien la vida ni lo oscurezcan.
            var el = GetComponent<EnemyLevel>();
            if (el == null) el = gameObject.AddComponent<EnemyLevel>();
            el.isBoss = true; el.displayName = bossName.ToUpperInvariant(); el.enabled = false;

            // Hombro: rápido; codo y mano: más blandos (se retrasan y rebotan = movimiento de látigo).
            shL = new Spring(armL, 150f, 17f); elL = new Spring(elbowL, 105f, 11f); haL = new Spring(handL, 80f, 8f);
            shR = new Spring(armR, 150f, 17f); elR = new Spring(elbowR, 105f, 11f); haR = new Spring(handR, 80f, 8f);
            torso = new Spring(body, 60f, 10f);
            neck = new Spring(head, 45f, 9f);
            if (body != null) bodyRest = body.localPosition;
            if (runeRing != null) runeRing.localScale = Vector3.one * 0.01f;
            mpb = new MaterialPropertyBlock();
            ringColor = RingIdle;
            lastPos = transform.position;
        }

        void Start()
        {
            groundMask = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                var cc = p.GetComponentInParent<CharacterController>();
                player = cc != null ? cc.transform : p.transform;
                playerHealth = player.GetComponentInParent<HealthSystem>();
            }
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            CurrentState = State.Dormant;
        }

        void OnDestroy()
        {
            if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; }
            if (barRoot != null) Destroy(barRoot);
            if (chargeOrb != null) Destroy(chargeOrb);
        }

        void Update()
        {
            if (playerHealth == null && player != null) playerHealth = player.GetComponentInParent<HealthSystem>();

            switch (CurrentState)
            {
                case State.Dormant:
                    if (player != null && Flat(player.position - transform.position) < wakeRadius) StartCoroutine(Wake());
                    break;
                case State.Fight:
                    Fight();
                    break;
            }

            Animate();
            UpdateBar();
        }

        // ------------------------------------------------------------------ despertar
        IEnumerator Wake()
        {
            CurrentState = State.Waking;
            CameraShake3D.Shake(0.35f, 1.2f);
            UIManager.Instance?.ShowMessage($"¡El {bossName} despierta!", 3f);
            UIManager.Instance?.SetObjective($"Derrota al {bossName}");
            IsoCameraFollow.SetEncounter(transform, cameraExtraDistance, 0.5f, -14f);
            BuildBar();

            // Se yergue y abre los brazos (rugido) mientras el anillo aparece girando.
            poseL = poseR = poseBody = true;
            SetArm(1f, new Vector3(-55f, 0f, 55f), new Vector3(-35f, 0f, 0f), new Vector3(-25f, 0f, 0f), 0.8f);
            SetArm(-1f, new Vector3(-55f, 0f, 55f), new Vector3(-35f, 0f, 0f), new Vector3(-25f, 0f, 0f), 0.8f);
            torso.target = new Vector3(-14f, 0f, 0f); bodyDropTarget = 0.25f;
            float t = 0f;
            while (t < 1.6f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 1.6f);
                if (runeRing != null) runeRing.localScale = Vector3.one * Mathf.Max(0.01f, k);
                if (flames != null) flames.localScale = Vector3.one * (0.4f + 0.6f * k);
                flash = 1f - k;
                ringPop = Mathf.Max(ringPop, 1f - k);
                yield return null;
            }
            poseL = poseR = poseBody = false;
            nextMelee = Time.time + 1f;
            nextFireball = Time.time + 2f;
            nextEruption = Time.time + 5f;
            CurrentState = State.Fight;
        }

        // ------------------------------------------------------------------ combate
        bool PlayerAvailable =>
            player != null && playerHealth != null && !playerHealth.IsDead && !GameOverScreen.IsOpen &&
            (UIManager.Instance == null || (!UIManager.Instance.IsModalOpen && !UIManager.Instance.InCinematic));

        void Fight()
        {
            if (!PlayerAvailable) return;                       // el ataque en curso termina solo (el daño ya se comprueba)

            Vector3 to = player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            Face(to);
            if (busy) return;

            if (dist <= meleeRange && Time.time >= nextMelee) { StartCoroutine(Swipe()); return; }
            if (Time.time >= nextEruption) { StartCoroutine(Eruptions()); return; }
            if (dist > meleeRange * 1.4f && Time.time >= nextFireball) { StartCoroutine(Fireballs()); return; }
            if (dist > meleeRange * 0.85f) Move(to.normalized);
        }

        void Move(Vector3 dir)
        {
            Vector3 next = transform.position + dir * moveSpeed * (PhaseTwo ? 1.35f : 1f) * Time.deltaTime;
            Vector3 off = next - arenaCenter; off.y = 0f;
            if (off.magnitude > arenaRadius) next = arenaCenter + off.normalized * arenaRadius + Vector3.up * next.y;
            if (Physics.Raycast(next + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
                next.y = Mathf.Lerp(transform.position.y, Mathf.Max(hit.point.y, 0f) + hoverHeight, 1f - Mathf.Exp(-6f * Time.deltaTime));   // flota sobre la lava y los bordes
            transform.position = next;
        }

        void Face(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir), turnSpeed * Time.deltaTime);
        }

        /// Aviso del anillo rúnico: empieza a cargar con el color del ataque.
        void Telegraph(Attack a) { ringLevel = a; ringChargeTarget = 1f; }
        /// El ataque se suelta: destello del anillo y la carga se apaga poco a poco.
        void Release() { ringPop = 1f; ringChargeTarget = 0f; }

        float Windup(float baseTime) => baseTime * (PhaseTwo ? 0.8f : 1f);

        IEnumerator Swipe()
        {
            busy = true;
            nextMelee = Time.time + meleeCooldown * (PhaseTwo ? 0.7f : 1f);
            float s = Random.value < 0.5f ? 1f : -1f;            // lado del brazo
            bool left = s > 0f;
            Telegraph(Attack.Swipe);
            if (left) poseL = true; else poseR = true;
            poseBody = true;

            // Anticipación: echa el brazo atrás y arriba, gira el torso hacia ese lado.
            SetArm(s, new Vector3(-125f, -10f * s, 38f), new Vector3(-80f, 0f, 0f), new Vector3(-40f, 0f, 0f), 1f);
            torso.target = new Vector3(-8f, 20f * s, 0f);
            float wind = Windup(0.6f);
            for (float t = 0f; t < wind; t += Time.deltaTime) { flash = t / wind * 0.6f; yield return null; }

            // Golpe en látigo: el hombro arranca y el codo y la mano lo siguen con retraso.
            SetArm(s, new Vector3(48f, 10f * s, -12f), new Vector3(-6f, 0f, 0f), new Vector3(28f, 0f, 0f), 3.2f);
            torso.target = new Vector3(14f, -24f * s, 0f); stiffBody = 2.5f;
            Release();
            yield return new WaitForSeconds(0.12f);
            if (PlayerAvailable && Flat(player.position - transform.position) <= meleeRange + 0.7f)
            {
                if (playerHealth.TakeDamage(meleeDamage)) CameraShake3D.Shake(0.25f, 0.25f);
            }
            yield return new WaitForSeconds(0.18f);

            // Recuperación lenta (el resorte vuelve a la pose de reposo).
            stiffBody = 1f; if (left) stiffL = 0.8f; else stiffR = 0.8f;
            poseBody = false; if (left) poseL = false; else poseR = false;
            yield return new WaitForSeconds(0.35f);
            stiffL = stiffR = 1f;
            busy = false;
        }

        IEnumerator Fireballs()
        {
            busy = true;
            nextFireball = Time.time + fireballCooldown * (PhaseTwo ? 0.65f : 1f);
            float s = Random.value < 0.5f ? 1f : -1f;
            Transform hand = s > 0f ? handL : handR;
            Telegraph(Attack.Fireball);
            poseL = poseR = poseBody = true;

            // Carga: recoge la mano junto al hombro mientras crece una bola de fuego; el otro brazo se abre.
            SetArm(s, new Vector3(-100f, 0f, 12f), new Vector3(-105f, 0f, 0f), new Vector3(-55f, 0f, 0f), 1f);
            SetArm(-s, new Vector3(-30f, 0f, 42f), new Vector3(-30f, 0f, 0f), new Vector3(-15f, 0f, 0f), 1f);
            torso.target = new Vector3(-6f, 16f * s, 0f);
            if (hand != null && fireballMesh != null)
            {
                chargeOrb = MakeFx("BolaCargando", fireballMesh, fireMaterial, hand.position, Vector3.one * 0.05f);
                chargeOrb.transform.SetParent(hand, true);
                chargeOrb.transform.localPosition = new Vector3(0f, -0.35f, 0.15f);
                var inner = MakeFx("Centro", fireballMesh, hotMaterial, chargeOrb.transform.position, Vector3.one * 0.03f);
                inner.transform.SetParent(chargeOrb.transform, false); inner.transform.localPosition = Vector3.zero; inner.transform.localScale = Vector3.one * 0.6f;
            }
            float wind = Windup(0.85f);
            for (float t = 0f; t < wind; t += Time.deltaTime)
            {
                float k = t / wind;
                flash = k;
                if (chargeOrb != null)
                {
                    chargeOrb.transform.localScale = Vector3.one * (0.08f + 0.42f * k) * (1f + Mathf.Sin(Time.time * 30f) * 0.06f);
                    chargeOrb.transform.Rotate(300f * Time.deltaTime, 420f * Time.deltaTime, 0f);
                }
                yield return null;
            }

            // Lanzamiento: el brazo se estira hacia delante con un latigazo de muñeca.
            SetArm(s, new Vector3(-78f, -8f * s, -6f), new Vector3(-4f, 0f, 0f), new Vector3(22f, 0f, 0f), 3f);
            torso.target = new Vector3(12f, -14f * s, 0f); stiffBody = 2.2f;
            yield return new WaitForSeconds(0.08f);
            Release();
            Vector3 origin = chargeOrb != null ? chargeOrb.transform.position : transform.position + Vector3.up * 2.8f + transform.forward;
            if (chargeOrb != null) Destroy(chargeOrb);
            origin.y = Mathf.Max(origin.y, transform.position.y + 2f);
            if (PlayerAvailable)
            {
                Vector3 aim = player.position + Vector3.up * 0.9f - origin; aim.y = 0f;
                int count = PhaseTwo ? 3 : 1;
                for (int i = 0; i < count; i++)
                {
                    float ang = count == 1 ? 0f : (i - 1) * 18f;
                    SpawnFireball(origin, Quaternion.Euler(0f, ang, 0f) * aim.normalized);
                }
            }
            yield return new WaitForSeconds(0.2f);
            stiffBody = 1f; poseL = poseR = poseBody = false;
            stiffL = stiffR = 0.8f;
            yield return new WaitForSeconds(0.35f);
            stiffL = stiffR = 1f;
            busy = false;
        }

        IEnumerator Eruptions()
        {
            busy = true;
            nextEruption = Time.time + eruptionCooldown * (PhaseTwo ? 0.6f : 1f);
            Telegraph(Attack.Eruption);
            poseL = poseR = poseBody = true;

            // Invoca: brazos arriba, se eleva y se echa atrás.
            SetArm(1f, new Vector3(-168f, 0f, 24f), new Vector3(-28f, 0f, 0f), new Vector3(-22f, 0f, 0f), 0.9f);
            SetArm(-1f, new Vector3(-168f, 0f, 24f), new Vector3(-28f, 0f, 0f), new Vector3(-22f, 0f, 0f), 0.9f);
            torso.target = new Vector3(-12f, 0f, 0f); bodyDropTarget = 0.4f;
            float wind = Windup(1.0f);
            for (float t = 0f; t < wind; t += Time.deltaTime) { flash = 0.4f + 0.6f * t / wind; yield return null; }

            // Golpe contra el suelo.
            SetArm(1f, new Vector3(-38f, 0f, 14f), new Vector3(-55f, 0f, 0f), new Vector3(42f, 0f, 0f), 3.5f);
            SetArm(-1f, new Vector3(-38f, 0f, 14f), new Vector3(-55f, 0f, 0f), new Vector3(42f, 0f, 0f), 3.5f);
            torso.target = new Vector3(22f, 0f, 0f); stiffBody = 3f; bodyDropTarget = -0.55f;
            yield return new WaitForSeconds(0.12f);
            Release();
            CameraShake3D.Shake(0.25f, 0.45f);
            if (PlayerAvailable)
            {
                var spots = new List<Vector3> { player.position };
                int extra = PhaseTwo ? 3 : 1;
                for (int i = 0; i < extra; i++)
                {
                    Vector2 r = Random.insideUnitCircle * 6f;
                    spots.Add(player.position + new Vector3(r.x, 0f, r.y));
                }
                foreach (var sp in spots) StartCoroutine(Eruption(sp));
            }
            yield return new WaitForSeconds(0.3f);
            stiffBody = 1f; poseL = poseR = poseBody = false; bodyDropTarget = 0f;
            stiffL = stiffR = 0.7f;
            yield return new WaitForSeconds(0.45f);
            stiffL = stiffR = 1f;
            busy = false;
        }

        IEnumerator Eruption(Vector3 at)
        {
            Vector3 c = at - arenaCenter; c.y = 0f;
            if (c.magnitude > arenaRadius + 1f) at = arenaCenter + c.normalized * (arenaRadius + 1f);
            if (Physics.Raycast(at + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 20f, groundMask, QueryTriggerInteraction.Ignore)) at = hit.point;
            at.y = Mathf.Max(at.y, -0.3f);

            var warn = MakeFx("AvisoErupcion", discMesh, warningMaterial, at + Vector3.up * 0.06f, new Vector3(0.2f, 1f, 0.2f));
            var r = warn.GetComponent<Renderer>();
            for (float t = 0f; t < eruptionWarning; t += Time.deltaTime)
            {
                float k = t / eruptionWarning;
                warn.transform.localScale = new Vector3(eruptionRadius * k, 1f, eruptionRadius * k);
                SetColor(r, Color.Lerp(new Color(0.9f, 0.05f, 0.2f, 1f) * 1.6f, new Color(1f, 0.6f, 0.2f, 1f) * 1.6f, Mathf.PingPong(t * 6f, 1f)));
                yield return null;
            }
            Destroy(warn);

            var pillar = MakeFx("ColumnaDeFuego", pillarMesh, fireMaterial, at, new Vector3(eruptionRadius * 0.8f, 0.1f, eruptionRadius * 0.8f));
            var core = MakeFx("Nucleo", pillarMesh, hotMaterial, at, new Vector3(eruptionRadius * 0.4f, 0.1f, eruptionRadius * 0.4f));
            if (PlayerAvailable && Flat(player.position - at) <= eruptionRadius && player.position.y < at.y + 3f)
                playerHealth.TakeDamage(eruptionDamage);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float k = t / 0.7f, h = Mathf.Sin(k * Mathf.PI) * 5.5f;
                float w = eruptionRadius * (0.8f + 0.2f * Mathf.Sin(t * 30f));
                pillar.transform.localScale = new Vector3(w, Mathf.Max(0.1f, h), w);
                core.transform.localScale = new Vector3(w * 0.5f, Mathf.Max(0.1f, h * 1.15f), w * 0.5f);
                yield return null;
            }
            Destroy(pillar); Destroy(core);
        }

        void SpawnFireball(Vector3 origin, Vector3 dir)
        {
            var go = MakeFx("BolaDeFuego", fireballMesh, fireMaterial, origin, Vector3.one * 0.55f);
            var inner = MakeFx("Centro", fireballMesh, hotMaterial, origin, Vector3.one * 0.32f);
            inner.transform.SetParent(go.transform, true);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = CoreCol; l.range = 5f; l.intensity = 2f; l.shadows = LightShadows.None;
            var fb = go.AddComponent<BossFireball>();
            fb.Init(dir, fireballSpeed, fireballDamage, player, playerHealth, arenaCenter, arenaRadius + 6f, groundMask);
        }

        GameObject MakeFx(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.position = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            liveFx.RemoveAll(x => x == null);
            liveFx.Add(go);
            return go;
        }

        void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb); mpb.SetColor(BaseColorId, c); r.SetPropertyBlock(mpb);
        }

        // ------------------------------------------------------------------ daño y muerte
        void OnDamaged(HealthSystem _, float amount)
        {
            flash = Mathf.Max(flash, 0.8f);
            if (CurrentState == State.Dormant) StartCoroutine(Wake());      // si le disparan de lejos, despierta
            if (!PhaseTwo && health.Normalized <= 0.5f && !health.IsDead)
            {
                PhaseTwo = true;
                ringPop = 1f;
                CameraShake3D.Shake(0.3f, 0.8f);
                UIManager.Instance?.ShowMessage("¡El núcleo de obsidiana se desborda!", 3f);
            }
        }

        void OnDied(HealthSystem _)
        {
            StopAllCoroutines();
            foreach (var fx in liveFx) if (fx != null) Destroy(fx);       // avisos y columnas a medias
            liveFx.Clear();
            busy = true;
            CurrentState = State.Dead;
            StartCoroutine(Collapse());
        }

        IEnumerator Collapse()
        {
            UIManager.Instance?.SetObjective("");
            CameraShake3D.Shake(0.45f, 1.8f);
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
            if (smokeFx != null) foreach (var ps in smokeFx) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // Brazos caídos, cabeza gacha: se deshace en ceniza hundiéndose en su columna de humo.
            poseL = poseR = poseBody = true;
            SetArm(1f, new Vector3(20f, 0f, 30f), new Vector3(-50f, 0f, 0f), new Vector3(-30f, 0f, 0f), 0.6f);
            SetArm(-1f, new Vector3(20f, 0f, 30f), new Vector3(-50f, 0f, 0f), new Vector3(-30f, 0f, 0f), 0.6f);
            torso.target = new Vector3(28f, 0f, 0f);
            ringChargeTarget = 0f;
            Vector3 start = transform.position;
            Quaternion rot = transform.rotation;
            Vector3 smokeScale = smokeColumn != null ? smokeColumn.localScale : Vector3.one;
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                float k = t / 2.6f;
                flash = Mathf.PingPong(t * 8f, 1f) * (1f - k);
                transform.position = start + Vector3.down * (k * k * 3.5f);
                transform.rotation = rot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 20f) * 3f * (1f - k));
                if (flames != null) flames.localScale = Vector3.one * Mathf.Max(0.01f, 1f - k);
                if (runeRing != null) runeRing.localScale = Vector3.one * Mathf.Max(0.01f, 1f - k * 1.4f);
                if (smokeColumn != null) smokeColumn.localScale = new Vector3(smokeScale.x * (1f + k * 0.8f), smokeScale.y * Mathf.Max(0.05f, 1f - k), smokeScale.z * (1f + k * 0.8f));
                if (coreLight != null) coreLight.intensity = Mathf.Lerp(4f, 0f, k);
                if (barRoot != null) barRoot.GetComponent<CanvasGroup>().alpha = 1f - k;
                yield return null;
            }
            if (barRoot != null) Destroy(barRoot);
            IsoCameraFollow.ClearEncounter();
            yield return new WaitForSeconds(0.6f);
            BossVictoryScreen.Show(bossName);
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        // ------------------------------------------------------------------ animación procedural
        /// Fija la pose objetivo de un brazo (s = +1 brazo "L", −1 brazo "R"). Los ángulos Z se dan para el lado +1
        /// y se reflejan solos para el otro brazo. stiff > 1 = movimiento más rápido (golpes).
        void SetArm(float s, Vector3 shoulder, Vector3 elbow, Vector3 hand, float stiff)
        {
            Vector3 m(Vector3 v) => new Vector3(v.x, v.y, v.z * s);
            if (s > 0f) { shL.target = m(shoulder); elL.target = m(elbow); haL.target = m(hand); stiffL = stiff; }
            else { shR.target = m(shoulder); elR.target = m(elbow); haR.target = m(hand); stiffR = stiff; }
        }

        void Animate()
        {
            float t = Time.time, dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool dormant = CurrentState == State.Dormant;

            // Velocidad (para inclinarse al avanzar y para que el humo se arrastre detrás).
            Vector3 v = dt > 0f ? (transform.position - lastPos) / dt : Vector3.zero;
            lastPos = transform.position;
            smoothVel = Vector3.Lerp(smoothVel, v, 1f - Mathf.Exp(-5f * dt));
            Vector3 lv = transform.InverseTransformDirection(smoothVel);

            // --- Reposo: respira, se balancea al flotar; los brazos ondean desfasados (no se ve rígido).
            if (!poseL)
            {
                if (dormant) SetArm(1f, new Vector3(8f, 0f, 4f), new Vector3(-12f, 0f, 0f), new Vector3(-5f, 0f, 0f), 0.5f);
                else SetArm(1f, new Vector3(-10f + Mathf.Sin(t * 1.3f) * 6f - lv.z * 6f, 0f, 12f + Mathf.Sin(t * 0.9f) * 4f),
                            new Vector3(-25f + Mathf.Sin(t * 1.3f - 0.6f) * 8f, 0f, 0f), new Vector3(-12f + Mathf.Sin(t * 1.3f - 1.2f) * 10f, 0f, 0f), 1f);
            }
            if (!poseR)
            {
                if (dormant) SetArm(-1f, new Vector3(8f, 0f, 4f), new Vector3(-12f, 0f, 0f), new Vector3(-5f, 0f, 0f), 0.5f);
                else SetArm(-1f, new Vector3(-10f + Mathf.Sin(t * 1.3f + 2f) * 6f - lv.z * 6f, 0f, 12f + Mathf.Sin(t * 0.9f + 1f) * 4f),
                             new Vector3(-25f + Mathf.Sin(t * 1.3f + 1.4f) * 8f, 0f, 0f), new Vector3(-12f + Mathf.Sin(t * 1.3f + 0.8f) * 10f, 0f, 0f), 1f);
            }
            if (!poseBody)
            {
                torso.target = dormant ? new Vector3(18f, 0f, 0f)
                    : new Vector3(Mathf.Clamp(lv.z * 5f, -6f, 10f) + Mathf.Sin(t * 1.4f) * 2f, Mathf.Sin(t * 0.7f) * 5f, Mathf.Clamp(-lv.x * 5f, -8f, 8f) + Mathf.Sin(t * 1.1f) * 2f);
                bodyDropTarget = dormant ? -0.3f : 0f;
            }

            shL.Step(dt, stiffL); elL.Step(dt, stiffL); haL.Step(dt, stiffL);
            shR.Step(dt, stiffR); elR.Step(dt, stiffR); haR.Step(dt, stiffR);
            torso.Step(dt, stiffBody);

            // Cuerpo: flota arriba y abajo + sube/baja según la pose (con resorte).
            bodyDropVel += (90f * (bodyDropTarget - bodyDrop) - 12f * bodyDropVel) * dt;
            bodyDrop += bodyDropVel * dt;
            if (body != null && CurrentState != State.Dead)
                body.localPosition = bodyRest + Vector3.up * (Mathf.Sin(t * 1.4f) * 0.12f + bodyDrop);

            // Cabeza: sigue al mago (limitada), gacha mientras duerme.
            if (head != null)
            {
                Vector3 look = Vector3.zero;
                if (!dormant && player != null && CurrentState != State.Dead)
                {
                    Vector3 d = head.parent.InverseTransformDirection(player.position + Vector3.up * 1f - head.position);
                    float yaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -45f, 45f);
                    float pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -10f, 30f);
                    look = new Vector3(pitch, yaw, 0f);
                }
                else if (dormant) look = new Vector3(28f, 0f, 0f);
                else look = new Vector3(35f, 0f, 0f);
                neck.target = look + new Vector3(Mathf.Sin(t * 1.7f) * 2f, 0f, Mathf.Sin(t * 1.1f) * 3f);
                neck.Step(dt, 1f);
            }

            // Columna de humo: se arrastra detrás del movimiento y ondula.
            if (smokeColumn != null && CurrentState != State.Dead)
            {
                float sx = Mathf.Clamp(-lv.z * 9f, -18f, 18f) + Mathf.Sin(t * 1.9f) * 3f;
                float sz = Mathf.Clamp(lv.x * 9f, -18f, 18f) + Mathf.Sin(t * 1.3f + 1f) * 3f;
                smokeColumn.localRotation = Quaternion.Slerp(smokeColumn.localRotation, Quaternion.Euler(sx, t * 20f, sz), 1f - Mathf.Exp(-4f * dt));
            }

            // Corona de llamas.
            if (flames != null && CurrentState != State.Dead && CurrentState != State.Waking)
            {
                float f = 1f + Mathf.Sin(t * 13f) * 0.06f + Mathf.Sin(t * 7.3f) * 0.05f;
                float baseScale = dormant ? 0.4f : (PhaseTwo ? 1.25f : 1f);
                baseScale *= 1f + ringCharge * 0.25f;
                flames.localScale = new Vector3(baseScale * (2f - f), baseScale * f, baseScale * (2f - f));
            }

            AnimateRing(dt);

            // Núcleo: late y se enciende al cargar ataques.
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.5f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (PhaseTwo ? 7f : 3f));
            Color c = Color.Lerp(CoreCol, CoreHot, Mathf.Clamp01(pulse * 0.35f + flash));
            if (dormant) c *= 0.55f;
            if (coreRenderers != null) foreach (var r in coreRenderers) SetColor(r, c * (1f + flash * 1.5f));
            if (coreLight != null && CurrentState != State.Dead)
                coreLight.intensity = (dormant ? 1.2f : 3f) + pulse * 1.2f + flash * 3f;
        }

        /// Anillo rúnico: en reposo gira despacio; al cargar un ataque acelera, crece, late y cambia de color
        /// (naranja → rojo → guindo fosforescente según la fuerza del ataque). Al soltarlo da un destello.
        void AnimateRing(float dt)
        {
            if (runeRing == null) return;
            bool dormant = CurrentState == State.Dormant;
            ringCharge = Mathf.MoveTowards(ringCharge, ringChargeTarget, dt * (ringChargeTarget > ringCharge ? 1.6f : 1.1f));
            ringPop = Mathf.MoveTowards(ringPop, 0f, dt * 2.2f);
            int lvl = (int)ringLevel;
            float k = ringCharge;

            float baseSpeed = dormant ? 6f : (PhaseTwo ? 60f : 28f);
            float speed = baseSpeed + k * (120f + 110f * lvl) + ringPop * 260f;
            ringSpinO += speed * dt;
            ringSpinI -= speed * 1.6f * dt;
            if (ringOuter != null) ringOuter.localRotation = Quaternion.Euler(0f, 0f, ringSpinO);
            if (ringInner != null) ringInner.localRotation = Quaternion.Euler(0f, 0f, ringSpinI);
            if (ringOuter == null && ringInner == null) runeRing.localRotation = Quaternion.Euler(0f, 0f, ringSpinO);

            if (CurrentState == State.Fight)
            {
                float throb = Mathf.Sin(Time.time * (10f + 6f * lvl)) * 0.04f * k * lvl;
                runeRing.localScale = Vector3.one * (1f + 0.1f * lvl * k + throb + ringPop * 0.3f);
            }

            Color idle = PhaseTwo ? RingIdle2 * (0.85f + 0.25f * Mathf.Sin(Time.time * 6f)) : RingIdle * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f));
            if (dormant) idle *= 0.45f;
            Color attack = ringLevel == Attack.Eruption ? RingErupt : ringLevel == Attack.Fireball ? RingFire : RingSwipe;
            if (PhaseTwo) attack *= 1.25f;
            float blink = k > 0.75f ? 0.75f + 0.25f * Mathf.Sin(Time.time * 40f) : 1f;    // parpadea justo antes de soltar
            Color target = Color.Lerp(idle, attack * blink, Mathf.SmoothStep(0f, 1f, k)) + attack * ringPop * 0.5f;
            ringColor = Color.Lerp(ringColor, target, 1f - Mathf.Exp(-14f * dt));
            if (ringRenderers != null) foreach (var r in ringRenderers) SetColor(r, ringColor);
            if (ringLight != null)
            {
                float maxC = Mathf.Max(ringColor.r, Mathf.Max(ringColor.g, ringColor.b, 0.001f));
                ringLight.color = ringColor / maxC;
                ringLight.intensity = (dormant ? 0.4f : 1.2f) + k * (1.5f + lvl) + ringPop * 3f;
            }
        }

        // ------------------------------------------------------------------ barra de vida del jefe
        void BuildBar()
        {
            if (barRoot != null) return;
            var hud = GameObject.Find("HUD");
            if (hud == null || hud.GetComponent<Canvas>() == null) return;

            barRoot = new GameObject("BossBar", typeof(RectTransform), typeof(CanvasGroup));
            barRoot.transform.SetParent(hud.transform, false);
            var rt = (RectTransform)barRoot.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -54f); rt.sizeDelta = new Vector2(560f, 52f);

            barLabel = UIText.Make(rt, "Name", bossName.ToUpperInvariant(), 18, new Color(1f, 0.82f, 0.55f), TextAnchor.UpperCenter);
            barLabel.fontStyle = FontStyle.Bold;
            var lrt = barLabel.rectTransform; lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f); lrt.pivot = new Vector2(.5f, 1f);
            lrt.anchoredPosition = Vector2.zero; lrt.sizeDelta = new Vector2(0f, 24f);
            var ol = barLabel.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, .8f);

            var frame = Bar("Frame", rt, new Color(1f, 0.55f, 0.15f, 1f));
            var frt = frame.rectTransform; frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0f); frt.pivot = new Vector2(.5f, 0f);
            frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(0f, 24f);
            var back = Bar("Back", frame.transform, new Color(0.08f, 0.04f, 0.05f, 1f)); Inset(back.rectTransform, 3f);
            barTrail = Bar("Trail", back.transform, new Color(1f, 0.9f, 0.6f, 1f)).rectTransform; Inset(barTrail, 0f);
            barFill = Bar("Fill", back.transform, new Color(0.95f, 0.3f, 0.08f, 1f)).rectTransform; Inset(barFill, 0f);
        }

        static Image Bar(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.color = c; im.raycastTarget = false;
            return im;
        }

        static void Inset(RectTransform rt, float px)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(px, px); rt.offsetMax = new Vector2(-px, -px);
        }

        void UpdateBar()
        {
            if (barFill == null || health == null) return;
            float n = Mathf.Clamp01(health.Normalized);
            barFill.anchorMax = new Vector2(n, 1f);
            float trail = Mathf.MoveTowards(barTrail.anchorMax.x, n, Time.deltaTime * 0.35f);
            barTrail.anchorMax = new Vector2(Mathf.Max(trail, n), 1f);
            barFill.GetComponent<Image>().color = PhaseTwo ? new Color(1f, 0.15f + 0.1f * Mathf.Sin(Time.time * 8f), 0.05f) : new Color(0.95f, 0.3f, 0.08f);
            if (barLabel != null) barLabel.text = bossName.ToUpperInvariant() + (PhaseTwo ? "  ·  FURIA" : "");
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, wakeRadius);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, meleeRange);
            Gizmos.color = new Color(1f, .5f, 0f); Gizmos.DrawWireSphere(arenaCenter, arenaRadius);
        }
    }
}
