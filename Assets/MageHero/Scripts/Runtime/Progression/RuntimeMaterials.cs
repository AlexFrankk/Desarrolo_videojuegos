using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Materiales creados en tiempo de juego (sin assets) con los mismos shaders del proyecto:
    /// "MageHero/ToonLit" para objetos sólidos y URP Unlit para brillos. Se cachean por color.
    public static class RuntimeMaterials
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Material Toon(Color c) => Get("toon", c, "MageHero/ToonLit", "Universal Render Pipeline/Lit");
        public static Material Glow(Color c) => Get("glow", c, "Universal Render Pipeline/Unlit", "Unlit/Color");

        /// Unlit transparente (alfa del color = opacidad). Para burbujas de escudo y marcas en el suelo.
        public static Material GlowTransparent(Color c)
        {
            string key = "glowT" + ColorUtility.ToHtmlStringRGBA(c);
            if (cache.TryGetValue(key, out var m) && m != null) return new Material(m);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh == null) return null;
            m = new Material(sh) { name = "glowT_" + ColorUtility.ToHtmlStringRGB(c) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
            }
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            cache[key] = m;
            return new Material(m);                                   // copia: cada uso puede animar su opacidad
        }

        static Material Get(string kind, Color c, string shader, string fallback)
        {
            string key = kind + ColorUtility.ToHtmlStringRGBA(c);
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            var sh = Shader.Find(shader) ?? Shader.Find(fallback);
            if (sh == null) return null;
            m = new Material(sh) { name = $"{kind}_{ColorUtility.ToHtmlStringRGB(c)}" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_ShadeColor")) m.SetColor("_ShadeColor", c * 0.6f);
            if (m.HasProperty("_UseVertexColor")) m.SetFloat("_UseVertexColor", 0f);
            cache[key] = m;
            return m;
        }

        /// Primitiva sin collider con un material (útil para decorados construidos por código).
        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// Altura del suelo (capa Environment) bajo un punto; false si no hay suelo caminable.
        public static bool Ground(Vector3 p, out Vector3 hitPoint)
        {
            int ground = GameLayers.Exists(GameLayers.Environment) ? GameLayers.Mask(GameLayers.Environment) : Physics.DefaultRaycastLayers;
            int block = 0;
            if (GameLayers.Exists(GameLayers.Water)) block |= GameLayers.Mask(GameLayers.Water);
            if (GameLayers.Exists(GameLayers.Obstacles)) block |= GameLayers.Mask(GameLayers.Obstacles);
            hitPoint = p;
            if (!Physics.Raycast(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, out RaycastHit hit, 200f, ground | block, QueryTriggerInteraction.Collide))
                return false;
            hitPoint = hit.point;
            return ((1 << hit.collider.gameObject.layer) & ground) != 0;
        }
    }
}
