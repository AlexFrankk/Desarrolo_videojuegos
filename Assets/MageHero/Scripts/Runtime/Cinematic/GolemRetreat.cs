using System.Collections;
using UnityEngine;

namespace MageHero
{
    /// Final del tutorial de apertura: el Gólem de Piedra ruge ante el báculo encendido y huye al fondo del bosque
    /// (derribando los árboles que encuentra en vez de atravesarlos).
    /// El combate de verdad es contra el jefe del final del camino iluminado (GolemBoss).
    /// El Espíritu del Maestro Athan asiente al joven y se desvanece en motas de luz.
    public class GolemRetreat : MonoBehaviour
    {
        StoneGolem golem;
        SpiritAthan spirit;
        Transform player;
        Vector3 forest;

        public static void Begin(StoneGolem golem, SpiritAthan spirit, Transform player, Vector3 forestPoint)
        {
            var r = new GameObject("GolemRetreat").AddComponent<GolemRetreat>();
            r.golem = golem; r.spirit = spirit; r.player = player; r.forest = forestPoint;
        }

        IEnumerator Start()
        {
            var ui = UIManager.Instance;
            ui?.SetObjective("Sigue el camino iluminado y derrota al Gólem de Piedra");
            if (player == null) { var p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; }
            if (spirit != null) StartCoroutine(SpiritFarewell());
            if (golem == null) { Destroy(gameObject); yield break; }

            golem.StopAllCoroutines();
            if (golem.GetComponent<TreeBreaker>() == null) golem.gameObject.AddComponent<TreeBreaker>();
            golem.LookAt(player.position + Vector3.up * 1.4f);
            yield return new WaitForSeconds(0.8f);
            yield return golem.Roar(1.1f);
            ui?.ShowMessage("¡El Gólem huye al fondo del bosque! Síguelo por el camino iluminado.", 4.5f);
            golem.ClearLook();

            // huye hacia los árboles del sur, alejándose del mago, y se pierde entre el polvo
            Vector3 away = golem.transform.position - player.position; away.y = 0;
            Vector3 target = forest + away.normalized * 8f;
            float t = 0;
            while (golem != null && t < 7f)
            {
                t += Time.deltaTime;
                golem.StepToward(target, 2.6f, 140f);
                if ((golem.transform.position - target).sqrMagnitude < 1f) break;
                yield return null;
            }
            if (golem != null)
            {
                golem.DustPuff(golem.transform.position + Vector3.up, 50);
                yield return new WaitForSeconds(0.2f);
                Destroy(golem.gameObject);
            }
            Destroy(gameObject, 1f);
        }

        IEnumerator SpiritFarewell()
        {
            yield return new WaitForSeconds(2.2f);
            if (spirit == null) yield break;
            spirit.Reach(null);
            spirit.Look(player.position + Vector3.up * 1.4f);
            yield return spirit.Nod(1, 0.8f);
            spirit.SetAura(0f);
            for (float t = 0; t < 2.5f && spirit != null; t += Time.deltaTime)
            {
                spirit.transform.position += Vector3.up * Time.deltaTime * 0.35f;
                yield return null;
            }
            if (spirit != null) Destroy(spirit.gameObject);
        }
    }
}
