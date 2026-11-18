using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageHero
{
    /// Prepara el combate en TODOS los niveles al cargarlos (Otoño, Desierto y Primavera), sin tener que
    /// ejecutar herramientas del editor en cada escena:
    ///  - Mago: vida (100), hechizo (clic / Enter) y PlayerRespawn3D (que abre la pantalla de Game Over).
    ///  - Enemigos numéricos: vida, barra de vida e IA que persigue y ataca al mago.
    ///  - HUD: barra de vida (UIManager) y pantalla de Game Over.
    /// Si la escena ya tiene algo configurado (p. ej. Level_primavera), lo respeta y solo añade lo que falte.
    public static class CombatBootstrap
    {
        public const float PlayerMaxHealth = 100f, EnemyMaxHealth = 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // sceneLoaded llega después de Awake/OnEnable y antes de Start: los Start de la IA y del HUD ya ven al mago con vida.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;                                   // por si se salió de una pausa (Game Over)
            var tagged = GameObject.FindWithTag("Player");
            if (tagged == null) return;                            // menú principal u otra escena sin mago

            var cc = tagged.GetComponentInParent<CharacterController>();
            var player = cc != null ? cc.gameObject : tagged;
            SetupPlayer(player);
            int enemies = SetupEnemies();
            SetupHud();
            Billboard.RestoreOnWorldLabels();
            Debug.Log($"[MageHero] Combate listo en '{scene.name}': {enemies} enemigos persiguen al mago.");
        }

        static void SetupPlayer(GameObject player)
        {
            var hs = player.GetComponent<HealthSystem>();
            if (hs == null)
            {
                hs = player.AddComponent<HealthSystem>();
                hs.SetMaxHealth(PlayerMaxHealth);
                hs.hitInvulnerability = 0.4f;
            }
            hs.team = Team.Player;
            hs.destroyOnDeath = false;

            if (player.GetComponent<ClickAimingCombat3D>() == null && player.GetComponent<PlayerCombat3D>() == null)
            {
                var combat = player.AddComponent<ClickAimingCombat3D>();
                combat.firePoint = FindDeep(player.transform, "Crystal");
            }
            if (player.GetComponent<PlayerRespawn3D>() == null && player.GetComponent<CharacterController>() != null)
                player.AddComponent<PlayerRespawn3D>();
        }

        static int SetupEnemies()
        {
            int n = 0;
            foreach (var ne in Object.FindObjectsByType<NumberEnemy>(FindObjectsSortMode.None))
            {
                if (ne.GetComponent<EnemyAI3D>() == null) EnemyAI3D.MakeCombatant(ne.gameObject, EnemyMaxHealth);
                n++;
            }
            return n;
        }

        static void SetupHud()
        {
            if (Object.FindAnyObjectByType<UIManager>() == null)
            {
                var hud = GameObject.Find("HUD");
                if (hud != null && hud.GetComponent<Canvas>() != null) hud.AddComponent<UIManager>();
            }
            if (Object.FindAnyObjectByType<GameOverScreen>() == null)
                new GameObject("GameOverScreen").AddComponent<GameOverScreen>();
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
