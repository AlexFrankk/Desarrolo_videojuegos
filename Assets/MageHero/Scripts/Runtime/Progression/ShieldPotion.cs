using UnityEngine;

namespace MageHero
{
    /// Poción de escudo: al tocarla, el mago recibe un escudo mágico que absorbe daño durante un tiempo
    /// (ver HealthSystem.AddShield y ShieldBubble). La sueltan algunos enemigos y hay varias repartidas por el mapa.
    [RequireComponent(typeof(SphereCollider))]
    public class ShieldPotion : MonoBehaviour
    {
        public float shieldAmount = 60f;
        public float duration = 25f;
        [Tooltip("0 = no desaparece (pociones colocadas en el mapa).")]
        public float lifetime = 0f;

        Transform visual;
        float born;
        bool taken;

        public static ShieldPotion Spawn(Vector3 position, float lifetime)
        {
            Vector3 p = position;
            if (RuntimeMaterials.Ground(position, out var g)) p = g;
            var go = new GameObject("PocionDeEscudo", typeof(SphereCollider));
            go.layer = 2;
            go.transform.position = p;
            var sp = go.AddComponent<ShieldPotion>();
            sp.lifetime = lifetime;
            return sp;
        }

        void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 1.3f; col.center = Vector3.up * 0.8f;
            born = Time.time;
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.localPosition = Vector3.up * 0.9f;
            var glass = RuntimeMaterials.Glow(new Color(0.35f, 0.7f, 1f));
            var cork = RuntimeMaterials.Toon(new Color(0.55f, 0.38f, 0.22f));
            var shine = RuntimeMaterials.Glow(new Color(0.85f, 0.95f, 1f));
            RuntimeMaterials.Prim(PrimitiveType.Sphere, "Frasco", visual, Vector3.zero, new Vector3(0.5f, 0.55f, 0.5f), glass, false);
            RuntimeMaterials.Prim(PrimitiveType.Cylinder, "Cuello", visual, new Vector3(0f, 0.35f, 0f), new Vector3(0.18f, 0.12f, 0.18f), glass, false);
            RuntimeMaterials.Prim(PrimitiveType.Cylinder, "Corcho", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.06f, 0.2f), cork);
            RuntimeMaterials.Prim(PrimitiveType.Sphere, "Brillo", visual, new Vector3(-0.1f, 0.1f, -0.18f), Vector3.one * 0.12f, shine, false);
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(visual, false);
            l.type = LightType.Point; l.color = new Color(0.4f, 0.7f, 1f); l.range = 4f; l.intensity = 2.5f; l.shadows = LightShadows.None;
        }

        void Update()
        {
            if (taken) return;
            float age = Time.time - born;
            visual.localPosition = Vector3.up * (0.9f + Mathf.Sin(Time.time * 2.6f) * 0.15f);
            visual.Rotate(0f, 80f * Time.deltaTime, 0f, Space.World);
            if (lifetime > 0f)
            {
                if (age > lifetime - 5f) visual.gameObject.SetActive(Mathf.Repeat(age * 6f, 1f) > 0.3f);
                if (age > lifetime) Destroy(gameObject);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken || !other.CompareTag("Player")) return;
            var hs = other.GetComponentInParent<HealthSystem>();
            if (hs == null || hs.team != Team.Player || hs.IsDead) return;
            taken = true;
            hs.AddShield(shieldAmount, duration);
            var bubble = hs.GetComponent<ShieldBubble>();
            if (bubble == null) hs.gameObject.AddComponent<ShieldBubble>();
            HitFX.Spark(transform.position + Vector3.up, new Color(0.4f, 0.75f, 1f), 1.2f);
            UIManager.Instance?.ShowMessage($"¡Escudo mágico! Absorbe {shieldAmount:0} de daño durante {duration:0} s", 2.5f);
            Destroy(gameObject);
        }
    }
}
