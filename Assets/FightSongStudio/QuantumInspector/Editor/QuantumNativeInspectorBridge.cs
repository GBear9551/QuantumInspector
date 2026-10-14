using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    /// <summary>Uses public UI Toolkit APIs, with guarded row-name matching for Unity 6000.3.</summary>
    [InitializeOnLoad]
    public static class QuantumNativeInspectorBridge
    {
        private static readonly Dictionary<EditorWindow,InspectorState> inspectors=new Dictionary<EditorWindow,InspectorState>();
        private static double nextScan;
        private static int revision;
        private static GameObject requestedObject;
        private static Component requestedComponent;
        private static string requestedQuery="";
        private static HashSet<int> requestedMatches;
        private static double requestExpires;

        static QuantumNativeInspectorBridge()
        {
            EditorApplication.update+=Update;
            AssemblyReloadEvents.beforeAssemblyReload+=Cleanup;
            EditorApplication.quitting+=Cleanup;
        }

        public static void Reveal(Component component)
        {
            if(component==null)return;
            requestedObject=component.gameObject;requestedComponent=component;
            requestedQuery="";requestedMatches=null;revision++;nextScan=0;requestExpires=EditorApplication.timeSinceStartup+2;
            // The shared tracker belongs to the normal selection-following Inspector.
            // Do not change a tracker lock or another Inspector's inspected object.
            var tracker=ActiveEditorTracker.sharedTracker;
            var editors=tracker.activeEditors;
            for(int i=0;i<editors.Length;i++)
                if(editors[i]!=null&&editors[i].target==component)tracker.SetVisible(i,1);
        }

        public static void Search(GameObject target,string query,IEnumerable<Component> matches=null)
        {
            if(target==null)return;
            requestedObject=target;requestedComponent=null;requestedQuery=query??"";
            requestedMatches=matches==null?null:new HashSet<int>(matches.Where(c=>c!=null&&c.gameObject==target).Select(c=>c.GetInstanceID()));
            revision++;nextScan=0;requestExpires=EditorApplication.timeSinceStartup+2;
        }

        private static void Update()
        {
            if(EditorApplication.isCompiling||EditorApplication.timeSinceStartup<nextScan)return;
            nextScan=EditorApplication.timeSinceStartup+.25;
            var windows=Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach(var window in windows)
            {
                var header=window.rootVisualElement.Q(className:"game-object-inspector");
                if(header==null||!(ResolveRow(header) is GameObject target))
                {
                    if(inspectors.TryGetValue(window,out var old)){old.Dispose();inspectors.Remove(window);}
                    continue;
                }
                if(!inspectors.TryGetValue(window,out var state))inspectors.Add(window,state=new InspectorState(window));
                state.Update(header,target);
            }
            foreach(var dead in inspectors.Keys.Where(w=>w==null||!windows.Contains(w)).ToArray())
            {inspectors[dead].Dispose();inspectors.Remove(dead);}
        }

        internal static UnityEngine.Object ResolveRow(VisualElement row)
        {
            string name=row.name??"";int separator=name.LastIndexOf('_');
            if(separator<0||!int.TryParse(name.Substring(separator+1),out int id))return null;
            var target=EditorUtility.EntityIdToObject((EntityId)id);
            return target!=null&&name.Contains("_"+target.GetType().Name+"_")?target:null;
        }

        private static void Cleanup()
        {
            foreach(var state in inspectors.Values)state.Dispose();inspectors.Clear();
        }

        private sealed class InspectorState:IDisposable
        {
            private readonly EditorWindow window;
            private readonly VisualElement toolbar=new VisualElement{name="quantumInspectorComponentToolbar"};
            private readonly ToolbarSearchField search=new ToolbarSearchField{name="quantumInspectorComponentSearch"};
            private readonly Label count=new Label();
            private readonly Label empty=new Label("No matching components. Clear the search to show all components.");
            private readonly Dictionary<VisualElement,StyleEnum<DisplayStyle>> hidden=new Dictionary<VisualElement,StyleEnum<DisplayStyle>>();
            private GameObject target;
            private VisualElement rows;
            private string query="";
            private HashSet<int> matches;
            private int seenRevision;
            private Component pending;
            private double scrollUntil;
            private bool scrollNeeded;
            private bool dirty=true;
            private int rowSignature;

            public InspectorState(EditorWindow window)
            {
                this.window=window;
                toolbar.style.flexShrink=0;toolbar.style.paddingLeft=6;toolbar.style.paddingRight=6;toolbar.style.paddingTop=4;toolbar.style.paddingBottom=4;
                var line=new VisualElement();line.style.flexDirection=FlexDirection.Row;toolbar.Add(line);
                var label=new Label("Components");label.style.unityTextAlign=TextAnchor.MiddleLeft;label.style.marginRight=4;line.Add(label);
                search.tooltip="Filter components by type, alias or category. Quantum Inspector searches synchronize here. Clearing restores all components.";
                search.style.flexGrow=1;search.style.minWidth=40;line.Add(search);
                count.style.marginLeft=6;count.style.unityTextAlign=TextAnchor.MiddleRight;line.Add(count);
                empty.style.whiteSpace=WhiteSpace.Normal;empty.style.fontSize=11;empty.style.marginTop=4;toolbar.Add(empty);
                search.RegisterValueChangedCallback(e=>{query=e.newValue??"";matches=null;pending=null;scrollNeeded=true;dirty=true;Apply();});
                window.rootVisualElement.Insert(0,toolbar);
            }

            public void Update(VisualElement header,GameObject owner)
            {
                if(target!=owner||rows!=header.parent)
                {
                    Restore();target=owner;rows=header.parent;query="";matches=null;pending=null;
                    search.SetValueWithoutNotify("");scrollNeeded=false;dirty=true;
                }
                if(seenRevision!=revision&&target==requestedObject&&EditorApplication.timeSinceStartup<requestExpires)
                {
                    seenRevision=revision;
                    query=requestedQuery;matches=requestedMatches;pending=requestedComponent;
                    search.SetValueWithoutNotify(query);scrollNeeded=true;dirty=true;
                }
                if(toolbar.parent==null)window.rootVisualElement.Insert(0,toolbar);
                int signature=17;
                foreach(var row in rows.Children())unchecked{signature=signature*31+row.GetHashCode();signature=signature*31+(row.name??"").GetHashCode();}
                if(signature!=rowSignature){rowSignature=signature;dirty=true;}
                if(dirty)Apply();
                if(pending!=null&&EditorApplication.timeSinceStartup<scrollUntil)ScrollTo(pending);
            }

            private void Apply()
            {
                if(target==null||rows==null)return;
                dirty=false;
                Restore();
                var entries=new ComponentDiscoveryService().Discover(target,null,false);
                var ids=matches??new HashSet<int>(ComponentDiscoveryService.Filter(entries,query).Where(e=>e.Component!=null).Select(e=>e.Component.GetInstanceID()));
                int total=0,visible=0;Component first=null;
                foreach(var row in rows.Children())
                {
                    if(!(ResolveRow(row) is Component component)||component.gameObject!=target)continue;
                    total++;
                    if(string.IsNullOrWhiteSpace(query)||ids.Contains(component.GetInstanceID()))
                    {visible++;if(first==null)first=component;}
                    else{hidden[row]=row.style.display;row.style.display=DisplayStyle.None;}
                }
                count.text=visible+" / "+total;
                empty.style.display=visible==0&&!string.IsNullOrWhiteSpace(query)?DisplayStyle.Flex:DisplayStyle.None;
                if(scrollNeeded)
                {
                    scrollNeeded=false;pending=pending!=null?pending:!string.IsNullOrWhiteSpace(query)?first:null;
                    if(pending!=null)
                    {
                        var tracker=ActiveEditorTracker.sharedTracker;
                        var editors=tracker.activeEditors;
                        for(int i=0;i<editors.Length;i++)if(editors[i]!=null&&editors[i].target==pending)tracker.SetVisible(i,1);
                        scrollUntil=EditorApplication.timeSinceStartup+.8;
                        var component=pending;toolbar.schedule.Execute(()=>ScrollTo(component)).ExecuteLater(50);
                    }
                }
            }

            private void ScrollTo(Component component)
            {
                if(component==null||rows==null||toolbar.panel==null)return;
                var row=rows.Children().FirstOrDefault(e=>ResolveRow(e)==component);
                var scroll=rows.GetFirstAncestorOfType<ScrollView>();
                if(row==null||scroll==null||row.resolvedStyle.display==DisplayStyle.None)return;
                float offset=scroll.scrollOffset.y+row.worldBound.yMin-scroll.contentViewport.worldBound.yMin;
                if(float.IsFinite(offset))scroll.scrollOffset=new Vector2(scroll.scrollOffset.x,Mathf.Clamp(offset,0,scroll.verticalScroller.highValue));
                window.Repaint();
            }

            private void Restore()
            {
                foreach(var pair in hidden)pair.Key.style.display=pair.Value;
                hidden.Clear();
            }
            public void Dispose(){Restore();toolbar.RemoveFromHierarchy();}
        }
    }
}
