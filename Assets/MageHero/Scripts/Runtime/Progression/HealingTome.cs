using UnityEngine;

namespace MageHero
{
    /// Tomo de curación que sueltan los enemigos (ProgressionManager decide la probabilidad).
    /// Flota y gira con un brillo verde; al tocarlo, el mago recupera parte de su vida.
    /// Desaparece a los 30 s (parpadea los últimos 5).
    [RequireComponent(typeof(SphereCollider))]
    public class HealingTome : MonoBehaviour
    {
        [Range(0.1f, 1f)] public float healFraction = 0.5f;
        public float lifetime = 30f;

        Transform visual;
        float born;
        bool taken;

        /// Crea un tomo en el suelo junto a 'position'.
        public static HealingTome Spawn(Vector3 position)
        {
            Vector3 p = position;
            if (RuntimeMaterials.Ground(position, out var g)) p = g;
            var go = new GameObject("TomoDeCuracion", typeof(SphereCollider));
            go.transform.position = p;
            go.layer = 2;                                     // Ignore Raycast: no intercepta el clic de apuntado
            return go.AddComponent<HealingTome>();
        }

        void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 1.3f; col.center = Vector3.up * 0.8f;
            born = Time.time;
            BuildVisual();
        }

        void BuildVisual()
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.localPosition = Vector3.up * 0.9f;
            visual.localScale = Vector3.zero;
            var cover = RuntimeMaterials.Toon(new Color(0.2f, 0.62f, 0.32f));
            var pages = RuntimeMaterials.Toon(new Color(0.98f, 0.94f, 0.82f));
            var glow = RuntimeMaterials.Glow(new Color(0.65f, 1f, 0.6f));
            RuntimeMaterials.Prim(PrimitiveType.Cube, "Cover", visual, Vector3.zero, new Vector3(0.62f, 0.16f, 0.8f), cover);
            RuntimeMaterials.Prim(PrimitiveType.Cube, "Pages", visual, new Vector3(0.03f, 0f, 0f), new Vector3(0.58f, 0.12f, 0.74f), pages);
            // Cruz de curación brillante sobre la tapa
            RuntimeMaterials.Prim(PrimitiveType.Cube, "CrossV", visual, new Vector3(0f, 0.09f, 0f), new Vector3(0.1f, 0.02f, 0.42f), glow, false);
            RuntimeMaterials.Prim(PrimitiveType.Cube, "CrossH", visual, new Vector3(0f, 0.09f, 0f), new Vector3(0.36f, 0.02f, 0.1f), glow, false);
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(visual, false);
            l.type = LightType.Point; l.color = new Color(0.5f, 1f, 0.55f); l.range = 4f; l.intensity = 2.5f;
        }

        void Update()
        {
            if (taken) return;
            float age = Time.time - born;
            float pop = Mathf.Clamp01(age / 0.35f);
            visual.localScale = Vector3.one * (pop < 1f ? Mathf.Sin(pop * Mathf.PI * 0.75f) * 1.25f : 1f);
            visual.localPosition = Vector3.up * (0.9f + Mathf.Sin(Time.time * 2.4f) * 0.15f);
            visual.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
            if (age > lifetime - 5f) visual.gameObject.SetActive(Mathf.Repeat(age * 6f, 1f) > 0.3f);
            if (age > lifetime) Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken || !other.CompareTag("Player")) return;
            var hs = other.GetComponentInParent<HealthSystem>();
            if (hs == null || hs.team != Team.Player || hs.IsDead) return;
            taken = true;
            float before = hs.Current;
            hs.Heal(hs.maxHealth * healFraction);
            float gained = hs.Current - before;
            UIManager.Instance?.ShowMessage(gained > 0.5f ? $"Tomo de curación: +{gained:0} de vida" : "Tomo de curación: ya tenías la vida al máximo", 2.2f);
            Destroy(gameObject);
        }
    }
}
