using UnityEngine;

namespace MageHero
{
    /// Proyectil en parábola (rocas del gólem): marca en el suelo dónde caerá, vuela en arco y al aterrizar
    /// hace daño en área, explota y tiembla la cámara si el mago está cerca.
    public class ArcProjectile : MonoBehaviour
    {
        Vector3 start, target;
        float flight, t, radius, damage, height;
        Transform marker;
        Material markerMat;
        Color fxColor;
        Vector3 spin;

        public static ArcProjectile Launch(GameObject visual, Vector3 from, Vector3 to, float flightTime, float arcHeight, float damage, float radius, Color fxColor)
        {
            var a = visual.AddComponent<ArcProjectile>();
            a.start = from; a.target = to; a.flight = Mathf.Max(0.3f, flightTime); a.height = arcHeight;
            a.damage = damage; a.radius = radius; a.fxColor = fxColor;
            a.spin = Random.onUnitSphere * 360f;
            visual.transform.position = from;
            // Marca roja en el suelo: avisa al jugador para que esquive.
            a.markerMat = RuntimeMaterials.GlowTransparent(new Color(1f, 0.25f, 0.15f, 0.35f));
            var m = RuntimeMaterials.Prim(PrimitiveType.Cylinder, "MarcaImpacto", null, to + Vector3.up * 0.06f, new Vector3(0.2f, 0.01f, 0.2f), a.markerMat, false);
            a.marker = m.transform;
            return a;
        }

        void Update()
        {
            t += Time.deltaTime / flight;
            float k = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(start, target, k) + Vector3.up * (4f * height * k * (1f - k));
            transform.position = p;
            transform.Rotate(spin * Time.deltaTime, Space.World);
            if (marker != null)
            {
                float d = radius * 2f * Mathf.Lerp(0.3f, 1f, k);
                marker.localScale = new Vector3(d, 0.01f, d);
                var c = markerMat.GetColor("_BaseColor"); c.a = 0.25f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 10f)); markerMat.SetColor("_BaseColor", c);
            }
            if (k >= 1f) Land();
        }

        void Land()
        {
            HitFX.Explosion(target + Vector3.up * 0.4f, 1.3f, fxColor);
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var hs = player.GetComponentInParent<HealthSystem>();
                float d = Vector3.Distance(player.transform.position, target);
                if (hs != null && d <= radius) hs.TakeDamage(damage);
                if (d < radius * 3f) CameraShake3D.Shake(0.3f, 0.35f);
            }
            if (marker != null) Destroy(marker.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy() { if (marker != null) Destroy(marker.gameObject); }
    }
}
