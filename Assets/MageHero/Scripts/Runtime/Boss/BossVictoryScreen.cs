using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Pantalla de victoria al derrotar al jefe (mismo estilo que el menú): repetir, otro escenario o menú.
    public static class BossVictoryScreen
    {
        static GameObject current;
        public static bool IsOpen => current != null && current.activeInHierarchy;

        public static void Show(string bossName)
        {
            if (IsOpen) return;
            var ui = UIManager.Instance;
            ui?.SetPlayerLocked(true);
            if (PauseMenu.IsOpen) PauseMenu.Instance.Resume();

            var c = new GameObject("VictoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = c.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 450;
            var sc = c.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;

            var box = MenuStyle.Window(c.transform, "Victory", "¡VICTORIA!", new Vector2(600, 420), MenuStyle.Gold, null, out current);

            var sub = MenuStyle.Label(box, $"Derrotaste al {bossName}.\nEl volcán vuelve a dormir.", 24, Color.white, new Color(0, 0, 0, .6f), false);
            MenuStyle.Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -120), new Vector2(540, 70));

            var again = MenuStyle.GlossyButton("Again", box, "Repetir combate", MenuStyle.Orange, 26, () => Load(SceneManager.GetActiveScene().name));
            MenuStyle.Place((RectTransform)again.transform, new Vector2(.5f, 0f), new Vector2(-140, 130), new Vector2(250, 66));
            var levels = MenuStyle.GlossyButton("Levels", box, "Otro escenario", MenuStyle.Purple, 26, () => { StyledMainMenu.OpenLevelsOnStart = true; Load("MainMenu"); });
            MenuStyle.Place((RectTransform)levels.transform, new Vector2(.5f, 0f), new Vector2(140, 130), new Vector2(250, 66));
            var menu = MenuStyle.GlossyButton("Menu", box, "Menú principal", MenuStyle.Green, 26, () => Load("MainMenu"));
            MenuStyle.Place((RectTransform)menu.transform, new Vector2(.5f, 0f), new Vector2(0, 50), new Vector2(280, 62));
            ButtonSound.Add(again); ButtonSound.Add(levels); ButtonSound.Add(menu);   // sonidos hover/clic (rama alice)
        }

        static void Load(string scene)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(scene);
        }
    }
}
