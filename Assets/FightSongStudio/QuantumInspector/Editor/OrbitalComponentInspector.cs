using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    internal sealed class OrbitalComponentInspector : VisualElement, IDisposable
    {
        internal readonly Button Back, Forward, Return;
        private readonly Label title=new Label(), context=new Label();
        private readonly ScrollView body=new ScrollView(ScrollViewMode.Vertical){name="orbitalNativeInspectorBody"};
        private readonly VisualElement content=new VisualElement();
        private Component component;
        private IMGUIContainer header;
        internal Editor ComponentEditor {get;private set;}

        internal OrbitalComponentInspector(Action<int> history,Action restore)
        {
            name="orbitalComponentInspector";AddToClassList("orbital-inspector");AddToClassList("quantum-hidden");
            var rim=new VisualElement();rim.AddToClassList("orbital-inspector-rim");Add(rim);
            content.AddToClassList("orbital-inspector-content");rim.Add(content);
            var navigation=new VisualElement();navigation.AddToClassList("orbital-inspector-navigation");content.Add(navigation);
            Back=new Button(()=>history(-1)){text="←",tooltip="Back to previous inspection",name="orbitalInspectorBack"};
            Forward=new Button(()=>history(1)){text="→",tooltip="Forward to next inspection",name="orbitalInspectorForward"};
            Return=new Button(restore){text="Orbit parameters",tooltip="Release the parameters back into orbit",name="orbitalInspectorReturn"};
            navigation.Add(Back);navigation.Add(Forward);navigation.Add(Return);
            title.AddToClassList("orbital-inspector-title");context.AddToClassList("orbital-inspector-context");
            content.Add(title);content.Add(context);
            body.AddToClassList("orbital-native-body");body.EnableInClassList("orbital-native-light",!EditorGUIUtility.isProSkin);content.Add(body);
            RegisterCallback<DetachFromPanelEvent>(e=>{if(e.target==this)Dispose();});
        }
        internal void SetComponent(Component target)
        {
            if(component==target)return;
            ReleaseEditor();component=target;
            title.text=target!=null?ComponentAliasDatabase.DisplayName(target):"Component unavailable";
            context.text=target!=null?target.gameObject.name+"  /  "+target.GetType().Name:"Return to the wheel to continue.";
            context.tooltip=target!=null?target.GetType().FullName:"";
        }
        internal void Present(float progress,Vector2 viewport,float hubDiameter,bool opening)
        {
            bool visible=progress>0;
            EnableInClassList("quantum-hidden",!visible);
            if(!visible){ReleaseEditor();return;}
            float growth=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1,progress));
            var available=new Vector2(Mathf.Max(1,viewport.x-24),Mathf.Max(1,viewport.y-24));
            var expanded=new Vector2(Mathf.Min(640,available.x),Mathf.Min(740,available.y));
            var collapsed=Vector2.Min(Vector2.one*hubDiameter,available);
            var size=Vector2.Lerp(collapsed,expanded,growth);
            style.width=size.x;style.height=size.y;
            style.left=(viewport.x-size.x)*.5f;style.top=(viewport.y-size.y)*.5f;
            float radius=Mathf.Lerp(hubDiameter*.5f,18,growth);
            style.borderTopLeftRadius=style.borderTopRightRadius=style.borderBottomLeftRadius=style.borderBottomRightRadius=radius;
            style.opacity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.45f,progress));
            float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1,progress));
            content.style.opacity=reveal;
            content.style.visibility=reveal>0?Visibility.Visible:Visibility.Hidden;
            body.SetEnabled(opening&&progress>=1);
            if(opening&&progress>=1&&component!=null&&ComponentEditor==null)BuildEditor();
        }
        private void BuildEditor()
        {
            ComponentEditor=Editor.CreateEditor(component);
            if(ComponentEditor==null)return;
            header=new IMGUIContainer(()=>{if(ComponentEditor!=null&&ComponentEditor.target!=null)ComponentEditor.DrawHeader();});
            header.name="orbitalNativeComponentHeader";body.Add(header);
            body.Add(new InspectorElement(ComponentEditor){name="orbitalNativeComponentEditor"});
        }
        internal void RefreshEditor()
        {
            if(ComponentEditor!=null&&ComponentEditor.RequiresConstantRepaint())body.MarkDirtyRepaint();
        }
        private void ReleaseEditor()
        {
            body.Unbind();body.Clear();
            if(header!=null){header.onGUIHandler=null;header.Dispose();header=null;}
            if(ComponentEditor!=null)UnityEngine.Object.DestroyImmediate(ComponentEditor);
            ComponentEditor=null;
        }
        public void Dispose(){ReleaseEditor();}
    }
}
