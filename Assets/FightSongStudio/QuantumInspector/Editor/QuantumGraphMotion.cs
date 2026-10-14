using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightSong.InspectorTool
{
    /// <summary>One elapsed-time transition for a committed snapshot. Targets remain independent of drawing.</summary>
    public sealed class QuantumGraphMotion
    {
        private struct Pose
        {
            public QuantumGraphNode Node;
            public Vector2 From,To;
            public float RadiusFrom,RadiusTo,AlphaFrom,AlphaTo;
        }
        private readonly List<Pose> poses=new List<Pose>();
        private double started;
        private double duration=Duration;
        private int generation;
        public bool Active {get;private set;}
        public const double Duration=.24;
        public void Reset(){poses.Clear();Active=false;generation=-1;}
        public void Commit(QuantumGraphDocument graph,double now,QuantumGraphMotionMode mode,bool animate=true)
        {
            Sample(now,graph.Generation);
            generation=graph.Generation;started=now;poses.Clear();
            bool spatial=animate&&mode==QuantumGraphMotionMode.Full;
            duration=Duration;
            foreach(var node in graph.Nodes.Values)
            {
                bool visible=graph.Visible(node);
                var target=node.Position;
                QuantumGraphNode cluster=null;
                if(node.ClusterId!=null)graph.Nodes.TryGetValue(node.ClusterId,out cluster);
                if(cluster!=null&&!cluster.Expanded&&!node.Pinned)target=cluster.Position;
                var from=node.HasPresentation?node.PresentedPosition:cluster!=null&&!node.Pinned?cluster.DrawPosition:target;
                float alpha=node.HasPresentation?node.Opacity:0;
                float radius=node.Kind==QuantumGraphNodeKind.Cluster&&node.Expanded?node.Radius:0;
                var pose=new Pose{Node=node,From=from,To=target,RadiusFrom=node.HasPresentation?node.PresentedRadius:radius,RadiusTo=radius,AlphaFrom=alpha,AlphaTo=visible?1:0};
                if(pose.RadiusTo>pose.RadiusFrom)duration=.32;
                node.HasPresentation=true;
                if(spatial&&(pose.From!=pose.To||pose.AlphaFrom!=pose.AlphaTo||pose.RadiusFrom!=pose.RadiusTo))poses.Add(pose);
                else {node.PresentedPosition=target;node.PresentedRadius=radius;node.Opacity=visible?1:0;}
            }
            Active=poses.Count>0;
            if(Active)Sample(now,generation);
        }
        public bool Sample(double now,int currentGeneration)
        {
            if(!Active)return false;
            if(currentGeneration!=generation){Reset();return false;}
            float t=Mathf.Clamp01((float)((now-started)/duration));
            float ease=1-(1-t)*(1-t)*(1-t);
            foreach(var pose in poses)
            {
                pose.Node.PresentedPosition=Vector2.LerpUnclamped(pose.From,pose.To,ease);
                pose.Node.PresentedRadius=Mathf.LerpUnclamped(pose.RadiusFrom,pose.RadiusTo,ease);
                pose.Node.Opacity=Mathf.LerpUnclamped(pose.AlphaFrom,pose.AlphaTo,ease);
            }
            if(t>=1){Active=false;poses.Clear();}
            return true;
        }
        public void Direct(QuantumGraphDocument graph)
        {
            Reset();
            foreach(var node in graph.Nodes.Values)
            {node.HasPresentation=true;node.PresentedPosition=node.Position;node.PresentedRadius=node.Expanded?node.Radius:0;node.Opacity=graph.Visible(node)?1:0;}
        }
        public void TakeControl(QuantumGraphDocument graph,IEnumerable<QuantumGraphNode> nodes,double now)
        {
            Sample(now,graph.Generation);
            foreach(var node in nodes)
            {
                if(node.HasPresentation&&graph.Visible(node))node.Position=node.PresentedPosition;
                poses.RemoveAll(p=>p.Node==node);
            }
            Active=poses.Count>0;
        }
    }
}
