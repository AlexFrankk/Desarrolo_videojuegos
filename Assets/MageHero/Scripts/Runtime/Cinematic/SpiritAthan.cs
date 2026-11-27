using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Espíritu del Maestro Athan: una esfera de luz viva (un "fuego fatuo"), azul celeste con destellos dorados.
    /// Núcleo blanco muy brillante, capa con borde luminoso, halo suave siempre de cara a la cámara,
    /// pequeñas chispas que orbitan a su alrededor dejando estela y motas de luz que se desprenden.
    /// No tiene cuerpo: "señala" enviando una de sus chispas hasta un punto del mundo (Reach), "asiente" con un
    /// pequeño rebote, se inclina hacia lo que mira y late con más fuerza mientras habla (voz en off del tutorial).
    /// Mantiene la misma API que el antiguo modelo humanoide, así la cinemática no cambia.
    public class SpiritAthan : MonoBehaviour
    {
        Transform body, core, shell, halo, pointer, orbitRoot;
        Transform[] satellites;
        Material coreMat, shellMat, haloMat, satMat, pointerMat, pointerHaloMat;
        ParticleSystem motes, trail, pointerSparks;
        Light glow;

        const float OrbHeight = 1.25f;              // altura del núcleo sobre el pivote (el pivote flota 0.28 sobre el suelo)
        const int SatelliteCount = 5;
        const float HaloSize = 1.1f;

        float floatPhase, nod, nodTarget, tilt, tiltTarget, unease, aura = 1f, auraTarget = 1f, talk, talkTarget, talkPhase;
        Vector3? reachTarget;
        Vector3 pointerPos;
        Vector3 lookTarget; bool hasLook;
        Vector3 lean;

        /// La "mano" es la chispa que sale a señalar; Head es el centro del orbe (los planos de cámara se encuadran ahí).
        public Transform HandR => pointer;
        public Transform HandL => pointer;
        public Transform Head => core;

        static readonly Color CoreC = new Color(3.4f, 3.8f, 4.4f), ShellC = new Color(0.45f, 1.35f, 3.0f),
                              HaloC = new Color(0.35f, 0.95f, 2.2f), Gold = new Color(2.6f, 2.0f, 0.9f);

        public static SpiritAthan Create(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Espiritu_MaestroAthan");
            go.transform.SetPositionAndRotation(position, rotation);
            var s = go.AddComponent<SpiritAthan>();
            s.BuildModel();
            return s;
        }

        void BuildModel()
        {
            coreMat = CineGlow.Make(CoreC, 0.4f, 1f, 0.05f);
            shellMat = CineGlow.Make(ShellC, 2.6f, 0.22f, 0.1f);
            haloMat = CineGlow.Make(HaloC, 0f, 1f, 0.08f, CineGlow.SoftDot);
            satMat = CineGlow.Make(new Color(1.6f, 2.6f, 3.6f), 0.5f, 1f, 0.2f);
            pointerMat = CineGlow.Make(Gold * 1.2f, 0.5f, 1f, 0.15f);

            body = LowPoly.Pivot("Orbe", transform, Vector3.zero);

            // Núcleo y capa exterior (esferas suaves)
            core = LowPoly.Pivot("Nucleo", body, new Vector3(0, OrbHeight, 0));
            Sphere("NucleoMesh", core, 0.15f, coreMat, 3);
            shell = LowPoly.Pivot("Capa", core, Vector3.zero);
            Sphere("CapaMesh", shell, 0.27f, shellMat, 3);

            // Halo: un disco de luz suave que siempre mira a la cámara
            halo = Quad("Halo", core, HaloSize, haloMat).transform;
            halo.gameObject.AddComponent<Billboard>();

            // Chispas en órbita (cada una en su propio plano inclinado) con estela
            orbitRoot = LowPoly.Pivot("Orbitas", core, Vector3.zero);
            satellites = new Transform[SatelliteCount];
            for (int i = 0; i < SatelliteCount; i++)
            {
                var plane = LowPoly.Pivot("Plano_" + i, orbitRoot, Vector3.zero);
                plane.localRotation = Quaternion.Euler(25f + i * 31f, i * 72f, i * 17f);
                var sat = LowPoly.Pivot("Chispa_" + i, plane, new Vector3(0.42f + (i % 2) * 0.08f, 0, 0));
                Sphere("ChispaMesh", sat, 0.035f, satMat, 1);
                var st = CineGlow.Motes("Estela", sat, Vector3.zero, new Color(0.7f, 1.7f, 3.2f), 38f, 0.06f, 0.02f, 0.45f, 0.005f, 0f);
                var sm = st.main; sm.startSpeed = 0f;
                satellites[i] = sat;
            }

            // La chispa dorada que sale a señalar / dibujar números en el aire / tocar el pecho del joven
            pointer = LowPoly.Pivot("ChispaDorada", transform, Vector3.zero);
            Sphere("ChispaDoradaMesh", pointer, 0.055f, pointerMat, 2);
            pointerHaloMat = CineGlow.Make(Gold, 0f, 1f, 0.1f, CineGlow.SoftDot);
            var ph = Quad("HaloChispa", pointer, 0.32f, pointerHaloMat);
            ph.AddComponent<Billboard>();
            pointerSparks = CineGlow.Motes("ChispasMano", pointer, Vector3.zero, Gold * 1.6f, 0f, 0.06f, 0.3f, 0.8f, 0.03f, 0.1f);
            var ptrail = CineGlow.Motes("EstelaDorada", pointer, Vector3.zero, Gold, 30f, 0.05f, 0.02f, 0.5f, 0.005f, 0f);
            var pm = ptrail.main; pm.startSpeed = 0f;
            pointerPos = RestPointer();
            pointer.position = pointerPos;

            // Motas que se desprenden del orbe y una estela suave que queda al moverse
            motes = CineGlow.Motes("MotasDeLuz", core, Vector3.zero, new Color(0.6f, 1.4f, 2.6f), 26f, 0.07f, 0.25f, 1.8f, 0.22f, -0.04f);
            trail = CineGlow.Motes("Estela", core, Vector3.zero, new Color(0.4f, 1.1f, 2.4f), 18f, 0.22f, 0.03f, 1.1f, 0.12f, 0f);

            var lg = new GameObject("LuzEspiritu"); lg.transform.SetParent(core, false);
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(0.55f, 0.85f, 1f); glow.range = 5f; glow.intensity = 1.6f;
            glow.shadows = LightShadows.None;
        }

        static void Sphere(string name, Transform parent, float radius, Material mat, int subdiv)
        {
            var lp = new LowPoly().Paint(Color.white);
            lp.Rock(radius, 1, 0f, subdiv);
            lp.Spawn(name, parent, mat, default, false);
        }

        static Mesh quadMesh;
        /// Disco de luz (quad de doble cara con UV completas para el punto suave). Con Billboard siempre mira a la cámara.
        static GameObject Quad(string name, Transform parent, float size, Material mat)
        {
            if (quadMesh == null)
            {
                quadMesh = new Mesh { name = "HaloQuad" };
                quadMesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), new Vector3(.5f, -.5f, 0) };
                quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
                quadMesh.RecalculateNormals();
                quadMesh.RecalculateBounds();
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<MeshFilter>().sharedMesh = quadMesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ control
        /// Envía la chispa dorada a un punto del mundo. null = vuelve a orbitar junto al orbe.
        public void Reach(Vector3? world, bool right = true) { reachTarget = world; }
        public void Look(Vector3 world) { lookTarget = world; hasLook = true; }
        public void ClearLook() => hasLook = false;
        public void Tilt(float degrees) => tiltTarget = degrees;
        /// Inquietud: el brillo parpadea y las chispas orbitan nerviosas.
        public void SetUnease(float k) => unease = Mathf.Clamp01(k);
        public void SetAura(float k) => auraTarget = k;
        public void Sparks(bool on) { var em = pointerSparks.emission; em.rateOverTime = on ? 60f : 0f; }
        /// Late al ritmo de la voz (tutorial).
        public void SetTalking(bool on) => talkTarget = on ? 1f : 0f;

        /// "Asiente": un pequeño rebote vertical (n veces).
        public IEnumerator Nod(int times = 1, float speed = 1f)
        {
            for (int i = 0; i < times; i++)
            {
                nodTarget = 18f; yield return new WaitForSeconds(0.32f / speed);
                nodTarget = -3f; yield return new WaitForSeconds(0.32f / speed);
            }
            nodTarget = 0f;
        }

        /// Se desliza flotando hasta un punto.
        public IEnumerator GlideTo(Vector3 target, float seconds, Vector3? faceTowards = null)
        {
            Vector3 from = transform.position;
            Quaternion r0 = transform.rotation;
            Quaternion r1 = r0;
            if (faceTowards.HasValue) { Vector3 d = faceTowards.Value - target; d.y = 0; if (d.sqrMagnitude > 0.01f) r1 = Quaternion.LookRotation(d); }
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(from, target, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.25f;   // pequeño arco
                transform.rotation = Quaternion.Slerp(r0, r1, e);
                yield return null;
            }
            transform.position = target; transform.rotation = r1;
        }

        /// Traza un número/operación en el aire con la chispa dorada mientras aparece el texto de luz.
        public IEnumerator DrawInAir(string text, Vector3 world, float seconds, System.Action<GlowNumber> created)
        {
            var gn = CineGlow.Number(text, null, world, new Color(2.6f, 1.9f, 0.7f), 0.55f);
            gn.FadeTo(0f, 1f);
            created?.Invoke(gn);
            Sparks(true);
            Vector3 right = Camera.main != null ? Camera.main.transform.right : transform.right;
            float w = 0.18f * text.Length;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                Vector3 p = world - right * w * 0.5f + right * w * t + Vector3.up * Mathf.Sin(t * Mathf.PI * text.Length * 2f) * 0.12f;
                Reach(p);
                gn.FadeTo(Mathf.Clamp01(t * 1.4f), 4f);
                yield return null;
            }
            Sparks(false);
            gn.FadeTo(1f, 2f);
        }

        // ------------------------------------------------------------------ animación
        Vector3 RestPointer()
        {
            // en reposo la chispa dorada da vueltas cerca del orbe
            float a = floatPhase * 2.1f;
            return core.position + new Vector3(Mathf.Cos(a) * 0.36f, Mathf.Sin(floatPhase * 1.3f) * 0.12f + 0.05f, Mathf.Sin(a) * 0.36f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            floatPhase += dt;
            nod = Mathf.Lerp(nod, nodTarget, 1f - Mathf.Exp(-9f * dt));
            tilt = Mathf.Lerp(tilt, tiltTarget, 1f - Mathf.Exp(-4f * dt));
            talk = Mathf.MoveTowards(talk, talkTarget, dt * 3f);
            talkPhase += dt * 9f;

            // Flota con un vaivén lento; "asentir" = bajar y subir un poco; "inclinarse" = desplazarse de lado.
            float bob = Mathf.Sin(floatPhase * 1.4f) * 0.07f;
            body.localPosition = new Vector3(0, 0.28f + bob - nod * 0.006f, 0);
            Vector3 wantLean = Vector3.zero;
            if (hasLook)
            {
                Vector3 d = lookTarget - core.position;
                if (d.sqrMagnitude > 0.01f) wantLean = d.normalized * 0.05f;              // se inclina hacia lo que mira
            }
            wantLean += transform.right * (-tilt * 0.006f);
            lean = Vector3.Lerp(lean, wantLean, 1f - Mathf.Exp(-4f * dt));
            core.localPosition = new Vector3(0, OrbHeight, 0) + transform.InverseTransformDirection(lean);

            // Respira (escala) y late al hablar
            float pulse = 1f + Mathf.Sin(floatPhase * 2.2f) * 0.04f + talk * (0.07f + 0.06f * Mathf.Sin(talkPhase));
            float squash = 1f - nod * 0.004f;
            shell.localScale = new Vector3(pulse, pulse * squash, pulse);
            halo.localScale = Vector3.one * HaloSize * (1f + talk * 0.25f + Mathf.Sin(floatPhase * 1.7f) * 0.05f);

            // Órbitas: giran siempre; con inquietud van más rápido y temblorosas
            float spin = 140f * (1f + unease * 1.6f) * dt;
            for (int i = 0; i < satellites.Length; i++)
            {
                var plane = satellites[i].parent;
                plane.Rotate(Vector3.up, spin * (i % 2 == 0 ? 1f : -1.3f), Space.Self);
                float r = 0.42f + (i % 2) * 0.08f + Mathf.Sin(floatPhase * 3f + i) * 0.03f + talk * 0.06f;
                if (unease > 0f) r += (Mathf.PerlinNoise(Time.time * 7f, i) - 0.5f) * 0.12f * unease;
                satellites[i].localPosition = new Vector3(r, 0, 0);
            }

            // Chispa dorada: va al objetivo (rápida) o vuelve a orbitar junto al núcleo (suave)
            Vector3 want = reachTarget ?? RestPointer();
            pointerPos = Vector3.Lerp(pointerPos, want, 1f - Mathf.Exp(-(reachTarget.HasValue ? 7f : 5f) * dt));
            pointer.position = pointerPos;

            // Brillo general; con inquietud parpadea de forma irregular
            aura = Mathf.MoveTowards(aura, auraTarget, dt * 1.5f);
            float jitter = unease > 0f ? Mathf.Lerp(1f, Mathf.PerlinNoise(Time.time * 11f, 0.7f) * 1.3f, unease) : 1f;
            float k = aura * jitter * (1f + talk * 0.35f * (0.5f + 0.5f * Mathf.Sin(talkPhase)));
            CineGlow.SetIntensity(coreMat, k);
            CineGlow.SetIntensity(shellMat, k);
            CineGlow.SetIntensity(haloMat, k * 0.55f);
            CineGlow.SetIntensity(satMat, k);
            CineGlow.SetIntensity(pointerMat, k);
            CineGlow.SetIntensity(pointerHaloMat, k * 0.7f);
            glow.intensity = 1.6f * k;
            var em = motes.emission; em.rateOverTime = 26f * Mathf.Clamp01(aura) + talk * 20f;
            var te = trail.emission; te.rateOverTime = 18f * Mathf.Clamp01(aura);
        }

        void OnDestroy()
        {
            foreach (var m in new[] { coreMat, shellMat, haloMat, satMat, pointerMat, pointerHaloMat }) if (m != null) Destroy(m);
        }
    }
}
