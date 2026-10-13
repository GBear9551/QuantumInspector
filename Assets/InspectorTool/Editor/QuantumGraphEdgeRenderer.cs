using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public static class QuantumGraphEdgeRenderer
    {
        private static readonly float[] Dash={5f,4f};
        private static Color Alpha(Color color,float alpha){color.a=alpha;return color;}
        public static void Draw(MeshGenerationContext context,QuantumGraphDocument graph,QuantumGraphViewportController viewport,Vector2 size,
            InspectorWheelSettings settings,IReadOnlyList<QuantumGraphEdge> edges,IReadOnlyDictionary<string,Vector2> labels,HashSet<string> selected,float clock)
        {
            if(graph==null)return;
            var p=context.painter2D;
            foreach(var node in graph.Nodes.Values)
            {
                if(node.Kind!=QuantumGraphNodeKind.Cluster||!node.Expanded||!graph.Visible(node))continue;
                var center=viewport.ToScreen(node.Position,size);float radius=node.Radius*viewport.Zoom;
                if(center.x+radius<0||center.x-radius>size.x||center.y+radius<0||center.y-radius>size.y)continue;
                p.strokeColor=Alpha(node.Color,.35f);p.fillColor=Alpha(node.Color,.025f);p.lineWidth=1;
                p.BeginPath();p.Arc(center,radius,Angle.Degrees(0),Angle.Degrees(360));p.Fill();p.Stroke();
            }
            foreach(var edge in edges)
            {
                if(!graph.Nodes.TryGetValue(edge.From,out var from)||!graph.Nodes.TryGetValue(edge.To,out var to)||!graph.Visible(from)||!graph.Visible(to))continue;
                Vector2 a=labels.TryGetValue(from.Id,out var labelA)?labelA:viewport.ToScreen(from.DisplayPosition,size),b=labels.TryGetValue(to.Id,out var labelB)?labelB:viewport.ToScreen(to.DisplayPosition,size);
                if(Mathf.Max(a.x,b.x)<0||Mathf.Min(a.x,b.x)>size.x||Mathf.Max(a.y,b.y)<0||Mathf.Min(a.y,b.y)>size.y)continue;
                bool active=selected.Contains(from.Id)||selected.Contains(to.Id);float alpha=(active?.85f:edge.Overlay?.5f:.22f)*Mathf.Lerp(.65f,1.25f,settings.GlowStrength*.5f);
                p.strokeColor=Alpha(edge.Color,alpha);p.lineWidth=active?2:1;
                if(edge.Overlay)p.SetDashPattern(Dash);else p.SetDashPattern(System.Array.Empty<float>());
                Vector2 bend=(a+b)*.5f+new Vector2(-(b-a).y,(b-a).x)*.055f;
                p.BeginPath();p.MoveTo(a);p.QuadraticCurveTo(bend,b);p.Stroke();
                if(edge.Overlay)
                {
                    Vector2 d=(b-bend).normalized,tip=Vector2.Lerp(a,b,.7f);
                    p.BeginPath();p.MoveTo(tip-d*7+new Vector2(-d.y,d.x)*3);p.LineTo(tip);p.LineTo(tip-d*7-new Vector2(-d.y,d.x)*3);p.Stroke();
                }
                if(active&&!settings.ReducedMotion&&settings.AnimationIntensity>0)
                {
                    Vector2 particle=Vector2.Lerp(a,b,Mathf.Repeat(clock*.25f,1));p.fillColor=Alpha(edge.Color,.8f);
                    p.BeginPath();p.Arc(particle,2,Angle.Degrees(0),Angle.Degrees(360));p.Fill();
                }
            }
            p.SetDashPattern(System.Array.Empty<float>());
            foreach(var pair in labels)
            {
                var node=graph.Nodes[pair.Key];var anchor=viewport.ToScreen(node.DisplayPosition,size);
                if(Vector2.Distance(pair.Value,anchor)<8)continue;
                p.strokeColor=Alpha(node.Color,.35f);p.lineWidth=1;
                p.BeginPath();p.MoveTo(anchor);p.LineTo(pair.Value);p.Stroke();
            }
        }
    }
}
