using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MageHero
{
    /// Foco en el problema matemático durante el combate: oscurece suavemente los bordes de la pantalla,
    /// quita color a la escena y añade viñeta (sin tapar la acción de fondo, que sigue en cámara lenta).
    /// Se dibuja por debajo del HUD, así el panel del problema queda nítido encima.
    public class BossFocus : MonoBehaviour
    {
        static readonly List<BossFocus> active = new List<BossFocus>();

        Image shade;
        Volume volume;
        VolumeProfile profile;
        UniversalAdditionalCameraData camData;
        bool hadPost, ending;
        float k;
        static Texture2D vignetteTex;

        public static BossFocus Begin()
        {
            var go = new GameObject("FocoProblema");
            var f = go.AddComponent<BossFocus>();
            f.Build();
            active.Add(f);
            return f;
        }

        public static void EndAll() { foreach (var f in active.ToArray()) if (f != null) f.End(); }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -5;                                   // debajo del HUD y del panel del problema
            shade = new GameObject("Sombreado", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shade.transform.SetParent(transform, false);
            MenuStyle.Stretch(shade.rectTransform);
            shade.sprite = Sprite.Create(VignetteTexture(), new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
            shade.raycastTarget = false;
            shade.color = new Color(1, 1, 1, 0);

            var cam = Camera.main;
            if (cam != null)
            {
                camData = cam.GetUniversalAdditionalCameraData();
                hadPost = camData.renderPostProcessing;
                camData.renderPostProcessing = true;
            }
            volume = new GameObject("VolumenFoco").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true; volume.priority = 90; volume.weight = 0f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.value = -55f; ca.postExposure.value = -0.35f; ca.contrast.value = 8f;
            var vg = profile.Add<Vignette>(true);
            vg.intensity.value = 0.42f; vg.smoothness.value = 0.6f; vg.color.value = new Color(0.05f, 0.03f, 0.08f);
            volume.sharedProfile = profile;
        }

        /// Degradado radial: transparente en el centro, sombra suave (45 %) en los bordes.
        static Texture2D VignetteTexture()
        {
            if (vignetteTex != null) return vignetteTex;
            const int n = 128;
            vignetteTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2((x + .5f) / n - .5f, (y + .5f) / n - .5f).magnitude / 0.707f;
                float a = Mathf.SmoothStep(0.25f, 1f, d) * 0.55f + 0.12f;
                vignetteTex.SetPixel(x, y, new Color(0.02f, 0.02f, 0.05f, a));
            }
            vignetteTex.Apply();
            return vignetteTex;
        }

        public void End() { ending = true; }

        void Update()
        {
            k = Mathf.MoveTowards(k, ending ? 0f : 1f, Time.unscaledDeltaTime * (ending ? 3f : 2.2f));
            shade.color = new Color(1, 1, 1, k);
            volume.weight = k;
            if (ending && k <= 0f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            active.Remove(this);
            if (camData != null) camData.renderPostProcessing = hadPost;
            if (profile != null) Destroy(profile);
        }
    }
}
