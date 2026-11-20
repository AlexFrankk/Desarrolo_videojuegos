using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MageHero
{
    public class MenuController : MonoBehaviour
    {
        [Tooltip("Escenas que se cargan con las teclas 1-4 (solo en el menú principal).")]
        public string[] hotkeyScenes;
        [Tooltip("Usa el menú con estilo cartoon (StyledMainMenu), que sustituye al menú generado por el editor.")]
        public bool styledMenu = true;

        /// El HUD de cada nivel también lleva un MenuController (para ReloadScene): allí NO debe aparecer el menú
        /// ni funcionar las teclas 1-3. Solo es menú principal la escena que no tiene al mago.
        public static bool IsMainMenuScene => GameObject.FindWithTag("Player") == null;

        void Awake()
        {
            if (styledMenu && IsMainMenuScene && GetComponent<Canvas>() != null && GetComponent<StyledMainMenu>() == null)
                gameObject.AddComponent<StyledMainMenu>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!IsMainMenuScene) return;                        // en un nivel: sin atajos de cambio de nivel
            // La lista guardada en la escena puede ser antigua (3 niveles): se completa con todos los escenarios del menú.
            var defs = LevelMenu.Entries();
            if (hotkeyScenes == null || hotkeyScenes.Length < defs.Length)
            {
                hotkeyScenes = new string[defs.Length];
                for (int i = 0; i < defs.Length; i++) hotkeyScenes[i] = LevelMenu.SceneName(defs[i]);
            }
            if (hotkeyScenes.Length > 0 && kb.digit1Key.wasPressedThisFrame) LoadLevel(hotkeyScenes[0]);
            if (hotkeyScenes.Length > 1 && kb.digit2Key.wasPressedThisFrame) LoadLevel(hotkeyScenes[1]);
            if (hotkeyScenes.Length > 2 && kb.digit3Key.wasPressedThisFrame) LoadLevel(hotkeyScenes[2]);
            if (hotkeyScenes.Length > 3 && kb.digit4Key.wasPressedThisFrame) LoadLevel(hotkeyScenes[3]);
        }

        /// Con el menú cartoon, la carga pasa por su pantalla de "Cargando..."; si no, carga directa.
        public void LoadLevel(string sceneName)
        {
            var styled = GetComponent<StyledMainMenu>();
            if (styled != null && styled.isActiveAndEnabled) { styled.LoadScene(sceneName); return; }
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
        public void ReloadScene() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
