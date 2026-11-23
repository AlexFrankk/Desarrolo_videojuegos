using UnityEngine;

namespace MageHero
{
    /// Utilidades compartidas por los jefes construidos por código (gólem y dragones).
    public static class BossKit
    {
        /// Pieza (primitiva sin collider) colgada de 'parent'.
        public static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = RuntimeMaterials.Prim(type, name, parent, pos, scale, mat);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        /// Pivote vacío (articulación) para animar brazos, piernas, alas…
        public static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// Bola de fuego en línea recta (usa Projectile3D: daña al mago, explota al chocar).
        public static void Fireball(Vector3 from, Vector3 dir, float speed, float damage, Color core, Color glow, float size = 0.7f)
        {
            var go = RuntimeMaterials.Prim(PrimitiveType.Sphere, "BolaDeFuego", null, from, Vector3.one * size, RuntimeMaterials.Glow(core), false);
            RuntimeMaterials.Prim(PrimitiveType.Sphere, "Halo", go.transform, Vector3.zero, Vector3.one * 1.5f, RuntimeMaterials.GlowTransparent(new Color(glow.r, glow.g, glow.b, 0.35f)), false);
            var col = go.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.7f;
            go.AddComponent<Rigidbody>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.35f; trail.startWidth = size * 1.2f; trail.endWidth = 0f;
            trail.sharedMaterial = RuntimeMaterials.Glow(glow); trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = glow; l.range = 5f; l.intensity = 2.5f; l.shadows = LightShadows.None;
            var p = go.AddComponent<Projectile3D>();
            p.speed = speed; p.damage = damage; p.lifetime = 4f;
            p.Launch(dir, Team.Enemy);
        }

        /// Dónde estará el mago dentro de 'seconds' (para apuntar por delante si se mueve).
        public static Vector3 Predict(Transform player, float seconds)
        {
            var cc = player.GetComponent<CharacterController>();
            Vector3 v = cc != null ? new Vector3(cc.velocity.x, 0f, cc.velocity.z) : Vector3.zero;
            return player.position + v * seconds;
        }

        public static bool LineOfSight(Vector3 from, Vector3 to)
        {
            int obs = GameLayers.Exists(GameLayers.Obstacles) ? GameLayers.Mask(GameLayers.Obstacles) : 0;
            return obs == 0 || !Physics.Linecast(from, to, obs, QueryTriggerInteraction.Ignore);
        }
    }
}
