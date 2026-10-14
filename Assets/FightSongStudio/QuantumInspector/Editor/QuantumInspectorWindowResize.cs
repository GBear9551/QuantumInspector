using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    [Flags] internal enum QuantumWindowEdge { Left=1,Right=2,Top=4,Bottom=8 }
    internal sealed class QuantumInspectorWindowResize:PointerManipulator
    {
        private readonly EditorWindow window;
        private readonly QuantumWindowEdge edges;
        private int pointer=-1;
        private Rect original;
        private Vector2 start;
        private QuantumInspectorWindowResize(EditorWindow window,QuantumWindowEdge edges){this.window=window;this.edges=edges;}
        internal static void Install(EditorWindow window)
        {
            var layer=new VisualElement{name="quantumWindowResizeBorder",pickingMode=PickingMode.Ignore};layer.AddToClassList("quantum-window-resize-border");
            foreach(var edge in new[]{QuantumWindowEdge.Left,QuantumWindowEdge.Right,QuantumWindowEdge.Top,QuantumWindowEdge.Bottom,QuantumWindowEdge.Left|QuantumWindowEdge.Top,QuantumWindowEdge.Right|QuantumWindowEdge.Top,QuantumWindowEdge.Left|QuantumWindowEdge.Bottom,QuantumWindowEdge.Right|QuantumWindowEdge.Bottom})
            {
                var handle=new VisualElement{name="windowResize"+edge.ToString().Replace(", ",""),focusable=true,tooltip="Drag this border to resize the floating Quantum Inspector window."};
                handle.AddToClassList("quantum-window-resize-handle");
                if((edge&QuantumWindowEdge.Left)!=0)handle.AddToClassList("resize-left");
                if((edge&QuantumWindowEdge.Right)!=0)handle.AddToClassList("resize-right");
                if((edge&QuantumWindowEdge.Top)!=0)handle.AddToClassList("resize-top");
                if((edge&QuantumWindowEdge.Bottom)!=0)handle.AddToClassList("resize-bottom");
                handle.AddManipulator(new QuantumInspectorWindowResize(window,edge));layer.Add(handle);
            }
            window.rootVisualElement.Add(layer);
            void Refresh(){if(window!=null)layer.style.display=window.docked?DisplayStyle.None:DisplayStyle.Flex;}
            layer.RegisterCallback<GeometryChangedEvent>(_=>Refresh());
            layer.RegisterCallback<AttachToPanelEvent>(_=>Refresh());Refresh();
        }
        internal static Rect Bounds(Rect original,Vector2 delta,Vector2 minimum,Vector2 maximum,QuantumWindowEdge edges)
        {
            var result=original;
            if((edges&QuantumWindowEdge.Left)!=0){result.width=Mathf.Clamp(original.width-delta.x,minimum.x,Mathf.Max(minimum.x,maximum.x));result.x=original.xMax-result.width;}
            else if((edges&QuantumWindowEdge.Right)!=0)result.width=Mathf.Clamp(original.width+delta.x,minimum.x,Mathf.Max(minimum.x,maximum.x));
            if((edges&QuantumWindowEdge.Top)!=0){result.height=Mathf.Clamp(original.height-delta.y,minimum.y,Mathf.Max(minimum.y,maximum.y));result.y=original.yMax-result.height;}
            else if((edges&QuantumWindowEdge.Bottom)!=0)result.height=Mathf.Clamp(original.height+delta.y,minimum.y,Mathf.Max(minimum.y,maximum.y));
            return result;
        }
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Down);target.RegisterCallback<PointerMoveEvent>(Move);target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCaptureOutEvent>(Lost);target.RegisterCallback<KeyDownEvent>(Key);target.RegisterCallback<DetachFromPanelEvent>(Detach);
        }
        protected override void UnregisterCallbacksFromTarget()
        {
            Release();target.UnregisterCallback<PointerDownEvent>(Down);target.UnregisterCallback<PointerMoveEvent>(Move);target.UnregisterCallback<PointerUpEvent>(Up);
            target.UnregisterCallback<PointerCaptureOutEvent>(Lost);target.UnregisterCallback<KeyDownEvent>(Key);target.UnregisterCallback<DetachFromPanelEvent>(Detach);
        }
        private void Down(PointerDownEvent e)
        {
            if(e.button!=0||pointer>=0||window==null||window.docked)return;
            original=window.position;start=original.position+(Vector2)e.position;pointer=e.pointerId;
            target.CapturePointer(pointer);target.Focus();target.AddToClassList("resizing");e.StopPropagation();
        }
        private void Move(PointerMoveEvent e)
        {
            if(e.pointerId!=pointer)return;
            if(window==null||window.docked||e.pressedButtons==0){Release();return;}
            var delta=window.position.position+(Vector2)e.position-start;
            window.position=Bounds(original,delta,window.minSize,window.maxSize,edges);e.StopPropagation();
        }
        private void Up(PointerUpEvent e){if(e.pointerId!=pointer)return;Release();e.StopPropagation();}
        private void Lost(PointerCaptureOutEvent e){if(e.pointerId==pointer){pointer=-1;target.RemoveFromClassList("resizing");}}
        private void Detach(DetachFromPanelEvent e)=>Release();
        private void Key(KeyDownEvent e){if(pointer>=0&&e.keyCode==KeyCode.Escape){if(window!=null&&!window.docked)window.position=original;Release();e.StopPropagation();}}
        private void Release(){int id=pointer;pointer=-1;if(id>=0&&target.HasPointerCapture(id))target.ReleasePointer(id);target.RemoveFromClassList("resizing");}
    }
}
