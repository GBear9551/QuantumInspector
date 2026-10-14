using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    /// <summary>Unity serializes/rebinds these object references with the window across reloads and Play Mode.
    /// Positions are only transferred after the rebound object and kind match; runtime identities remain session scoped.</summary>
    [Serializable] public sealed class QuantumGraphWindowHandoff
    {
        [Serializable] public sealed class Entry
        {
            public Object Target,Owner;
            public string Family,Property,AssetPath,OwnerPath,Id;
            public QuantumGraphNodeKind Kind;
            public Vector2 Position,OverviewOffset,Size;
            public bool Pinned,Manual,CustomSize,Expanded,Selected;
            public int Slot;
        }
        public Object Root;
        public string Folder;
        public bool Project;
        public bool Playing;
        public Vector2 Pan;
        public float Zoom;
        public List<Entry> Nodes=new List<Entry>();
        public static QuantumGraphWindowHandoff Capture(QuantumGraphView view)
        {
            var graph=view?.Document;if(graph?.Root==null)return null;
            var snapshot=new QuantumGraphWindowHandoff{Root=graph.Root.Target,Folder=graph.Root.AssetPath,Project=graph.Project,Playing=UnityEditor.EditorApplication.isPlaying,Pan=view.Camera.Pan,Zoom=view.Camera.Zoom};
            foreach(var node in graph.Nodes.Values)
            {
                QuantumGraphNode parent=null;if(node.ParentId!=null)graph.Nodes.TryGetValue(node.ParentId,out parent);Object owner=parent?.Target;
                snapshot.Nodes.Add(new Entry{Id=node.Id,Target=node.Target,Owner=owner,OwnerPath=owner==null?parent?.Id:null,Family=node.FamilyKey,Property=node.PropertyPath,AssetPath=node.AssetPath,Kind=node.Kind,Position=node.Position,OverviewOffset=node.OverviewOffset,Size=node.Size,Pinned=node.Pinned,Manual=node.Manual,CustomSize=node.CustomSize,Expanded=node.Expanded,Slot=node.Slot,Selected=view.Selection.Contains(node.Id)});
            }
            return snapshot;
        }
    }
}
