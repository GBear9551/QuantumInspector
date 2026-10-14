using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class WheelSearchField:VisualElement,INotifyValueChanged<string>
    {
        private readonly TextField input=new TextField {name="scopeSearch"};
        private readonly Button clear;
        private readonly VisualElement icon=new VisualElement {pickingMode=PickingMode.Ignore};
        private float displayScale=1;
        public string value {get=>input.value;set=>input.value=value;}
        public WheelSearchField()
        {
            AddToClassList("quantum-search");
            icon.AddToClassList("search-magnifier");
            icon.generateVisualContent+=ctx=>
            {
                var p=ctx.painter2D;p.strokeColor=new Color(.5f,.87f,1);p.lineWidth=1.6f*displayScale;
                p.BeginPath();p.Arc(new Vector2(6,6)*displayScale,4.3f*displayScale,Angle.Degrees(0),Angle.Degrees(360));p.Stroke();
                p.BeginPath();p.MoveTo(new Vector2(9.4f,9.4f)*displayScale);p.LineTo(new Vector2(14,14)*displayScale);p.Stroke();
            };
            Add(icon);Add(input);input.tooltip="Search this scope by name, alias or type";
            clear=new Button(()=>value="") {text="×",tooltip="Clear search"};clear.AddToClassList("search-clear");Add(clear);
            input.RegisterValueChangedCallback(e=>
            {
                e.StopPropagation();clear.SetEnabled(!string.IsNullOrEmpty(e.newValue));
                using(var changed=ChangeEvent<string>.GetPooled(e.previousValue,e.newValue)){changed.target=this;SendEvent(changed);}
            });
            clear.SetEnabled(false);
        }
        public void SetScale(float scale)
        {
            displayScale=float.IsFinite(scale)?Mathf.Clamp(scale,1,3):1;
            style.height=style.minHeight=26*displayScale;style.marginBottom=6*displayScale;
            input.style.fontSize=12*displayScale;
            icon.style.width=icon.style.height=16*displayScale;
            icon.style.marginLeft=6*displayScale;icon.style.marginRight=4*displayScale;
            clear.style.fontSize=14*displayScale;clear.style.width=20*displayScale;
            clear.style.height=clear.style.minHeight=20*displayScale;
            clear.style.marginLeft=clear.style.marginRight=2*displayScale;
            icon.MarkDirtyRepaint();
        }
        public void SetValueWithoutNotify(string text){input.SetValueWithoutNotify(text??"");clear.SetEnabled(!string.IsNullOrEmpty(text));}
    }
}
