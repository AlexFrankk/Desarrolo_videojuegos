using UnityEngine;
using UnityEngine.UI;

namespace MageHero
{
    /// Aplica el estilo del menú (paneles redondeados, cintas, botones brillantes) al HUD que viene guardado
    /// en las escenas de los niveles: cuadro de nivel/zona, línea de ayuda, aviso de zona y botones Mapa / Pausa.
    /// Lo llama CombatBootstrap al cargar cada nivel, así no hace falta regenerar las escenas.
    public static class HudTheme
    {
        public static void Apply()
        {
            var hud = GameObject.Find("HUD");
            if (hud == null || hud.GetComponent<Canvas>() == null) return;
            var info = Object.FindAnyObjectByType<LevelInfo>();
            Color accent = MenuStyle.LevelAccent(info != null ? info.levelId : "");
            var t = hud.transform;

            StyleLevelBanner(t.Find("LevelBanner"), accent);
            StyleHint(t, t.Find("Hint") ? t.Find("Hint").GetComponent<Text>() : null);
            StyleToast(t.Find("ZoneToast"), accent);
            ReplaceButtons(t, accent);
        }

        static void StyleLevelBanner(Transform banner, Color accent)
        {
            if (banner == null) return;
            var img = banner.GetComponent<Image>();
            if (img != null) { img.sprite = MenuStyle.HudSprite(accent); img.type = Image.Type.Sliced; img.color = Color.white; }
            foreach (var n in new[] { "GoldLine", "EmberLine" }) { var l = banner.Find(n); if (l != null) Object.Destroy(l.gameObject); }
            if (banner.GetComponent<Shadow>() == null) { var s = banner.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .45f); s.effectDistance = new Vector2(0, -5); }

            var lvl = banner.Find("Level") ? banner.Find("Level").GetComponent<Text>() : null;
            if (lvl != null) { lvl.color = Color.Lerp(accent, Color.white, 0.35f); lvl.fontStyle = FontStyle.Bold; Outline(lvl, new Color(0, 0, 0, .6f), 1f); }
            var zone = banner.Find("Zone") ? banner.Find("Zone").GetComponent<Text>() : null;
            if (zone != null) { zone.fontSize = 24; Outline(zone, MenuStyle.Darker(accent, 0.45f), 2f); }
        }

        static void StyleHint(Transform hud, Text hint)
        {
            if (hint == null) return;
            hint.text = hint.text.Replace("Esc: menú", "Esc: pausa");    // textos guardados en escenas antiguas
            if (!hint.text.Contains("Esc")) hint.text += " · Esc: pausa";
            hint.color = MenuStyle.Cream;
            if (hud.Find("HintBg") != null) return;
            var bg = MenuStyle.Image("HintBg", hud, MenuStyle.HudSprite(new Color(1f, 1f, 1f, 0.25f)), new Color(1f, 1f, 1f, 0.85f));
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(4f, -5f);
            rt.sizeDelta = new Vector2(hint.preferredWidth + 18f, 32f);
            bg.transform.SetSiblingIndex(hint.transform.GetSiblingIndex());   // detrás del texto
        }

        static void StyleToast(Transform toast, Color accent)
        {
            if (toast == null) return;
            var text = toast.GetComponentInChildren<Text>();
            if (text == null) return;
            Outline(text, MenuStyle.Darker(accent, 0.45f), 3f);
            if (text.GetComponent<Shadow>() == null || text.GetComponents<Shadow>().Length < 2)
            { var s = text.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .5f); s.effectDistance = new Vector2(0, -5); }
        }

        /// Sustituye los botones planos del HUD por botones con el estilo del menú.
        /// "Menú (Esc)" pasa a ser "Pausa (Esc)": abre el menú de pausa en vez de salir al inicio.
        static void ReplaceButtons(Transform hud, Color accent)
        {
            var oldMenu = hud.Find("MenuButton"); if (oldMenu != null) Object.Destroy(oldMenu.gameObject);
            var oldMap = hud.Find("MapButton"); if (oldMap != null) Object.Destroy(oldMap.gameObject);

            var pause = MenuStyle.TintButton("PauseButton", hud, "Pausa (Esc)", MenuStyle.Red, 18, () => PauseMenu.Instance?.Open());
            Corner(pause, new Vector2(-14f, 14f));

            var map = Object.FindAnyObjectByType<MapOverlay>();
            if (map != null && map.panel != null)
            {
                var mb = MenuStyle.TintButton("MapButton", hud, "Mapa (M)", accent, 18, map.Toggle);
                Corner(mb, new Vector2(-14f, 66f));
            }
        }

        static void Corner(Button b, Vector2 pos)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160f, 46f);
        }

        static void Outline(Text t, Color c, float d)
        {
            // Estilo low poly: una sola sombra hacia abajo (contorno + sombra hacía las letras dobles y borrosas).
            foreach (var old in t.GetComponents<UnityEngine.UI.Outline>()) Object.Destroy(old);
            var sh = t.GetComponent<Shadow>();
            if (sh == null || sh is UnityEngine.UI.Outline) sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, Mathf.Min(0.75f, c.a)); sh.effectDistance = new Vector2(0f, -Mathf.Max(1.5f, d));
        }
    }
}
