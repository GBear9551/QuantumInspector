using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    [Serializable] public sealed class QuantumSavedNode
    {
        public string Id;
        public Vector2 Position,Size;
        public bool Manual,Pinned,Expanded,Positioned;
    }
    [Serializable] public sealed class QuantumSavedGraph
    {
        public string Key,Selected="",Search="";
        public Vector2 Pan;
        public float Zoom=1;
        public bool HasViewport;
        public List<QuantumSavedNode> Nodes=new List<QuantumSavedNode>();
    }
    [FilePath("Library/FightSong/QuantumGraphResearch.asset",FilePathAttribute.Location.ProjectFolder)]
    public sealed class QuantumGraphStatePersistence:ScriptableSingleton<QuantumGraphStatePersistence>
    {
        [SerializeField] private List<QuantumSavedGraph> graphs=new List<QuantumSavedGraph>();
        private readonly Dictionary<string,QuantumSavedGraph> transient=new Dictionary<string,QuantumSavedGraph>();
        public QuantumSavedGraph Get(string key)=>transient.TryGetValue(key,out var state)?state:graphs.FirstOrDefault(g=>g.Key==key);
        public void Store(QuantumSavedGraph state)
        {
            if(state.Key.Contains("session:")){transient[state.Key]=state;return;}
            state.Nodes.RemoveAll(n=>n.Id.Contains("session:"));
            var index=graphs.FindIndex(g=>g.Key==state.Key);if(index>=0)graphs[index]=state;else graphs.Add(state);
            Save(true);
        }
        public void Forget(string key){transient.Remove(key);graphs.RemoveAll(g=>g.Key==key);Save(true);}
    }
}
