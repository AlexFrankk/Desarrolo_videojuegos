namespace MageHero
{
    /// Escenarios que aparecen en el menú principal: los 3 niveles generados desde LevelData
    /// más los niveles hechos a mano (por ahora, el Cráter de Obsidiana con el jefe).
    /// LevelData es autogenerado: no añadir aquí niveles a LevelData.All() (los builders lo recorren).
    public static class LevelMenu
    {
        public static LevelDef Volcan() => new LevelDef
        {
            id = "volcan", name = "Cráter de Obsidiana", sub = "Volcán: el jefe te espera",
            lv = "Jefe", topic = "Combate final",
            desc = "Un cráter de lava donde despierta el Elemental de Obsidiana.",
            tags = new[] { "Jefe", "Lava", "Erupciones" },
            accent = "#ff6a1a",
            sky = 0x2a0f0c,
            pal = new PaletteDef { path = 0x3a2622 },
        };

        public static LevelDef[] Entries()
        {
            var a = LevelData.All();
            var all = new LevelDef[a.Length + 1];
            a.CopyTo(all, 0);
            all[a.Length] = Volcan();
            return all;
        }

        public static string SceneName(LevelDef d) => "Level_" + d.id;
    }
}
