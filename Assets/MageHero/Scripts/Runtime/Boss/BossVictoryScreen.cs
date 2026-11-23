using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Pantalla de victoria al derrotar al jefe: repetir el combate o volver al menú.
    public static class BossVictoryScreen
    {
        public static void Show(string bossName)
        {
            var ui = UIManager.Instance;
            ui?.SetPlayerLocked(true);
            var hud = GameObject.Find("HUD");
            Transform parent = hud != null ? hud.transform : null;
            if (parent == null)
            {
                var c = new GameObject("VictoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                c.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; c.GetComponent<Canvas>().sortingOrder = 50;
                var sc = c.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720);
                parent = c.transform;
            }

            var root = new GameObject("Victory", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rrt = (RectTransform)root.transform; rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.05f, 0.02f, 0.02f, 0.78f);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var crt = (RectTransform)card.transform; crt.anchorMin = crt.anchorMax = new Vector2(.5f, .5f); crt.sizeDelta = new Vector2(560f, 300f);
            card.GetComponent<Image>().color = new Color(0.09f, 0.06f, 0.07f, 0.97f);
            var edge = card.AddComponent<Outline>(); edge.effectColor = new Color(1f, 0.55f, 0.15f, 1f); edge.effectDistance = new Vector2(3f, -3f);

            var title = UIText.Make(card.transform, "Title", "¡VICTORIA!", 46, new Color(1f, 0.78f, 0.25f));
            title.fontStyle = FontStyle.Bold; Place(title.rectTransform, new Vector2(0f, 90f), new Vector2(520f, 60f));
            var sub = UIText.Make(card.transform, "Sub", $"Derrotaste al {bossName}.\nEl volcán vuelve a dormir.", 20, Color.white);
            Place(sub.rectTransform, new Vector2(0f, 20f), new Vector2(520f, 60f));

            var again = MakeButton(card.transform, "Repetir combate", new Color(1f, 0.55f, 0.15f), new Color(0.12f, 0.05f, 0.03f), new Vector2(-130f, -85f));
            again.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            var menu = MakeButton(card.transform, "Menú principal", new Color(0.18f, 0.2f, 0.3f), Color.white, new Vector2(130f, -85f));
            menu.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static Button MakeButton(Transform parent, string label, Color bg, Color fg, Vector2 pos)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, pos, new Vector2(230f, 56f));
            var im = go.GetComponent<Image>(); im.color = bg;
            var b = go.GetComponent<Button>(); b.targetGraphic = im;
            var t = UIText.Make(go.transform, "Label", label, 20, fg); t.fontStyle = FontStyle.Bold;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            ButtonSound.Add(b);
            return b;
        }
    }
}
