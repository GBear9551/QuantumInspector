using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    public static class QuantumProjectPaneVerification
    {
        private static readonly Dictionary<string,List<Vector2>> rows=new Dictionary<string,List<Vector2>>();
        private static EditorWindow project;
        private static readonly List<string> events=new List<string>();
        public static object Begin()
        {
            rows.Clear();events.Clear();EditorApplication.projectWindowItemOnGUI-=Capture;
            EditorApplication.projectWindowItemOnGUI+=Capture;
            EditorApplication.ExecuteMenuItem("Window/General/Project");project=EditorWindow.focusedWindow;
            EditorApplication.RepaintProjectWindow();return new{ready=true};
        }
        private static void Capture(string guid,Rect rect)
        {
            if(Event.current.type!=EventType.Repaint&&Event.current.type!=EventType.Layout&&rect.Contains(Event.current.mousePosition))events.Add(AssetDatabase.GUIDToAssetPath(guid)+" "+Event.current.type+" raw="+Event.current.rawType);
            if(Event.current.type!=EventType.Repaint)return;
            var path=AssetDatabase.GUIDToAssetPath(guid);
            if(string.IsNullOrEmpty(path))return;
            var point=GUIUtility.GUIToScreenPoint(rect.center)-project.position.position;
            if(!rows.TryGetValue(path,out var points))rows.Add(path,points=new List<Vector2>());
            if(!points.Any(p=>Vector2.Distance(p,point)<1))points.Add(point);
        }
        public static object Read()=>rows.Select(p=>new{path=p.Key,folder=AssetDatabase.IsValidFolder(p.Key),points=p.Value.Select(v=>new{x=v.x,y=v.y}).ToArray()}).Take(35).ToArray();
        public static object Click(string path,int index=0)
        {
            var point=rows[path][index];
            project.SendEvent(new Event{type=EventType.MouseDown,button=0,clickCount=1,mousePosition=point});
            project.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=point});
            return new{clicked=path,x=point.x,y=point.y};
        }
        public static object State()=>new{selection=AssetDatabase.GetAssetPath(Selection.activeObject),events=events.TakeLast(10).ToArray(),views=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().Select(w=>new{locked=w.Pinned,pending=typeof(InspectorWheelWindow).GetField("pendingProjectItem",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(w),last=typeof(InspectorWheelWindow).GetField("lastProjectItem",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(w),tab=w.ActiveTab.ToString(),folder=w.CurrentScope.FolderPath,asset=AssetDatabase.GetAssetPath(w.CurrentScope.Asset)}).ToArray()};
        public static object Close(){EditorApplication.projectWindowItemOnGUI-=Capture;rows.Clear();return new{closed=true};}
    }
}
