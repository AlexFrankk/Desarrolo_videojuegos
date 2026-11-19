using UnityEngine;

namespace MageHero
{
    /// Jefe final de la zona: un enemigo numérico gigante y oscuro (Nv 5) que espera en su arena al final de
    /// BossPath. Despierta cuando el mago se acerca; no se aleja de la arena. Al morir suelta un Tomo de curación.
    [RequireComponent(typeof(HealthSystem))]
    public class FinalBoss : MonoBehaviour
    {
        public string bossName = "Jefe final";
        public float maxHealth = 480f, damage = 24f, attackCooldown = 1.5f, attackRange = 3.2f, moveSpeed = 2.1f;
        public float wakeRadius = 15f, scale = 2.3f;

        public bool IsAwake { get; private set; }

        EnemyAI3D ai;
        HealthSystem health;
        Transform player;

        /// Crea el jefe a partir de un enemigo numérico de la escena (o de una cápsula si no hay ninguno).
        public static FinalBoss Spawn(Vector3 position, string name)
        {
            GameObject go;
            var template = FindAnyObjectByType<NumberEnemy>();
            if (template != null)
            {
                go = Instantiate(template.gameObject, position, Quaternion.identity);
                foreach (var n in new[] { "HealthBar", "LevelLabel" }) { var c = go.transform.Find(n); if (c != null) Destroy(c.gameObject); }
                var oldLevel = go.GetComponent<EnemyLevel>(); if (oldLevel != null) DestroyImmediate(oldLevel);
                var ne = go.GetComponent<NumberEnemy>(); if (ne != null) Destroy(ne);      // que no lo copie el spawner ni el mapa
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.position = position + Vector3.up;
                go.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Toon(new Color(0.2f, 0.12f, 0.25f));
            }
            go.name = "JefeFinal";
            var boss = go.AddComponent<FinalBoss>();
            boss.bossName = name;
            go.transform.localScale = Vector3.one * boss.scale;
            var ai = EnemyAI3D.MakeCombatant(go, boss.maxHealth);
            go.GetComponent<HealthSystem>().SetMaxHealth(boss.maxHealth);
            var el = go.AddComponent<EnemyLevel>();
            el.isBoss = true; el.displayName = name.ToUpperInvariant();
            el.SetLevel(LevelStats.MaxLevel, refill: false);
            return boss;
        }

        void Start()
        {
            health = GetComponent<HealthSystem>();
            ai = GetComponent<EnemyAI3D>();
            var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform;
            if (ai != null)
            {
                ai.attackDamage = damage; ai.attackCooldown = attackCooldown; ai.attackRange = attackRange;
                ai.moveSpeed = moveSpeed; ai.detectionRange = wakeRadius; ai.closeSenseRange = wakeRadius;
                ai.loseRange = wakeRadius + 8f; ai.leashRange = 12f; ai.requireLineOfSight = false;
                ai.enabled = false;                                   // dormido hasta que llegue el mago
            }
            var l = new GameObject("AuraRoja").AddComponent<Light>();
            l.transform.SetParent(transform, false); l.transform.localPosition = Vector3.up * 1.2f;
            l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.2f); l.range = 7f; l.intensity = 2.5f; l.shadows = LightShadows.None;
            health.Died += OnDied;
        }

        void Update()
        {
            if (IsAwake || player == null || ai == null) return;
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.magnitude > wakeRadius) return;
            IsAwake = true;
            ai.enabled = true;
            CameraShake3D.Shake(0.3f, 0.7f);
            UIManager.Instance?.ShowMessage($"¡{bossName} despierta!  Derrótalo para liberar la zona", 3.5f);
        }

        void OnDied(HealthSystem _)
        {
            UIManager.Instance?.ShowMessage($"¡Derrotaste a {bossName}!  La zona está a salvo.", 6f);
            UIManager.Instance?.SetObjective("");
        }
    }
}
