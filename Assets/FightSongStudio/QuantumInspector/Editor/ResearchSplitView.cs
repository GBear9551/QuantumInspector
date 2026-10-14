using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    internal sealed class ResearchSplitView:TwoPaneSplitView
    {
        private readonly Action<float> saveWidth;
        private readonly VisualElement divider;
        private bool resizing,collapsed;
        private float startingWidth;

        public ResearchSplitView(float width,Action<float> saveWidth):base(1,width,TwoPaneSplitViewOrientation.Horizontal)
        {
            this.saveWidth=saveWidth;name="researchSplit";AddToClassList("quantum-body");
            divider=this.Q("unity-dragline-anchor");
            divider.tooltip="Drag this border left or right to resize the graph and Component Details / research panels.";
            RegisterCallback<PointerDownEvent>(e=>
            {
                if(e.button!=0||collapsed||!(e.target is VisualElement element)||!(element==divider||divider.Contains(element)))return;
                resizing=true;startingWidth=fixedPane.resolvedStyle.width;divider.AddToClassList("resizing");
            },TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(e=>{if(e.button==0)FinishResize();},TrickleDown.TrickleDown);
            RegisterCallback<PointerCaptureOutEvent>(_=>FinishResize(),TrickleDown.TrickleDown);
            RegisterCallback<DetachFromPanelEvent>(_=>{resizing=false;divider.RemoveFromClassList("resizing");});
        }

        public void Configure(InspectorWheelSettings settings,bool hideAuxiliary=false,ResearchLayouts? visibleLayouts=null)
        {
            if(resizing)return;
            float width=settings.PreferredResearchPanelWidth;
            if(!Mathf.Approximately(fixedPaneInitialDimension,width))fixedPaneInitialDimension=width;
            bool hide=(visibleLayouts??settings.Layouts)==ResearchLayouts.None||hideAuxiliary;
            if(hide==collapsed)return;
            collapsed=hide;
            if(hide)CollapseChild(1);else UnCollapse();
        }

        private void FinishResize()
        {
            if(!resizing)return;
            resizing=false;divider.RemoveFromClassList("resizing");
            float width=fixedPane.style.width.value.value;
            if(width>0&&!Mathf.Approximately(width,startingWidth))schedule.Execute(()=>saveWidth(Mathf.Round(width)));
        }
    }
}
