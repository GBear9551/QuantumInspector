using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    internal sealed class QuantumGraphComponentInspector : VisualElement, IDisposable
    {
        private readonly Label title=new Label(), owner=new Label();
        private readonly ScrollView body=new ScrollView(ScrollViewMode.Vertical);
        private IMGUIContainer header;
        private string dismissedId;
        internal Editor ComponentEditor {get;private set;}
        internal Vector2 Anchor {get;private set;}
        internal Rect PanelBounds {get;private set;}
        internal bool Visible {get;private set;}
        internal string NodeId {get;private set;}

        internal QuantumGraphComponentInspector(Action changed)
        {
            name="graphComponentInspector";AddToClassList("graph-component-inspector");AddToClassList("quantum-hidden");
            var heading=new VisualElement();heading.AddToClassList("graph-inspector-heading");Add(heading);
            title.AddToClassList("graph-inspector-title");heading.Add(title);
            heading.Add(new Button(()=>{dismissedId=NodeId;Hide();changed();}){text="×",name="graphInspectorClose",tooltip="Close inspector; select the component again to reopen"});
            owner.AddToClassList("graph-inspector-owner");Add(owner);
            body.name="graphNativeInspectorBody";body.AddToClassList("orbital-native-body");body.EnableInClassList("orbital-native-light",!EditorGUIUtility.isProSkin);Add(body);
            RegisterCallback<DetachFromPanelEvent>(e=>{if(e.target==this)Dispose();});
        }
        internal void Reopen(){dismissedId=null;}
        internal void Present(QuantumGraphNode node,Vector2 anchor,Vector2 halfSize,Vector2 viewport)
        {
            if(node==null||node.Kind!=QuantumGraphNodeKind.Component||!(node.Target is Component component)||component==null||node.Id==dismissedId||!node.Active||viewport.x<32||viewport.y<32)
            {Hide();return;}
            if(anchor.x+halfSize.x<0||anchor.y+halfSize.y<0||anchor.x-halfSize.x>viewport.x||anchor.y-halfSize.y>viewport.y)
            {Hide();return;}
            NodeId=node.Id;Anchor=anchor;
            PanelBounds=Place(anchor,halfSize,viewport);
            style.left=PanelBounds.x;style.top=PanelBounds.y;style.width=PanelBounds.width;style.height=PanelBounds.height;
            title.text=node.Label;title.tooltip=component.GetType().FullName;
            owner.text=component.gameObject.name+" / "+component.GetType().Name;owner.tooltip=owner.text;
            if(ComponentEditor==null||ComponentEditor.target!=component)
            {
                ReleaseEditor();ComponentEditor=Editor.CreateEditor(component);
                if(ComponentEditor!=null)
                {
                    header=new IMGUIContainer(()=>{if(ComponentEditor!=null&&ComponentEditor.target!=null)ComponentEditor.DrawHeader();});body.Add(header);
                    body.Add(new InspectorElement(ComponentEditor){name="graphNativeComponentEditor"});
                }
            }
            Visible=true;RemoveFromClassList("quantum-hidden");
            if(parent!=null&&parent.IndexOf(this)!=parent.childCount-1)BringToFront();
        }
        internal static Rect Place(Vector2 anchor,Vector2 halfSize,Vector2 viewport)
        {
            const float margin=12,gap=18;
            var size=new Vector2(Mathf.Min(360,Mathf.Max(1,viewport.x-margin*2)),Mathf.Min(460,Mathf.Max(1,viewport.y-margin*2)));
            float x=anchor.x+halfSize.x+gap,y=anchor.y-48;
            if(x+size.x>viewport.x-margin)
            {
                x=anchor.x-halfSize.x-gap-size.x;
                if(x<margin)
                {
                    x=anchor.x-size.x*.5f;
                    y=anchor.y+halfSize.y+gap;
                    if(y+size.y>viewport.y-margin)y=anchor.y-halfSize.y-gap-size.y;
                }
            }
            return new Rect(new Vector2(Mathf.Clamp(x,margin,Mathf.Max(margin,viewport.x-size.x-margin)),Mathf.Clamp(y,margin,Mathf.Max(margin,viewport.y-size.y-margin))),size);
        }
        internal void DrawLink(Painter2D painter)
        {
            if(!Visible)return;
            var end=new Vector2(Mathf.Clamp(Anchor.x,PanelBounds.xMin,PanelBounds.xMax),Mathf.Clamp(Anchor.y,PanelBounds.yMin,PanelBounds.yMax));
            painter.strokeColor=new Color(.45f,.84f,.95f,.8f);painter.lineWidth=1.5f;
            painter.BeginPath();painter.MoveTo(Anchor);painter.LineTo(end);painter.Stroke();
        }
        internal void Tick()
        {
            if(!Visible)return;
            if(ComponentEditor==null||ComponentEditor.target==null){Hide();return;}
            if(ComponentEditor.RequiresConstantRepaint())body.MarkDirtyRepaint();
        }
        internal void Hide(){Visible=false;NodeId=null;AddToClassList("quantum-hidden");ReleaseEditor();}
        private void ReleaseEditor()
        {
            if(ComponentEditor==null&&header==null&&body.childCount==0)return;
            body.Unbind();body.Clear();
            if(header!=null){header.onGUIHandler=null;header.Dispose();header=null;}
            if(ComponentEditor!=null)UnityEngine.Object.DestroyImmediate(ComponentEditor);
            ComponentEditor=null;
        }
        public void Dispose(){Hide();}
    }
}
