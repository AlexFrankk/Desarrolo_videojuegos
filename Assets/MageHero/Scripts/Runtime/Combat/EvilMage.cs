using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// "Mago oscuro": enemigo a distancia con el mismo modelo del protagonista pero teñido de negro y violeta,
    /// ojos y cristal rojos y un aura. Lanza orbes oscuros (EnemyCaster3D) y mantiene la distancia.
    /// ProgressionManager los coloca en zonas alejadas del mago y añade más cuando sube la amenaza.
    public class EvilMage : MonoBehaviour
    {
        public const string DisplayName = "MAGO OSCURO";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static readonly List<EvilMage> All = new List<EvilMage>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// Crea un mago oscuro en 'position' (se apoya en el suelo). Devuelve null si no hay modelo del mago.
        public static EvilMage Spawn(Vector3 position, int level)
        {
            var player = GameObject.FindWithTag("Player");
            Transform visuals = player != null ? FindDeep(player.transform, "Visuals") : null;

            var root = new GameObject("MagoOscuro");
            root.SetActive(false);                                  // se configura completo antes de que corran Awake/Start
            if (RuntimeMaterials.Ground(position, out var g)) position = g;
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            if (visuals != null)
            {
                var model = Instantiate(visuals.gameObject, root.transform);
                model.name = "Visuals";
                model.transform.localPosition = visuals.localPosition;
                model.transform.localRotation = visuals.localRotation;
                model.transform.localScale = visuals.localScale * 1.12f;
                // Se borran al final del frame; el mago se activa en el siguiente (ver abajo), así los scripts del
                // jugador copiados nunca llegan a ejecutarse. (DestroyImmediate no se permite dentro de eventos de física.)
                foreach (var cv in model.GetComponentsInChildren<Canvas>(true)) Destroy(cv.gameObject);   // runas del báculo
                foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var l in model.GetComponentsInChildren<Light>(true)) Destroy(l);
                Tint(model);
            }
            else
            {
                RuntimeMaterials.Prim(PrimitiveType.Capsule, "Cuerpo", root.transform, Vector3.up, new Vector3(0.8f, 1f, 0.8f),
                                      RuntimeMaterials.Toon(new Color(0.12f, 0.06f, 0.16f)));
            }

            var aura = new GameObject("Aura").AddComponent<Light>();
            aura.transform.SetParent(root.transform, false); aura.transform.localPosition = Vector3.up * 1.3f;
            aura.type = LightType.Point; aura.color = new Color(0.7f, 0.15f, 1f); aura.range = 4f; aura.intensity = 2f; aura.shadows = LightShadows.None;

            var col = root.AddComponent<CapsuleCollider>(); col.radius = 0.45f; col.height = 1.9f; col.center = Vector3.up * 0.95f;
            var hs = root.AddComponent<HealthSystem>(); hs.team = Team.Enemy; hs.destroyOnDeath = true;
            var ai = root.AddComponent<EnemyAI3D>();
            var caster = root.AddComponent<EnemyCaster3D>();
            caster.orbColor = new Color(0.75f, 0.2f, 1f);
            var crystal = FindDeep(root.transform, "Crystal");
            if (crystal != null) caster.castPoint = crystal;
            var el = root.AddComponent<EnemyLevel>();
            el.displayName = DisplayName; el.showName = true;
            var mage = root.AddComponent<EvilMage>();
            if (GameLayers.Exists(GameLayers.Enemies)) GameLayers.Set(root, GameLayers.Enemies);

            DelayedCall.Run(0.02f, () =>
            {
                if (root == null) return;
                root.SetActive(true);
                el.SetLevel(level, refill: true);                   // también ajusta el daño y la cadencia del orbe
                HitFX.Spark(root.transform.position + Vector3.up, new Color(0.7f, 0.25f, 1f), 1.2f);
            });
            return mage;
        }

        static void Tint(GameObject model)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("eye") || n.Contains("crystal") || n.Contains("orb") || n.Contains("star") || n.Contains("rune"))
                {
                    r.sharedMaterial = RuntimeMaterials.Glow(new Color(1f, 0.15f, 0.2f));           // ojos y cristal rojos
                    continue;
                }
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty(BaseColorId)) continue;
                // Túnica, sombrero y bastón casi negros con un matiz violeta (el color de vértice aporta el detalle).
                Color dark = new Color(0.22f, 0.08f, 0.3f);
                r.GetPropertyBlock(mpb); mpb.SetColor(BaseColorId, dark); r.SetPropertyBlock(mpb);
            }
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
