using UnityEngine;

namespace MageHero
{
    /// Hace que el objeto mire siempre a la cámara principal (etiquetas de ejercicio sobre los enemigos).
    /// Vive en su propio archivo para que Unity pueda guardarlo en las escenas
    /// (antes estaba dentro de UIText.cs y Unity lo quitaba al cargar: "references runtime script in scene file").
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c != null) transform.rotation = c.transform.rotation;
        }

        /// Vuelve a poner Billboard en las etiquetas del mundo que lo perdieron (escenas guardadas con la versión antigua).
        public static int RestoreOnWorldLabels()
        {
            int n = 0;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.renderMode != RenderMode.WorldSpace || !canvas.name.StartsWith("Label_")) continue;
                // El símbolo de la barriga ("Label_+", "Label_×"...) va pegado al cuerpo y no debe girar.
                if (canvas.name.Length <= "Label_".Length + 1) continue;
                if (canvas.GetComponent<Billboard>() != null) continue;
                canvas.gameObject.AddComponent<Billboard>();
                n++;
            }
            return n;
        }
    }
}
