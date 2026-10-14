using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    public sealed class DemoPowerDistributor : MonoBehaviour
    {
        [SerializeField] private DemoReactorController source;
        [SerializeField] private DemoDevice[] consumers;
        [SerializeField] private Transform[] distributionHubs;
        [SerializeField] private float suppliedKilowatts;
        public void Configure(DemoReactorController reactor, DemoDevice[] devices, Transform[] hubs)
        { source = reactor; consumers = devices; distributionHubs = hubs; }
        public void Sample()
        {
            float load = 0;
            foreach (var consumer in consumers)
                if (consumer != null && consumer.Operational) load += consumer.RatedKilowatts;
            suppliedKilowatts = source != null ? load * source.Output : 0;
        }
    }
}
