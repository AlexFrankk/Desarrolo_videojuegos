using UnityEngine;
using UnityEngine.Rendering;

namespace MageHero
{
    /// Efectos de impacto generados por código (sin prefabs):
    ///  - Spark: chispas + onda de choque + destello. Para los hechizos que chocan contra algo.
    ///  - Explosion: bola de fuego + chispas + humo + destello. Cuando un enemigo golpea al mago o muere.
    /// Cada efecto se destruye solo al terminar.
    public static class HitFX
    {
        static Material additive, alpha;
        static Texture2D soft;

        // ------------------------------------------------------------------ API
        public static void Spark(Vector3 pos, Color color, float scale = 1f)
        {
            var root = NewRoot("FX_Chispas", pos, 1.5f);
            Core(root, color, 1.1f * scale, 0.18f);
            Burst(root, "Chispas", Additive(), count: Mathf.RoundToInt(34 * scale), life: new Vector2(0.3f, 0.6f),
                  speed: new Vector2(4f, 10f) * scale, size: new Vector2(0.18f, 0.38f) * scale,
                  start: Color.white, end: color, gravity: 0.6f, radius: 0.15f, stretch: true);
            Burst(root, "Brillo", Additive(), count: Mathf.RoundToInt(10 * scale), life: new Vector2(0.25f, 0.45f),
                  speed: new Vector2(0.5f, 2f) * scale, size: new Vector2(0.5f, 0.9f) * scale,
                  start: color, end: color, gravity: 0f, radius: 0.2f, stretch: false);
            Shockwave(root, color, 3f * scale, 0.35f);
            Flash(root, color, 6f * scale, 0.18f);
        }

        public static void Explosion(Vector3 pos, float scale = 1f, Color? tint = null)
        {
            Color core = tint ?? new Color(1f, 0.55f, 0.15f);
            var root = NewRoot("FX_Explosion", pos, 2f);
            Core(root, core, 1.8f * scale, 0.25f);
            Burst(root, "Fuego", Additive(), count: Mathf.RoundToInt(26 * scale), life: new Vector2(0.25f, 0.55f),
                  speed: new Vector2(1.5f, 5f) * scale, size: new Vector2(0.55f, 1.1f) * scale,
                  start: new Color(1f, 0.95f, 0.6f), end: new Color(core.r, core.g * 0.4f, core.b * 0.3f, 0f), gravity: -0.4f, radius: 0.25f * scale, stretch: false);
            Burst(root, "Chispas", Additive(), count: Mathf.RoundToInt(18 * scale), life: new Vector2(0.2f, 0.45f),
                  speed: new Vector2(5f, 11f) * scale, size: new Vector2(0.14f, 0.3f) * scale,
                  start: Color.white, end: core, gravity: 1.2f, radius: 0.1f, stretch: true);
            Burst(root, "Humo", Alpha(), count: Mathf.RoundToInt(8 * scale), life: new Vector2(0.6f, 1.1f),
                  speed: new Vector2(0.5f, 1.6f) * scale, size: new Vector2(0.6f, 1.1f) * scale,
                  start: new Color(0.25f, 0.22f, 0.22f, 0.55f), end: new Color(0.25f, 0.22f, 0.22f, 0f), gravity: -0.25f, radius: 0.3f * scale, stretch: false);
            Shockwave(root, core, 3.8f * scale, 0.4f);
            Flash(root, core, 7f * scale, 0.2f);
        }

        // ------------------------------------------------------------------ piezas
        static Transform NewRoot(string name, Vector3 pos, float life)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            Object.Destroy(go, life);
            return go.transform;
        }

        static void Burst(Transform root, string name, Material mat, int count, Vector2 life, Vector2 speed, Vector2 size,
                          Color start, Color end, float gravity, float radius, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = start;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count + 4;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = Mathf.Max(0.01f, radius);

            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                      new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a * 0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, stretch ? AnimationCurve.Linear(0f, 1f, 1f, 0.2f) : AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f; r.lengthScale = 2.5f; }
            ps.Play();
        }

        static void Shockwave(Transform root, Color c, float size, float life)
        {
            var go = new GameObject("Onda");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.1f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = life; main.startSpeed = 0f; main.startSize = size;
            main.startColor = new Color(c.r, c.g, c.b, 0.8f); main.maxParticles = 1;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            var sh = ps.shape; sh.enabled = false;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Additive(); r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        /// Esfera brillante que crece y se desvanece en el punto de impacto (se ve bien desde la cámara alta).
        static void Core(Transform root, Color c, float size, float life)
        {
            var go = RuntimeMaterials.Prim(PrimitiveType.Sphere, "Nucleo", root, Vector3.zero, Vector3.one * 0.2f,
                                           RuntimeMaterials.Glow(Color.Lerp(c, Color.white, 0.5f)), false);
            var g = go.AddComponent<FXGrowFade>();
            g.maxSize = size; g.life = life;
        }

        static void Flash(Transform root, Color c, float range, float life)
        {
            var l = new GameObject("Destello").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 3f; l.shadows = LightShadows.None;
            Object.Destroy(l, life);
        }

        // ------------------------------------------------------------------ materiales (URP Particles/Unlit)
        static Material Additive() => additive != null ? additive : (additive = MakeParticleMat(true));
        static Material Alpha() => alpha != null ? alpha : (alpha = MakeParticleMat(false));

        static Material MakeParticleMat(bool add)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = add ? "FX_Additive" : "FX_Alpha" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Soft());
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Soft());
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);                                 // transparente
                m.SetFloat("_Blend", add ? 2f : 0f);                        // 2 = aditivo, 0 = alpha
                m.SetFloat("_SrcBlend", (float)(add ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
                m.SetFloat("_DstBlend", (float)(add ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// Textura circular difuminada para que las partículas no sean cuadrados.
        static Texture2D Soft()
        {
            if (soft != null) return soft;
            const int n = 64;
            soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "FX_Soft", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            soft.SetPixels32(px); soft.Apply(false, true);
            return soft;
        }
    }

    /// Hace crecer un objeto hasta maxSize y lo encoge/destruye al terminar su vida (núcleo del impacto).
    public class FXGrowFade : MonoBehaviour
    {
        public float maxSize = 1f, life = 0.2f;
        float t;
        void Update()
        {
            t += Time.deltaTime / life;
            float k = t < 0.5f ? t / 0.5f : 1f - (t - 0.5f) / 0.5f;
            transform.localScale = Vector3.one * maxSize * Mathf.Clamp01(k) * (t < 0.5f ? 1f : 1f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
