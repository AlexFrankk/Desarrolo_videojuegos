using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Director de la progresión en cada nivel (se crea solo al cargar una escena con el mago):
    ///  1) Cuenta los enemigos derrotados → el mago sube de nivel cada 4 (PlayerLevel).
    ///  2) Cada 2 derrotas sube la AMENAZA: los enemigos vivos y los que aparezcan después suben de nivel
    ///     (más daño, atacan más rápido, color más oscuro) y se ofrece un RETO MATEMÁTICO (tecla R) que,
    ///     si se resuelve bien, sube al mago un nivel. Si la amenaza supera al mago en 2 niveles, el reto se abre solo.
    ///  3) Los enemigos derrotados pueden soltar un Tomo de curación (HealingTome).
    ///  4) Construye el camino iluminado hacia el jefe final (BossPath).
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager Instance { get; private set; }

        [Header("Tomo de curación")]
        [Range(0f, 1f)] public float tomeDropChance = 0.35f;
        [Tooltip("Probabilidad extra si el mago tiene menos de la mitad de la vida.")]
        [Range(0f, 1f)] public float tomeLowHealthBonus = 0.25f;

        [Header("Reto matemático")]
        public float failedChallengeCooldown = 15f;
        [Tooltip("Si la amenaza supera al mago en estos niveles, el reto se abre automáticamente.")]
        public int forceChallengeGap = 2;

        public int Kills { get; private set; }
        public int ThreatLevel { get; private set; } = 1;
        public int PendingChallenges { get; private set; }

        PlayerLevel player;
        HealthSystem playerHealth;
        ArithmeticEquation challenge;
        int challengeAttempts;
        float challengeLockedUntil;
        bool forceQueued;

        // HUD
        Text levelText, xpText, threatText, bannerText;
        Image xpFill;
        GameObject banner;

        // ------------------------------------------------------------------ arranque automático
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameObject.FindWithTag("Player") == null) return;           // menú principal
            if (FindAnyObjectByType<ProgressionManager>() == null)
                new GameObject("ProgressionManager").AddComponent<ProgressionManager>();
        }

        void Awake() => Instance = this;

        IEnumerator Start()
        {
            yield return null;                                               // deja que CombatBootstrap prepare mago y enemigos
            var tagged = GameObject.FindWithTag("Player");
            if (tagged == null) { enabled = false; yield break; }
            var cc = tagged.GetComponentInParent<CharacterController>();
            var go = cc != null ? cc.gameObject : tagged;
            playerHealth = go.GetComponent<HealthSystem>();
            if (playerHealth == null) { enabled = false; yield break; }
            player = go.GetComponent<PlayerLevel>();
            if (player == null) player = go.AddComponent<PlayerLevel>();
            player.Changed += RefreshHud;

            HealthSystem.AnyDied += OnAnyDied;
            BuildHud();
            BossPath.BuildForCurrentLevel();
            StartCoroutine(ScanEnemies());
            RefreshHud();
        }

        void OnDestroy()
        {
            HealthSystem.AnyDied -= OnAnyDied;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ enemigos
        /// Da nivel a cada enemigo nuevo (los del nivel al empezar y los que genera EnemySpawner).
        IEnumerator ScanEnemies()
        {
            var wait = new WaitForSeconds(0.5f);
            while (true)
            {
                foreach (var hs in HealthSystem.Alive)
                {
                    if (hs == null || hs.team != Team.Enemy || hs.GetComponent<EnemyLevel>() != null) continue;
                    var el = hs.gameObject.AddComponent<EnemyLevel>();
                    el.SetLevel(LevelForNewEnemy(), refill: true);
                }
                yield return wait;
            }
        }

        int LevelForNewEnemy()
        {
            // Al empezar: casi todos Nv1 y algunos Nv2. Después: nivel de amenaza o uno menos, al azar.
            if (Kills == 0) return Random.value < 0.3f ? 2 : 1;
            return LevelStats.Clamp(Random.Range(ThreatLevel - 1, ThreatLevel + 1));
        }

        void OnAnyDied(HealthSystem hs)
        {
            if (hs == null || hs.team != Team.Enemy) return;
            Kills++;
            player.RegisterKill();
            TryDropTome(hs);

            int newThreat = LevelStats.Clamp(1 + Kills / LevelStats.KillsPerThreatLevel);
            if (newThreat > ThreatLevel)
            {
                ThreatLevel = newThreat;
                StrengthenAliveEnemies();
                if (!player.IsMax) PendingChallenges++;
                UIManager.Instance?.ShowMessage($"¡Los enemigos se volvieron más fuertes!  Amenaza Nv {ThreatLevel}", 3f);
                if (ThreatLevel - player.Level >= forceChallengeGap) forceQueued = true;
            }
            RefreshHud();
        }

        void StrengthenAliveEnemies()
        {
            foreach (var hs in HealthSystem.Alive)
            {
                if (hs == null || hs.team != Team.Enemy) continue;
                var el = hs.GetComponent<EnemyLevel>();
                if (el != null && !el.isBoss && el.Level < ThreatLevel) el.SetLevel(ThreatLevel, refill: false);
            }
        }

        void TryDropTome(HealthSystem enemy)
        {
            var el = enemy.GetComponent<EnemyLevel>();
            float chance = tomeDropChance + (playerHealth.Normalized < 0.5f ? tomeLowHealthBonus : 0f);
            if ((el != null && el.isBoss) || Random.value < chance)
                HealingTome.Spawn(enemy.transform.position);
        }

        // ------------------------------------------------------------------ reto matemático
        void Update()
        {
            if (player == null) return;
            var ui = UIManager.Instance;
            bool busy = ui == null || ui.IsModalOpen || ui.InCinematic || GameOverScreen.IsOpen || MapOverlay.IsOpen;

            if (forceQueued && !busy && PendingChallenges > 0 && Time.time >= challengeLockedUntil)
            {
                forceQueued = false;
                ui.ShowMessage("¡La amenaza te supera! Resuelve el reto para subir de nivel.", 2.5f);
                OpenChallenge();
            }
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && !busy) OpenChallenge();

            if (banner != null)
            {
                bool show = PendingChallenges > 0 && !player.IsMax;
                if (banner.activeSelf != show) banner.SetActive(show);
                if (show)
                {
                    float left = challengeLockedUntil - Time.time;
                    bannerText.text = left > 0f
                        ? $"Reto matemático disponible en {Mathf.CeilToInt(left)} s"
                        : $"¡Reto matemático disponible! Pulsa  R  para subir a Nivel {player.Level + 1}";
                    var c = bannerText.color; c.a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f); bannerText.color = c;
                }
            }
        }

        public void OpenChallenge()
        {
            var ui = UIManager.Instance;
            if (ui == null || player == null) return;
            if (player.IsMax) { PendingChallenges = 0; ui.ShowMessage("Ya eres Nivel 5, el máximo.", 2f); RefreshHud(); return; }
            if (PendingChallenges <= 0) { ui.ShowMessage("Aún no hay reto: derrota enemigos para desbloquearlo.", 2f); return; }
            if (Time.time < challengeLockedUntil) { ui.ShowMessage($"Espera {Mathf.CeilToInt(challengeLockedUntil - Time.time)} s para otro intento.", 2f); return; }

            challenge = ArithmeticEquation.Random();
            challengeAttempts = 0;
            var labels = new string[4];
            for (int i = 0; i < 4; i++) labels[i] = challenge.options[i].ToString();
            ui.OpenQuiz(new UIManager.QuizData
            {
                title = "Reto matemático",
                subtitle = $"Resuélvelo para subir a Nivel {player.Level + 1} y enfrentar a los enemigos de Nv {ThreatLevel}",
                question = challenge.text,
                options = labels
            }, OnChallengeAnswer, OnChallengeExit);
        }

        void OnChallengeAnswer(int index)
        {
            var ui = UIManager.Instance;
            if (index == challenge.correctIndex)
            {
                ui.LockQuiz(index, "¡Correcto! Tu magia se fortalece.");
                PendingChallenges = Mathf.Max(0, PendingChallenges - 1);
                StartCoroutine(CloseThen(1.1f, () => player.LevelUp("con el Reto matemático")));
                return;
            }
            challengeAttempts++;
            if (challengeAttempts >= 2)
            {
                challengeLockedUntil = Time.time + failedChallengeCooldown;
                ui.LockQuiz(challenge.correctIndex, $"La respuesta era {challenge.answer}. Podrás intentarlo de nuevo en {failedChallengeCooldown:0} s (R).");
                StartCoroutine(CloseThen(2.2f, null));
            }
            else ui.MarkOptionWrong(index, $"Incorrecto. {challenge.tip} Te queda 1 intento.");
            RefreshHud();
        }

        void OnChallengeExit() => UIManager.Instance?.ShowMessage("Pulsa R cuando quieras intentar el reto.", 2.5f);

        IEnumerator CloseThen(float seconds, System.Action after)
        {
            yield return new WaitForSecondsRealtime(seconds);
            UIManager.Instance?.CloseQuiz(false);
            after?.Invoke();
            RefreshHud();
        }

        // ------------------------------------------------------------------ HUD
        void BuildHud()
        {
            var hud = GameObject.Find("HUD");
            if (hud == null) return;
            var old = hud.transform.Find("LevelPanel"); if (old != null) Destroy(old.gameObject);
            old = hud.transform.Find("ChallengeBanner"); if (old != null) Destroy(old.gameObject);

            var navy = new Color(0.114f, 0.208f, 0.341f, 1f);
            var gold = new Color(1f, 0.718f, 0.012f, 1f);

            // Panel de nivel, junto a la barra de vida (abajo a la izquierda)
            var panel = Img("LevelPanel", hud.transform, navy);
            var prt = panel.rectTransform; prt.anchorMin = prt.anchorMax = prt.pivot = Vector2.zero;
            prt.anchoredPosition = new Vector2(352f, 14f); prt.sizeDelta = new Vector2(250f, 58f);
            var line = Img("GoldLine", panel.transform, gold).rectTransform;
            line.anchorMin = new Vector2(0, 1); line.anchorMax = Vector2.one; line.pivot = new Vector2(.5f, 1f); line.sizeDelta = new Vector2(0, 3);

            levelText = UIText.Make(panel.transform, "Level", "NIVEL 1", 20, gold, TextAnchor.UpperLeft);
            levelText.fontStyle = FontStyle.Bold;
            Place(levelText.rectTransform, new Vector2(0, 1), new Vector2(12, -6), new Vector2(120, 26));
            threatText = UIText.Make(panel.transform, "Threat", "Amenaza Nv 1", 13, Color.white, TextAnchor.UpperRight);
            threatText.fontStyle = FontStyle.Bold;
            Place(threatText.rectTransform, new Vector2(1, 1), new Vector2(-12, -9), new Vector2(120, 20), new Vector2(1, 1));

            var bg = Img("XpBg", panel.transform, new Color(0.06f, 0.1f, 0.18f, 1f)).rectTransform;
            bg.anchorMin = new Vector2(0, 0); bg.anchorMax = new Vector2(1, 0); bg.pivot = new Vector2(.5f, 0);
            bg.offsetMin = new Vector2(12, 10); bg.offsetMax = new Vector2(-12, 22);
            xpFill = Img("XpFill", bg, new Color(0.5f, 0.75f, 1f, 1f));
            var frt = xpFill.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f); frt.offsetMin = frt.offsetMax = Vector2.zero;
            xpText = UIText.Make(bg, "XpText", "", 10, Color.white);
            var xrt = xpText.rectTransform; xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one; xrt.offsetMin = xrt.offsetMax = Vector2.zero;

            // Aviso del reto (arriba, centrado)
            var b = Img("ChallengeBanner", hud.transform, new Color(0.35f, 0.12f, 0.45f, 0.9f));
            var brt = b.rectTransform; brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -66f); brt.sizeDelta = new Vector2(600f, 40f);
            bannerText = UIText.Make(b.transform, "Text", "", 18, new Color(1f, 0.9f, 0.5f));
            bannerText.fontStyle = FontStyle.Bold;
            var trt = bannerText.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            banner = b.gameObject;
            banner.SetActive(false);
        }

        void RefreshHud()
        {
            if (player == null || levelText == null) return;
            levelText.text = $"NIVEL {player.Level}";
            threatText.text = $"Amenaza Nv {ThreatLevel}";
            threatText.color = LevelStats.LevelColor(ThreatLevel);
            if (player.IsMax)
            {
                xpFill.rectTransform.anchorMax = Vector2.one;
                xpText.text = "NIVEL MÁXIMO";
            }
            else
            {
                float k = player.KillProgress / (float)LevelStats.KillsPerPlayerLevel;
                xpFill.rectTransform.anchorMax = new Vector2(k, 1f);
                xpText.text = $"Victorias {player.KillProgress}/{LevelStats.KillsPerPlayerLevel} para Nivel {player.Level + 1}";
            }
        }

        static Image Img(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
    }
}
