using System;
using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    /// <summary>One Play Mode update drives the whole laboratory. No ExecuteAlways or Editor updates.</summary>
    public sealed class DemoLabAnimator : MonoBehaviour
    {
        [Serializable] public struct Rotor { public Transform target; public Vector3 degreesPerSecond; }
        [Serializable] public struct Pulse { public Renderer target; public Color color; public float phase; public bool blink; }
        [Serializable] public struct View { public string label; public Vector3 position; public Vector3 lookAt; }
        [SerializeField] private DemoReactorController reactor;
        [SerializeField] private DemoControlConsole console;
        [SerializeField] private DemoPowerDistributor power;
        [SerializeField] private DemoSensorNetwork sensors;
        [SerializeField] private DemoRoboticArm[] arms;
        [SerializeField] private Rotor[] rotors;
        [SerializeField] private Pulse[] pulses;
        [SerializeField] private Camera presentationCamera;
        [SerializeField] private View[] views;
        [SerializeField] private bool animate = true;
        [SerializeField] private bool showControls = true;
        private MaterialPropertyBlock properties;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private float simulationTime, nextTelemetry;
        private int viewIndex;
        private GUIStyle heading, caption, button;
        public float SimulationTime => simulationTime;
        public bool IsAnimating => animate;
        public int ViewIndex => viewIndex;
        public void SetAnimationEnabled(bool value) => animate = value;
        public void NextView() => viewIndex = (viewIndex + 1) % views.Length;
        public void Configure(DemoReactorController core, DemoControlConsole control, DemoPowerDistributor grid, DemoSensorNetwork network,
            DemoRoboticArm[] robots, Rotor[] rotating, Pulse[] glowing, Camera camera, View[] cameraViews)
        { reactor = core; console = control; power = grid; sensors = network; arms = robots; rotors = rotating; pulses = glowing; presentationCamera = camera; views = cameraViews; }
        private void Awake() => properties = new MaterialPropertyBlock();
        private void Update()
        {
            if (animate)
            {
                float delta = Time.deltaTime;
                simulationTime += delta;
                foreach (var rotor in rotors) rotor.target.Rotate(rotor.degreesPerSecond * delta * Mathf.Lerp(.2f, 1, reactor.Output), Space.Self);
                foreach (var arm in arms) arm.Animate(simulationTime);
                foreach (var pulse in pulses)
                {
                    properties.Clear();
                    float wave = pulse.blink ? (Mathf.Repeat(simulationTime + pulse.phase, 1.2f) < .35f ? 1.3f : .06f) : .8f + .4f * Mathf.Sin(simulationTime * 1.8f + pulse.phase);
                    properties.SetColor(Emission, pulse.color * wave * Mathf.Lerp(.4f, 3, reactor.Output));
                    pulse.target.SetPropertyBlock(properties);
                }
                if (simulationTime >= nextTelemetry)
                { nextTelemetry = simulationTime + .2f; reactor.Sample(simulationTime); power.Sample(); sensors.Sample(simulationTime); }
            }
            var view = views[viewIndex];
            var cameraTransform = presentationCamera.transform;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 3);
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, view.position, blend);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, Quaternion.LookRotation(view.lookAt - cameraTransform.position), blend);
        }
        private void OnGUI()
        {
            if (!showControls) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                heading.normal.textColor = new Color(.55f, .94f, 1);
                caption = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                caption.normal.textColor = new Color(.75f, .81f, .87f);
                button = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            }
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Clamp(Screen.width / 1600f, .65f, 1.6f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUI.Box(new Rect(20, 20, 390, 122), GUIContent.none);
            GUI.Label(new Rect(34, 29, 366, 28), "QUANTUM / LIVING LABORATORY", heading);
            GUI.Label(new Rect(34, 59, 360, 22), views[viewIndex].label + "  |  Quantum Inspector demo", caption);
            if (GUI.Button(new Rect(34, 96, 108, 28), animate ? "Pause systems" : "Resume systems", button)) SetAnimationEnabled(!animate);
            if (GUI.Button(new Rect(152, 96, 108, 28), "Cycle power", button)) console.CyclePower();
            if (GUI.Button(new Rect(270, 96, 124, 28), "Next viewpoint", button)) NextView();
            GUI.matrix = previous;
        }
    }
}
