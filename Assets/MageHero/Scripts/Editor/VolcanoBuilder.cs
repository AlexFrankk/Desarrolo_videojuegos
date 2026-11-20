#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MageHero.Editor
{
    /// Nivel aparte del jefe: un cráter volcánico pequeño (low poly) con cráteres de lava, conos piramidales, grietas
    /// incandescentes, un altar de obsidiana y el Elemental alienígena de obsidiana (Diseños/Enemigo). Se genera con
    /// Tools > Mage Hero > Rebuild > Nivel Volcán (jefe) y se guarda en Scenes/Level_volcan.unity.
    public static class VolcanoBuilder
    {
        public const string LevelId = "volcan";
        public static string ScenePath => AssetLib.Root + "/Scenes/Level_" + LevelId + ".unity";
        const string Dir = AssetLib.Gen + "/" + LevelId;
        const string BossPrefabPath = AssetLib.Root + "/Prefabs/Boss_Obsidiana.prefab";

        // Arena
        const float FloorR = 24f;                                  // radio del suelo jugable
        static readonly Vector3 Altar = new Vector3(0f, 0f, 12f);
        const float AltarR = 6f, AltarH = 0.6f;
        static readonly Vector3 Spawn = new Vector3(0f, 0f, -17f);
        static readonly Vector3[] Pools =                          // cráteres con lava: x, z, radio
        {
            new Vector3(-13f, 3f, 3.6f), new Vector3(14f, -3f, 3.2f), new Vector3(-8f, -13f, 2.4f),
            new Vector3(12.5f, 12f, 2.6f), new Vector3(-12f, 15f, 2.4f), new Vector3(9f, -14f, 1.8f)
        };
        static readonly float[] PoolRim = { 2.1f, 1.9f, 1.4f, 1.8f, 1.6f, 1.2f };   // altura del borde de cada cráter
        static readonly Vector4[] DryCraters =                     // cráteres apagados (humean): x, z, radio, borde
        {
            new Vector4(-18f, -9.5f, 2.0f, 1.2f), new Vector4(17.5f, 5f, 1.8f, 1.1f),
            new Vector4(4f, -7f, 1.2f, 0.6f), new Vector4(-5f, 5f, 1.1f, 0.55f)
        };
        static readonly Vector4[] Cones =                          // conos piramidales (hornitos): x, z, radio base, altura
        {
            new Vector4(-19.5f, -3f, 3.6f, 6.5f), new Vector4(20.5f, -9f, 3.4f, 5.8f), new Vector4(-15f, -16f, 2.8f, 4.6f),
            new Vector4(19.5f, 16.5f, 3.8f, 7.2f), new Vector4(-19f, 9.5f, 3.2f, 5.6f), new Vector4(10f, -21f, 2.6f, 4.2f)
        };
        const float LavaY = -0.35f;

        // Paleta (ficha técnica: obsidiana negra, núcleo naranja, resplandor amarillo, runas rojas)
        const int Obs = 0x1c1a21, Obs2 = 0x26222c, Obs3 = 0x312b36;
        const int BObs = 0x2b2838, BObs2 = 0x37334a, BObs3 = 0x4a4462;    // obsidiana del jefe: brillo frío violeta para que destaque
        const int Fire = 0xff5a14, Hot = 0xffd23f, Rune = 0xff3b1a;

        static List<List<Vector2>> cracks = new List<List<Vector2>>();

        [MenuItem("Tools/Mage Hero/Rebuild/Nivel Volcán (jefe)")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorUtility.DisplayDialog("Nivel Volcán", "Escena generada: " + ScenePath + "\n\nPulsa Play para enfrentar al Elemental de Obsidiana.", "OK");
        }

        public static void Build()
        {
            PhysicsSetup.SetupLayersAndMatrix();
            AssetLib.EnsureFolder(Dir);
            AssetLib.EnsureFolder(AssetLib.Root + "/Prefabs");
            AssetLib.EnsureFolder(AssetLib.Root + "/Scenes");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MageBuilder.PrefabPath) == null) MageBuilder.BuildPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            GenerateCracks();

            var world = new GameObject("World").transform;
            BuildTerrain(world);
            BuildLava(world);
            BuildCracks(world);
            BuildCones(world);
            BuildDryCraters(world);
            BuildAltar(world);
            BuildShards(world);
            BuildVolcano(world);
            BuildParticles(world);
            BuildBounds(world);

            var bossPrefab = BuildBossPrefab();
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
            boss.transform.position = new Vector3(Altar.x, AltarH + 0.25f, Altar.z);
            boss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);        // mira hacia la entrada (sur)

            var player = BuildPlayerAndCamera();
            BuildHud(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene();
            MenuThumbnailCapture.CaptureOpenScene(LevelId);             // imagen de la tarjeta en "Elige un escenario"
            AssetDatabase.SaveAssets();
            Debug.Log("[MageHero] Nivel volcánico generado: " + ScenePath);
        }

        // =============================================================== ILUMINACIÓN Y POSTPROCESO
        static void SetupLighting()
        {
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.23f, 0.08f, 0.06f);
            RenderSettings.fogStartDistance = 50f; RenderSettings.fogEndDistance = 240f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.36f, 0.34f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.intensity = 0.95f;
            sun.color = new Color(1f, 0.62f, 0.42f);
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.35f, -1f, 0.55f));
        }

        /// Bloom: hace que la lava, las grietas y sobre todo el anillo rúnico del jefe brillen (colores HDR).
        static void BuildPostFx(Camera cam)
        {
            string path = Dir + "/VolcanPostFx.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.85f, 0.8f));
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.28f); vig.color.Override(new Color(0.12f, 0.02f, 0.02f)); vig.smoothness.Override(0.5f);
            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);

            var go = new GameObject("PostProceso");
            var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 1f; vol.sharedProfile = profile;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
        }

        // =============================================================== TERRENO
        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static float Bump(float d, float c, float w) { float k = (d - c) / w; return Mathf.Exp(-k * k); }
        static float Hash(float k) { float v = Mathf.Sin(k * 12.9898f) * 43758.5453f; return v - Mathf.Floor(v); }

        /// Altura del cráter en (x, z) (coordenadas de Unity): suelo ondulado, cráteres con borde levantado,
        /// paredes altas y empinadas con picos piramidales (más bajas al norte para ver el volcán detrás del jefe).
        static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z), ang = Mathf.Atan2(z, x);
            float n = Mathf.PerlinNoise(x * 0.13f + 100f, z * 0.13f + 100f);
            float h = (n - 0.5f) * 0.4f + (Mathf.PerlinNoise(x * 0.045f + 7f, z * 0.045f + 3f) - 0.5f) * 0.7f;
            h *= Smooth(3f, 6f, Vector2.Distance(new Vector2(x, z), new Vector2(Spawn.x, Spawn.z)));   // entrada llana

            float da = Vector2.Distance(new Vector2(x, z), new Vector2(Altar.x, Altar.z));
            h = Mathf.Max(h, AltarH * Smooth(AltarR, AltarR - 1.2f, da));

            for (int i = 0; i < Pools.Length; i++)                                 // cráteres con lava
            {
                var p = Pools[i];
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.y));
                h += PoolRim[i] * Bump(d, p.z + 1.7f, 1.6f);
                h = Mathf.Lerp(h, -0.9f, Smooth(p.z + 1.2f, p.z - 0.4f, d));
            }
            foreach (var c in DryCraters)                                          // cráteres apagados
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.y));
                h += c.w * Bump(d, c.z + 0.6f, 0.8f);
                h -= 0.65f * Smooth(c.z + 0.3f, c.z - 0.7f, d);
            }

            // Paredes del cráter: altas, empinadas e irregulares
            float edge = FloorR + 1.5f * Mathf.Sin(ang * 3f) + 1.2f * Mathf.Sin(ang * 7f + 1f);
            float t = Smooth(edge, edge + 7f, r);
            float north = Mathf.Lerp(1f, 0.5f, Smooth(0.2f, 0.9f, z / Mathf.Max(1f, r)) * Smooth(edge, edge + 4f, r));
            float m = Mathf.PerlinNoise(x * 0.07f + 30f, z * 0.07f + 30f);
            float jag = Mathf.PerlinNoise(x * 0.25f, z * 0.25f);
            float wall = t * (15f + 8f * m + 4f * jag);
            for (int k = 0; k < 11; k++)                                           // picos piramidales en el borde
            {
                float pa = k * Mathf.PI * 2f / 11f + 0.4f * Mathf.Sin(k * 2.3f);
                float pr = FloorR + 9f + 3f * Mathf.Sin(k * 1.7f);
                float dd = Vector2.Distance(new Vector2(x, z), new Vector2(Mathf.Cos(pa) * pr, Mathf.Sin(pa) * pr));
                wall += (9f + 7f * Hash(k)) * Mathf.Pow(Mathf.Clamp01(1f - dd / 9f), 1.3f);
            }
            h += wall * north;
            return h;
        }

        static void BuildTerrain(Transform parent)
        {
            const float ext = 66f, step = 1.5f;
            int n = Mathf.RoundToInt(ext * 2f / step);
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            var hs = new float[(n + 1) * (n + 1)];
            for (int j = 0; j <= n; j++) for (int i = 0; i <= n; i++) hs[j * (n + 1) + i] = Height(-ext + i * step, -ext + j * step);
            var rnd = new System.Random(11);

            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = P(i, j), b = P(i + 1, j), c = P(i + 1, j + 1), d = P(i, j + 1);
                    bool flip = ((i + j) & 1) == 0;                         // alterna la diagonal: aspecto más facetado
                    if (flip) { Tri(a, d, c); Tri(a, c, b); } else { Tri(a, d, b); Tri(b, d, c); }
                }

            Vector3 P(int i, int j) => new Vector3(-ext + i * step, hs[j * (n + 1) + i], -ext + j * step);

            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                Vector3 cen = (p0 + p1 + p2) / 3f;
                Vector3 nrm = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                Color32 col = TerrainColor(cen, nrm, rnd);
                int bse = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                cols.Add(col); cols.Add(col); cols.Add(col);
                tris.Add(bse); tris.Add(bse + 1); tris.Add(bse + 2);
            }

            var mesh = new Mesh { name = "VolcanTerrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh = SaveMesh("Terrain", mesh);

            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = AssetLib.VertexToon();
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameLayers.Set(go, GameLayers.Environment);
            go.isStatic = true;
        }

        static Color32 TerrainColor(Vector3 c, Vector3 nrm, System.Random rnd)
        {
            float r = new Vector2(c.x, c.z).magnitude;
            float v = (float)rnd.NextDouble();
            float da = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(Altar.x, Altar.z));
            if (c.y < -0.55f) return World.Hex(0x3a1208);                                  // lecho de lava
            if (c.y < -0.08f && r < FloorR) return World.Hex(v < 0.5f ? 0x7a2a12 : 0x5e1f10); // costra caliente
            if (da < AltarR - 1.1f && c.y > AltarH - 0.1f) return World.Hex(v < 0.5f ? 0x1d1a22 : 0x25212b);   // obsidiana pulida
            if (c.y < 1.6f && r < FloorR + 1f)
            {
                float cd = CrackDistance(new Vector2(c.x, c.z));
                if (cd < 0.55f) return World.Hex(v < 0.5f ? 0x6e2410 : 0x58200f);           // borde chamuscado de las grietas
                if (cd < 1.1f && v < 0.45f) return World.Hex(0x3e2420);
                if (v < 0.02f) return World.Hex(0xc2410c);                                  // chispa incandescente suelta
                bool rim = nrm.y < 0.8f;                                                    // laderas de los bordes de cráter
                if (rim) return World.Hex(v < 0.5f ? 0x241e21 : 0x2c2427);
                return World.Hex(v < 0.33f ? 0x2a2427 : v < 0.66f ? 0x332b2e : 0x3b3134);   // basalto
            }
            bool steep = nrm.y < 0.55f;
            if (c.y < 5f) return World.Hex(steep ? 0x2a2224 : 0x3d3336);
            if (c.y < 12f) return World.Hex(steep ? 0x3a3032 : (v < 0.5f ? 0x4a3f40 : 0x52464a));
            if (c.y < 20f) return World.Hex(steep ? 0x4a4042 : (v < 0.5f ? 0x5a4f50 : 0x625759));
            return World.Hex(v < 0.5f ? 0x6e6465 : 0x7a7071);                               // ceniza en las cimas
        }

        // =============================================================== GRIETAS DE LAVA
        static Vector2 Dir2(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        /// Grietas ramificadas que salen de los cráteres de lava y del altar del jefe.
        static void GenerateCracks()
        {
            cracks = new List<List<Vector2>>();
            var rnd = new System.Random(77);
            foreach (var p in Pools)
            {
                int k = 2 + rnd.Next(2);
                for (int i = 0; i < k; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    Walk(new Vector2(p.x, p.y) + Dir2(a) * (p.z + 2.5f), a, 6 + rnd.Next(4), rnd, 1);
                }
            }
            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2f / 7f + (float)rnd.NextDouble() * 0.3f;
                Walk(new Vector2(Altar.x, Altar.z) + Dir2(a) * (AltarR + 0.4f), a, 5 + rnd.Next(4), rnd, 1);
            }
        }

        static void Walk(Vector2 p, float a, int steps, System.Random rnd, int depth)
        {
            if (!CrackOk(p)) return;
            var path = new List<Vector2> { p };
            for (int s = 0; s < steps; s++)
            {
                a += ((float)rnd.NextDouble() - 0.5f) * 1.1f;
                var q = p + Dir2(a) * (1.1f + (float)rnd.NextDouble() * 0.9f);
                if (!CrackOk(q)) break;
                path.Add(q); p = q;
                if (depth > 0 && rnd.NextDouble() < 0.22) Walk(p, a + (rnd.NextDouble() < 0.5 ? 0.9f : -0.9f), steps - s - 1, rnd, depth - 1);
            }
            if (path.Count >= 3) cracks.Add(path);
        }

        static bool CrackOk(Vector2 q)
        {
            if (q.magnitude > FloorR - 1.5f) return false;
            if (Vector2.Distance(q, new Vector2(Spawn.x, Spawn.z)) < 4.5f) return false;
            if (Vector2.Distance(q, new Vector2(Altar.x, Altar.z)) < AltarR + 0.2f) return false;
            foreach (var p in Pools) if (Vector2.Distance(q, new Vector2(p.x, p.y)) < p.z + 1.6f) return false;
            foreach (var c in DryCraters) if (Vector2.Distance(q, new Vector2(c.x, c.y)) < c.z + 0.9f) return false;
            foreach (var c in Cones) if (Vector2.Distance(q, new Vector2(c.x, c.y)) < c.z + 0.3f) return false;
            return true;
        }

        static float CrackDistance(Vector2 p)
        {
            float best = 99f;
            foreach (var path in cracks)
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    Vector2 a = path[i], b = path[i + 1], ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            return best;
        }

        static void BuildCracks(Transform parent)
        {
            var outer = new StripBuilder(); var inner = new StripBuilder();
            foreach (var path in cracks)
            {
                // Densifica la polilínea para que siga bien el relieve
                var pts = new List<Vector2>();
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    int sub = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(path[i], path[i + 1]) / 0.6f));
                    for (int k = 0; k < sub; k++) pts.Add(Vector2.Lerp(path[i], path[i + 1], k / (float)sub));
                }
                pts.Add(path[path.Count - 1]);
                outer.Add(pts, 0.34f, 0.05f);
                inner.Add(pts, 0.12f, 0.07f);
            }
            var g = new GameObject("Grietas").transform; g.SetParent(parent, false);
            var o = Renderable("Lava", g, SaveMesh("Cracks", outer.ToMesh("Cracks")), AssetLib.Glow(Fire), false);
            var po = o.AddComponent<GlowPulse>(); po.colorA = new Color(0.9f, 0.25f, 0.04f); po.colorB = new Color(1.3f, 0.5f, 0.1f); po.speed = 0.9f;
            var i2 = Renderable("Nucleo", g, SaveMesh("CracksCore", inner.ToMesh("CracksCore")), AssetLib.Glow(Hot), false);
            var pi = i2.AddComponent<GlowPulse>(); pi.colorA = new Color(1f, 0.6f, 0.2f); pi.colorB = new Color(1.5f, 1.1f, 0.45f); pi.speed = 1.3f;
        }

        /// Tiras planas que siguen el terreno (grietas) — de ancho decreciente hacia la punta.
        class StripBuilder
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<int> t = new List<int>();
            public void Add(List<Vector2> pts, float width, float lift)
            {
                int start = v.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 d = (i + 1 < pts.Count ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                    Vector2 nrm = new Vector2(-d.y, d.x);
                    float u = i / (float)(pts.Count - 1);
                    float w = width * (1f - Mathf.Pow(u, 1.6f)) * (0.75f + 0.5f * Mathf.PerlinNoise(pts[i].x * 0.9f, pts[i].y * 0.9f)) + 0.01f;
                    Vector2 l = pts[i] + nrm * w * 0.5f, r = pts[i] - nrm * w * 0.5f;
                    v.Add(new Vector3(l.x, Height(l.x, l.y) + lift, l.y));
                    v.Add(new Vector3(r.x, Height(r.x, r.y) + lift, r.y));
                }
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    int a = start + i * 2;
                    t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
                    t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); t.Add(a + 2);   // doble cara
                }
            }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        // =============================================================== LAVA
        static void BuildLava(Transform parent)
        {
            var g = new GameObject("Lava").transform; g.SetParent(parent, false);
            var disc = SaveMesh("LavaDisc", Disc(14));
            var mat = AssetLib.Glow(Fire);
            int i = 0;
            foreach (var p in Pools)
            {
                var pool = new GameObject("Crater_" + (i++)).transform;
                pool.SetParent(g, false);
                pool.position = new Vector3(p.x, LavaY, p.y);
                var surf = Renderable("Superficie", pool, disc, mat);
                surf.transform.localScale = new Vector3(p.z + 0.7f, 1f, p.z + 0.7f);
                surf.transform.localRotation = Quaternion.Euler(0f, i * 23f, 0f);
                var l = new GameObject("Luz").AddComponent<Light>();
                l.transform.SetParent(pool, false); l.transform.localPosition = Vector3.up * 1.6f;
                l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.12f); l.range = p.z * 2.8f; l.shadows = LightShadows.None;
                var pulse = surf.AddComponent<GlowPulse>();
                pulse.colorA = new Color(1f, 0.32f, 0.05f); pulse.colorB = new Color(1.25f, 0.62f, 0.15f); pulse.speed = 0.8f + 0.2f * i;
                pulse.pairedLight = l; pulse.lightMin = 1.2f; pulse.lightMax = 2.6f;
                var hz = pool.gameObject.AddComponent<LavaHazard>();
                hz.radius = p.z + 0.2f; hz.surfaceY = LavaY;
                VentSmoke(pool, Vector3.up * 0.2f, p.z * 0.5f, 1.5f, 0.8f);
            }
        }

        // =============================================================== CONOS PIRAMIDALES Y CRÁTERES APAGADOS
        static void BuildCones(Transform parent)
        {
            var g = new GameObject("Conos").transform; g.SetParent(parent, false);
            var rnd = new System.Random(31);
            var rock = new MeshBuilder(); var glow = new MeshBuilder();
            int i = 0;
            foreach (var c in Cones)
            {
                float baseY = Height(c.x, c.y) - 0.6f;
                float yaw = (float)rnd.NextDouble() * 60f;
                var m = Matrix4x4.TRS(new Vector3(c.x, baseY, c.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                float h1 = c.w * 0.55f, r1 = c.z * 0.55f;
                rock.Cyl(m, 6, r1, c.z, h1, MeshBuilder.Col(0x2a2325));                                     // falda (piramidal)
                rock.Cyl(m * Matrix4x4.TRS(new Vector3(0f, h1, 0f), Quaternion.Euler(0f, 30f, 0f), Vector3.one), 6, c.z * 0.24f, r1, c.w - h1 + 0.6f, MeshBuilder.Col(0x4a3f41));
                for (int k = 0; k < 3; k++)                                                                   // rocas al pie
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    rock.Icosa(Matrix4x4.TRS(new Vector3(c.x + Mathf.Cos(a) * c.z * 0.95f, baseY + 0.5f, c.y + Mathf.Sin(a) * c.z * 0.95f), Quaternion.Euler(0f, a * 50f, 0f), new Vector3(1f, 0.7f, 1f)), 0.5f + (float)rnd.NextDouble() * 0.4f, MeshBuilder.Col(0x2e2628));
                }
                Vector3 tip = new Vector3(c.x, baseY + c.w + 0.6f, c.y);
                glow.Cyl(Matrix4x4.TRS(tip + Vector3.down * 0.05f, Quaternion.Euler(0f, yaw + 30f, 0f), Vector3.one), 6, c.z * 0.2f, c.z * 0.2f, 0.06f, MeshBuilder.Col(Fire));   // boca incandescente
                // Coladas de lava bajando pegadas a dos caras del cono (centro de cara abajo, arista arriba)
                const float cos30 = 0.866f;
                for (int k = 0; k < 2; k++)
                {
                    float a = (yaw + 30f + k * 180f + 60f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                    Vector3 axis = new Vector3(c.x, 0f, c.y), off = dir * 0.07f;
                    Vector3 pTop = axis + dir * c.z * 0.24f + Vector3.up * (baseY + c.w + 0.55f) + off;
                    Vector3 pMid = axis + dir * r1 * cos30 + Vector3.up * (baseY + h1) + off;
                    Vector3 pBot = axis + dir * c.z * cos30 * 0.97f + Vector3.up * (baseY + 0.75f) + off;
                    foreach (var seg in new[] { (pTop, pMid), (pMid, pBot) })
                    {
                        Vector3 d = seg.Item2 - seg.Item1;
                        glow.Box(Matrix4x4.TRS((seg.Item1 + seg.Item2) * 0.5f, Quaternion.LookRotation(d), Vector3.one), 0.24f + 0.06f * k, 0.1f, d.magnitude + 0.15f, MeshBuilder.Col(Fire));
                    }
                }
                var l = new GameObject("Luz_" + i).AddComponent<Light>();
                l.transform.SetParent(g, false); l.transform.position = tip + Vector3.up * 0.8f;
                l.type = LightType.Point; l.color = new Color(1f, 0.42f, 0.1f); l.range = 7f; l.intensity = 2.2f; l.shadows = LightShadows.None;
                VentSmoke(g, tip, c.z * 0.25f, 2.2f, 1.1f);
                i++;
            }
            var go = Renderable("Roca", g, SaveMesh("Cones", rock.ToMesh("Cones")), AssetLib.VertexToon());
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            GameLayers.Set(go, GameLayers.Environment);
            go.isStatic = true;
            var lv = Renderable("Lava", g, SaveMesh("ConesLava", glow.ToMesh("ConesLava")), AssetLib.Glow(Fire), false);
            var p = lv.AddComponent<GlowPulse>(); p.colorA = new Color(1f, 0.3f, 0.05f); p.colorB = new Color(1.35f, 0.6f, 0.15f); p.speed = 1.1f;
        }

        static void BuildDryCraters(Transform parent)
        {
            var g = new GameObject("CrateresApagados").transform; g.SetParent(parent, false);
            var disc = SaveMesh("LavaDisc", Disc(14));
            int i = 0;
            foreach (var c in DryCraters)
            {
                var t = new GameObject("Crater_" + (i++)).transform; t.SetParent(g, false);
                t.position = new Vector3(c.x, Height(c.x, c.y) + 0.04f, c.y);
                var ember = Renderable("Brasa", t, disc, AssetLib.Glow(0xc2410c), false);
                ember.transform.localScale = new Vector3(c.z * 0.45f, 1f, c.z * 0.45f);
                var p = ember.AddComponent<GlowPulse>(); p.colorA = new Color(0.55f, 0.12f, 0.03f); p.colorB = new Color(1f, 0.32f, 0.06f); p.speed = 0.7f + 0.3f * i;
                VentSmoke(t, Vector3.up * 0.1f, c.z * 0.4f, 1.2f, 0.7f);
            }
        }

        /// Columna de humo que sale de una boca (cráter, cono).
        static void VentSmoke(Transform parent, Vector3 localPos, float radius, float size, float rate)
        {
            var ps = Particles("Humo", parent, Vector3.zero, SmokePuffMesh(), SmokeMat("Puff", new Color(0.12f, 0.09f, 0.09f, 0.75f), 0.7f, 1.2f, 0.25f, 0.03f, 1.8f));
            ps.transform.localPosition = localPos;
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size); main.maxParticles = 40; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6f);
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = Mathf.Max(0.1f, radius); sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.2f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.5f); vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            var size2 = ps.sizeOverLifetime; size2.enabled = true; size2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.4f));
            FadeInOut(ps, 0.7f);
        }

        static void FadeInOut(ParticleSystem ps, float peak)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.2f), new GradientAlphaKey(peak * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
        }

        // =============================================================== ALTAR Y RUNAS
        static void BuildAltar(Transform parent)
        {
            var g = new GameObject("AltarDeObsidiana").transform; g.SetParent(parent, false);
            g.position = new Vector3(Altar.x, AltarH + 0.03f, Altar.z);
            var mb = new RingBuilder();
            mb.Ring(Matrix4x4.identity, 4.7f, 0.22f, 48);
            mb.Ring(Matrix4x4.identity, 3.3f, 0.09f, 40);
            var rnd = new System.Random(5);
            for (int k = 0; k < 14; k++)
            {
                float a = k * Mathf.PI * 2f / 14f;
                var m = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 4.0f, 0f, Mathf.Sin(a) * 4.0f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + (float)rnd.NextDouble() * 40f, 0f), Vector3.one);
                mb.Quad(m, 0.18f + (float)rnd.NextDouble() * 0.2f, 0.45f);
            }
            var mesh = SaveMesh("AltarRunes", mb.ToMesh("AltarRunes"));
            var r = Renderable("Runas", g, mesh, AssetLib.Glow(Rune));
            var pulse = r.AddComponent<GlowPulse>(); pulse.colorA = new Color(0.8f, 0.12f, 0.05f); pulse.colorB = new Color(1.2f, 0.45f, 0.1f); pulse.speed = 1.6f;
            r.AddComponent<SlowSpin>().degreesPerSecond = new Vector3(0f, 6f, 0f);
        }

        // =============================================================== FRAGMENTOS DE OBSIDIANA
        static bool NearFeature(float x, float z, float margin)
        {
            foreach (var p in Pools) if (Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.y)) < p.z + 1.8f + margin) return true;
            foreach (var c in DryCraters) if (Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.y)) < c.z + 1.0f + margin) return true;
            foreach (var c in Cones) if (Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.y)) < c.z + 0.4f + margin) return true;
            return false;
        }

        static void BuildShards(Transform parent)
        {
            var g = new GameObject("Obsidiana").transform; g.SetParent(parent, false);
            var rnd = new System.Random(21);
            var dark = new MeshBuilder(); var glow = new MeshBuilder();
            var colliders = new GameObject("Colliders").transform; colliders.SetParent(g, false);

            int placed = 0;
            for (int tries = 0; tries < 500 && placed < 30; tries++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = FloorR - 4f + (float)rnd.NextDouble() * 4.5f;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(Spawn.x, Spawn.z)) < 7f) continue;     // entrada despejada
                if (NearFeature(x, z, 0f)) continue;
                float y = Height(x, z) - 0.2f;
                float h = 1.4f + (float)rnd.NextDouble() * 3.2f, rad = 0.35f + (float)rnd.NextDouble() * 0.45f;
                float tilt = ((float)rnd.NextDouble() - 0.5f) * 30f, yaw = (float)rnd.NextDouble() * 360f;
                Shard(dark, glow, x, y, z, h, rad, tilt, yaw, rnd);
                int kids = rnd.Next(1, 4);
                for (int k = 0; k < kids; k++)
                {
                    float ka = (float)rnd.NextDouble() * Mathf.PI * 2f, kd = rad + 0.4f + (float)rnd.NextDouble() * 0.6f;
                    float kx = x + Mathf.Cos(ka) * kd, kz = z + Mathf.Sin(ka) * kd;
                    Shard(dark, glow, kx, Height(kx, kz) - 0.1f, kz, h * (0.3f + (float)rnd.NextDouble() * 0.35f), rad * 0.55f, tilt + ((float)rnd.NextDouble() - 0.5f) * 50f, yaw + 60f * k, rnd);
                }
                var col = new GameObject("Shard").AddComponent<CapsuleCollider>();
                col.transform.SetParent(colliders, false);
                col.transform.position = new Vector3(x, y, z);
                col.radius = rad + 0.25f; col.height = h; col.center = new Vector3(0f, h * 0.5f, 0f);
                GameLayers.Set(col.gameObject, GameLayers.Obstacles);
                placed++;
            }
            Renderable("Fragmentos", g, SaveMesh("Shards", dark.ToMesh("Shards")), AssetLib.VertexToon()).isStatic = true;
            Renderable("Vetas", g, SaveMesh("ShardVeins", glow.ToMesh("ShardVeins")), AssetLib.Glow(Fire), false).isStatic = true;
        }

        static void Shard(MeshBuilder dark, MeshBuilder glow, float x, float y, float z, float h, float r, float tilt, float yaw, System.Random rnd)
        {
            int sides = rnd.Next(4, 6);
            var m = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(tilt, yaw, tilt * 0.5f), Vector3.one);
            int c = rnd.NextDouble() < 0.5 ? Obs : Obs2;
            dark.Cyl(m, sides, r * 0.75f, r, h * 0.45f, MeshBuilder.Col(c));
            dark.Cone(m * Matrix4x4.Translate(new Vector3(0f, h * 0.45f, 0f)), sides, r * 0.75f, h * 0.55f, MeshBuilder.Col(Obs3));
            if (rnd.NextDouble() < 0.45)
                glow.Octa(m * Matrix4x4.TRS(new Vector3(r * 0.6f, h * 0.25f, 0f), Quaternion.identity, new Vector3(0.12f, h * 0.18f, 0.12f)), 1f, MeshBuilder.Col(Fire));
        }

        // =============================================================== VOLCÁN DE FONDO
        /// Gran volcán cónico y facetado detrás del jefe, con cráter humeante y coladas de lava hacia la arena.
        static void BuildVolcano(Transform parent)
        {
            var g = new GameObject("VolcanFondo").transform; g.SetParent(parent, false);
            g.position = new Vector3(6f, -8f, 128f);
            const int sides = 18, rings = 9; const float baseR = 88f, topR = 14f, H = 80f;
            float Profile(float u) => topR + (baseR - topR) * Mathf.Pow(1f - u, 1.7f);
            var rnd = new System.Random(9);
            var P = new Vector3[rings + 1, sides];
            for (int j = 0; j <= rings; j++)
            {
                float u = j / (float)rings, r = Profile(u);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides + (j % 2) * 0.08f;
                    float jit = j == 0 || j == rings ? 0.04f : 0.16f;
                    float rr = r * (1f + ((float)rnd.NextDouble() - 0.5f) * jit) * (1f + 0.05f * Mathf.Sin(a * 5f + j));
                    float yy = H * u + (j > 0 && j < rings ? ((float)rnd.NextDouble() - 0.5f) * 3.5f : 0f);
                    P[j, i] = new Vector3(Mathf.Cos(a) * rr, yy, Mathf.Sin(a) * rr);
                }
            }
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, int hex)
            {
                int bse = verts.Count; verts.Add(a); verts.Add(b); verts.Add(c);
                Color32 col = World.Hex(hex); cols.Add(col); cols.Add(col); cols.Add(col);
                tris.Add(bse); tris.Add(bse + 1); tris.Add(bse + 2);
            }
            int ColorAt(float u, int i, int j) => u < 0.35f ? ((i + j) % 2 == 0 ? 0x2b2326 : 0x241d20) : u < 0.72f ? ((i + j) % 2 == 0 ? 0x3a3033 : 0x332a2d) : ((i + j) % 2 == 0 ? 0x5e5455 : 0x544a4c);
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < sides; i++)
                {
                    int i2 = (i + 1) % sides; float u = (j + 0.5f) / rings;
                    Tri(P[j, i], P[j + 1, i], P[j + 1, i2], ColorAt(u, i, j));
                    Tri(P[j, i], P[j + 1, i2], P[j, i2], ColorAt(u, i + 1, j));
                }
            // Interior del cráter
            var innerRing = new Vector3[sides];
            for (int i = 0; i < sides; i++) { float a = i * Mathf.PI * 2f / sides; innerRing[i] = new Vector3(Mathf.Cos(a) * topR * 0.7f, H - 6f, Mathf.Sin(a) * topR * 0.7f); }
            for (int i = 0; i < sides; i++)
            {
                int i2 = (i + 1) % sides;
                Tri(P[rings, i], P[rings, i2], innerRing[i2], 0x1f1a1c);
                Tri(P[rings, i], innerRing[i2], innerRing[i], 0x1f1a1c);
            }
            var mesh = new Mesh { name = "Volcano", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Renderable("Montaña", g, SaveMesh("Volcano", mesh), AssetLib.VertexToon());

            // Lago de lava del cráter y coladas hacia la arena (cara sur)
            var lava = new MeshBuilder();
            lava.Cyl(Matrix4x4.TRS(new Vector3(0f, H - 5.6f, 0f), Quaternion.identity, Vector3.one), sides, topR * 0.72f, topR * 0.72f, 0.4f, MeshBuilder.Col(Fire));
            for (int k = 0; k < 5; k++)
            {
                float a0 = (230f + k * 20f + (float)rnd.NextDouble() * 8f) * Mathf.Deg2Rad;
                Vector3 prev = Vector3.zero; bool first = true;
                for (int s = 0; s <= 10; s++)
                {
                    float u = 1f - s / 10f * 0.92f;
                    float a = a0 + Mathf.Sin(s * 0.9f + k) * 0.06f;
                    float r = Profile(u) * 1.03f + 1.2f;
                    Vector3 pt = new Vector3(Mathf.Cos(a) * r, H * u + 0.8f, Mathf.Sin(a) * r);
                    if (!first)
                    {
                        Vector3 d = pt - prev, mid = (pt + prev) * 0.5f;
                        float w = 2f + (1f - u) * 4f - k * 0.2f;
                        lava.Box(Matrix4x4.TRS(mid, Quaternion.LookRotation(d), Vector3.one), w, 0.5f, d.magnitude + 0.6f, MeshBuilder.Col(Fire));
                    }
                    prev = pt; first = false;
                }
            }
            var lv = Renderable("Lava", g, SaveMesh("VolcanoLava", lava.ToMesh("VolcanoLava")), AssetLib.Glow(Fire), false);
            var pulse = lv.AddComponent<GlowPulse>(); pulse.colorA = new Color(1f, 0.3f, 0.05f); pulse.colorB = new Color(1.4f, 0.6f, 0.14f); pulse.speed = 0.6f;
            var l = new GameObject("Resplandor").AddComponent<Light>();
            l.transform.SetParent(g, false); l.transform.localPosition = new Vector3(0f, H + 6f, 0f);
            l.type = LightType.Point; l.color = new Color(1f, 0.4f, 0.1f); l.range = 90f; l.intensity = 3f; l.shadows = LightShadows.None;

            // Penacho de humo y ceniza del volcán
            var smoke = Particles("Penacho", g, g.position + new Vector3(0f, H - 2f, 0f), SmokePuffMesh(), SmokeMat("Plume", new Color(0.14f, 0.1f, 0.1f, 0.85f), 0.9f, 1.0f, 0.2f, 0.05f, 0.25f));
            {
                var main = smoke.main; main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f); main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(6f, 11f); main.maxParticles = 140; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6f);
                var em = smoke.emission; em.rateOverTime = 8f;
                var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 8f; sh.rotation = new Vector3(-90f, 0f, 0f);
                var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(1.5f, 3f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                var size = smoke.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.4f));
                var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                FadeInOut(smoke, 0.85f);
            }
        }

        // =============================================================== PARTÍCULAS DEL AMBIENTE
        static void BuildParticles(Transform parent)
        {
            var octa = SaveMesh("EmberMesh", Single(b => b.Octa(Matrix4x4.identity, 1f, MeshBuilder.Col(Hot))));

            // Brasas que suben por toda la arena
            var embers = Particles("Brasas", parent, new Vector3(0f, 0f, 0f), octa, AssetLib.Glow(0xff8a2a));
            {
                var main = embers.main; main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f); main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f); main.maxParticles = 400; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6f);
                main.startColor = new Color(1.6f, 0.8f, 0.3f);
                var em = embers.emission; em.rateOverTime = 45f;
                var sh = embers.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(46f, 0.5f, 46f);
                var vel = embers.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f); vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                var size = embers.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }

            // Ceniza que cae lentamente sobre el cráter
            var ash = Particles("Ceniza", parent, new Vector3(0f, 14f, 0f), octa, AssetLib.Toon(0x6b6264));
            {
                var main = ash.main; main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f); main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); main.maxParticles = 300; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6f);
                var em = ash.emission; em.rateOverTime = 25f;
                var sh = ash.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(50f, 1f, 50f);
                var vel = ash.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0.2f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(-1.4f, -0.8f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                var rot = ash.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true; rot.x = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            }
        }

        static ParticleSystem Particles(string name, Transform parent, Vector3 pos, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.duration = 10f; main.prewarm = true;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = mesh; r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        // =============================================================== LÍMITES
        static void BuildBounds(Transform parent)
        {
            var g = new GameObject("LimiteDelCrater").transform; g.SetParent(parent, false);
            const int n = 36; float r = FloorR + 1.5f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var b = new GameObject("Muro_" + i).AddComponent<BoxCollider>();
                b.transform.SetParent(g, false);
                b.transform.position = new Vector3(Mathf.Cos(a) * r, 4f, Mathf.Sin(a) * r);
                b.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                b.size = new Vector3(2f * Mathf.PI * r / n + 0.6f, 14f, 1f);
                GameLayers.Set(b.gameObject, GameLayers.MapBounds);
            }
        }

        // =============================================================== HUMO (materiales y mallas)
        static Mesh smokePuff;
        static Mesh SmokePuffMesh()
        {
            if (smokePuff == null) smokePuff = SaveMesh("SmokePuff", SmoothIcosa(1));
            return smokePuff;
        }

        /// Icosaedro con normales suaves (el shader de humo necesita normales suaves para difuminar el contorno).
        static Mesh SmoothIcosa(int detail)
        {
            var b = new MeshBuilder(); b.Icosa(Matrix4x4.identity, 1f, new Color32(255, 255, 255, 255), detail);
            var m = b.ToMesh("puff");
            var v = m.vertices; var nrm = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) nrm[i] = v[i].normalized;
            m.normals = nrm;
            return m;
        }

        static Material SmokeMat(string name, Color baseC, float edge, float soft, float dissolve, float wobble, float noiseScale)
        {
            string path = AssetLib.Gen + "/Materials/Smoke_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("MageHero/Smoke");
                if (sh == null) { Debug.LogError("[MageHero] Falta el shader MageHero/Smoke."); sh = Shader.Find("Universal Render Pipeline/Unlit"); }
                m = new Material(sh);
                AssetLib.EnsureFolder(AssetLib.Gen + "/Materials");
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", baseC);
            m.SetColor("_EdgeColor", new Color(1f, 0.33f, 0.07f, 1f));
            m.SetFloat("_EdgeStrength", edge); m.SetFloat("_Soft", soft); m.SetFloat("_Dissolve", dissolve);
            m.SetFloat("_Wobble", wobble); m.SetFloat("_NoiseScale", noiseScale);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// Superficie de revolución con normales suaves (capas de la columna de humo).
        static Mesh Lathe(string name, float[] ys, float[] rs, int sides, int seed, float jitter)
        {
            var rnd = new System.Random(seed);
            var v = new List<Vector3>(); var t = new List<int>();
            for (int j = 0; j < ys.Length; j++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides + j * 0.35f;
                    float r = rs[j] * (1f + ((float)rnd.NextDouble() - 0.5f) * jitter);
                    v.Add(new Vector3(Mathf.Cos(a) * r, ys[j], Mathf.Sin(a) * r));
                }
            for (int j = 0; j + 1 < ys.Length; j++)
                for (int i = 0; i < sides; i++)
                {
                    int i2 = (i + 1) % sides;
                    int a = j * sides + i, b = j * sides + i2, c = (j + 1) * sides + i, d = (j + 1) * sides + i2;
                    t.Add(a); t.Add(c); t.Add(d); t.Add(a); t.Add(d); t.Add(b);
                }
            var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// Estela de humo que sale de una articulación (se arrastra al mover el brazo: movimiento más fluido).
        static ParticleSystem Wisp(string name, Transform parent, Vector3 localPos, float size, float rate, float perMeter)
        {
            var ps = Particles(name, parent, Vector3.zero, SmokePuffMesh(), SmokeMat("Wisp", new Color(0.06f, 0.045f, 0.05f, 0.8f), 1.1f, 1.1f, 0.25f, 0.03f, 2.4f));
            ps.transform.localPosition = localPos;
            var main = ps.main; main.prewarm = false; main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size); main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6f);
            var em = ps.emission; em.rateOverTime = rate; em.rateOverDistance = perMeter;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = size * 0.4f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f); vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.7f); vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.6f));
            FadeInOut(ps, 0.85f);
            return ps;
        }

        // =============================================================== JEFE (modelo según la ficha técnica)
        static GameObject BuildBossPrefab()
        {
            var root = new GameObject("Jefe_ElementalObsidiana");
            var hs = root.AddComponent<HealthSystem>();
            hs.team = Team.Enemy; hs.maxHealth = 900f; hs.destroyOnDeath = false; hs.hitInvulnerability = 0.1f;
            var el = root.AddComponent<EnemyLevel>(); el.isBoss = true; el.displayName = "ELEMENTAL DE OBSIDIANA"; el.enabled = false;
            var col = root.AddComponent<CapsuleCollider>(); col.radius = 1.05f; col.height = 4.6f; col.center = new Vector3(0f, 2.3f, 0f);
            var fx = new List<ParticleSystem>();
            var ember = SaveMesh("EmberMesh", Single(x => x.Octa(Matrix4x4.identity, 1f, MeshBuilder.Col(Hot))));

            var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
            var toon = AssetLib.VertexToon();
            var fire = AssetLib.Glow(Fire); var hot = AssetLib.Glow(Hot);

            // --- Torso de obsidiana facetada (de la cintura para arriba: abajo es humo)
            var b = new MeshBuilder();
            b.Cone(T(0f, 2.15f, 0f, 180f, 0f, 0f), 7, 0.6f, 0.95f, MeshBuilder.Col(BObs2));        // pelvis en punta que se hunde en el humo
            b.Cyl(T(0f, 2.05f, 0f), 7, 0.62f, 0.5f, 0.3f, MeshBuilder.Col(BObs3));                // cintura
            b.Cyl(T(0f, 2.3f, 0f), 6, 0.98f, 0.6f, 1.1f, MeshBuilder.Col(BObs2));                 // caparazón del torso
            b.Box(T(0.4f, 3.0f, 0.56f, 0f, 20f, 0f), 0.48f, 0.55f, 0.12f, MeshBuilder.Col(BObs3));   // placas pectorales
            b.Box(T(-0.4f, 3.0f, 0.56f, 0f, -20f, 0f), 0.48f, 0.55f, 0.12f, MeshBuilder.Col(BObs3));
            b.Cyl(T(0f, 3.4f, 0f), 6, 0.45f, 0.86f, 0.25f, MeshBuilder.Col(BObs));                // collar
            foreach (float s in new[] { 1f, -1f })
            {
                b.Icosa(T(s * 1.0f, 3.32f, 0f, 0f, 0f, 0f, 1.1f, 0.9f, 1f), 0.45f, MeshBuilder.Col(BObs3));        // hombros
                b.Cone(T(s * 1.15f, 3.55f, -0.05f, 0f, 0f, -s * 30f), 4, 0.17f, 1.05f, MeshBuilder.Col(BObs));   // espinas de hombro
                b.Cone(T(s * 0.85f, 3.6f, -0.25f, -25f, 0f, -s * 15f), 4, 0.12f, 0.7f, MeshBuilder.Col(BObs));
                b.Cone(T(s * 0.32f, 3.25f, -0.45f, -45f, 0f, -s * 15f), 4, 0.13f, 0.9f, MeshBuilder.Col(BObs));   // espinas dorsales
            }
            b.Cone(T(0f, 3.0f, -0.5f, -55f, 0f, 0f), 4, 0.16f, 1.15f, MeshBuilder.Col(BObs));
            b.Cone(T(0f, 2.55f, -0.48f, -60f, 0f, 0f), 4, 0.12f, 0.8f, MeshBuilder.Col(BObs));
            b.Cyl(T(0f, 3.55f, 0.02f), 5, 0.18f, 0.24f, 0.3f, MeshBuilder.Col(BObs));             // cuello
            var torsoMesh = SaveMesh("BossBody", b.ToMesh("BossBody"));
            Renderable("Caparazon", body, torsoMesh, toon);

            // Halo espectral: envoltura de humo que ondula alrededor del torso y difumina su contorno
            var halo = Renderable("HaloDeHumo", body, torsoMesh, SmokeMat("Halo", new Color(0.04f, 0.03f, 0.035f, 0.32f), 1.6f, 0.7f, 0.5f, 0.06f, 2.6f), false);
            halo.transform.localScale = Vector3.one * 1.09f; halo.transform.localPosition = new Vector3(0f, -2.8f * 0.09f, 0f);

            // --- Núcleo ígneo asimétrico y vetas
            var core = new MeshBuilder();
            core.Octa(T(0.03f, 2.85f, 0.52f, 0f, 0f, 6f, 0.3f, 0.62f, 0.14f), 1f, MeshBuilder.Col(Fire));
            core.Box(T(0f, 2.42f, 0.56f), 0.09f, 0.6f, 0.05f, MeshBuilder.Col(Fire));
            core.Box(T(0.2f, 2.98f, 0.6f, 0f, 0f, 35f), 0.06f, 0.52f, 0.04f, MeshBuilder.Col(Fire));
            core.Box(T(-0.22f, 3.05f, 0.6f, 0f, 0f, -40f), 0.06f, 0.42f, 0.04f, MeshBuilder.Col(Fire));
            core.Box(T(0.28f, 2.6f, 0.57f, 0f, 0f, -40f), 0.05f, 0.38f, 0.04f, MeshBuilder.Col(Fire));
            core.Octa(T(0f, 2.95f, -0.52f, 0f, 0f, 0f, 0.25f, 0.45f, 0.1f), 1f, MeshBuilder.Col(Fire));    // ventila dorsal
            foreach (float s in new[] { 1f, -1f })
                core.Box(T(s * 0.62f, 3.15f, 0.22f), 0.05f, 0.35f, 0.05f, MeshBuilder.Col(Fire));            // juntas de los hombros
            var coreR = Renderable("Nucleo", body, SaveMesh("BossCore", core.ToMesh("BossCore")), fire, false);
            var inner = new MeshBuilder();
            inner.Octa(T(0.03f, 2.85f, 0.6f, 0f, 0f, 6f, 0.13f, 0.38f, 0.08f), 1f, MeshBuilder.Col(Hot));
            Renderable("Resplandor", body, SaveMesh("BossCoreInner", inner.ToMesh("BossCoreInner")), hot, false);
            var coreEmbers = Wisp("BrasasNucleo", body, new Vector3(0f, 2.9f, 0.5f), 0.12f, 6f, 0f);
            coreEmbers.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetLib.Glow(0xff8a2a);
            coreEmbers.GetComponent<ParticleSystemRenderer>().mesh = ember;
            fx.Add(coreEmbers);

            // --- Cabeza (pivote en el cuello: sigue al mago) con ojos y corona de llamas
            var head = new GameObject("Head").transform; head.SetParent(body, false); head.localPosition = new Vector3(0f, 3.6f, 0.02f);
            var hm = new MeshBuilder();
            hm.Octa(T(0f, 0.38f, 0.04f, 10f, 0f, 0f, 0.32f, 0.5f, 0.38f), 1f, MeshBuilder.Col(BObs2));     // cabeza alienígena facetada
            hm.Cone(T(0f, 0.18f, 0.22f, 150f, 0f, 0f), 4, 0.11f, 0.35f, MeshBuilder.Col(BObs2));            // mentón en punta
            foreach (float s in new[] { 1f, -1f })
                hm.Cone(T(s * 0.16f, 0.45f, -0.07f, -20f, 0f, -s * 25f), 4, 0.07f, 0.55f, MeshBuilder.Col(BObs));   // cuernos
            Renderable("Craneo", head, SaveMesh("BossHead", hm.ToMesh("BossHead")), toon);
            var eyes = new MeshBuilder();
            foreach (float s in new[] { 1f, -1f })
                eyes.Box(T(s * 0.11f, 0.38f, 0.38f, 0f, 0f, s * 15f), 0.13f, 0.04f, 0.04f, MeshBuilder.Col(Hot));
            Renderable("Ojos", head, SaveMesh("BossEyes", eyes.ToMesh("BossEyes")), hot, false);

            var flames = new GameObject("Flames").transform; flames.SetParent(head, false); flames.localPosition = new Vector3(0f, 0.62f, -0.02f);
            var fo = new MeshBuilder();
            fo.Cone(T(0f, 0f, 0f), 5, 0.3f, 1.05f, MeshBuilder.Col(Fire));
            fo.Cone(T(0.17f, -0.02f, 0.02f, 0f, 0f, -24f), 4, 0.15f, 0.68f, MeshBuilder.Col(Fire));
            fo.Cone(T(-0.17f, -0.02f, 0.02f, 0f, 0f, 24f), 4, 0.15f, 0.62f, MeshBuilder.Col(Fire));
            fo.Cone(T(0f, 0.05f, -0.13f, -18f, 0f, 0f), 4, 0.17f, 0.8f, MeshBuilder.Col(Fire));
            Renderable("Exterior", flames, SaveMesh("BossFlames", fo.ToMesh("BossFlames")), fire, false);
            var fi = new MeshBuilder();
            fi.Cone(T(0f, 0f, 0.05f), 4, 0.14f, 1.3f, MeshBuilder.Col(Hot));
            Renderable("Interior", flames, SaveMesh("BossFlamesInner", fi.ToMesh("BossFlamesInner")), hot, false);

            // --- Brazos articulados: hombro → codo → mano (cada uno con su estela de humo)
            var armL = Arm(body, "L", 1f, toon, fire, fx);
            var armR = Arm(body, "R", -1f, toon, fire, fx);
            foreach (float s in new[] { 1f, -1f }) fx.Add(Wisp("HumoHombro" + (s > 0 ? "L" : "R"), body, new Vector3(s * 1.05f, 3.5f, -0.1f), 0.4f, 5f, 2f));

            // --- Columna de humo (en lugar de piernas): capas que ondulan + humo y ceniza que se arrastran
            var smoke = new GameObject("ColumnaDeHumo").transform; smoke.SetParent(root.transform, false);
            var shellIn = Lathe("BossSmokeInner", new[] { 0f, 0.3f, 0.75f, 1.3f, 1.85f, 2.35f }, new[] { 1.2f, 0.95f, 0.72f, 0.6f, 0.52f, 0.38f }, 10, 3, 0.18f);
            var shellOut = Lathe("BossSmokeOuter", new[] { 0f, 0.2f, 0.6f, 1.1f, 1.7f, 2.2f }, new[] { 1.75f, 1.35f, 0.98f, 0.8f, 0.66f, 0.45f }, 11, 5, 0.28f);
            var skirt = Lathe("BossSmokeSkirt", new[] { 0f, 0.18f, 0.45f, 0.8f }, new[] { 2.4f, 2.0f, 1.3f, 0.7f }, 12, 7, 0.3f);
            var colOuter = SmokeMat("ColumnOuter", new Color(0.08f, 0.06f, 0.065f, 0.55f), 0.8f, 2.0f, 0.42f, 0.15f, 1.3f);
            var s1 = Renderable("Capa1", smoke, SaveMesh("BossSmokeInner", shellIn), SmokeMat("Column", new Color(0.05f, 0.04f, 0.045f, 0.92f), 1.0f, 1.3f, 0.28f, 0.1f, 1.7f), false);
            s1.AddComponent<SlowSpin>().degreesPerSecond = new Vector3(0f, 40f, 0f);
            var s2 = Renderable("Capa2", smoke, SaveMesh("BossSmokeOuter", shellOut), colOuter, false);
            s2.AddComponent<SlowSpin>().degreesPerSecond = new Vector3(0f, -28f, 0f);
            var s3 = Renderable("Falda", smoke, SaveMesh("BossSmokeSkirt", skirt), colOuter, false);
            s3.AddComponent<SlowSpin>().degreesPerSecond = new Vector3(0f, 18f, 0f);

            // Fragmentos de obsidiana que orbitan dentro del humo (restos del cuerpo)
            var orbit = new GameObject("Fragmentos").transform; orbit.SetParent(smoke, false);
            var ob = new MeshBuilder(); var rr0 = new System.Random(17);
            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2f / 7f, rad = 0.75f + (float)rr0.NextDouble() * 0.5f, y = 0.4f + (float)rr0.NextDouble() * 1.3f;
                ob.Octa(T(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad, (float)rr0.NextDouble() * 90f, a * 57f, 20f, 0.12f, 0.28f, 0.12f), 1f, MeshBuilder.Col(k % 3 == 0 ? Fire : BObs3));
            }
            Renderable("Restos", orbit, SaveMesh("BossOrbitShards", ob.ToMesh("BossOrbitShards")), toon, false);
            orbit.gameObject.AddComponent<SlowSpin>().degreesPerSecond = new Vector3(0f, -55f, 0f);

            var puffs = Particles("HumoBase", smoke, Vector3.zero, SmokePuffMesh(), SmokeMat("Puff", new Color(0.12f, 0.09f, 0.09f, 0.75f), 0.7f, 1.2f, 0.25f, 0.03f, 1.8f));
            {
                puffs.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                var main = puffs.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f); main.maxParticles = 90; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startColor = new Color(0.55f, 0.45f, 0.45f, 1f);
                main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6f);
                var em = puffs.emission; em.rateOverTime = 22f; em.rateOverDistance = 3f;
                var sh = puffs.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.9f; sh.rotation = new Vector3(-90f, 0f, 0f);
                var vel = puffs.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f); vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.5f); vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                var size = puffs.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.0f));
                FadeInOut(puffs, 0.8f);
            }
            fx.Add(puffs);
            var ashB = Particles("CenizaRemolino", smoke, Vector3.zero, ember, AssetLib.Toon(0x6b6264));
            {
                ashB.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                var main = ashB.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var em = ashB.emission; em.rateOverTime = 20f;
                var sh = ashB.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.1f; sh.rotation = new Vector3(-90f, 0f, 0f);
                var vel = ashB.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);       // los tres ejes orbitales deben usar el mismo modo
                vel.orbitalY = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
                var size = ashB.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }
            fx.Add(ashB);

            // --- Anillo rúnico detrás del jefe: dos ruedas que giran en sentidos opuestos con inscripciones
            var ring = new GameObject("RuneRing").transform; ring.SetParent(root.transform, false);
            ring.localPosition = new Vector3(0f, 3.0f, -1.05f);
            var outerT = new GameObject("RuedaExterior").transform; outerT.SetParent(ring, false);
            var innerT = new GameObject("RuedaInterior").transform; innerT.SetParent(ring, false); innerT.localPosition = new Vector3(0f, 0f, 0.03f);
            var rr = new System.Random(3);
            var ro = new RingBuilder(true);
            ro.Ring(Matrix4x4.identity, 2.05f, 0.12f, 48);
            ro.Ring(Matrix4x4.identity, 1.78f, 0.04f, 44);
            for (int k = 0; k < 24; k++)                                                          // marcas de la ruleta
            {
                float a = k * Mathf.PI * 2f / 24f;
                var m = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 1.92f, Mathf.Sin(a) * 1.92f, 0f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), Vector3.one);
                ro.Quad(m, k % 3 == 0 ? 0.2f : 0.1f, 0.035f);
            }
            for (int k = 0; k < 10; k++)                                                          // lenguas de fuego en el borde
            {
                float a = (k + 0.5f) * Mathf.PI * 2f / 10f;
                var m = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 2.1f, Mathf.Sin(a) * 2.1f, 0f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f), Vector3.one);
                ro.Tri(m, 0.12f, 0.35f + (float)rr.NextDouble() * 0.25f);
            }
            var ri = new RingBuilder(true);
            ri.Ring(Matrix4x4.identity, 1.6f, 0.06f, 40);
            ri.Ring(Matrix4x4.identity, 1.12f, 0.05f, 36);
            for (int k = 0; k < 14; k++)                                                          // inscripciones (glifos de 2-3 trazos)
            {
                float a = k * Mathf.PI * 2f / 14f, deg = a * Mathf.Rad2Deg;
                Vector3 c = new Vector3(Mathf.Cos(a) * 1.36f, Mathf.Sin(a) * 1.36f, 0f);
                int strokes = 2 + rr.Next(2);
                for (int q = 0; q < strokes; q++)
                {
                    float off = (q - (strokes - 1) * 0.5f) * 0.07f;
                    float rot = new[] { 0f, 35f, -35f, 90f }[rr.Next(4)];
                    var m = Matrix4x4.TRS(c + Quaternion.Euler(0f, 0f, deg) * new Vector3(0f, off, 0f), Quaternion.Euler(0f, 0f, deg + rot), Vector3.one);
                    ri.Quad(m, 0.03f + (float)rr.NextDouble() * 0.02f, 0.13f + (float)rr.NextDouble() * 0.1f);
                }
            }
            for (int k = 0; k < 6; k++)                                                           // estrella central
            {
                float a = k * Mathf.PI / 3f;
                var m = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.55f, 0f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), Vector3.one);
                ri.Quad(m, 0.03f, 0.95f);
            }
            var runeMat = AssetLib.Glow(Rune);
            var outerR = Renderable("Runas", outerT, SaveMesh("BossRuneRing", ro.ToMesh("BossRuneRing")), runeMat, false);
            var innerR = Renderable("Inscripciones", innerT, SaveMesh("BossRuneRingInner", ri.ToMesh("BossRuneRingInner")), runeMat, false);
            var ringLight = new GameObject("LuzAnillo").AddComponent<Light>();
            ringLight.transform.SetParent(ring, false); ringLight.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            ringLight.type = LightType.Point; ringLight.color = new Color(1f, 0.3f, 0.1f); ringLight.range = 8f; ringLight.intensity = 1.2f; ringLight.shadows = LightShadows.None;

            // --- Luz de relleno fría delante del jefe: separa la obsidiana del fondo cálido
            var fill = new GameObject("LuzRelleno").AddComponent<Light>();
            fill.transform.SetParent(root.transform, false); fill.transform.localPosition = new Vector3(0f, 3.4f, 3.2f);
            fill.type = LightType.Point; fill.color = new Color(0.55f, 0.6f, 1f); fill.range = 7.5f; fill.intensity = 1.6f; fill.shadows = LightShadows.None;

            // --- Luz del núcleo
            var light = new GameObject("LuzNucleo").AddComponent<Light>();
            light.transform.SetParent(body, false); light.transform.localPosition = new Vector3(0f, 2.9f, 1.3f);
            light.type = LightType.Point; light.color = new Color(1f, 0.45f, 0.1f); light.range = 9f; light.intensity = 2f; light.shadows = LightShadows.None;

            // --- Comportamiento
            var boss = root.AddComponent<ObsidianBoss>();
            boss.body = body; boss.head = head; boss.flames = flames;
            boss.armL = armL[0]; boss.elbowL = armL[1]; boss.handL = armL[2];
            boss.armR = armR[0]; boss.elbowR = armR[1]; boss.handR = armR[2];
            boss.smokeColumn = smoke;
            boss.runeRing = ring; boss.ringOuter = outerT; boss.ringInner = innerT;
            boss.ringRenderers = new Renderer[] { outerR.GetComponent<Renderer>(), innerR.GetComponent<Renderer>() };
            boss.ringLight = ringLight;
            boss.coreRenderers = new Renderer[] { coreR.GetComponent<Renderer>() };
            boss.coreLight = light;
            boss.smokeFx = fx.ToArray();
            boss.arenaCenter = new Vector3(0f, 0f, 0f); boss.arenaRadius = FloorR - 3f;
            boss.fireballMesh = SaveMesh("FxFireball", Single(x => x.Icosa(Matrix4x4.identity, 1f, MeshBuilder.Col(Fire))));
            boss.discMesh = SaveMesh("FxDisc", Disc(24));
            boss.pillarMesh = SaveMesh("FxPillar", Single(x => x.Cyl(Matrix4x4.identity, 7, 0.55f, 1f, 1f, MeshBuilder.Col(Fire))));
            boss.fireMaterial = fire; boss.hotMaterial = hot; boss.warningMaterial = AssetLib.Glow(0xff7a1a);

            GameLayers.Set(root, GameLayers.Enemies, true);
            AssetDatabase.DeleteAsset(BossPrefabPath);                 // la jerarquía cambió: prefab nuevo y limpio
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// Brazo en 3 piezas articuladas. Devuelve { hombro, codo, mano }.
        static Transform[] Arm(Transform body, string side, float s, Material toon, Material fire, List<ParticleSystem> fx)
        {
            var shoulder = new GameObject("Hombro" + side).transform; shoulder.SetParent(body, false);
            shoulder.localPosition = new Vector3(s * 0.98f, 3.22f, 0f);
            shoulder.localRotation = Quaternion.Euler(0f, 0f, s * 8f);
            var ua = new MeshBuilder();
            ua.Cyl(T(s * 0.08f, -1.0f, 0f), 5, 0.22f, 0.17f, 1.0f, MeshBuilder.Col(BObs2));          // brazo
            ua.Icosa(T(s * 0.1f, -1.05f, 0f), 0.18f, MeshBuilder.Col(BObs3));                         // codo
            Renderable("Brazo", shoulder, SaveMesh("BossArm" + side, ua.ToMesh("BossArm" + side)), toon);

            var elbow = new GameObject("Codo" + side).transform; elbow.SetParent(shoulder, false);
            elbow.localPosition = new Vector3(s * 0.1f, -1.05f, 0f);
            var fa = new MeshBuilder(); var fg = new MeshBuilder();
            fa.Cyl(T(s * 0.02f, -0.95f, 0.05f), 5, 0.16f, 0.13f, 0.95f, MeshBuilder.Col(BObs2));    // antebrazo
            fa.Cone(T(s * 0.14f, -0.4f, -0.1f, -120f, 0f, -s * 20f), 4, 0.06f, 0.38f, MeshBuilder.Col(BObs));
            fg.Box(T(0f, 0f, 0.15f), 0.2f, 0.04f, 0.04f, MeshBuilder.Col(Fire));                     // articulación incandescente
            fg.Box(T(s * 0.1f, -0.55f, 0.14f), 0.035f, 0.5f, 0.035f, MeshBuilder.Col(Fire));
            Renderable("Antebrazo", elbow, SaveMesh("BossForearm" + side, fa.ToMesh("BossForearm" + side)), toon);
            Renderable("Vetas", elbow, SaveMesh("BossForearmGlow" + side, fg.ToMesh("BossForearmGlow" + side)), fire, false);

            var hand = new GameObject("Mano" + side).transform; hand.SetParent(elbow, false);
            hand.localPosition = new Vector3(s * 0.02f, -0.95f, 0.05f);
            var hd = new MeshBuilder(); var hg = new MeshBuilder();
            hd.Box(T(0f, -0.1f, 0.03f), 0.27f, 0.2f, 0.2f, MeshBuilder.Col(BObs3));                  // mano
            for (int k = 0; k < 4; k++)                                                              // garras con puntas encendidas
            {
                float dx = (k - 1.5f) * 0.075f, dz = k == 0 || k == 3 ? 0.02f : 0.1f;
                hd.Cone(T(dx, -0.18f, 0.07f + dz, 165f, 0f, dx * 60f), 4, 0.04f, 0.42f, MeshBuilder.Col(BObs));
                hg.Cone(T(dx * 1.1f, -0.52f, 0.15f + dz, 165f, 0f, dx * 60f), 4, 0.026f, 0.14f, MeshBuilder.Col(Fire));
            }
            Renderable("Garra", hand, SaveMesh("BossHand" + side, hd.ToMesh("BossHand" + side)), toon);
            Renderable("Puntas", hand, SaveMesh("BossHandGlow" + side, hg.ToMesh("BossHandGlow" + side)), fire, false);

            fx.Add(Wisp("HumoCodo" + side, elbow, Vector3.zero, 0.32f, 4f, 3f));
            fx.Add(Wisp("HumoMano" + side, hand, new Vector3(0f, -0.25f, 0.08f), 0.28f, 3f, 5f));
            return new[] { shoulder, elbow, hand };
        }

        static Matrix4x4 T(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1)
            => MeshBuilder.TRS(x, y, z, rx, ry, rz, sx, sy, sz);

        // =============================================================== JUGADOR, CÁMARA Y HUD
        static Transform BuildPlayerAndCamera()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MageBuilder.PrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(Spawn.x, Height(Spawn.x, Spawn.z) + 0.1f, Spawn.z);

            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.07f, 0.06f);
            cam.farClipPlane = 400f; cam.fieldOfView = 46f;
            var follow = camGo.AddComponent<IsoCameraFollow>(); follow.target = player.transform;
            BuildPostFx(cam);
            camGo.transform.position = player.transform.position + new Vector3(0f, 14f, -10f);
            camGo.transform.LookAt(player.transform.position);
            return player.transform;
        }

        static void BuildHud(Transform player)
        {
            var info = new GameObject("LevelInfo").AddComponent<LevelInfo>();
            info.levelId = LevelId; info.levelName = "Cráter de Obsidiana"; info.worldHalfSize = 30f;
            info.zones = new[]
            {
                new ZoneInfo { name = "Entrada del Cráter", position = new Vector2(Spawn.x, Spawn.z) },
                new ZoneInfo { name = "Altar de Obsidiana", position = new Vector2(Altar.x, Altar.z) },
            };

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var hud = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var menu = hud.AddComponent<MenuController>(); menu.styledMenu = false;
            var zh = hud.AddComponent<ZoneHUD>();

            Color navy = new Color(0.16f, 0.07f, 0.07f, 0.95f), ember = new Color(1f, 0.55f, 0.15f, 1f);
            var banner = Img("LevelBanner", hud.transform, navy);
            Anchor(banner.rectTransform, Vector2.one, Vector2.one, new Vector2(-12, -12), new Vector2(320, 70));
            var line = Img("EmberLine", banner.transform, ember);
            line.rectTransform.anchorMin = Vector2.zero; line.rectTransform.anchorMax = new Vector2(1, 0); line.rectTransform.pivot = new Vector2(.5f, 0);
            line.rectTransform.anchoredPosition = Vector2.zero; line.rectTransform.sizeDelta = new Vector2(0, 4);
            var lvl = UIText.Make(banner.transform, "Level", info.levelName.ToUpperInvariant(), 13, new Color(1f, 0.7f, 0.45f), TextAnchor.UpperRight);
            Fill(lvl.rectTransform, 8, 8, 12, 30);
            var zone = UIText.Make(banner.transform, "Zone", info.levelName, 22, Color.white, TextAnchor.LowerRight);
            zone.fontStyle = FontStyle.Bold; Fill(zone.rectTransform, 8, 28, 12, 10);

            var toastRt = new GameObject("ZoneToast", typeof(RectTransform)).GetComponent<RectTransform>(); toastRt.SetParent(hud.transform, false);
            var tg = toastRt.gameObject.AddComponent<CanvasGroup>(); tg.alpha = 0f; tg.blocksRaycasts = false;
            toastRt.anchorMin = toastRt.anchorMax = new Vector2(.5f, .74f); toastRt.sizeDelta = new Vector2(900, 90);
            var toast = UIText.Make(toastRt, "Text", "", 48, Color.white); toast.fontStyle = FontStyle.Bold; Fill(toast.rectTransform, 0, 0, 0, 0);
            var ol = toast.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, .55f); ol.effectDistance = new Vector2(2, -2);

            var hint = UIText.Make(hud.transform, "Hint", "WASD: caminar · Shift: correr · Espacio: saltar · Clic/Enter: hechizo · Q/E: girar · Esc: menú · ¡Evita la lava!", 14, Color.white, TextAnchor.UpperLeft);
            Anchor(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(900, 24));
            hint.rectTransform.pivot = new Vector2(0, 1);
            var hol = hint.gameObject.AddComponent<Outline>(); hol.effectColor = new Color(0, 0, 0, .6f); hol.effectDistance = new Vector2(1, -1);

            var menuBtnImg = Img("MenuButton", hud.transform, navy);
            Anchor(menuBtnImg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 14), new Vector2(150, 44));
            var menuBtn = menuBtnImg.gameObject.AddComponent<Button>(); menuBtn.targetGraphic = menuBtnImg;
            var ml = UIText.Make(menuBtnImg.transform, "Label", "Menú (Esc)", 16, Color.white); Fill(ml.rectTransform, 0, 0, 0, 0);
            UnityEventTools.AddStringPersistentListener(menuBtn.onClick, menu.LoadLevel, "MainMenu");

            zh.info = info; zh.player = player; zh.levelText = lvl; zh.zoneText = zone; zh.toastText = toast; zh.toastGroup = tg; zh.menuScene = "MainMenu";
        }

        static Image Img(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.color = c; return im;
        }
        static void Anchor(RectTransform rt, Vector2 amin, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = amin; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
        static void Fill(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        static void RegisterScene()
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == ScenePath)) return;
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // =============================================================== UTILIDADES DE MALLA
        static GameObject Renderable(string name, Transform parent, Mesh mesh, Material mat, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            if (!shadows) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            return go;
        }

        static Mesh Single(System.Action<MeshBuilder> build) { var b = new MeshBuilder(); build(b); return b.ToMesh("m"); }

        /// Disco plano de radio 1 (y = 0) con normales hacia arriba.
        static Mesh Disc(int sides)
        {
            var v = new List<Vector3> { Vector3.zero }; var t = new List<int>();
            for (int i = 0; i < sides; i++) { float a = i * Mathf.PI * 2f / sides; v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))); }
            for (int i = 0; i < sides; i++) { t.Add(0); t.Add(1 + (i + 1) % sides); t.Add(1 + i); }
            var m = new Mesh { name = "Disc" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// Guarda (o reemplaza) la malla como asset para que la escena la referencie.
        static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = Dir + "/" + name + ".asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// Anillos y glifos planos (runas). Por defecto en el plano XZ; con vertical = true en el plano XY.
        class RingBuilder
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<int> t = new List<int>();
            readonly bool vertical;
            public RingBuilder(bool vertical = false) { this.vertical = vertical; }

            Vector3 Pt(float a, float b) => vertical ? new Vector3(a, b, 0f) : new Vector3(a, 0f, b);

            void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int i = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
                // Dos caras para que se vea por ambos lados
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }

            public void Ring(Matrix4x4 m, float radius, float width, int seg)
            {
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                    float ri = radius - width * 0.5f, ro = radius + width * 0.5f;
                    AddQuad(m.MultiplyPoint3x4(Pt(Mathf.Cos(a0) * ri, Mathf.Sin(a0) * ri)), m.MultiplyPoint3x4(Pt(Mathf.Cos(a0) * ro, Mathf.Sin(a0) * ro)),
                            m.MultiplyPoint3x4(Pt(Mathf.Cos(a1) * ro, Mathf.Sin(a1) * ro)), m.MultiplyPoint3x4(Pt(Mathf.Cos(a1) * ri, Mathf.Sin(a1) * ri)));
                }
            }

            public void Quad(Matrix4x4 m, float w, float h)
            {
                AddQuad(m.MultiplyPoint3x4(Pt(-w / 2, -h / 2)), m.MultiplyPoint3x4(Pt(w / 2, -h / 2)), m.MultiplyPoint3x4(Pt(w / 2, h / 2)), m.MultiplyPoint3x4(Pt(-w / 2, h / 2)));
            }

            public void Tri(Matrix4x4 m, float w, float h)
            {
                Vector3 a = m.MultiplyPoint3x4(Pt(-w / 2, 0f)), b = m.MultiplyPoint3x4(Pt(w / 2, 0f)), c = m.MultiplyPoint3x4(Pt(0f, h));
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 1);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }
    }
}
#endif
