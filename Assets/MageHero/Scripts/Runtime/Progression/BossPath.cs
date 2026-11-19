using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Camino iluminado hacia el jefe final de la zona.
    /// Sigue una de las rutas del nivel (LevelData): la que cruza el puente si existe (en Primavera, la
    /// ruta S que lleva al Sendero de Adoquines tras el puente que derrumba el Gran Cero); si no, la más larga.
    /// Coloca faroles a ambos lados (dorados al principio, rojo-violeta al acercarse al jefe), una arena
    /// de piedras con antorchas al final y registra la ruta para dibujarla en el mapa (MapOverlay, tecla M).
    public class BossPath : MonoBehaviour
    {
        /// Puntos (x, z) del camino para el mapa, el jefe y un número de versión para redibujar.
        public static Vector2[] MapRoute { get; private set; }
        public static Vector3 BossPosition { get; private set; }
        public static string BossName { get; private set; }
        public static int Version { get; private set; }

        public float lanternSpacing = 7f;
        public float arenaRadius = 8f;

        public static void BuildForCurrentLevel()
        {
            if (FindAnyObjectByType<BossPath>() != null) return;
            var info = FindAnyObjectByType<LevelInfo>();
            if (info == null) return;
            LevelDef def = null;
            foreach (var d in LevelData.All()) if (d.id == info.levelId) def = d;
            if (def == null || def.routes == null || def.routes.Length == 0) return;

            World.Configure(def);                                   // calcula las rutas suavizadas (spline) del nivel
            Route route = null;
            if (def.bridgeRoutes != null && def.bridgeRoutes.Length > 0 && def.bridgeRoutes[0] < World.RT.Count) route = World.RT[def.bridgeRoutes[0]];
            else foreach (var r in World.RT) if (route == null || Length(r.q) > Length(route.q)) route = r;
            if (route == null || route.q.Length < 2) return;

            // Coordenadas del nivel → Unity (z invertida, como hace LevelBuilder).
            var pts = new List<Vector3>();
            foreach (var q in route.q) pts.Add(new Vector3(q.x, 0f, -q.y));
            var player = GameObject.FindWithTag("Player");
            if (player != null && Flat(pts[pts.Count - 1] - player.transform.position) < Flat(pts[0] - player.transform.position)) pts.Reverse();

            BossName = info.levelId == "primavera" ? "El Gran Cero" : "Guardián del Caos";
            var go = new GameObject("CaminoAlJefe");
            go.AddComponent<BossPath>().Build(pts, route.w);
        }

        void Build(List<Vector3> pts, float width)
        {
            float total = 0f;
            for (int i = 1; i < pts.Count; i++) total += Flat(pts[i] - pts[i - 1]);
            float bossAt = Mathf.Max(total * 0.6f, total - 8f);    // el jefe se coloca un poco antes del final de la ruta
            Vector3 arena = Sample(pts, bossAt, out _);
            if (RuntimeMaterials.Ground(arena, out var ga)) arena = ga;
            BossPosition = arena;

            var post = RuntimeMaterials.Toon(new Color(0.32f, 0.22f, 0.15f));
            int n = 0;
            for (float d = 6f; d < bossAt - arenaRadius; d += lanternSpacing, n++)
            {
                Vector3 c = Sample(pts, d, out Vector3 dir);
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * (width * 0.5f + 1.1f) * (n % 2 == 0 ? 1f : -1f);
                if (!RuntimeMaterials.Ground(c + side, out Vector3 p)) continue;          // agua, puente u obstáculo: se salta
                float k = d / bossAt;
                var orbCol = Color.Lerp(new Color(1f, 0.82f, 0.35f), new Color(0.85f, 0.2f, 0.75f), k);
                Lantern(p, post, RuntimeMaterials.Glow(orbCol), orbCol, n % 3 == 0);
            }
            MapRoute = BuildMapRoute(pts, bossAt);

            BuildArena(arena, post);
            FinalBoss.Spawn(arena, BossName);
            Version++;
        }

        static Vector2[] BuildMapRoute(List<Vector3> pts, float upTo)
        {
            var list = new List<Vector2>();
            for (float d = 0f; d <= upTo; d += 3f) { var c = Sample(pts, d, out _); list.Add(new Vector2(c.x, c.z)); }
            return list.ToArray();
        }

        void Lantern(Vector3 ground, Material postMat, Material orbMat, Color lightCol, bool withLight)
        {
            var root = new GameObject("Farol").transform;
            root.SetParent(transform, false);
            root.position = ground;
            RuntimeMaterials.Prim(PrimitiveType.Cylinder, "Poste", root, new Vector3(0f, 0.9f, 0f), new Vector3(0.14f, 0.9f, 0.14f), postMat);
            RuntimeMaterials.Prim(PrimitiveType.Cube, "Brazo", root, new Vector3(0f, 1.8f, 0f), new Vector3(0.5f, 0.08f, 0.08f), postMat);
            RuntimeMaterials.Prim(PrimitiveType.Sphere, "Luz", root, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.36f, orbMat, false);
            if (!withLight) return;
            var l = new GameObject("PointLight").AddComponent<Light>();
            l.transform.SetParent(root, false); l.transform.localPosition = new Vector3(0f, 2f, 0f);
            l.type = LightType.Point; l.color = lightCol; l.range = 6f; l.intensity = 1.6f; l.shadows = LightShadows.None;
        }

        void BuildArena(Vector3 center, Material postMat)
        {
            var root = new GameObject("ArenaDelJefe").transform;
            root.SetParent(transform, false);
            root.position = center;
            var stone = RuntimeMaterials.Toon(new Color(0.22f, 0.18f, 0.26f));
            var fire = RuntimeMaterials.Glow(new Color(1f, 0.25f, 0.2f));
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * arenaRadius;
                if (!RuntimeMaterials.Ground(p, out p)) continue;
                var s = RuntimeMaterials.Prim(PrimitiveType.Cube, "Piedra", root, Vector3.zero, new Vector3(0.7f, 1.2f + (i % 3) * 0.5f, 0.7f), stone);
                s.transform.position = p + Vector3.up * 0.5f;
                s.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 90f), Random.Range(-8f, 8f));
                if (i % 3 != 0) continue;
                var torch = RuntimeMaterials.Prim(PrimitiveType.Cylinder, "Antorcha", root, Vector3.zero, new Vector3(0.12f, 1.1f, 0.12f), postMat);
                torch.transform.position = p + Vector3.up * 1.1f + (center - p).normalized * 1.1f;
                RuntimeMaterials.Prim(PrimitiveType.Sphere, "Fuego", torch.transform, new Vector3(0f, 1.1f, 0f), new Vector3(3.5f, 0.35f, 3.5f), fire, false);
            }
            var l = new GameObject("LuzArena").AddComponent<Light>();
            l.transform.SetParent(root, false); l.transform.localPosition = Vector3.up * 4f;
            l.type = LightType.Point; l.color = new Color(1f, 0.2f, 0.25f); l.range = arenaRadius * 2.2f; l.intensity = 2.2f; l.shadows = LightShadows.None;
        }

        static Vector3 Sample(List<Vector3> pts, float dist, out Vector3 dir)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Flat(pts[i] - pts[i - 1]);
                if (dist <= seg || i == pts.Count - 1)
                {
                    dir = pts[i] - pts[i - 1]; dir.y = 0f; dir.Normalize();
                    return Vector3.Lerp(pts[i - 1], pts[i], seg > 0f ? Mathf.Clamp01(dist / seg) : 0f);
                }
                dist -= seg;
            }
            dir = Vector3.forward;
            return pts[pts.Count - 1];
        }

        static float Length(Vector2[] q) { float s = 0f; for (int i = 1; i < q.Length; i++) s += Vector2.Distance(q[i], q[i - 1]); return s; }
        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        void OnDestroy() { MapRoute = null; Version++; }
    }
}
