using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MageHero
{
    /// Cuando la vida del mago llega a 0 se abre la pantalla de Game Over (GameOverScreen). Si el jugador
    /// resuelve el problema, ReviveHere() lo reinicia en la posición donde murió, con la vida llena,
    /// unos segundos de invulnerabilidad (parpadeo) y los enemigos cercanos se retiran para no encadenar muertes.
    /// Sin GameOverScreen en la escena, revive al instante (comportamiento anterior).
    [RequireComponent(typeof(HealthSystem), typeof(CharacterController))]
    public class PlayerRespawn3D : MonoBehaviour
    {
        [Min(0f)] public float respawnInvulnerability = 2.5f;
        public float calmEnemiesRadius = 8f;
        public float calmEnemiesSeconds = 3f;
        [Tooltip("Por debajo de esta altura no se guarda la posición de muerte (caídas al vacío).")]
        public float minValidY = -20f;
        public UnityEvent onRespawned = new UnityEvent();

        public Vector3 LastDeathPosition { get; private set; }
        public int Deaths { get; private set; }

        HealthSystem health;
        CharacterController cc;
        PlayerController controller;
        PlayerSafety safety;
        Renderer[] renderers;

        void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.team = Team.Player;
            health.destroyOnDeath = false;
            cc = GetComponent<CharacterController>();
            controller = GetComponent<PlayerController>();
            safety = GetComponent<PlayerSafety>();
            renderers = GetComponentsInChildren<Renderer>();
        }

        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void OnDied(HealthSystem _)
        {
            Deaths++;
            LastDeathPosition = transform.position;
            // Con pantalla de Game Over: el mago solo revive si resuelve el problema (GameOverScreen llama a ReviveHere).
            if (GameOverScreen.Instance != null) { GameOverScreen.Instance.Show(this); return; }
            ReviveHere();
        }

        /// Revive al mago donde cayó, con la vida llena y unos segundos de invulnerabilidad.
        public void ReviveHere()
        {
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;

            if (pos.y < minValidY && safety != null) { safety.RespawnAtSafe(); pos = transform.position; }

            cc.enabled = false;                            // el CharacterController no permite teletransportar con el componente activo
            transform.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
            if (controller != null) controller.ResetMotion();

            health.Revive(respawnInvulnerability);
            EnemyAI3D.CalmAround(pos, calmEnemiesRadius, calmEnemiesSeconds);
            if (UIManager.Instance != null) UIManager.Instance.ShowMessage("¡Has revivido! Sigue intentándolo", 2f);
            onRespawned.Invoke();
            StopAllCoroutines();
            StartCoroutine(Blink(respawnInvulnerability));
        }

        IEnumerator Blink(float seconds)
        {
            float end = Time.time + seconds;
            bool on = true;
            while (Time.time < end)
            {
                on = !on;
                foreach (var r in renderers) if (r != null) r.enabled = on;
                yield return new WaitForSeconds(0.1f);
            }
            foreach (var r in renderers) if (r != null) r.enabled = true;
        }
    }
}
