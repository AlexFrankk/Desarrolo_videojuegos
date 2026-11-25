using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Vida del bosque para la escena 3 (el presagio): una bandada de aves que huye en pánico, un zorro y un ciervo
    /// que pasan corriendo, y las hojas otoñales que caen (y se congelan en el aire cuando llega el silencio).
    public static class CineCritters
    {
        static Material M(int hex) => RuntimeMaterials.Toon(MenuStyle.Hex(hex));

        // ------------------------------------------------------------------ hojas
        /// Hojas que caen alrededor de un punto. Devuelve el sistema (para congelarlo con simulationSpeed = 0).
        public static ParticleSystem Leaves(Vector3 center, float radius)
        {
            var go = new GameObject("HojasCayendo");
            go.transform.position = center + Vector3.up * 7f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.prewarm = true;
            main.startLifetime = 9f; main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = 0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var g = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.12f), new Color(0.95f, 0.25f, 0.1f)) { mode = ParticleSystemGradientMode.TwoColors };
            main.startColor = g;
            var em = ps.emission; em.rateOverTime = 14f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(radius * 2f, 1f, radius * 2f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.35f; noise.scrollSpeed = 0.2f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = LeafMesh();
            var mat = IntroSet.VertexToon();
            r.sharedMaterial = RuntimeMaterials.Toon(Color.white);
            // el color de la partícula llega como color de vértice: usar el toon con color de vértice
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static Mesh leaf;
        static Mesh LeafMesh()
        {
            if (leaf != null) return leaf;
            var lp = new LowPoly().Paint(Color.white);
            Vector3 a = new Vector3(0, 0, -0.5f), b = new Vector3(0.3f, 0, 0), c = new Vector3(0, 0, 0.5f), d = new Vector3(-0.3f, 0, 0);
            lp.Tri(a, b, c); lp.Tri(a, c, d); lp.Tri(a, c, b); lp.Tri(a, d, c);
            leaf = lp.Build("Hoja");
            return leaf;
        }

        // ------------------------------------------------------------------ aves
        /// Bandada que sale de las copas (alrededor de <paramref name="from"/>) y huye en la dirección dada.
        public static void Flock(MonoBehaviour host, Vector3 from, Vector3 dir, int count, float spread = 6f)
        {
            var root = new GameObject("Bandada").transform;
            var rng = new System.Random(9);
            for (int i = 0; i < count; i++)
            {
                var bird = Bird(root);
                Vector3 p = from + new Vector3(((float)rng.NextDouble() - .5f) * spread, (float)rng.NextDouble() * 3f, ((float)rng.NextDouble() - .5f) * spread);
                bird.position = p;
                host.StartCoroutine(FlyAway(bird, dir, 7f + (float)rng.NextDouble() * 4f, (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() * 10f));
            }
            Object.Destroy(root.gameObject, 9f);
        }

        static Transform Bird(Transform parent)
        {
            var t = new GameObject("Ave").transform;
            t.SetParent(parent, false);
            var body = new LowPoly().Paint(Color.white);
            body.At(Vector3.zero, Quaternion.Euler(90, 0, 0), new Vector3(0.6f, 1f, 0.6f)).Prism(4, 0.06f, 0.0f, 0.32f);
            body.At(new Vector3(0, 0, -0.05f), Quaternion.Euler(-90, 0, 0), new Vector3(0.6f, 1f, 0.6f)).Prism(4, 0.06f, 0.0f, 0.16f);
            body.Spawn("Cuerpo", t, M(0x3b2f2a), default, false);
            foreach (float s in new[] { -1f, 1f })
            {
                var w = LowPoly.Pivot(s < 0 ? "Ala_L" : "Ala_R", t, Vector3.zero);
                var wm = new LowPoly().Paint(Color.white);
                wm.Tri(new Vector3(0, 0, 0.08f), new Vector3(s * 0.32f, 0, -0.02f), new Vector3(0, 0, -0.1f));
                wm.Tri(new Vector3(0, 0, 0.08f), new Vector3(0, 0, -0.1f), new Vector3(s * 0.32f, 0, -0.02f));
                wm.Spawn("Pluma", w, M(0x4a3b33), default, false);
            }
            return t;
        }

        static IEnumerator FlyAway(Transform bird, Vector3 dir, float speed, float delay, float phase)
        {
            yield return new WaitForSeconds(delay);
            var wl = bird.Find("Ala_L"); var wr = bird.Find("Ala_R");
            Vector3 vel = (dir.normalized + Vector3.up * 0.9f).normalized * speed * 0.5f;
            float t = 0;
            while (bird != null && t < 8f)
            {
                t += Time.deltaTime;
                // primero suben en vertical (espantadas) y luego se alinean con la huida
                Vector3 want = (dir.normalized + Vector3.up * Mathf.Lerp(0.9f, 0.15f, t / 1.5f)).normalized * speed;
                vel = Vector3.Lerp(vel, want, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                bird.position += vel * Time.deltaTime + Vector3.up * Mathf.Sin(t * 9f + phase) * 0.01f;
                bird.rotation = Quaternion.LookRotation(vel);
                float flap = Mathf.Sin(t * 22f + phase) * 55f;
                wl.localRotation = Quaternion.Euler(0, 0, flap); wr.localRotation = Quaternion.Euler(0, 0, -flap);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ zorro y ciervo
        public static Transform Fox()
        {
            var t = new GameObject("Zorro").transform;
            var lp = new LowPoly().Paint(MenuStyle.Hex(0xe0782a));
            lp.At(new Vector3(0, 0.35f, 0)).Box(new Vector3(0.28f, 0.26f, 0.7f));                          // cuerpo
            lp.At(new Vector3(0, 0.48f, 0.42f), Quaternion.Euler(80, 0, 0), Vector3.one).Prism(4, 0.13f, 0.03f, 0.32f);   // cabeza/hocico
            lp.At(new Vector3(-0.07f, 0.62f, 0.4f)).Prism(3, 0.04f, 0f, 0.13f);                          // orejas
            lp.At(new Vector3(0.07f, 0.62f, 0.4f)).Prism(3, 0.04f, 0f, 0.13f);
            lp.At(new Vector3(0, 0.42f, -0.35f), Quaternion.Euler(-115, 0, 0), Vector3.one).Prism(5, 0.1f, 0.02f, 0.55f);    // cola
            lp.Paint(MenuStyle.Hex(0xfff1e0));
            lp.At(new Vector3(0, 0.36f, -0.83f), Quaternion.Euler(-115, 0, 0), Vector3.one).Prism(5, 0.04f, 0f, 0.1f);       // punta blanca
            AddLegs(lp, t, MenuStyle.Hex(0x3a2418), 0.12f, 0.27f, 0.25f, 0.035f);
            return t;
        }

        public static Transform Deer()
        {
            var t = new GameObject("Ciervo").transform;
            var lp = new LowPoly().Paint(MenuStyle.Hex(0xa86d3e));
            lp.At(new Vector3(0, 0.95f, 0)).Box(new Vector3(0.42f, 0.42f, 1.05f));
            lp.At(new Vector3(0, 1.2f, 0.48f), Quaternion.Euler(30, 0, 0), Vector3.one).Prism(5, 0.12f, 0.1f, 0.45f);        // cuello
            lp.At(new Vector3(0, 1.58f, 0.66f), Quaternion.Euler(95, 0, 0), Vector3.one).Prism(5, 0.12f, 0.05f, 0.34f);      // cabeza
            lp.Paint(MenuStyle.Hex(0xe8d6b8));
            lp.At(new Vector3(0, 1.05f, -0.55f)).Box(new Vector3(0.14f, 0.16f, 0.06f));                                       // cola blanca
            lp.Paint(MenuStyle.Hex(0x6b4a2c));
            foreach (float s in new[] { -1f, 1f })
            {
                lp.At(new Vector3(s * 0.06f, 1.72f, 0.6f), Quaternion.Euler(-15, 0, s * 25), Vector3.one).Prism(3, 0.02f, 0.012f, 0.35f);
                lp.At(new Vector3(s * 0.14f, 1.92f, 0.56f), Quaternion.Euler(-40, 0, s * 50), Vector3.one).Prism(3, 0.015f, 0.008f, 0.18f);
            }
            AddLegs(lp, t, MenuStyle.Hex(0x5a3a22), 0.16f, 0.4f, 0.76f, 0.045f);
            return t;
        }

        static void AddLegs(LowPoly bodyMesh, Transform t, Color legColor, float halfW, float halfL, float legLen, float r)
        {
            var go = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(t, false);
            go.GetComponent<MeshFilter>().sharedMesh = bodyMesh.Build(t.name);
            go.GetComponent<MeshRenderer>().sharedMaterial = IntroSet.VertexToon();
            int i = 0;
            foreach (var p in new[] { new Vector3(-halfW, 0, halfL), new Vector3(halfW, 0, halfL), new Vector3(-halfW, 0, -halfL), new Vector3(halfW, 0, -halfL) })
            {
                var leg = LowPoly.Pivot("Pata" + (i++), t, p + Vector3.up * legLen);
                var lm = new LowPoly().Paint(legColor);
                lm.At(new Vector3(0, -legLen, 0)).Prism(4, r * 0.7f, r, legLen);
                lm.Spawn("PataMesh", leg, RuntimeMaterials.Toon(legColor));
            }
        }

        /// Hace correr a un animal en línea recta pegado al suelo, con galope de patas y cabeceo.
        public static IEnumerator Run(Transform animal, Vector3 from, Vector3 to, float speed, float gallop)
        {
            animal.position = from;
            Vector3 dir = to - from; dir.y = 0; dir.Normalize();
            animal.rotation = Quaternion.LookRotation(dir);
            var legs = new List<Transform>();
            foreach (Transform c in animal) if (c.name.StartsWith("Pata")) legs.Add(c);
            float t = 0, dist = Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z)), travelled = 0;
            while (travelled < dist && animal != null)
            {
                t += Time.deltaTime;
                float step = speed * Time.deltaTime;
                travelled += step;
                Vector3 p = animal.position + dir * step;
                p.y = RuntimeMaterials.Ground(p, out Vector3 hit) ? hit.y : p.y;
                p.y += Mathf.Abs(Mathf.Sin(t * gallop)) * 0.12f;
                animal.position = p;
                animal.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Mathf.Sin(t * gallop * 2f) * 4f, 0, 0);
                for (int i = 0; i < legs.Count; i++)
                    legs[i].localRotation = Quaternion.Euler(Mathf.Sin(t * gallop * 2f + (i < 2 ? 0f : 1.6f)) * 40f, 0, 0);
                yield return null;
            }
            if (animal != null) Object.Destroy(animal.gameObject);
        }
    }
}
