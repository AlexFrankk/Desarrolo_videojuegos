using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Primer combate del juego (tras la cinemática): el Gólem de Piedra en el sendero junto a la cabaña.
    ///  1) Su coraza de piedra lo hace inmune a los hechizos mientras arda la operación de su pecho (8 + 5).
    ///  2) Acércate y pulsa F para resolverla. Correcto → la coraza se rompe y los hechizos le hacen daño.
    ///     Incorrecto → el Gólem ruge y golpea el suelo.
    ///  3) Camina despacio hacia el mago y golpea con los dos puños si está cerca.
    /// El Espíritu del Maestro Athan acompaña al mago durante el combate y se desvanece al terminar.
    public class GolemEncounter : MonoBehaviour
    {
        public float maxHealth = 140f;
        public float smashDamage = 14f, smashRange = 3.6f, smashCooldown = 2.8f;
        public float wakeRange = 13f, quizRange = 8f, moveSpeed = 1.5f;

        StoneGolem golem;
        HealthSystem health;
        SpiritAthan spirit;
        Transform player;
        HealthSystem playerHealth;
        bool awake, shielded = true, busy, dead;
        float nextSmash;
        int wrong;
        string[] options; int correctIndex;

        public static GolemEncounter Spawn(IntroSet set, StoneGolem existing, bool awakeNow, SpiritAthan spirit = null)
        {
            var g = existing;
            if (g == null)
            {
                Vector3 toCabin = set.mageMark - set.golemStop; toCabin.y = 0;
                g = StoneGolem.Create(set.golemStop, Quaternion.LookRotation(toCabin.normalized));
                g.SetEyes(0f, true); g.SetCracks(0.6f); g.SetHunch(0.8f);
            }
            var enc = g.gameObject.AddComponent<GolemEncounter>();
            enc.golem = g;
            enc.spirit = spirit;
            enc.awake = awakeNow;
            return enc;
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                var cc = p.GetComponentInParent<CharacterController>();
                player = cc != null ? cc.transform : p.transform;
                playerHealth = player.GetComponent<HealthSystem>();
            }
            health = gameObject.AddComponent<HealthSystem>();
            health.team = Team.Enemy;
            health.SetMaxHealth(maxHealth);
            health.destroyOnDeath = false;
            health.hitInvulnerability = 0.12f;
            health.Died += OnDied;
            health.Damaged += (h, amount) => { if (!shielded) StartCoroutine(HitFlash()); };

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 2.1f, 0); col.radius = 1.1f; col.height = 4.2f;
            if (GameLayers.Exists(GameLayers.Enemies)) GameLayers.Set(gameObject, GameLayers.Enemies);
            var bar = gameObject.AddComponent<WorldHealthBar3D>();
            bar.health = health; bar.height = 5.2f; bar.sizeInPixels = new Vector2(220f, 20f); bar.hideWhenFull = false;

            BuildQuiz();
            if (spirit != null) spirit.gameObject.AddComponent<SpiritFollower>().Init(player, golem.transform);
            if (awake) Wake(false);
            else UIManager.Instance?.SetObjective("Explora la arboleda (un Gólem de Piedra duerme junto a la cabaña)");
        }

        void BuildQuiz()
        {
            // 8 + 5 = 13 con distractores típicos (contar de más, de menos, confundir con 8 + 6).
            int[] vals = { golem.answer, golem.answer - 1, golem.answer + 1, golem.answer + 2 };
            for (int i = vals.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (vals[i], vals[j]) = (vals[j], vals[i]); }
            options = new string[vals.Length];
            for (int i = 0; i < vals.Length; i++) { options[i] = vals[i].ToString(); if (vals[i] == golem.answer) correctIndex = i; }
        }

        void Wake(bool roar)
        {
            awake = true;
            golem.SetHunch(0f); golem.SetEyes(1f); golem.SetCracks(1f); golem.ShowOperation(true);
            if (roar) StartCoroutine(golem.Roar(1.2f));
            var ui = UIManager.Instance;
            ui?.SetObjective("Derrota al Gólem de Piedra");
            ui?.ShowMessage("¡El Gólem de Piedra bloquea el sendero! Su coraza es inmune: resuelve la operación de su pecho (F).", 5f);
            IsoCameraFollow.SetEncounter(golem.transform, 6f, 0.4f, -10f);
        }

        void Update()
        {
            if (dead || player == null || IntroCinematic.IsPlaying) return;
            var ui = UIManager.Instance;
            bool uiBusy = ui != null && (ui.IsModalOpen || ui.InCinematic);
            Vector3 d = player.position - transform.position; d.y = 0;
            float dist = d.magnitude;

            if (!awake) { if (dist < wakeRange) Wake(true); return; }
            if (shielded) health.SetInvulnerable(0.25f);
            golem.LookAt(player.position + Vector3.up * 1.4f);

            // Reto de la coraza
            if (shielded && ui != null && !uiBusy)
            {
                if (dist < quizRange) ui.ShowPrompt("F · Resolver la operación del Gólem (" + golem.operation + ")", this);
                else ui.HidePrompt(this);
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (dist < quizRange && kb != null && kb.fKey.wasPressedThisFrame) OpenQuiz();
            }

            if (busy || uiBusy) { golem.SetWalk(0f); return; }

            // Movimiento y ataque
            if (dist > smashRange * 0.85f && dist < 30f)
            {
                Quaternion want = Quaternion.LookRotation(d.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 60f * Time.deltaTime);
                golem.SetWalk(1f);
                Vector3 p = transform.position + transform.forward * moveSpeed * Time.deltaTime;
                p.y = IntroCinematic.Ground(p);
                transform.position = p;
            }
            else golem.SetWalk(0f);

            if (dist <= smashRange && Time.time >= nextSmash) StartCoroutine(SmashAttack());
        }

        IEnumerator SmashAttack()
        {
            busy = true;
            nextSmash = Time.time + smashCooldown;
            golem.SetWalk(0f);
            yield return golem.Smash();
            // El daño llega en el impacto (Smash espera la subida + bajada).
            Vector3 d = player.position - transform.position; d.y = 0;
            if (d.magnitude <= smashRange + 0.4f && playerHealth != null) playerHealth.TakeDamage(smashDamage);
            yield return new WaitForSeconds(0.4f);
            busy = false;
        }

        void OpenQuiz()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            ui.HidePrompt(this);
            ui.OpenQuiz(new UIManager.QuizData
            {
                title = "Coraza del Gólem de Piedra",
                subtitle = "Resuelve la operación que arde en su pecho para romper su coraza",
                question = golem.operation + " = ?",
                options = options
            }, OnAnswer, () => ui.ShowMessage("La coraza sigue intacta. Pulsa F cerca del Gólem para intentarlo.", 3f));
        }

        void OnAnswer(int index)
        {
            var ui = UIManager.Instance;
            if (index == correctIndex)
            {
                ui.LockQuiz(index, "¡Correcto! " + golem.operation + " = " + golem.answer + ". La coraza se agrieta…");
                StartCoroutine(BreakShield());
                return;
            }
            wrong++;
            ui.MarkOptionWrong(index, wrong == 1 ? "No es esa. Pista: 8 + 2 = 10… ¿y luego cuánto falta para sumar 5?" : "Casi. Cuenta 3 más desde 10.");
            StartCoroutine(golem.Roar(0.9f));
        }

        IEnumerator BreakShield()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            UIManager.Instance?.CloseQuiz(false);
            shielded = false;
            busy = true;
            Vector3 chest = golem.Chest.TransformPoint(new Vector3(0, 1.08f, 0.7f));
            CineGlow.Burst(chest, new Color(3.5f, 1.6f, 0.4f), 80, 3f, 0.14f, 1.6f, 0.6f);
            CameraShake3D.Shake(0.25f, 0.5f);
            golem.ShowOperation(false);
            golem.SetCracks(2.4f);
            yield return golem.Roar(1.2f);
            golem.SetCracks(1.3f);
            UIManager.Instance?.ShowMessage("¡Coraza rota! Lanza hechizos al Gólem (clic izquierdo).", 4f);
            if (spirit != null) StartCoroutine(spirit.Nod(1, 1f));
            busy = false;
        }

        IEnumerator HitFlash()
        {
            golem.SetCracks(2.6f);
            yield return new WaitForSeconds(0.12f);
            golem.SetCracks(1.3f);
        }

        void OnDied(HealthSystem h)
        {
            if (dead) return;
            dead = true;
            UIManager.Instance?.HidePrompt(this);
            IsoCameraFollow.ClearEncounter();
            StartCoroutine(Crumble());
        }

        /// El Gólem se desmorona: las rocas se sueltan, caen y se hunden; las grietas se apagan.
        IEnumerator Crumble()
        {
            golem.SetEyes(0f); golem.SetCracks(0f); golem.SetWalk(0f);
            golem.enabled = false;
            var col = GetComponent<Collider>(); if (col != null) col.enabled = false;
            var bar = GetComponent<WorldHealthBar3D>(); if (bar != null) bar.enabled = false;
            CameraShake3D.Shake(0.3f, 0.6f);
            golem.DustPuff(transform.position + Vector3.up * 0.5f, 50);
            var rocks = GetComponentsInChildren<MeshRenderer>();
            var vel = new Vector3[rocks.Length];
            for (int i = 0; i < rocks.Length; i++)
            {
                rocks[i].transform.SetParent(null, true);
                vel[i] = (rocks[i].transform.position - transform.position - Vector3.up * 1.5f).normalized * Random.Range(1f, 3f) + Vector3.up * Random.Range(1f, 3f);
            }
            float ground = IntroCinematic.Ground(transform.position);
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                for (int i = 0; i < rocks.Length; i++)
                {
                    var r = rocks[i]; if (r == null) continue;
                    Vector3 p = r.transform.position;
                    if (p.y > ground + 0.3f) { vel[i] += Physics.gravity * Time.deltaTime; p += vel[i] * Time.deltaTime; r.transform.Rotate(vel[i] * 40f * Time.deltaTime); }
                    if (t > 1.4f) p.y -= Time.deltaTime * 0.6f;
                    r.transform.position = p;
                }
                yield return null;
            }
            foreach (var r in rocks) if (r != null) Destroy(r.gameObject);

            var ui = UIManager.Instance;
            ui?.ShowMessage("¡Derrotaste al Gólem de Piedra! El camino hacia el Cráter de Obsidiana comienza aquí.", 5f);
            ui?.SetObjective("Derrota a las criaturas numéricas de la arboleda");
            if (spirit != null)
            {
                var f = spirit.GetComponent<SpiritFollower>();
                if (f != null) f.FadeAway(4f);
            }
            Destroy(gameObject, 0.5f);
        }
    }

    /// El Espíritu del Maestro Athan flota a la derecha del mago durante el primer combate y luego se desvanece.
    public class SpiritFollower : MonoBehaviour
    {
        Transform player, focus;
        SpiritAthan spirit;
        bool fading;

        public void Init(Transform player, Transform focus) { this.player = player; this.focus = focus; spirit = GetComponent<SpiritAthan>(); }

        void LateUpdate()
        {
            if (player == null || fading) return;
            Vector3 fwd = player.forward; fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
            Vector3 want = player.position + Vector3.Cross(Vector3.up, fwd) * 1.6f - fwd * 0.8f;
            want.y = IntroCinematic.Ground(want);
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-2.2f * Time.deltaTime));
            Vector3 look = (focus != null ? focus.position : player.position + fwd * 5f) - transform.position; look.y = 0;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (spirit != null) spirit.Look(focus != null ? focus.position + Vector3.up * 2.5f : player.position + Vector3.up * 1.4f);
        }

        public void FadeAway(float seconds) { fading = true; StartCoroutine(Fade(seconds)); }

        IEnumerator Fade(float seconds)
        {
            if (spirit != null)
            {
                spirit.Look(player.position + Vector3.up * 1.4f);
                yield return spirit.Nod(1, 0.8f);
                spirit.SetAura(0f);
            }
            for (float t = 0; t < seconds; t += Time.deltaTime) { transform.position += Vector3.up * Time.deltaTime * 0.3f; yield return null; }
            Destroy(gameObject);
        }
    }
}
