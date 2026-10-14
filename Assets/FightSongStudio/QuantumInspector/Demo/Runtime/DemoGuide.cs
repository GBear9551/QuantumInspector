using UnityEngine;

namespace FightSongStudio.QuantumInspector.Demo
{
    public sealed class DemoGuide : MonoBehaviour
    {
        [SerializeField, TextArea(3, 8)] private string explorationNotes;
        [SerializeField] private Object[] relatedObjects;
        public void Configure(string notes, params Object[] objects)
        { explorationNotes = notes; relatedObjects = objects; }
    }
}
