using System;
using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Gólem de Piedra (sirviente del Gran Cero): modelo y animación compartidos por la cinemática de apertura
    /// y por el jefe del Nivel 1 (GolemBoss).
    /// Diseño: rocas cubiertas de musgo y hojas de otoño, grietas de lava, cristales en la espalda, ojos de magma
    /// y una operación que arde en el pecho.
    /// Animación procedural "con peso": piernas con rodilla (se dobla al dar el paso y el pie no patina),
    /// la cadera baja en cada pisada y se desplaza sobre la pierna de apoyo, los brazos siguen con inercia
    /// (resortes) y en reposo respira y cambia el peso de pierna.
    /// Poses de combate: agacharse a arrancar una roca, sostenerla sobre la cabeza, lanzarla, golpear el suelo,
    /// rugir, tambalearse, curarse y enrollarse en forma de rueda.
    public class StoneGolem : MonoBehaviour
    {
        public string operation = "8 + 5";
        public int answer = 13;

        // Articulaciones
        Transform body, hips, torso, head, legL, legR, kneeL, kneeR, shoulderL, shoulderR, elbowL, elbowR, fistL, fistR;
        Transform wheel;                                   // forma de rueda (oculta salvo al rodar)
        Material eyeMat, crackMat, chestMat;
        Light coreLight;
        GlowNumber chestText;
        ParticleSystem embers;

        // Estado de animación
        float walk, walkTarget, phase, eyes, eyesTarget, cracks = 1f, cracksTarget = 1f;
        float armsUp, armsUpTarget, stompLeg, stompTarget, hunch, hunchTarget, roar, roarTarget;
        float crouch, crouchTarget, hold, holdTarget, stagger, heal, healTarget, curl, curlTarget;
        Vector3 lookTarget; bool hasLook;
        float headYaw, headPitch, idleT;
        // inercia de los brazos (resorte: ángulo y velocidad)
        float armLagL, armLagLv, armLagR, armLagRv;
        Vector3 lastPos; float planarSpeed;

        public float Height => 4.4f;
        public bool Busy { get; private set; }
        public Transform Head => head;
        public Transform Chest => torso;
        public Transform FistL => fistL;
        public Transform FistR => fistR;
        public Vector3 HandsCenter => (fistL.position + fistR.position) * 0.5f;
        public bool IsWheel => curl > 0.5f;

        static readonly Color StoneA = MenuStyle.Hex(0x9a9189), StoneB = MenuStyle.Hex(0x7d746b), StoneC = MenuStyle.Hex(0xb0a69a);
        static readonly Color MossA = MenuStyle.Hex(0x6f9a3c), MossB = MenuStyle.Hex(0x58823a);
        static readonly Color LeafA = MenuStyle.Hex(0xe0772a), LeafB = MenuStyle.Hex(0xc9452b), LeafC = MenuStyle.Hex(0xf2b13a);
        static readonly Color Lava = new Color(3.2f, 1.05f, 0.22f), Eye = new Color(4.5f, 1.6f, 0.3f);

        public static StoneGolem Create(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Golem_DePiedra");
            go.transform.SetPositionAndRotation(position, rotation);
            var g = go.AddComponent<StoneGolem>();
            g.BuildModel();
            return g;
        }

        /// Construye el modelo sobre un objeto existente (lo usa el jefe GolemBoss, que ya tiene vida y collider).
        public void BuildOn(GameObject root) { if (body == null) BuildModel(); }

        // ------------------------------------------------------------------ modelo
        void BuildModel()
        {
            crackMat = CineGlow.Make(Lava, 0f, 1f, 0.35f);
            eyeMat = CineGlow.Make(Eye, 0f, 1f, 0.1f);
            var rock = IntroSet.VertexToon();

            body = LowPoly.Pivot("Cuerpo", transform, Vector3.zero);
            hips = LowPoly.Pivot("Cadera", body, new Vector3(0, 1.6f, 0));
            Chunk(hips, "Pelvis", Vector3.zero, new Vector3(1.25f, 0.6f, 0.85f), 1, StoneB, rock);

            // Piernas con rodilla: muslo (cadera→rodilla) y espinilla + pie (rodilla→suelo).
            legL = LowPoly.Pivot("Pierna_L", hips, new Vector3(-0.5f, -0.12f, 0));
            legR = LowPoly.Pivot("Pierna_R", hips, new Vector3(0.5f, -0.12f, 0));
            foreach (var (leg, s) in new[] { (legL, 2), (legR, 3) })
            {
                Chunk(leg, "Muslo", new Vector3(0, -0.36f, 0), new Vector3(0.78f, 0.8f, 0.78f), 10 + s, StoneA, rock);
                var knee = LowPoly.Pivot("Rodilla", leg, new Vector3(0, -0.72f, 0.02f));
                Chunk(knee, "Espinilla", new Vector3(0, -0.3f, 0), new Vector3(0.7f, 0.65f, 0.7f), 15 + s, StoneB, rock);
                Chunk(knee, "Pie", new Vector3(0, -0.62f, 0.14f), new Vector3(0.95f, 0.42f, 1.2f), 20 + s, StoneB, rock);
                Crack(knee, new Vector3(0, 0.02f, 0.2f), new Vector3(0.48f, 0.05f, 0.38f), 12f);
                if (leg == legL) kneeL = knee; else kneeR = knee;
            }

            // Torso: gran roca + espalda + hombreras, con musgo, hojas y cristales de lava.
            torso = LowPoly.Pivot("Torso", hips, new Vector3(0, 0.25f, 0));
            Chunk(torso, "Pecho", new Vector3(0, 1.05f, 0), new Vector3(1.85f, 1.45f, 1.25f), 31, StoneA, rock);
            Chunk(torso, "Espalda", new Vector3(0, 1.35f, -0.45f), new Vector3(1.6f, 1.1f, 0.9f), 32, StoneB, rock);
            Chunk(torso, "Hombrera_L", new Vector3(-1.05f, 1.65f, 0), new Vector3(0.9f, 0.7f, 0.9f), 33, StoneC, rock);
            Chunk(torso, "Hombrera_R", new Vector3(1.05f, 1.65f, 0), new Vector3(0.9f, 0.7f, 0.9f), 34, StoneC, rock);
            Moss(torso, new Vector3(-1.05f, 1.98f, 0.02f), new Vector3(0.85f, 0.25f, 0.8f), 81);
            Moss(torso, new Vector3(1.05f, 1.98f, -0.02f), new Vector3(0.8f, 0.22f, 0.85f), 82);
            Moss(torso, new Vector3(0.1f, 1.88f, -0.45f), new Vector3(1.3f, 0.3f, 0.75f), 83);
            Leaves(torso, new Vector3(0, 2.0f, -0.4f), 1.1f, 16, 91);
            Leaves(torso, new Vector3(-1.05f, 2.05f, 0f), 0.4f, 6, 92);
            // cristales de lava que sobresalen de la espalda
            var crystals = new LowPoly().Paint(Color.white);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.55f + i * 0.28f, h = 0.35f + (i % 2) * 0.25f + (i == 2 ? 0.2f : 0f);
                crystals.At(new Vector3(x, 1.6f + (i % 2) * 0.1f, -0.85f), Quaternion.Euler(-35f + i * 4f, 0, (i - 2) * 12f), Vector3.one).Prism(4, 0.09f, 0f, h);
            }
            crystals.Spawn("CristalesLava", torso, crackMat, default, false);
            // grietas: venas de lava sobre el pecho
            Crack(torso, new Vector3(0, 0.25f, 0.05f), new Vector3(1.1f, 0.07f, 0.8f), 0f);
            Crack(torso, new Vector3(-0.35f, 1.55f, 0.62f), new Vector3(0.05f, 0.7f, 0.05f), 25f);
            Crack(torso, new Vector3(0.42f, 1.5f, 0.6f), new Vector3(0.05f, 0.55f, 0.05f), -30f);
            Crack(torso, new Vector3(0.1f, 0.55f, 0.62f), new Vector3(0.75f, 0.05f, 0.05f), 8f);
            Crack(torso, new Vector3(-0.75f, 1.1f, 0.4f), new Vector3(0.05f, 0.6f, 0.05f), -12f);
            // grieta ancha del pecho, donde arde la operación
            var plate = new LowPoly().Paint(Color.white);
            plate.At(new Vector3(0, 1.08f, 0.64f), Quaternion.Euler(-4, 0, 3), Vector3.one).Box(new Vector3(0.95f, 0.36f, 0.03f));
            plate.At(new Vector3(-0.42f, 1.18f, 0.63f), Quaternion.Euler(-4, 0, 28), Vector3.one).Box(new Vector3(0.35f, 0.2f, 0.03f));
            plate.At(new Vector3(0.4f, 0.98f, 0.63f), Quaternion.Euler(-4, 0, -22), Vector3.one).Box(new Vector3(0.32f, 0.18f, 0.03f));
            chestMat = CineGlow.Make(new Color(0.9f, 0.18f, 0.04f), 0f, 1f, 0.25f);
            plate.Spawn("PlacaLava", torso, chestMat, default, false);
            chestText = CineGlow.Number(operation, torso, new Vector3(0, 1.08f, 0.7f), new Color(5f, 2.6f, 0.9f), 0.36f, false);
            chestText.transform.localRotation = Quaternion.Euler(-4, 180, 0);   // TextMesh se lee mirando a -Z: lo giramos hacia delante
            chestText.Pulse = 0.2f;
            chestText.FadeTo(1f, 10f);

            // Cabeza pequeña y hundida entre los hombros, con ceja de piedra y musgo encima
            head = LowPoly.Pivot("Cabeza", torso, new Vector3(0, 1.95f, 0.3f));
            Chunk(head, "Craneo", new Vector3(0, 0.22f, 0), new Vector3(0.75f, 0.6f, 0.7f), 41, StoneC, rock);
            Chunk(head, "Ceja", new Vector3(0, 0.36f, 0.25f), new Vector3(0.8f, 0.2f, 0.35f), 42, StoneB, rock);
            Moss(head, new Vector3(0, 0.5f, -0.05f), new Vector3(0.6f, 0.18f, 0.55f), 84);
            Chunk(head, "Mandibula", new Vector3(0, -0.02f, 0.18f), new Vector3(0.6f, 0.22f, 0.4f), 43, StoneB, rock);
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                var e = new LowPoly().Paint(Color.white);
                e.At(new Vector3(x, 0.22f, 0.33f), Quaternion.identity, new Vector3(1.3f, 0.7f, 0.6f)).Rock(0.075f, 5, 0f, 0);
                e.Spawn("Ojo", head, eyeMat, default, false);
            }

            // Brazos largos con puños enormes (los arrastra al andar)
            shoulderL = LowPoly.Pivot("Hombro_L", torso, new Vector3(-1.15f, 1.55f, 0));
            shoulderR = LowPoly.Pivot("Hombro_R", torso, new Vector3(1.15f, 1.55f, 0));
            foreach (var (sh, side) in new[] { (shoulderL, -1), (shoulderR, 1) })
            {
                Chunk(sh, "Brazo", new Vector3(side * 0.12f, -0.45f, 0), new Vector3(0.6f, 0.9f, 0.6f), 50 + side, StoneA, rock);
                var el = LowPoly.Pivot("Codo", sh, new Vector3(side * 0.15f, -0.95f, 0));
                Chunk(el, "Antebrazo", new Vector3(0, -0.4f, 0.05f), new Vector3(0.62f, 0.85f, 0.62f), 60 + side, StoneB, rock);
                var fist = LowPoly.Pivot("Puno", el, new Vector3(0, -1.0f, 0.1f));
                Chunk(fist, "PunoMesh", Vector3.zero, new Vector3(0.95f, 0.85f, 0.95f), 70 + side, StoneC, rock);
                Moss(fist, new Vector3(0, 0.38f, -0.1f), new Vector3(0.6f, 0.15f, 0.5f), 85 + side);
                Crack(el, new Vector3(0, 0.0f, 0.05f), new Vector3(0.45f, 0.05f, 0.45f), 0f);
                if (side < 0) { elbowL = el; fistL = fist; } else { elbowR = el; fistR = fist; }
            }

            BuildWheel(rock);

            // Luz de lava (ilumina el suelo y las flores) y brasas que suben
            var lg = new GameObject("LuzLava"); lg.transform.SetParent(torso, false); lg.transform.localPosition = new Vector3(0, 1.0f, 1.2f);
            coreLight = lg.AddComponent<Light>(); coreLight.type = LightType.Point; coreLight.color = new Color(1f, 0.45f, 0.12f); coreLight.range = 6f; coreLight.intensity = 2.2f;
            coreLight.shadows = LightShadows.None;
            embers = CineGlow.Motes("Brasas", torso, new Vector3(0, 1.0f, 0), new Color(3f, 0.9f, 0.2f), 9f, 0.12f, 0.6f, 2.2f, 0.9f, -0.12f);

            SetEyes(0f, true);
            lastPos = transform.position;
        }

        /// Forma de rueda: el gólem enrollado sobre sí mismo (anillo de rocas con grietas de lava y los ojos en el centro).
        void BuildWheel(Material rock)
        {
            wheel = LowPoly.Pivot("Rueda", transform, new Vector3(0, 1.55f, 0));
            var spin = LowPoly.Pivot("Giro", wheel, Vector3.zero);
            const float R = 1.55f;
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Vector3 p = new Vector3(0, Mathf.Cos(a) * R * 0.78f, Mathf.Sin(a) * R * 0.78f);
                Chunk(spin, "Segmento", p, new Vector3(1.45f, 0.95f, 0.95f), 100 + i, i % 3 == 0 ? StoneC : (i % 2 == 0 ? StoneA : StoneB), rock);
            }
            Chunk(spin, "Nucleo", Vector3.zero, new Vector3(1.3f, 1.6f, 1.6f), 120, StoneB, rock);
            var cr = new LowPoly().Paint(Color.white);
            for (int i = 0; i < 6; i++)
                cr.At(Vector3.zero, Quaternion.Euler(i * 30f, 0, 0), Vector3.one).Box(new Vector3(1.5f, 0.06f, R * 1.9f));
            cr.Spawn("GrietasRueda", spin, crackMat, default, false);
            Moss(spin, new Vector3(0.6f, 0.9f, 0.4f), new Vector3(0.6f, 0.3f, 0.7f), 121);
            Moss(spin, new Vector3(-0.6f, -0.7f, -0.6f), new Vector3(0.6f, 0.3f, 0.7f), 122);
            wheel.gameObject.SetActive(false);
        }

        void Chunk(Transform parent, string name, Vector3 pos, Vector3 scale, int seed, Color c, Material mat)
        {
            var lp = new LowPoly().Paint(c);
            lp.At(pos, Quaternion.Euler(seed * 13 % 30 - 15, seed * 47 % 360, seed * 7 % 20 - 10), scale).Rock(0.5f, seed, 0.2f);
            lp.Paint(Color.Lerp(c, Color.black, 0.15f));
            lp.At(pos + new Vector3(scale.x * 0.3f, scale.y * 0.25f, scale.z * 0.28f), Quaternion.Euler(0, seed * 31, 0), scale * 0.35f).Rock(0.5f, seed + 100, 0.25f, 0);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = lp.Build(name);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void Moss(Transform parent, Vector3 pos, Vector3 scale, int seed)
        {
            var lp = new LowPoly().Paint(seed % 2 == 0 ? MossA : MossB);
            lp.At(pos, Quaternion.Euler(0, seed * 29, 0), scale).Rock(0.5f, seed, 0.25f);
            var go = new GameObject("Musgo", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = lp.Build("Musgo");
            go.GetComponent<MeshRenderer>().sharedMaterial = IntroSet.VertexToon();
        }

        /// Hojas de otoño caídas sobre el gólem (lleva tiempo dormido en el bosque).
        void Leaves(Transform parent, Vector3 center, float radius, int count, int seed)
        {
            var rng = new System.Random(seed);
            var lp = new LowPoly();
            Color[] cols = { LeafA, LeafB, LeafC };
            for (int i = 0; i < count; i++)
            {
                lp.Paint(cols[rng.Next(cols.Length)]);
                float a = (float)rng.NextDouble() * 6.28f, d = (float)rng.NextDouble() * radius;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * d, (float)rng.NextDouble() * 0.08f, Mathf.Sin(a) * d * 0.6f);
                lp.At(p, Quaternion.Euler((float)rng.NextDouble() * 30f - 15f, a * 57f, (float)rng.NextDouble() * 30f - 15f), Vector3.one * 0.16f);
                Vector3 q0 = new Vector3(0, 0, -0.6f), q1 = new Vector3(0.35f, 0, 0), q2 = new Vector3(0, 0, 0.6f), q3 = new Vector3(-0.35f, 0, 0);
                lp.Tri(q0, q2, q1); lp.Tri(q0, q3, q2); lp.Tri(q0, q1, q2); lp.Tri(q0, q2, q3);
            }
            var go = new GameObject("Hojas", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = lp.Build("Hojas");
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = IntroSet.VertexToon(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Crack(Transform parent, Vector3 pos, Vector3 size, float roll)
        {
            var lp = new LowPoly().Paint(Color.white);
            lp.At(pos, Quaternion.Euler(0, 0, roll), Vector3.one).Box(size);
            lp.Spawn("Grieta", parent, crackMat, default, false);
        }

        // ------------------------------------------------------------------ control
        public void SetWalk(float speed01) => walkTarget = Mathf.Clamp01(speed01);
        public void SetEyes(float k, bool instant = false) { eyesTarget = k; if (instant) eyes = k; }
        public void SetCracks(float k) => cracksTarget = k;
        public void SetHunch(float k) => hunchTarget = k;
        public void LookAt(Vector3 world) { lookTarget = world; hasLook = true; }
        public void ClearLook() => hasLook = false;
        public void ShowOperation(bool on) => chestText.FadeTo(on ? 1f : 0f, 3f);
        public void SetOperationText(string text) { chestText.Text = text; }
        /// Agacharse (0 = de pie, 1 = en cuclillas con los puños en el suelo).
        public void SetCrouch(float k) => crouchTarget = k;
        /// Sostener algo con los dos puños sobre la cabeza (0..1).
        public void SetHold(float k) => holdTarget = k;
        public void SetArmsUp(float k) => armsUpTarget = k;
        public void SetHeal(float k) => healTarget = k;

        /// Un paso de persecución por fotograma: avanza "a tirones" sincronizado con las pisadas (sin patinar).
        public void StepToward(Vector3 target, float speed, float turnDegPerSec = 75f)
        {
            Vector3 d = target - transform.position; d.y = 0;
            if (d.magnitude < 0.05f) { SetWalk(0f); return; }
            SetWalk(1f);
            Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnDegPerSec * Time.deltaTime);
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, d.normalized));           // no avanza de lado
            float stepPush = StepPush;
            Vector3 p = transform.position + transform.forward * Mathf.Min(d.magnitude, speed * stepPush * facing * walk * Time.deltaTime);
            p.y = GroundY(p, transform.position.y);
            transform.position = p;
        }

        /// 0.4 en el contacto del pie, ~1.4 a mitad del paso: el cuerpo avanza cuando la pierna empuja.
        float StepPush => 0.4f + Mathf.Abs(Mathf.Sin(phase)) * 1.6f;

        /// Camina hasta un punto pegado al suelo, girando hacia él. Pasos pesados.
        public IEnumerator WalkTo(Vector3 target, float speed = 1.3f)
        {
            Busy = true;
            SetWalk(1f);
            while (true)
            {
                Vector3 d = target - transform.position; d.y = 0;
                if (d.magnitude < 0.08f) break;
                StepToward(target, speed, 70f);
                yield return null;
            }
            SetWalk(0f);
            Busy = false;
        }

        /// Pisotón: levanta la pierna derecha y la deja caer.
        public IEnumerator Stomp(float raise = 0.55f, float down = 0.12f)
        {
            Busy = true;
            stompTarget = 1f;
            yield return new WaitForSeconds(raise);
            stompTarget = -0.15f;
            yield return new WaitForSeconds(down);
            CameraShake3D.Shake(0.35f, 0.28f);
            DustPuff(kneeR.position + transform.forward * 0.3f + Vector3.down * 0.7f, 26);
            stompTarget = 0f;
            Busy = false;
        }

        /// Golpe con los dos puños contra el suelo. <paramref name="onImpact"/> se llama en el instante del golpe.
        public IEnumerator Smash(Action<Vector3> onImpact = null, float windup = 0.55f)
        {
            Busy = true;
            armsUpTarget = 1f; hunchTarget = -0.2f;
            yield return new WaitForSeconds(windup);
            armsUpTarget = -0.45f; hunchTarget = 0.9f; crouchTarget = 0.35f;
            yield return new WaitForSeconds(0.16f);
            Vector3 hit = transform.position + transform.forward * 1.9f;
            hit.y = GroundY(hit, transform.position.y);
            CameraShake3D.Shake(0.4f, 0.3f);
            DustPuff(hit, 30);
            onImpact?.Invoke(hit);
            yield return new WaitForSeconds(0.45f);
            armsUpTarget = 0f; hunchTarget = 0f; crouchTarget = 0f;
            yield return new WaitForSeconds(0.25f);
            Busy = false;
        }

        /// Rugido: echa la cabeza atrás, abre los brazos y las grietas se avivan.
        public IEnumerator Roar(float seconds = 1.4f)
        {
            Busy = true;
            roarTarget = 1f; cracksTarget = 2.2f;
            embers.Emit(30);
            CameraShake3D.Shake(0.12f, seconds * 0.8f);
            yield return new WaitForSeconds(seconds);
            roarTarget = 0f; cracksTarget = 1f;
            Busy = false;
        }

        /// Tambaleo al recibir un golpe fuerte (o al romperse su coraza).
        public void Stagger(float k = 1f) { stagger = Mathf.Max(stagger, k); }

        /// Se enrolla en forma de rueda (true) o vuelve a ponerse de pie (false). Una nube de polvo tapa el cambio.
        public IEnumerator Curl(bool toWheel)
        {
            Busy = true;
            if (toWheel)
            {
                crouchTarget = 1f; hunchTarget = 1f; armsUpTarget = -0.2f;
                yield return new WaitForSeconds(0.45f);
                DustPuff(transform.position + Vector3.up * 1f, 40);
                body.gameObject.SetActive(false);
                wheel.gameObject.SetActive(true);
                curl = curlTarget = 1f;
            }
            else
            {
                DustPuff(transform.position + Vector3.up * 1f, 40);
                wheel.gameObject.SetActive(false);
                body.gameObject.SetActive(true);
                curl = curlTarget = 0f;
                crouch = 1f; crouchTarget = 0f; hunchTarget = 0f; armsUpTarget = 0f;
                yield return new WaitForSeconds(0.5f);
            }
            Busy = false;
        }

        /// Gira la rueda según la distancia recorrida (llamar mientras rueda).
        public void SpinWheel(float distance)
        {
            if (wheel == null) return;
            var spin = wheel.GetChild(0);
            spin.localRotation = Quaternion.Euler(distance / 1.55f * Mathf.Rad2Deg, 0, 0) * Quaternion.identity;
            wheel.localPosition = new Vector3(0, 1.55f + Mathf.Abs(Mathf.Sin(distance * 1.7f)) * 0.08f, 0);
        }

        public void DustPuff(Vector3 at, int count)
        {
            var ps = CineGlow.Motes("Polvo", null, at, new Color(0.35f, 0.3f, 0.25f), 0f, 0.7f, 2.2f, 1.3f, 0.5f, -0.02f);
            CineGlow.SetIntensity(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial, 0.6f);
            ps.Emit(count);
            Destroy(ps.gameObject, 2f);
        }

        /// El gólem se desmorona: las rocas se sueltan, caen y se hunden en el suelo; las grietas se apagan.
        public IEnumerator Crumble()
        {
            SetEyes(0f); SetCracks(0f); SetWalk(0f);
            enabled = false;
            CameraShake3D.Shake(0.3f, 0.6f);
            DustPuff(transform.position + Vector3.up * 0.5f, 50);
            if (embers != null) embers.Stop();
            CineGlow.SetIntensity(eyeMat, 0f); CineGlow.SetIntensity(crackMat, 0.1f); CineGlow.SetIntensity(chestMat, 0f);
            if (chestText != null) chestText.gameObject.SetActive(false);
            if (coreLight != null) coreLight.enabled = false;
            var rocks = GetComponentsInChildren<MeshRenderer>();
            var vel = new Vector3[rocks.Length];
            for (int i = 0; i < rocks.Length; i++)
            {
                rocks[i].transform.SetParent(null, true);
                vel[i] = (rocks[i].transform.position - transform.position - Vector3.up * 1.5f).normalized * UnityEngine.Random.Range(1f, 3f) + Vector3.up * UnityEngine.Random.Range(1f, 3f);
            }
            float ground = GroundY(transform.position, transform.position.y);
            for (float t = 0; t < 2.4f; t += Time.deltaTime)
            {
                for (int i = 0; i < rocks.Length; i++)
                {
                    var r = rocks[i]; if (r == null) continue;
                    Vector3 p = r.transform.position;
                    if (p.y > ground + 0.3f) { vel[i] += Physics.gravity * Time.deltaTime; p += vel[i] * Time.deltaTime; r.transform.Rotate(vel[i] * 40f * Time.deltaTime); }
                    if (t > 1.5f) p.y -= Time.deltaTime * 0.7f;
                    r.transform.position = p;
                }
                yield return null;
            }
            foreach (var r in rocks) if (r != null) Destroy(r.gameObject);
        }

        static float GroundY(Vector3 p, float fallback) => RuntimeMaterials.Ground(p, out Vector3 hit) ? hit.y : fallback;

        // ------------------------------------------------------------------ animación procedural
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 dp = transform.position - lastPos; dp.y = 0; lastPos = transform.position;
            planarSpeed = Mathf.Lerp(planarSpeed, dp.magnitude / dt, 1f - Mathf.Exp(-8f * dt));

            walk = Mathf.MoveTowards(walk, walkTarget, dt * 2.5f);
            phase += dt * 3.2f * Mathf.Max(walk, 0.0001f);
            idleT += dt;
            eyes = Mathf.MoveTowards(eyes, eyesTarget, dt * 3f);
            cracks = Mathf.MoveTowards(cracks, cracksTarget, dt * 2f);
            armsUp = Damp(armsUp, armsUpTarget, armsUpTarget < armsUp ? 22f : 5f, dt);
            stompLeg = Damp(stompLeg, stompTarget, stompTarget < stompLeg ? 25f : 6f, dt);
            hunch = Damp(hunch, hunchTarget, 4f, dt);
            roar = Damp(roar, roarTarget, 4f, dt);
            crouch = Damp(crouch, crouchTarget, 5f, dt);
            hold = Damp(hold, holdTarget, 6f, dt);
            heal = Damp(heal, healTarget, 3f, dt);
            stagger = Mathf.MoveTowards(stagger, 0f, dt * 1.6f);

            if (curl > 0.5f) { UpdateGlow(); return; }      // en forma de rueda la anima SpinWheel

            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float w = walk;
            // Idle: cambio de peso lento de una pierna a otra (solo parado).
            float idleShift = Mathf.Sin(idleT * 0.45f) * (1f - w);

            // ---- Piernas: muslo adelante/atrás; la rodilla se dobla en la fase de balanceo (pie en el aire).
            // fwd = cuánto adelanta cada pierna (piernas en oposición); en el balanceo (la pierna avanza) se dobla la rodilla.
            float fwdL = s * 26f * w, fwdR = -s * 26f * w;
            float swingL = Mathf.Max(0f, c), swingR = Mathf.Max(0f, -c);
            legL.localRotation = Quaternion.Euler(-fwdL - crouch * 55f, 0, -idleShift * 2f);
            legR.localRotation = Quaternion.Euler(-fwdR - stompLeg * 55f - crouch * 55f, 0, -idleShift * 2f);
            kneeL.localRotation = Quaternion.Euler(swingL * 38f * w + crouch * 95f, 0, 0);
            kneeR.localRotation = Quaternion.Euler(swingR * 38f * w + stompLeg * 70f + crouch * 95f, 0, 0);

            // ---- Cadera: baja en cada pisada (dos veces por ciclo) y se desplaza sobre la pierna de apoyo.
            float bob = (1f - Mathf.Abs(c)) * 0.13f * w;                          // más alta a mitad de paso
            float sway = s * 0.12f * w + idleShift * 0.06f;
            hips.localPosition = new Vector3(sway, 1.6f - 0.13f * w + bob - crouch * 0.75f + stompLeg * 0.12f, 0);
            hips.localRotation = Quaternion.Euler(0, s * 7f * w, -s * 4f * w - idleShift * 2f + Mathf.Sin(stagger * 20f) * 6f * stagger);

            // ---- Torso: inclinado al andar, respira, gira contra la cadera (contrapeso).
            float breathe = Mathf.Sin(idleT * 1.3f) * 1.2f;
            float torsoPitch = 10f * hunch + 7f * w - 18f * roar + breathe - armsUp * 8f + crouch * 30f - hold * 6f - heal * 12f - stagger * 10f;
            torso.localRotation = Quaternion.Euler(torsoPitch, -s * 9f * w, c * 3f * w + idleShift * 1.5f);
            torso.localScale = new Vector3(1f, 1f + Mathf.Sin(idleT * 1.3f) * 0.012f, 1f);

            // ---- Brazos: cuelgan pesados con inercia (resorte) y balanceo opuesto a las piernas.
            float swingArm = s * 20f * w;                                             // brazo izquierdo atrás cuando avanza la pierna izquierda
            Spring(ref armLagL, ref armLagLv, swingArm, dt);
            Spring(ref armLagR, ref armLagRv, -swingArm, dt);
            float up = armsUp;
            float holdUp = hold * 175f;                                               // sostener sobre la cabeza
            float crouchArms = crouch * 55f;                                          // manos al suelo
            shoulderL.localRotation = Quaternion.Euler(armLagL - up * 160f - roar * 40f - holdUp - crouchArms - heal * 60f, 0, -6f - roar * 35f + hold * 22f - heal * 30f);
            shoulderR.localRotation = Quaternion.Euler(armLagR - up * 160f - roar * 40f - holdUp - crouchArms - heal * 60f, 0, 6f + roar * 35f - hold * 22f + heal * 30f);
            float elbowBend = -14f - up * 30f - hold * 70f - Mathf.Abs(armLagL) * 0.3f;
            elbowL.localRotation = Quaternion.Euler(elbowBend, 0, hold * 25f);
            elbowR.localRotation = Quaternion.Euler(elbowBend, 0, -hold * 25f);

            // ---- Cabeza: mira al objetivo con un giro lento y mecánico; de vez en cuando mira alrededor en reposo.
            float wantYaw = Mathf.Sin(idleT * 0.3f) * 18f * (1f - w), wantPitch = 0f;
            if (hasLook)
            {
                Vector3 local = torso.InverseTransformPoint(lookTarget) - head.localPosition;
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25f, 30f);
            }
            headYaw = Mathf.MoveTowardsAngle(headYaw, wantYaw, dt * 55f);
            headPitch = Mathf.MoveTowards(headPitch, wantPitch - roar * 25f - heal * 15f, dt * 60f);
            head.localRotation = Quaternion.Euler(headPitch, headYaw, Mathf.Sin(stagger * 17f) * 10f * stagger);

            UpdateGlow();
        }

        void UpdateGlow()
        {
            float flick = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0.3f);
            float k = cracks + heal * 1.5f;
            CineGlow.SetIntensity(eyeMat, eyes * (0.9f + 0.1f * Mathf.Sin(Time.time * 9f)));
            CineGlow.SetIntensity(crackMat, k * flick);
            CineGlow.SetIntensity(chestMat, k * 0.9f * flick);
            if (coreLight != null)
            {
                coreLight.intensity = 1.6f * k * flick + eyes * 0.4f;
                coreLight.color = Color.Lerp(new Color(1f, 0.45f, 0.12f), new Color(0.55f, 1f, 0.35f), Mathf.Clamp01(heal));
            }
        }

        static void Spring(ref float x, ref float v, float target, float dt)
        {
            // resorte amortiguado: sigue al objetivo con un pequeño retraso y rebote (inercia de los brazos)
            const float k = 40f, damping = 9f;
            v += (k * (target - x) - damping * v) * dt;
            x += v * dt;
        }

        static float Damp(float a, float b, float k, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-k * dt));

        void OnDestroy()
        {
            if (eyeMat != null) Destroy(eyeMat);
            if (crackMat != null) Destroy(crackMat);
            if (chestMat != null) Destroy(chestMat);
        }
    }
}
