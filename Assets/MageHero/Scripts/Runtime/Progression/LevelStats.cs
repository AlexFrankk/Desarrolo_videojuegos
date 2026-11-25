using UnityEngine;

namespace MageHero
{
    /// Tabla única de progresión (niveles 1 a 5) para el mago y los enemigos.
    /// Cambia aquí los números para equilibrar el juego: todo lo demás lee de esta clase.
    public static class LevelStats
    {
        public const int MinLevel = 1, MaxLevel = 5;

        /// Enemigos derrotados que necesita el mago para subir un nivel por combate.
        public const int KillsPerPlayerLevel = 4;
        /// Cada cuántos enemigos derrotados se fortalecen los siguientes (sube la "amenaza").
        public const int KillsPerThreatLevel = 2;

        public static int Clamp(int level) => Mathf.Clamp(level, MinLevel, MaxLevel);
        static float T(int level) => (Clamp(level) - 1) / (float)(MaxLevel - 1);      // 0 en Nv1, 1 en Nv5

        // ----------------------------------------------------------------- Mago
        /// Vida máxima del mago: 100, 125, 150, 175, 200.
        public static float PlayerMaxHealth(int level) => 100f + 25f * (Clamp(level) - 1);
        /// Multiplicador de daño de los hechizos: x1.00, x1.25, x1.50, x1.75, x2.00.
        public static float PlayerDamageMultiplier(int level) => 1f + 0.25f * (Clamp(level) - 1);

        // ----------------------------------------------------------------- Enemigos
        /// Vida: 60, 81, 102, 123, 144.
        public static float EnemyMaxHealth(int level) => 60f * (1f + 0.35f * (Clamp(level) - 1));
        /// Daño por golpe: 10, 13, 16, 19, 22.
        public static float EnemyDamage(int level) => 10f * (1f + 0.3f * (Clamp(level) - 1));
        /// Segundos entre ataques (más bajo = ataca más rápido): 1.20 → 0.82.
        public static float EnemyAttackCooldown(int level) => 1.2f * (1f - 0.08f * (Clamp(level) - 1));
        /// Velocidad de movimiento: 2.3 → 2.85.
        public static float EnemyMoveSpeed(int level) => 2.3f * (1f + 0.06f * (Clamp(level) - 1));

        /// Distancia a la que un enemigo detecta y empieza a perseguir al mago: 20 → 28 m.
        public static float EnemyDetectionRange(int level) => 20f + 2f * (Clamp(level) - 1);
        /// Daño del orbe de los enemigos a distancia (magos oscuros): 8 → 17.6.
        public static float RangedDamage(int level) => 8f * (1f + 0.3f * (Clamp(level) - 1));
        /// Segundos entre orbes: 2.6 → 1.77.
        public static float RangedCooldown(int level) => 2.6f * (1f - 0.08f * (Clamp(level) - 1));

        /// Tinte que multiplica el color del enemigo: más oscuro y sombrío (violeta apagado) a mayor nivel.
        public static Color EnemyTint(int level)
        {
            float t = T(level);
            var dark = new Color(0.30f, 0.20f, 0.36f, 1f);
            return Color.Lerp(Color.white, dark, Mathf.SmoothStep(0f, 1f, t));
        }

        /// Color del cartel "Nv X" sobre el enemigo: verde → amarillo → naranja → rojo → violeta.
        public static Color LevelColor(int level)
        {
            switch (Clamp(level))
            {
                case 1: return new Color(0.45f, 0.9f, 0.45f);
                case 2: return new Color(1f, 0.85f, 0.3f);
                case 3: return new Color(1f, 0.55f, 0.2f);
                case 4: return new Color(0.95f, 0.25f, 0.25f);
                default: return new Color(0.75f, 0.35f, 1f);
            }
        }
    }
}
