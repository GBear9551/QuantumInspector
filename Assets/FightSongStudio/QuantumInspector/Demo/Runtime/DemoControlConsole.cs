using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    public sealed class DemoControlConsole : MonoBehaviour
    {
        [SerializeField] private DemoReactorController reactor;
        [SerializeField] private DemoPowerDistributor powerGrid;
        [SerializeField] private DemoSensorNetwork sensorNetwork;
        [SerializeField] private DemoDevice[] securityCameras;
        [SerializeField] private Renderer[] holographicDisplays;
        public void Configure(DemoReactorController core, DemoPowerDistributor grid, DemoSensorNetwork network, DemoDevice[] cameras, Renderer[] displays)
        { reactor = core; powerGrid = grid; sensorNetwork = network; securityCameras = cameras; holographicDisplays = displays; }
        public void CyclePower() => reactor.SetOutput(reactor.Output > .9f ? .15f : reactor.Output < .3f ? .65f : 1);
        [ContextMenu("Demo / Cycle reactor power")] private void CycleFromInspector() { if (Application.isPlaying) CyclePower(); }
    }
}
