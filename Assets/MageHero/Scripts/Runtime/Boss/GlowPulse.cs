using UnityEngine;

namespace MageHero
{
    /// Hace latir el color de un material brillante (lava, runas) sin crear materiales nuevos.
    public class GlowPulse : MonoBehaviour
    {
        public Color colorA = new Color(1f, 0.35f, 0.05f), colorB = new Color(1f, 0.7f, 0.2f);
        public float speed = 1.2f;
        public Light pairedLight;
        public float lightMin = 1.5f, lightMax = 3f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Renderer rend; MaterialPropertyBlock mpb; float phase;

        void Awake() { rend = GetComponent<Renderer>(); mpb = new MaterialPropertyBlock(); phase = Random.value * 10f; }

        void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * speed);
            if (rend != null)
            {
                rend.GetPropertyBlock(mpb); mpb.SetColor(BaseColorId, Color.Lerp(colorA, colorB, k)); rend.SetPropertyBlock(mpb);
            }
            if (pairedLight != null) pairedLight.intensity = Mathf.Lerp(lightMin, lightMax, k);
        }
    }
}
