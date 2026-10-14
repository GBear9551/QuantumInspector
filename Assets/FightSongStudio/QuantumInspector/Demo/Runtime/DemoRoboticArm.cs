using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    public sealed class DemoRoboticArm : MonoBehaviour
    {
        [SerializeField] private Transform[] joints;
        [SerializeField] private Transform toolMount;
        [SerializeField] private Transform assemblyTarget;
        [SerializeField] private DemoPowerDistributor powerSupply;
        [SerializeField] private float phase;
        private Quaternion[] restRotations;
        public void Configure(Transform[] armJoints, Transform tool, Transform target, DemoPowerDistributor power, float offset)
        { joints = armJoints; toolMount = tool; assemblyTarget = target; powerSupply = power; phase = offset; }
        private void Awake()
        {
            restRotations = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) restRotations[i] = joints[i].localRotation;
        }
        public void Animate(float time)
        {
            if (restRotations == null) return;
            for (int i = 0; i < joints.Length; i++)
                joints[i].localRotation = restRotations[i] * Quaternion.AngleAxis(Mathf.Sin(time * .55f + phase + i) * 17, i == 0 ? Vector3.up : Vector3.forward);
        }
    }
}
