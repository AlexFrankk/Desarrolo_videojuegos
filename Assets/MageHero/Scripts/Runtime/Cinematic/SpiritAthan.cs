using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Espíritu del Maestro Athan: proyección espectral benigna (azul celeste y dorado suave).
    /// Flota sobre el suelo, su túnica se deshace en motas de luz y no habla: se comunica con gestos.
    /// Brazos de dos segmentos con IK analítica: cada mano puede ir a un punto del mundo (señalar, dibujar en el aire,
    /// poner la mano en el pecho del joven). La cabeza asiente, se inclina y mira a un objetivo.
    public class SpiritAthan : MonoBehaviour
    {
        Transform body, head, shoulderL, shoulderR, elbowL, elbowR, handL, handR;
        Material robeMat, faceMat, beardMat, eyeMat, handMat;
        ParticleSystem motes, handSparks;
        Light glow;

        const float Upper = 0.42f, Fore = 0.42f;
        float floatPhase, nod, nodTarget, tilt, tiltTarget, unease, aura = 1f, auraTarget = 1f;
        Vector3? handTargetL, handTargetR;
        Vector3 handPosL, handPosR;               // posición actual (suavizada) de cada mano, en el mundo
        Vector3 lookTarget; bool hasLook;
        float headYaw, headPitch;

        public Transform HandR => handR;
        public Transform HandL => handL;
        public Transform Head => head;

        static readonly Color Robe = new Color(0.35f, 0.85f, 1.6f), Gold = new Color(1.7f, 1.35f, 0.75f), EyeC = new Color(2.6f, 2.8f, 3.2f);

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
            robeMat = CineGlow.Make(Robe, 2.0f, 0.3f, 0.12f);
            faceMat = CineGlow.Make(Robe * 1.1f, 1.6f, 0.3f, 0.05f);
            beardMat = CineGlow.Make(Gold, 1.4f, 0.35f, 0.08f);
            handMat = CineGlow.Make(Robe * 1.3f, 1.2f, 0.4f, 0.05f);
            eyeMat = CineGlow.Make(EyeC, 0f, 1f, 0f);

            body = LowPoly.Pivot("Cuerpo", transform, Vector3.zero);

            // Túnica: cono ancho abajo que se estrecha hacia el pecho; dobladillo abierto (se disuelve en motas).
            var robe = new LowPoly().Paint(Color.white);
            robe.At(new Vector3(0, 0.05f, 0)).Prism(10, 0.58f, 0.3f, 1.3f, false, 0.25f);
            robe.At(new Vector3(0, 1.3f, 0)).Prism(10, 0.3f, 0.24f, 0.35f, false);
            robe.At(new Vector3(0, 1.62f, 0)).Prism(8, 0.24f, 0.1f, 0.1f, true);
            robe.Spawn("Tunica", body, robeMat, default, false);
            // Cinturón y ribetes dorados
            var trim = new LowPoly().Paint(Color.white);
            trim.At(new Vector3(0, 1.05f, 0)).Prism(10, 0.36f, 0.35f, 0.07f, false);
            trim.At(new Vector3(0, 0.05f, 0)).Prism(10, 0.6f, 0.585f, 0.06f, false);
            trim.Spawn("Ribetes", body, beardMat, default, false);

            // Cabeza, capucha de mago, barba larga y ojos
            head = LowPoly.Pivot("Cabeza", body, new Vector3(0, 1.82f, 0.02f));
            var face = new LowPoly().Paint(Color.white);
            face.At(new Vector3(0, 0.05f, 0), Quaternion.identity, new Vector3(0.9f, 1.05f, 0.95f)).Rock(0.19f, 4, 0.05f, 1);
            face.Spawn("Rostro", head, faceMat, default, false);
            var hood = new LowPoly().Paint(Color.white);
            hood.At(new Vector3(0, 0.1f, -0.05f), Quaternion.Euler(-18, 0, 0), Vector3.one).Prism(9, 0.26f, 0.03f, 0.48f, false, 0.4f);
            hood.Spawn("Capucha", head, robeMat, default, false);
            var beard = new LowPoly().Paint(Color.white);
            beard.At(new Vector3(0, -0.04f, 0.12f), Quaternion.Euler(170, 0, 0), new Vector3(1f, 1f, 0.7f)).Prism(7, 0.15f, 0.015f, 0.62f, false, 0.3f);
            beard.At(new Vector3(0, 0.05f, 0.15f), Quaternion.Euler(0, 0, 90), new Vector3(1, 1, 0.6f)).Prism(5, 0.035f, 0.02f, 0.2f, true);       // bigote
            beard.At(new Vector3(0, 0.05f, 0.15f), Quaternion.Euler(0, 0, -90), new Vector3(1, 1, 0.6f)).Prism(5, 0.035f, 0.02f, 0.2f, true);
            beard.At(new Vector3(0, 0.13f, 0.16f), Quaternion.Euler(0, 0, 90), new Vector3(1, 1, 0.5f)).Prism(4, 0.02f, 0.012f, 0.12f, true);      // cejas
            beard.At(new Vector3(0, 0.13f, 0.16f), Quaternion.Euler(0, 0, -90), new Vector3(1, 1, 0.5f)).Prism(4, 0.02f, 0.012f, 0.12f, true);
            beard.Spawn("Barba", head, beardMat, default, false);
            foreach (float x in new[] { -0.065f, 0.065f })
            {
                var e = new LowPoly().Paint(Color.white);
                e.At(new Vector3(x, 0.08f, 0.165f), Quaternion.identity, new Vector3(1.2f, 0.7f, 0.5f)).Rock(0.025f, 2, 0f, 0);
                e.Spawn("Ojo", head, eyeMat, default, false);
            }

            // Brazos: manga ancha (hombro→codo), antebrazo y mano
            shoulderL = LowPoly.Pivot("Hombro_L", body, new Vector3(-0.27f, 1.55f, 0));
            shoulderR = LowPoly.Pivot("Hombro_R", body, new Vector3(0.27f, 1.55f, 0));
            foreach (var sh in new[] { shoulderL, shoulderR })
            {
                var up = new LowPoly().Paint(Color.white);
                up.At(Vector3.zero, Quaternion.Euler(180, 0, 0), Vector3.one).Prism(7, 0.08f, 0.11f, Upper, false);
                up.Spawn("Manga", sh, robeMat, default, false);
                var el = LowPoly.Pivot("Codo", sh, new Vector3(0, -Upper, 0));
                var fo = new LowPoly().Paint(Color.white);
                fo.At(Vector3.zero, Quaternion.Euler(180, 0, 0), Vector3.one).Prism(7, 0.11f, 0.16f, Fore * 0.85f, false);
                fo.Spawn("Antebrazo", el, robeMat, default, false);
                var hand = LowPoly.Pivot("Mano", el, new Vector3(0, -Fore, 0));
                var hm = new LowPoly().Paint(Color.white);
                hm.At(Vector3.zero, Quaternion.identity, new Vector3(0.8f, 1.1f, 0.5f)).Rock(0.065f, 9, 0.1f, 0);
                hm.At(new Vector3(0, -0.07f, 0.0f), Quaternion.Euler(180, 0, 0), Vector3.one).Prism(4, 0.015f, 0.008f, 0.1f, true);   // dedo índice
                hm.Spawn("ManoMesh", hand, handMat, default, false);
                if (sh == shoulderL) { elbowL = el; handL = hand; } else { elbowR = el; handR = hand; }
            }

            motes = CineGlow.Motes("MotasDeLuz", body, new Vector3(0, 0.15f, 0), new Color(0.6f, 1.3f, 2.2f), 22f, 0.09f, 0.35f, 2.4f, 0.5f, -0.06f);
            handSparks = CineGlow.Motes("ChispasMano", handR, Vector3.zero, Gold * 1.6f, 0f, 0.06f, 0.3f, 0.8f, 0.03f, 0.1f);

            var lg = new GameObject("LuzEspiritu"); lg.transform.SetParent(body, false); lg.transform.localPosition = new Vector3(0, 1.4f, 0.5f);
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(0.55f, 0.85f, 1f); glow.range = 4f; glow.intensity = 1.3f;
            glow.shadows = LightShadows.None;

            handPosL = shoulderL.position + Vector3.down * 0.8f;
            handPosR = shoulderR.position + Vector3.down * 0.8f;
        }

        // ------------------------------------------------------------------ control
        /// Lleva la mano (derecha por defecto) a un punto del mundo. null = la deja caer relajada.
        public void Reach(Vector3? world, bool right = true) { if (right) handTargetR = world; else handTargetL = world; }
        public void Look(Vector3 world) { lookTarget = world; hasLook = true; }
        public void ClearLook() => hasLook = false;
        public void Tilt(float degrees) => tiltTarget = degrees;
        /// Inquietud: el aura parpadea y pierde intensidad.
        public void SetUnease(float k) => unease = Mathf.Clamp01(k);
        public void SetAura(float k) => auraTarget = k;
        public void Sparks(bool on) { var em = handSparks.emission; em.rateOverTime = on ? 60f : 0f; }

        /// Asiente despacio (n veces).
        public IEnumerator Nod(int times = 1, float speed = 1f)
        {
            for (int i = 0; i < times; i++)
            {
                nodTarget = 18f; yield return new WaitForSeconds(0.32f / speed);
                nodTarget = -3f; yield return new WaitForSeconds(0.32f / speed);
            }
            nodTarget = 0f;
        }

        /// Desliza el espíritu flotando hasta un punto (sin pasos: un fantasma se desliza).
        public IEnumerator GlideTo(Vector3 target, float seconds, Vector3? faceTowards = null)
        {
            Vector3 from = transform.position;
            Quaternion r0 = transform.rotation;
            Quaternion r1 = r0;
            if (faceTowards.HasValue) { Vector3 d = faceTowards.Value - target; d.y = 0; if (d.sqrMagnitude > 0.01f) r1 = Quaternion.LookRotation(d); }
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(from, target, e);
                transform.rotation = Quaternion.Slerp(r0, r1, e);
                yield return null;
            }
            transform.position = target; transform.rotation = r1;
        }

        /// Traza un número/operación en el aire con el dedo: la mano recorre un pequeño garabato
        /// mientras aparece el texto de luz. Devuelve el número creado.
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
                // zigzag de izquierda a derecha (como escribiendo cada cifra)
                Vector3 p = world - right * w * 0.5f + right * w * t + Vector3.up * Mathf.Sin(t * Mathf.PI * text.Length * 2f) * 0.12f;
                Reach(p);
                gn.FadeTo(Mathf.Clamp01(t * 1.4f), 4f);
                yield return null;
            }
            Sparks(false);
            gn.FadeTo(1f, 2f);
        }

        // ------------------------------------------------------------------ animación
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            floatPhase += dt;
            body.localPosition = new Vector3(0, 0.28f + Mathf.Sin(floatPhase * 1.4f) * 0.07f, 0);
            body.localRotation = Quaternion.Euler(Mathf.Sin(floatPhase * 0.9f) * 1.5f, 0, Mathf.Sin(floatPhase * 0.7f) * 1.2f);

            nod = Mathf.Lerp(nod, nodTarget, 1f - Mathf.Exp(-9f * dt));
            tilt = Mathf.Lerp(tilt, tiltTarget, 1f - Mathf.Exp(-4f * dt));
            float wantYaw = 0f, wantPitch = 0f;
            if (hasLook)
            {
                Vector3 local = body.InverseTransformPoint(lookTarget) - head.localPosition;
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
            }
            headYaw = Mathf.LerpAngle(headYaw, wantYaw, 1f - Mathf.Exp(-5f * dt));
            headPitch = Mathf.Lerp(headPitch, wantPitch, 1f - Mathf.Exp(-5f * dt));
            head.localRotation = Quaternion.Euler(headPitch + nod, headYaw, tilt);

            SolveArm(shoulderL, elbowL, handTargetL, ref handPosL, -1f, dt);
            SolveArm(shoulderR, elbowR, handTargetR, ref handPosR, 1f, dt);

            // Aura: brillo general; con inquietud parpadea de forma irregular.
            aura = Mathf.MoveTowards(aura, auraTarget, dt * 1.5f);
            float jitter = unease > 0f ? Mathf.Lerp(1f, Mathf.PerlinNoise(Time.time * 11f, 0.7f) * 1.3f, unease) : 1f;
            float k = aura * jitter;
            CineGlow.SetIntensity(robeMat, k);
            CineGlow.SetIntensity(faceMat, k);
            CineGlow.SetIntensity(handMat, k);
            CineGlow.SetIntensity(beardMat, k);
            CineGlow.SetIntensity(eyeMat, k);
            glow.intensity = 1.3f * k;
        }

        /// IK de dos huesos: el hombro apunta hacia la mano y el codo se dobla hacia fuera y atrás.
        void SolveArm(Transform shoulder, Transform elbow, Vector3? target, ref Vector3 handPos, float side, float dt)
        {
            // Pose relajada: mano colgando un poco por delante, a la altura de la cadera.
            Vector3 rest = body.TransformPoint(new Vector3(side * 0.36f, 0.85f + Mathf.Sin(floatPhase * 1.1f + side) * 0.02f, 0.16f));
            Vector3 want = target ?? rest;
            handPos = Vector3.Lerp(handPos, want, 1f - Mathf.Exp(-(target.HasValue ? 7f : 4f) * dt));

            Vector3 s = shoulder.position;
            Vector3 toHand = handPos - s;
            float reach = Upper + Fore;
            float d = Mathf.Clamp(toHand.magnitude, 0.12f, reach * 0.999f);
            Vector3 dir = toHand.sqrMagnitude > 1e-6f ? toHand.normalized : Vector3.down;
            // ángulo del hombro respecto a la línea hombro-mano (ley de cosenos)
            float a = Mathf.Acos(Mathf.Clamp((Upper * Upper + d * d - Fore * Fore) / (2f * Upper * d), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 pole = (body.TransformDirection(new Vector3(side, -0.3f, -0.6f))).normalized;
            // El codo se dobla hacia el "polo" (fuera, abajo y atrás): componente del polo perpendicular a la línea hombro-mano.
            Vector3 perp = Vector3.ProjectOnPlane(pole, dir);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(body.up * -1f, dir);
            perp.Normalize();
            float ar = a * Mathf.Deg2Rad;
            Vector3 upperDir = (dir * Mathf.Cos(ar) + perp * Mathf.Sin(ar)).normalized;
            // El hueso apunta a -Y local: rotación que lleva -Y a la dirección deseada.
            shoulder.rotation = Quaternion.FromToRotation(Vector3.down, upperDir) ;
            Vector3 elbowPos = s + upperDir * Upper;
            Vector3 foreDir = (s + dir * d - elbowPos).normalized;
            elbow.rotation = Quaternion.FromToRotation(Vector3.down, foreDir);
        }

        void OnDestroy()
        {
            foreach (var m in new[] { robeMat, faceMat, beardMat, eyeMat, handMat }) if (m != null) Destroy(m);
        }
    }
}
