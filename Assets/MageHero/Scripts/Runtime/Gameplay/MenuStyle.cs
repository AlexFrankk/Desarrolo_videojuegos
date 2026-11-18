using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MageHero
{
    /// Gráficos del menú generados por código (sin imágenes externas):
    /// botones redondeados con brillo, círculos, cinta del título y fondo con manchas.
    public static class MenuStyle
    {
        public static readonly Color Orange = Hex(0xff7a1a), OrangeDark = Hex(0xc4470c);
        public static readonly Color Red = Hex(0xf0533a), RedDark = Hex(0xa82a1a);
        public static readonly Color Green = Hex(0x8fd63a), GreenDark = Hex(0x4f9a1c);
        public static readonly Color Purple = Hex(0xc23ad6), PurpleDark = Hex(0x7a1a8f);
        public static readonly Color Blue = Hex(0x3aa7e8), BlueDark = Hex(0x1a6aa8);
        public static readonly Color Panel = new Color(0.10f, 0.13f, 0.17f, 0.88f);

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        public static Color Darker(Color c, float k = 0.62f) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        // ------------------------------------------------------------------ Sprites
        /// Rectángulo redondeado con degradado vertical y borde. Se usa como sprite 9-slice.
        public static Sprite RoundedRect(Color top, Color bottom, Color border, int radius = 26, int borderWidth = 4, int w = 128, int h = 96)
        {
            var tex = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = RoundedDist(x + 0.5f, y + 0.5f, w, h, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    Color c = Color.Lerp(bottom, top, (float)y / (h - 1));
                    if (d > -borderWidth) c = Color.Lerp(c, border, Mathf.Clamp01(d + borderWidth + 0.5f));
                    c.a *= a;
                    px[y * w + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            float b = radius + borderWidth;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// Brillo blanco (más opaco arriba) para la mitad superior de un botón.
        public static Sprite Gloss(int radius = 20, int w = 96, int h = 48)
        {
            var tex = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = RoundedDist(x + 0.5f, y + 0.5f, w, h, radius);
                    float a = Mathf.Clamp01(0.5f - d) * Mathf.Lerp(0.05f, 0.55f, (float)y / (h - 1));
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        public static Sprite Circle(Color top, Color bottom, Color border, int size = 128, int borderWidth = 6)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) - r;
                    Color c = Color.Lerp(bottom, top, (float)y / (size - 1));
                    if (d > -borderWidth) c = Color.Lerp(c, border, Mathf.Clamp01(d + borderWidth + 0.5f));
                    // brillo en la parte superior
                    float gy = (y - size * 0.55f) / (size * 0.45f), gx = (x - r) / (r * 0.75f);
                    if (gy > 0f && gx * gx + (gy - 0.5f) * (gy - 0.5f) * 4f < 1f) c = Color.Lerp(c, Color.white, 0.35f * gy);
                    c.a *= Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        /// Cinta (banner) del título: banda con borde ondulado y extremos en punta de cola.
        public static Sprite Ribbon(Color fill, Color border, int w = 512, int h = 128)
        {
            var tex = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float wave = Mathf.Sin(u * Mathf.PI * 2f) * 0.06f;           // banda ligeramente ondulada
                    float top = 0.92f + wave, bottom = 0.10f + wave;
                    float mid = (top + bottom) * 0.5f, half = (top - bottom) * 0.5f;
                    float notch = 34f * Mathf.Clamp01(1f - Mathf.Abs(v - mid) / half);  // corte en V en los extremos (cola)
                    float distTop = (top - v) * h, distBot = (v - bottom) * h;
                    float distSide = Mathf.Min(x + 0.5f, w - x - 0.5f) - notch;
                    float d = Mathf.Min(Mathf.Min(distTop, distBot), distSide);
                    Color c = Color.Lerp(Darker(fill, 0.85f), fill, Mathf.InverseLerp(bottom, top, v));
                    if (d < 7f) c = Color.Lerp(border, c, Mathf.Clamp01(d - 6f));
                    c.a = Mathf.Clamp01(d + 0.5f);
                    px[y * w + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
        }

        /// Fondo gris pizarra con manchas redondas y viñeta, parecido a una pizarra o piedra.
        public static Texture2D Background(int w = 640, int h = 360, int seed = 7)
        {
            var tex = NewTex(w, h);
            var px = new Color[w * h];
            var rnd = new System.Random(seed);
            Color baseC = new Color(0.27f, 0.33f, 0.37f), dark = new Color(0.12f, 0.15f, 0.18f);
            for (int i = 0; i < px.Length; i++) px[i] = baseC;

            for (int n = 0; n < 70; n++)                                         // manchas
            {
                float cx = (float)rnd.NextDouble() * w, cy = (float)rnd.NextDouble() * h;
                float r = 6f + (float)rnd.NextDouble() * 34f, k = 0.06f + (float)rnd.NextDouble() * 0.12f;
                int x0 = Mathf.Max(0, (int)(cx - r - 2)), x1 = Mathf.Min(w - 1, (int)(cx + r + 2));
                int y0 = Mathf.Max(0, (int)(cy - r - 2)), y1 = Mathf.Min(h - 1, (int)(cy + r + 2));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = Mathf.Clamp01((r - d) / 3f) * k;
                        if (d > r - 2.5f && d < r + 1f) a += 0.05f;              // borde marcado
                        px[y * w + x] = Color.Lerp(px[y * w + x], dark, a);
                    }
            }
            for (int y = 0; y < h; y++)                                          // viñeta + grano
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w - 0.5f, v = y / (float)h - 0.5f;
                    float vig = Mathf.Clamp01((u * u + v * v) * 2.2f);
                    float grain = ((float)rnd.NextDouble() - 0.5f) * 0.035f;
                    Color c = Color.Lerp(px[y * w + x], dark, vig * 0.75f);
                    px[y * w + x] = new Color(c.r + grain, c.g + grain, c.b + grain, 1f);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ Widgets
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color tint)
        {
            var rt = Rect(name, parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite; im.color = tint; im.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) im.type = UnityEngine.UI.Image.Type.Sliced;
            return im;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Color outline, bool bold = true, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var t = UIText.Make(parent, "Label", text, size, color, anchor);
            if (bold) t.fontStyle = FontStyle.Bold;
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = outline; o.effectDistance = new Vector2(2f, -2f);
            var s = t.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .45f); s.effectDistance = new Vector2(0f, -4f);
            return t;
        }

        /// Botón redondeado con brillo, sombra, rebote al pasar el ratón y texto con contorno.
        public static Button GlossyButton(string name, Transform parent, string text, Color color, int fontSize, System.Action onClick)
        {
            Color dark = Darker(color);
            var img = Image(name, parent, RoundedRect(Color.Lerp(color, Color.white, 0.18f), Darker(color, 0.85f), dark), Color.white);
            img.raycastTarget = true;
            var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .5f); sh.effectDistance = new Vector2(0, -6);

            var gloss = Image("Gloss", img.transform, Gloss(), Color.white);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f); gloss.rectTransform.anchorMax = new Vector2(1f, 1f);
            gloss.rectTransform.offsetMin = new Vector2(10f, 0f); gloss.rectTransform.offsetMax = new Vector2(-10f, -7f);

            var label = Label(img.transform, text, fontSize, Color.white, dark);
            Stretch(label.rectTransform);

            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.0f, 1.0f, 1.0f); colors.pressedColor = new Color(.82f, .82f, .82f);
            colors.selectedColor = Color.white; colors.fadeDuration = 0.08f;
            b.colors = colors;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<ButtonBounce>();
            return b;
        }

        public static Button RoundButton(string name, Transform parent, string symbol, Color color, int size, System.Action onClick)
        {
            var img = Image(name, parent, Circle(Color.Lerp(color, Color.white, 0.2f), Darker(color, 0.8f), Color.white), Color.white);
            img.raycastTarget = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .5f); sh.effectDistance = new Vector2(0, -5);
            var label = Label(img.transform, symbol, (int)(size * 0.5f), Color.white, Darker(color));
            Stretch(label.rectTransform);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<ButtonBounce>();
            return b;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ------------------------------------------------------------------ Internos
        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static float RoundedDist(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }

    /// Rebote suave del botón al pasar el ratón y al pulsarlo (funciona aunque el juego esté en pausa).
    public class ButtonBounce : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.07f, pressScale = 0.95f;
        float target = 1f;
        bool over;

        public void OnPointerEnter(PointerEventData e) { over = true; target = hoverScale; }
        public void OnPointerExit(PointerEventData e) { over = false; target = 1f; }
        public void OnPointerDown(PointerEventData e) { target = pressScale; }
        public void OnPointerUp(PointerEventData e) { target = over ? hoverScale : 1f; }
        void OnDisable() { over = false; target = 1f; transform.localScale = Vector3.one; }

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
