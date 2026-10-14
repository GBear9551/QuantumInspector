using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    public sealed class DemoSensorNetwork : MonoBehaviour
    {
        [SerializeField] private DemoSensor[] chamberAndReactorSensors;
        [SerializeField] private DemoControlConsole reportingConsole;
        [SerializeField] private float averageReading;
        public void Configure(DemoSensor[] sensors, DemoControlConsole console)
        { chamberAndReactorSensors = sensors; reportingConsole = console; }
        public void Sample(float time)
        {
            float sum = 0;
            int count = 0;
            foreach (var sensor in chamberAndReactorSensors)
            {
                if (sensor == null) continue;
                sensor.Sample(time); sum += sensor.Reading; count++;
            }
            averageReading = count > 0 ? sum / count : 0;
        }
    }
}
