using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageHero
{
    /// Lleva la mecánica del puente (El Gran Cero lo derrumba → quiz para repararlo → Pergamino de Athan como pista)
    /// a TODOS los niveles que tengan puente, al cargarlos y sin tocar las escenas.
    /// Si la escena ya está configurada a mano (Level_primavera con Tools > Mage Hero > Bridge Puzzle), la respeta.
    public class BridgePuzzleBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameObject.FindWithTag("Player") == null) return;
            new GameObject("BridgePuzzleBootstrap").AddComponent<BridgePuzzleBootstrap>();
        }

        IEnumerator Start()
        {
            yield return null;                                         // que CombatBootstrap prepare mago y HUD primero
            if (FindAnyObjectByType<BridgePuzzleManager>() == null) Setup();
            Destroy(gameObject);
        }

        static void Setup()
        {
            var info = FindAnyObjectByType<LevelInfo>();
            var deck = PickDeck(info);
            if (deck == null) return;                                  // nivel sin puente (p. ej. volcán)

            // Piezas: barandas y pilotes que están sobre la cubierta.
            Vector3 half = deck.lossyScale * 0.5f + new Vector3(0.7f, 0f, 0.7f);
            var rails = new List<Transform>(); var pilings = new List<Transform>();
            if (deck.parent != null)
                foreach (Transform c in deck.parent)
                {
                    if (c.name != "BridgeRail" && c.name != "Piling") continue;
                    Vector3 d = c.position - deck.position;
                    if (Mathf.Abs(d.x) > half.x || Mathf.Abs(d.z) > half.z) continue;
                    (c.name == "BridgeRail" ? rails : pilings).Add(c);
                }

            Color water = new Color(0.25f, 0.75f, 0.85f);
            var def = FindDef(info);
            if (def != null && def.pal != null) water = World.Hex(def.pal.water);

            var root = new GameObject("PuenteDelRio");
            root.SetActive(false);                                     // Awake del manager corre con todo asignado
            root.transform.position = deck.position;
            var m = root.AddComponent<BridgePuzzleManager>();
            m.deck = deck; m.rails = rails.ToArray(); m.pilings = pilings.ToArray();
            m.gapMaterial = RuntimeMaterials.Toon(water);
            m.villainDarkMaterial = RuntimeMaterials.Glow(new Color(0.05f, 0.03f, 0.08f));
            m.villainRedMaterial = RuntimeMaterials.Glow(new Color(1f, 0.15f, 0.1f));
            m.repairGlowMaterial = RuntimeMaterials.Glow(new Color(0.5f, 1f, 1f));

            // Pergamino de Athan en una zona alcanzable sin cruzar el puente.
            Vector3 spot = PickClueSpot(info, deck.position, out string zone);
            m.clueZoneName = zone;
            root.SetActive(true);

            var clueGo = new GameObject("PergaminoDeAthan", typeof(SphereCollider));
            clueGo.SetActive(false);
            clueGo.transform.position = spot;
            var clue = clueGo.AddComponent<ClueItemSystem>();
            clue.puzzle = m;
            clue.scrollMaterial = RuntimeMaterials.Toon(new Color(0.96f, 0.9f, 0.72f));
            clue.glowMaterial = RuntimeMaterials.Glow(new Color(1f, 0.82f, 0.3f));
            clueGo.SetActive(true);

            Debug.Log($"[MageHero] Puente destructible listo en '{SceneManager.GetActiveScene().name}' ({rails.Count} barandas, {pilings.Count} pilotes). Pergamino en «{zone}».");
        }

        /// La cubierta del puente de la ruta principal (la que usa el camino al jefe); si no, la primera que haya.
        static Transform PickDeck(LevelInfo info)
        {
            var decks = new List<Transform>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "BridgeDeck") decks.Add(t);
            if (decks.Count == 0) return null;
            var def = FindDef(info);
            if (def == null || def.bridgeRoutes == null || def.bridgeRoutes.Length == 0) return decks[0];
            World.Configure(def);
            foreach (var b in World.BR)
            {
                if (b.pier) continue;
                var target = new Vector3(b.x, 0f, -b.z);
                Transform best = null; float bd = float.MaxValue;
                foreach (var d in decks) { float dist = Vector2.Distance(new Vector2(d.position.x, d.position.z), new Vector2(target.x, target.z)); if (dist < bd) { bd = dist; best = d; } }
                return best;
            }
            return decks[0];
        }

        static LevelDef FindDef(LevelInfo info)
        {
            if (info == null) return null;
            foreach (var d in LevelData.All()) if (d.id == info.levelId) return d;
            return null;
        }

        static Vector3 PickClueSpot(LevelInfo info, Vector3 bridge, out string zoneName)
        {
            zoneName = "";
            var player = GameObject.FindWithTag("Player");
            Vector3 pp = player != null ? player.transform.position : Vector3.zero;
            int water = GameLayers.Exists(GameLayers.Water) ? GameLayers.Mask(GameLayers.Water) : 0;
            float best = -1f; Vector3 bestPos = pp + new Vector3(20f, 0f, 20f);
            if (info != null && info.zones != null)
                foreach (var z in info.zones)
                {
                    var c = new Vector3(z.position.x, pp.y, z.position.y);
                    float fromPlayer = Flat(c - pp), fromBridge = Flat(c - bridge);
                    if (fromPlayer < 25f || fromPlayer > 75f || fromBridge < 40f) continue;
                    if (CrossesWater(pp, c, water)) continue;
                    foreach (var off in new[] { new Vector3(5, 0, 5), new Vector3(-5, 0, 5), new Vector3(5, 0, -5), new Vector3(-5, 0, -5) })
                    {
                        if (!RuntimeMaterials.Ground(c + off, out var g)) continue;
                        if (fromBridge > best) { best = fromBridge; bestPos = g; zoneName = z.name; }
                        break;
                    }
                }
            if (best < 0f && RuntimeMaterials.Ground(bestPos, out var fallback)) bestPos = fallback;
            return bestPos;
        }

        static bool CrossesWater(Vector3 a, Vector3 b, int waterMask)
        {
            if (waterMask == 0) return false;
            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                Vector3 p = Vector3.Lerp(a, b, t);
                if (Physics.Raycast(new Vector3(p.x, 80f, p.z), Vector3.down, 200f, waterMask, QueryTriggerInteraction.Collide)) return true;
            }
            return false;
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
