using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphDrawMetrics
    {
        public double Initialization,Decoration,Filaments,Points;
        internal static double Milliseconds(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
    }
    public static class QuantumGraphEdgeRenderer
    {
        private static Color Alpha(Color color,float alpha){color.a=alpha;return color;}
        private static Color Ink(Color color,float factor){color.r*=factor;color.g*=factor;color.b*=factor;return color;}
        public static void Draw(MeshGenerationContext context,QuantumGraphDocument graph,QuantumGraphViewportController viewport,Vector2 size,
            InspectorWheelSettings settings,IReadOnlyList<QuantumGraphEdge> edges,IReadOnlyDictionary<string,Vector2> labels,HashSet<string> selected,float clock,QuantumGraphDrawMetrics metrics=null,float enclosureOpacity=1,bool labelLeaders=true)
            =>Draw(context.painter2D,graph,viewport,size,settings,edges,labels,selected,clock,metrics,enclosureOpacity,labelLeaders);
        internal static void Draw(Painter2D p,QuantumGraphDocument graph,QuantumGraphViewportController viewport,Vector2 size,
            InspectorWheelSettings settings,IReadOnlyList<QuantumGraphEdge> edges,IReadOnlyDictionary<string,Vector2> labels,HashSet<string> selected,float clock,QuantumGraphDrawMetrics metrics=null,float enclosureOpacity=1,bool labelLeaders=true)
        {
            if(graph==null)return;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            bool cosmic=settings.GraphPalette==QuantumGraphPalette.CosmicNebula;
            bool detail=cosmic&&settings.GraphQuality!=QuantumGraphQuality.Low&&edges.Count<300;
            bool simple=edges.Count>=300;
            float ink=settings.GraphPalette==QuantumGraphPalette.Light||settings.GraphPalette==QuantumGraphPalette.Editor&&!UnityEditor.EditorGUIUtility.isProSkin?.55f:1;
            foreach(var node in graph.Nodes.Values)
            {
                if(enclosureOpacity<=0||node.Kind!=QuantumGraphNodeKind.Cluster||node.DrawRadius<=0||!graph.Presented(node))continue;
                var center=viewport.ToScreen(node.DrawPosition,size);float radius=node.DrawRadius*viewport.Zoom;
                if(center.x+radius<0||center.x-radius>size.x||center.y+radius<0||center.y-radius>size.y)continue;
                if(cosmic)QuantumNebulaDrawing.Enclosure(p,center,radius,QuantumNebulaDrawing.Tint(node,graph,settings),QuantumGraphIdentity.Hash(node.Id),detail,enclosureOpacity);
                else{p.strokeColor=Alpha(Ink(node.Color,ink),.35f*enclosureOpacity);p.fillColor=Alpha(node.Color,.025f*enclosureOpacity);p.lineWidth=1;p.BeginPath();p.Arc(center,radius,Angle.Degrees(0),Angle.Degrees(360));p.Fill();p.Stroke();}
            }
            foreach(var pair in labels)
            {
                if(!labelLeaders)break;
                var node=graph.Nodes[pair.Key];var anchor=viewport.ToScreen(node.DisplayPosition,size);
                if(Vector2.Distance(pair.Value,anchor)<8)continue;
                p.strokeColor=Alpha(Ink(node.Color,ink),.35f);p.lineWidth=1;
                p.BeginPath();p.MoveTo(anchor);p.LineTo(pair.Value);p.Stroke();
            }
            if(metrics!=null)metrics.Decoration=System.Math.Max(metrics.Decoration,QuantumGraphDrawMetrics.Milliseconds(started));
            started=System.Diagnostics.Stopwatch.GetTimestamp();
            float thickness=float.IsFinite(settings.GraphLinkThickness)?Mathf.Clamp(settings.GraphLinkThickness,.5f,6):2;
            p.lineCap=LineCap.Round;
            int accents=0;
            for(int overlayPass=0;overlayPass<3;overlayPass++)
            {
                if(overlayPass==1)p.SetDashPattern(1f,5f);
                if(overlayPass==2)p.SetDashPattern(5f,4f);
                foreach(var edge in edges)
                {
                    bool reference=edge.Overlay||edge.Relation==QuantumGraphRelation.SerializedReference||edge.Relation==QuantumGraphRelation.DirectDependency||edge.Relation==QuantumGraphRelation.TransitiveDependency||edge.Relation==QuantumGraphRelation.PrefabSource||edge.Relation==QuantumGraphRelation.RequiredComponent||edge.Relation==QuantumGraphRelation.ExploredReference;
                    int pass=edge.Relation==QuantumGraphRelation.Grouping?1:reference?2:0;
                    if(pass!=overlayPass)continue;
                    if(!graph.Nodes.TryGetValue(edge.From,out var from)||!graph.Nodes.TryGetValue(edge.To,out var to)||!graph.Presented(from)||!graph.Presented(to))continue;
                    Vector2 a=labels.TryGetValue(from.Id,out var labelA)?labelA:viewport.ToScreen(from.DisplayPosition,size),b=labels.TryGetValue(to.Id,out var labelB)?labelB:viewport.ToScreen(to.DisplayPosition,size);
                    if(Mathf.Max(a.x,b.x)<0||Mathf.Min(a.x,b.x)>size.x||Mathf.Max(a.y,b.y)<0||Mathf.Min(a.y,b.y)>size.y)continue;
                    if(!ClipLine(ref a,ref b,size))continue;
                    bool active=selected.Contains(from.Id)||selected.Contains(to.Id);float alpha=Mathf.Clamp01((active?.9f:edge.Overlay?.65f:.55f)*Mathf.Lerp(.85f,1.15f,settings.GlowStrength*.5f));
                    if(selected.Count>0&&!active)alpha*=.4f;
                    alpha*=Mathf.Min(from.Opacity,to.Opacity);
                    Color color=cosmic?QuantumNebulaDrawing.Tint(to,graph,settings):edge.Color;
                    p.strokeColor=Alpha(Ink(color,ink),alpha);p.lineWidth=thickness*(active?1.5f:1);
                    Vector2 bend=(a+b)*.5f+(simple?Vector2.zero:new Vector2(-(b-a).y,(b-a).x)*(edge.Relation==QuantumGraphRelation.Grouping?-.12f:.055f));
                    if(active&&detail&&accents++<24)
                    {
                        p.strokeColor=Alpha(color,.075f);p.lineWidth=thickness+5;
                        p.BeginPath();p.MoveTo(a);p.QuadraticCurveTo(bend,b);p.Stroke();
                        if(clock>0&&clock<1&&settings.EffectiveGraphMotion==QuantumGraphMotionMode.Full)
                        {
                            float t=selected.Contains(to.Id)?1-clock:clock;
                            var point=(1-t)*(1-t)*a+2*(1-t)*t*bend+t*t*b;
                            p.fillColor=Alpha(color,Mathf.Sin(clock*Mathf.PI)*.8f);p.BeginPath();p.Arc(point,2.4f,Angle.Degrees(0),Angle.Degrees(360));p.Fill();
                        }
                        p.strokeColor=Alpha(color,alpha);p.lineWidth=thickness*1.5f;
                    }
                    p.BeginPath();p.MoveTo(a);if(simple)p.LineTo(b);else p.QuadraticCurveTo(bend,b);p.Stroke();
                    if(reference||edge.Relation==QuantumGraphRelation.Contains||edge.Relation==QuantumGraphRelation.SerializedField)
                    {
                        Vector2 d=(.3f*(bend-a)+.7f*(b-bend)).normalized,tip=.09f*a+.42f*bend+.49f*b;
                        float arrow=6+p.lineWidth;p.fillColor=p.strokeColor;
                        p.BeginPath();p.MoveTo(tip-d*arrow+new Vector2(-d.y,d.x)*arrow*.45f);p.LineTo(tip);p.LineTo(tip-d*arrow-new Vector2(-d.y,d.x)*arrow*.45f);
                        if(edge.Relation==QuantumGraphRelation.DirectDependency||edge.Relation==QuantumGraphRelation.TransitiveDependency)p.LineTo(tip-d*arrow*2);
                        p.ClosePath();p.Fill();
                    }
                }
            }
            if(metrics!=null)metrics.Filaments=System.Math.Max(metrics.Filaments,QuantumGraphDrawMetrics.Milliseconds(started));
        }
        // Clip off-screen segments before dashing. A far-away endpoint must not
        // tessellate thousands of invisible dash fragments. Relationship styles remain intact.
        private static bool ClipLine(ref Vector2 a,ref Vector2 b,Vector2 size)
        {
            var direction=b-a;float low=0,high=1;
            if(!Clip(-direction.x,a.x+8,ref low,ref high)||!Clip(direction.x,size.x+8-a.x,ref low,ref high)||
               !Clip(-direction.y,a.y+8,ref low,ref high)||!Clip(direction.y,size.y+8-a.y,ref low,ref high))return false;
            b=a+direction*high;a+=direction*low;return true;
        }
        private static bool Clip(float p,float q,ref float low,ref float high)
        {
            if(Mathf.Abs(p)<.0001f)return q>=0;
            float r=q/p;if(p<0){if(r>high)return false;low=Mathf.Max(low,r);}else{if(r<low)return false;high=Mathf.Min(high,r);}return true;
        }
    }
}
