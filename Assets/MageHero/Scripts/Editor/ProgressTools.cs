#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MageHero.Editor
{
    /// Atajos para probar el bloqueo de niveles sin tener que derrotar a cada jefe.
    public static class ProgressTools
    {
        [MenuItem("Tools/Mage Hero/Progreso/Reiniciar progreso (solo nivel 1 abierto)")]
        static void ResetAll() { LevelProgress.ResetProgress(); Debug.Log("[MageHero] Progreso reiniciado: solo el primer nivel está desbloqueado."); }

        [MenuItem("Tools/Mage Hero/Progreso/Desbloquear todos los niveles")]
        static void UnlockAll()
        {
            PlayerPrefs.SetInt("MageHero.UnlockedLevel", LevelMenu.Entries().Length - 1); PlayerPrefs.Save();
            Debug.Log("[MageHero] Todos los niveles desbloqueados.");
        }
    }
}
#endif
