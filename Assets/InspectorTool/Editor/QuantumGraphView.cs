using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphView:VisualElement,IDisposable
    {
        public readonly VisualElement Viewport=new VisualElement();
        public readonly QuantumGraphViewportController Camera=new QuantumGraphViewportController();
        public readonly QuantumGraphLayoutEngine Layout=new QuantumGraphLayoutEngine();
        public readonly QuantumGraphDataProvider Provider=new QuantumGraphDataProvider();
        public QuantumGraphDocument Document=>Provider.Document;
        public readonly HashSet<string> Selection=new HashSet<string>();
        public QuantumGraphNode Selected=>selectedId!=null&&Document!=null&&Document.Nodes.TryGetValue(selectedId,out var node)?node:null;
        private readonly Dictionary<string,QuantumGraphNodeView> shown=new Dictionary<string,QuantumGraphNodeView>();
        private readonly Stack<QuantumGraphNodeView> pool=new Stack<QuantumGraphNodeView>();
        private readonly Dictionary<string,Vector2> screenAnchors=new Dictionary<string,Vector2>();
        private readonly List<QuantumGraphEdge> visibleEdges=new List<QuantumGraphEdge>();
        private HashSet<QuantumGraphNode> inView=new HashSet<QuantumGraphNode>();
        private readonly HashSet<string> matches=new HashSet<string>();
        private readonly Action<QuantumGraphNode> select,navigate;
        private readonly Action<Object> bookmark;
        private readonly Action<bool> entireChanged,componentAction;
        private readonly Action<Action> configure;
        private readonly Action<string> status;
        private readonly Label progress=new Label();
        private readonly WheelSearchField search=new WheelSearchField();
        private readonly Toggle investigation=new Toggle("Investigation Lock"),entire=new Toggle("Entire Project Graph");
        private InspectorWheelSettings settings;
        private string selectedId,query="",contextKey;
        private bool dirtyState,initialFit,paused,restoreSelection;
        private float clock;
        private double lastSave;
        public QuantumGraphView(InspectorWheelSettings settings,Action<QuantumGraphNode> select,Action<QuantumGraphNode> navigate,Action<Object> bookmark,
            Action<bool> entireChanged,Action<bool> componentAction,Action<Action> configure,Action<string> status)
        {
            this.settings=settings;this.select=select;this.navigate=navigate;this.bookmark=bookmark;this.entireChanged=entireChanged;this.componentAction=componentAction;this.configure=configure;this.status=status;
            AddToClassList("quantum-graph-workspace");
            var toolbar=new VisualElement();toolbar.AddToClassList("graph-toolbar");Add(toolbar);
            investigation.tooltip="Keep this root while selecting or editing nodes. Manual positions, panning and zoom remain available.";
            investigation.RegisterValueChangedCallback(e=>configure(()=>settings.GraphInvestigationLock=e.newValue));toolbar.Add(investigation);
            entire.RegisterValueChangedCallback(e=>entireChanged(e.newValue));toolbar.Add(entire);
            var actions=new VisualElement();actions.AddToClassList("graph-toolbar");Add(actions);
            Button(actions,"Fit Entire Graph",Fit);Button(actions,"Center selected",CenterSelected);
            Button(actions,"Auto Layout",()=>AutoLayout(false));Button(actions,"Reset Layout",()=>AutoLayout(true));
            var clusters=new ToolbarMenu{text="Clusters"};
            clusters.menu.AppendAction("Expand all loaded clusters",_=>SetClusters(true));clusters.menu.AppendAction("Collapse all clusters",_=>SetClusters(false));
            clusters.menu.AppendAction("Expand next folder level",_=>{foreach(var n in Document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Folder&&Document.Visible(n)&&!n.Loaded).ToArray())Provider.Expand(n);Changed();});actions.Add(clusters);
            var overlays=new ToolbarMenu{text="Overlays"};
            foreach(QuantumGraphOverlays kind in new[]{QuantumGraphOverlays.SerializedReferences,QuantumGraphOverlays.AssetDependencies,QuantumGraphOverlays.PrefabSources})
            {var captured=kind;overlays.menu.AppendAction(kind.ToString(),_=>configure(()=>settings.GraphOverlays^=captured),_=>(settings.GraphOverlays&captured)!=0?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);}
            actions.Add(overlays);
            var pause=new Toggle("Pause expansion");pause.RegisterValueChangedCallback(e=>paused=e.newValue);actions.Add(pause);
            var searchRow=new VisualElement();searchRow.AddToClassList("graph-toolbar");Add(searchRow);
            search.name="graphSearch";search.AddToClassList("graph-search");search.tooltip="Highlight names, aliases, classes and paths in the discovered graph. Hidden descendants are revealed by Next match.";
            search.RegisterCallback<ChangeEvent<string>>(e=>{query=e.newValue;UpdateMatches();Changed();});searchRow.Add(search);
            Button(searchRow,"Next match",NextMatch);progress.AddToClassList("graph-progress");searchRow.Add(progress);
            Viewport.AddToClassList("quantum-graph-viewport");Viewport.name="graphViewport";Viewport.focusable=true;Add(Viewport);
            Viewport.generateVisualContent+=Draw;Viewport.RegisterCallback<GeometryChangedEvent>(_=>{if(initialFit&&!Provider.Busy)Fit();UpdateViews();});
            Viewport.AddManipulator(new QuantumGraphManipulator(this));
            Viewport.RegisterCallback<KeyDownEvent>(e=>
            {
                if(e.keyCode==KeyCode.F){Fit();e.StopPropagation();}
                else if(e.keyCode==KeyCode.Return&&Selected!=null){Toggle(Selected);e.StopPropagation();}
                else if(e.keyCode==KeyCode.Escape){Selection.Clear();selectedId=null;Changed();}
            });
        }
        private static void Button(VisualElement parent,string text,Action action)=>parent.Add(new Button(action){text=text});
        public void Bind(WheelScope scope,bool wholeProject,InspectorWheelSettings value)
        {
            settings=value;investigation.SetValueWithoutNotify(settings.GraphInvestigationLock);
            entire.EnableInClassList("quantum-hidden",scope.Kind!=WheelScopeKind.ProjectFolder);entire.SetValueWithoutNotify(wholeProject);
            string key=QuantumGraphDataProvider.KeyFor(scope,wholeProject,settings);
            if(contextKey==key)return;
            SaveState();contextKey=key;Provider.Open(scope,wholeProject,settings);Selection.Clear();selectedId=null;
            var saved=QuantumGraphStatePersistence.instance.Get(Document.Key);query=saved?.Search??"";search.SetValueWithoutNotify(query);
            Camera.Pan=saved?.Pan??Vector2.zero;Camera.Zoom=saved?.Zoom??1;initialFit=saved==null||!saved.HasViewport;
            if(!float.IsFinite(Camera.Zoom)||!float.IsFinite(Camera.Pan.x)||!float.IsFinite(Camera.Pan.y)){Camera.Pan=Vector2.zero;Camera.Zoom=1;initialFit=true;}
            selectedId=saved?.Selected;restoreSelection=!string.IsNullOrEmpty(selectedId);if(restoreSelection)Selection.Add(selectedId);
            Layout.Arrange(Document,settings);UpdateMatches();UpdateViews();dirtyState=true;
            if(initialFit&&!Provider.Busy)Fit();
        }
        public void Configure(InspectorWheelSettings value)
        {settings=value;investigation.SetValueWithoutNotify(settings.GraphInvestigationLock);Provider.Configure(value);if(Document!=null){Layout.Arrange(Document,settings);UpdateMatches();UpdateViews();}}
        public void Refresh(bool projectChanged=false)
        {Provider.Refresh(projectChanged);if(Document!=null){Layout.Arrange(Document,settings);UpdateMatches();UpdateViews();}}
        public void Tick(float dt)
        {
            if(Document==null)return;
            clock+=settings.ReducedMotion?0:dt*settings.AnimationIntensity;
            if(!paused&&Provider.Tick())
            {
                Layout.Arrange(Document,settings);UpdateMatches();dirtyState=true;
                if(initialFit&&!Provider.Busy)Fit();
            }
            if(restoreSelection&&Selected!=null){restoreSelection=false;select(Selected);}
            UpdateViews();
            if(dirtyState&&!Provider.Busy&&EditorApplication.timeSinceStartup-lastSave>2)SaveState();
        }
        public void Fit()
        {
            if(Document==null||!(Viewport.contentRect.width>20)||!(Viewport.contentRect.height>20))return;
            Camera.Fit(QuantumGraphLayoutEngine.Bounds(Document),Viewport.contentRect.size);initialFit=false;Changed();
        }
        public void CenterSelected(){if(Selected==null)return;Camera.Zoom=Mathf.Max(.75f,Camera.Zoom);Camera.Center(Selected.DisplayPosition);Changed();}
        public void AutoLayout(bool reset)
        {
            if(Document==null)return;
            if(reset)foreach(var node in Document.Nodes.Values){node.Pinned=node.Manual=false;node.Positioned=node.Id==Document.RootId;if(node.Id==Document.RootId)node.Position=Vector2.zero;}
            Layout.Arrange(Document,settings,true);Fit();Changed();
        }
        public void SetClusters(bool expanded)
        {
            if(Document==null)return;
            foreach(var cluster in Document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Cluster))cluster.Expanded=expanded;
            Layout.Arrange(Document,settings);Changed();
        }
        public void Toggle(QuantumGraphNode node)
        {
            if(node==null)return;
            if(node.Expanded&&node.Loaded)Provider.Collapse(node);else Provider.Expand(node);
            Layout.Arrange(Document,settings);Changed();
        }
        public void Select(QuantumGraphNode node,bool additive=false)
        {
            if(node==null){if(!additive){Selection.Clear();selectedId=null;}Changed();return;}
            restoreSelection=false;
            if(!additive)Selection.Clear();
            if(additive&&Selection.Contains(node.Id)){Selection.Remove(node.Id);selectedId=Selection.FirstOrDefault();}
            else{Selection.Add(node.Id);selectedId=node.Id;select(node);}
            Changed();
        }
        public void DoubleClick(QuantumGraphNode node)
        {
            if(node==null)return;
            if(!settings.GraphInvestigationLock&&(node.Kind==QuantumGraphNodeKind.Folder||node.Kind==QuantumGraphNodeKind.GameObject))navigate(node);
            else Toggle(node);
        }
        public QuantumGraphNode Hit(Vector2 local)
        {
            var world=Camera.ToWorld(local,Viewport.contentRect.size);
            return Layout.Spatial.Query(new Rect(world-Vector2.one*2,Vector2.one*4)).Where(n=>n.Bounds.Contains(world)).OrderBy(n=>n.Kind==QuantumGraphNodeKind.Cluster?1:0).FirstOrDefault();
        }
        public HashSet<QuantumGraphNode> DragSet(QuantumGraphNode node)
        {
            var items=new HashSet<QuantumGraphNode>();
            foreach(var id in Selection.Contains(node.Id)?Selection:new HashSet<string>{node.Id})
            {
                if(!Document.Nodes.TryGetValue(id,out var selected))continue;items.Add(selected);
                if(selected.Kind==QuantumGraphNodeKind.Cluster)
                {
                    foreach(var item in Document.Nodes.Values.Where(n=>n.ClusterId==selected.Id))items.Add(item);
                    foreach(var item in Document.Descendants(selected.Id))items.Add(item);
                }
                else if(selected.Kind==QuantumGraphNodeKind.Folder)
                    foreach(var item in Document.Descendants(selected.Id))items.Add(item);
            }
            return items;
        }
        public void Moved(bool nodes=true){initialFit=false;if(nodes)Layout.Reindex(Document);Changed();}
        public void Changed(){dirtyState=true;UpdateViews();}
        private void UpdateMatches()
        {
            matches.Clear();if(Document==null)return;
            var terms=(query??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(ComponentDiscoveryService.Normalize).ToArray();
            if(terms.Length==0)return;
            foreach(var node in Document.Nodes.Values)if(terms.All(t=>(node.SearchText??"").Contains(t)))matches.Add(node.Id);
        }
        private void NextMatch()
        {
            if(matches.Count==0)return;var ids=matches.OrderBy(x=>x,StringComparer.Ordinal).ToList();int index=ids.IndexOf(selectedId);
            var node=Document.Nodes[ids[(index+1)%ids.Count]];var ancestor=node;
            while(ancestor.ParentId!=null&&Document.Nodes.TryGetValue(ancestor.ParentId,out ancestor))ancestor.Expanded=true;
            if(node.ClusterId!=null&&Document.Nodes.TryGetValue(node.ClusterId,out var cluster))cluster.Expanded=true;
            Layout.Arrange(Document,settings);Select(node);CenterSelected();
        }
        private void Menu(QuantumGraphNode node,ContextualMenuPopulateEvent e)
        {
            e.menu.AppendAction("Inspect",_=>Select(node));
            if(node.CanExpand)e.menu.AppendAction(node.Expanded?"Collapse":"Expand",_=>Toggle(node));
            e.menu.AppendAction(node.Pinned?"Unpin position":"Pin position",_=>{node.Pinned=!node.Pinned;Changed();});
            e.menu.AppendAction("Enlarge node",_=>{node.Size*=1.2f;Layout.Reindex(Document);Changed();});
            e.menu.AppendAction("Restore node size",_=>{node.Size=new Vector2(settings.GraphNodeWidth,64);Layout.Reindex(Document);Changed();});
            if(node.Kind==QuantumGraphNodeKind.GameObject||node.Kind==QuantumGraphNodeKind.Folder)
                e.menu.AppendAction("Make graph root",_=>navigate(node),_=>settings.GraphInvestigationLock?DropdownMenuAction.Status.Disabled:DropdownMenuAction.Status.Normal);
            if(node.Kind==QuantumGraphNodeKind.GameObject)
            {
                e.menu.AppendAction("Show child GameObjects",_=>{Provider.RevealHierarchy(node);Layout.Arrange(Document,settings);Changed();});
                if(node.Id==Document.RootId){e.menu.AppendAction("Add component",_=>componentAction(true));e.menu.AppendAction("Remove component",_=>componentAction(false));}
            }
            if(node.Kind==QuantumGraphNodeKind.Cluster||node.Kind==QuantumGraphNodeKind.Folder)return;
            e.menu.AppendAction("Reveal serialized references",_=>{Provider.RevealReferences(node,QuantumGraphOverlays.SerializedReferences);Changed();});
            if(!string.IsNullOrEmpty(node.AssetPath))e.menu.AppendAction("Reveal direct asset dependencies",_=>{Provider.RevealReferences(node,QuantumGraphOverlays.AssetDependencies);Changed();});
            e.menu.AppendAction("Reveal prefab source",_=>{Provider.RevealReferences(node,QuantumGraphOverlays.PrefabSources);Changed();});
            e.menu.AppendAction("Open native Inspector",_=>{var target=Provider.Resolve(node);if(target!=null)EditorUtility.OpenPropertyEditor(target);});
            e.menu.AppendAction("Ping in Unity",_=>{var target=Provider.Resolve(node);if(target!=null)EditorGUIUtility.PingObject(target);});
            e.menu.AppendAction("Bookmark",_=>{var target=Provider.Resolve(node);if(target!=null)bookmark(target);});
            if(node.Target is Component component&&node.Kind==QuantumGraphNodeKind.Component)e.menu.AppendAction("Rename alias / appearance",_=>ComponentAliasEditor.Open(component));
        }
        private void UpdateViews()
        {
            if(Document==null||!(Viewport.contentRect.width>1)||!(Viewport.contentRect.height>1)||!float.IsFinite(Camera.Zoom))return;
            var size=Viewport.contentRect.size;var top=Camera.ToWorld(new Vector2(-120,-120),size);var bottom=Camera.ToWorld(size+Vector2.one*120,size);
            inView=Layout.Spatial.Query(Rect.MinMaxRect(top.x,top.y,bottom.x,bottom.y));var keep=new HashSet<string>();
            screenAnchors.Clear();var labelRects=new List<Rect>();
            string selectedCluster=Selected?.Kind==QuantumGraphNodeKind.Cluster?Selected.Id:Selected?.ClusterId;
            foreach(var node in inView.OrderBy(n=>n.Id==Document.RootId?0:n.Kind==QuantumGraphNodeKind.Cluster?1:2).ThenBy(n=>n.Id,StringComparer.Ordinal))
            {
                if(!Document.Visible(node))continue;
                bool summary=node.Kind==QuantumGraphNodeKind.Cluster||node.Id==Document.RootId||((node.Kind==QuantumGraphNodeKind.Folder||node.Kind==QuantumGraphNodeKind.GameObject)&&node.ParentId==Document.RootId);
                if(Camera.Zoom<.3f&&!summary&&!Selection.Contains(node.Id))continue;
                float zoom=summary?Mathf.Max(.6f,Camera.Zoom):Camera.Zoom;
                var anchor=Camera.ToScreen(node.DisplayPosition,size);
                // At overview scale, summary labels remain legible while individual nodes become points.
                // This pass offsets labels only; saved graph positions and Unity transforms are untouched.
                if(Camera.Zoom<.3f)
                {
                    var dimensions=node.Size*zoom+Vector2.one*8;var candidate=anchor;int attempt=0;
                    while((labelRects.Any(r=>r.Overlaps(new Rect(candidate-dimensions*.5f,dimensions)))||candidate.x<dimensions.x*.5f||candidate.x>size.x-dimensions.x*.5f||candidate.y<dimensions.y*.5f||candidate.y>size.y-dimensions.y*.5f)&&attempt<64)
                    {attempt++;float angle=attempt*2.399963f;candidate=anchor+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Mathf.Sqrt(attempt)*24;}
                    if(attempt>=64&&node.Id!=Document.RootId&&!Selection.Contains(node.Id))continue;
                    anchor=candidate;labelRects.Add(new Rect(anchor-dimensions*.5f,dimensions));
                }
                screenAnchors[node.Id]=anchor;keep.Add(node.Id);
                if(!shown.TryGetValue(node.Id,out var view))
                {view=pool.Count>0?pool.Pop():new QuantumGraphNodeView(Toggle,Menu,status);shown[node.Id]=view;Viewport.Add(view);}
                view.Bind(node,anchor,zoom,settings.GraphFontSize,Selection.Contains(node.Id),matches.Contains(node.Id),!string.IsNullOrEmpty(query),node.Id==selectedCluster);
                view.EnableInClassList("graph-compact",zoom<.65f);
            }
            foreach(var key in shown.Keys.Where(k=>!keep.Contains(k)).ToArray()){var view=shown[key];view.RemoveFromHierarchy();pool.Push(view);shown.Remove(key);}
            visibleEdges.Clear();
            foreach(var edge in Document.Edges.Values)
            {
                if(!Document.Nodes.TryGetValue(edge.From,out var from)||!Document.Nodes.TryGetValue(edge.To,out var to)||!Document.Visible(from)||!Document.Visible(to))continue;
                var a=Camera.ToScreen(from.DisplayPosition,size);var b=Camera.ToScreen(to.DisplayPosition,size);
                if(Mathf.Max(a.x,b.x)>=0&&Mathf.Min(a.x,b.x)<=size.x&&Mathf.Max(a.y,b.y)>=0&&Mathf.Min(a.y,b.y)<=size.y)visibleEdges.Add(edge);
            }
            progress.text=(string.IsNullOrEmpty(query)?"":matches.Count+" matches · ")+Provider.Status;
            progress.tooltip=progress.text;Viewport.MarkDirtyRepaint();
        }
        private void Draw(MeshGenerationContext context)
        {
            if(Document==null||!(Viewport.contentRect.width>1)||!(Viewport.contentRect.height>1)||!float.IsFinite(Camera.Zoom))return;
            var size=Viewport.contentRect.size;
            QuantumGraphEdgeRenderer.Draw(context,Document,Camera,size,settings,visibleEdges,screenAnchors,Selection,clock);
            var p=context.painter2D;
            foreach(var node in inView)
            {
                if(shown.ContainsKey(node.Id)||!Document.Visible(node))continue;
                Vector2 point=Camera.ToScreen(node.Position,size);p.fillColor=node.Color;
                p.BeginPath();p.Arc(point,matches.Contains(node.Id)?4:2.5f,Angle.Degrees(0),Angle.Degrees(360));p.Fill();
            }
        }
        public void SaveState()
        {
            if(Document==null)return;
            QuantumGraphStatePersistence.instance.Store(new QuantumSavedGraph{Key=Document.Key,Selected=selectedId,Search=query,Pan=Camera.Pan,Zoom=Camera.Zoom,HasViewport=!initialFit,
                Nodes=Document.Nodes.Values.Select(n=>new QuantumSavedNode{Id=n.Id,Position=n.Position,Size=n.Size,Manual=n.Manual,Pinned=n.Pinned,Expanded=n.Expanded,Positioned=n.Positioned}).ToList()});
            dirtyState=false;lastSave=EditorApplication.timeSinceStartup;
        }
        public void Dispose(){SaveState();Provider.Dispose();}
    }

    internal sealed class QuantumGraphManipulator:PointerManipulator
    {
        private readonly QuantumGraphView view;
        private int pointer=-1;
        private Vector2 start,last;
        private bool moved,additive;
        private QuantumGraphNode pressed;
        private HashSet<QuantumGraphNode> moving;
        private double previousClickTime;
        private string previousClick;
        public QuantumGraphManipulator(QuantumGraphView view){this.view=view;}
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Down);target.RegisterCallback<PointerMoveEvent>(Move);target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCaptureOutEvent>(Lost);target.RegisterCallback<WheelEvent>(Wheel);
        }
        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(Down);target.UnregisterCallback<PointerMoveEvent>(Move);target.UnregisterCallback<PointerUpEvent>(Up);
            target.UnregisterCallback<PointerCaptureOutEvent>(Lost);target.UnregisterCallback<WheelEvent>(Wheel);
        }
        private void Down(PointerDownEvent e)
        {
            if(pointer>=0||(e.button!=0&&e.button!=2))return;
            var element=e.target as VisualElement;if(element is Button||element?.GetFirstAncestorOfType<Button>()!=null)return;
            pointer=e.pointerId;start=last=e.position;moved=false;additive=e.shiftKey||e.ctrlKey||e.commandKey;
            var nodeView=element as QuantumGraphNodeView??element?.GetFirstAncestorOfType<QuantumGraphNodeView>();
            pressed=e.button==2||e.altKey?null:nodeView?.Node??view.Hit(target.WorldToLocal(e.position));
            moving=pressed!=null?view.DragSet(pressed):null;
            target.CapturePointer(pointer);target.Focus();e.StopPropagation();
        }
        private void Move(PointerMoveEvent e)
        {
            if(e.pointerId!=pointer)return;
            if(e.pressedButtons==0){Release();return;}
            if(Vector2.Distance(start,e.position)>3)moved=true;
            if(moved)
            {
                Vector2 delta=(Vector2)e.position-last;
                if(pressed==null)view.Camera.Pan+=delta;
                else foreach(var node in moving){node.Position+=delta/view.Camera.Zoom;node.Manual=node.Positioned=true;}
                view.Moved(pressed!=null);
            }
            last=e.position;e.StopPropagation();
        }
        private void Up(PointerUpEvent e)
        {
            if(e.pointerId!=pointer)return;
            if(!moved)
            {
                view.Select(pressed,additive);
                double now=EditorApplication.timeSinceStartup;
                if(pressed!=null&&pressed.Id==previousClick&&now-previousClickTime<.35)view.DoubleClick(pressed);
                previousClick=pressed?.Id;previousClickTime=now;
            }
            Release();e.StopPropagation();
        }
        private void Lost(PointerCaptureOutEvent e){if(e.pointerId==pointer)Release();}
        private void Release(){int id=pointer;pointer=-1;if(id>=0&&target.HasPointerCapture(id))target.ReleasePointer(id);if(moved)view.SaveState();pressed=null;moving=null;}
        private void Wheel(WheelEvent e){view.Camera.ZoomAt(target.WorldToLocal(e.mousePosition),target.contentRect.size,e.delta.y);view.Moved(false);e.StopPropagation();}
    }
}
