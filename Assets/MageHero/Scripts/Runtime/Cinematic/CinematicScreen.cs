using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MageHero
{
    /// Capa de "cine" de la cinemática: barras negras 2.35:1, fundidos a negro/blanco, título, aviso de "Saltar"
    /// y un perfil de post-procesado propio (bloom, profundidad de campo, color cálido, viñeta y grano).
    /// Al terminar se retira suavemente y deja la cámara y el HUD como estaban.
    public class CinematicScreen : MonoBehaviour
    {
        Canvas canvas;
        RectTransform barTop, barBottom;
        Image fade;
        Text title, subtitle, skipLabel, caption;
        float captionUntil;
        Image skipFill;
        CanvasGroup titleGroup, skipGroup;
        float bars, barsTarget;

        Volume volume;
        VolumeProfile profile;
        Bloom bloom;
        DepthOfField dof;
        ColorAdjustments color;
        WhiteBalance white;
        Vignette vignette;
        FilmGrain grain;
        UniversalAdditionalCameraData camData;
        bool hadPostProcessing;

        float skipHold;
        public bool SkipRequested { get; private set; }
        public bool AllowSkip = true;
        /// true cuando Release() terminó de retirar barras y post-procesado (ya se puede destruir sin cortes).
        public bool Released { get; private set; }
        const float SkipHoldSeconds = 0.9f;

        public static CinematicScreen Create()
        {
            var go = new GameObject("CinematicScreen");
            var s = go.AddComponent<CinematicScreen>();
            s.Build();
            return s;
        }

        // ------------------------------------------------------------------ construcción
        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            barTop = Bar("BarraArriba", new Vector2(0, 1));
            barBottom = Bar("BarraAbajo", new Vector2(0, 0));

            fade = new GameObject("Fundido", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fade.transform.SetParent(transform, false);
            MenuStyle.Stretch(fade.rectTransform);
            fade.color = Color.black;
            fade.raycastTarget = false;

            // Título
            var tg = new GameObject("Titulo", typeof(RectTransform), typeof(CanvasGroup));
            tg.transform.SetParent(transform, false);
            MenuStyle.Stretch((RectTransform)tg.transform);
            titleGroup = tg.GetComponent<CanvasGroup>(); titleGroup.alpha = 0f; titleGroup.blocksRaycasts = false;
            title = UIText.Make(tg.transform, "Texto", "", 118, new Color(1f, 0.9f, 0.62f));
            title.fontStyle = FontStyle.Bold;
            var o = title.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.35f, 0.18f, 0.02f, 0.9f); o.effectDistance = new Vector2(3, -3);
            var sh = title.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(1f, 0.6f, 0.15f, 0.35f); sh.effectDistance = new Vector2(0, -8);
            MenuStyle.Place(title.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(1800, 160));
            subtitle = UIText.Make(tg.transform, "Sub", "", 34, new Color(1f, 1f, 1f, 0.9f));
            subtitle.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            MenuStyle.Place(subtitle.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(1600, 60));

            // Aviso de saltar (mantener Esc / Espacio o clic en el botón)
            var sk = new GameObject("Saltar", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            sk.transform.SetParent(transform, false);
            var skr = (RectTransform)sk.transform;
            MenuStyle.Place(skr, new Vector2(1, 0), new Vector2(-170, 62), new Vector2(280, 54));
            var skImg = sk.GetComponent<Image>();
            skImg.sprite = MenuStyle.RoundedRect(new Color(0, 0, 0, .55f), new Color(0, 0, 0, .55f), new Color(1, 1, 1, .35f), 20, 2);
            skImg.type = Image.Type.Sliced;
            sk.GetComponent<Button>().onClick.AddListener(() => { if (AllowSkip) SkipRequested = true; });
            skipGroup = sk.GetComponent<CanvasGroup>(); skipGroup.alpha = 0f;
            skipFill = new GameObject("Progreso", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            skipFill.transform.SetParent(sk.transform, false);
            skipFill.color = new Color(1f, 0.85f, 0.4f, 0.35f);
            skipFill.raycastTarget = false;
            var fr = skipFill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0, 1); fr.offsetMin = new Vector2(4, 4); fr.offsetMax = new Vector2(-4, -4);
            skipLabel = UIText.Make(sk.transform, "Texto", "Mantén Esc para saltar  ►", 22, new Color(1, 1, 1, .9f));
            MenuStyle.Stretch(skipLabel.rectTransform);

            // Subtítulos de la voz en off (sobre la barra negra inferior)
            caption = UIText.Make(transform, "Subtitulo", "", 30, new Color(1f, 0.97f, 0.9f, 1f));
            var co = caption.gameObject.AddComponent<Outline>(); co.effectColor = new Color(0, 0, 0, 0.85f); co.effectDistance = new Vector2(2, -2);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            MenuStyle.Place(caption.rectTransform, new Vector2(.5f, 0), new Vector2(0, 64), new Vector2(1500, 96));

            BuildVolume();
        }

        /// Subtítulo de la narración; texto vacío lo borra.
        public void ShowCaption(string text, float seconds)
        {
            if (caption == null) return;
            caption.text = text;
            captionUntil = Time.unscaledTime + seconds;
        }

        RectTransform Bar(string name, Vector2 anchor)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(transform, false);
            img.color = Color.black;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, anchor.y); rt.anchorMax = new Vector2(1, anchor.y); rt.pivot = new Vector2(0.5f, anchor.y);
            rt.sizeDelta = new Vector2(0, 0); rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        void BuildVolume()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                camData = cam.GetUniversalAdditionalCameraData();
                hadPostProcessing = camData.renderPostProcessing;
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            var vgo = new GameObject("VolumenCine");
            vgo.transform.SetParent(transform, false);
            volume = vgo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.weight = 1f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PerfilCine";
            volume.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.95f; bloom.intensity.value = 1.1f; bloom.scatter.value = 0.72f;
            bloom.tint.value = new Color(1f, 0.93f, 0.82f);

            dof = profile.Add<DepthOfField>(true);
            dof.mode.value = DepthOfFieldMode.Bokeh;
            dof.focusDistance.value = 6f; dof.focalLength.value = 55f; dof.aperture.value = 4f;
            dof.bladeCount.value = 6; dof.bladeCurvature.value = 0.8f;

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.05f; color.contrast.value = 14f; color.saturation.value = 14f;
            color.colorFilter.value = new Color(1f, 0.96f, 0.9f);
            color.hueShift.value = 0f;

            white = profile.Add<WhiteBalance>(true);
            white.temperature.value = 14f; white.tint.value = 2f;

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.32f; vignette.smoothness.value = 0.45f; vignette.color.value = new Color(0.08f, 0.04f, 0.02f);

            grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Thin2; grain.intensity.value = 0.18f; grain.response.value = 0.8f;
        }

        // ------------------------------------------------------------------ control
        public void SetBars(float k) => barsTarget = Mathf.Clamp01(k);
        public void SetFocus(float distance, float aperture = 4f, float focal = 55f)
        {
            dof.focusDistance.value = Mathf.Max(0.1f, distance); dof.aperture.value = aperture; dof.focalLength.value = focal;
        }
        public void SetDepthOfField(bool on) => dof.active = on;
        /// Temperatura del color (cálido > 0, frío < 0) y filtro.
        public void SetGrade(float temperature, Color filter, float saturation = 14f, float exposure = 0.05f)
        {
            white.temperature.value = temperature; color.colorFilter.value = filter; color.saturation.value = saturation; color.postExposure.value = exposure;
        }
        public void SetBloom(float intensity) => bloom.intensity.value = intensity;
        public void SetVignette(float k) => vignette.intensity.value = k;
        public float Weight { get => volume.weight; set => volume.weight = Mathf.Clamp01(value); }

        /// Fundido. Si la pantalla se destruye a mitad (fin de la cinemática o salto), la corrutina se detiene sola.
        public IEnumerator Fade(Color c, float to, float seconds)
        {
            if (fade == null) yield break;
            Color from = fade.color;
            float a0 = from.a;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                if (fade == null) yield break;
                fade.color = new Color(c.r, c.g, c.b, Mathf.Lerp(a0, to, t));
                yield return null;
            }
            if (fade != null) fade.color = new Color(c.r, c.g, c.b, to);
        }

        /// Destello instantáneo (blanco o rojo) que se apaga en <paramref name="seconds"/>.
        public void Flash(Color c, float seconds) { StartCoroutine(FlashRoutine(c, seconds)); }
        IEnumerator FlashRoutine(Color c, float seconds)
        {
            fade.color = c;
            yield return Fade(c, 0f, seconds);
        }

        public IEnumerator ShowTitle(string text, string sub, float fadeIn, float hold, float fadeOut)
        {
            title.text = text; subtitle.text = sub;
            title.rectTransform.localScale = Vector3.one * 0.94f;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / fadeIn)
            {
                titleGroup.alpha = t; title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, t);
                yield return null;
            }
            titleGroup.alpha = 1f;
            for (float t = 0; t < hold; t += Time.unscaledDeltaTime) { title.rectTransform.localScale = Vector3.one * (1f + t * 0.01f); yield return null; }
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / fadeOut) { titleGroup.alpha = 1f - t; yield return null; }
            titleGroup.alpha = 0f;
        }

        /// Retira el post-procesado y las barras poco a poco (paso al gameplay sin cortes).
        public IEnumerator Release(float seconds)
        {
            SetBars(0f);
            AllowSkip = false;
            if (volume == null) yield break;
            float w0 = volume.weight;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                if (volume == null || skipGroup == null) yield break;
                volume.weight = Mathf.Lerp(w0, 0f, t * t * (3f - 2f * t));
                skipGroup.alpha = Mathf.Min(skipGroup.alpha, 1f - t);
                yield return null;
            }
            if (volume != null) volume.weight = 0f;
            Released = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bars = Mathf.MoveTowards(bars, barsTarget, dt * 1.2f);
            float h = 1080f * 0.5f * (1f - (16f / 9f) / 2.35f);   // altura de cada barra para 2.35:1 sobre 16:9
            float e = bars * bars * (3f - 2f * bars);
            barTop.sizeDelta = new Vector2(0, h * e);
            barBottom.sizeDelta = new Vector2(0, h * e);
            if (caption != null && captionUntil > 0f && Time.unscaledTime > captionUntil) { caption.text = ""; captionUntil = 0f; }

            // Saltar: mantener Esc (o Espacio) un momento. El aviso aparece al pulsar cualquier tecla o mover el ratón.
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool holding = AllowSkip && kb != null && (kb.escapeKey.isPressed || kb.spaceKey.isPressed);
            bool anyInput = (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame));
            if (anyInput && AllowSkip) skipVisibleUntil = Time.unscaledTime + 3f;
            skipHold = holding ? skipHold + dt : Mathf.MoveTowards(skipHold, 0f, dt * 3f);
            if (skipHold >= SkipHoldSeconds) SkipRequested = true;
            skipFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(skipHold / SkipHoldSeconds), 1f);
            float want = AllowSkip && (Time.unscaledTime < skipVisibleUntil || skipHold > 0f) ? 1f : 0f;
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, want, dt * 3f);
            skipGroup.blocksRaycasts = skipGroup.alpha > 0.5f;
        }
        float skipVisibleUntil;

        void OnDestroy()
        {
            if (camData != null) camData.renderPostProcessing = hadPostProcessing;
            if (profile != null) Destroy(profile);
        }
    }
}
