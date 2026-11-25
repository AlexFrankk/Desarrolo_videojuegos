using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    /// Decorado de la cinemática de apertura en la Arboleda de Otoño: la cabaña del mago, el tocón con el hacha,
    /// la pila de leña, la roca con musgo del Maestro Athan, el báculo apoyado en la pared y las flores.
    /// Se construye por código al cargar el nivel (sobrevive a que se regenere la escena con los builders).
    /// Queda en el nivel después de la cinemática: es la casa del mago.
    public class IntroSet : MonoBehaviour
    {
        // --- Marcas del decorado (coordenadas del mundo, Arboleda de Otoño): prado entre el camino Este y el bosque del sur.
        public static readonly Vector2 StumpXZ = new Vector2(28f, -11f);
        public static readonly Vector2 CabinXZ = new Vector2(34.6f, -13.2f);
        public static readonly Vector2 RockXZ = new Vector2(24.6f, -13.6f);
        public static readonly Vector2 PileXZ = new Vector2(31.2f, -10.3f);
        public static readonly Vector2 GolemFromXZ = new Vector2(26.0f, -23.5f);
        public static readonly Vector2 GolemStopXZ = new Vector2(27.0f, -16.6f);
        public static readonly Vector2 FallTreeXZ = new Vector2(26.5f, -20.9f);

        public Transform cabin, stump, pile, rock, staffProp, axe, flowersOnPath;
        public Transform logWhole, logHalfA, logHalfB;
        public readonly List<Transform> pileExtra = new List<Transform>();
        public Vector3 stumpTop, pileTop, spiritSeat, mageMark, mageFacing, staffGrip, golemFrom, golemStop, doorPoint;
        public Transform fallTree;

        static readonly Color Wood = Hex(0x8a5a34), WoodDark = Hex(0x5e3b21), WoodLight = Hex(0xd9b07a), Bark = Hex(0x6b4528);
        static readonly Color Roof = Hex(0x9b3d26), RoofDark = Hex(0x74291a), Stone = Hex(0x8f8a84), StoneDark = Hex(0x6b6763);
        static readonly Color Moss = Hex(0x6e9b38), Metal = Hex(0x9aa3ab), MetalDark = Hex(0x5d646b);

        public static Color Hex(int rgb) => MenuStyle.Hex(rgb);
        static Material M(Color c) => RuntimeMaterials.Toon(c);

        public static IntroSet Build()
        {
            var go = new GameObject("IntroSet_CabanaDelMago");
            var set = go.AddComponent<IntroSet>();
            set.BuildAll();
            return set;
        }

        static Vector3 G(Vector2 xz, float lift = 0f)
        {
            Vector3 p = new Vector3(xz.x, 50f, xz.y);
            if (!RuntimeMaterials.Ground(p, out Vector3 hit))
                if (Physics.Raycast(new Vector3(xz.x, 80f, xz.y), Vector3.down, out RaycastHit h, 200f)) hit = h.point; else hit = new Vector3(xz.x, 2f, xz.y);
            return hit + Vector3.up * lift;
        }

        void BuildAll()
        {
            BuildCabin();
            BuildStumpAndAxe();
            BuildPile();
            BuildRock();
            BuildStaffProp();
            BuildFlowers();

            Vector3 s = G(StumpXZ);
            mageMark = s + new Vector3(0f, 0f, -0.95f);          // el mago corta de frente al norte, con el tocón delante
            mageFacing = Vector3.forward;
            golemFrom = G(GolemFromXZ);
            golemStop = G(GolemStopXZ);
            fallTree = NearestTree(FallTreeXZ, 3f);
        }

        // ------------------------------------------------------------------ cabaña
        void BuildCabin()
        {
            Vector3 basePos = G(CabinXZ, -0.1f);
            cabin = new GameObject("Cabana").transform;
            cabin.SetParent(transform, false);
            cabin.position = basePos;
            cabin.rotation = Quaternion.Euler(0f, -90f, 0f);     // la puerta (local +Z) mira al oeste, hacia el tocón

            const float W = 4.4f, D = 3.6f, H = 2.3f, r = 0.17f;
            int rows = Mathf.RoundToInt(H / (r * 1.8f));
            var walls = new LowPoly().Paint(Wood);
            var wallsDark = new LowPoly().Paint(WoodDark);
            for (int i = 0; i < rows; i++)
            {
                float y = 0.25f + i * r * 1.8f;
                var lp = (i % 2 == 0) ? walls : wallsDark;
                // Trasera y laterales completas; delantera con hueco para la puerta (x de -0.55 a 0.55, hasta 1.9 m).
                Log(lp, new Vector3(-W / 2 - .3f, y, -D / 2), new Vector3(W / 2 + .3f, y, -D / 2), r);
                Log(lp, new Vector3(-W / 2, y + r * .9f, -D / 2 - .3f), new Vector3(-W / 2, y + r * .9f, D / 2 + .3f), r);
                bool window = y > 0.9f && y < 1.7f;
                if (window)
                {   // ventana en el lateral derecho: dos tramos
                    Log(lp, new Vector3(W / 2, y + r * .9f, -D / 2 - .3f), new Vector3(W / 2, y + r * .9f, -0.45f), r);
                    Log(lp, new Vector3(W / 2, y + r * .9f, 0.55f), new Vector3(W / 2, y + r * .9f, D / 2 + .3f), r);
                }
                else Log(lp, new Vector3(W / 2, y + r * .9f, -D / 2 - .3f), new Vector3(W / 2, y + r * .9f, D / 2 + .3f), r);
                if (y < 1.9f)
                {
                    Log(lp, new Vector3(-W / 2 - .3f, y, D / 2), new Vector3(-0.6f, y, D / 2), r);
                    Log(lp, new Vector3(0.6f, y, D / 2), new Vector3(W / 2 + .3f, y, D / 2), r);
                }
                else Log(lp, new Vector3(-W / 2 - .3f, y, D / 2), new Vector3(W / 2 + .3f, y, D / 2), r);
            }
            walls.Spawn("Muros", cabin, M(Wood));
            wallsDark.Spawn("MurosOscuros", cabin, M(WoodDark));

            float top = 0.25f + rows * r * 1.8f;
            // Hastiales (triángulos de madera bajo el tejado), delante y detrás.
            var gable = new LowPoly().Paint(WoodLight);
            float gh = 1.35f;
            foreach (float z in new[] { -D / 2, D / 2 })
            {
                float zz = z + Mathf.Sign(z) * 0.02f;
                Vector3 a = new Vector3(-W / 2 - .1f, top, zz), b = new Vector3(W / 2 + .1f, top, zz), c = new Vector3(0, top + gh, zz);
                if (z > 0) gable.Tri(a, c, b); else gable.Tri(a, b, c);
            }
            gable.Spawn("Hastiales", cabin, M(Hex(0xb98a55)));

            // Tejado a dos aguas (cumbrera paralela a X: las aguas caen hacia delante y hacia atrás).
            float slope = Mathf.Atan2(gh, D / 2) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(gh * gh + D * D / 4f) + 0.55f;
            var roof = new LowPoly().Paint(Roof);
            for (int s = -1; s <= 1; s += 2)
            {
                // Tejas: tres franjas por agua con un pequeño escalón (aspecto de tablillas).
                for (int k = 0; k < 4; k++)
                {
                    float f = (k + 0.5f) / 4f;
                    Vector3 c = new Vector3(0, top + gh - f * gh * (len / (len - 0.55f)) + 0.12f, s * f * (D / 2 + 0.45f));
                    roof.At(c, Quaternion.Euler(s * slope, 0, 0), Vector3.one).Box(new Vector3(W + 1.0f, 0.12f + (k % 2) * 0.03f, len / 4f + 0.08f));
                }
            }
            roof.Spawn("Tejado", cabin, M(Roof));
            var ridge = new LowPoly().Paint(RoofDark);
            ridge.At(new Vector3(0, top + gh + 0.1f, 0), Quaternion.Euler(45, 0, 0), Vector3.one).Box(new Vector3(W + 1.1f, 0.22f, 0.22f));
            ridge.Spawn("Cumbrera", cabin, M(RoofDark));

            // Chimenea de piedra en el lateral izquierdo.
            var chim = new LowPoly().Paint(Stone);
            for (int i = 0; i < 9; i++)
                chim.At(new Vector3(-W / 2 - 0.35f, 0.3f + i * 0.42f, -0.3f + ((i % 2) * 0.06f)), Quaternion.Euler(0, i * 23, 0), Vector3.one).Rock(0.42f, 30 + i, 0.15f, 0);
            chim.At(new Vector3(-W / 2 - 0.35f, 3.95f, -0.3f), Quaternion.identity, new Vector3(1, 0.5f, 1)).Box(new Vector3(0.75f, 0.5f, 0.75f));
            chim.Spawn("Chimenea", cabin, M(Stone));

            // Puerta, marco y escalón.
            var door = new LowPoly().Paint(WoodDark);
            door.At(new Vector3(0, 1.0f, D / 2 + 0.02f)).Box(new Vector3(1.1f, 1.9f, 0.08f));
            door.Spawn("Puerta", cabin, M(Hex(0x4a2f1b)));
            var frame = new LowPoly().Paint(WoodLight);
            frame.At(new Vector3(-0.62f, 1.0f, D / 2 + 0.06f)).Box(new Vector3(0.12f, 2.05f, 0.14f));
            frame.At(new Vector3(0.62f, 1.0f, D / 2 + 0.06f)).Box(new Vector3(0.12f, 2.05f, 0.14f));
            frame.At(new Vector3(0, 2.05f, D / 2 + 0.06f)).Box(new Vector3(1.4f, 0.14f, 0.16f));
            frame.At(new Vector3(0, 0.1f, D / 2 + 0.45f)).Box(new Vector3(1.6f, 0.2f, 0.7f));
            frame.Spawn("Marco", cabin, M(Hex(0xb98a55)));
            var knob = new LowPoly().Paint(Metal);
            knob.At(new Vector3(0.36f, 1.0f, D / 2 + 0.08f)).Rock(0.05f, 3, 0f, 0);
            knob.Spawn("Pomo", cabin, M(Hex(0xd8b04a)));
            doorPoint = cabin.TransformPoint(new Vector3(0, 0, D / 2 + 1.2f));

            // Ventana con luz cálida del interior.
            var win = new LowPoly().Paint(Color.white);
            win.At(new Vector3(W / 2 + 0.02f, 1.35f, 0.05f)).Box(new Vector3(0.05f, 0.75f, 0.95f));
            var wgo = win.Spawn("VentanaLuz", cabin, CineGlow.Make(new Color(1.6f, 0.95f, 0.45f), 0f, 1f, 0.05f), default, false);
            var wf = new LowPoly().Paint(WoodLight);
            wf.At(new Vector3(W / 2 + 0.07f, 1.35f, 0.05f)).Box(new Vector3(0.06f, 0.08f, 1.05f));
            wf.At(new Vector3(W / 2 + 0.07f, 1.35f, 0.05f)).Box(new Vector3(0.06f, 0.85f, 0.08f));
            wf.At(new Vector3(W / 2 + 0.1f, 0.93f, 0.05f)).Box(new Vector3(0.22f, 0.08f, 1.15f));
            wf.Spawn("MarcoVentana", cabin, M(Hex(0xb98a55)));

            // Barril y banco junto a la puerta.
            var barrel = new LowPoly().Paint(Wood);
            barrel.At(new Vector3(1.55f, 0f, D / 2 + 0.6f)).Prism(8, 0.33f, 0.36f, 0.85f);
            barrel.Spawn("Barril", cabin, M(Hex(0x7a4c2b)));
            var bands = new LowPoly().Paint(MetalDark);
            bands.At(new Vector3(1.55f, 0.18f, D / 2 + 0.6f)).Prism(8, 0.355f, 0.37f, 0.06f);
            bands.At(new Vector3(1.55f, 0.66f, D / 2 + 0.6f)).Prism(8, 0.37f, 0.355f, 0.06f);
            bands.Spawn("Aros", cabin, M(MetalDark));
        }

        static void Log(LowPoly lp, Vector3 a, Vector3 b, float r, int sides = 6)
        {
            Vector3 d = b - a;
            lp.At(a, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one).Prism(sides, r, r * 0.95f, d.magnitude);
        }

        // ------------------------------------------------------------------ tocón, tronco y hacha
        void BuildStumpAndAxe()
        {
            Vector3 s = G(StumpXZ, -0.05f);
            stump = new GameObject("Tocon").transform;
            stump.SetParent(transform, false);
            stump.position = s;
            var bark = new LowPoly().Paint(Bark);
            bark.Prism(8, 0.5f, 0.42f, 0.55f);
            // raíces
            for (int i = 0; i < 4; i++)
                bark.At(Vector3.zero, Quaternion.Euler(0, i * 90 + 20, 62), Vector3.one).Prism(5, 0.14f, 0.04f, 0.6f, true);
            bark.Spawn("Corteza", stump, M(Bark));
            var top = new LowPoly().Paint(WoodLight);
            top.At(new Vector3(0, 0.55f, 0)).Prism(8, 0.43f, 0.43f, 0.02f);
            top.Spawn("Anillos", stump, M(WoodLight));
            stumpTop = s + Vector3.up * 0.57f;

            // Tronco que se parte (entero + dos mitades ocultas).
            logWhole = SplitLog("TroncoEntero", 1f, stumpTop);
            logHalfA = SplitLog("MitadA", 0.5f, stumpTop + new Vector3(-0.09f, 0, 0));
            logHalfB = SplitLog("MitadB", 0.5f, stumpTop + new Vector3(0.09f, 0, 0));
            logHalfB.rotation = Quaternion.Euler(0, 180, 0);
            logHalfA.gameObject.SetActive(false); logHalfB.gameObject.SetActive(false);

            // Astillas por el suelo.
            var chips = new LowPoly().Paint(WoodLight);
            var rng = new System.Random(5);
            for (int i = 0; i < 22; i++)
            {
                float a = (float)rng.NextDouble() * 6.28f, d = 0.6f + (float)rng.NextDouble() * 1.1f;
                chips.At(new Vector3(Mathf.Cos(a) * d, 0.04f, Mathf.Sin(a) * d), Quaternion.Euler(0, a * 57f, 0), Vector3.one)
                     .Box(new Vector3(0.18f, 0.03f, 0.06f));
            }
            chips.Spawn("Astillas", stump, M(WoodLight), default, false);

            // Hacha clavada en el tocón: la hoja hundida en la madera y el mango en diagonal hacia arriba.
            axe = BuildAxe(transform);
            axe.rotation = Quaternion.Euler(0, 70, 152);
            axe.position = stumpTop + new Vector3(0.18f, -0.06f, 0.05f) - axe.rotation * new Vector3(0.15f, 0.82f, 0f);
        }

        Transform SplitLog(string name, float fraction, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.position = at;
            var lp = new LowPoly().Paint(Bark);
            const float r = 0.22f, h = 0.5f;
            if (fraction >= 1f) lp.Prism(7, r, r, h);
            else
            {   // media luna: medio prisma + cara plana clara
                int sides = 7;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = Mathf.PI / 2 + i / (float)sides * Mathf.PI, a1 = Mathf.PI / 2 + (i + 1) / (float)sides * Mathf.PI;
                    Vector3 b0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), b1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                    lp.Quad(b0, b0 + Vector3.up * h, b1 + Vector3.up * h, b1);
                    lp.Tri(Vector3.zero, b0, b1);
                    lp.Tri(Vector3.up * h, b1 + Vector3.up * h, b0 + Vector3.up * h);
                }
                lp.Paint(WoodLight);
                lp.Quad(new Vector3(0, 0, -r), new Vector3(0, h, -r), new Vector3(0, h, r), new Vector3(0, 0, r));
            }
            var go = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(t, false);
            go.GetComponent<MeshFilter>().sharedMesh = lp.Build(name);
            go.GetComponent<MeshRenderer>().sharedMaterial = VertexToon();
            return t;
        }

        static Material vertexToon;
        /// Toon con color de vértice (para piezas de dos colores en una sola malla).
        public static Material VertexToon()
        {
            if (vertexToon != null) return vertexToon;
            var sh = Shader.Find("MageHero/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            vertexToon = new Material(sh) { name = "toon_vertexcolor" };
            if (vertexToon.HasProperty("_BaseColor")) vertexToon.SetColor("_BaseColor", Color.white);
            if (vertexToon.HasProperty("_ShadeColor")) vertexToon.SetColor("_ShadeColor", new Color(0.62f, 0.6f, 0.7f));
            if (vertexToon.HasProperty("_UseVertexColor")) vertexToon.SetFloat("_UseVertexColor", 1f);
            return vertexToon;
        }

        /// Hacha (mango + cabeza). El pivote está en el extremo del mango (donde se agarra).
        public static Transform BuildAxe(Transform parent)
        {
            var t = new GameObject("Hacha").transform;
            t.SetParent(parent, false);
            var lp = new LowPoly().Paint(Hex(0x9a6a3c));
            lp.Prism(5, 0.035f, 0.03f, 0.9f);
            lp.Paint(MetalDark);
            lp.At(new Vector3(0.07f, 0.82f, 0)).Box(new Vector3(0.2f, 0.14f, 0.05f));
            lp.Paint(Metal);
            lp.At(new Vector3(0.2f, 0.82f, 0)).Box(new Vector3(0.08f, 0.24f, 0.035f));
            var go = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(t, false);
            go.GetComponent<MeshFilter>().sharedMesh = lp.Build("Hacha");
            go.GetComponent<MeshRenderer>().sharedMaterial = VertexToon();
            return t;
        }

        // ------------------------------------------------------------------ pila de leña
        void BuildPile()
        {
            Vector3 p = G(PileXZ);
            pile = new GameObject("PilaDeLena").transform;
            pile.SetParent(transform, false);
            pile.position = p;
            pile.rotation = Quaternion.Euler(0, 8, 0);
            // 7 troncos (4 abajo + 3 encima) y 3 que el mago añade en la escena 1 (2 + 1) → 10.
            Vector3[] slots =
            {
                new Vector3(-0.42f, .13f, 0), new Vector3(-0.14f, .13f, 0), new Vector3(0.14f, .13f, 0), new Vector3(0.42f, .13f, 0),
                new Vector3(-0.28f, .37f, 0), new Vector3(0f, .37f, 0), new Vector3(0.28f, .37f, 0),
                new Vector3(-0.14f, .61f, 0), new Vector3(0.14f, .61f, 0), new Vector3(0f, .85f, 0),
            };
            for (int i = 0; i < slots.Length; i++)
            {
                var log = PileLog("Lena_" + (i + 1), pile, slots[i], i);
                if (i >= 7) { pileExtra.Add(log); log.gameObject.SetActive(false); }
            }
            pileTop = pile.TransformPoint(new Vector3(0, 1.75f, 0));
        }

        Transform PileLog(string name, Transform parent, Vector3 lp, int seed)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = lp;
            t.localRotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, seed * 37, 0);   // tumbado a lo largo de Z, girado sobre su eje
            var m = new LowPoly().Paint(Bark);
            m.At(new Vector3(0, -0.4f, 0)).Prism(6, 0.13f, 0.13f, 0.8f, false);
            m.Paint(WoodLight);
            m.At(new Vector3(0, -0.4f, 0), Quaternion.Euler(180, 0, 0), Vector3.one).Prism(6, 0.13f, 0.13f, 0.001f, true);
            m.At(new Vector3(0, 0.4f, 0)).Prism(6, 0.13f, 0.13f, 0.001f, true);
            var go = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(t, false);
            go.GetComponent<MeshFilter>().sharedMesh = m.Build(name);
            go.GetComponent<MeshRenderer>().sharedMaterial = VertexToon();
            return t;
        }

        /// Tronco suelto (el que el mago carga en brazos), mismo aspecto que los de la pila.
        public Transform LooseLog(string name) => PileLog(name, transform, Vector3.zero, 3);

        // ------------------------------------------------------------------ roca del maestro
        void BuildRock()
        {
            Vector3 p = G(RockXZ, 0.15f);
            rock = new GameObject("RocaDelMaestro").transform;
            rock.SetParent(transform, false);
            rock.position = p;
            var stone = new LowPoly().Paint(Stone);
            stone.At(Vector3.zero, Quaternion.Euler(0, 20, 0), new Vector3(1.5f, 0.8f, 1.15f)).Rock(0.75f, 77, 0.2f);
            stone.Paint(StoneDark);
            stone.At(new Vector3(0.9f, -0.15f, 0.5f), Quaternion.identity, new Vector3(0.7f, 0.5f, 0.6f)).Rock(0.5f, 12, 0.25f, 0);
            var go = new GameObject("Piedra", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(rock, false);
            go.GetComponent<MeshFilter>().sharedMesh = stone.Build("Roca");
            go.GetComponent<MeshRenderer>().sharedMaterial = VertexToon();
            var moss = new LowPoly().Paint(Moss);
            moss.At(new Vector3(-0.1f, 0.32f, 0), Quaternion.Euler(0, 40, 0), new Vector3(1.25f, 0.32f, 0.95f)).Rock(0.72f, 78, 0.18f);
            moss.Spawn("Musgo", rock, M(Moss));
            spiritSeat = p + Vector3.up * 1.05f;
        }

        // ------------------------------------------------------------------ báculo apoyado en la cabaña
        void BuildStaffProp()
        {
            var player = GameObject.FindWithTag("Player");
            staffProp = new GameObject("BaculoApoyado").transform;
            staffProp.SetParent(transform, false);
            // Junto a la puerta, al otro lado de la pila: apoyado en la pared delantera (local +Z de la cabaña).
            staffProp.position = cabin.TransformPoint(new Vector3(-1.25f, 0.05f, 1.95f));
            staffProp.rotation = cabin.rotation * Quaternion.Euler(-14f, 0f, 0f);
            if (player != null)
            {
                var armR = FindDeep(player.transform, "Arm_R");
                if (armR != null)
                {
                    foreach (var n in new[] { "Staff", "Crystal" })
                    {
                        var src = FindDeep(armR, n);
                        if (src == null) continue;
                        var copy = Instantiate(src.gameObject, staffProp);
                        copy.name = n;
                        copy.transform.localPosition = src.localPosition;
                        copy.transform.localRotation = src.localRotation;
                        copy.transform.localScale = src.localScale;
                    }
                    // El bastón del mago cuelga de la mano: lo subimos para que la base toque el suelo.
                    var staffR = staffProp.GetComponentInChildren<Renderer>();
                    if (staffR != null)
                    {
                        float minY = float.MaxValue;
                        foreach (var r in staffProp.GetComponentsInChildren<Renderer>()) minY = Mathf.Min(minY, r.bounds.min.y);
                        staffProp.position += Vector3.up * (staffProp.position.y - minY);
                    }
                }
            }
            staffGrip = staffProp.position + Vector3.up * 1.0f;
        }

        // ------------------------------------------------------------------ flores
        void BuildFlowers()
        {
            Color[] cols = { Hex(0xff8fb8), Hex(0xfff2f5), Hex(0xffd23f), Hex(0xc98bff), Hex(0xff6f5e) };
            // Alrededor de la cabaña y en el camino del Gólem (que las aplasta en la escena 4).
            Patch("FloresCabana", G(CabinXZ + new Vector2(-2.6f, 2.3f)), 2.2f, 26, cols, 11);
            Patch("FloresCabana2", G(CabinXZ + new Vector2(-2.4f, -2.6f)), 1.6f, 18, cols, 12);
            Patch("FloresRoca", G(RockXZ + new Vector2(0.8f, 1.4f)), 1.4f, 14, cols, 13);
            flowersOnPath = Patch("FloresDelSendero", G(new Vector2(26.6f, -18.4f)), 1.8f, 30, cols, 14);
        }

        Transform Patch(string name, Vector3 center, float radius, int count, Color[] cols, int seed)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.position = center;
            var rng = new System.Random(seed);
            var stems = new LowPoly().Paint(Hex(0x4f8a2a));
            var petals = new LowPoly();
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * 6.28f, d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                Vector3 p = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                float h = 0.18f + (float)rng.NextDouble() * 0.2f;
                stems.At(p).Prism(3, 0.015f, 0.01f, h, false);
                petals.Paint(cols[rng.Next(cols.Length)]);
                petals.At(p + Vector3.up * h, Quaternion.Euler(0, a * 57f, 0), new Vector3(1, 0.5f, 1)).Rock(0.075f, seed * 100 + i, 0.1f, 0);
            }
            stems.Spawn("Tallos", t, M(Hex(0x4f8a2a)), default, false);
            var pg = new GameObject("Petalos", typeof(MeshFilter), typeof(MeshRenderer));
            pg.transform.SetParent(t, false);
            pg.GetComponent<MeshFilter>().sharedMesh = petals.Build(name);
            var pr = pg.GetComponent<MeshRenderer>(); pr.sharedMaterial = VertexToon(); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        // ------------------------------------------------------------------ utilidades
        static Transform NearestTree(Vector2 xz, float maxDist)
        {
            var trees = GameObject.Find("World/Trees");
            if (trees == null) return null;
            Transform best = null; float bd = maxDist;
            foreach (Transform c in trees.transform)
            {
                float d = Vector2.Distance(new Vector2(c.position.x, c.position.z), xz);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }
    }
}
