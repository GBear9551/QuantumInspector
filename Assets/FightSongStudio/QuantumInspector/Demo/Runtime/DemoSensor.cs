using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    [DisallowMultipleComponent]
    public sealed class DemoSensor : MonoBehaviour
    {
        [SerializeField] private Transform observedObject;
        [SerializeField] private MonoBehaviour signalSource;
        [SerializeField] private string unit = "kelvin";
        [SerializeField] private float baseline = 290;
        [SerializeField] private float reading;
        [SerializeField] private float phase;
        public float Reading => reading;
        public string Unit => unit;
        public void Configure(Transform observed, MonoBehaviour source, float offset)
        { observedObject = observed; signalSource = source; phase = offset; reading = baseline; }
        public void Sample(float time) => reading = baseline + Mathf.Sin(time * .6f + phase) * 8;
    }
}
