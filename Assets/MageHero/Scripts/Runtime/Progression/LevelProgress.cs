using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Progreso entre niveles: solo el primer nivel está abierto; el siguiente se desbloquea al derrotar
    /// al jefe final del anterior. Se guarda en PlayerPrefs (persiste entre partidas).
    /// Orden: el de LevelMenu.Entries() (Otoño → Desierto → Primavera → Volcán).
    public static class LevelProgress
    {
        const string Key = "MageHero.UnlockedLevel";

        /// Índice del nivel más alto desbloqueado (0 = solo el primero).
        public static int Unlocked => Mathf.Max(0, PlayerPrefs.GetInt(Key, 0));

        public static int IndexOf(string sceneOrId)
        {
            var defs = LevelMenu.Entries();
            for (int i = 0; i < defs.Length; i++)
                if (defs[i].id == sceneOrId || LevelMenu.SceneName(defs[i]) == sceneOrId) return i;
            return -1;
        }

        public static bool IsUnlocked(string sceneOrId)
        {
            int i = IndexOf(sceneOrId);
            return i < 0 || i <= Unlocked;                    // escenas fuera de la lista (menú, pruebas) siempre abiertas
        }

        /// Nombre del nivel que hay que terminar para abrir 'sceneOrId'.
        public static string RequiredLevelName(string sceneOrId)
        {
            int i = IndexOf(sceneOrId);
            var defs = LevelMenu.Entries();
            return i > 0 ? defs[i - 1].name : "";
        }

        /// Marca como completado el nivel actual y desbloquea el siguiente. Devuelve el siguiente (o null si era el último).
        public static LevelDef CompleteCurrent()
        {
            var defs = LevelMenu.Entries();
            int i = IndexOf(SceneManager.GetActiveScene().name);
            if (i < 0) return null;
            if (i + 1 > Unlocked) { PlayerPrefs.SetInt(Key, Mathf.Min(i + 1, defs.Length - 1)); PlayerPrefs.Save(); }
            return i + 1 < defs.Length ? defs[i + 1] : null;
        }

        public static void ResetProgress() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }

        // ------------------------------------------------------------------ pantalla de nivel completado
        public static void ShowLevelComplete(string bossName, LevelDef next)
        {
            UIManager.Instance?.SetPlayerLocked(true);
            var hud = GameObject.Find("HUD");
            if (hud == null) return;
            var root = new GameObject("NivelCompletado", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(hud.transform, false);
            var rrt = (RectTransform)root.transform; rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            var bg = root.GetComponent<Image>(); bg.color = new Color(0.02f, 0.04f, 0.08f, 0.78f); bg.raycastTarget = true;

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var crt = (RectTransform)card.transform; crt.anchorMin = crt.anchorMax = new Vector2(.5f, .5f); crt.sizeDelta = new Vector2(600f, 320f);
            card.GetComponent<Image>().color = new Color(0.114f, 0.208f, 0.341f, 0.98f);
            var edge = card.AddComponent<Outline>(); edge.effectColor = new Color(1f, 0.72f, 0.01f); edge.effectDistance = new Vector2(3f, -3f);

            var title = UIText.Make(card.transform, "Title", "¡NIVEL COMPLETADO!", 40, new Color(1f, 0.78f, 0.25f));
            title.fontStyle = FontStyle.Bold; Place(title.rectTransform, new Vector2(0f, 105f), new Vector2(560f, 56f));
            string info = next != null ? $"Derrotaste a {bossName}.\nDesbloqueaste: {next.name}" : $"Derrotaste a {bossName}.\n¡Completaste todos los niveles!";
            var sub = UIText.Make(card.transform, "Sub", info, 21, Color.white);
            Place(sub.rectTransform, new Vector2(0f, 30f), new Vector2(560f, 70f));

            if (next != null)
            {
                string scene = LevelMenu.SceneName(next);
                Button(card.transform, $"Siguiente: {next.name}", new Color(1f, 0.72f, 0.01f), new Color(0.1f, 0.12f, 0.2f), new Vector2(-145f, -95f),
                       () => { Time.timeScale = 1f; SceneManager.LoadScene(scene); });
            }
            Button(card.transform, "Menú principal", new Color(0.2f, 0.62f, 0.32f), Color.white, new Vector2(next != null ? 145f : 0f, -95f),
                   () => { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); });
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void Button(Transform parent, string label, Color bg, Color fg, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, pos, new Vector2(270f, 58f));
            var im = go.GetComponent<Image>(); im.color = bg;
            var b = go.GetComponent<Button>(); b.targetGraphic = im; b.onClick.AddListener(onClick);
            var t = UIText.Make(go.transform, "Label", label, 19, fg); t.fontStyle = FontStyle.Bold;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        }
    }
}
