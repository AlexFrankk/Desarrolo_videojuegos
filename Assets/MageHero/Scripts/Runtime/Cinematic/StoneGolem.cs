using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Gólem de Piedra: primer sirviente del Gran Cero. Rocas unidas por grietas de lava, ojos de magma y una
    /// operación ardiendo en el pecho (la llave para vencerlo).
    /// Modelo construido por código + animación procedural por articulaciones (pesada, mecánica):
    /// caminar, pisotón, golpe con los puños, mirar a un objetivo y encender los ojos.
    public class StoneGolem : MonoBehaviour
    {
        public string operation = "8 + 5";
        public int answer = 13;

        // Articulaciones
        Transform body, hips, torso, head, legL, legR, shoulderL, shoulderR, elbowL, elbowR;
        Material eyeMat, crackMat, chestMat;
        Light coreLight;
        GlowNumber chestText;
        ParticleSystem embers;

        // Estado de animación
        float walk, walkTarget, phase, eyes, eyesTarget, cracks = 1f, cracksTarget = 1f;
        float armsUp, armsUpTarget, stompLeg, stompTarget, hunch, hunchTarget, roar, roarTarget;
        Vector3 lookTarget; bool hasLook;
        float headYaw, headPitch;
        public float Height => 4.4f;
        public bool Busy { get; private set; }
        public Transform Head => head;
        public Transform Chest => torso;

        static readonly Color StoneA = MenuStyle.Hex(0x9a9189), StoneB = MenuStyle.Hex(0x7d746b), StoneC = MenuStyle.Hex(0xb0a69a);
        static readonly Color Lava = new Color(3.2f, 1.05f, 0.22f), Eye = new Color(4.5f, 1.6f, 0.3f);

        public static StoneGolem Create(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Golem_DePiedra");
            go.transform.SetPositionAndRotation(position, rotation);
            var g = go.AddComponent<StoneGolem>();
            g.BuildModel();
            return g;
        }

        // ------------------------------------------------------------------ modelo
        void BuildModel()
        {
            crackMat = CineGlow.Make(Lava, 0f, 1f, 0.35f);
            eyeMat = CineGlow.Make(Eye, 0f, 1f, 0.1f);
            var rock = IntroSet.VertexToon();

            body = LowPoly.Pivot("Cuerpo", transform, Vector3.zero);
            hips = LowPoly.Pivot("Cadera", body, new Vector3(0, 1.55f, 0));
            Chunk(hips, "Pelvis", new Vector3(0, 0, 0), new Vector3(1.25f, 0.6f, 0.85f), 0.55f, 1, StoneB, rock);

            // Piernas: muslo + pie (de un solo pivote, andar rígido y pesado)
            legL = LowPoly.Pivot("Pierna_L", hips, new Vector3(-0.5f, -0.1f, 0));
            legR = LowPoly.Pivot("Pierna_R", hips, new Vector3(0.5f, -0.1f, 0));
            foreach (var (leg, s) in new[] { (legL, 2), (legR, 3) })
            {
                Chunk(leg, "Muslo", new Vector3(0, -0.5f, 0), new Vector3(0.75f, 0.95f, 0.75f), 0.5f, 10 + s, StoneA, rock);
                Chunk(leg, "Pie", new Vector3(0, -1.25f, 0.12f), new Vector3(0.95f, 0.55f, 1.15f), 0.45f, 20 + s, StoneB, rock);
                Crack(leg, new Vector3(0, -0.95f, 0.18f), new Vector3(0.5f, 0.05f, 0.4f), 15f);
            }

            // Torso: gran roca + hombreras + grietas de lava
            torso = LowPoly.Pivot("Torso", hips, new Vector3(0, 0.25f, 0));
            Chunk(torso, "Pecho", new Vector3(0, 1.05f, 0), new Vector3(1.85f, 1.45f, 1.25f), 0.8f, 31, StoneA, rock);
            Chunk(torso, "Espalda", new Vector3(0, 1.35f, -0.45f), new Vector3(1.6f, 1.1f, 0.9f), 0.7f, 32, StoneB, rock);
            Chunk(torso, "Hombrera_L", new Vector3(-1.05f, 1.65f, 0), new Vector3(0.9f, 0.7f, 0.9f), 0.5f, 33, StoneC, rock);
            Chunk(torso, "Hombrera_R", new Vector3(1.05f, 1.65f, 0), new Vector3(0.9f, 0.7f, 0.9f), 0.5f, 34, StoneC, rock);
            // grietas: venas de lava sobre el pecho
            Crack(torso, new Vector3(0, 0.25f, 0.05f), new Vector3(1.1f, 0.07f, 0.8f), 0f);          // cintura (entre pelvis y torso)
            Crack(torso, new Vector3(-0.35f, 1.55f, 0.62f), new Vector3(0.05f, 0.7f, 0.05f), 25f);
            Crack(torso, new Vector3(0.42f, 1.5f, 0.6f), new Vector3(0.05f, 0.55f, 0.05f), -30f);
            Crack(torso, new Vector3(0.1f, 0.55f, 0.62f), new Vector3(0.75f, 0.05f, 0.05f), 8f);
            Crack(torso, new Vector3(-0.75f, 1.1f, 0.4f), new Vector3(0.05f, 0.6f, 0.05f), -12f);
            // placa del pecho con la operación
            // grieta ancha e irregular (no un cartel): varias losas finas de lava rojiza alrededor de la operación
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

            // Cabeza pequeña y hundida entre los hombros (aspecto tosco), ojos de magma
            head = LowPoly.Pivot("Cabeza", torso, new Vector3(0, 1.95f, 0.3f));
            Chunk(head, "Craneo", new Vector3(0, 0.22f, 0), new Vector3(0.75f, 0.6f, 0.7f), 0.5f, 41, StoneC, rock);
            Chunk(head, "Ceja", new Vector3(0, 0.36f, 0.25f), new Vector3(0.8f, 0.2f, 0.35f), 0.4f, 42, StoneB, rock);
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
                Chunk(sh, "Brazo", new Vector3(side * 0.12f, -0.45f, 0), new Vector3(0.6f, 0.9f, 0.6f), 0.5f, 50 + side, StoneA, rock);
                var el = LowPoly.Pivot("Codo", sh, new Vector3(side * 0.15f, -0.95f, 0));
                Chunk(el, "Antebrazo", new Vector3(0, -0.4f, 0.05f), new Vector3(0.62f, 0.85f, 0.62f), 0.5f, 60 + side, StoneB, rock);
                Chunk(el, "Puno", new Vector3(0, -1.0f, 0.1f), new Vector3(0.95f, 0.85f, 0.95f), 0.55f, 70 + side, StoneC, rock);
                Crack(el, new Vector3(0, 0.0f, 0.05f), new Vector3(0.45f, 0.05f, 0.45f), 0f);
                if (side < 0) elbowL = el; else elbowR = el;
            }

            // Luz de lava (ilumina el suelo y las flores) y brasas que suben
            var lg = new GameObject("LuzLava"); lg.transform.SetParent(torso, false); lg.transform.localPosition = new Vector3(0, 1.0f, 1.2f);
            coreLight = lg.AddComponent<Light>(); coreLight.type = LightType.Point; coreLight.color = new Color(1f, 0.45f, 0.12f); coreLight.range = 6f; coreLight.intensity = 2.2f;
            coreLight.shadows = LightShadows.None;
            embers = CineGlow.Motes("Brasas", torso, new Vector3(0, 1.0f, 0), new Color(3f, 0.9f, 0.2f), 9f, 0.12f, 0.6f, 2.2f, 0.9f, -0.12f);

            SetEyes(0f, true);
        }

        void Chunk(Transform parent, string name, Vector3 pos, Vector3 scale, float radius, int seed, Color c, Material mat)
        {
            var lp = new LowPoly().Paint(c);
            lp.At(pos, Quaternion.Euler(seed * 13 % 30 - 15, seed * 47 % 360, seed * 7 % 20 - 10), scale).Rock(radius / Mathf.Max(0.001f, radius) * 0.5f, seed, 0.2f);
            // detalle: un par de piedras más pequeñas incrustadas
            lp.Paint(Color.Lerp(c, Color.black, 0.15f));
            lp.At(pos + new Vector3(scale.x * 0.3f, scale.y * 0.25f, scale.z * 0.28f), Quaternion.Euler(0, seed * 31, 0), scale * 0.35f).Rock(0.5f, seed + 100, 0.25f, 0);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = lp.Build(name);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
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

        /// Camina hasta un punto pegado al suelo, girando hacia él. Pasos pesados.
        public IEnumerator WalkTo(Vector3 target, float speed = 1.3f)
        {
            Busy = true;
            SetWalk(1f);
            while (true)
            {
                Vector3 d = target - transform.position; d.y = 0;
                if (d.magnitude < 0.08f) break;
                Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 70f * Time.deltaTime);
                // avance a tirones: más rápido a mitad de cada paso (sensación de peso)
                float stepPush = 0.45f + 0.9f * Mathf.Abs(Mathf.Sin(phase));
                Vector3 p = transform.position + transform.forward * Mathf.Min(d.magnitude, speed * stepPush * Time.deltaTime);
                p.y = GroundY(p, transform.position.y);
                transform.position = p;
                yield return null;
            }
            SetWalk(0f);
            Busy = false;
        }

        /// Pisotón: levanta la pierna derecha y la deja caer (con temblor de cámara si se pide).
        public IEnumerator Stomp(float raise = 0.55f, float down = 0.12f)
        {
            Busy = true;
            stompTarget = 1f;
            yield return new WaitForSeconds(raise);
            stompTarget = -0.15f;
            yield return new WaitForSeconds(down);
            CameraShake3D.Shake(0.35f, 0.28f);
            DustPuff(legR.position + transform.forward * 0.3f + Vector3.down * 1.4f, 26);
            stompTarget = 0f;
            Busy = false;
        }

        /// Golpe con los dos puños (contra un árbol o el suelo).
        public IEnumerator Smash()
        {
            Busy = true;
            armsUpTarget = 1f;
            yield return new WaitForSeconds(0.55f);
            armsUpTarget = -0.4f;
            yield return new WaitForSeconds(0.18f);
            CameraShake3D.Shake(0.4f, 0.3f);
            DustPuff(transform.position + transform.forward * 1.6f, 30);
            yield return new WaitForSeconds(0.3f);
            armsUpTarget = 0f;
            Busy = false;
        }

        /// Rugido silencioso: echa la cabeza atrás, abre los brazos y las grietas se avivan.
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

        public void DustPuff(Vector3 at, int count)
        {
            var ps = CineGlow.Motes("Polvo", null, at, new Color(0.35f, 0.3f, 0.25f), 0f, 0.7f, 2.2f, 1.3f, 0.5f, -0.02f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            // polvo: material aditivo muy tenue (no oscurece, pero se ve la nube contra la luz)
            CineGlow.SetIntensity(r.sharedMaterial, 0.6f);
            ps.Emit(count);
            Destroy(ps.gameObject, 2f);
        }

        static float GroundY(Vector3 p, float fallback)
        {
            return RuntimeMaterials.Ground(p, out Vector3 hit) ? hit.y : fallback;
        }

        // ------------------------------------------------------------------ animación procedural
        void Update()
        {
            float dt = Time.deltaTime;
            walk = Mathf.MoveTowards(walk, walkTarget, dt * 2.5f);
            phase += dt * 3.4f * Mathf.Max(walk, 0.0001f);
            eyes = Mathf.MoveTowards(eyes, eyesTarget, dt * 3f);
            cracks = Mathf.MoveTowards(cracks, cracksTarget, dt * 2f);
            armsUp = Damp(armsUp, armsUpTarget, armsUpTarget < armsUp ? 22f : 5f, dt);
            stompLeg = Damp(stompLeg, stompTarget, stompTarget < stompLeg ? 25f : 6f, dt);
            hunch = Damp(hunch, hunchTarget, 3f, dt);
            roar = Damp(roar, roarTarget, 4f, dt);

            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            // Piernas rígidas, balanceo lateral del torso y bajada en cada paso.
            legL.localRotation = Quaternion.Euler(s * 24f * walk, 0, 0);
            legR.localRotation = Quaternion.Euler(-s * 24f * walk - stompLeg * 55f, 0, 0);
            hips.localPosition = new Vector3(0, 1.55f - Mathf.Abs(c) * 0.12f * walk + stompLeg * 0.12f, 0);
            hips.localRotation = Quaternion.Euler(0, s * 6f * walk, c * 4f * walk);
            float breathe = Mathf.Sin(Time.time * 1.3f) * 0.6f;
            torso.localRotation = Quaternion.Euler(10f * hunch + 6f * walk - 18f * roar + breathe - armsUp * 6f, -s * 5f * walk, -c * 3f * walk);

            // Brazos: cuelgan pesados, balanceo opuesto a las piernas; golpe: arriba y abajo.
            float swing = -s * 18f * walk;
            float up = armsUp;
            shoulderL.localRotation = Quaternion.Euler(swing - up * 160f - roar * 40f, 0, -6f - roar * 35f);
            shoulderR.localRotation = Quaternion.Euler(-swing - up * 160f - roar * 40f, 0, 6f + roar * 35f);
            elbowL.localRotation = Quaternion.Euler(-12f - up * 30f, 0, 0);
            elbowR.localRotation = Quaternion.Euler(-12f - up * 30f, 0, 0);

            // Cabeza: mira al objetivo (giro lento y mecánico).
            float wantYaw = 0f, wantPitch = 0f;
            if (hasLook)
            {
                Vector3 local = torso.InverseTransformPoint(lookTarget) - head.localPosition;
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25f, 30f);
            }
            headYaw = Mathf.MoveTowardsAngle(headYaw, wantYaw, dt * 55f);
            headPitch = Mathf.MoveTowards(headPitch, wantPitch - roar * 25f, dt * 60f);
            head.localRotation = Quaternion.Euler(headPitch, headYaw, 0);

            // Brillos
            float flick = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0.3f);
            CineGlow.SetIntensity(eyeMat, eyes * (0.9f + 0.1f * Mathf.Sin(Time.time * 9f)));
            CineGlow.SetIntensity(crackMat, cracks * flick);
            CineGlow.SetIntensity(chestMat, cracks * 0.9f * flick);
            coreLight.intensity = 1.6f * cracks * flick + eyes * 0.4f;
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
