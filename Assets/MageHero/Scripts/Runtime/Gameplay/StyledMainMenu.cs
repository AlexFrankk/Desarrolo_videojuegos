using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageHero
{
    /// Menú principal con estilo "cartoon": fondo de pizarra con manchas, cinta naranja con el título,
    /// botones redondeados con brillo y botones circulares. Tres pantallas:
    ///  1) Portada: Jugar · Controles · Salir.
    ///  2) Escenarios: una tarjeta por nivel con imagen de referencia, nombre, tema y botón ¡Jugar!.
    ///  3) Controles (ventana emergente).
    /// Se construye en tiempo de ejecución sobre el Canvas de MenuController (sustituye al menú antiguo).
    /// Imágenes de los escenarios: Resources/MenuThumbs/<id>.png
    /// (se regeneran con Tools > Mage Hero > Menu > Capturar imágenes de escenarios).
    public class StyledMainMenu : MonoBehaviour
    {
        public const string ThumbFolder = "MenuThumbs";
        /// Si es true, el menú se abre directamente en la lista de escenarios (lo usa "Cambiar de escenario" de la pausa).
        public static bool OpenLevelsOnStart;

        MenuController menu;
        GameObject mainPage, levelsPage, controlsPanel, loadingPanel;
        Text loadingTitle, loadingInfo;
        RectTransform loadingFill;
        bool loading;

        public bool IsLoading => loading;

        void Start()
        {
            // Seguridad: nunca construir el menú dentro de un nivel (borraría el HUD del juego).
            if (!MenuController.IsMainMenuScene) { Destroy(this); return; }
            menu = GetComponent<MenuController>();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);   // menú antiguo
            EnsureClickable();
            Build();
            if (OpenLevelsOnStart) { OpenLevelsOnStart = false; ShowLevels(); }
            else ShowMain();
        }

        /// Sin EventSystem o sin GraphicRaycaster los botones no reciben clics.
        void EnsureClickable()
        {
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (loading || kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (controlsPanel.activeSelf) controlsPanel.SetActive(false);
            else if (levelsPage.activeSelf) ShowMain();
        }

        void ShowMain() { mainPage.SetActive(true); levelsPage.SetActive(false); controlsPanel.SetActive(false); }
        void ShowLevels() { mainPage.SetActive(false); levelsPage.SetActive(true); controlsPanel.SetActive(false); }
        void ShowControls() { controlsPanel.SetActive(true); }
        void Load(string scene) => LoadScene(scene);
        void Quit() { if (menu != null) menu.Quit(); }

        // ------------------------------------------------------------------ Carga del escenario
        /// Carga el nivel mostrando una pantalla de "Cargando..." con barra de progreso
        /// (los niveles tardan unos segundos y sin aviso parecía que el botón no funcionaba).
        public void LoadScene(string scene)
        {
            if (loading) return;
            // Progreso: hay que terminar el nivel anterior (derrotar a su jefe) para entrar.
            if (!LevelProgress.IsUnlocked(scene))
            {
                ShowLoading("Nivel bloqueado", $"Termina «{LevelProgress.RequiredLevelName(scene)}» derrotando a su jefe para desbloquearlo.", 0f);
                StartCoroutine(HideLoadingLater(2.5f));
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[MageHero] La escena '{scene}' no está en la lista de escenas del build (File > Build Profiles).");
                ShowLoading("No se pudo abrir", $"Falta la escena '{scene}' en File > Build Profiles.", 0f);
                StartCoroutine(HideLoadingLater(3f));
                return;
            }
            StartCoroutine(LoadRoutine(scene));
        }

        IEnumerator LoadRoutine(string scene)
        {
            loading = true;
            Time.timeScale = 1f;
            string niceName = scene;
            foreach (var d in LevelMenu.Entries()) if (LevelMenu.SceneName(d) == scene) niceName = d.name;
            ShowLoading("Cargando...", niceName, 0f);
            Debug.Log($"[MageHero] Cargando escena '{scene}'...");
            yield return null;                                   // dibuja la pantalla de carga antes de empezar
            yield return null;

            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (op == null) { loading = false; ShowLoading("No se pudo abrir", scene, 0f); StartCoroutine(HideLoadingLater(3f)); yield break; }
            while (!op.isDone)
            {
                SetProgress(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
        }

        void ShowLoading(string title, string info, float progress)
        {
            loadingPanel.SetActive(true);
            loadingPanel.transform.SetAsLastSibling();
            loadingTitle.text = title;
            loadingInfo.text = info;
            SetProgress(progress);
        }

        void SetProgress(float p) { if (loadingFill != null) loadingFill.anchorMax = new Vector2(Mathf.Max(0.02f, p), 1f); }

        IEnumerator HideLoadingLater(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!loading) loadingPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ Construcción
        void Build()
        {
            var bg = MenuStyle.Rect("Background", transform).gameObject.AddComponent<RawImage>();
            bg.texture = MenuStyle.Background();
            bg.raycastTarget = false;
            MenuStyle.Stretch(bg.rectTransform);

            BuildMainPage();
            BuildLevelsPage();
            BuildControlsPanel();
            BuildLoadingPanel();
        }

        void BuildLoadingPanel()
        {
            var overlay = MenuStyle.Image("LoadingPanel", transform, null, new Color(0.06f, 0.08f, 0.11f, 0.92f));
            overlay.raycastTarget = true;                         // bloquea más clics mientras carga
            MenuStyle.Stretch(overlay.rectTransform);
            loadingPanel = overlay.gameObject;

            var r = MenuStyle.Image("Ribbon", overlay.transform, MenuStyle.Ribbon(MenuStyle.Orange, MenuStyle.OrangeDark), Color.white);
            MenuStyle.Place(r.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 90), new Vector2(620, 120));
            loadingTitle = MenuStyle.Label(r.transform, "Cargando...", 50, Color.white, MenuStyle.OrangeDark);
            MenuStyle.Stretch(loadingTitle.rectTransform);
            loadingTitle.rectTransform.offsetMin = new Vector2(40, 6); loadingTitle.rectTransform.offsetMax = new Vector2(-40, 0);

            loadingInfo = MenuStyle.Label(overlay.transform, "", 28, Color.white, new Color(0, 0, 0, .6f));
            MenuStyle.Place(loadingInfo.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(900, 40));

            var bar = MenuStyle.Image("Bar", overlay.transform, MenuStyle.RoundedRect(new Color(0, 0, 0, .5f), new Color(0, 0, 0, .5f), Color.white, 16, 3), Color.white);
            MenuStyle.Place(bar.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -70), new Vector2(520, 36));
            var fill = MenuStyle.Image("Fill", bar.transform, MenuStyle.RoundedRect(Color.Lerp(MenuStyle.Green, Color.white, .2f), MenuStyle.GreenDark, MenuStyle.GreenDark, 12, 2), Color.white);
            loadingFill = fill.rectTransform;
            loadingFill.anchorMin = Vector2.zero; loadingFill.anchorMax = new Vector2(0.02f, 1f);
            loadingFill.offsetMin = new Vector2(5, 5); loadingFill.offsetMax = new Vector2(-5, -5);

            loadingPanel.SetActive(false);
        }

        void BuildMainPage()
        {
            mainPage = MenuStyle.Rect("MainPage", transform).gameObject;
            MenuStyle.Stretch((RectTransform)mainPage.transform);

            Ribbon(mainPage.transform, "Mago de los Números", new Vector2(0, -105), new Vector2(940, 150), 64);
            var sub = MenuStyle.Label(mainPage.transform, "¡Aprende matemáticas con magia!", 24, new Color(1f, 0.93f, 0.6f), new Color(0, 0, 0, .6f));
            MenuStyle.Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -200), new Vector2(800, 36));

            var play = MenuStyle.GlossyButton("PlayButton", mainPage.transform, "Jugar", MenuStyle.Red, 46, ShowLevels);
            MenuStyle.Place((RectTransform)play.transform, new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(340, 92));
            var controls = MenuStyle.GlossyButton("ControlsButton", mainPage.transform, "Controles", MenuStyle.Green, 40, ShowControls);
            MenuStyle.Place((RectTransform)controls.transform, new Vector2(.5f, .5f), new Vector2(0, -100), new Vector2(340, 84));
            var quit = MenuStyle.GlossyButton("QuitButton", mainPage.transform, "Salir", MenuStyle.Green, 40, Quit);
            MenuStyle.Place((RectTransform)quit.transform, new Vector2(.5f, .5f), new Vector2(0, -205), new Vector2(340, 84));

            var help = MenuStyle.RoundButton("HelpButton", mainPage.transform, "?", MenuStyle.Purple, 92, ShowControls);
            MenuStyle.Place((RectTransform)help.transform, new Vector2(0f, 0f), new Vector2(90, 80), new Vector2(92, 92));
            var levels = MenuStyle.RoundButton("LevelsButton", mainPage.transform, "►", MenuStyle.Blue, 72, ShowLevels);
            MenuStyle.Place((RectTransform)levels.transform, new Vector2(0f, 0f), new Vector2(190, 72), new Vector2(72, 72));

            // Vuelve a reproducir la cinemática de apertura y entra al Nivel 1.
            var story = MenuStyle.GlossyButton("StoryButton", mainPage.transform, "Ver historia", MenuStyle.Purple, 28, () =>
            {
                IntroCinematic.ForceNext = true;
                LoadScene(IntroCinematic.LevelScene);
            });
            MenuStyle.Place((RectTransform)story.transform, new Vector2(1f, 0f), new Vector2(-150, 72), new Vector2(240, 66));
        }

        void BuildLevelsPage()
        {
            levelsPage = MenuStyle.Rect("LevelsPage", transform).gameObject;
            MenuStyle.Stretch((RectTransform)levelsPage.transform);

            Ribbon(levelsPage.transform, "Elige un escenario", new Vector2(0, -78), new Vector2(760, 120), 50);
            var defs = LevelMenu.Entries();
            string keys = defs.Length <= 1 ? "1" : string.Join(", ", System.Linq.Enumerable.Range(1, defs.Length - 1)) + " o " + defs.Length;
            var hint = MenuStyle.Label(levelsPage.transform, "Haz clic en una tarjeta o pulsa " + keys, 20, new Color(1, 1, 1, .85f), new Color(0, 0, 0, .6f), false);
            MenuStyle.Place(hint.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -150), new Vector2(800, 30));

            // Con más de 3 escenarios las tarjetas se encogen para caber en el ancho de referencia (1280).
            const float step = 395f, maxRow = 1240f;
            float scale = Mathf.Min(1f, maxRow / (defs.Length * step));
            for (int i = 0; i < defs.Length; i++)
            {
                float x = (i - (defs.Length - 1) / 2f) * step * scale;
                // La escala va en un contenedor: ButtonBounce anima la escala de la propia tarjeta.
                var slot = MenuStyle.Rect("Slot_" + defs[i].id, levelsPage.transform);
                MenuStyle.Place(slot, new Vector2(.5f, .5f), new Vector2(x, -55 + (1f - scale) * 60f), new Vector2(365, 480));
                slot.localScale = new Vector3(scale, scale, 1f);
                LevelCard(slot, defs[i], i, Vector2.zero);
            }

            var back = MenuStyle.GlossyButton("BackButton", levelsPage.transform, "< Volver", MenuStyle.Green, 28, ShowMain);
            MenuStyle.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(125, 50), new Vector2(200, 62));
        }

        RectTransform LevelCard(Transform parent, LevelDef d, int index, Vector2 pos)
        {
            Color accent; if (!ColorUtility.TryParseHtmlString(d.accent, out accent)) accent = MenuStyle.Orange;
            string scene = LevelMenu.SceneName(d);

            var card = MenuStyle.Image("Card_" + d.id, parent,
                MenuStyle.RoundedRect(new Color(0.16f, 0.21f, 0.28f, 0.95f), new Color(0.09f, 0.12f, 0.17f, 0.95f), accent, 24, 5), Color.white);
            MenuStyle.Place(card.rectTransform, new Vector2(.5f, .5f), pos, new Vector2(365, 480));
            var shadow = card.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .55f); shadow.effectDistance = new Vector2(0, -8);
            // Toda la tarjeta se puede pulsar para entrar al escenario.
            card.raycastTarget = true;
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;
            var cardNav = cardBtn.navigation; cardNav.mode = Navigation.Mode.None; cardBtn.navigation = cardNav;
            cardBtn.onClick.AddListener(() => Load(scene));
            card.gameObject.AddComponent<ButtonBounce>().hoverScale = 1.03f;

            // Imagen de referencia (recortada con esquinas redondeadas mediante Mask).
            var frame = MenuStyle.Image("ThumbFrame", card.transform, MenuStyle.RoundedRect(Color.white, Color.white, Color.white, 18, 0), Color.white);
            MenuStyle.Place(frame.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -122), new Vector2(325, 200));
            frame.raycastTarget = true;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var thumb = MenuStyle.Rect("Thumb", frame.transform).gameObject.AddComponent<RawImage>();
            MenuStyle.Stretch(thumb.rectTransform);
            thumb.raycastTarget = false;
            SetThumb(thumb, d, 325f / 200f);
            var thumbBtn = frame.gameObject.AddComponent<Button>();
            thumbBtn.transition = Selectable.Transition.None;
            thumbBtn.onClick.AddListener(() => Load(scene));
            frame.gameObject.AddComponent<ButtonBounce>().hoverScale = 1.04f;

            // Insignia "NIVEL n" sobre la imagen.
            var badge = MenuStyle.Image("Badge", card.transform, MenuStyle.RoundedRect(Color.Lerp(accent, Color.white, .15f), MenuStyle.Darker(accent, .85f), MenuStyle.Darker(accent), 16, 3), Color.white);
            MenuStyle.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(82, -30), new Vector2(130, 38));
            var bl = MenuStyle.Label(badge.transform, $"{d.lv.ToUpperInvariant()}  ·  {index + 1}", 17, Color.white, MenuStyle.Darker(accent));
            MenuStyle.Stretch(bl.rectTransform);

            var name = MenuStyle.Label(card.transform, d.name, 28, Color.white, new Color(0, 0, 0, .7f));
            MenuStyle.Place(name.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -250), new Vector2(345, 36));
            var sub = UIText.Make(card.transform, "Sub", d.sub, 16, new Color(1, 1, 1, .8f));
            MenuStyle.Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -280), new Vector2(345, 24));
            var tags = UIText.Make(card.transform, "Tags", d.tags != null ? string.Join("  ·  ", d.tags) : "", 14, new Color(1, 1, 1, .6f));
            tags.horizontalOverflow = HorizontalWrapMode.Wrap;
            MenuStyle.Place(tags.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -312), new Vector2(330, 40));

            var topic = MenuStyle.Image("Topic", card.transform, MenuStyle.RoundedRect(new Color(0, 0, 0, .35f), new Color(0, 0, 0, .35f), accent, 16, 2), Color.white);
            MenuStyle.Place(topic.rectTransform, new Vector2(.5f, 0f), new Vector2(0, 112), new Vector2(290, 36));
            var tl = UIText.Make(topic.transform, "Label", "Tema: " + d.topic, 18, accent);
            tl.fontStyle = FontStyle.Bold;
            MenuStyle.Stretch(tl.rectTransform);

            var play = MenuStyle.GlossyButton("PlayButton", card.transform, "¡Jugar!", accent, 34, () => Load(scene));
            MenuStyle.Place((RectTransform)play.transform, new Vector2(.5f, 0f), new Vector2(0, 50), new Vector2(240, 70));

            // Tarjeta bloqueada: velo oscuro con candado y el requisito para abrirla.
            if (!LevelProgress.IsUnlocked(scene))
            {
                var veil = MenuStyle.Image("Bloqueado", card.transform, MenuStyle.RoundedRect(new Color(0.02f, 0.03f, 0.06f, 0.78f), new Color(0.02f, 0.03f, 0.06f, 0.78f), new Color(0, 0, 0, 0.8f), 24, 0), Color.white);
                MenuStyle.Stretch(veil.rectTransform);
                veil.raycastTarget = true;
                var lockBtn = veil.gameObject.AddComponent<Button>();
                lockBtn.transition = Selectable.Transition.None;
                lockBtn.onClick.AddListener(() => Load(scene));
                var lockIcon = MenuStyle.Label(veil.transform, "BLOQUEADO", 34, new Color(1f, 0.8f, 0.3f), new Color(0, 0, 0, .8f));
                MenuStyle.Place(lockIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(330, 50));
                var req = UIText.Make(veil.transform, "Requisito", $"Termina «{LevelProgress.RequiredLevelName(scene)}»\npara desbloquear este nivel", 18, Color.white);
                req.horizontalOverflow = HorizontalWrapMode.Wrap;
                MenuStyle.Place(req.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -30), new Vector2(320, 60));
            }
            return card.rectTransform;
        }

        /// Pone la imagen del escenario recortándola para llenar el hueco sin deformarse.
        static void SetThumb(RawImage img, LevelDef d, float slotAspect)
        {
            var tex = Resources.Load<Texture2D>(ThumbFolder + "/" + d.id);
            if (tex == null)
            {
                // Sin imagen: degradado con el color del cielo y del terreno del nivel.
                tex = new Texture2D(1, 2) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                tex.SetPixels(new[] { MenuStyle.Hex(d.pal != null ? d.pal.path : 0x8fb84e), MenuStyle.Hex(d.sky) });
                tex.Apply();
                img.texture = tex;
                img.uvRect = new Rect(0, 0, 1, 1);
                return;
            }
            img.texture = tex;
            float a = (float)tex.width / tex.height;
            img.uvRect = a > slotAspect
                ? new Rect((1f - slotAspect / a) * 0.5f, 0f, slotAspect / a, 1f)
                : new Rect(0f, (1f - a / slotAspect) * 0.5f, 1f, a / slotAspect);
        }

        void BuildControlsPanel()
        {
            var overlay = MenuStyle.Image("ControlsPanel", transform, null, new Color(0, 0, 0, .65f));
            overlay.raycastTarget = true;
            MenuStyle.Stretch(overlay.rectTransform);
            controlsPanel = overlay.gameObject;

            var box = MenuStyle.Image("Box", overlay.transform,
                MenuStyle.RoundedRect(new Color(0.18f, 0.23f, 0.30f), new Color(0.10f, 0.13f, 0.18f), MenuStyle.Orange, 26, 5), Color.white);
            MenuStyle.Place(box.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -20), new Vector2(680, 500));
            Ribbon(box.transform, "Controles", new Vector2(0, 5), new Vector2(460, 100), 44, new Vector2(.5f, 1f));

            string[,] rows =
            {
                { "W A S D / Flechas", "Caminar" }, { "Shift", "Correr" }, { "Espacio", "Saltar" },
                { "Clic izquierdo / Enter", "Lanzar hechizo" }, { "Clic derecho + arrastrar", "Girar la cámara" },
                { "Q / E · Z / X · Rueda", "Girar · Zoom" }, { "F · H", "Interactuar · Ver pista" }, { "M · Esc", "Mapa · Pausa" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = -95 - i * 38;
                var k = UIText.Make(box.transform, "Key", rows[i, 0], 20, new Color(1f, .8f, .3f), TextAnchor.MiddleRight);
                k.fontStyle = FontStyle.Bold;
                MenuStyle.Place(k.rectTransform, new Vector2(.5f, 1f), new Vector2(-150, y), new Vector2(300, 32));
                var v = UIText.Make(box.transform, "Action", rows[i, 1], 20, Color.white, TextAnchor.MiddleLeft);
                MenuStyle.Place(v.rectTransform, new Vector2(.5f, 1f), new Vector2(170, y), new Vector2(300, 32));
            }

            var close = MenuStyle.GlossyButton("CloseButton", box.transform, "Cerrar", MenuStyle.Red, 30, () => controlsPanel.SetActive(false));
            MenuStyle.Place((RectTransform)close.transform, new Vector2(.5f, 0f), new Vector2(0, 50), new Vector2(220, 66));
        }

        static void Ribbon(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Vector2? anchor = null)
        {
            var r = MenuStyle.Image("Ribbon", parent, MenuStyle.Ribbon(MenuStyle.Orange, MenuStyle.OrangeDark), Color.white);
            MenuStyle.Place(r.rectTransform, anchor ?? new Vector2(.5f, 1f), pos, size);
            r.rectTransform.localRotation = Quaternion.Euler(0, 0, 1.5f);
            var sh = r.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .45f); sh.effectDistance = new Vector2(0, -8);
            var t = MenuStyle.Label(r.transform, text, fontSize, Color.white, MenuStyle.OrangeDark);
            t.GetComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            MenuStyle.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(40, 6);
            t.rectTransform.offsetMax = new Vector2(-40, 0);
        }
    }
}
