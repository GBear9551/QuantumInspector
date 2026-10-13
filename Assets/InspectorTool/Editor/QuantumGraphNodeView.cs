using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphNodeView:VisualElement
    {
        public QuantumGraphNode Node {get;private set;}
        private readonly Label title=new Label(),subtitle=new Label(),glyph=new Label();
        private readonly Image icon=new Image();
        private readonly Button expand;
        public QuantumGraphNodeView(Action<QuantumGraphNode> toggle,Action<QuantumGraphNode,ContextualMenuPopulateEvent> menu,Action<string> hover)
        {
            AddToClassList("quantum-graph-node");
            glyph.AddToClassList("graph-node-glyph");icon.AddToClassList("graph-node-icon");
            title.AddToClassList("graph-node-title");subtitle.AddToClassList("graph-node-subtitle");
            foreach(var item in new VisualElement[]{title,subtitle,glyph,icon})item.pickingMode=PickingMode.Ignore;
            Add(glyph);Add(icon);Add(title);Add(subtitle);
            expand=new Button(()=>{if(Node!=null)toggle(Node);}){tooltip="Expand or collapse this node"};expand.AddToClassList("graph-node-expand");Add(expand);
            this.AddManipulator(new ContextualMenuManipulator(e=>{if(Node!=null)menu(Node,e);}));
            RegisterCallback<PointerEnterEvent>(_=>{if(Node!=null)hover(Node.Label+" · "+Node.Subtitle+" · "+(Node.PropertyPath??Node.AssetPath??"Right-click for graph actions"));});
            RegisterCallback<PointerLeaveEvent>(_=>hover(""));
        }
        public void Bind(QuantumGraphNode node,Vector2 screen,float zoom,float font,bool selected,bool matching,bool searching,bool clusterSelected)
        {
            Node=node;name="graphNode_"+node.Id;
            title.text=node.Label;subtitle.text=node.Kind==QuantumGraphNodeKind.Cluster?node.Subtitle+(node.Expanded?" · expanded":" · collapsed"):node.Subtitle;
            glyph.text=node.Glyph??"◈";icon.image=node.Icon;icon.EnableInClassList("quantum-hidden",node.Icon==null);glyph.EnableInClassList("quantum-hidden",node.Icon!=null);
            expand.text=node.Loading?"…":node.Expanded?"−":"+";expand.EnableInClassList("quantum-hidden",!node.CanExpand);
            EnableInClassList("graph-selected",selected);EnableInClassList("graph-match",matching&&searching);
            EnableInClassList("graph-dim",searching&&!matching&&!selected);EnableInClassList("graph-cluster",node.Kind==QuantumGraphNodeKind.Cluster);
            EnableInClassList("graph-cluster-selected",clusterSelected);EnableInClassList("graph-pinned",node.Pinned);
            style.translate=new Translate(screen.x-node.Size.x*zoom*.5f,screen.y-node.Size.y*zoom*.5f);
            style.width=node.Size.x*zoom;style.height=node.Size.y*zoom;
            title.style.fontSize=Mathf.Clamp(font*zoom,9,22);subtitle.style.fontSize=Mathf.Clamp((font-2)*zoom,8,18);
            subtitle.EnableInClassList("quantum-hidden",zoom<.48f);glyph.EnableInClassList("quantum-hidden",zoom<.5f||node.Icon!=null);
            icon.EnableInClassList("quantum-hidden",zoom<.5f||node.Icon==null);
            expand.EnableInClassList("quantum-hidden",!node.CanExpand||zoom<.42f);
            style.borderTopColor=style.borderBottomColor=style.borderLeftColor=style.borderRightColor=node.Color;
            tooltip=node.Label+"\n"+node.Subtitle+"\n"+(node.AssetPath??node.PropertyPath??"")+(node.Pinned?"\nPosition pinned":"");
        }
    }
}
