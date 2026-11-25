#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MageHero.Editor
{
    /// Atajos del editor para probar la cinemática de apertura (Diseños/guion_v2.md).
    public static class IntroCinematicMenu
    {
        [MenuItem("Tools/Mage Hero/Cinemática/Volver a ver la intro al entrar al Nivel 1")]
        static void ResetSeen()
        {
            PlayerPrefs.DeleteKey(IntroCinematic.SeenKey);
            PlayerPrefs.Save();
            Debug.Log("[MageHero] La cinemática de apertura se reproducirá la próxima vez que se entre a la Arboleda de Otoño.");
        }

        [MenuItem("Tools/Mage Hero/Cinemática/Marcar la intro como vista")]
        static void MarkSeen()
        {
            PlayerPrefs.SetInt(IntroCinematic.SeenKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[MageHero] Cinemática marcada como vista: el Nivel 1 empieza directamente en el juego.");
        }
    }
}
#endif
