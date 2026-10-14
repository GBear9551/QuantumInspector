using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    // Screen-space semantic zoom. This never writes graph positions, expansion, pins or identity.
    internal sealed class QuantumGraphOverview
    {
        internal sealed class Group
        {
            public QuantumGraphNode Node;
            public readonly List<QuantumGraphNode> Members=new List<QuantumGraphNode>();
            public Rect Bounds;
            public Vector2 HeaderSize;
            public int Columns;
            public float Pitch;
            public float DotRadius;
            public float Scale=1;
            public Vector2 HeaderCenter=>new Vector2(Bounds.center.x,Bounds.yMin+(8+HeaderSize.y*.5f)*Scale);
        }
        internal readonly Dictionary<string,Group> Groups=new Dictionary<string,Group>();
        internal readonly Dictionary<string,string> Families=new Dictionary<string,string>();
        internal readonly Dictionary<string,Vector2> Points=new Dictionary<string,Vector2>();
        internal readonly Dictionary<string,float> Radii=new Dictionary<string,float>();
        internal float Blend {get;private set;}
        private float from,target;
        private double started;
        private bool moving;
        private string packedKey;
        private Vector2 packedPan,packedViewport;
        private float packedZoom;
        private readonly Dictionary<string,Rect> packedBounds=new Dictionary<string,Rect>();
        internal static bool Summary(QuantumGraphNode node,QuantumGraphDocument graph)=>node.Id==graph.RootId||node.Kind==QuantumGraphNodeKind.Cluster||node.Kind==QuantumGraphNodeKind.Folder||node.Kind==QuantumGraphNodeKind.GameObject&&node.ParentId==graph.RootId;
        internal void Target(float zoom,QuantumGraphMotionMode mode,double now,bool immediate=false)
        {
            float next=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,.38f,zoom));
            if(mode!=QuantumGraphMotionMode.Full||immediate){Blend=target=next;moving=false;return;}
            if(Mathf.Abs(next-target)<.00001f)return;
            Sample(now);from=Blend;target=next;started=now;moving=true;
        }
        internal bool Sample(double now)
        {
            if(!moving)return false;
            float t=Mathf.Clamp01((float)((now-started)/.18));Blend=Mathf.Lerp(from,target,1-Mathf.Pow(1-t,3));
            if(t>=1){Blend=target;moving=false;}return true;
        }
        internal void ResetPacking(){packedKey=null;}
        internal void Prepare(QuantumGraphDocument graph,float font,Vector2 viewport=default,float dotScale=2)
        {
            Points.Clear();Radii.Clear();Families.Clear();
            foreach(var group in Groups.Values){group.Members.Clear();group.Bounds=default;}
            foreach(var node in graph.Nodes.Values)
            {
                if(!Summary(node,graph)||!graph.Presented(node))continue;
                if(!Groups.TryGetValue(node.Id,out var group))Groups[node.Id]=group=new Group();
                group.Node=node;group.HeaderSize=node.Size*QuantumGraphNodeSizing.DisplayScale(.6f,font);
            }
            foreach(var key in Groups.Keys.Where(id=>!graph.Nodes.TryGetValue(id,out var node)||!graph.Presented(node)||!Summary(node,graph)).ToArray())Groups.Remove(key);
            foreach(var node in graph.Nodes.Values)
            {
                if(Summary(node,graph)||!graph.Presented(node))continue;
                Group group=null;
                if(node.ClusterId!=null)Groups.TryGetValue(node.ClusterId,out group);
                var parent=node;int guard=0;
                while(group==null&&parent.ParentId!=null&&graph.Nodes.TryGetValue(parent.ParentId,out parent)&&++guard<=graph.Nodes.Count)Groups.TryGetValue(parent.Id,out group);
                if(group==null)continue;
                group.Members.Add(node);Families[node.Id]=group.Node.Id;
            }
            float bodyBudget=100;
            if(viewport.x>0&&viewport.y>0&&Groups.Count>0)
            {
                float width=Groups.Values.Max(g=>g.HeaderSize.x)+20,height=Groups.Values.Max(g=>g.HeaderSize.y);
                int columns=Mathf.Max(1,Mathf.FloorToInt(viewport.x/width)),rows=Mathf.CeilToInt((float)Groups.Count/columns);
                bodyBudget=Mathf.Clamp(viewport.y/rows-height-44,24,100);
            }
            foreach(var group in Groups.Values)
            {
                group.Members.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id));
                float width=Mathf.Max(72,group.HeaderSize.x-24);
                group.Columns=Mathf.Max(1,Mathf.CeilToInt(Mathf.Sqrt(group.Members.Count*width/bodyBudget)));
                int rows=Mathf.CeilToInt((float)group.Members.Count/group.Columns);
                group.Pitch=Mathf.Min(10,width/group.Columns,bodyBudget/Mathf.Max(1,rows));
                group.DotRadius=Mathf.Min(3,group.Pitch*.29f);
                float spacedPitch=Mathf.Max(group.Pitch,group.DotRadius*2*dotScale+2);
                int spacedColumns=Mathf.Max(1,Mathf.Min(group.Columns,Mathf.FloorToInt(width/spacedPitch)));
                int spacedRows=Mathf.CeilToInt((float)group.Members.Count/spacedColumns);
                if(spacedRows*spacedPitch<=bodyBudget){group.Columns=spacedColumns;group.Pitch=spacedPitch;}
            }
        }
        internal Vector2 Size(Group group)
        {
            float body=group.Members.Count==0?0:Mathf.Max(32,Mathf.CeilToInt((float)group.Members.Count/group.Columns)*group.Pitch+20);
            return group.HeaderSize+new Vector2(16,body+16);
        }
        internal void Arrange(QuantumGraphViewportController camera,Vector2 viewport,IEnumerable<QuantumGraphNode> visible,string rootId)
        {
            var groups=visible.Where(n=>Groups.ContainsKey(n.Id)).Select(n=>Groups[n.Id]).OrderBy(g=>g.Node.Id==rootId?0:1).ThenBy(g=>g.Node.Id,StringComparer.Ordinal).ToArray();
            if(groups.Length==0)return;
            float width=groups.Max(g=>Size(g).x)+4,height=groups.Max(g=>Size(g).y)+4;
            int columns=Mathf.Max(1,Mathf.FloorToInt((viewport.x+4)/width)),rows=Mathf.CeilToInt((float)groups.Length/columns);
            bool fits=columns*width-4<=viewport.x&&rows*height-4<=viewport.y;
            string key=viewport+"|"+width+"|"+height+"|"+string.Join(";",groups.Select(g=>g.Node.Id));
            if(packedKey!=key)
            {
                packedKey=key;packedPan=camera.Pan;packedViewport=viewport;packedZoom=camera.Zoom;packedBounds.Clear();
                if(fits)
                {
                    var slots=new List<Vector2>();var origin=(viewport-new Vector2(columns*width-4,rows*height-4))*.5f;
                    for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)slots.Add(origin+new Vector2(col*width+(width-4)*.5f,row*height+(height-4)*.5f));
                    foreach(var group in groups)
                    {
                        var desired=camera.ToScreen(group.Node.DrawPosition-group.Node.OverviewOffset,viewport);var slot=slots.OrderBy(p=>(p-desired).sqrMagnitude).First();slots.Remove(slot);
                        var dimensions=Size(group);packedBounds[group.Node.Id]=new Rect(slot-dimensions*.5f,dimensions);
                    }
                }
            }
            float scale=camera.Zoom/packedZoom;
            foreach(var group in groups)
            {
                if(!packedBounds.TryGetValue(group.Node.Id,out var bounds))continue;
                var center=viewport*.5f+camera.Pan+(bounds.center-packedViewport*.5f-packedPan)*scale;
                var dimensions=Size(group)*scale;bounds=new Rect(center-dimensions*.5f,dimensions);
                bounds.position+=group.Node.OverviewOffset*camera.Zoom;Place(group,bounds,scale);
            }
        }
        internal void PlaceFallback(Group group,Rect bounds,QuantumGraphViewportController camera)
        {
            float scale=camera.Zoom/packedZoom;
            var center=packedViewport*.5f+packedPan+(bounds.center-packedViewport*.5f-camera.Pan)/scale;
            packedBounds[group.Node.Id]=new Rect(center-bounds.size*.5f/scale,bounds.size/scale);
            bounds.position+=group.Node.OverviewOffset*camera.Zoom;Place(group,bounds);
        }
        internal void Place(Group group,Rect bounds,float scale=1)
        {
            group.Bounds=bounds;group.Scale=scale;
            float top=bounds.yMin+(8+group.HeaderSize.y+12)*scale;
            for(int i=0;i<group.Members.Count;i++)
            {
                int row=i/group.Columns,col=i%group.Columns;
                int columns=Mathf.Min(group.Columns,group.Members.Count-row*group.Columns);
                Points[group.Members[i].Id]=new Vector2(bounds.center.x+(col-(columns-1)*.5f)*group.Pitch*scale,top+row*group.Pitch*scale);
                Radii[group.Members[i].Id]=group.DotRadius*scale;
            }
        }
        internal QuantumGraphNode Representative(QuantumGraphNode node,QuantumGraphDocument graph)
            =>node!=null&&Families.TryGetValue(node.Id,out var id)&&graph.Nodes.TryGetValue(id,out var group)?group:node;
        internal void Draw(Painter2D p,QuantumGraphDocument graph,QuantumGraphViewportController camera,Vector2 size,InspectorWheelSettings settings)
        {
            if(Blend<=0)return;
            foreach(var group in Groups.Values)
            {
                if(group.Bounds.width<=0||group.Members.Count==0)continue;
                var tint=QuantumNebulaDrawing.Tint(group.Node,graph,settings);
                var original=camera.ToScreen(group.Node.DisplayPosition,size);
                var finalHeader=group.HeaderCenter;
                var rect=group.Bounds;rect.position+=(original-finalHeader)*(1-Blend);
                p.fillColor=QuantumNebulaDrawing.Alpha(tint,.065f*Blend);p.strokeColor=QuantumNebulaDrawing.Alpha(tint,.35f*Blend);p.lineWidth=1;
                const float radius=12;
                p.BeginPath();p.MoveTo(new Vector2(rect.xMin+radius,rect.yMin));p.LineTo(new Vector2(rect.xMax-radius,rect.yMin));
                p.Arc(new Vector2(rect.xMax-radius,rect.yMin+radius),radius,Angle.Degrees(270),Angle.Degrees(360));p.LineTo(new Vector2(rect.xMax,rect.yMax-radius));
                p.Arc(new Vector2(rect.xMax-radius,rect.yMax-radius),radius,Angle.Degrees(0),Angle.Degrees(90));p.LineTo(new Vector2(rect.xMin+radius,rect.yMax));
                p.Arc(new Vector2(rect.xMin+radius,rect.yMax-radius),radius,Angle.Degrees(90),Angle.Degrees(180));p.LineTo(new Vector2(rect.xMin,rect.yMin+radius));
                p.Arc(new Vector2(rect.xMin+radius,rect.yMin+radius),radius,Angle.Degrees(180),Angle.Degrees(270));p.ClosePath();p.Fill();p.Stroke();
            }
        }
    }
    internal struct QuantumGraphDot
    {
        internal QuantumGraphNode Node;
        internal Vector2 Point;
        internal Color Color;
        internal float Radius;
        internal bool Selected,Matched,Disabled;
        private static readonly Vector2[] circle=Enumerable.Range(0,12).Select(i=>new Vector2(Mathf.Cos(i*Mathf.PI/6),Mathf.Sin(i*Mathf.PI/6))).ToArray();
        internal static bool IsDisabled(QuantumGraphNode node)=>node.Target is Behaviour b&&!b.enabled||node.Target is Renderer r&&!r.enabled||node.Target is Collider c&&!c.enabled||node.Target is Component component&&!component.gameObject.activeInHierarchy||node.Target is GameObject go&&!go.activeInHierarchy;
        internal static void Draw(MeshGenerationContext context,List<QuantumGraphDot> dots)
        {
            for(int start=0;start<dots.Count;start+=800)
            {
                int count=Math.Min(800,dots.Count-start),vertices=0,indices=0;
                for(int i=0;i<count;i++){var d=dots[start+i];bool hollow=d.Disabled||d.Node.Kind==QuantumGraphNodeKind.Missing;vertices+=hollow?24:13;indices+=hollow?72:36;if(d.Selected||d.Matched){vertices+=24;indices+=72;}if(d.Node.Pinned){vertices+=4;indices+=6;}if(d.Node.Kind==QuantumGraphNodeKind.Missing){vertices+=8;indices+=12;}}
                var mesh=context.Allocate(vertices,indices);ushort offset=0;
                for(int i=0;i<count;i++)
                {
                    var d=dots[start+i];float radius=d.Radius;var color=d.Color;
                    if(d.Selected||d.Matched)Ring(mesh,ref offset,d.Point,radius+3,radius+1.5f,new Color(1,1,1,color.a));
                    if(d.Disabled||d.Node.Kind==QuantumGraphNodeKind.Missing)Ring(mesh,ref offset,d.Point,radius,radius*.55f,color);
                    else Disc(mesh,ref offset,d.Point,radius,color);
                    if(d.Node.Pinned)Bar(mesh,ref offset,d.Point+new Vector2(-3,radius+3),d.Point+new Vector2(3,radius+3),1.5f,color);
                    if(d.Node.Kind==QuantumGraphNodeKind.Missing)
                    {Bar(mesh,ref offset,d.Point+new Vector2(-2,-2),d.Point+new Vector2(2,2),1,color);Bar(mesh,ref offset,d.Point+new Vector2(2,-2),d.Point+new Vector2(-2,2),1,color);}
                }
            }
        }
        private static void Ring(MeshWriteData mesh,ref ushort offset,Vector2 center,float outer,float inner,Color color)
        {
            for(int i=0;i<12;i++){var a=center+circle[i]*outer;var b=center+circle[i]*inner;Vertex(mesh,a,color);Vertex(mesh,b,color);}
            for(int i=0;i<12;i++){ushort a=(ushort)(offset+i*2),b=(ushort)(offset+(i+1)%12*2);mesh.SetNextIndex(a);mesh.SetNextIndex(b);mesh.SetNextIndex((ushort)(a+1));mesh.SetNextIndex(b);mesh.SetNextIndex((ushort)(b+1));mesh.SetNextIndex((ushort)(a+1));}offset+=24;
        }
        private static void Disc(MeshWriteData mesh,ref ushort offset,Vector2 center,float radius,Color color)
        {
            Vertex(mesh,center,color);for(int i=0;i<12;i++)Vertex(mesh,center+circle[i]*radius,color);
            for(int i=0;i<12;i++){mesh.SetNextIndex(offset);mesh.SetNextIndex((ushort)(offset+i+1));mesh.SetNextIndex((ushort)(offset+(i+1)%12+1));}offset+=13;
        }
        private static void Bar(MeshWriteData mesh,ref ushort offset,Vector2 a,Vector2 b,float width,Color color)
        {
            var d=(b-a).normalized;var normal=new Vector2(-d.y,d.x)*width*.5f;
            Vertex(mesh,a-normal,color);Vertex(mesh,b-normal,color);Vertex(mesh,b+normal,color);Vertex(mesh,a+normal,color);
            mesh.SetNextIndex(offset);mesh.SetNextIndex((ushort)(offset+1));mesh.SetNextIndex((ushort)(offset+2));mesh.SetNextIndex(offset);mesh.SetNextIndex((ushort)(offset+2));mesh.SetNextIndex((ushort)(offset+3));offset+=4;
        }
        private static void Vertex(MeshWriteData mesh,Vector2 p,Color color)=>mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,UnityEngine.UIElements.Vertex.nearZ),tint=color});
    }
}
