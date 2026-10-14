using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphSpatialIndex
    {
        private const float CellSize=256;
        private readonly Dictionary<Vector2Int,List<QuantumGraphNode>> cells=new Dictionary<Vector2Int,List<QuantumGraphNode>>();
        public void Clear()=>cells.Clear();
        public void Add(QuantumGraphNode node)
        {
            var rect=node.Bounds;
            for(int x=Mathf.FloorToInt(rect.xMin/CellSize);x<=Mathf.FloorToInt(rect.xMax/CellSize);x++)
                for(int y=Mathf.FloorToInt(rect.yMin/CellSize);y<=Mathf.FloorToInt(rect.yMax/CellSize);y++)
                {var key=new Vector2Int(x,y);if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<QuantumGraphNode>();list.Add(node);}
        }
        public HashSet<QuantumGraphNode> Query(Rect rect)
        {
            var found=new HashSet<QuantumGraphNode>();
            int minX=Mathf.FloorToInt(rect.xMin/CellSize),maxX=Mathf.FloorToInt(rect.xMax/CellSize),minY=Mathf.FloorToInt(rect.yMin/CellSize),maxY=Mathf.FloorToInt(rect.yMax/CellSize);
            if((long)(maxX-minX+1)*(maxY-minY+1)>cells.Count*4L)
            {foreach(var list in cells.Values)foreach(var node in list)if(node.Bounds.Overlaps(rect))found.Add(node);return found;}
            for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
                if(cells.TryGetValue(new Vector2Int(x,y),out var list))foreach(var node in list)if(node.Bounds.Overlaps(rect))found.Add(node);
            return found;
        }
        public bool Overlaps(Rect bounds,string except=null)=>Query(bounds).Any(n=>n.Id!=except&&n.Bounds.Overlaps(bounds));
    }

    public static class QuantumClusterLayoutSolver
    {
        private sealed class Neighborhood
        {
            private readonly float cell;
            private readonly Dictionary<Vector2Int,List<QuantumGraphNode>> buckets=new Dictionary<Vector2Int,List<QuantumGraphNode>>();
            public bool Limited;
            public Neighborhood(IList<QuantumGraphNode> clusters,float gap){cell=Mathf.Max(1,clusters.Count==0?1:clusters.Max(n=>n.Radius)*2+gap);}
            private Vector2Int Key(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/cell),Mathf.FloorToInt(p.y/cell));
            public void Add(QuantumGraphNode node){var key=Key(node.Position);if(!buckets.TryGetValue(key,out var list))buckets[key]=list=new List<QuantumGraphNode>();list.Add(node);}
            public int Count(QuantumGraphNode node,float gap)
            {
                var key=Key(node.Position);int count=0,checks=0;Limited=false;
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
                    if(buckets.TryGetValue(key+new Vector2Int(x,y),out var list))foreach(var other in list)
                    {if(++checks>512){Limited=true;return count;}if(Vector2.Distance(node.Position,other.Position)+1<node.Radius+other.Radius+gap)count++;}
                return count;
            }
        }
        public static float EstimatedRadius(int count,float spacing,float minimum)=>Mathf.Max(minimum,spacing*Mathf.Sqrt(Mathf.Max(0,count)/(Mathf.PI*.72f))+24);
        public static void Separate(IList<QuantumGraphNode> clusters,QuantumGraphDocument graph,float gap,HashSet<string> movable)
        {
            var placed=new Neighborhood(clusters,gap);
            var families=graph.Nodes.Values.Where(n=>n.ClusterId!=null).GroupBy(n=>n.ClusterId).ToDictionary(g=>g.Key,g=>g.ToArray());
            foreach(var cluster in clusters.OrderBy(n=>movable.Contains(n.Id)?1:0).ThenBy(n=>n.Id,StringComparer.Ordinal))
            {
                if(!cluster.Pinned&&movable.Contains(cluster.Id))
                {
                    var direction=QuantumClusterClassifier.Direction(cluster.FamilyKey??cluster.Id);
                    var original=cluster.Position;
                    for(int attempt=0;attempt<256;attempt++)
                    {if(placed.Count(cluster,gap)==0&&!placed.Limited)break;cluster.Position+=direction*Mathf.Max(40,gap);}
                    var delta=cluster.Position-original;
                    if(families.TryGetValue(cluster.Id,out var members))foreach(var member in members)member.Position+=delta;
                }
                placed.Add(cluster);
            }
        }
        public static int CountOverlaps(IList<QuantumGraphNode> clusters,float gap,out bool limited)
        {
            var neighborhood=new Neighborhood(clusters,gap);int count=0;limited=false;
            foreach(var cluster in clusters){count+=neighborhood.Count(cluster,gap);limited|=neighborhood.Limited;neighborhood.Add(cluster);}
            return count;
        }
    }
    public sealed class QuantumGraphLayoutEngine
    {
        public readonly QuantumGraphSpatialIndex Spatial=new QuantumGraphSpatialIndex();
        public int UnresolvedOverlaps {get;private set;}
        public bool SpacingCheckLimited {get;private set;}
        public double MeasureMilliseconds {get;private set;}
        public double PlacementMilliseconds {get;private set;}
        public double IndexMilliseconds {get;private set;}
        // Square spiral slots are independent of total count. Vacated slots can be reused without renumbering survivors.
        public static Vector2 SlotPoint(int slot)
        {
            if(slot<=0)return Vector2.zero;
            int ring=Mathf.CeilToInt((Mathf.Sqrt(slot+1)-1)/2),side=2*ring;
            int offset=slot-(2*ring-1)*(2*ring-1);
            if(offset<side)return new Vector2(ring,-ring+1+offset);
            if(offset<2*side)return new Vector2(ring-1-(offset-side),ring);
            if(offset<3*side)return new Vector2(-ring,ring-1-(offset-2*side));
            return new Vector2(-ring+1+(offset-3*side),-ring);
        }
        public void Arrange(QuantumGraphDocument graph,InspectorWheelSettings settings,bool force=false)
        {
            if(graph?.Root==null)return;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            foreach(var node in graph.Nodes.Values)QuantumGraphNodeSizing.Apply(node,settings);
            MeasureMilliseconds=watch.Elapsed.TotalMilliseconds;watch.Restart();
            PlaceBranches(graph,settings,force);
            var clusters=graph.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Cluster).OrderBy(n=>n.Id,StringComparer.Ordinal).ToList();
            var families=graph.Nodes.Values.Where(n=>n.ClusterId!=null&&n.Kind!=QuantumGraphNodeKind.Cluster).GroupBy(n=>n.ClusterId).ToDictionary(g=>g.Key,g=>g.OrderBy(n=>n.Id,StringComparer.Ordinal).ToList());
            var movable=new HashSet<string>();
            Spatial.Clear();
            foreach(var n in graph.Nodes.Values.Where(n=>n.Positioned&&graph.Visible(n)))Spatial.Add(n);
            foreach(var cluster in clusters)
            {
                if(!graph.Nodes.TryGetValue(cluster.ParentId??graph.RootId,out var owner))owner=graph.Root;
                bool fresh=!cluster.Positioned;
                var members=families.TryGetValue(cluster.Id,out var found)?found:new List<QuantumGraphNode>();
                bool anchored=cluster.Pinned||cluster.Manual||members.Any(n=>n.Pinned||n.Manual);
                var direction=QuantumClusterClassifier.Direction(cluster.FamilyKey??cluster.Id);
                if(fresh){cluster.Position=owner.Position+direction*(settings.GraphBaseDistance+settings.GraphMinimumClusterRadius+settings.GraphSafetyGap);cluster.Positioned=true;}
                var pitch=new Vector2(members.Count>0?members.Max(n=>n.Size.x):settings.GraphNodeWidth,members.Count>0?members.Max(n=>n.Size.y):64)+Vector2.one*settings.GraphNodeGap;
                var used=members.Where(n=>n.Slot>=0).Select(n=>n.Slot).ToHashSet();int next=0;
                foreach(var member in members)
                {
                    if(member.Slot<0){while(used.Contains(next))next++;member.Slot=next;used.Add(next);}
                    if(member.Pinned||member.Positioned&&(!force||member.Manual))continue;
                    var point=SlotPoint(member.Slot);
                    member.Position=cluster.Position+Vector2.Scale(point,pitch);
                    if(!fresh)member.Position=FreePosition(member.Position,member.Size,settings.GraphNodeGap,member.Id);
                    member.Positioned=true;Spatial.Add(member);
                }
                float previous=cluster.Radius;
                cluster.Radius=Mathf.Max(settings.GraphMinimumClusterRadius,cluster.Size.x*.5f+24);
                foreach(var member in members)cluster.Radius=Mathf.Max(cluster.Radius,Vector2.Distance(member.Position,cluster.Position)+member.Size.magnitude*.5f+24);
                bool mayMove=!anchored&&(fresh||force||!settings.GraphInvestigationLock&&cluster.Radius>previous+1);
                if(mayMove)
                {
                    float required=Mathf.Max(settings.GraphBaseDistance,owner.Size.magnitude*.5f)+cluster.Radius+settings.GraphSafetyGap;
                    float distance=Mathf.Max(required,Vector2.Distance(cluster.Position,owner.Position));
                    var destination=owner.Position+direction*distance;var delta=destination-cluster.Position;
                    cluster.Position=destination;foreach(var member in members)member.Position+=delta;
                    movable.Add(cluster.Id);
                }
            }
            QuantumClusterLayoutSolver.Separate(clusters,graph,settings.GraphSafetyGap,movable);
            Spatial.Clear();foreach(var node in graph.Nodes.Values.Where(n=>n.Positioned&&graph.Visible(n)))Spatial.Add(node);
            UnresolvedOverlaps=QuantumClusterLayoutSolver.CountOverlaps(clusters,settings.GraphSafetyGap,out bool limited);SpacingCheckLimited=limited;
            PlacementMilliseconds=watch.Elapsed.TotalMilliseconds;watch.Restart();Reindex(graph);IndexMilliseconds=watch.Elapsed.TotalMilliseconds;
        }
        private static void PlaceBranches(QuantumGraphDocument graph,InspectorWheelSettings settings,bool force)
        {
            var queue=new Queue<QuantumGraphNode>();var seen=new HashSet<string>();queue.Enqueue(graph.Root);
            foreach(var orphan in graph.Nodes.Values.Where(n=>n.Id!=graph.RootId&&n.ParentId==null))queue.Enqueue(orphan);
            while(queue.Count>0)
            {
                var parent=queue.Dequeue();if(!seen.Add(parent.Id))continue;
                var children=graph.DirectChildren(parent.Id).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
                var loose=children.Where(n=>n.ClusterId==null&&n.Kind!=QuantumGraphNodeKind.Cluster).ToArray();
                var pitch=new Vector2(loose.Length>0?loose.Max(n=>n.Size.x):settings.GraphNodeWidth,loose.Length>0?loose.Max(n=>n.Size.y):64)+Vector2.one*settings.GraphNodeGap;
                var used=loose.Where(n=>n.Slot>=0).Select(n=>n.Slot).ToHashSet();int next=0;
                foreach(var node in loose)
                {
                    if(node.Slot<0){while(used.Contains(next))next++;node.Slot=next;used.Add(next);}
                    if(node.Positioned&&(!force||node.Pinned||node.Manual))continue;
                    var direction=QuantumClusterClassifier.Direction(parent.Id);
                    node.Position=graph.Project&&parent.Kind==QuantumGraphNodeKind.Folder?
                        parent.Position+Vector2.Scale(new Vector2((node.Slot+1)%4,(node.Slot+1)/4),pitch):
                        parent.Position+direction*settings.GraphBranchLength+Vector2.Scale(SlotPoint(node.Slot),pitch);node.Positioned=true;
                }
                foreach(var child in children)queue.Enqueue(child);
            }
            foreach(var orphan in graph.Nodes.Values.Where(n=>!n.Positioned&&n.ParentId==null)){orphan.Position=graph.Root.Position+QuantumClusterClassifier.Direction(orphan.Id)*settings.GraphBranchLength;orphan.Positioned=true;}
        }
        private Vector2 FreePosition(Vector2 origin,Vector2 size,float gap,string id)
        {
            Vector2 candidate=origin;
            for(int step=1;step<=512&&Spatial.Overlaps(new Rect(candidate-(size+Vector2.one*gap)*.5f,size+Vector2.one*gap),id);step++)
            {float radius=Mathf.Sqrt(step)*(Mathf.Max(size.x,size.y)+gap)*.65f;float angle=step*2.399963f;candidate=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
            return candidate;
        }
        public void Reindex(QuantumGraphDocument graph){Spatial.Clear();if(graph==null)return;foreach(var node in graph.Nodes.Values.Where(n=>n.Positioned&&graph.Visible(n)))Spatial.Add(node);}
        public static Rect Bounds(QuantumGraphDocument graph)
        {
            bool first=true;Rect bounds=default;
            foreach(var node in graph.Nodes.Values.Where(graph.Visible))
            {
                var rect=node.Bounds;
                if(node.Kind==QuantumGraphNodeKind.Cluster&&node.Expanded)rect=Rect.MinMaxRect(Mathf.Min(rect.xMin,node.Position.x-node.Radius),Mathf.Min(rect.yMin,node.Position.y-node.Radius),Mathf.Max(rect.xMax,node.Position.x+node.Radius),Mathf.Max(rect.yMax,node.Position.y+node.Radius));
                bounds=first?rect:Rect.MinMaxRect(Mathf.Min(bounds.xMin,rect.xMin),Mathf.Min(bounds.yMin,rect.yMin),Mathf.Max(bounds.xMax,rect.xMax),Mathf.Max(bounds.yMax,rect.yMax));first=false;
            }
            return bounds;
        }
    }
    public sealed class QuantumGraphViewportController
    {
        public const float MinZoom=.005f,MaxZoom=2.5f;
        public Vector2 Pan;
        public float Zoom=1;
        public Vector2 ToScreen(Vector2 world,Vector2 size)=>size*.5f+Pan+world*Zoom;
        public Vector2 ToWorld(Vector2 screen,Vector2 size)=>(screen-size*.5f-Pan)/Zoom;
        public void ZoomAt(Vector2 mouse,Vector2 size,float delta)
        {if(!float.IsFinite(size.x)||!float.IsFinite(size.y))return;var fixedPoint=ToWorld(mouse,size);Zoom=Mathf.Clamp(Zoom*Mathf.Exp(-delta*.075f),MinZoom,MaxZoom);Pan=mouse-size*.5f-fixedPoint*Zoom;}
        public void Fit(Rect bounds,Vector2 size)
        {if(!(size.x>0)||!(size.y>0)||!float.IsFinite(bounds.width)||!float.IsFinite(bounds.height))return;Zoom=Mathf.Clamp(Mathf.Min(Mathf.Max(40,size.x-70)/Mathf.Max(1,bounds.width),Mathf.Max(40,size.y-70)/Mathf.Max(1,bounds.height)),MinZoom,1);Pan=-bounds.center*Zoom;}
        public void Center(Vector2 position)=>Pan=-position*Zoom;
    }
}
