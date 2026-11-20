#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageHero.Editor
{
    /// Si el proyecto ya tiene el menú pero falta la escena del volcán (p. ej. tras hacer pull),
    /// la genera automáticamente al volver al modo edición, para que la tarjeta del menú funcione.
    [InitializeOnLoad]
    static class VolcanoAutoBuild
    {
        static VolcanoAutoBuild()
        {
            EditorApplication.delayCall += TryBuild;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryBuild; };
        }

        static void TryBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(VolcanoBuilder.ScenePath) || !File.Exists(MenuBuilder.ScenePath)) return;
            if (SessionState.GetBool("MageHero_VolcanAutoBuilt", false)) return;
            SessionState.SetBool("MageHero_VolcanAutoBuilt", true);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;
            try
            {
                EditorUtility.DisplayProgressBar("Mage Hero", "Generando el nivel del volcán (jefe)…", 0.5f);
                VolcanoBuilder.Build();
            }
            catch (System.Exception e) { Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); }
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("[MageHero] Nivel del volcán generado automáticamente y añadido a Build Profiles. Ya puedes elegirlo en el menú.");
        }
    }
}
#endif
