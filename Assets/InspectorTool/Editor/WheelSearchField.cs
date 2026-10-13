using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class WheelSearchField:VisualElement,INotifyValueChanged<string>
    {
        private readonly TextField input=new TextField {name="scopeSearch"};
        private readonly Button clear;
        public string value {get=>input.value;set=>input.value=value;}
        public WheelSearchField()
        {
            AddToClassList("quantum-search");
            var icon=new VisualElement {pickingMode=PickingMode.Ignore};icon.AddToClassList("search-magnifier");
            icon.generateVisualContent+=ctx=>
            {
                var p=ctx.painter2D;p.strokeColor=new Color(.5f,.87f,1);p.lineWidth=1.6f;
                p.BeginPath();p.Arc(new Vector2(6,6),4.3f,Angle.Degrees(0),Angle.Degrees(360));p.Stroke();
                p.BeginPath();p.MoveTo(new Vector2(9.4f,9.4f));p.LineTo(new Vector2(14,14));p.Stroke();
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
        public void SetValueWithoutNotify(string text){input.SetValueWithoutNotify(text??"");clear.SetEnabled(!string.IsNullOrEmpty(text));}
    }
}
