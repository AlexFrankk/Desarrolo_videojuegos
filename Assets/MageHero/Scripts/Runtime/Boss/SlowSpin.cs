using UnityEngine;

namespace MageHero
{
    /// Gira lentamente un objeto (humo, runas decorativas).
    public class SlowSpin : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 10f, 0f);
        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
