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
            // Mismo estilo low poly que el resto de ventanas (MenuStyle.Window).
            var box = MenuStyle.Window(hud.transform, "NivelCompletado", "¡NIVEL COMPLETADO!", new Vector2(620, 380), MenuStyle.Gold, null, out var root);
            root.transform.SetAsLastSibling();
            string info = next != null ? $"Derrotaste a {bossName}.\nDesbloqueaste: {next.name}" : $"Derrotaste a {bossName}.\n¡Completaste todos los niveles!";
            var sub = MenuStyle.Label(box, info, 24, MenuStyle.Cream, new Color(0, 0, 0, .6f), false);
            MenuStyle.Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -130), new Vector2(560, 80));

            if (next != null)
            {
                string scene = LevelMenu.SceneName(next);
                var go = MenuStyle.GlossyButton("Next", box, $"Siguiente: {next.name}", MenuStyle.Orange, 24, () => { Time.timeScale = 1f; SceneManager.LoadScene(scene); });
                MenuStyle.Place((RectTransform)go.transform, new Vector2(.5f, 0f), new Vector2(-150, 60), new Vector2(280, 66));
            }
            var menu = MenuStyle.GlossyButton("Menu", box, "Menú principal", MenuStyle.Green, 24, () => { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); });
            MenuStyle.Place((RectTransform)menu.transform, new Vector2(.5f, 0f), new Vector2(next != null ? 150 : 0, 60), new Vector2(260, 66));
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
