using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FightSong.InspectorTool
{
    public static class QuantumGraphOverviewVerification
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static object InspectView(QuantumGraphView view)
        {
            var positions=view.Document.Nodes.ToDictionary(p=>p.Key,p=>p.Value.Position);
            var member=view.Document.Nodes.Values.First(n=>n.Kind==QuantumGraphNodeKind.Component);
            member.Pinned=true;view.Select(member);view.Fit();view.Camera.Zoom=Mathf.Min(.08f,view.Camera.Zoom);view.Moved(false);
            var overview=(QuantumGraphOverview)typeof(QuantumGraphView).GetField("zoomOverview",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            var dots=(System.Collections.Generic.List<QuantumGraphDot>)typeof(QuantumGraphView).GetField("pointMarks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            int expected=view.Document.Nodes.Values.Count(n=>view.Document.Presented(n)&&!QuantumGraphOverview.Summary(n,view.Document));
            Check(dots.Count==expected,"Real fixture lost dots at overview zoom.");
            Check(dots.Any(d=>d.Node==member&&d.Selected&&d.Node.Pinned),"Selected pinned member stayed a card or lost its state.");
            Check(dots.All(d=>view.Hit(d.Point)==d.Node),"Dot hit testing did not resolve the exact component instance.");
            Check(view.VisibleCount==overview.Groups.Count,"Individual cards remained visible at distant zoom.");
            var viewport=new Rect(Vector2.zero,view.Viewport.contentRect.size);
            Check(overview.Groups.Values.All(g=>viewport.Contains(g.Bounds.min)&&g.Bounds.xMax<=viewport.xMax&&g.Bounds.yMax<=viewport.yMax),"Overview family bounds were outside the viewport.");
            var groups=overview.Groups.Values.ToArray();
            for(int i=0;i<groups.Length;i++)for(int j=i+1;j<groups.Length;j++)Check(!groups[i].Bounds.Overlaps(groups[j].Bounds),"Overview families overlap.");
            Check(positions.All(p=>view.Document.Nodes[p.Key].Position==p.Value),"Zoom changed actual node positions.");
            int cards=view.VisibleCount,count=dots.Count;
            var focus=dots.Single(d=>d.Node==member).Point;var pan=view.Camera.Pan;float zoom=view.Camera.Zoom;
            var world=view.Camera.ToWorld(focus,view.Viewport.contentRect.size);
            view.Camera.Zoom=.3f;view.Camera.Pan=focus-view.Viewport.contentRect.size*.5f-world*view.Camera.Zoom;view.Moved(false);
            Check(dots.Any(d=>d.Node==member&&Vector2.Distance(d.Point,focus)<.05f),"Zoom dropped or displaced the dot under the pointer.");
            Check(view.Hit(focus)==member,"Zoom lost exact dot hit testing.");
            view.Camera.Pan=pan;view.Camera.Zoom=zoom;view.Moved(false);
            Check(dots.Count==count,"Zoom round trip lost overview members.");
            view.DoubleClick(member);view.Tick(.033f);
            Check(view.Selected==member&&view.Camera.Zoom>=1&&view.VisibleDotCount==0,"Double-click did not restore the exact member card.");
            view.Fit();view.Camera.Zoom=Mathf.Min(.08f,view.Camera.Zoom);view.Moved(false);
            return new{passed=true,nodes=view.Document.Nodes.Count,overviewCards=cards,overviewDots=count,exactInstanceHitTesting=true,selectedPinVisible=true,nonOverlappingGroups=true,doubleClickFocus=true,positionsPreserved=true};
        }
        public static object Run()
        {
            var graph=(QuantumGraphDocument)typeof(QuantumGraphConstellationVerification).GetMethod("Synthetic",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{2000,false});
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            try
            {
                settings.GraphMotion=QuantumGraphMotionMode.Off;
                new QuantumGraphLayoutEngine().Arrange(graph,settings);new QuantumGraphMotion().Commit(graph,0,QuantumGraphMotionMode.Off);
                var pinned=graph.Nodes["member00000"];pinned.Pinned=true;pinned.Manual=true;
                var positions=graph.Nodes.ToDictionary(p=>p.Key,p=>(p.Value.Position,p.Value.Slot,p.Value.Pinned,p.Value.Expanded));
                var overview=new QuantumGraphOverview();overview.Target(.2f,QuantumGraphMotionMode.Full,1);overview.Sample(1.09);
                float middle=overview.Blend;Check(middle>0&&middle<1,"Semantic zoom did not animate.");
                overview.Target(1,QuantumGraphMotionMode.Full,1.09);Check(overview.Blend==middle,"Interrupted semantic zoom jumped.");
                overview.Sample(1.3);Check(overview.Blend==0,"Zoom-in failed to restore cards exactly.");
                overview.Target(.2f,QuantumGraphMotionMode.Full,2);overview.Sample(2.06);overview.Target(.2f,QuantumGraphMotionMode.Reduced,2.06);
                Check(overview.Blend==1,"Reduced motion did not settle immediately.");
                overview.Target(1,QuantumGraphMotionMode.Off,3);Check(overview.Blend==0,"Off did not restore detail immediately.");
                overview.Target(.2f,QuantumGraphMotionMode.Off,4);overview.Prepare(graph,14);
                foreach(var group in overview.Groups.Values)overview.Place(group,new Rect(Vector2.zero,overview.Size(group)));
                Check(overview.Points.Count==2000,"Overview omitted graph members.");
                Check(overview.Families[pinned.Id]==pinned.ClusterId,"Pinned instance lost its family.");
                foreach(var pair in overview.Points)
                {
                    var group=overview.Groups[overview.Families[pair.Key]];
                    Check(group.Bounds.Contains(pair.Value)&&pair.Value.y>group.Bounds.yMin+group.HeaderSize.y+8,"Dot escaped its family or overlapped its header.");
                }
                Check(overview.Groups.Values.All(g=>g.Members.Select(n=>overview.Points[n.Id]).Distinct().Count()==g.Members.Count),"Duplicate instances share a dot slot.");
                var points=overview.Points.ToDictionary(p=>p.Key,p=>p.Value);
                overview.Prepare(graph,14);foreach(var group in overview.Groups.Values)overview.Place(group,new Rect(Vector2.zero,overview.Size(group)));
                Check(points.All(p=>overview.Points[p.Key]==p.Value),"Dot packing was not deterministic.");
                Check(positions.All(p=>(graph.Nodes[p.Key].Position,graph.Nodes[p.Key].Slot,graph.Nodes[p.Key].Pinned,graph.Nodes[p.Key].Expanded)==p.Value),"Semantic zoom changed the saved graph layout.");
                var viewport=new Vector2(1800,1200);var camera=new QuantumGraphViewportController{Zoom=.2f};
                overview.ResetPacking();overview.Prepare(graph,14,viewport);overview.Arrange(camera,viewport,graph.Nodes.Values,graph.RootId);
                Check(overview.Points.Count==2000,"Packed overview omitted members.");
                var packed=overview.Points.ToDictionary(p=>p.Key,p=>p.Value);var focus=packed[pinned.Id];
                float originalZoom=camera.Zoom;var originalPan=camera.Pan;
                camera.ZoomAt(focus,viewport,-3);
                float ratio=camera.Zoom/originalZoom;
                overview.Prepare(graph,14,viewport);overview.Arrange(camera,viewport,graph.Nodes.Values,graph.RootId);
                Check(Vector2.Distance(overview.Points[pinned.Id],focus)<.01f,"Overview dot was not fixed under the zoom pointer.");
                Check(packed.All(p=>Vector2.Distance(overview.Points[p.Key],focus+(p.Value-focus)*ratio)<.02f),"Overview dots did not follow camera zoom.");
                var delta=new Vector2(73,-41);camera.Pan+=delta;
                overview.Prepare(graph,14,viewport);overview.Arrange(camera,viewport,graph.Nodes.Values,graph.RootId);
                Check(Vector2.Distance(overview.Points[pinned.Id],focus+delta)<.01f,"Overview pan stuck after zoom.");
                camera.Pan=originalPan;camera.Zoom=originalZoom;
                overview.Prepare(graph,14,viewport);overview.Arrange(camera,viewport,graph.Nodes.Values,graph.RootId);
                Check(packed.All(p=>Vector2.Distance(overview.Points[p.Key],p.Value)<.02f),"Zoom round trip changed packed positions.");
                var family=graph.Nodes[pinned.ClusterId];family.Expanded=false;new QuantumGraphMotion().Commit(graph,5,QuantumGraphMotionMode.Off);
                overview.Prepare(graph,14);Check(!overview.Families.ContainsKey(pinned.Id),"A deliberately collapsed family still exposed its member dots.");
                return new{passed=true,members=2000,stablePacking=true,uniqueInstanceDots=true,pinsAndLayoutPreserved=true,interruptible=true,reducedAndOff=true,collapsedMembersHidden=true};
            }
            finally{UnityEngine.Object.DestroyImmediate(settings);}
        }
    }
}
