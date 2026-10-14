using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public static class InspectorWheelInformationVerification
    {
        private static InspectorWheelWindow window;
        private static InspectorWheelSettings settings;
        private static float hiddenWidth;
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static object Begin()
        {
            Check(window==null,"Close the previous information-panel verification first.");
            settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();settings.Layouts=ResearchLayouts.None;
            window=ScriptableObject.CreateInstance<InspectorWheelWindow>();window.Pinned=true;
            typeof(InspectorWheelWindow).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,settings);
            window.position=new Rect(40,60,1180,700);window.ShowUtility();window.CreateGUI();
            if(Selection.activeGameObject!=null)window.Inspect(Selection.activeGameObject);
            return new{ready=true};
        }
        public static object Show()
        {
            var panels=window.rootVisualElement.Q(className:"quantum-panels");
            Check(panels.resolvedStyle.display==DisplayStyle.None,"Information panel is visible by default.");
            var button=window.rootVisualElement.Q<Button>("wheelInformationToggle");
            Check(button.text=="Show Info"&&button.resolvedStyle.display!=DisplayStyle.None,"Wheel header toggle is missing.");
            hiddenWidth=window.Wheel.contentRect.width;
            window.SetWheelInformationVisible(true);
            return new{ready=true,hiddenWidth};
        }
        public static object VerifyAndHide()
        {
            var panels=window.rootVisualElement.Q(className:"quantum-panels");
            Check(panels.resolvedStyle.display!=DisplayStyle.None,"Toggle did not reveal the panel.");
            var titles=panels.Query<Label>(className:"research-title").ToList().Select(l=>l.text).ToArray();
            Check(titles.Contains("COMPONENT DETAILS")&&titles.Contains("BOOKMARKS & HISTORY"),"Both requested information sections are not available.");
            Check(hiddenWidth>window.Wheel.contentRect.width+200,"Hidden information panel did not return its width to the wheel.");
            Check(settings.Layouts==ResearchLayouts.None,"Wheel toggle changed shared graph layout settings.");
            string saved=EditorJsonUtility.ToJson(window);window.SetWheelInformationVisible(false);
            EditorJsonUtility.FromJsonOverwrite(saved,window);window.CreateGUI();
            Check(window.rootVisualElement.Q<Button>("wheelInformationToggle").text=="Hide Info","Information visibility did not survive a UI rebuild.");
            window.SetGraphMode(true);
            Check(window.rootVisualElement.Q<Button>("wheelInformationToggle").ClassListContains("quantum-hidden"),"Wheel-only toggle appears in graph mode.");
            Check(window.rootVisualElement.Q(className:"quantum-header").Contains(window.rootVisualElement.Q("bookmarksButton"))&&!window.rootVisualElement.Q("wheelHeaderControls").Contains(window.rootVisualElement.Q("bookmarksButton")),"Bookmarks is hidden with wheel controls in graph mode.");
            window.SetGraphMode(false);
            Check(window.rootVisualElement.Q<Button>("wheelInformationToggle").text=="Hide Info","Switching modes lost the wheel visibility preference.");
            window.SetWheelInformationVisible(false);
            return new{passed=true,headerToggle=true,bothSections=true,moreWheelSpace=true,serializedVisibility=true,independentGraphSettings=true};
        }
        public static object VerifyHidden()
        {
            Check(window.rootVisualElement.Q(className:"quantum-panels").resolvedStyle.display==DisplayStyle.None,"Panel did not collapse again.");
            Check(Mathf.Abs(window.Wheel.contentRect.width-hiddenWidth)<2,"Wheel width was not restored.");
            return new{passed=true,restoredWidth=window.Wheel.contentRect.width};
        }
        public static object ResizeForReview(float width,float height)
        {
            Check(window!=null,"Call Begin first.");window.minSize=new Vector2(400,400);
            window.position=new Rect(20,40,width,height);window.Focus();
            return new{ready=true,width,height};
        }
        public static object VerifyResponsiveHeader()
        {
            Check(window!=null,"Call Begin first.");
            var header=window.rootVisualElement.Q(className:"quantum-header");
            var credit=window.rootVisualElement.Q<Label>("developerCredit");
            Check(credit!=null&&credit.text=="Developed by Gary Lougheed & Astra"&&credit.resolvedStyle.display!=DisplayStyle.None,"Developer credit is missing.");
            Check(credit.worldBound.yMax<=window.rootVisualElement.worldBound.yMax+1&&credit.worldBound.width>=credit.MeasureTextSize(credit.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x,"Developer credit is clipped.");
            var controls=window.rootVisualElement.Q("wheelHeaderControls");
            Check(controls.Contains(window.rootVisualElement.Q("projectTab"))&&controls.Contains(window.rootVisualElement.Q("hierarchyTab"))&&controls.Contains(window.rootVisualElement.Q("wheelInformationToggle")),"Navigation and Show Info do not share the header.");
            var buttons=controls.Query<Button>().ToList().Where(b=>b.resolvedStyle.display!=DisplayStyle.None).ToArray();
            foreach(var button in buttons)Check(button.worldBound.xMin>=header.worldBound.xMin-1&&button.worldBound.xMax<=header.worldBound.xMax+1&&button.worldBound.yMax<=header.worldBound.yMax+1,"Responsive header clips "+button.text+": "+button.worldBound+" outside "+header.worldBound);
            var bookmarks=window.rootVisualElement.Q<Button>("bookmarksButton");
            Check(controls.Contains(bookmarks)&&bookmarks.resolvedStyle.display!=DisplayStyle.None,"Bookmarks is not directly available in the header.");
            Check(window.Wheel.contentRect.height>window.rootVisualElement.contentRect.height*.65f,"Header leaves too little room for the wheel.");
            var node=window.Wheel.Nodes.FirstOrDefault(n=>n.Entry!=null);
            if(node!=null)Check(node.Q<Label>(className:"node-name").resolvedStyle.unityFontStyleAndWeight==FontStyle.Bold,"Wheel node label is not bold.");
            return new{passed=true,width=window.rootVisualElement.contentRect.width,headerHeight=header.contentRect.height,wheelHeight=window.Wheel.contentRect.height,scale=window.Wheel.Stage.resolvedStyle.scale.value.x,compact=window.rootVisualElement.ClassListContains("wheel-compact"),narrow=window.rootVisualElement.ClassListContains("wheel-narrow")};
        }
        public static object Close()
        {
            if(window!=null)window.Close();window=null;
            if(settings!=null)UnityEngine.Object.DestroyImmediate(settings);settings=null;
            return new{closed=true};
        }
    }
}
