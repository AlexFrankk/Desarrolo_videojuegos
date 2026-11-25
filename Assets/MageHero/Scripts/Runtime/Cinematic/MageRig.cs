using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Control del Mago Joven durante la cinemática: lo mueve sin físicas, usa el Animator solo para caminar
    /// y posa los brazos a mano (hachazo, secarse la frente, preguntar, contar, colocar leña, empuñar el báculo).
    /// Las poses se aplican en LateUpdate, después del Animator, así que se superponen a sus clips.
    public class MageRig
    {
        public Transform Root { get; }
        readonly MonoBehaviour host;
        readonly Transform model, armL, armR, body;
        readonly Animator anim;
        readonly GameObject[] staffParts;
        readonly Vector3 handLocal;
        readonly float groundOffset;
        Transform crystal;
        CharacterController cc;

        Vector3 eulerL, eulerR;
        Quaternion curL = Quaternion.identity, curR = Quaternion.identity;
        float armSpeed = 8f, lean, leanTarget, roll, rollTarget, walkSpeed;
        Transform heldAxe;
        ParticleSystem glowMotes;

        // Ajustes del hachazo (ángulos de brazo) y del agarre del hacha respecto a la mano.
        const float AxeGrip = 205f, ChopUp = -165f, ChopDown = -62f;

        public MageRig(Transform playerRoot, MonoBehaviour host, List<Behaviour> disabled)
        {
            var cc = playerRoot.GetComponentInParent<CharacterController>();
            Root = cc != null ? cc.transform : playerRoot;
            this.host = host;
            model = IntroSet.FindDeep(Root, "Model");
            armL = IntroSet.FindDeep(Root, "Arm_L");
            armR = IntroSet.FindDeep(Root, "Arm_R");
            body = IntroSet.FindDeep(Root, "Body");
            anim = Root.GetComponentInChildren<Animator>();
            crystal = IntroSet.FindDeep(armR, "Crystal");

            void Off(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; disabled.Add(b); } }
            this.cc = cc;
            if (cc != null) cc.enabled = false;           // es un Collider: se reactiva en Release()
            Off(Root.GetComponent<PlayerController>());
            Off(Root.GetComponentInChildren<PlayerVisuals>());
            Off(Root.GetComponent<PlayerSafety>());
            Off(Root.GetComponent<ClickAimingCombat3D>());
            Off(Root.GetComponent<PlayerCombat3D>());

            // El báculo duerme apoyado en la cabaña: se ocultan sus piezas de la mano.
            var parts = new List<GameObject>();
            foreach (var n in new[] { "Staff", "Crystal", "RuneOrbit" })
            {
                var t = IntroSet.FindDeep(armR, n);
                if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); parts.Add(t.gameObject); }
            }
            staffParts = parts.ToArray();

            // Posición de la mano: extremo inferior de la malla del brazo.
            var armMesh = armR.GetComponentInChildren<MeshFilter>();
            if (armMesh != null && armMesh.sharedMesh != null)
            {
                var b = armMesh.sharedMesh.bounds;
                handLocal = armMesh.transform.localPosition + new Vector3(b.center.x, b.min.y + 0.06f, b.center.z);
            }
            else handLocal = new Vector3(0, -0.55f, 0);

            groundOffset = Root.position.y - IntroCinematic.Ground(Root.position);
            if (groundOffset < -0.5f || groundOffset > 1.5f) groundOffset = 0f;
            if (anim != null) { anim.SetFloat("Speed", 0f); anim.SetBool("IsGrounded", true); }
            ArmsRest();
        }

        // ------------------------------------------------------------------ puntos útiles
        float BodyTop => body != null ? body.GetComponent<Renderer>().bounds.max.y : Root.position.y + 1.6f;
        public Vector3 Head => new Vector3(Root.position.x, BodyTop - 0.28f, Root.position.z);
        public Vector3 Chest => Root.position + Vector3.up * ((BodyTop - Root.position.y) * 0.62f) + Root.forward * 0.12f;
        public Vector3 HandPos => armR.TransformPoint(handLocal);
        public Transform CrystalTransform => crystal != null ? crystal : armR;
        public Vector3 CrystalPos => crystal != null && crystal.gameObject.activeInHierarchy ? crystal.GetComponent<Renderer>().bounds.center : HandPos;

        // ------------------------------------------------------------------ colocación
        public void Teleport(Vector3 groundPos, Vector3 facing)
        {
            Root.position = new Vector3(groundPos.x, IntroCinematic.Ground(groundPos) + groundOffset, groundPos.z);
            facing.y = 0;
            if (facing.sqrMagnitude > 1e-4f) Root.rotation = Quaternion.LookRotation(facing.normalized);
        }

        public void Face(Vector3 worldPoint) { host.StartCoroutine(TurnTo(worldPoint, 0.45f)); }

        IEnumerator TurnTo(Vector3 p, float seconds)
        {
            Vector3 d = p - Root.position; d.y = 0;
            if (d.sqrMagnitude < 1e-4f) yield break;
            Quaternion r0 = Root.rotation, r1 = Quaternion.LookRotation(d.normalized);
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                Root.rotation = Quaternion.Slerp(r0, r1, t * t * (3f - 2f * t));
                yield return null;
            }
            Root.rotation = r1;
        }

        public IEnumerator WalkTo(Vector3 target, float speed)
        {
            walkSpeed = speed;
            while (true)
            {
                Vector3 d = target - Root.position; d.y = 0;
                if (d.magnitude < 0.06f) break;
                Root.rotation = Quaternion.RotateTowards(Root.rotation, Quaternion.LookRotation(d.normalized), 540f * Time.deltaTime);
                Vector3 p = Root.position + d.normalized * Mathf.Min(d.magnitude, speed * Time.deltaTime);
                p.y = IntroCinematic.Ground(p) + groundOffset;
                Root.position = p;
                if (anim != null) anim.SetFloat("Speed", Mathf.Clamp01(speed / 3.2f));
                yield return null;
            }
            walkSpeed = 0f;
            if (anim != null) anim.SetFloat("Speed", 0f);
        }

        public IEnumerator StepBack(Vector3 dir, float distance, float seconds)
        {
            Vector3 p0 = Root.position;
            dir.y = 0; dir.Normalize();
            if (anim != null) anim.SetFloat("Speed", 0.3f);
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                Vector3 p = p0 + dir * distance * (1f - (1f - t) * (1f - t));
                p.y = IntroCinematic.Ground(p) + groundOffset;
                Root.position = p;
                yield return null;
            }
            if (anim != null) anim.SetFloat("Speed", 0f);
        }

        // ------------------------------------------------------------------ poses
        void Pose(Vector3 left, Vector3 right, float speed = 8f) { eulerL = left; eulerR = right; armSpeed = speed; }
        public void ArmsRest() { Pose(new Vector3(-8f, 0, -4f), new Vector3(-8f, 0, 4f), 6f); leanTarget = 0f; rollTarget = 0f; }
        public void ArmsUp() { Pose(new Vector3(ChopUp, 0, 8f), new Vector3(ChopUp, 0, -8f), 30f); leanTarget = -6f; }
        public void ArmsGuard() { Pose(new Vector3(-58f, 0, 18f), new Vector3(-62f, 0, -12f), 10f); leanTarget = -4f; }

        public void WipeForehead(bool on)
        {
            if (on) { Pose(new Vector3(-158f, 0, 34f), eulerR, 5f); rollTarget = -4f; }
            else { Pose(new Vector3(-8f, 0, -4f), eulerR, 5f); rollTarget = 0f; }
        }

        /// Gesto de "¿así está bien?": abre la mano hacia la pila e inclina la cabeza.
        public void Ask(bool on)
        {
            if (on) { Pose(new Vector3(-8f, 0, -4f), new Vector3(-52f, 0, 34f), 6f); rollTarget = 7f; }
            else { ArmsRest(); }
        }

        public IEnumerator Count(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Pose(new Vector3(-88f, 0, 14f), eulerR, 14f);
                yield return new WaitForSeconds(0.28f);
                Pose(new Vector3(-72f, 0, 14f), eulerR, 14f);
                yield return new WaitForSeconds(0.28f);
            }
            ArmsRest();
            yield return new WaitForSeconds(0.25f);
        }

        /// Se agacha, coloca un tronco en la pila (aparece con un pequeño rebote) y se incorpora.
        public IEnumerator PlaceLog(Transform log)
        {
            Pose(new Vector3(-62f, 0, 6f), new Vector3(-62f, 0, -6f), 9f);
            leanTarget = 24f;
            yield return new WaitForSeconds(0.35f);
            log.gameObject.SetActive(true);
            Vector3 s = log.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.25f)
            {
                float k = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                log.localScale = s * Mathf.Lerp(0.2f, 1f, t) * k;
                yield return null;
            }
            log.localScale = s;
            leanTarget = 0f;
            ArmsRest();
            yield return new WaitForSeconds(0.3f);
        }

        // ------------------------------------------------------------------ hacha
        public void HoldAxe(Transform axe)
        {
            heldAxe = axe;
            axe.SetParent(armR, false);
            axe.localPosition = handLocal;
            axe.localRotation = Quaternion.Euler(AxeGrip, 0, 0);
        }

        public IEnumerator ChopRaise(float seconds) { ArmsUp(); yield return new WaitForSeconds(seconds); }

        public IEnumerator ChopSwing(float seconds)
        {
            Pose(new Vector3(ChopDown, 0, 6f), new Vector3(ChopDown, 0, -6f), 60f);
            leanTarget = 14f;
            yield return new WaitForSeconds(seconds);
        }

        /// Deja el hacha clavada en el tocón.
        public void LeaveAxe(Transform axe, Vector3 headPoint)
        {
            heldAxe = null;
            axe.SetParent(null, true);
            axe.rotation = Quaternion.Euler(0, 70, 152);
            axe.position = headPoint - axe.rotation * new Vector3(0.15f, 0.82f, 0f);
        }

        public IEnumerator DropAxe(Transform axe)
        {
            heldAxe = null;
            axe.SetParent(null, true);
            Pose(new Vector3(-20f, 0, -4f), new Vector3(-30f, 0, 12f), 6f);
            Vector3 v = Root.right * 0.4f;
            float ground = IntroCinematic.Ground(axe.position);
            Quaternion r0 = axe.rotation, r1 = Quaternion.LookRotation(Root.right, Vector3.up) * Quaternion.Euler(90, 0, 0);
            for (float t = 0; t < 1f && axe.position.y > ground + 0.05f; t += Time.deltaTime / 0.6f)
            {
                v += Physics.gravity * Time.deltaTime;
                axe.position += v * Time.deltaTime;
                axe.rotation = Quaternion.Slerp(r0, r1, t);
                yield return null;
            }
            axe.position = new Vector3(axe.position.x, ground + 0.04f, axe.position.z);
            axe.rotation = r1;
            CameraShake3D.Shake(0.02f, 0.1f);
        }

        public void DropAxeInstant(Transform axe)
        {
            heldAxe = null;
            axe.SetParent(null, true);
            Vector3 p = Root.position + Root.right * 0.6f;
            axe.position = new Vector3(p.x, IntroCinematic.Ground(p) + 0.04f, p.z);
            axe.rotation = Quaternion.LookRotation(Root.right, Vector3.up) * Quaternion.Euler(90, 0, 0);
        }

        // ------------------------------------------------------------------ báculo
        public void TakeStaff(Transform staffProp)
        {
            if (staffProp != null) staffProp.gameObject.SetActive(false);
            foreach (var p in staffParts) if (p != null) p.SetActive(true);
            Pose(new Vector3(-8f, 0, -4f), new Vector3(-38f, 0, 10f), 7f);
            leanTarget = -5f;
        }

        public void Glow(float seconds)
        {
            if (glowMotes == null) glowMotes = CineGlow.Motes("RunasDespiertas", Root, Vector3.up * 1f, new Color(0.7f, 1.5f, 2.6f), 0f, 0.07f, 0.4f, 1.2f, 0.45f, -0.1f);
            host.StartCoroutine(GlowRoutine(seconds));
        }

        IEnumerator GlowRoutine(float seconds)
        {
            var em = glowMotes.emission;
            em.rateOverTime = 70f;
            yield return new WaitForSeconds(seconds);
            em.rateOverTime = 6f;            // queda un leve resplandor de runas
        }

        // ------------------------------------------------------------------ aplicación por fotograma
        public void Apply(float dt)
        {
            if (armL == null || armR == null) return;
            float k = 1f - Mathf.Exp(-armSpeed * dt);
            curL = Quaternion.Slerp(curL, Quaternion.Euler(eulerL), k);
            curR = Quaternion.Slerp(curR, Quaternion.Euler(eulerR), k);
            if (walkSpeed > 0.01f)
            {
                // caminando: balanceo natural del Animator con la pose encima (brazos un poco abiertos)
                armL.localRotation = armL.localRotation * Quaternion.Euler(eulerL.x * 0.15f, 0, 0);
                armR.localRotation = heldAxe != null ? curR : armR.localRotation * Quaternion.Euler(eulerR.x * 0.15f, 0, 0);
            }
            else { armL.localRotation = curL; armR.localRotation = curR; }
            lean = Mathf.Lerp(lean, leanTarget, 1f - Mathf.Exp(-7f * dt));
            roll = Mathf.Lerp(roll, rollTarget, 1f - Mathf.Exp(-5f * dt));
            if (model != null) model.localRotation = Quaternion.Euler(lean, 0, roll);
        }

        /// Devuelve el mago al control del jugador (el director reactiva sus componentes).
        public void Release()
        {
            foreach (var p in staffParts) if (p != null) p.SetActive(true);
            if (heldAxe != null) { heldAxe.SetParent(null, true); heldAxe = null; }
            if (model != null) model.localRotation = Quaternion.identity;
            if (armL != null) armL.localRotation = Quaternion.identity;
            if (armR != null) armR.localRotation = Quaternion.identity;
            if (anim != null) anim.SetFloat("Speed", 0f);
            if (glowMotes != null) Object.Destroy(glowMotes.gameObject, 2f);
            if (cc != null) cc.enabled = true;
            var pc = Root.GetComponent<PlayerController>();
            if (pc != null) pc.ResetMotion();
        }
    }
}
