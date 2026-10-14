using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public static class QuantumInspectorWorkspaceVerification
    {
        private static Dictionary<EditorWindow,Rect> bounds;
        private static EditorWindow originalFocus;
        private static QuantumInspectorWorkspaceGuide guide;
        public static object Begin()
        {
            if(bounds!=null)throw new InvalidOperationException("Close the previous verification first.");
            originalFocus=EditorWindow.focusedWindow;
            bounds=Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>!(w is QuantumInspectorWorkspaceGuide)).ToDictionary(w=>w,w=>w.position);
            guide=ScriptableObject.CreateInstance<QuantumInspectorWorkspaceGuide>();
            guide.position=new Rect(80,80,420,520);guide.ShowUtility();
            return new{ready=true};
        }
        public static object Verify()
        {
            if(guide==null||guide.rootVisualElement.panel==null)throw new InvalidOperationException("Setup guide is not visible.");
            var buttons=guide.rootVisualElement.Query<Button>().ToList();
            if(buttons.Count!=5)throw new InvalidOperationException("Setup shortcuts are missing.");
            foreach(var pair in bounds)if(pair.Key!=null&&pair.Key.position!=pair.Value)throw new InvalidOperationException("Setup guide changed an existing window's bounds.");
            return new{passed=true,guideVisible=true,shortcuts=buttons.Count,existingLayoutPreserved=true};
        }
        public static object Close()
        {
            if(guide!=null)guide.Close();guide=null;bounds=null;
            if(originalFocus!=null)originalFocus.Focus();return new{closed=true};
        }
    }
}
