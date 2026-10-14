using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    /// <summary>Repeated, passive instrumentation channels for inspecting distinct instances of the same script.</summary>
    public sealed class DemoProbe : MonoBehaviour
    {
        [SerializeField] private string channel;
        [SerializeField] private float calibration=1;
        [SerializeField] private Transform sampleTarget;
        [SerializeField] private DemoProbe upstream;
        public void Configure(string name,float gain,Transform sample,DemoProbe previous)
        {channel=name;calibration=gain;sampleTarget=sample;upstream=previous;}
    }
}
