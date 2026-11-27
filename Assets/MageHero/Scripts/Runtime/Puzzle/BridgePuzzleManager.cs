using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace MageHero
{
    /// Puente del río de level_primavera:
    ///  1) Al acercarse, cinemática: El Gran Cero aparece y derrumba el puente (bloquea el camino principal).
    ///  2) Junto al puente roto, F abre el quiz: ecuación aritmética aleatoria con 4 opciones.
    ///  3) Correcta → el puente se repara (piezas vuelven a su sitio, se quita el bloqueo).
    ///     Incorrecta → −vida (sin morir) y reintento; tras 2 fallos el Gran Cero cambia la ecuación.
    ///  4) "Salir" cierra el quiz para explorar y buscar el Pergamino de Athan (ClueItemSystem).
    public class BridgePuzzleManager : MonoBehaviour
    {
        public enum BridgeState { Intact, Collapsing, Broken, Repairing, Repaired }

        public static BridgePuzzleManager Instance { get; private set; }

        [Header("Piezas del puente (las asigna Tools > Mage Hero > Bridge Puzzle)")]
        public Transform deck;
        public Transform[] rails;
        public Transform[] pilings;

        [Header("Zonas (Trigger Colliders hijos)")]
        public TriggerRelay3D collapseZone;
        public TriggerRelay3D repairZone;
        [Tooltip("Collider sólido (capa Obstacles) que bloquea el paso mientras el puente está roto.")]
        public BoxCollider blocker;
        [Tooltip("Agua visible en el hueco del puente roto (sin collider).")]
        public GameObject gapWater;

        [Header("Materiales")]
        public Material gapMaterial;
        public Material villainDarkMaterial;
        public Material villainRedMaterial;
        public Material repairGlowMaterial;

        [Header("Quiz")]
        [Min(0f)] public float wrongAnswerPenalty = 10f;
        [Min(1)] public int wrongAnswersBeforeNewEquation = 2;
        [Tooltip("Zona donde está el pergamino (solo para el mensaje de ayuda).")]
        public string clueZoneName = "";

        [Header("Eventos")]
        public UnityEvent onCollapsed = new UnityEvent();
        public UnityEvent onRepaired = new UnityEvent();

        public BridgeState State { get; private set; } = BridgeState.Intact;
        public ArithmeticEquation CurrentEquation { get; private set; }

        Transform player;
        HealthSystem playerHealth;
        int wrongCount;
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<GameObject> deckSegments = new List<GameObject>();
        Renderer deckRenderer;

        class Piece
        {
            public Transform t; public Vector3 pos; public Quaternion rot; public Vector3 brokenPos; public Quaternion brokenRot;
            public Renderer r; public Material mat; public Renderer original;
            // derrumbe con física sencilla
            public bool falls, inWater, lean, hang; public float delay, seed, hingeSign; public Vector3 vel, angVel, hinge, leanAxis;
        }

        // ------------------------------------------------------------------ Ciclo de vida
        void Awake()
        {
            Instance = this;
            if (collapseZone == null || repairZone == null || blocker == null || gapWater == null) BuildHelpers();
            blocker.enabled = false;
            gapWater.SetActive(false);
            CurrentEquation = ArithmeticEquation.Random();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) { player = p.transform; playerHealth = p.GetComponentInParent<HealthSystem>(); }
            collapseZone.Entered += _ => { if (State == BridgeState.Intact) StartCoroutine(CollapseCinematic()); };
        }

        void Update()
        {
            var ui = UIManager.Instance;
            if (ui == null || State != BridgeState.Broken) return;

            bool near = repairZone.PlayerInside && !ui.IsModalOpen && !ui.InCinematic;
            if (near) ui.ShowPrompt("Pulsa  F  para reparar el puente", this);
            else ui.HidePrompt(this);

            var kb = Keyboard.current; var gp = Gamepad.current;
            bool interact = (kb != null && kb.fKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
            if (near && interact) OpenQuiz();
        }

        // ------------------------------------------------------------------ Quiz
        public void OpenQuiz()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            ui.HidePrompt(this);
            ui.OpenQuiz(BuildQuizData(), OnAnswer, OnQuizExit);
        }

        UIManager.QuizData BuildQuizData()
        {
            var labels = new string[4];
            for (int i = 0; i < 4; i++) labels[i] = CurrentEquation.options[i].ToString();
            return new UIManager.QuizData
            {
                title = "Repara el puente",
                subtitle = "Resuelve la ecuación del Gran Cero para reconstruirlo",
                question = CurrentEquation.text,
                options = labels
            };
        }

        void OnAnswer(int index)
        {
            var ui = UIManager.Instance;
            if (index == CurrentEquation.correctIndex)
            {
                ui.LockQuiz(index, $"¡Correcto! {CurrentEquation.text.Replace("?", CurrentEquation.answer.ToString())}");
                StartCoroutine(RepairSequence());
                return;
            }

            wrongCount++;
            if (playerHealth != null && wrongAnswerPenalty > 0f) playerHealth.Drain(wrongAnswerPenalty, 1f);
            if (wrongCount >= wrongAnswersBeforeNewEquation)
            {
                wrongCount = 0;
                CurrentEquation = ArithmeticEquation.Random();
                ui.RefreshQuiz(BuildQuizData(), $"¡El Gran Cero cambió la ecuación! (−{wrongAnswerPenalty:0} de vida)");
            }
            else
            {
                ui.MarkOptionWrong(index, $"Incorrecto (−{wrongAnswerPenalty:0} de vida). {CurrentEquation.tip} Inténtalo otra vez.");
            }
        }

        void OnQuizExit()
        {
            var ui = UIManager.Instance;
            if (ui == null || State != BridgeState.Broken) return;
            bool hasClue = ClueItemSystem.Instance != null && ClueItemSystem.Instance.Collected;
            ui.ShowMessage(hasClue ? "Repasa el pergamino (H) y vuelve a intentarlo." : "Explora el mapa: el Pergamino de Athan te enseñará a resolverla.", 3f);
        }

        // ------------------------------------------------------------------ Cinemática de destrucción
        // Planos: vista general del puente → aparece el Gran Cero (acercamiento) → carga energía (contrapicado)
        // → rayo e impacto (cámara lenta) → los tablones caen al río y flotan → el Gran Cero se esfuma → vuelve el juego.
        IEnumerator CollapseCinematic()
        {
            State = BridgeState.Collapsing;
            var ui = UIManager.Instance;
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<IsoCameraFollow>() : null;
            ui?.BeginCinematic();
            if (follow != null) follow.enabled = false;
            BridgeAxes(out Vector3 axis, out Vector3 side, out float len);
            Vector3 c = transform.position;
            Vector3 toPlayer = player != null ? player.position - c : -axis; toPlayer.y = 0f;
            if (Vector3.Dot(toPlayer, side) < 0f) side = -side;                    // la cámara se queda del lado del jugador

            // 1) vista general lateral del puente
            Vector3 wideP = c + side * (len * 1.25f + 6f) + Vector3.up * 6.5f;
            yield return CamMove(cam, wideP, c + Vector3.up * 0.5f, 1.4f, 45f);

            // 2) el Gran Cero aparece al otro lado (estallido rojo y sube flotando)
            Vector3 away = -toPlayer.normalized; if (away.sqrMagnitude < 0.01f) away = axis;
            Vector3 villainPos = c + away * (LongHalf() + 3.5f) + Vector3.up * 3.4f;
            var villain = BuildGranCero(villainPos);
            HitFX.Explosion(villainPos, 2.2f, new Color(1f, 0.2f, 0.15f));
            CameraShake3D.Shake(0.2f, 0.5f);
            StartCoroutine(Scale(villain.transform, Vector3.zero, Vector3.one, 0.7f));
            StartCoroutine(CamMove(cam, villainPos - away * 7.5f + side * 3.2f + Vector3.up * 0.6f, villainPos, 3.2f, 38f));
            ui?.SetSubtitle("EL GRAN CERO:  ¡Ja, ja! Ningún mago cruzará mi río...  ¡Este puente ahora vale CERO!");
            yield return new WaitForSeconds(3.0f);

            // 3) carga: la energía roja converge en su mano y el anillo gira más rápido (contrapicado desde el puente)
            var spin = villain.GetComponentInChildren<SpinRing>();
            if (spin != null) spin.speed = 260f;
            Vector3 hand = villainPos + Vector3.up * 0.6f - away * 0.9f;
            var charge = CineGlow.Motes("Carga", null, hand, new Color(3f, 0.4f, 0.25f), 90f, 0.18f, -2.2f, 0.7f, 1.6f, 0f);
            yield return CamCut(cam, c - away * (LongHalf() * 0.7f) + side * 1.6f + Vector3.up * 0.9f, villainPos + Vector3.up * 0.4f, 34f);
            yield return new WaitForSeconds(1.3f);
            charge.Stop(); Destroy(charge.gameObject, 1f);

            // 4) rayo sostenido contra el centro del puente; impacto en cámara lenta
            Vector3 hit = c + Vector3.up * 0.25f;
            StartCoroutine(CamMove(cam, c + side * (len * 0.9f + 4f) + Vector3.up * 3.2f - away * 1.5f, c + Vector3.up * 0.3f, 0.5f, 42f));
            yield return Beam(hand, hit, 0.55f);
            MageAudio.PlayBridgeBreak(gameObject);
            CameraShake3D.Shake(0.5f, 1.3f);
            HitFX.Explosion(hit, 2.6f, new Color(1f, 0.45f, 0.2f));
            BreakBridge();
            Time.timeScale = 0.35f;
            StartCoroutine(RestoreTime(0.9f));
            yield return SimulateCollapse(3.4f);

            // 5) el Gran Cero se burla mientras la cámara rodea el hueco del puente
            StartCoroutine(CamMove(cam, c + side * (len * 0.8f + 5f) + axis * 4f + Vector3.up * 4.5f, c + Vector3.down * 0.6f, 2.8f, 44f));
            ui?.SetSubtitle("EL GRAN CERO:  Si quieres pasar, resuelve mi ecuación...  ¡si es que puedes!");
            yield return new WaitForSeconds(2.6f);
            HitFX.Explosion(villainPos, 1.6f, new Color(0.6f, 0.15f, 0.6f));
            yield return Scale(villain.transform, Vector3.one, Vector3.zero, 0.45f);
            Destroy(villain);

            if (follow != null) follow.enabled = true;                                // la cámara de juego vuelve sola junto al mago
            ui?.EndCinematic();
            EnemyAI3D.CalmAround(transform.position, 25f, 4f);

            State = BridgeState.Broken;
            onCollapsed.Invoke();
            string where = string.IsNullOrEmpty(clueZoneName) ? "en otra zona del mapa" : $"cerca de «{clueZoneName}»";
            ui?.ShowMessage("¡El puente se derrumbó! Acércate y pulsa F para repararlo.", 4f);
            ui?.SetObjective($"Objetivo: repara el puente (F).  ¿Atascado? Dicen que el Maestro Athan dejó un pergamino {where}.");
        }

        IEnumerator RestoreTime(float realSeconds)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.4f) { Time.timeScale = Mathf.Lerp(0.35f, 1f, t); yield return null; }
            Time.timeScale = 1f;
        }

        // ---- cámara de la cinemática (se mueve a mano; IsoCameraFollow está apagada)
        int camToken;
        IEnumerator CamMove(Camera cam, Vector3 pos, Vector3 look, float seconds, float fov)
        {
            if (cam == null) yield break;
            int token = ++camToken;
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            Quaternion r1 = Quaternion.LookRotation(look - pos, Vector3.up);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                if (token != camToken || cam == null) yield break;
                float k = t * t * (3f - 2f * t);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, pos, k), Quaternion.Slerp(r0, r1, k));
                cam.fieldOfView = Mathf.Lerp(f0, fov, k);
                yield return null;
            }
            if (token == camToken && cam != null) { cam.transform.SetPositionAndRotation(pos, r1); cam.fieldOfView = fov; }
        }

        IEnumerator CamCut(Camera cam, Vector3 pos, Vector3 look, float fov)
        {
            camToken++;
            if (cam != null) { cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up)); cam.fieldOfView = fov; }
            yield return null;
        }

        void BridgeAxes(out Vector3 axis, out Vector3 side, out float len)
        {
            bool alongZ = deck == null || deck.lossyScale.z >= deck.lossyScale.x;
            axis = deck == null ? Vector3.forward : (alongZ ? deck.forward : deck.right);
            axis.y = 0f; axis.Normalize();
            side = Vector3.Cross(Vector3.up, axis);
            len = LongHalf() * 2f;
        }

        /// Rayo rojo sostenido: un prisma brillante que crece desde la mano hasta el puente, con chispas en el impacto.
        IEnumerator Beam(Vector3 from, Vector3 to, float seconds)
        {
            var mat = CineGlow.Make(new Color(4f, 0.6f, 0.35f), 0f, 1f, 0.5f);
            var lp = new LowPoly().Paint(Color.white);
            lp.Prism(6, 0.22f, 0.16f, 1f, true);
            var beam = lp.Spawn("RayoGranCero", null, mat, from, false);
            Vector3 d = to - from;
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            var sparks = CineGlow.Motes("ChispasRayo", null, to, new Color(3f, 0.8f, 0.3f), 140f, 0.14f, 4f, 0.6f, 0.4f, 0.6f);
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                float grow = Mathf.Clamp01(t / 0.15f);
                float w = 1f + Mathf.Sin(t * 60f) * 0.25f;
                beam.transform.localScale = new Vector3(w, d.magnitude * grow, w);
                CameraShake3D.Shake(0.08f, 0.1f);
                yield return null;
            }
            sparks.Stop(); Destroy(sparks.gameObject, 1f);
            Destroy(beam); Destroy(mat);
        }

        void BreakBridge()
        {
            pieces.Clear();
            // La cubierta se parte en 6 tablones: los de los extremos quedan colgando del borde, los centrales caen al río.
            deckRenderer = deck != null ? deck.GetComponent<Renderer>() : null;
            if (deck != null)
            {
                bool alongZ = deck.lossyScale.z >= deck.lossyScale.x;
                float len = alongZ ? deck.lossyScale.z : deck.lossyScale.x, wid = alongZ ? deck.lossyScale.x : deck.lossyScale.z;
                Vector3 axis = alongZ ? deck.forward : deck.right;
                const int N = 6;
                for (int i = 0; i < N; i++)
                {
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(seg.GetComponent<Collider>());
                    seg.name = "DeckSegment_" + i;
                    seg.transform.SetParent(transform, true);
                    float f = (i - (N - 1) * 0.5f) / N;
                    seg.transform.SetPositionAndRotation(deck.position + axis * (f * len), deck.rotation * Quaternion.Euler(0f, Random.Range(-3f, 3f), 0f));
                    seg.transform.localScale = alongZ ? new Vector3(wid * Random.Range(0.9f, 1f), deck.lossyScale.y, len / N - 0.12f) : new Vector3(len / N - 0.12f, deck.lossyScale.y, wid * Random.Range(0.9f, 1f));
                    if (deckRenderer != null) seg.GetComponent<Renderer>().sharedMaterial = deckRenderer.sharedMaterial;
                    deckSegments.Add(seg);
                    bool edge = i == 0 || i == N - 1;
                    float sgn = f < 0f ? -1f : 1f;
                    var p = AddPiece(seg.transform, seg.transform.position, seg.transform.rotation);
                    if (edge) p.hang = true;                                                                 // cuelga del borde
                    else
                    {
                        p.falls = true;
                        p.delay = (1f - Mathf.Abs(f) * 2f) * 0.05f + Random.Range(0f, 0.12f);           // primero el centro
                        p.vel = axis * sgn * Random.Range(1.2f, 3f) + Vector3.up * Random.Range(2.5f, 4.5f);
                        p.angVel = new Vector3(Random.Range(-220f, 220f), Random.Range(-60f, 60f), Random.Range(-220f, 220f));
                    }
                    p.hingeSign = sgn;
                }
                if (deckRenderer != null) deckRenderer.enabled = false;
            }
            if (rails != null)
                foreach (var r in rails)
                {
                    if (r == null) continue;
                    Vector3 outward = r.position - transform.position; outward.y = 0f; outward.Normalize();
                    var p = AddPiece(r, r.position, r.rotation);
                    p.falls = true; p.delay = Random.Range(0.05f, 0.25f);
                    p.vel = outward * Random.Range(1f, 2.2f) + Vector3.up * Random.Range(1f, 2.5f);
                    p.angVel = new Vector3(Random.Range(-90f, 90f), 0f, Random.Range(-90f, 90f));
                }
            if (pilings != null)
                foreach (var pl in pilings)
                {
                    if (pl == null) continue;
                    bool central = MiddleFraction(pl.position) < 0.45f;
                    var p = AddPiece(pl, pl.position, pl.rotation);
                    if (central) { p.lean = true; p.delay = Random.Range(0.2f, 0.5f); p.leanAxis = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.right; }
                }
            Splinters(transform.position + Vector3.up * 0.4f, 34);
            blocker.enabled = true;
            gapWater.SetActive(true);
            PushPlayerOffBlocker();
            PushEnemiesOffBridge();
        }

        /// Física sencilla del derrumbe: tiembla, los tablones salen despedidos girando, caen al agua (salpicadura)
        /// y quedan flotando a la deriva. Al final se guarda la pose de cada pieza (la reparación parte de ahí).
        IEnumerator SimulateCollapse(float seconds)
        {
            float waterY = WaterLevel();
            // crujido previo: todo el puente vibra un instante
            var basePos = new Dictionary<Piece, Vector3>();
            foreach (var p in pieces) basePos[p] = p.t.position;
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                foreach (var p in pieces) p.t.position = basePos[p] + Random.insideUnitSphere * 0.05f;
                yield return null;
            }
            foreach (var p in pieces) p.t.position = basePos[p];

            float time = 0f;
            while (time < seconds)
            {
                float dt = Time.deltaTime; time += dt;
                foreach (var p in pieces)
                {
                    if (p.t == null) continue;
                    if (time < p.delay) continue;
                    if (p.falls)
                    {
                        if (!p.inWater)
                        {
                            p.vel += Physics.gravity * dt;
                            p.t.position += p.vel * dt;
                            p.t.rotation = Quaternion.Euler(p.angVel * dt) * p.t.rotation;
                            if (p.t.position.y <= waterY)
                            {
                                p.inWater = true;
                                Splash(new Vector3(p.t.position.x, waterY + 0.05f, p.t.position.z), Mathf.Clamp(p.vel.magnitude / 6f, 0.5f, 1.4f));
                                p.vel = new Vector3(p.vel.x * 0.15f, 0f, p.vel.z * 0.15f);
                                p.angVel *= 0.15f;
                            }
                        }
                        else
                        {
                            // flota: sube a la superficie, se aplana poco a poco y deriva con la corriente
                            Vector3 pos = p.t.position;
                            pos += p.vel * dt;
                            pos.y = Mathf.Lerp(pos.y, waterY - 0.08f + Mathf.Sin(time * 2.2f + p.seed) * 0.05f, 1f - Mathf.Exp(-3f * dt));
                            p.t.position = pos;
                            Quaternion flat = Quaternion.LookRotation(Vector3.ProjectOnPlane(p.t.forward, Vector3.up).normalized + Vector3.forward * 0.0001f, Vector3.up);
                            p.t.rotation = Quaternion.Slerp(p.t.rotation, flat * Quaternion.Euler(Mathf.Sin(time + p.seed) * 6f, 0, Mathf.Cos(time * 0.8f + p.seed) * 6f), 1f - Mathf.Exp(-1.5f * dt));
                            p.vel *= 1f - 0.6f * dt;
                        }
                    }
                    else if (p.lean)
                    {
                        // pilar que se inclina y se hunde
                        float k = Mathf.Clamp01((time - p.delay) / 1.4f);
                        p.t.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, 55f, k * k), p.leanAxis) * p.rot;
                        p.t.position = p.pos + Vector3.down * (1.4f * k);
                    }
                    else if (p.hang)
                    {
                        // tablón del extremo: queda colgando, inclinado hacia el hueco
                        float k = Mathf.Clamp01((time - p.delay) / 0.5f);
                        float ease = 1f - Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.15f;
                        BridgeAxes(out Vector3 ax, out _, out _);
                        p.t.rotation = Quaternion.AngleAxis(-p.hingeSign * 24f * ease, Vector3.Cross(Vector3.up, ax)) * p.rot;
                        p.t.position = p.pos + Vector3.down * 0.35f * ease - ax * p.hingeSign * 0.2f * ease;
                    }
                }
                yield return null;
            }
            foreach (var p in pieces) if (p.t != null) { p.brokenPos = p.t.position; p.brokenRot = p.t.rotation; }
        }

        float WaterLevel()
        {
            float fallback = (deck != null ? deck.position.y : transform.position.y) - 1.6f;
            int water = GameLayers.Exists(GameLayers.Water) ? GameLayers.Mask(GameLayers.Water) : 0;
            if (water != 0 && Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, out RaycastHit h, 30f, water, QueryTriggerInteraction.Collide))
                if (h.point.y < fallback + 1.2f) return h.point.y;
            return fallback;
        }

        void Splash(Vector3 at, float size)
        {
            var ps = CineGlow.Motes("Salpicadura", null, at, new Color(1.1f, 1.3f, 1.5f), 0f, 0.22f * size, 4.2f * size, 0.9f, 0.3f, 1.4f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f;
            ps.transform.rotation = Quaternion.LookRotation(Vector3.up);
            ps.Emit(Mathf.RoundToInt(30 * size));
            Destroy(ps.gameObject, 2f);
            var ring = CineGlow.Motes("Ondas", null, at, new Color(0.7f, 0.9f, 1.1f), 0f, 0.5f * size, 1.6f, 0.8f, 0.1f, 0f);
            var rsh = ring.shape; rsh.shapeType = ParticleSystemShapeType.Circle; rsh.radius = 0.2f;
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.Emit(Mathf.RoundToInt(18 * size));
            Destroy(ring.gameObject, 2f);
        }

        /// Astillas de madera que saltan con el impacto (pequeños tablones que giran y caen al río).
        void Splinters(Vector3 at, int count)
        {
            float waterY = WaterLevel();
            for (int i = 0; i < count; i++)
            {
                var lp = new LowPoly().Paint(Color.Lerp(new Color(0.55f, 0.36f, 0.2f), new Color(0.8f, 0.62f, 0.4f), Random.value));
                lp.Box(new Vector3(Random.Range(0.08f, 0.22f), 0.04f, Random.Range(0.3f, 0.7f)));
                var go = lp.Spawn("Astilla", null, IntroSet.VertexToon(), at + Random.insideUnitSphere * 0.6f, false);
                StartCoroutine(Fly(go.transform, Random.insideUnitSphere * 4f + Vector3.up * Random.Range(3f, 7f), Random.insideUnitSphere * 720f, waterY));
            }
        }

        IEnumerator Fly(Transform t, Vector3 v, Vector3 spin, float waterY)
        {
            while (t != null && t.position.y > waterY)
            {
                v += Physics.gravity * Time.deltaTime;
                t.position += v * Time.deltaTime;
                t.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
            if (t == null) yield break;
            if (Random.value < 0.3f) Splash(t.position, 0.4f);
            for (float k = 0; k < 1f && t != null; k += Time.deltaTime / 2.5f) { t.position += Vector3.down * Time.deltaTime * 0.15f; yield return null; }
            if (t != null) Destroy(t.gameObject);
        }

        Piece AddPiece(Transform t, Vector3 brokenPos, Quaternion brokenRot)
        {
            var r = t.GetComponent<Renderer>();
            Renderer original = null;
            // En los niveles cuyas piezas son estáticas (static batching) moverlas no cambia lo que se ve:
            // se oculta la original y se anima una copia con la misma forma y material.
            if (r != null && r.isPartOfStaticBatch)
            {
                original = r;
                var mf = t.GetComponent<MeshFilter>();
                var proxy = new GameObject(t.name + "_Anim", typeof(MeshFilter), typeof(MeshRenderer));
                proxy.transform.SetParent(transform, true);
                proxy.transform.SetPositionAndRotation(t.position, t.rotation);
                proxy.transform.localScale = t.lossyScale;
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || mesh.name.StartsWith("Combined Mesh"))
                {
                    var tmp = GameObject.CreatePrimitive(t.name.Contains("Piling") ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    mesh = tmp.GetComponent<MeshFilter>().sharedMesh; Destroy(tmp);
                }
                proxy.GetComponent<MeshFilter>().sharedMesh = mesh;
                var pr = proxy.GetComponent<MeshRenderer>(); pr.sharedMaterial = r.sharedMaterial;
                r.enabled = false;
                t = proxy.transform; r = pr;
            }
            var piece = new Piece { t = t, pos = t.position, rot = t.rotation, brokenPos = brokenPos, brokenRot = brokenRot, r = r, mat = r != null ? r.sharedMaterial : null, original = original, seed = Random.value * 10f };
            pieces.Add(piece);
            return piece;
        }

        // ------------------------------------------------------------------ Reparación
        // Números dorados suben del agua y cada pieza vuela en arco de vuelta a su sitio, de las orillas hacia el centro,
        // con un destello al encajar. Al final una onda de luz recorre el puente.
        IEnumerator RepairSequence()
        {
            State = BridgeState.Repairing;
            var ui = UIManager.Instance;
            yield return new WaitForSecondsRealtime(1.1f);
            ui?.CloseQuiz(false);
            ui?.BeginCinematic();
            ui?.SetSubtitle("¡La ecuación es correcta! La magia de los números reconstruye el puente...");
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<IsoCameraFollow>() : null;
            if (follow != null) follow.enabled = false;
            BridgeAxes(out Vector3 axis, out Vector3 side, out float len);
            Vector3 c = transform.position;
            Vector3 toPlayer = player != null ? player.position - c : side; toPlayer.y = 0f;
            if (Vector3.Dot(toPlayer, side) < 0f) side = -side;
            StartCoroutine(CamMove(cam, c + side * (len * 1.05f + 5f) + Vector3.up * 4.5f - axis * 2f, c, 1.2f, 44f));

            foreach (var p in pieces) if (p.r != null && repairGlowMaterial != null) p.r.sharedMaterial = repairGlowMaterial;
            var lightGo = new GameObject("RepairLight");
            lightGo.transform.position = transform.position + Vector3.up * 2f;
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 14f; light.intensity = 4f; light.color = new Color(0.5f, 1f, 1f);
            StartCoroutine(RisingNumbers(c, len, 2.8f));
            yield return new WaitForSeconds(0.6f);

            // de las orillas al centro
            var order = new List<Piece>(pieces);
            order.Sort((a, b) => MiddleFraction(b.pos).CompareTo(MiddleFraction(a.pos)));
            const float flight = 0.9f, stagger = 0.16f;
            for (int i = 0; i < order.Count; i++) StartCoroutine(FlyBack(order[i], i * stagger, flight));
            StartCoroutine(CamMove(cam, c + side * (len * 0.75f + 4f) + Vector3.up * 3f + axis * 2f, c + Vector3.up * 0.3f, order.Count * stagger + flight, 40f));
            yield return new WaitForSeconds(order.Count * stagger + flight + 0.1f);

            // onda de luz a lo largo del puente
            for (int k = 0; k < 7; k++)
            {
                float f = (k / 6f - 0.5f);
                CineGlow.Burst(c + axis * f * len + Vector3.up * 0.4f, new Color(1.6f, 2.4f, 2.6f), 14, 1.6f, 0.1f, 1f, -0.3f);
            }
            CameraShake3D.Shake(0.12f, 0.3f);
            foreach (var p in pieces) if (p.r != null) p.r.sharedMaterial = p.mat;
            Destroy(lightGo);

            foreach (var p in pieces) if (p.original != null) { p.original.enabled = true; Destroy(p.t.gameObject); }
            foreach (var s in deckSegments) Destroy(s);
            deckSegments.Clear();
            if (deckRenderer != null) deckRenderer.enabled = true;
            blocker.enabled = false;
            gapWater.SetActive(false);
            yield return new WaitForSeconds(0.8f);

            if (follow != null) follow.enabled = true;
            ui?.EndCinematic();
            State = BridgeState.Repaired;
            ui?.SetObjective("");
            ui?.ShowMessage("¡Puente reparado! El camino está libre.", 3.5f);
            onRepaired.Invoke();
        }

        /// Una pieza sale del agua y vuela en arco hasta su sitio, girando, con estela dorada y destello al encajar.
        IEnumerator FlyBack(Piece p, float delay, float seconds)
        {
            yield return new WaitForSeconds(delay);
            if (p.t == null) yield break;
            if ((p.t.position - p.pos).sqrMagnitude < 0.01f) { CineGlow.Burst(p.pos, new Color(2.6f, 2.1f, 0.9f), 8, 0.8f, 0.07f, 0.6f, -0.2f); yield break; }   // no se movió
            Vector3 a = p.t.position; Quaternion ra = p.t.rotation;
            Vector3 mid = Vector3.Lerp(a, p.pos, 0.5f) + Vector3.up * 2.4f;
            var trail = CineGlow.Motes("Estela", p.t, Vector3.zero, new Color(2.2f, 1.7f, 0.6f), 50f, 0.1f, 0.2f, 0.6f, 0.25f, -0.1f);
            if (a.y < WaterLevel() + 0.3f) Splash(a, 0.6f);
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float k = t * t * (3f - 2f * t);
                Vector3 q = Vector3.Lerp(Vector3.Lerp(a, mid, k), Vector3.Lerp(mid, p.pos, k), k);      // curva de Bézier
                p.t.position = q;
                p.t.rotation = Quaternion.Slerp(ra, p.rot, k) * Quaternion.Euler(0f, (1f - k) * 180f, 0f);
                yield return null;
            }
            p.t.SetPositionAndRotation(p.pos, p.rot);
            trail.Stop(); Destroy(trail.gameObject, 1f);
            CineGlow.Burst(p.pos, new Color(2.6f, 2.1f, 0.9f), 18, 1.2f, 0.08f, 0.7f, -0.2f);
            CameraShake3D.Shake(0.04f, 0.1f);
        }

        /// Números dorados que suben del río mientras se repara el puente.
        IEnumerator RisingNumbers(Vector3 center, float len, float seconds)
        {
            string syms = "0123456789+−×÷=";
            BridgeAxes(out Vector3 axis, out Vector3 side, out _);
            float waterY = WaterLevel();
            for (float t = 0; t < seconds; t += 0.12f)
            {
                Vector3 p = center + axis * Random.Range(-0.5f, 0.5f) * len + side * Random.Range(-2.5f, 2.5f);
                p.y = waterY + 0.2f;
                var gn = CineGlow.Number(syms[Random.Range(0, syms.Length)].ToString(), null, p, new Color(2.6f, 2f, 0.7f), Random.Range(0.3f, 0.55f));
                gn.FadeTo(1f, 3f);
                StartCoroutine(RiseAndFade(gn));
                yield return new WaitForSeconds(0.12f);
            }
        }

        IEnumerator RiseAndFade(GlowNumber gn)
        {
            float speed = Random.Range(1.2f, 2.2f);
            for (float t = 0; t < 2.2f && gn != null; t += Time.deltaTime)
            {
                gn.transform.position += Vector3.up * speed * Time.deltaTime;
                if (t > 1.4f) gn.FadeTo(0f, 2f);
                yield return null;
            }
            if (gn != null) Destroy(gn.gameObject);
        }

        // ------------------------------------------------------------------ Ayudantes
        /// Crea (si faltan) las zonas trigger, el bloqueo y el agua del hueco a partir del tamaño de la cubierta.
        /// Lo usa el menú del editor para que queden visibles en la escena.
        public void BuildHelpers()
        {
            Vector3 size = deck != null ? deck.lossyScale : new Vector3(9f, 0.2f, 14f);
            bool alongZ = size.z >= size.x;
            float len = alongZ ? size.z : size.x, wid = alongZ ? size.x : size.z;
            Vector3 Box(float l, float h, float w) => alongZ ? new Vector3(w, h, l) : new Vector3(l, h, w);
            float topY = deck != null ? deck.position.y + size.y * 0.5f - transform.position.y : 0.1f;

            if (collapseZone == null)
            {
                var c = NewChild("CollapseTrigger").AddComponent<BoxCollider>();
                c.isTrigger = true; c.size = Box(len + 14f, 6f, wid + 10f); c.center = Vector3.up * 2f;
                collapseZone = c.gameObject.AddComponent<TriggerRelay3D>();
            }
            if (repairZone == null)
            {
                var c = NewChild("RepairZone").AddComponent<BoxCollider>();
                c.isTrigger = true; c.size = Box(len + 5f, 5f, wid + 3f); c.center = Vector3.up * 1.5f;
                repairZone = c.gameObject.AddComponent<TriggerRelay3D>();
            }
            if (blocker == null)
            {
                var go = NewChild("Blocker");
                if (GameLayers.Exists(GameLayers.Obstacles)) GameLayers.Set(go, GameLayers.Obstacles);
                blocker = go.AddComponent<BoxCollider>();
                blocker.size = Box(len * 0.55f, 5f, wid); blocker.center = Vector3.up * 2f;
                blocker.enabled = false;
            }
            if (gapWater == null)
            {
                gapWater = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gapWater.name = "GapWater";
                DestroyImmediate(gapWater.GetComponent<Collider>());
                gapWater.transform.SetParent(transform, false);
                gapWater.transform.localPosition = Vector3.up * (topY + 0.02f);
                gapWater.transform.localScale = Box(len * 0.55f, 0.06f, wid - 0.3f);
                if (gapMaterial != null) gapWater.GetComponent<Renderer>().sharedMaterial = gapMaterial;
                gapWater.SetActive(false);
            }
        }

        GameObject NewChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = 2;   // Ignore Raycast: los triggers no interceptan el clic de apuntado
            return go;
        }

        /// El Gran Cero (versión low poly para la cinemática del puente): torso de obsidiana facetada flotando,
        /// ojos y grietas de lava, un gran anillo "0" de energía roja que gira detrás, símbolos que orbitan y brasas.
        GameObject BuildGranCero(Vector3 pos)
        {
            var root = new GameObject("ElGranCero");
            root.transform.position = pos;
            var body = new GameObject("Cuerpo").transform; body.SetParent(root.transform, false);
            var obs = new LowPoly();
            Color o1 = new Color(0.17f, 0.15f, 0.22f), o2 = new Color(0.24f, 0.21f, 0.31f), o3 = new Color(0.31f, 0.27f, 0.4f);
            obs.Paint(o2).At(new Vector3(0, 0.2f, 0), Quaternion.Euler(0, 20, 0), new Vector3(1.5f, 1.7f, 1.1f)).Rock(0.6f, 3, 0.22f);       // torso
            obs.Paint(o3).At(new Vector3(0, 1.25f, 0.05f), Quaternion.Euler(0, 40, 0), new Vector3(0.9f, 0.85f, 0.8f)).Rock(0.5f, 4, 0.2f); // cabeza
            obs.Paint(o1).At(new Vector3(-0.95f, 0.65f, 0), Quaternion.Euler(0, 0, 30), new Vector3(0.7f, 0.6f, 0.7f)).Rock(0.5f, 5, 0.25f); // hombros
            obs.Paint(o1).At(new Vector3(0.95f, 0.65f, 0), Quaternion.Euler(0, 0, -30), new Vector3(0.7f, 0.6f, 0.7f)).Rock(0.5f, 6, 0.25f);
            obs.Paint(o2).At(new Vector3(-1.2f, -0.1f, 0.3f), Quaternion.Euler(20, 0, 15), new Vector3(0.45f, 1.1f, 0.45f)).Rock(0.5f, 7, 0.2f); // brazos
            obs.Paint(o2).At(new Vector3(1.2f, -0.1f, 0.3f), Quaternion.Euler(20, 0, -15), new Vector3(0.45f, 1.1f, 0.45f)).Rock(0.5f, 8, 0.2f);
            obs.Paint(o1).At(new Vector3(0, -0.9f, 0), Quaternion.Euler(180, 0, 0), Vector3.one).Prism(7, 0.55f, 0.05f, 1.4f, false, 0.6f); // cola de humo sólido
            obs.Spawn("Obsidiana", body, IntroSet.VertexToon());
            var lava = CineGlow.Make(new Color(4f, 0.7f, 0.25f), 0f, 1f, 0.35f);
            var cr = new LowPoly().Paint(Color.white);
            cr.At(new Vector3(0, 0.3f, 0.62f), Quaternion.Euler(0, 0, 20), Vector3.one).Box(new Vector3(0.05f, 0.8f, 0.04f));
            cr.At(new Vector3(0.2f, 0.1f, 0.6f), Quaternion.Euler(0, 0, -35), Vector3.one).Box(new Vector3(0.05f, 0.5f, 0.04f));
            cr.At(new Vector3(-0.17f, 1.3f, 0.43f), Quaternion.identity, new Vector3(1.4f, 0.6f, 0.6f)).Rock(0.07f, 1, 0f, 0);   // ojos
            cr.At(new Vector3(0.17f, 1.3f, 0.43f), Quaternion.identity, new Vector3(1.4f, 0.6f, 0.6f)).Rock(0.07f, 2, 0f, 0);
            cr.Spawn("Lava", body, lava, default, false);

            // Anillo "0" (óvalo de segmentos de energía) detrás del cuerpo, siempre de cara a la cámara y girando
            var ringRoot = new GameObject("AuraCero").transform; ringRoot.SetParent(root.transform, false);
            ringRoot.gameObject.AddComponent<Billboard>();
            var ring = new GameObject("Anillo").transform; ring.SetParent(ringRoot, false); ring.localPosition = new Vector3(0, 0.35f, 0.6f);
            var rm = CineGlow.Make(new Color(3.5f, 0.45f, 0.3f), 0f, 1f, 0.4f);
            var rl = new LowPoly().Paint(Color.white);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                rl.At(new Vector3(Mathf.Cos(a) * 1.7f, Mathf.Sin(a) * 2.3f, 0), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg), Vector3.one).Box(new Vector3(0.12f, 0.5f, 0.06f));
            }
            rl.Spawn("Segmentos", ring, rm, default, false);
            ring.gameObject.AddComponent<SpinRing>();
            string[] vars = { "x", "y", "+", "÷", "0" };
            for (int i = 0; i < vars.Length; i++)
            {
                float a = i / (float)vars.Length * Mathf.PI * 2f;
                var gn = CineGlow.Number(vars[i], root.transform, new Vector3(Mathf.Cos(a) * 2.5f, 0.4f + Mathf.Sin(a) * 1.2f, Mathf.Sin(a) * 0.5f), new Color(3f, 0.5f, 0.35f), 0.45f);
                gn.FadeTo(1f, 2f);
            }
            CineGlow.Motes("Brasas", root.transform, Vector3.zero, new Color(3f, 0.6f, 0.2f), 25f, 0.12f, 0.8f, 1.8f, 1.2f, -0.15f);
            var l = new GameObject("RedAura").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.1f); l.range = 12f; l.intensity = 3.5f;
            root.AddComponent<FloatAndSpin>();
            root.transform.localScale = Vector3.zero;
            return root;
        }

        float LongHalf()
        {
            if (deck == null) return 7f;
            return Mathf.Max(deck.lossyScale.x, deck.lossyScale.z) * 0.5f;
        }

        /// 0 = centro del puente, 1 = extremo (a lo largo del eje largo).
        float MiddleFraction(Vector3 worldPos)
        {
            if (deck == null) return 1f;
            bool alongZ = deck.lossyScale.z >= deck.lossyScale.x;
            Vector3 local = worldPos - deck.position;
            float d = Mathf.Abs(alongZ ? Vector3.Dot(local, deck.forward) : Vector3.Dot(local, deck.right));
            return d / LongHalf();
        }

        /// Ningún enemigo debe quedarse flotando sobre el hueco del puente roto.
        void PushEnemiesOffBridge()
        {
            var b = blocker.bounds; b.Expand(1.5f);
            bool alongZ = deck == null || deck.lossyScale.z >= deck.lossyScale.x;
            Vector3 axis = alongZ ? (deck != null ? deck.forward : Vector3.forward) : deck.right;
            foreach (var hs in HealthSystem.Alive.ToArray())
            {
                if (hs == null || hs.team != Team.Enemy) continue;
                var t = hs.transform;
                if (!b.Contains(new Vector3(t.position.x, b.center.y, t.position.z))) continue;
                float s = Mathf.Sign(Vector3.Dot(t.position - transform.position, axis)); if (s == 0f) s = 1f;
                Vector3 target = transform.position + axis * s * (LongHalf() + 1.5f);
                target.y = t.position.y;
                var cc = t.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                t.position = target;
                if (cc != null) cc.enabled = true;
                HitFX.Spark(target + Vector3.up, new Color(0.6f, 0.2f, 1f));
            }
        }

        void PushPlayerOffBlocker()
        {
            if (player == null) return;
            var b = blocker.bounds; b.Expand(1f);
            if (!b.Contains(player.position + Vector3.up)) return;
            var cc = player.GetComponent<CharacterController>();
            Vector3 dir = player.position - transform.position; dir.y = 0f;
            bool alongZ = deck == null || deck.lossyScale.z >= deck.lossyScale.x;
            Vector3 axis = alongZ ? (deck != null ? deck.forward : Vector3.forward) : deck.right;
            float s = Mathf.Sign(Vector3.Dot(dir, axis)); if (s == 0f) s = -1f;
            Vector3 target = transform.position + axis * s * (LongHalf() - 1f);
            target.y = player.position.y;
            if (cc != null) cc.enabled = false;
            player.position = target;
            if (cc != null) cc.enabled = true;
        }

        static IEnumerator Scale(Transform t, Vector3 from, Vector3 to, float d)
        {
            for (float k = 0f; k < 1f; k += Time.deltaTime / d) { if (t == null) yield break; t.localScale = Vector3.LerpUnclamped(from, to, EaseOut(k)); yield return null; }
            if (t != null) t.localScale = to;
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        static float EaseInBack(float t) { t = Mathf.Clamp01(t); const float c = 1.7f; return t * t * ((c + 1f) * t - c); }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
            if (blocker != null) { Gizmos.matrix = blocker.transform.localToWorldMatrix; Gizmos.DrawCube(blocker.center, blocker.size); }
        }
    }

    /// Giro del anillo "0" del Gran Cero (acelera cuando carga el ataque).
    public class SpinRing : MonoBehaviour
    {
        public float speed = 40f;
        void Update() => transform.Rotate(0f, 0f, speed * Time.deltaTime, Space.Self);
    }

    /// Flotación y giro suave (antagonista, coleccionables).
    public class FloatAndSpin : MonoBehaviour
    {
        public float bob = 0.25f, bobSpeed = 2f, spin = 0f;
        Vector3 basePos; bool init;
        void Update()
        {
            if (!init) { basePos = transform.localPosition; init = true; }
            transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bob;
            if (spin != 0f) transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
        }
    }
}
