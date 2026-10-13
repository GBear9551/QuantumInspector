using System;
using UnityEngine;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphVerificationFixture:ScriptableObject
    {
        public int Number=10;
        public int[] Samples=Array.Empty<int>();
        public UnityEngine.Object Reference;
        [NonSerialized] public int RuntimeOnly;
    }
}
