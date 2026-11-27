using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MageHero
{
    /// Director de la cinemática de apertura (Diseños/guion_v2.md y storyboard.md).
    /// Se reproduce la primera vez que se entra al Nivel 1 (Arboleda de Otoño) o cuando el menú pide "Ver historia".
    ///  Escena 1 · La lección silenciosa   (cabaña, hacha, 7 + 3 = 10)
    ///  Escena 2 · La cicatriz en el horizonte (meteorito, Gran Cero, onda de corrupción). Si existe el video
    ///             Resources/Cinematic/Escena2_Cicatriz se reproduce; si no, versión en tiempo real.
    ///  Escena 3 · El presagio (silencio, aves, animales, árbol que cae, Gólem de Piedra)
    ///  Escena 4 · El llamado del maestro (chispa, báculo, título) → gameplay sin cortes.
    /// Una voz en off (Narrator, español de México) cuenta la historia con subtítulos.
    /// Al terminar empieza el tutorial con el Gólem (GolemTutorial); los enemigos del nivel vuelven al acabarlo.
    /// Se puede saltar manteniendo Esc. Todo se construye por código: no hace falta tocar la escena.
    [DefaultExecutionOrder(900)]
    public class IntroCinematic : MonoBehaviour
    {
        public const string LevelScene = "Level_otono";
        public const string SeenKey = "MageHero.IntroVista";
        public const string VideoResource = "Cinematic/Escena2_Cicatriz";

        /// El menú lo activa para volver a ver la historia aunque ya se haya visto.
        public static bool ForceNext;
        public static bool IsPlaying { get; private set; }

        IntroSet set;
        bool play;

        // Actores
        MageRig mage;
        SpiritAthan spirit;
        StoneGolem golem;
        CinematicScreen screen;
        ParticleSystem leaves;
        GameObject smokeColumn;

        // Cámara
        Camera cam;
        IsoCameraFollow iso;
        bool rig;
        Vector3 camPos, camLook;
        float camFov = 40f, camRoll;
        Color skyColor0, fogColor0; float fogEnd0, fogStart0;

        // Estado del juego que se congela durante la cinemática
        readonly List<Behaviour> disabled = new List<Behaviour>();
        readonly List<GameObject> hidden = new List<GameObject>();
        readonly List<Behaviour> enemiesOff = new List<Behaviour>();     // spawners e IA: siguen apagados durante el tutorial
        AudioSource ambience; float ambienceVol0;
        Coroutine main;
        bool finishing;
        Quaternion? fallTreeRot0;          // rotación del árbol antes de caer (por si se salta la cinemática a mitad de la caída)

        // ------------------------------------------------------------------ arranque
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != LevelScene || GameObject.FindWithTag("Player") == null) return;
            var set = IntroSet.Build();
            var go = new GameObject("IntroCinematic");
            var ic = go.AddComponent<IntroCinematic>();
            ic.set = set;
            ic.play = ForceNext || PlayerPrefs.GetInt(SeenKey, 0) == 0;
            ForceNext = false;
        }

        IEnumerator Start()
        {
            if (!play)
            {
                // Ya vista: el nivel empieza directamente (el Gólem espera como jefe al final del camino iluminado).
                yield break;
            }
            IsPlaying = true;
            screen = CinematicScreen.Create();          // negro desde el primer fotograma
            Narrator.SubtitleSink = screen.ShowCaption;
            yield return null;                          // deja que el HUD, el combate y la progresión se construyan
            yield return null;
            Freeze();
            main = StartCoroutine(Run());
        }

        void Update()
        {
            if (IsPlaying && !finishing && screen != null && screen.SkipRequested) StartCoroutine(SkipRoutine());
        }

        // ------------------------------------------------------------------ guion
        IEnumerator Run()
        {
            yield return Scene1();
            yield return Scene2();
            yield return Scene3();
            yield return Scene4();
            yield return Handoff(false);
        }

        // ================================================================== ESCENA 1 · LA LECCIÓN SILENCIOSA
        IEnumerator Scene1()
        {
            Vector3 S = set.stumpTop;
            screen.SetBars(1f);
            screen.SetGrade(18f, new Color(1f, 0.93f, 0.82f), 16f, 0.08f);

            // 1.1 · Negro → plano detalle del tronco sobre el tocón.
            CamCut(S + new Vector3(-1.3f, 0.02f, 0.62f), S + new Vector3(0f, 0.22f, 0f), 30f);
            screen.SetFocus(1.45f, 2.0f, 60f);
            StartCoroutine(CamMove(S + new Vector3(-1.12f, 0.06f, 0.5f), S + new Vector3(0f, 0.22f, 0f), 3.4f, 29f));
            StartCoroutine(screen.Fade(Color.black, 0f, 1.8f));
            mage.HoldAxe(set.axe);
            Narrator.Say("intro_01");
            yield return W(1.6f);

            // 1.2 · ¡TOC! El hacha cae y parte el tronco.
            yield return mage.ChopRaise(0.55f);
            yield return mage.ChopSwing(0.1f);
            SplitLog();
            CameraShake3D.Shake(0.05f, 0.15f);
            yield return SlowMo(0.3f, 0.45f);
            yield return W(0.5f);

            // 1.3 · Grúa ascendente: de las astillas al plano medio del Mago Joven; se seca la frente.
            Vector3 M = mage.Root.position;
            StartCoroutine(CamMove(M + new Vector3(1.35f, 1.55f, 2.75f), M + new Vector3(0f, 1.05f, 0f), 3.2f, 38f));
            StartCoroutine(FocusTo(3.1f, 3.2f, 2.8f));
            mage.ArmsRest();
            yield return W(1.0f);
            mage.WipeForehead(true);
            yield return W(1.4f);
            mage.WipeForehead(false);
            yield return W(0.4f);
            mage.LeaveAxe(set.axe, set.stumpTop + new Vector3(0.18f, -0.06f, 0.05f));

            // 1.4 · Plano general lateral: el maestro (izquierda), el joven y la pila (derecha). Aparece el 7.
            Vector3 pileStand = set.pile.position + new Vector3(-1.05f, 0f, -0.55f);
            CamCut(new Vector3(27.6f, 0f, -19.6f) + Vector3.up * (set.pile.position.y + 2.2f), new Vector3(28.1f, set.pile.position.y + 1.25f, -11.6f), 40f);
            screen.SetFocus(8.5f, 5.6f, 50f);
            StartCoroutine(CamMove(new Vector3(27.9f, set.pile.position.y + 2.0f, -18.9f), new Vector3(28.4f, set.pile.position.y + 1.2f, -11.6f), 4.5f, 39f));
            StartCoroutine(mage.WalkTo(pileStand, 1.7f));
            yield return W(2.4f);
            var seven = CineGlow.Number("7", null, set.pileTop, new Color(2.4f, 1.8f, 0.7f), 0.6f);
            seven.FadeTo(1f, 1.5f);
            Narrator.Say("intro_02");
            mage.Face(set.pile.position);
            yield return W(1.8f);

            // 1.5 · Primer plano del joven (sobre el hombro del maestro): "¿Así está bien?"
            Vector3 spHead = spirit.Head.position;
            Vector3 mChest = mage.Chest;
            Vector3 toMage = (mChest - spHead); toMage.y = 0; toMage.Normalize();
            Vector3 sideL = Vector3.Cross(Vector3.up, toMage);
            CamCut(spHead - toMage * 0.9f + sideL * 0.55f + Vector3.up * 0.05f, mChest + Vector3.up * 0.35f, 26f);
            screen.SetFocus(Vector3.Distance(spHead, mChest) + 0.9f, 2.8f, 70f);
            mage.Face(spirit.transform.position);
            spirit.Look(mage.Head);
            yield return W(0.6f);
            mage.Ask(true);
            yield return W(1.9f);
            mage.Ask(false);

            // 1.6 · Primer plano del maestro: sonríe y dibuja 7 + 3 (las cifras se suman al 7 de la pila).
            Vector3 spFront = spHead + toMage * 2.4f + Vector3.up * -0.1f - sideL * 0.4f;
            CamCut(spFront, spHead + Vector3.down * 0.15f, 30f);
            StartCoroutine(CamMove(spHead + toMage * 1.8f + Vector3.up * -0.1f - sideL * 0.35f, spHead + Vector3.down * 0.15f, 5f, 28f));
            screen.SetFocus(2.2f, 2.4f, 70f);
            spirit.Tilt(9f);
            yield return spirit.Nod(1, 0.8f);
            yield return W(0.4f);
            spirit.Sparks(true);
            spirit.Reach(spHead + toMage * 0.55f + Vector3.up * 0.1f);
            yield return W(0.5f);
            // escribe "+ 3" en el aire con el dedo
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.6f)
            {
                spirit.Reach(spHead + toMage * 0.6f + sideL * Mathf.Lerp(0.18f, -0.18f, t) + Vector3.up * (0.05f + Mathf.Sin(t * 12f) * 0.08f));
                yield return null;
            }
            spirit.Sparks(false);
            seven.Text = "7 + 3";
            seven.Flash(2.5f);
            spirit.Tilt(4f);
            yield return W(0.5f);

            // 1.7 · Plano medio del joven y la pila: cuenta con los dedos y coloca tres troncos.
            Vector3 P = set.pile.position;
            CamCut(P + new Vector3(-2.3f, 1.05f, -2.25f), P + new Vector3(0.05f, 0.62f, 0f), 34f);
            StartCoroutine(CamMove(P + new Vector3(-2.0f, 1.0f, -2.0f), P + new Vector3(0.05f, 0.6f, 0f), 5f, 33f));
            screen.SetFocus(2.9f, 3.0f, 60f);
            mage.Face(P);
            spirit.ClearLook(); spirit.Reach(null);
            yield return mage.Count(3);
            for (int i = 0; i < set.pileExtra.Count; i++)
            {
                yield return mage.PlaceLog(set.pileExtra[i]);
                seven.Flash(1.6f + i * 0.4f);
                yield return W(0.25f);
            }

            // 1.8 · Las cifras se funden en 10, destellan y se deshacen en chispas que suben.
            CamCut(set.pileTop + new Vector3(-1.6f, -0.3f, -1.4f), set.pileTop, 34f);
            screen.SetFocus(2.1f, 2.6f, 60f);
            yield return W(0.4f);
            seven.Text = "10";
            seven.Flash(5f);
            Narrator.Say("intro_03");
            screen.Flash(new Color(1f, 0.9f, 0.6f, 0.35f), 0.6f);
            CineGlow.Burst(set.pileTop, new Color(3f, 2.2f, 0.8f), 60, 1.6f, 0.12f, 2.2f, -0.35f);
            yield return W(0.9f);
            seven.FadeTo(0f, 2f);
            Destroy(seven.gameObject, 2f);
        }

        // ================================================================== ESCENA 2 · LA CICATRIZ EN EL HORIZONTE
        IEnumerator Scene2()
        {
            var clip = Resources.Load<VideoClip>(VideoResource);
            if (clip != null)
            {
                // Las chispas suben y la cámara las sigue hasta el cielo; ahí entra el video pregrabado.
                Vector3 up = set.pileTop + new Vector3(-1f, 6f, -2f);
                StartCoroutine(CamMove(up, up + new Vector3(0, 3f, -6f), 1.4f, 45f));
                yield return W(1.0f);
                Narrator.Say("intro_04"); Narrator.Say("intro_05");
                yield return PlayVideo(clip);
                SkyToCrimson(1f);
                BuildSmokeColumn(1f);
                yield break;
            }
            yield return SkyScarRealtime();
        }

        /// Versión en tiempo real de la escena 2: la cámara se eleva sobre el bosque, el cielo del sur se rasga,
        /// cae el meteorito tras las montañas, se alza la columna de humo y la onda de corrupción barre el mundo.
        IEnumerator SkyScarRealtime()
        {
            screen.SetDepthOfField(false);
            // 2.1 · Grúa vertical rápida siguiendo las chispas, por encima de las copas.
            Vector3 high = new Vector3(29f, set.pile.position.y + 26f, -2f);
            Vector3 south = new Vector3(28f, 10f, -170f);
            StartCoroutine(CamMove(high, south + Vector3.up * 30f, 4.2f, 52f, Ease.InOut));
            StartCoroutine(FogTo(360f, 4f));
            yield return W(3.4f);

            // 2.2 · El cielo se rasga con un destello carmesí.
            StartCoroutine(Rumble(2.5f, 0.06f));
            SkyToCrimson(2.5f);
            screen.Flash(new Color(1f, 0.25f, 0.15f, 0.45f), 0.7f);
            Narrator.Say("intro_04");
            yield return W(0.8f);

            // 2.3 · El meteorito cae tras las montañas del sur.
            Vector3 m0 = new Vector3(60f, 190f, -330f), m1 = new Vector3(28f, 4f, -175f);
            var meteor = Meteor();
            StartCoroutine(CamMove(high + new Vector3(0, -2f, -3f), Vector3.Lerp(m0, m1, 0.6f), 2.0f, 46f, Ease.Out));
            for (float t = 0; t < 1f; t += Time.deltaTime / 2.2f)
            {
                meteor.position = Vector3.Lerp(m0, m1, t * t);
                meteor.Rotate(140f * Time.deltaTime, 90f * Time.deltaTime, 0);
                yield return null;
            }
            Destroy(meteor.gameObject, 0.1f);

            // 2.4 · IMPACTO: destello blanco, temblor y un segundo de silencio.
            screen.Flash(Color.white, 1.2f);
            CameraShake3D.Shake(0.9f, 1.4f);
            SetAmbience(0f, 0.05f);
            BuildSmokeColumn(0f);
            StartCoroutine(GrowSmoke(5f));
            Narrator.Say("intro_05");
            yield return W(1.6f);

            // 2.5 · La onda de corrupción barre el mundo (anillo rojo que viene hacia la cámara).
            StartCoroutine(CamMove(high + new Vector3(0, -6f, 4f), new Vector3(28f, 25f, -170f), 3f, 50f));
            yield return CorruptionWave(m1, 3.0f);
            screen.SetDepthOfField(true);
        }

        // ================================================================== ESCENA 3 · EL PRESAGIO
        IEnumerator Scene3()
        {
            // Entre planos: el joven ha vuelto al tocón con otro tronco y el hacha en alto.
            mage.Teleport(set.mageMark, Vector3.forward);
            mage.HoldAxe(set.axe);
            set.logHalfA.gameObject.SetActive(false); set.logHalfB.gameObject.SetActive(false);
            set.logWhole.gameObject.SetActive(true);
            set.logWhole.position = set.stumpTop; set.logWhole.rotation = Quaternion.identity;
            mage.ArmsUp();
            Vector3 M = set.mageMark;

            // 3.1 · Picado: la cámara regresa del cielo al bosque.
            StartCoroutine(CamMove(M + new Vector3(1.7f, 1.45f, 2.3f), M + new Vector3(0f, 1.35f, 0f), 1.8f, 36f, Ease.In));
            SetAmbience(ambienceVol0, 1.5f);
            screen.SetFocus(2.9f, 2.6f, 60f);
            yield return W(1.9f);

            // 3.2 · Se detiene en seco. Silencio total; las hojas se quedan quietas en el aire.
            SetAmbience(0f, 0.05f);
            var lm = leaves.main; lm.simulationSpeed = 0f;
            Narrator.Say("intro_06");
            StartCoroutine(CamMove(M + new Vector3(1.35f, 1.4f, 1.8f), M + new Vector3(0f, 1.4f, 0f), 3f, 32f));
            yield return W(2.8f);

            // 3.3 · Decenas de aves salen de las copas en pánico, todas hacia el norte.
            Vector3 g = set.golemFrom;
            CamCut(new Vector3(27.2f, g.y + 0.9f, -14.2f), new Vector3(26.6f, g.y + 9f, -23f), 44f);
            screen.SetFocus(9f, 5.6f, 45f);
            CineCritters.Flock(this, new Vector3(26.5f, g.y + 7f, -24f), new Vector3(0.2f, 0f, 1f), 28, 9f);
            StartCoroutine(CamMove(new Vector3(27.2f, g.y + 0.8f, -13.8f), new Vector3(26.8f, g.y + 11f, -18f), 3f, 46f));
            yield return W(2.8f);

            // 3.4 · Un zorro y un ciervo pasan corriendo, ignorando al joven.
            CamCut(M + new Vector3(-2.6f, 0.55f, 2.6f), M + new Vector3(0.6f, 0.65f, -0.8f), 38f);
            screen.SetFocus(3.4f, 3.5f, 50f);
            StartCoroutine(CineCritters.Run(CineCritters.Fox(), M + new Vector3(-6f, 0, -3.5f), M + new Vector3(9f, 0, 3.5f), 7.5f, 9f));
            yield return W(0.5f);
            StartCoroutine(CineCritters.Run(CineCritters.Deer(), M + new Vector3(-7f, 0, -5f), M + new Vector3(9f, 0, 2.5f), 8.5f, 7f));
            yield return W(2.4f);

            // 3.5 · Plano/contraplano: el joven baja el hacha y mira al maestro; el aura del maestro parpadea.
            mage.ArmsRest();
            mage.Face(spirit.transform.position);
            Vector3 mh = mage.Head;
            Vector3 toSp = spirit.Head.position - mh; toSp.y = 0; toSp.Normalize();
            CamCut(mh - toSp * 1.7f + Vector3.Cross(Vector3.up, toSp) * 0.6f + Vector3.up * 0.1f, mh, 30f);
            screen.SetFocus(1.8f, 2.4f, 70f);
            yield return W(1.8f);
            Vector3 sh = spirit.Head.position;
            CamCut(sh + Vector3.right * 1.6f + new Vector3(0.4f, 0f, 1.2f), sh, 30f);
            screen.SetFocus(2.1f, 2.4f, 70f);
            spirit.Tilt(0f);
            spirit.Look(new Vector3(26.5f, sh.y, -24f));
            spirit.SetUnease(0.9f);
            yield return W(2.2f);

            // 3.6 · ¡CRACK! Un árbol frondoso cae derribado.
            Vector3 treeBase = set.fallTree != null ? set.fallTree.position : new Vector3(26.5f, g.y, -20.9f);
            CamCut(treeBase + new Vector3(2.8f, 2.6f, 7.2f), treeBase + new Vector3(0.6f, 2.8f, 0f), 42f);
            screen.SetFocus(7.6f, 4.5f, 45f);
            golem = StoneGolem.Create(g, Quaternion.LookRotation(Vector3.forward));
            golem.SetEyes(0f, true); golem.SetCracks(0.45f); golem.ShowOperation(false);
            yield return TreeFall();

            // 3.7 · Paneo lento hacia el árbol caído: entre el polvo, la silueta del Gólem.
            StartCoroutine(CamMove(treeBase + new Vector3(1.2f, 2.0f, 7.8f), treeBase + new Vector3(0.4f, 2.6f, -1f), 4f, 40f));
            Vector3 onTrunk = treeBase + new Vector3(0.6f, 0f, 0.2f);
            StartCoroutine(golem.WalkTo(new Vector3(onTrunk.x, g.y, onTrunk.z - 1.4f), 1.1f));
            yield return W(3.8f);

            // 3.8 · Cámara lenta: pisa y aplasta el tronco caído.
            Vector3 foot = golem.transform.position + golem.transform.forward * 0.8f;
            CamCut(foot + new Vector3(2.0f, 0.35f, 2.2f), foot + new Vector3(0, 0.9f, 0f), 40f);
            screen.SetFocus(3f, 2.4f, 50f);
            Time.timeScale = 0.45f;
            StartCoroutine(golem.Stomp(0.55f, 0.12f));
            yield return new WaitForSecondsRealtime(1.0f);
            CrushTrunk();
            yield return new WaitForSecondsRealtime(1.3f);
            Time.timeScale = 1f;

            // 3.9 · Primer plano del pecho: arde la operación 8 + 5.
            golem.ShowOperation(true); golem.SetCracks(1.4f);
            Narrator.Say("intro_07");
            Vector3 chest = golem.Chest.TransformPoint(new Vector3(0, 1.08f, 0.7f));
            CamCut(chest + golem.transform.forward * 2.6f + Vector3.up * -0.3f, chest, 30f);
            StartCoroutine(CamMove(chest + golem.transform.forward * 2.1f + Vector3.up * -0.25f, chest, 3f, 28f));
            screen.SetFocus(2.3f, 2.2f, 70f);
            yield return W(2.6f);

            // 3.10 · Gira la cabeza hacia el joven: sus ojos de magma se encienden.
            Vector3 head = golem.Head.position + Vector3.up * 0.25f;
            Vector3 toM = (mage.Head - head); toM.y = 0; toM.Normalize();
            CamCut(head + toM * 3.4f + Vector3.up * -0.5f, head, 30f);
            golem.LookAt(mage.Head);
            yield return W(1.0f);
            golem.SetEyes(1f);
            golem.SetCracks(1.7f);
            StartCoroutine(CamMove(head + toM * 2.2f + Vector3.up * -0.35f, head, 0.5f, 28f, Ease.Out));
            screen.SetFocus(2.2f, 2.0f, 70f);
            CameraShake3D.Shake(0.06f, 0.4f);
            yield return W(1.6f);
        }

        // ================================================================== ESCENA 4 · EL LLAMADO DEL MAESTRO
        IEnumerator Scene4()
        {
            golem.SetCracks(1f);
            Vector3 M = set.mageMark;
            Vector3 toGolem = golem.transform.position - M; toGolem.y = 0; toGolem.Normalize();
            Vector3 rightOfMage = Vector3.Cross(Vector3.up, toGolem);          // a la derecha mirando al Gólem

            // 4.1 · El joven retrocede un paso apretando el hacha.
            mage.Face(golem.transform.position);
            mage.ArmsGuard();
            CamCut(M + toGolem * 1.9f + rightOfMage * -0.9f + Vector3.up * 1.45f, M + Vector3.up * 1.15f, 32f);
            screen.SetFocus(2.1f, 2.6f, 60f);
            StartCoroutine(mage.StepBack(-toGolem, 0.9f, 0.8f));
            yield return W(1.8f);

            // 4.2 · El maestro se desliza flotando hasta su lado.
            Vector3 beside = mage.Root.position + rightOfMage * 1.15f - toGolem * 0.35f;
            beside.y = Ground(beside);
            CamCut(M - toGolem * 4.2f + rightOfMage * 2.2f + Vector3.up * 2f, M + Vector3.up * 1.1f + rightOfMage * 0.6f, 40f);
            screen.SetFocus(4.6f, 4f, 50f);
            spirit.SetUnease(0.25f);
            spirit.Look(golem.Head.position);
            yield return spirit.GlideTo(beside, 2.0f, golem.transform.position);
            yield return W(0.6f);

            // 4.3 · Sobre el hombro del maestro: señala al Gólem… y luego el humo del horizonte.
            Vector3 spH = spirit.Head.position;
            CamCut(spH - toGolem * 1.2f + rightOfMage * 0.55f + Vector3.up * 0.15f, golem.Head.position, 36f);
            screen.SetFocus(Vector3.Distance(spH, golem.transform.position), 5f, 45f);
            spirit.Reach(spirit.transform.position + Vector3.up * 1.55f + toGolem * 0.8f);
            yield return W(1.6f);
            Vector3 smoke = smokeColumn != null ? smokeColumn.transform.position + Vector3.up * 60f : M + toGolem * 100f + Vector3.up * 40f;
            Vector3 toSmoke = (smoke - spH).normalized;
            spirit.Reach(spH + toSmoke * 0.8f + Vector3.down * 0.15f);
            spirit.Look(smoke);
            StartCoroutine(CamMove(spH - toSmoke * 1.4f + rightOfMage * 0.7f + Vector3.up * 0.1f, spH + toSmoke * 30f, 2.4f, 40f));
            screen.SetFocus(60f, 8f, 40f);
            yield return W(2.4f);

            // 4.4 · Primer plano de perfil: la mano luminosa sobre el pecho del joven; pasa la chispa azul.
            spirit.Reach(null);
            Vector3 mid = (spirit.transform.position + mage.Root.position) * 0.5f + Vector3.up * 1.35f;
            CamCut(mid + toGolem * 2.4f + Vector3.up * 0.1f, mid, 30f);
            screen.SetFocus(2.4f, 2.2f, 70f);
            mage.Face(spirit.transform.position);
            spirit.Look(mage.Head);
            yield return W(0.6f);
            spirit.Reach(mage.Chest + (spirit.transform.position - mage.Root.position).normalized * 0.12f);
            yield return W(0.9f);
            CineGlow.Burst(mage.Chest, new Color(0.8f, 1.6f, 3f), 40, 1.1f, 0.08f, 1.4f, -0.1f);
            Narrator.Say("intro_08");
            screen.Flash(new Color(0.6f, 0.85f, 1f, 0.3f), 0.5f);
            mage.Glow(2.2f);
            yield return W(1.6f);

            // 4.5 · Señala el báculo apoyado en la cabaña y asiente con determinación.
            spirit.Reach(spirit.transform.position + Vector3.up * 1.5f + (set.staffProp.position - spirit.transform.position).normalized * 0.8f);
            spirit.Look(set.staffProp.position + Vector3.up);
            CamCut(spirit.Head.position + toGolem * 1.6f + rightOfMage * 0.5f, spirit.Head.position, 32f);
            screen.SetFocus(1.7f, 2.4f, 60f);
            yield return W(1.0f);
            StartCoroutine(CamMove(spirit.Head.position + toGolem * 1.4f + rightOfMage * 0.3f, set.staffProp.position + Vector3.up * 1.1f, 1.4f, 34f));
            StartCoroutine(FocusTo(Vector3.Distance(spirit.Head.position, set.staffProp.position), 3f, 1.4f));
            PulseStaffProp();
            yield return W(1.2f);
            spirit.Look(mage.Head);
            yield return spirit.Nod(1, 0.7f);

            // 4.6 · Tres planos rápidos: el Gólem que avanza aplastando flores → el humo → el maestro.
            Vector3 g0 = golem.transform.position;
            StartCoroutine(golem.WalkTo(set.golemStop, 0.9f));
            StartCoroutine(FlattenFlowers(set.flowersOnPath, 2.4f));
            Vector3 feet = set.flowersOnPath.position;
            CamCut(feet + new Vector3(2.2f, 0.4f, 2.4f), feet + new Vector3(0, 0.6f, -0.6f), 40f);
            screen.SetFocus(3.2f, 3f, 50f);
            yield return W(1.4f);
            Vector3 mh = mage.Head;
            CamCut(mh + new Vector3(0.6f, 0.2f, 2.4f), smoke, 45f);
            screen.SetFocus(80f, 8f, 40f);
            mage.Face(smoke);
            yield return W(1.2f);
            CamCut(spirit.Head.position + (mage.Head - spirit.Head.position).normalized * 1.8f, spirit.Head.position, 30f);
            screen.SetFocus(1.8f, 2.4f, 70f);
            yield return W(1.2f);

            // 4.7 · Plano detalle: suelta el hacha.
            mage.Face(golem.transform.position);
            Vector3 hand = mage.HandPos;
            CamCut(hand + new Vector3(1.0f, -0.1f, 1.0f), hand + Vector3.down * 0.4f, 34f);
            screen.SetFocus(1.4f, 2f, 60f);
            yield return W(0.3f);
            StartCoroutine(mage.DropAxe(set.axe));
            yield return W(1.5f);

            // 4.8 · Travelling: camina con paso firme hacia la cabaña.
            Vector3 grab = set.staffProp.position - set.cabin.forward * 0.6f; grab.y = Ground(grab);
            StartCoroutine(mage.WalkTo(grab, 2.0f));
            StartCoroutine(CamFollow(() => mage.Root.position + new Vector3(0.6f, 1.5f, 3.2f), () => mage.Root.position + Vector3.up * 1.1f, 2.6f, 40f));
            screen.SetFocus(3.4f, 3.2f, 50f);
            yield return W(2.7f);

            // 4.9 · Empuña el báculo: las runas se encienden como números dorados y la punta estalla en luz.
            mage.Face(set.staffProp.position);
            Vector3 c0 = mage.Root.position;
            StartCoroutine(CamOrbit(c0 + Vector3.up * 1.2f, 2.6f, 0.45f, 200f, 120f, 4f, 38f));
            screen.SetFocus(2.6f, 2.4f, 55f);
            yield return W(0.6f);
            mage.TakeStaff(set.staffProp);
            yield return W(0.2f);
            StaffAwakening();
            Narrator.Say("intro_09");
            yield return W(3.2f);

            // 4.10 · Transición fluida detrás del hombro: el maestro a su derecha señala al Gólem.
            Vector3 fight = set.mageMark + new Vector3(0.8f, 0f, -1.2f); fight.y = Ground(fight);
            StartCoroutine(mage.WalkTo(fight, 1.8f));
            Vector3 toG2 = set.golemStop - fight; toG2.y = 0; toG2.Normalize();
            Vector3 right2 = Vector3.Cross(Vector3.up, toG2);
            Vector3 sp2 = fight + right2 * 1.3f - toG2 * 0.2f; sp2.y = Ground(sp2);
            StartCoroutine(spirit.GlideTo(sp2, 2.2f, set.golemStop));
            StartCoroutine(CamFollow(() => mage.Root.position - toG2 * 2.4f + right2 * -0.75f + Vector3.up * 1.85f,
                                     () => set.golemStop + Vector3.up * 2.2f, 2.6f, 42f));
            screen.SetFocus(6f, 5f, 45f);
            yield return W(2.4f);
            mage.Face(set.golemStop);
            spirit.Look(golem.Head.position);
            spirit.Reach(spirit.transform.position + Vector3.up * 1.5f + toG2 * 0.85f);
        }

        // ================================================================== TÍTULO Y PASO AL JUEGO
        IEnumerator Handoff(bool skipped)
        {
            if (!skipped)
            {
                // deja terminar la frase del narrador antes del título
                for (float t = 0; t < 9f && Narrator.IsSpeaking; t += Time.unscaledDeltaTime) yield return null;
                // T.1 · Título
                yield return screen.ShowTitle("MAGO DE LOS NÚMEROS", "", 1.2f, 1.6f, 1.0f);
            }

            // T.2 · La cámara se aleja hasta el encuadre de juego (detrás del mago, mirando al Gólem).
            finishing = true;
            screen.AllowSkip = false;
            Vector3 toG = set.golemStop - mage.Root.position; toG.y = 0;
            if (iso != null)
            {
                iso.yaw = Quaternion.LookRotation(toG.normalized).eulerAngles.y;
                iso.GoalPose(out Vector3 gp, out Quaternion gr);
                Vector3 gl = gp + gr * Vector3.forward * 10f;
                StartCoroutine(CamMove(gp, gl, skipped ? 0.01f : 2.4f, iso.fieldOfView, Ease.InOut));
            }
            StartCoroutine(SkyRestore(3f));
            StartCoroutine(FogTo(fogEnd0, 3f));
            StartCoroutine(screen.Release(skipped ? 0.6f : 2.4f));
            spirit.Reach(null);
            // Espera a que se retiren barras, fundido y post-procesado antes de destruir la pantalla de cine.
            // (Antes, al saltar, se destruía a los 0.1 s con Fade/Release aún en marcha → MissingReferenceException.)
            float minWait = skipped ? 0.85f : 2.45f;
            for (float t = 0; t < minWait || (t < 4f && !screen.Released); t += Time.unscaledDeltaTime) yield return null;
            Finish();
        }

        IEnumerator SkipRoutine()
        {
            finishing = true;
            screen.AllowSkip = false;
            if (main != null) StopCoroutine(main);
            Time.timeScale = 1f;
            Narrator.Stop();
            yield return screen.Fade(Color.black, 1f, 0.35f);
            StopAllCoroutines();
            StartCoroutine(SkipFinish());
        }

        IEnumerator SkipFinish()
        {
            // Estado final del mundo, como si la cinemática hubiera terminado.
            foreach (var l in set.pileExtra) l.gameObject.SetActive(true);
            set.logWhole.gameObject.SetActive(false);
            set.logHalfA.gameObject.SetActive(true); set.logHalfB.gameObject.SetActive(true);
            if (set.fallTree != null && set.fallTree.gameObject.activeSelf && !ForestTrees.IsToppled(set.fallTree))
            {
                set.fallTree.rotation = Quaternion.AngleAxis(-88f, Vector3.forward) * (fallTreeRot0 ?? set.fallTree.rotation);
                ForestTrees.MarkToppled(set.fallTree);
            }
            foreach (var t in set.pathTrees) ForestTrees.ToppleInstant(t, ForestTrees.AwayFromPath(t, set.golemFrom, set.golemStop));
            if (golem == null) golem = StoneGolem.Create(set.golemStop, Quaternion.identity);
            golem.StopAllCoroutines();
            golem.transform.position = set.golemStop;
            golem.SetWalk(0f); golem.SetEyes(1f, true); golem.SetCracks(1f); golem.ShowOperation(true);
            Time.timeScale = 1f;
            if (smokeColumn == null) { BuildSmokeColumn(1f); }
            SkyToCrimson(0.01f);
            var lm = leaves.main; lm.simulationSpeed = 1f;
            Vector3 fight = set.mageMark + new Vector3(0.8f, 0f, -1.2f); fight.y = Ground(fight);
            mage.Teleport(fight, set.golemStop - fight);
            mage.DropAxeInstant(set.axe);
            mage.TakeStaff(set.staffProp);
            Vector3 toG = set.golemStop - fight; toG.y = 0; toG.Normalize();
            Vector3 sp = fight + Vector3.Cross(Vector3.up, toG) * 1.3f; sp.y = Ground(sp);
            spirit.StopAllCoroutines();
            spirit.transform.position = sp; spirit.SetUnease(0.2f);
            golem.transform.rotation = Quaternion.LookRotation(-toG);
            golem.LookAt(mage.Head);
            SetAmbience(ambienceVol0, 0.5f);
            yield return null;
            StartCoroutine(screen.Fade(Color.black, 0f, 0.8f));
            yield return Handoff(true);
        }

        /// Devuelve el control al jugador y deja el mundo listo para el primer combate.
        void Finish()
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            rig = false;
            Transform playerT = mage.Root;
            mage.Release();
            mage = null;
            Narrator.Stop();
            Narrator.SubtitleSink = null;
            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
            // Los enemigos siguen apagados/ocultos hasta que termine el tutorial.
            var enemies = new List<Behaviour>(enemiesOff); var hiddenEnemies = new List<GameObject>(hidden);
            enemiesOff.Clear(); hidden.Clear();
            if (iso != null) { iso.Snap(); iso.enabled = true; }
            if (leaves != null) { var lm = leaves.main; lm.simulationSpeed = 1f; }
            SetAmbience(ambienceVol0, 1f);
            var ui = UIManager.Instance;
            if (ui != null) ui.EndCinematic();
            GolemTutorial.Begin(golem, spirit, playerT, set.golemFrom, () =>
            {
                foreach (var b in enemies) if (b != null) b.enabled = true;
                foreach (var g in hiddenEnemies) if (g != null) g.SetActive(true);
            });
            IsPlaying = false;
            if (screen != null) Destroy(screen.gameObject, 0.1f);
            Destroy(this, 0.2f);
        }

        // ------------------------------------------------------------------ preparación
        void Freeze()
        {
            cam = Camera.main;
            iso = cam != null ? cam.GetComponent<IsoCameraFollow>() : null;
            skyColor0 = cam.backgroundColor; fogColor0 = RenderSettings.fogColor; fogEnd0 = RenderSettings.fogEndDistance; fogStart0 = RenderSettings.fogStartDistance;
            Off(iso);
            foreach (var s in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) OffEnemy(s);
            foreach (var ai in FindObjectsByType<EnemyAI3D>(FindObjectsSortMode.None))
            {
                OffEnemy(ai);
                if (Vector3.Distance(ai.transform.position, set.stumpTop) < 26f && ai.gameObject.activeSelf) { ai.gameObject.SetActive(false); hidden.Add(ai.gameObject); }
            }
            // HUD: se ocultan todos los lienzos de pantalla (menos el de la cinemática).
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && c.gameObject != screen.gameObject) Off(c);
            var ui = UIManager.Instance;
            if (ui != null) ui.BeginCinematic();

            var li = FindAnyObjectByType<LevelInfo>();
            ambience = li != null ? li.GetComponent<AudioSource>() : null;
            ambienceVol0 = ambience != null ? ambience.volume : 0.35f;

            var player = GameObject.FindWithTag("Player");
            mage = new MageRig(player.transform, this, disabled);
            mage.Teleport(set.mageMark, set.mageFacing);
            Vector3 toM = set.mageMark - set.spiritSeat; toM.y = 0;
            spirit = SpiritAthan.Create(set.spiritSeat - Vector3.up * 0.28f, Quaternion.LookRotation(toM.normalized));
            spirit.Look(set.mageMark + Vector3.up * 1.4f);
            leaves = CineCritters.Leaves(set.stumpTop, 16f);
            rig = true;
        }

        void Off(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; disabled.Add(b); } }
        void OffEnemy(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; enemiesOff.Add(b); } }

        // ------------------------------------------------------------------ cámara
        enum Ease { Linear, InOut, In, Out }
        static float E(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.InOut: return t * t * (3f - 2f * t);
                case Ease.In: return t * t;
                case Ease.Out: return 1f - (1f - t) * (1f - t);
                default: return t;
            }
        }

        int camToken;
        void CamCut(Vector3 pos, Vector3 look, float fov) { camToken++; camPos = pos; camLook = look; camFov = fov; }

        IEnumerator CamMove(Vector3 pos, Vector3 look, float seconds, float fov, Ease e = Ease.InOut)
        {
            int token = ++camToken;
            Vector3 p0 = camPos, l0 = camLook; float f0 = camFov;
            for (float t = 0; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                if (token != camToken) yield break;
                float k = E(e, t);
                camPos = Vector3.Lerp(p0, pos, k); camLook = Vector3.Lerp(l0, look, k); camFov = Mathf.Lerp(f0, fov, k);
                yield return null;
            }
            if (token == camToken) { camPos = pos; camLook = look; camFov = fov; }
        }

        IEnumerator CamFollow(Func<Vector3> pos, Func<Vector3> look, float seconds, float fov)
        {
            int token = ++camToken;
            Vector3 p0 = camPos, l0 = camLook; float f0 = camFov;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (token != camToken) yield break;
                float k = E(Ease.InOut, Mathf.Clamp01(t / 0.8f));
                camPos = Vector3.Lerp(p0, pos(), k); camLook = Vector3.Lerp(l0, look(), k); camFov = Mathf.Lerp(f0, fov, k);
                yield return null;
            }
        }

        /// Órbita alrededor de un punto (contrapicado que gira alrededor del mago).
        IEnumerator CamOrbit(Vector3 center, float radius, float height, float fromYaw, float toYaw, float seconds, float fov)
        {
            int token = ++camToken;
            camFov = fov;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                if (token != camToken) yield break;
                float yaw = Mathf.Lerp(fromYaw, toYaw, E(Ease.InOut, t)) * Mathf.Deg2Rad;
                camPos = center + new Vector3(Mathf.Sin(yaw) * radius, height - 0.9f, Mathf.Cos(yaw) * radius);
                camLook = center + Vector3.up * 0.25f;
                yield return null;
            }
        }

        IEnumerator FocusTo(float distance, float aperture, float seconds)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                screen.SetFocus(Mathf.Lerp(3f, distance, t), aperture);
                yield return null;
            }
        }

        void LateUpdate()
        {
            if (mage != null) mage.Apply(Time.deltaTime);
            if (!rig || cam == null) return;
            cam.transform.position = camPos;
            Vector3 d = camLook - camPos;
            if (d.sqrMagnitude > 1e-6f) cam.transform.rotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0, 0, camRoll);
            cam.fieldOfView = camFov;
        }

        // ------------------------------------------------------------------ efectos de la escena
        WaitForSeconds W(float s) => new WaitForSeconds(s);

        IEnumerator SlowMo(float scale, float realSeconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
        }

        void SplitLog()
        {
            set.logWhole.gameObject.SetActive(false);
            set.logHalfA.gameObject.SetActive(true); set.logHalfB.gameObject.SetActive(true);
            set.logHalfA.position = set.stumpTop + new Vector3(-0.09f, 0, 0); set.logHalfA.rotation = Quaternion.identity;
            set.logHalfB.position = set.stumpTop + new Vector3(0.09f, 0, 0); set.logHalfB.rotation = Quaternion.Euler(0, 180, 0);
            StartCoroutine(Topple(set.logHalfA, Vector3.left, 1f));
            StartCoroutine(Topple(set.logHalfB, Vector3.right, -1f));
            Chips(set.stumpTop + Vector3.up * 0.3f, 14);
        }

        /// Media leña que cae hacia un lado desde el tocón y rueda un poco.
        IEnumerator Topple(Transform t, Vector3 dir, float sign)
        {
            Vector3 p0 = t.position; Quaternion r0 = t.rotation;
            float ground = Ground(p0 + dir * 0.6f);
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.55f)
            {
                float e = E(Ease.In, k);
                t.position = new Vector3(p0.x + dir.x * 0.55f * e, Mathf.Lerp(p0.y, ground + 0.12f, e), p0.z + dir.z * 0.55f * e);
                t.rotation = Quaternion.AngleAxis(sign * 90f * e, Vector3.forward) * r0;
                yield return null;
            }
        }

        /// Astillas de madera que saltan (pequeños trozos con trayectoria balística).
        void Chips(Vector3 at, int count)
        {
            var rng = new System.Random(Time.frameCount);
            for (int i = 0; i < count; i++)
            {
                var lp = new LowPoly().Paint(MenuStyle.Hex(0xd9b07a));
                lp.Box(new Vector3(0.09f, 0.025f, 0.035f));
                var go = lp.Spawn("Astilla", null, IntroSet.VertexToon(), default, false);
                go.transform.position = at;
                Vector3 v = new Vector3(((float)rng.NextDouble() - .5f) * 3f, 1.5f + (float)rng.NextDouble() * 2.5f, ((float)rng.NextDouble() - .5f) * 3f);
                StartCoroutine(Ballistic(go.transform, v, (float)rng.NextDouble() * 720f));
            }
        }

        IEnumerator Ballistic(Transform t, Vector3 v, float spin)
        {
            float ground = Ground(t.position);
            while (t != null && t.position.y > ground + 0.02f)
            {
                v += Physics.gravity * Time.deltaTime;
                t.position += v * Time.deltaTime;
                t.Rotate(spin * Time.deltaTime, spin * 0.7f * Time.deltaTime, 0);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject, 6f);
        }

        IEnumerator TreeFall()
        {
            if (set.fallTree == null) { yield return W(1f); yield break; }
            Transform tree = set.fallTree;
            // Primero tiembla (algo lo empuja desde dentro del bosque), luego cae hacia el este.
            Vector3 p0 = tree.position; Quaternion r0 = tree.rotation;
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                tree.position = p0 + new Vector3(Mathf.Sin(t * 60f) * 0.06f, 0, Mathf.Cos(t * 47f) * 0.05f);
                yield return null;
            }
            tree.position = p0;
            CameraShake3D.Shake(0.1f, 0.3f);
            fallTreeRot0 = r0;
            // El Gólem se abre paso: los demás árboles de su ruta caen hacia los lados (ya no los atraviesa).
            for (int i = 0; i < set.pathTrees.Count; i++)
            {
                var pt = set.pathTrees[i];
                ForestTrees.StartTopple(pt, ForestTrees.AwayFromPath(pt, set.golemFrom, set.golemStop), 1.0f, 0.15f + i * 0.22f);
            }
            Vector3 axis = Vector3.forward;              // gira sobre Z: la copa cae hacia +X (este), cruzando el sendero del Gólem
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.1f)
            {
                float e = t * t * t;                     // cada vez más rápido
                tree.rotation = Quaternion.AngleAxis(-88f * e, axis) * r0;
                yield return null;
            }
            tree.rotation = Quaternion.AngleAxis(-88f, axis) * r0;
            ForestTrees.MarkToppled(tree);
            CameraShake3D.Shake(0.35f, 0.5f);
            Dust(p0 + new Vector3(3.5f, 0.4f, 0), 60, 2.2f);
            Dust(p0 + new Vector3(1.5f, 0.4f, 0), 30, 1.6f);
            // rebote
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                tree.rotation = Quaternion.AngleAxis(-88f + Mathf.Sin(t * Mathf.PI) * 4f, axis) * r0;
                yield return null;
            }
        }

        void CrushTrunk()
        {
            if (set.fallTree != null)
            {
                var t = set.fallTree;
                t.localScale = new Vector3(t.localScale.x * 1.05f, t.localScale.y * 0.97f, t.localScale.z * 0.8f);
                t.position += Vector3.down * 0.12f;
            }
            Vector3 foot = golem.transform.position + golem.transform.forward * 0.4f;
            Dust(foot, 40, 1.8f);
            Chips(foot + Vector3.up * 0.4f, 18);
        }

        void Dust(Vector3 at, int count, float size)
        {
            var ps = CineGlow.Motes("Polvo", null, at, new Color(0.42f, 0.36f, 0.28f), 0f, size, 2.4f, 2.4f, 0.8f, -0.01f);
            ps.Emit(count);
            Destroy(ps.gameObject, 3f);
        }

        IEnumerator FlattenFlowers(Transform patch, float delay)
        {
            yield return W(delay);
            if (patch == null) yield break;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
            {
                patch.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.15f, t), 1f);
                yield return null;
            }
        }

        void PulseStaffProp()
        {
            var cr = set.staffProp.Find("Crystal");
            Vector3 at = cr != null ? cr.GetComponent<Renderer>().bounds.center : set.staffProp.position + Vector3.up * 1.9f;
            CineGlow.Burst(at, new Color(0.6f, 1.6f, 2.6f), 16, 0.5f, 0.1f, 1.2f, -0.1f);
        }

        /// Números dorados que orbitan la punta del báculo y un estallido de luz blanca.
        void StaffAwakening()
        {
            Vector3 tip = mage.CrystalPos;
            string[] syms = { "1", "2", "3", "+", "−", "×", "÷" };
            for (int i = 0; i < syms.Length; i++)
            {
                var gn = CineGlow.Number(syms[i], null, tip, new Color(2.8f, 2.0f, 0.7f), 0.28f);
                StartCoroutine(OrbitNumber(gn, tip, i / (float)syms.Length * Mathf.PI * 2f, 0.15f * i));
            }
            StartCoroutine(TipFlash(1.0f));
        }

        IEnumerator OrbitNumber(GlowNumber gn, Vector3 center, float angle, float delay)
        {
            yield return W(delay);
            gn.FadeTo(1f, 4f);
            for (float t = 0; t < 2.6f; t += Time.deltaTime)
            {
                center = mage.CrystalPos;
                float a = angle + t * 2.4f, r = 0.35f + t * 0.25f;
                gn.transform.position = center + new Vector3(Mathf.Cos(a) * r, 0.2f + t * 0.35f, Mathf.Sin(a) * r);
                if (t > 1.8f) gn.FadeTo(0f, 2f);
                yield return null;
            }
            Destroy(gn.gameObject);
        }

        IEnumerator TipFlash(float delay)
        {
            yield return W(delay);
            Vector3 tip = mage.CrystalPos;
            screen.Flash(new Color(1f, 1f, 1f, 0.75f), 0.9f);
            CineGlow.Burst(tip, new Color(2.5f, 2.8f, 3.2f), 70, 2.6f, 0.1f, 1.4f, 0f);
            screen.SetBloom(2.2f);
            var lg = new GameObject("LuzBaculo").AddComponent<Light>();
            lg.type = LightType.Point; lg.color = new Color(0.85f, 0.95f, 1f); lg.range = 7f; lg.intensity = 6f;
            lg.transform.position = tip;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.4f)
            {
                lg.transform.position = mage.CrystalPos;
                lg.intensity = Mathf.Lerp(6f, 1.2f, t);
                screen.SetBloom(Mathf.Lerp(2.2f, 1.1f, t));
                yield return null;
            }
            lg.transform.SetParent(mage.CrystalTransform, true);
            Destroy(lg.gameObject, 6f);
        }

        // ---- cielo, humo y onda
        void SkyToCrimson(float seconds) => StartCoroutine(SkyLerp(new Color(0.93f, 0.62f, 0.48f), new Color(0.9f, 0.6f, 0.5f), seconds));
        IEnumerator SkyRestore(float seconds) { yield return SkyLerp(skyColor0, fogColor0, seconds); }

        IEnumerator SkyLerp(Color sky, Color fog, float seconds)
        {
            Color s0 = cam.backgroundColor, f0 = RenderSettings.fogColor;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                cam.backgroundColor = Color.Lerp(s0, sky, t);
                RenderSettings.fogColor = Color.Lerp(f0, fog, t);
                yield return null;
            }
            cam.backgroundColor = sky; RenderSettings.fogColor = fog;
        }

        IEnumerator FogTo(float end, float seconds)
        {
            float e0 = RenderSettings.fogEndDistance;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / seconds) { RenderSettings.fogEndDistance = Mathf.Lerp(e0, end, t); yield return null; }
            RenderSettings.fogEndDistance = end;
        }

        IEnumerator Rumble(float seconds, float amp)
        {
            for (float t = 0; t < seconds; t += 0.1f) { CameraShake3D.Shake(amp, 0.15f); yield return W(0.1f); }
        }

        Transform Meteor()
        {
            var t = new GameObject("Meteorito").transform;
            var core = new LowPoly().Paint(MenuStyle.Hex(0x2a2220));
            core.Rock(5.5f, 66, 0.25f);
            core.Spawn("Roca", t, RuntimeMaterials.Toon(MenuStyle.Hex(0x2a2220)), default, false);
            var fire = new LowPoly().Paint(Color.white);
            fire.Rock(7f, 67, 0.3f);
            fire.Spawn("Fuego", t, CineGlow.Make(new Color(4f, 1.4f, 0.3f), 1.5f, 0.2f, 0.4f), default, false);
            var trail = CineGlow.Motes("Estela", t, Vector3.zero, new Color(4f, 1.3f, 0.25f), 220f, 6f, 1.5f, 1.6f, 4f, 0f);
            var tm = trail.main; tm.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
            var smoke = CineGlow.Motes("Humo", t, Vector3.zero, new Color(0.25f, 0.18f, 0.15f), 120f, 10f, 0.8f, 3f, 4f, 0f);
            var sm = smoke.main; sm.startSize = new ParticleSystem.MinMaxCurve(8f, 16f);
            return t;
        }

        void BuildSmokeColumn(float grown)
        {
            if (smokeColumn != null) return;
            Vector3 at = new Vector3(28f, 0f, -175f);
            smokeColumn = new GameObject("ColumnaDeHumo_GranCero");
            smokeColumn.transform.position = at;
            var sh = Shader.Find("MageHero/Smoke");
            var lp = new LowPoly().Paint(Color.white);
            lp.Prism(14, 10f, 26f, 150f, false, 0.6f);
            Material mat;
            if (sh != null)
            {
                mat = new Material(sh);
                mat.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.08f, 0.9f));
                mat.SetColor("_EdgeColor", new Color(1f, 0.3f, 0.08f, 1f));
                mat.SetFloat("_EdgeStrength", 1.4f);
                mat.SetFloat("_NoiseScale", 0.05f);
                mat.SetFloat("_NoiseSpeed", 0.06f);
                mat.SetFloat("_Dissolve", 0.25f);
                mat.SetFloat("_Wobble", 2.5f);
                mat.SetFloat("_Soft", 2f);
            }
            else mat = RuntimeMaterials.Toon(new Color(0.15f, 0.1f, 0.1f));
            var go = lp.Spawn("Humo", smokeColumn.transform, mat, default, false);
            var glow = new LowPoly().Paint(Color.white);
            glow.At(Vector3.zero, Quaternion.identity, new Vector3(1f, 0.35f, 1f)).Rock(30f, 8, 0.2f);
            glow.Spawn("ResplandorLava", smokeColumn.transform, CineGlow.Make(new Color(2.2f, 0.5f, 0.1f), 1.2f, 0.25f, 0.3f), default, false);
            smokeColumn.transform.localScale = new Vector3(1f, Mathf.Max(0.02f, grown), 1f);
        }

        IEnumerator GrowSmoke(float seconds)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                if (smokeColumn != null) smokeColumn.transform.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, t), Mathf.Lerp(0.02f, 1f, E(Ease.Out, t)), Mathf.Lerp(0.6f, 1f, t));
                yield return null;
            }
        }

        /// Onda de corrupción: un anillo rojo y negro que se expande desde el cráter y pasa por encima de la cámara.
        IEnumerator CorruptionWave(Vector3 origin, float seconds)
        {
            var go = new GameObject("OndaDeCorrupcion");
            go.transform.position = origin + Vector3.up * 8f;
            var lp = new LowPoly().Paint(Color.white);
            int n = 64; float r0 = 0.94f, r1 = 1f, h = 0.035f;
            for (int i = 0; i < n; i++)
            {
                float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
                Vector3 i0 = new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0), i1 = new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                Vector3 o0 = new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1), o1 = new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                lp.Quad(i0, o0, o1, i1); lp.Quad(i0, i1, o1, o0);
            }
            var mat = CineGlow.Make(new Color(3f, 0.35f, 0.15f), 0f, 1f, 0.3f);
            lp.Spawn("Anillo", go.transform, mat, default, false);
            float dist = Vector3.Distance(origin, cam.transform.position) + 40f;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float r = Mathf.Lerp(5f, dist, E(Ease.Out, t));
                go.transform.localScale = new Vector3(r, r * 0.6f, r);
                CineGlow.SetIntensity(mat, Mathf.Lerp(1.6f, 0.4f, t));
                if (t > 0.55f && t < 0.6f) screen.Flash(new Color(0.6f, 0.05f, 0.05f, 0.4f), 0.8f);
                yield return null;
            }
            screen.SetGrade(4f, new Color(1f, 0.86f, 0.82f), 6f, 0f);    // el mundo pierde color tras la onda
            Destroy(go);
        }

        IEnumerator PlayVideo(VideoClip clip)
        {
            var rt = new RenderTexture(1920, 1080, 0);
            var img = new GameObject("Video", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(screen.transform, false);
            img.transform.SetSiblingIndex(0);
            MenuStyle.Stretch(img.rectTransform);
            img.texture = rt;
            img.raycastTarget = false;
            var vp = img.gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false; vp.clip = clip; vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.Prepare();
            while (!vp.isPrepared) yield return null;
            SetAmbience(0f, 0.3f);
            vp.Play();
            yield return new WaitForSeconds(0.1f);
            while (vp.isPlaying && !finishing) yield return null;
            Destroy(img.gameObject);
            rt.Release(); Destroy(rt);
        }

        void SetAmbience(float vol, float seconds) { if (ambience != null) StartCoroutine(AmbienceTo(vol, seconds)); }
        IEnumerator AmbienceTo(float vol, float seconds)
        {
            float v0 = ambience.volume;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds)) { ambience.volume = Mathf.Lerp(v0, vol, t); yield return null; }
            ambience.volume = vol;
        }

        public static float Ground(Vector3 p)
        {
            return RuntimeMaterials.Ground(p, out Vector3 hit) ? hit.y : p.y;
        }

        void OnDestroy()
        {
            IsPlaying = false;
            if (screen != null && Narrator.SubtitleSink == (Action<string, float>)screen.ShowCaption) Narrator.SubtitleSink = null;
            if (Time.timeScale < 1f) Time.timeScale = 1f;
        }
    }
}
