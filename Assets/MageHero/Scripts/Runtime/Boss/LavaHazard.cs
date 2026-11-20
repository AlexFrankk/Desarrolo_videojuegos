using UnityEngine;

namespace MageHero
{
    /// Charco de lava: quema al mago mientras esté dentro (comprobación por distancia, sin físicas).
    public class LavaHazard : MonoBehaviour
    {
        public float radius = 3f;
        public float damage = 8f, interval = 0.5f;
        public float surfaceY = -0.35f;

        static float nextWarning;
        Transform player; HealthSystem playerHealth;
        float nextTick;

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            var cc = p.GetComponentInParent<CharacterController>();
            player = cc != null ? cc.transform : p.transform;
            playerHealth = player.GetComponentInParent<HealthSystem>();
        }

        void Update()
        {
            if (player == null || playerHealth == null || playerHealth.IsDead) return;
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.magnitude > radius || player.position.y > surfaceY + 0.25f || Time.time < nextTick) return;
            nextTick = Time.time + interval;
            playerHealth.TakeDamage(damage);
            if (Time.time >= nextWarning)
            {
                nextWarning = Time.time + 4f;
                UIManager.Instance?.ShowMessage("¡La lava quema! Sal de ahí", 1.5f);
            }
        }

        void OnDrawGizmosSelected() { Gizmos.color = new Color(1f, .4f, 0f); Gizmos.DrawWireSphere(transform.position, radius); }
    }
}
