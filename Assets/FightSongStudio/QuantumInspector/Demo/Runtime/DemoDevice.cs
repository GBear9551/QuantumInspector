using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    [DisallowMultipleComponent]
    public sealed class DemoDevice : MonoBehaviour
    {
        [SerializeField] private string deviceId;
        [SerializeField, Min(0)] private float ratedKilowatts = 2;
        [SerializeField] private Transform connectedNode;
        [SerializeField] private bool operational = true;
        public bool Operational => operational;
        public float RatedKilowatts => ratedKilowatts;
        public void Configure(string id, Transform connection, float watts = 2)
        { deviceId = id; connectedNode = connection; ratedKilowatts = watts; }
        public void SetOperational(bool value) => operational = value;
    }
}
