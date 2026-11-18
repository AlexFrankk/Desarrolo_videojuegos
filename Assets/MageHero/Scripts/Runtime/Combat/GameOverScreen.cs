using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Pantalla de GAME OVER cuando la vida del mago llega a 0.
    ///  - "Resolver problema": aparece una ecuación con 4 opciones (teclas 1-4 o clic).
    ///    Correcta → el mago revive donde cayó. Incorrecta → vuelve al menú de inicio.
    ///  - "Volver al menú": vuelve al menú de inicio directamente.
    /// Mientras está abierta el juego queda en pausa (Time.timeScale = 0).
    /// La crea CombatBootstrap en cada nivel; construye su propio Canvas, no necesita prefab.
    public class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

        public string menuScene = "MainMenu";
        [Tooltip("Segundos que se ve el resultado antes de revivir o volver al menú.")]
        public float resultDelay = 1.6f;

        static readonly Color Navy = new Color(0.114f, 0.208f, 0.341f, 0.97f), Gold = new Color(1f, 0.718f, 0.012f, 1f);
        static readonly Color Red = new Color(0.92f, 0.25f, 0.25f, 1f), Green = new Color(0.4f, 0.85f, 0.45f, 1f);
        static readonly Color AnswerBg = new Color(1f, 0.83f, 0.35f, 1f), ButtonBg = new Color(0.3f, 0.55f, 0.95f, 1f);

        GameObject root, choicePage, quizPage;
        Text equationText, feedbackText;
        readonly Button[] answerButtons = new Button[4];
        readonly Text[] answerLabels = new Text[4];
        ArithmeticEquation equation;
        PlayerRespawn3D player;
        bool answered;
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
            if (root != null && root.activeSelf) Time.timeScale = savedTimeScale;
        }

        // ------------------------------------------------------------------ API
        public void Show(PlayerRespawn3D who)
        {
            player = who;
            answered = false;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            root.SetActive(true);
            choicePage.SetActive(true);
            quizPage.SetActive(false);
        }

        void OpenQuiz()
        {
            equation = ArithmeticEquation.Random();
            equationText.text = equation.text;
            feedbackText.text = "Elige la respuesta correcta (clic o teclas 1-4). Solo tienes un intento.";
            feedbackText.color = Color.white;
            for (int i = 0; i < 4; i++)
            {
                answerLabels[i].text = $"{i + 1})  {equation.options[i]}";
                answerButtons[i].image.color = AnswerBg;
                answerButtons[i].interactable = true;
            }
            choicePage.SetActive(false);
            quizPage.SetActive(true);
        }

        void Answer(int index)
        {
            if (answered || equation == null) return;
            answered = true;
            foreach (var b in answerButtons) b.interactable = false;
            answerButtons[equation.correctIndex].image.color = Green;

            if (index == equation.correctIndex)
            {
                feedbackText.text = "¡Correcto! El mago vuelve a la vida.";
                feedbackText.color = Green;
                StartCoroutine(After(resultDelay, Revive));
            }
            else
            {
                answerButtons[index].image.color = Red;
                feedbackText.text = $"Incorrecto. La respuesta era {equation.answer}. Volviendo al menú...";
                feedbackText.color = Red;
                StartCoroutine(After(resultDelay + 0.8f, GoToMenu));
            }
        }

        void Revive()
        {
            root.SetActive(false);
            Time.timeScale = savedTimeScale;
            if (player != null) player.ReviveHere();
        }

        void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuScene);
        }

        IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        void Update()
        {
            if (!IsOpen || !quizPage.activeSelf || answered) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Answer(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Answer(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Answer(2);
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Answer(3);
        }

        // ------------------------------------------------------------------ UI
        void Build()
        {
            var canvasGo = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;                                      // por encima del HUD y del mapa
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Fondo oscuro que bloquea los clics al juego.
            var bg = Img("Overlay", canvasGo.transform, new Color(0f, 0f, 0f, 0.78f));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            root = bg.gameObject;

            var box = Img("Box", root.transform, Navy);
            box.rectTransform.sizeDelta = new Vector2(640, 440);

            var title = UIText.Make(box.transform, "Title", "GAME OVER", 64, Red);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, 0, 160, 600, 80);

            // --- Página 1: elegir
            choicePage = new GameObject("Choice", typeof(RectTransform));
            choicePage.transform.SetParent(box.transform, false);
            Stretch((RectTransform)choicePage.transform);
            var lost = UIText.Make(choicePage.transform, "Lost", "¡Has perdido! Tu vida llegó a cero.", 28, Color.white);
            Place(lost.rectTransform, 0, 85, 600, 40);
            var offer = UIText.Make(choicePage.transform, "Offer",
                "Resuelve un problema matemático para revivir.\nSi fallas, volverás al menú de inicio.", 22, Gold);
            Place(offer.rectTransform, 0, 25, 600, 70);
            var solve = MakeButton("SolveButton", choicePage.transform, "Resolver problema", Green, Color.black, 28);
            Place((RectTransform)solve.transform, 0, -70, 380, 70);
            solve.onClick.AddListener(OpenQuiz);
            var menu = MakeButton("MenuButton", choicePage.transform, "Volver al menú", ButtonBg, Color.white, 22);
            Place((RectTransform)menu.transform, 0, -160, 280, 54);
            menu.onClick.AddListener(GoToMenu);

            // --- Página 2: problema
            quizPage = new GameObject("Quiz", typeof(RectTransform));
            quizPage.transform.SetParent(box.transform, false);
            Stretch((RectTransform)quizPage.transform);
            var eqBg = Img("EquationBg", quizPage.transform, new Color(0f, 0f, 0f, 0.35f));
            Place(eqBg.rectTransform, 0, 70, 560, 90);
            equationText = UIText.Make(eqBg.transform, "Equation", "", 46, Color.white);
            equationText.fontStyle = FontStyle.Bold;
            Stretch(equationText.rectTransform);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var b = MakeButton("Answer" + (i + 1), quizPage.transform, "", AnswerBg, Color.black, 30);
                Place((RectTransform)b.transform, (i % 2 == 0 ? -140 : 140), (i < 2 ? -40 : -115), 260, 60);
                b.onClick.AddListener(() => Answer(idx));
                answerButtons[i] = b;
                answerLabels[i] = b.GetComponentInChildren<Text>();
            }
            feedbackText = UIText.Make(quizPage.transform, "Feedback", "", 20, Color.white);
            Place(feedbackText.rectTransform, 0, -180, 600, 40);
        }

        static Image Img(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }

        static Button MakeButton(string name, Transform parent, string label, Color bg, Color fg, int size)
        {
            var img = Img(name, parent, bg);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f); colors.pressedColor = new Color(.75f, .75f, .75f, 1f);
            colors.disabledColor = Color.white; colors.colorMultiplier = 1f;
            b.colors = colors;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            var t = UIText.Make(img.transform, "Label", label, size, fg);
            t.fontStyle = FontStyle.Bold; Stretch(t.rectTransform);
            return b;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
