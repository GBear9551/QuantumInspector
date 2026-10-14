using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    public sealed class DemoExperimentPreset:ScriptableObject
    {
        [SerializeField] private string experiment="Constellation inspection trial";
        [SerializeField] private GameObject fixture;
        [SerializeField] private Texture2D calibration;
        public void Configure(GameObject target,Texture2D texture){fixture=target;calibration=texture;}
    }
}
