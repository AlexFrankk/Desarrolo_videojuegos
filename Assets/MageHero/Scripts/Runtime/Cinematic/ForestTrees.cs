using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Árboles del nivel (World/Trees) que el Gólem de Piedra derriba a su paso.
    /// El gólem no tiene físicas (se mueve por código), así que sin esto atravesaría los troncos:
    /// en su lugar, cualquier árbol que quede en su camino cae hacia un lado con polvo y temblor.
    public static class ForestTrees
    {
        static readonly HashSet<Transform> toppled = new HashSet<Transform>();
        static ForestTreesHost host;

        /// Derriba el árbol en una corrutina propia (sigue aunque el gólem que lo empujó desaparezca).
        public static void StartTopple(Transform tree, Vector3 away, float seconds = 1.0f, float delay = 0f)
        {
            if (tree == null || toppled.Contains(tree)) return;
            if (host == null) host = new GameObject("ForestTreesHost").AddComponent<ForestTreesHost>();
            host.StartCoroutine(Topple(tree, away, seconds, delay));
        }

        public static List<Transform> All()
        {
            var list = new List<Transform>();
            var trees = GameObject.Find("World/Trees");
            if (trees == null) return list;
            foreach (Transform c in trees.transform) if (c.gameObject.activeInHierarchy) list.Add(c);
            return list;
        }

        public static bool IsToppled(Transform t) => t != null && toppled.Contains(t);
        public static void MarkToppled(Transform t) { if (t != null) toppled.Add(t); }

        /// Árboles en pie cuyo tronco queda a menos de <paramref name="radius"/> del segmento a→b (en planta).
        public static List<Transform> NearSegment(Vector3 a, Vector3 b, float radius, Transform except = null)
        {
            toppled.RemoveWhere(t => t == null);
            var result = new List<Transform>();
            foreach (var t in All())
            {
                if (t == except || toppled.Contains(t)) continue;
                if (PlanarDistanceToSegment(t.position, a, b) < radius) result.Add(t);
            }
            return result;
        }

        public static float PlanarDistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
            Vector2 d = B - A;
            float t = d.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(P - A, d) / d.sqrMagnitude);
            return Vector2.Distance(P, A + d * t);
        }

        /// Dirección lateral (perpendicular al camino a→b) hacia el lado donde está el árbol: hacia ahí cae.
        public static Vector3 AwayFromPath(Transform tree, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a; dir.y = 0;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 rel = tree.position - a; rel.y = 0;
            return Vector3.Dot(rel, side) >= 0f ? side : -side;
        }

        /// Derriba el árbol hacia <paramref name="away"/>: tiembla, cae cada vez más rápido, rebota y levanta polvo.
        public static IEnumerator Topple(Transform tree, Vector3 away, float seconds = 1.0f, float delay = 0f)
        {
            if (tree == null || toppled.Contains(tree)) yield break;
            toppled.Add(tree);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            away.y = 0; if (away.sqrMagnitude < 1e-4f) away = Vector3.right; away.Normalize();
            Vector3 axis = Vector3.Cross(Vector3.up, away);           // girar sobre este eje inclina la copa hacia "away"
            Vector3 p0 = tree.position; Quaternion r0 = tree.rotation;
            for (float t = 0; t < 0.35f && tree != null; t += Time.deltaTime)
            {
                tree.position = p0 + new Vector3(Mathf.Sin(t * 60f) * 0.05f, 0, Mathf.Cos(t * 47f) * 0.04f);
                yield return null;
            }
            if (tree == null) yield break;
            tree.position = p0;
            const float Angle = 84f;
            for (float t = 0; t < 1f && tree != null; t += Time.deltaTime / seconds)
            {
                tree.rotation = Quaternion.AngleAxis(Angle * t * t * t, axis) * r0;
                yield return null;
            }
            if (tree == null) yield break;
            tree.rotation = Quaternion.AngleAxis(Angle, axis) * r0;
            CameraShake3D.Shake(0.22f, 0.4f);
            Dust(p0 + away * 3f + Vector3.up * 0.4f, 45, 2f);
            Dust(p0 + away * 1.3f + Vector3.up * 0.4f, 25, 1.4f);
            for (float t = 0; t < 1f && tree != null; t += Time.deltaTime / 0.35f)
            {
                tree.rotation = Quaternion.AngleAxis(Angle - Mathf.Sin(t * Mathf.PI) * 4f, axis) * r0;
                yield return null;
            }
        }

        /// Deja el árbol ya caído (al saltar la cinemática).
        public static void ToppleInstant(Transform tree, Vector3 away)
        {
            if (tree == null || toppled.Contains(tree)) return;
            toppled.Add(tree);
            away.y = 0; if (away.sqrMagnitude < 1e-4f) away = Vector3.right; away.Normalize();
            tree.rotation = Quaternion.AngleAxis(84f, Vector3.Cross(Vector3.up, away)) * tree.rotation;
        }

        static void Dust(Vector3 at, int count, float size)
        {
            var ps = CineGlow.Motes("Polvo", null, at, new Color(0.42f, 0.36f, 0.28f), 0f, size, 2.4f, 2.4f, 0.8f, -0.01f);
            ps.Emit(count);
            Object.Destroy(ps.gameObject, 3f);
        }
    }

    /// Se añade al Gólem mientras camina fuera de un combate: derriba los árboles que tiene justo delante
    /// (en vez de atravesarlos) hacia el lado contrario a él.
    public class TreeBreaker : MonoBehaviour
    {
        public float radius = 1.7f;
        float nextCheck;

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.08f;
            Vector3 p = transform.position;
            Vector3 ahead = p + transform.forward * 0.9f;
            foreach (var tree in ForestTrees.NearSegment(p, ahead, radius))
            {
                Vector3 away = tree.position - p; away.y = 0;
                // que caiga hacia delante y hacia fuera, nunca encima del gólem
                away = (away.normalized + transform.forward * 0.6f).normalized;
                ForestTrees.StartTopple(tree, away, 0.8f);
            }
        }
    }

    class ForestTreesHost : MonoBehaviour { }
}
