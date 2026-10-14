using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphNodeView:VisualElement
    {
        public QuantumGraphNode Node {get;private set;}
        public float DisplayScale {get;private set;}=1;
        private readonly Label title=new Label(),subtitle=new Label(),glyph=new Label();
        private readonly Image icon=new Image();
        private readonly Image preview=new Image{pickingMode=PickingMode.Ignore};
        private readonly Button expand;
        private readonly VisualElement surface=new VisualElement{pickingMode=PickingMode.Ignore};
        private Color tint,surfaceTop,surfaceBottom;
        private bool cosmic,isSelected,isHovered;
        private float hoverLight,selectionLight;
        private double lastAppearance;
        public QuantumGraphNodeView(Action<QuantumGraphNode> toggle,Action<QuantumGraphNode,ContextualMenuPopulateEvent> menu,Action<string> hover,QuantumGraphView workspace=null)
        {
            AddToClassList("quantum-graph-node");
            surface.AddToClassList("graph-node-surface");Add(surface);surface.generateVisualContent+=DrawSurface;
            glyph.AddToClassList("graph-node-glyph");icon.AddToClassList("graph-node-icon");
            title.AddToClassList("graph-node-title");subtitle.AddToClassList("graph-node-subtitle");
            foreach(var item in new VisualElement[]{title,subtitle,glyph,icon})item.pickingMode=PickingMode.Ignore;
            Add(glyph);Add(icon);Add(title);Add(subtitle);
            preview.AddToClassList("graph-node-preview");Add(preview);
            expand=new Button(()=>{if(Node!=null)toggle(Node);}){tooltip="Expand or collapse this node"};expand.AddToClassList("graph-node-expand");Add(expand);
            if(workspace!=null)
            {
                var resize=new VisualElement{name="nodeResizeHandle",tooltip="Drag to resize. Titles wrap as the node gets narrower."};
                resize.AddToClassList("graph-node-resize");Add(resize);
                resize.AddManipulator(new QuantumGraphNodeResizer(this,workspace));
            }
            this.AddManipulator(new ContextualMenuManipulator(e=>{if(Node!=null)menu(Node,e);}));
            RegisterCallback<PointerEnterEvent>(_=>{isHovered=true;if(Node!=null)hover(Node.Label+" · "+Node.Subtitle+" · "+(Node.PropertyPath??Node.AssetPath??"Right-click for graph actions"));});
            RegisterCallback<PointerLeaveEvent>(_=>{isHovered=false;hover("");});
        }
        public void Bind(QuantumGraphNode node,Vector2 screen,float zoom,float font,bool selected,bool matching,bool searching,bool clusterSelected)
        {
            if(Node!=node){hoverLight=selectionLight=0;isHovered=isSelected=false;}
            Node=node;DisplayScale=zoom;name="graphNode_"+node.Id;
            style.opacity=node.Opacity*(searching&&!matching&&!selected?.4f:1);
            title.text=node.Label;subtitle.text=node.Kind==QuantumGraphNodeKind.Cluster?node.Subtitle+(node.Expanded?" · expanded":" · collapsed"):node.Subtitle;
            glyph.text=node.Glyph??"◈";icon.image=node.Icon;icon.EnableInClassList("quantum-hidden",node.Icon==null);glyph.EnableInClassList("quantum-hidden",node.Icon!=null);
            expand.text=node.Loading?"…":node.Expanded?"−":"+";expand.EnableInClassList("quantum-hidden",!node.CanExpand);
            EnableInClassList("graph-selected",selected);EnableInClassList("graph-match",matching&&searching);
            EnableInClassList("graph-dim",searching&&!matching&&!selected);EnableInClassList("graph-cluster",node.Kind==QuantumGraphNodeKind.Cluster);
            EnableInClassList("graph-asset",node.Kind==QuantumGraphNodeKind.Asset);
            EnableInClassList("graph-cluster-selected",clusterSelected);EnableInClassList("graph-pinned",node.Pinned);
            style.translate=new Translate(screen.x-node.Size.x*zoom*.5f,screen.y-node.Size.y*zoom*.5f);
            style.transformOrigin=new TransformOrigin(0,0);style.scale=new Scale(new Vector3(zoom,zoom,1));
            style.width=node.Size.x;style.height=node.Size.y;
            title.style.fontSize=font;subtitle.style.fontSize=Mathf.Max(font-2,11/zoom);
            subtitle.EnableInClassList("quantum-hidden",zoom<.48f);glyph.EnableInClassList("quantum-hidden",zoom<.5f||node.Icon!=null);
            icon.EnableInClassList("quantum-hidden",zoom<.5f||node.Icon==null);
            expand.EnableInClassList("quantum-hidden",!node.CanExpand||zoom<.42f);
            style.borderTopColor=style.borderBottomColor=style.borderLeftColor=style.borderRightColor=node.Color;
            tooltip=node.Label+"\n"+node.Subtitle+"\n"+(node.AssetPath??node.PropertyPath??"")+(node.Pinned?"\nPosition pinned":"");
        }
        public void SetDetail(bool detail)
        {preview.image=Node?.Icon;preview.EnableInClassList("quantum-hidden",!detail||Node?.Kind!=QuantumGraphNodeKind.Asset||Node.Icon==null);}
        public void SetSemanticZoom(float blend,bool summary,int dots)
        {
            if(!summary)style.opacity=style.opacity.value*(1-blend);
            var mode=!summary&&blend>.5f?PickingMode.Ignore:PickingMode.Position;
            pickingMode=mode;expand.pickingMode=mode;var resize=this.Q("nodeResizeHandle");if(resize!=null)resize.pickingMode=mode;
            if(summary&&blend>=.5f&&dots>0){subtitle.text+=" · "+dots+" dots";subtitle.tooltip=subtitle.text;}
        }
        public void SetCleanSummary(bool owner)
        {
            if(!owner&&Node.Kind!=QuantumGraphNodeKind.Cluster)return;
            string flags=(Node.Pinned?"PIN · ":"")+(QuantumGraphDot.IsDisabled(Node)?"OFF · ":"");
            subtitle.text=flags+(owner?"OWNER"+(Node.Kind==QuantumGraphNodeKind.GameObject?" · "+Node.MemberCount+(Node.MemberCount==1?" component":" components"):Node.Kind==QuantumGraphNodeKind.Folder?" · "+Node.MemberCount+(Node.MemberCount==1?" item":" items"):""):Node.MemberCount+(Node.MemberCount==1?" member":" members"));
        }
        public void ConfigureAppearance(InspectorWheelSettings settings,Color color,bool owner,bool selected)
        {
            cosmic=settings.GraphPalette==QuantumGraphPalette.CosmicNebula;tint=color;isSelected=selected;
            surface.style.display=cosmic?DisplayStyle.Flex:DisplayStyle.None;
            surfaceTop=settings.Nebula.SurfaceLight;surfaceBottom=settings.Nebula.Surface;
            EnableInClassList("graph-owner",owner);
            bool disabled=Node.Target is Behaviour behaviour&&!behaviour.enabled||Node.Target is Renderer renderer&&!renderer.enabled||Node.Target is Collider collider&&!collider.enabled||Node.Target is Component component&&!component.gameObject.activeInHierarchy||Node.Target is GameObject go&&!go.activeInHierarchy;
            bool missing=Node.Kind==QuantumGraphNodeKind.Missing||!Node.Active;
            EnableInClassList("graph-disabled",disabled);EnableInClassList("graph-missing",missing);
            string flags=(selected?"SELECTED · ":"")+(Node.Pinned?"PIN · ":"")+(missing?"MISSING · ":disabled?"OFF · ":"")+(owner?"OWNER · ":"");
            subtitle.text=flags+(Node.Kind==QuantumGraphNodeKind.Cluster?Node.MemberCount+" members · "+(Node.Expanded?"expanded":"collapsed"):Node.Subtitle);
            if(Node.Kind==QuantumGraphNodeKind.Asset||owner&&Node.Target!=null)subtitle.text+=" · "+(Node.PreviewState==QuantumGraphPreviewState.Pending?"preview pending":Node.PreviewState==QuantumGraphPreviewState.Ready?"preview":"type icon");
            subtitle.tooltip=subtitle.text;tooltip=Node.Label+"\n"+subtitle.text+"\n"+(Node.AssetPath??Node.PropertyPath??"");
            if(cosmic){title.style.color=settings.Nebula.Text;subtitle.style.color=settings.Nebula.Supporting;}
            else{title.style.color=StyleKeyword.Null;subtitle.style.color=StyleKeyword.Null;}
            TickAppearance(EditorApplication.timeSinceStartup,settings.EffectiveGraphMotion);ApplyRim();surface.MarkDirtyRepaint();
        }
        public void TickAppearance(double now,QuantumGraphMotionMode mode)
        {
            float delta=lastAppearance==0?0:Mathf.Clamp((float)(now-lastAppearance),0,.1f);lastAppearance=now;
            float hover=mode==QuantumGraphMotionMode.Full?Mathf.MoveTowards(hoverLight,isHovered?1:0,delta/.11f):isHovered?1:0;
            float selection=mode==QuantumGraphMotionMode.Full?Mathf.MoveTowards(selectionLight,isSelected?1:0,delta/.18f):isSelected?1:0;
            if(hover==hoverLight&&selection==selectionLight)return;
            hoverLight=hover;selectionLight=selection;ApplyRim();surface.MarkDirtyRepaint();
        }
        private void ApplyRim()
        {
            Color rim=Color.Lerp(tint,Color.white,.3f*selectionLight+.14f*hoverLight);
            style.borderTopColor=style.borderBottomColor=style.borderLeftColor=style.borderRightColor=rim;
        }
        private void DrawSurface(MeshGenerationContext context)
        {
            if(!cosmic)return;
            var rect=surface.contentRect;if(rect.width<=0||rect.height<=0)return;
            var mesh=context.Allocate(4,6);
            Color32 top=Color.Lerp(surfaceTop,tint,.06f*hoverLight+.13f*selectionLight),bottom=surfaceBottom;
            mesh.SetNextVertex(new Vertex{position=new Vector3(0,0,Vertex.nearZ),tint=top});
            mesh.SetNextVertex(new Vertex{position=new Vector3(rect.width,0,Vertex.nearZ),tint=top});
            mesh.SetNextVertex(new Vertex{position=new Vector3(rect.width,rect.height,Vertex.nearZ),tint=bottom});
            mesh.SetNextVertex(new Vertex{position=new Vector3(0,rect.height,Vertex.nearZ),tint=bottom});
            mesh.SetNextIndex(0);mesh.SetNextIndex(1);mesh.SetNextIndex(2);mesh.SetNextIndex(2);mesh.SetNextIndex(3);mesh.SetNextIndex(0);
        }
    }

    internal sealed class QuantumGraphNodeResizer:PointerManipulator
    {
        private readonly QuantumGraphNodeView visual;
        private readonly QuantumGraphView workspace;
        private QuantumGraphNode node;
        private int pointer=-1;
        private Vector2 start,size,position;
        private float scale;
        private bool wasPinned,wasManual,wasCustom;
        public QuantumGraphNodeResizer(QuantumGraphNodeView visual,QuantumGraphView workspace){this.visual=visual;this.workspace=workspace;}
        protected override void RegisterCallbacksOnTarget()
        {
            workspace.CancelInteraction+=Cancel;
            target.RegisterCallback<PointerDownEvent>(Down);target.RegisterCallback<PointerMoveEvent>(Move);
            target.RegisterCallback<PointerUpEvent>(Up);target.RegisterCallback<PointerCaptureOutEvent>(Lost);target.RegisterCallback<DetachFromPanelEvent>(Detached);
        }
        protected override void UnregisterCallbacksFromTarget()
        {
            workspace.CancelInteraction-=Cancel;
            target.UnregisterCallback<PointerDownEvent>(Down);target.UnregisterCallback<PointerMoveEvent>(Move);
            target.UnregisterCallback<PointerUpEvent>(Up);target.UnregisterCallback<PointerCaptureOutEvent>(Lost);target.UnregisterCallback<DetachFromPanelEvent>(Detached);
        }
        private void Down(PointerDownEvent e)
        {
            if(e.button!=0||pointer>=0||visual.Node==null)return;
            node=visual.Node;workspace.BeginDirect(new[]{node});pointer=e.pointerId;start=workspace.Viewport.WorldToLocal(e.position);size=node.Size;position=node.Position;scale=visual.DisplayScale;
            wasPinned=node.Pinned;wasManual=node.Manual;wasCustom=node.CustomSize;
            target.CapturePointer(pointer);workspace.Select(node);e.StopPropagation();
        }
        private void Move(PointerMoveEvent e)
        {
            if(e.pointerId!=pointer)return;
            if(e.pressedButtons==0){Release();return;}
            var delta=workspace.Viewport.WorldToLocal(e.position)-start;
            workspace.ResizeNode(node,size+delta/scale);
            node.Position=position+(node.Size-size)*scale/(2*workspace.Camera.Zoom);node.Pinned=node.Manual=node.Positioned=true;
            workspace.Moved();e.StopPropagation();
        }
        private void Up(PointerUpEvent e){if(e.pointerId==pointer){Release();e.StopPropagation();}}
        private void Lost(PointerCaptureOutEvent e){if(e.pointerId==pointer)Release();}
        private void Detached(DetachFromPanelEvent e)=>Release();
        private void Cancel()
        {
            if(pointer<0||node==null)return;
            node.Size=size;node.Position=position;node.CustomSize=wasCustom;node.Pinned=wasPinned;node.Manual=wasManual;
            node.PresentedPosition=position;workspace.Moved();Release();
        }
        private void Release()
        {
            if(pointer<0)return;int previous=pointer;pointer=-1;
            if(target.HasPointerCapture(previous))target.ReleasePointer(previous);
            workspace.FinishResize(node);node=null;
        }
    }
}
