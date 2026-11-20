using UnityEngine;

namespace MageHero
{
    /// Bola de fuego del jefe: viaja en línea recta a la altura del pecho y quema al mago si lo alcanza.
    /// Comprueba la distancia en vez de usar físicas (no depende de la matriz de capas).
    public class BossFireball : MonoBehaviour
    {
        Vector3 dir; float speed, damage, life = 4.5f, maxDist; Transform target; HealthSystem targetHealth;
        Vector3 center; int groundMask;

        public void Init(Vector3 direction, float spd, float dmg, Transform tgt, HealthSystem hs, Vector3 arenaCenter, float maxDistance, int ground)
        {
            dir = direction.normalized; speed = spd; damage = dmg; target = tgt; targetHealth = hs;
            center = arenaCenter; maxDist = maxDistance; groundMask = ground;
        }

        void Update()
        {
            transform.position += dir * speed * Time.deltaTime;
            transform.Rotate(200f * Time.deltaTime, 340f * Time.deltaTime, 0f);
            float s = 1f + Mathf.Sin(Time.time * 25f) * 0.08f;
            transform.localScale = Vector3.one * 0.55f * s;
            life -= Time.deltaTime;

            if (target != null && targetHealth != null && !targetHealth.IsDead)
            {
                Vector3 chest = target.position + Vector3.up * 0.9f;
                if ((chest - transform.position).sqrMagnitude < 0.95f * 0.95f)
                {
                    targetHealth.TakeDamage(damage);
                    Destroy(gameObject);
                    return;
                }
            }
            Vector3 off = transform.position - center; off.y = 0f;
            bool hitRock = Physics.CheckSphere(transform.position, 0.3f, groundMask, QueryTriggerInteraction.Ignore);
            if (life <= 0f || off.magnitude > maxDist || hitRock) Destroy(gameObject);
        }
    }
}
