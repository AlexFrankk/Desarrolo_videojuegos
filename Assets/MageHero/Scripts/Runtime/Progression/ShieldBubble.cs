using UnityEngine;
using UnityEngine.UI;

namespace MageHero
{
    /// Burbuja azul translúcida alrededor del mago mientras tiene escudo, más un indicador en el HUD
    /// ("ESCUDO 45 · 12 s"). Destella al absorber un golpe y se apaga cuando el escudo se agota o caduca.
    [RequireComponent(typeof(HealthSystem))]
    public class ShieldBubble : MonoBehaviour
    {
        HealthSystem health;
        Transform bubble;
        Material mat;
        Text hudText;
        Image hudFill;
        GameObject hud;
        float pulse;
        float lastShield;

        void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.ShieldChanged += OnShieldChanged;
            mat = RuntimeMaterials.GlowTransparent(new Color(0.35f, 0.7f, 1f, 0.22f));
            bubble = RuntimeMaterials.Prim(PrimitiveType.Sphere, "BurbujaEscudo", transform, Vector3.up * 1.0f, Vector3.one * 2.3f, mat, false).transform;
            BuildHud();
            lastShield = health.Shield;
        }

        void OnDestroy() { if (health != null) health.ShieldChanged -= OnShieldChanged; if (hud != null) Destroy(hud); }

        void OnShieldChanged(HealthSystem hs)
        {
            if (hs.Shield < lastShield) { pulse = 1f; HitFX.Spark(transform.position + Vector3.up, new Color(0.45f, 0.8f, 1f), 0.8f); }
            lastShield = hs.Shield;
        }

        void Update()
        {
            bool on = health.Shield > 0.01f && !health.IsDead;
            if (bubble.gameObject.activeSelf != on) bubble.gameObject.SetActive(on);
            bool hudOn = on && !(UIManager.Instance != null && UIManager.Instance.InCinematic);
            if (hud != null && hud.activeSelf != hudOn) hud.SetActive(hudOn);
            if (!on) return;
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 3f);
            float s = 2.3f + Mathf.Sin(Time.time * 3f) * 0.05f + pulse * 0.4f;
            bubble.localScale = Vector3.one * s;
            bubble.Rotate(0f, 30f * Time.deltaTime, 0f);
            var c = mat.GetColor("_BaseColor"); c.a = 0.18f + pulse * 0.35f + (health.ShieldTimeLeft < 4f ? Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.15f : 0f);
            mat.SetColor("_BaseColor", c);
            if (hudText != null)
            {
                hudText.text = $"ESCUDO  {Mathf.CeilToInt(health.Shield)}  ·  {Mathf.CeilToInt(health.ShieldTimeLeft)} s";
                hudFill.rectTransform.anchorMax = new Vector2(health.ShieldMax > 0f ? health.Shield / health.ShieldMax : 0f, 1f);
            }
        }

        void BuildHud()
        {
            var canvas = GameObject.Find("HUD");
            if (canvas == null) return;
            hud = new GameObject("ShieldPanel", typeof(RectTransform), typeof(Image));
            hud.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)hud.transform; rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(352f, 78f); rt.sizeDelta = new Vector2(250f, 30f);
            var bg = hud.GetComponent<Image>(); bg.color = new Color(0.06f, 0.14f, 0.28f, 0.92f); bg.raycastTarget = false;
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(hud.transform, false);
            hudFill = fillGo.GetComponent<Image>(); hudFill.color = new Color(0.3f, 0.65f, 1f, 0.85f); hudFill.raycastTarget = false;
            var frt = hudFill.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
            hudText = UIText.Make(hud.transform, "Text", "", 15, Color.white);
            hudText.fontStyle = FontStyle.Bold;
            var trt = hudText.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            hud.SetActive(false);
        }
    }
}
