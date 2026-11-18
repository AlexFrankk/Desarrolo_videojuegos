#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageHero.Editor
{
    /// Hace una foto de cada nivel (vista aérea en 3/4 desde el punto de inicio del mago) y la guarda en
    /// Assets/MageHero/Resources/MenuThumbs/<id>.png. El menú principal (StyledMainMenu) las muestra en la lista de escenarios.
    public static class MenuThumbnailCapture
    {
        const string Folder = AssetLib.Root + "/Resources/" + StyledMainMenu.ThumbFolder;
        const int Width = 960, Height = 590;

        [MenuItem("Tools/Mage Hero/Menu/Capturar imágenes de escenarios")]
        public static void CaptureAll()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Menú", "Sal del modo Play antes de capturar.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

            int done = 0;
            try
            {
                var defs = LevelData.All();
                for (int i = 0; i < defs.Length; i++)
                {
                    string scenePath = AssetLib.Root + "/Scenes/Level_" + defs[i].id + ".unity";
                    EditorUtility.DisplayProgressBar("Capturando escenarios", defs[i].name, (float)i / defs.Length);
                    if (!File.Exists(scenePath)) { Debug.LogWarning("[MageHero] No existe " + scenePath); continue; }
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    if (Capture(Folder + "/" + defs[i].id + ".png")) done++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
            Debug.Log($"[MageHero] {done} imágenes de escenarios guardadas en {Folder}.");
        }

        static bool Capture(string path)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[MageHero] La escena no tiene Main Camera."); return false; }
            var player = GameObject.FindWithTag("Player");
            Vector3 focus = player != null ? player.transform.position : Vector3.zero;

            // Vista aérea en 3/4, más alejada que la cámara de juego para que se vea el paisaje.
            var t = cam.transform;
            Vector3 oldPos = t.position; Quaternion oldRot = t.rotation; float oldFov = cam.fieldOfView;
            var oldTarget = cam.targetTexture;
            Quaternion rot = Quaternion.Euler(32f, -35f, 0f);
            Vector3 look = focus + Vector3.up * 1.5f;
            t.position = look - rot * Vector3.forward * 38f;
            t.rotation = Quaternion.LookRotation(look - t.position, Vector3.up);
            cam.fieldOfView = 50f;

            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return true;
            }
            finally
            {
                cam.targetTexture = oldTarget;
                t.SetPositionAndRotation(oldPos, oldRot);
                cam.fieldOfView = oldFov;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
#endif
