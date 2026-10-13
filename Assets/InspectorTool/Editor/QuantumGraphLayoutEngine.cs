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
        public static float EstimatedRadius(int count,float spacing,float minimum)=>Mathf.Max(minimum,spacing*Mathf.Sqrt(Mathf.Max(0,count)/Mathf.PI));
        public static void Separate(IList<QuantumGraphNode> clusters,QuantumGraphDocument graph,float gap,HashSet<string> movable)
        {
            var placed=new List<QuantumGraphNode>();
            foreach(var cluster in clusters.OrderByDescending(n=>n.Pinned||!movable.Contains(n.Id)).ThenBy(n=>n.Id,StringComparer.Ordinal))
            {
                if(!cluster.Pinned&&movable.Contains(cluster.Id))
                {
                    var root=graph.Nodes[cluster.ParentId];var direction=(cluster.Position-root.Position).normalized;
                    if(direction.sqrMagnitude<.01f)direction=Vector2.right;
                    var original=cluster.Position;
                    float step=Mathf.Max(40,gap);
                    while(placed.Any(other=>Vector2.Distance(cluster.Position,other.Position)<cluster.Radius+other.Radius+gap))cluster.Position+=direction*step;
                    var delta=cluster.Position-original;
                    foreach(var member in graph.Nodes.Values.Where(n=>n.ClusterId==cluster.Id&&!n.Pinned))member.Position+=delta;
                }
                placed.Add(cluster);
            }
        }
    }
    public sealed class QuantumGraphLayoutEngine
    {
        public readonly QuantumGraphSpatialIndex Spatial=new QuantumGraphSpatialIndex();
        public void Arrange(QuantumGraphDocument graph,InspectorWheelSettings settings,bool force=false)
        {
            if(graph?.Root==null)return;
            Spatial.Clear();foreach(var n in graph.Nodes.Values.Where(n=>n.Positioned&&graph.Visible(n)))Spatial.Add(n);
            foreach(var n in graph.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.GameObject&&!n.Positioned&&graph.Visible(n)))
            {n.Position=FreePosition(graph.Root.Position+Vector2.right*settings.GraphBranchLength,n.Size,settings.GraphNodeGap,n.Id);n.Positioned=true;Spatial.Add(n);}
            var clusters=graph.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Cluster&&graph.Visible(n)).ToList();
            clusters=clusters.OrderBy(n=>Enum.TryParse<QuantumClusterCategory>(n.Label,out var c)?(int)c:100).ThenBy(n=>n.Id,StringComparer.Ordinal).ToList();
            var previousRadii=clusters.ToDictionary(n=>n.Id,n=>n.Radius);
            var automatic=new List<QuantumGraphNode>();
            foreach(var cluster in clusters)
            {
                var members=graph.Nodes.Values.Where(n=>n.ClusterId==cluster.Id&&graph.Visible(n)).OrderBy(n=>n.Id,StringComparer.Ordinal).ToList();
                if(!cluster.Expanded){cluster.Radius=cluster.Size.magnitude*.5f;}
                if((!cluster.Positioned||force)&&!cluster.Pinned&&!members.Any(n=>n.Pinned))
                {
                    float width=members.Count>0?members.Max(n=>n.Size.x):settings.GraphNodeWidth;
                    float height=members.Count>0?members.Max(n=>n.Size.y):64;
                    int columns=Math.Max(1,Mathf.CeilToInt(Mathf.Sqrt(members.Count*(height+settings.GraphNodeGap)/(width+settings.GraphNodeGap))));
                    int rows=Mathf.CeilToInt((float)members.Count/columns);
                    float radius=QuantumClusterLayoutSolver.EstimatedRadius(members.Count,Mathf.Sqrt((width+settings.GraphNodeGap)*(height+settings.GraphNodeGap)),settings.GraphMinimumClusterRadius);
                    radius=Mathf.Max(radius,new Vector2(columns*(width+settings.GraphNodeGap),rows*(height+settings.GraphNodeGap)).magnitude*.5f+20);
                    cluster.Radius=cluster.Expanded?radius:cluster.Size.magnitude*.5f;
                    uint hash=QuantumGraphIdentity.Hash(cluster.Id);float angle=(hash%360)*Mathf.Deg2Rad;
                    var siblings=clusters.Where(n=>n.ParentId==cluster.ParentId).ToList();
                    angle=(siblings.IndexOf(cluster)*360f/Mathf.Max(1,siblings.Count)-90)*Mathf.Deg2Rad;
                    var parent=graph.Nodes[cluster.ParentId];
                    cluster.Position=parent.Position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(settings.GraphBaseDistance+cluster.Radius+settings.GraphSafetyGap);
                    cluster.Positioned=true;
                    for(int i=0;i<members.Count;i++)
                    {
                        var member=members[i];if(member.Pinned||(!force&&member.Positioned))continue;
                        member.Position=cluster.Position+new Vector2((i%columns-(columns-1)*.5f)*(width+settings.GraphNodeGap),(i/columns-(rows-1)*.5f)*(height+settings.GraphNodeGap));
                        member.Positioned=true;
                    }
                    automatic.Add(cluster);
                }
                else if(cluster.Expanded&&members.Any(n=>n.Positioned))
                    cluster.Radius=Mathf.Max(settings.GraphMinimumClusterRadius,members.Where(n=>n.Positioned).Max(n=>Vector2.Distance(n.Position,cluster.Position)+n.Size.magnitude*.5f)+16);
            }
            if(automatic.Count>0)QuantumClusterLayoutSolver.Separate(clusters,graph,settings.GraphSafetyGap,automatic.Select(n=>n.Id).ToHashSet());
            Spatial.Clear();
            foreach(var node in graph.Nodes.Values.Where(n=>n.Positioned&&graph.Visible(n)))Spatial.Add(node);
            var ranks=new Dictionary<string,int>();
            foreach(var family in graph.Nodes.Values.Where(n=>n.Kind!=QuantumGraphNodeKind.Cluster).GroupBy(n=>n.ParentId??""))
            {int ordinal=0;foreach(var child in family)ranks[child.Id]=ordinal++;}
            foreach(var node in graph.Nodes.Values.Where(n=>graph.Visible(n)&&(!n.Positioned||(force&&n.Kind!=QuantumGraphNodeKind.Cluster&&n.ClusterId==null&&!n.Pinned&&n.Id!=graph.RootId))).ToArray())
            {
                var parent=node.ParentId!=null&&graph.Nodes.TryGetValue(node.ParentId,out var found)?found:graph.Root;
                int rank=ranks.TryGetValue(node.Id,out int index)?index:0;
                float angle=(QuantumGraphIdentity.Hash(parent.Id)%360)*Mathf.Deg2Rad+rank*2.399963f;
                float variation=1+(QuantumGraphIdentity.Hash(node.Id+"length")%1000/1000f-.5f)*.3f;
                float spacing=Mathf.Sqrt((node.Size.x+settings.GraphNodeGap)*(node.Size.y+settings.GraphNodeGap));
                float distance=settings.GraphBranchLength*variation+spacing*Mathf.Sqrt(rank)*.85f;
                var origin=parent.Position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*distance;
                node.Position=FreePosition(origin,node.Size,settings.GraphNodeGap,node.Id);node.Positioned=true;Spatial.Add(node);
            }
            foreach(var cluster in clusters.Where(n=>n.Expanded))
            {
                var members=graph.Nodes.Values.Where(n=>n.ClusterId==cluster.Id&&graph.Visible(n)).ToArray();
                if(members.Length>0)cluster.Radius=Mathf.Max(cluster.Radius,members.Max(n=>Vector2.Distance(n.Position,cluster.Position)+n.Size.magnitude*.5f)+16);
            }
            var moving=automatic.Select(n=>n.Id).ToHashSet();
            if(!settings.GraphInvestigationLock)
                foreach(var cluster in clusters.Where(n=>!n.Manual&&!n.Pinned&&n.Radius>previousRadii[n.Id]+1))
                    if(!graph.Nodes.Values.Any(n=>n.ClusterId==cluster.Id&&(n.Manual||n.Pinned)))moving.Add(cluster.Id);
            if(moving.Count>0)QuantumClusterLayoutSolver.Separate(clusters,graph,settings.GraphSafetyGap,moving);
            Reindex(graph);
        }
        private Vector2 FreePosition(Vector2 origin,Vector2 size,float gap,string id)
        {
            int step=0;Vector2 candidate=origin;
            while(Spatial.Overlaps(new Rect(candidate-(size+Vector2.one*gap)*.5f,size+Vector2.one*gap),id))
            {step++;float radius=Mathf.Sqrt(step)*(Mathf.Max(size.x,size.y)+gap)*.65f;float angle=step*2.399963f;candidate=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
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
        public Vector2 Pan;
        public float Zoom=1;
        public Vector2 ToScreen(Vector2 world,Vector2 size)=>size*.5f+Pan+world*Zoom;
        public Vector2 ToWorld(Vector2 screen,Vector2 size)=>(screen-size*.5f-Pan)/Zoom;
        public void ZoomAt(Vector2 mouse,Vector2 size,float delta)
        {if(!float.IsFinite(size.x)||!float.IsFinite(size.y))return;var fixedPoint=ToWorld(mouse,size);Zoom=Mathf.Clamp(Zoom*Mathf.Exp(-delta*.075f),.025f,2.5f);Pan=mouse-size*.5f-fixedPoint*Zoom;}
        public void Fit(Rect bounds,Vector2 size)
        {if(!(size.x>0)||!(size.y>0)||!float.IsFinite(bounds.width)||!float.IsFinite(bounds.height))return;Zoom=Mathf.Clamp(Mathf.Min(Mathf.Max(40,size.x-70)/Mathf.Max(1,bounds.width),Mathf.Max(40,size.y-70)/Mathf.Max(1,bounds.height)),.025f,1);Pan=-bounds.center*Zoom;}
        public void Center(Vector2 position)=>Pan=-position*Zoom;
    }
}
