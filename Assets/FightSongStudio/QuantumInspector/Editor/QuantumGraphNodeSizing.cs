using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace FightSong.InspectorTool
{
    internal static class QuantumGraphNodeSizing
    {
        private static GUIStyle titleStyle;
        private static readonly GUIContent title=new GUIContent();
        private static readonly Dictionary<(string,float,float,Vector2?),Vector2> measured=new Dictionary<(string,float,float,Vector2?),Vector2>();
        public static Vector2 Fit(string label,InspectorWheelSettings settings,Vector2? requested=null)
        {
            var key=(label,settings.GraphFontSize,settings.GraphNodeWidth,requested);
            if(measured.TryGetValue(key,out var cached))return cached;
            if(titleStyle==null)titleStyle=new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label){fontStyle=FontStyle.Bold,wordWrap=true};
            titleStyle.fontSize=Mathf.CeilToInt(settings.GraphFontSize);
            title.text=label??"";
            Vector2 textSize=titleStyle.CalcSize(title);
            float fontScale=QuantumInspectorTypography.NodeScale(settings.GraphFontSize);
            float automaticWidth=Mathf.Clamp(Mathf.Max(settings.GraphNodeWidth*fontScale,textSize.x+68),120*fontScale,640);
            float width=requested.HasValue&&float.IsFinite(requested.Value.x)?Mathf.Clamp(requested.Value.x,120*fontScale,1200):automaticWidth;
            float minimumHeight=Mathf.Max(64*fontScale,Mathf.Ceil(textSize.x<=width-64?textSize.y:titleStyle.CalcHeight(title,width-64))+settings.GraphFontSize+28);
            float height=requested.HasValue&&float.IsFinite(requested.Value.y)?Mathf.Clamp(requested.Value.y,64,1600):64;
            var result=new Vector2(width,Mathf.Max(height,minimumHeight));
            if(measured.Count>=8192)measured.Clear();measured[key]=result;return result;
        }
        public static void Apply(QuantumGraphNode node,InspectorWheelSettings settings)
        {
            if(node.MeasuredLabel==node.Label&&node.MeasuredFont==settings.GraphFontSize&&node.MeasuredWidth==settings.GraphNodeWidth&&node.MeasuredCustom==node.CustomSize&&(!node.CustomSize||node.MeasuredRequest==node.Size))return;
            node.Size=Fit(node.Label,settings,node.CustomSize?node.Size:(Vector2?)null);
            if(node.Kind==QuantumGraphNodeKind.Asset)node.Size.y=Mathf.Max(node.Size.y,Fit(node.Label,settings,new Vector2(node.Size.x,64)).y+60);
            node.MeasuredLabel=node.Label;node.MeasuredFont=settings.GraphFontSize;node.MeasuredWidth=settings.GraphNodeWidth;node.MeasuredCustom=node.CustomSize;node.MeasuredRequest=node.Size;
        }
        public static float DisplayScale(float zoom,float font)=>Mathf.Max(zoom,11/13f);
    }
}
