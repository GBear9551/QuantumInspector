using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    [DisallowMultipleComponent]
    public sealed class DemoReactorController : MonoBehaviour
    {
        [SerializeField] private Transform energyCore;
        [SerializeField] private Transform[] containmentNodes;
        [SerializeField] private DemoDevice[] coolingUnits;
        [SerializeField] private DemoSensor[] monitoringSensors;
        [SerializeField, Range(0, 1)] private float output = .8f;
        [SerializeField] private float coreTemperature;
        public float Output => output;
        public void Configure(Transform core, Transform[] nodes, DemoDevice[] cooling, DemoSensor[] sensors)
        { energyCore = core; containmentNodes = nodes; coolingUnits = cooling; monitoringSensors = sensors; }
        public void SetOutput(float value) => output = Mathf.Clamp01(value);
        public void Sample(float time) => coreTemperature = 290 + output * 140 + Mathf.Sin(time) * 3;
        [ContextMenu("Demo / Standby")] private void Standby() => SetOutput(.15f);
        [ContextMenu("Demo / Full power")] private void FullPower() => SetOutput(1);
    }
}
