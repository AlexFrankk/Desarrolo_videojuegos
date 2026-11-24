using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Menú de pausa de los niveles (Esc o botón "Pausa" del HUD). Antes Esc volvía directo al menú de inicio.
    /// Opciones: Continuar · Reiniciar nivel · Ver mapa · Cambiar de escenario · Menú principal, y la X para cerrar.
    /// Mientras está abierto el juego se congela (Time.timeScale = 0) y el sonido se pausa.
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

        public string menuScene = "MainMenu";
        GameObject root;
        Button mapButton;
        float savedTimeScale = 1f;

        void Awake()
        {
            Instance = this;
            Build();
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (root != null && root.activeSelf) { Time.timeScale = 1f; AudioListener.pause = false; }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (IsOpen) { Resume(); return; }
            // Esc primero cierra lo que esté abierto (quiz, pista, mapa); el Game Over y la victoria no se pausan.
            if (UIManager.BlocksEscape || MapOverlay.BlocksEscape || GameOverScreen.IsOpen || BossVictoryScreen.IsOpen) return;
            Open();
        }

        public void Open()
        {
            if (IsOpen || GameOverScreen.IsOpen || BossVictoryScreen.IsOpen) return;
            if (MapOverlay.IsOpen) FindAnyObjectByType<MapOverlay>()?.Toggle();
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (mapButton != null) mapButton.gameObject.SetActive(HasMap());
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Resume()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }

        void Restart() => Load(SceneManager.GetActiveScene().name);
        void ChangeLevel() { StyledMainMenu.OpenLevelsOnStart = true; Load(menuScene); }
        void MainMenu() => Load(menuScene);

        void ShowMap()
        {
            var map = FindAnyObjectByType<MapOverlay>();
            Resume();
            if (map != null && !MapOverlay.IsOpen) map.Toggle();
        }

        static bool HasMap()
        {
            var map = FindAnyObjectByType<MapOverlay>();
            return map != null && map.panel != null;
        }

        void Load(string scene)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(scene);
        }

        // ------------------------------------------------------------------ UI
        void Build()
        {
            var canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;                                       // encima del HUD y del mapa
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var box = MenuStyle.Window(canvasGo.transform, "PausePanel", "Pausa", new Vector2(520, 560), MenuStyle.Orange, Resume, out root);

            var sub = MenuStyle.Label(box, "El juego está detenido", 20, MenuStyle.Cream, new Color(0, 0, 0, .6f), false);
            MenuStyle.Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -80), new Vector2(460, 30));

            float y = -150f, step = 78f;
            Add(box, "Continuar", MenuStyle.Green, 34, Resume, ref y, step, 76f);
            Add(box, "Reiniciar nivel", MenuStyle.Blue, 28, Restart, ref y, step, 64f);
            mapButton = Add(box, "Ver mapa (M)", MenuStyle.Gold, 28, ShowMap, ref y, step, 64f);
            Add(box, "Cambiar de escenario", MenuStyle.Purple, 26, ChangeLevel, ref y, step, 64f);
            Add(box, "Menú principal", MenuStyle.Red, 28, MainMenu, ref y, step, 64f);

            var hint = UIText.Make(box, "Hint", "Esc o X: continuar", 16, new Color(1, 1, 1, .6f));
            MenuStyle.Place(hint.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 22), new Vector2(460, 24));
        }

        static Button Add(RectTransform box, string text, Color color, int font, System.Action onClick, ref float y, float step, float height)
        {
            var b = MenuStyle.GlossyButton(text, box, text, color, font, onClick);
            MenuStyle.Place((RectTransform)b.transform, new Vector2(.5f, 1f), new Vector2(0, y), new Vector2(360, height));
            y -= step;
            return b;
        }
    }
}
