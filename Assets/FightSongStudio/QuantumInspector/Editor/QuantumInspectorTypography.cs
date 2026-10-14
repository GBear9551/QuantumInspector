using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    internal static class QuantumInspectorTypography
    {
        internal static float NodeScale(float font)=>Mathf.Max(1,Mathf.Clamp(font,10,24)/13f);
        internal static float WheelDiameter(WheelNodeSize size,float scale)=>(size==WheelNodeSize.Compact?94:size==WheelNodeSize.Large?128:112)*Mathf.Clamp(scale,1,3);
        private static bool InsideHub(VisualElement element)
        {
            for(var ancestor=element.parent;ancestor!=null;ancestor=ancestor.parent)
                if(ancestor.ClassListContains("quantum-hub"))return true;
            return false;
        }
        internal static void ApplyButtons(VisualElement root,float font)
        {
            font=float.IsFinite(font)?Mathf.Clamp(font,10,24):12;
            root.Query<WheelSearchField>().ForEach(field=>{if(!InsideHub(field))field.style.height=field.style.minHeight=font+14;});
            root.Query<Button>().ForEach(button=>
            {
                if(InsideHub(button))return;
                for(var ancestor=button.parent;ancestor!=null;ancestor=ancestor.parent)
                    if(ancestor.ClassListContains("orbital-native-body"))return;
                if(button.style.fontSize.value.value==font)return;
                button.style.fontSize=font;
                button.style.minHeight=font+10;
                if(button.ClassListContains("search-clear"))button.style.width=font+8;
                if(button.ClassListContains("graph-node-expand"))button.style.width=button.style.height=font+10;
            });
        }
    }
}
