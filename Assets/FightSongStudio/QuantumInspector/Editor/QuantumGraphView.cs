using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public sealed class QuantumGraphView:VisualElement,IDisposable
    {
        public readonly VisualElement Viewport=new VisualElement();
        public readonly VisualElement CompactControls=new VisualElement{name="graphCompactControls"};
        private readonly VisualElement auxiliaryUi=new VisualElement{name="graphAuxiliaryInfo"};
        private readonly VisualElement auxiliarySearchRow=new VisualElement();
        private readonly VisualElement settingsPanel=new VisualElement{name="graphSettingsPanel",focusable=true};
        private readonly Toggle pauseExpansion=new Toggle("Pause expansion");
        private readonly Toggle selectionLockPreference=new Toggle("Lock Unity selection"){name="graphSelectionLock"};
        private Func<bool> selectionLocked;
        private Action<bool> setSelectionLocked;
        private readonly Toggle showAuxiliary=new Toggle("Show auxiliary UI"){name="graphShowAuxiliary"};
        private readonly Label settingsSelection=new Label();
        private readonly Button compactUpdates=new Button();
        public Action RestoreDefaultsRequested,OpenPresetRequested;
        public Action<string> SearchChanged;
        public readonly QuantumGraphViewportController Camera=new QuantumGraphViewportController();
        public readonly QuantumGraphLayoutEngine Layout=new QuantumGraphLayoutEngine();
        public readonly QuantumGraphDataProvider Provider=new QuantumGraphDataProvider();
        public readonly QuantumGraphMotion Motion=new QuantumGraphMotion();
        public readonly QuantumGraphPreviewCache Previews=new QuantumGraphPreviewCache();
        public QuantumGraphDocument Document=>Provider.Document;
        public readonly HashSet<string> Selection=new HashSet<string>();
        public QuantumGraphNode Selected=>selectedId!=null&&Document!=null&&Document.Nodes.TryGetValue(selectedId,out var node)?node:null;
        private readonly Dictionary<string,QuantumGraphNodeView> shown=new Dictionary<string,QuantumGraphNodeView>();
        private readonly Stack<QuantumGraphNodeView> pool=new Stack<QuantumGraphNodeView>();
        private readonly Dictionary<string,Vector2> screenAnchors=new Dictionary<string,Vector2>();
        private readonly List<QuantumGraphEdge> visibleEdges=new List<QuantumGraphEdge>();
        private readonly List<Label> edgeBadges=new List<Label>();
        private readonly List<QuantumGraphDot> pointMarks=new List<QuantumGraphDot>();
        private readonly QuantumGraphOverview zoomOverview=new QuantumGraphOverview();
        private readonly Dictionary<string,Vector2> overviewDragOffsets=new Dictionary<string,Vector2>();
        private HashSet<QuantumGraphNode> inView=new HashSet<QuantumGraphNode>();
        private readonly HashSet<string> matches=new HashSet<string>();
        private readonly Action<QuantumGraphNode> select,navigate;
        private readonly Action<Object> bookmark;
        private readonly Action<bool> entireChanged,componentAction;
        private readonly Action<Action> configure;
        private readonly Action<string> status;
        private readonly Label progress=new Label();
        private readonly Label relationshipDetail=new Label();
        private readonly EnumField motionChoice=new EnumField(QuantumGraphMotionMode.Full);
        private readonly EnumField paletteChoice=new EnumField(QuantumGraphPalette.Editor);
        private readonly EnumField qualityChoice=new EnumField(QuantumGraphQuality.Balanced);
        private readonly Slider wheelFontScale=new Slider("Wheel font scale (×)",1,3){showInputField=true,name="wheelFontScale"};
        private readonly Slider nodeFontSize=new Slider("Graph nodes",10,24){showInputField=true,name="nodeFontSize"};
        private readonly Slider buttonFontSize=new Slider("Buttons",10,24){showInputField=true,name="buttonFontSize"};
        private readonly Slider dotSize=new Slider("Dot size",1,6){showInputField=true,name="graphDotSize"};
        private readonly Toggle ambientChoice=new Toggle("Ambient"),lockedAmbientChoice=new Toggle("Ambient while locked");
        private readonly QuantumNebulaBackdrop backdrop=new QuantumNebulaBackdrop();
        private readonly VisualElement graphArt=new VisualElement{pickingMode=PickingMode.Ignore};
        internal readonly QuantumGraphComponentInspector ComponentInspector;
        private double selectionStarted=-1;
        private float selectionFeedback;
        private bool suspended;
        public double MaximumDrawMilliseconds {get;private set;}
        public double MaximumViewMilliseconds {get;private set;}
        public readonly QuantumGraphDrawMetrics DrawMetrics=new QuantumGraphDrawMetrics();
        public double BackdropGenerationMilliseconds=>backdrop.GenerationMilliseconds;
        private readonly WheelSearchField search=new WheelSearchField();
        private readonly Toggle investigation=new Toggle("Investigation Lock"),entire=new Toggle("Entire Project Graph");
        private InspectorWheelSettings settings;
        private string selectedId,query="",contextKey,pendingAssetPath;
        private bool dirtyState,initialFit,paused,restoreSelection;
        private bool overview,detail,detached,needsDraw;
        private bool cameraMoving;
        private Vector2 cameraFrom,cameraTarget;
        private float zoomFrom,zoomTarget;
        private double cameraStarted;
        private QuantumGraphNode[] preparing;
        private int prepareIndex,prepareGeneration,prepareRevision;
        private bool prepareDeliberate,prepareForce;
        private QuantumGraphWindowHandoff handoff;
        private readonly HashSet<string> handedOff=new HashSet<string>();
        private readonly Dictionary<(Object,Object,string,string,string,QuantumGraphNodeKind),QuantumGraphWindowHandoff.Entry> handoffEntries=new Dictionary<(Object,Object,string,string,string,QuantumGraphNodeKind),QuantumGraphWindowHandoff.Entry>();
        public double MaximumPreparationSliceMs {get;private set;}
        private string geometryKey,dataKey;
        private QuantumGraphMotionMode previousMotion;
        public int ViewUpdates {get;private set;}
        public int VisibleCount=>shown.Count;
        public int VisibleDotCount=>pointMarks.Count;
        public int VisibleEdgeCount=>visibleEdges.Count;
        public Action CancelInteraction;
        private double lastSave;
        public QuantumGraphView(InspectorWheelSettings settings,Action<QuantumGraphNode> select,Action<QuantumGraphNode> navigate,Action<Object> bookmark,
            Action<bool> entireChanged,Action<bool> componentAction,Action<Action> configure,Action<string> status)
        {
            this.settings=settings;this.select=select;this.navigate=navigate;this.bookmark=bookmark;this.entireChanged=entireChanged;this.componentAction=componentAction;this.configure=configure;this.status=status;
            ComponentInspector=new QuantumGraphComponentInspector(()=>graphArt.MarkDirtyRepaint());
            AddToClassList("quantum-graph-workspace");
            CompactControls.AddToClassList("graph-compact-toolbar");Add(CompactControls);
            Button(CompactControls,"Fit",Fit);
            CompactControls.Add(search);search.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return){NextMatch();e.StopPropagation();}});
            compactUpdates.clicked+=ApplyUpdates;compactUpdates.AddToClassList("graph-compact-updates");CompactControls.Add(compactUpdates);
            var settingsButton=new Button(ToggleSettings){text="Settings",name="graphSettingsButton",tooltip="Appearance, motion, graph actions, auxiliary information and clean defaults."};CompactControls.Add(settingsButton);
            Add(auxiliaryUi);
            settingsPanel.AddToClassList("graph-settings-panel");settingsPanel.style.display=DisplayStyle.None;
            var settingsHeader=new VisualElement();settingsHeader.AddToClassList("graph-settings-heading");settingsPanel.Add(settingsHeader);
            settingsHeader.Add(new Label("GRAPH SETTINGS"));Button(settingsHeader,"Close",()=>SetSettingsOpen(false));
            var settingsContent=new ScrollView();settingsPanel.Add(settingsContent);
            settingsContent.Add(new Label("FONT SIZES"));
            nodeFontSize.tooltip="Graph node font size. Nodes grow to fit the text.";
            wheelFontScale.tooltip="Scale the existing wheel preset proportionally: 1 = 100%, 1.5 = 150%, 2 = 200%, 3 = 300%.";
            wheelFontScale.RegisterValueChangedCallback(e=>configure(()=>settings.OrbitalFontScale=e.newValue));
            buttonFontSize.tooltip="Independent button font size. Both font settings are saved with your preset.";
            nodeFontSize.RegisterValueChangedCallback(e=>configure(()=>settings.GraphFontSize=e.newValue));
            buttonFontSize.RegisterValueChangedCallback(e=>configure(()=>settings.ButtonFontSize=e.newValue));
            settingsContent.Add(wheelFontScale);settingsContent.Add(nodeFontSize);settingsContent.Add(buttonFontSize);
            showAuxiliary.tooltip="Show navigation help, graph legends, relationship text, status and research panels. Off gives the largest graph field.";
            showAuxiliary.RegisterValueChangedCallback(e=>configure(()=>settings.GraphShowAuxiliary=e.newValue));settingsContent.Add(showAuxiliary);
            Button(settingsContent,"Restore clean defaults",RestoreDefaults);
            Button(settingsContent,"Open preset",()=>{if(OpenPresetRequested!=null)OpenPresetRequested();else EditorUtility.OpenPropertyEditor(settings);});
            settingsContent.Add(new Label("APPEARANCE & INVESTIGATION"){name="graphPreferencesHeading"});
            var toolbar=new VisualElement();toolbar.AddToClassList("graph-preferences-form");settingsContent.Add(toolbar);
            investigation.tooltip="Freeze automatic layout and queue content changes. Project assets, folders and GameObject selections still switch the graph unless Lock Unity selection is enabled. Apply Updates accepts pending changes without moving existing anchors.";
            investigation.RegisterValueChangedCallback(e=>configure(()=>settings.GraphInvestigationLock=e.newValue));toolbar.Add(investigation);
            selectionLockPreference.RegisterValueChangedCallback(e=>setSelectionLocked?.Invoke(e.newValue));selectionLockPreference.SetEnabled(false);toolbar.Add(selectionLockPreference);
            entire.RegisterValueChangedCallback(e=>entireChanged(e.newValue));toolbar.Add(entire);
            var actions=new VisualElement();actions.AddToClassList("graph-toolbar");settingsContent.Add(new Label("GRAPH ACTIONS"));settingsContent.Add(actions);
            Button(actions,"Fit Entire Graph",Fit);Button(actions,"Center selected",CenterSelected);
            Button(actions,"Apply Updates",ApplyUpdates);Button(actions,"Arrange",()=>AutoLayout(false));Button(actions,"Reset Layout",()=>AutoLayout(true));
            motionChoice.tooltip="Full: coordinated expansion (320 ms), collapse (240 ms), focus (340 ms). Reduced / Off: immediate spatial changes; no ambient movement.";
            motionChoice.label="Motion";paletteChoice.label="Theme";qualityChoice.label="Quality";
            motionChoice.RegisterValueChangedCallback(e=>configure(()=>settings.GraphMotion=(QuantumGraphMotionMode)e.newValue));toolbar.Add(motionChoice);
            paletteChoice.tooltip="Graph palette only. Does not change membership, geometry or motion timing.";paletteChoice.SetValueWithoutNotify(settings.GraphPalette);
            paletteChoice.RegisterValueChangedCallback(e=>configure(()=>settings.GraphPalette=(QuantumGraphPalette)e.newValue));toolbar.Add(paletteChoice);
            qualityChoice.tooltip="Cosmetic quality only. Dense graphs reduce decoration before information.";
            qualityChoice.RegisterValueChangedCallback(e=>configure(()=>settings.GraphQuality=(QuantumGraphQuality)e.newValue));toolbar.Add(qualityChoice);
            dotSize.tooltip="Overview dot diameter multiplier. 2 is twice the original size; raise it for larger dots. Saved as a personal preference.";
            dotSize.AddToClassList("graph-dot-size");dotSize.SetValueWithoutNotify(settings.GraphDotSize);
            dotSize.RegisterValueChangedCallback(e=>configure(()=>settings.GraphDotSize=e.newValue));toolbar.Add(dotSize);
            ambientChoice.RegisterValueChangedCallback(e=>configure(()=>settings.GraphAmbient=e.newValue));toolbar.Add(ambientChoice);
            lockedAmbientChoice.tooltip="Explicitly allow background-only motion during a locked investigation. Reduced / Off always stop it.";
            lockedAmbientChoice.RegisterValueChangedCallback(e=>configure(()=>settings.GraphAmbientWhileLocked=e.newValue));toolbar.Add(lockedAmbientChoice);
            var clusters=new ToolbarMenu{text="Clusters"};
            clusters.menu.AppendAction("Expand all loaded clusters",_=>SetClusters(true));clusters.menu.AppendAction("Collapse all clusters",_=>SetClusters(false));
            clusters.menu.AppendAction("Expand next folder level",_=>{foreach(var n in Document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Folder&&Document.Visible(n)&&!n.Loaded).ToArray())Provider.Expand(n);Changed();});actions.Add(clusters);
            var overlays=new ToolbarMenu{text="Overlays"};
            foreach(QuantumGraphOverlays kind in new[]{QuantumGraphOverlays.SerializedReferences,QuantumGraphOverlays.AssetDependencies,QuantumGraphOverlays.PrefabSources})
            {var captured=kind;overlays.menu.AppendAction(kind.ToString(),_=>configure(()=>settings.GraphOverlays^=captured),_=>(settings.GraphOverlays&captured)!=0?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);}
            actions.Add(overlays);
            pauseExpansion.RegisterValueChangedCallback(e=>paused=e.newValue);toolbar.Add(pauseExpansion);
            var searchRow=auxiliarySearchRow;searchRow.AddToClassList("graph-toolbar");auxiliaryUi.Add(searchRow);
            search.name="graphSearch";search.AddToClassList("graph-search");search.tooltip="Highlight names, aliases, classes and paths in the discovered graph. Hidden descendants are revealed by Next match.";
            search.RegisterCallback<ChangeEvent<string>>(e=>{query=e.newValue;UpdateMatches();Changed();SearchChanged?.Invoke(query);});
            Button(searchRow,"Next match",NextMatch);progress.AddToClassList("graph-progress");searchRow.Add(progress);
            Button(searchRow,"Settings",ToggleSettings);
            var legend=new Label("— owned   —▶ contains / field   ··· grouping   – –▶ reference   – –◆ dependency (direct)");legend.AddToClassList("graph-legend");auxiliaryUi.Add(legend);
            relationshipDetail.AddToClassList("graph-relationship-detail");relationshipDetail.text="Select a node to see verified relationships. Drag a category header or enclosure to move its members. Drag outside clusters to pan; Alt-drag or middle-drag pans over clusters. Wheel zooms about the pointer.";auxiliaryUi.Add(relationshipDetail);
            settingsContent.Add(new Label("SELECTION & HELP"));settingsSelection.AddToClassList("graph-settings-selection");settingsContent.Add(settingsSelection);
            settingsContent.Add(new Label("Drag a category header or its empty enclosure to move its members. Alt-drag or middle-drag pans. Scroll to zoom. Double-click a dot to focus it. Enter in search reveals the next match. F fits the graph."){name="graphSettingsHelp"});
            Viewport.AddToClassList("quantum-graph-viewport");Viewport.name="graphViewport";Viewport.focusable=true;Add(Viewport);
            Add(settingsPanel);settingsPanel.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Escape){SetSettingsOpen(false);e.StopPropagation();}});
            Viewport.RegisterCallback<PointerDownEvent>(_=>SetSettingsOpen(false),TrickleDown.TrickleDown);
            ConfigurePresentation();
            RegisterCallback<DetachFromPanelEvent>(e=>{if(e.target==this){detached=true;SuspendPresentation(true);}});
            RegisterCallback<AttachToPanelEvent>(e=>{if(e.target==this){detached=false;SuspendPresentation(false);needsDraw=true;}});
            Viewport.Add(backdrop);graphArt.AddToClassList("graph-art-layer");Viewport.Add(graphArt);graphArt.generateVisualContent+=Draw;Viewport.Add(ComponentInspector);
            Viewport.RegisterCallback<GeometryChangedEvent>(_=>{if(initialFit&&!Provider.Busy)Fit();UpdateViews();});
            Viewport.AddManipulator(new QuantumGraphManipulator(this));
            Viewport.RegisterCallback<PointerMoveEvent>(e=>{if(!IsInspectorTarget(e.target)&&e.pressedButtons==0&&zoomOverview.Blend>.5f){var node=Hit(Viewport.WorldToLocal(e.position));Viewport.tooltip=node==null?"":node.Label+"\n"+node.Subtitle+(node.Pinned?"\nPosition pinned":"")+(QuantumGraphOverview.Summary(node,Document)?"\nDrag the category to move its members; right-click for actions.":"\nDouble-click to focus; right-click for actions.");}});
            Viewport.AddManipulator(new ContextualMenuManipulator(e=>{var element=e.target as VisualElement;if(IsInspectorTarget(e.target)||element is QuantumGraphNodeView||element?.GetFirstAncestorOfType<QuantumGraphNodeView>()!=null)return;var node=Hit(Viewport.WorldToLocal(e.mousePosition));if(node!=null)Menu(node,e);}));
            Viewport.RegisterCallback<KeyDownEvent>(e=>
            {
                if(IsInspectorTarget(e.target))return;
                if(e.keyCode==KeyCode.F){Fit();e.StopPropagation();}
                else if(e.keyCode==KeyCode.Return&&Selected!=null){Toggle(Selected);e.StopPropagation();}
                else if(e.keyCode==KeyCode.Escape){CancelInteraction?.Invoke();Selection.Clear();selectedId=null;Changed();e.StopPropagation();}
            });
        }
        private static void Button(VisualElement parent,string text,Action action)=>parent.Add(new Button(action){text=text});
        public void ToggleSettings()=>SetSettingsOpen(settingsPanel.style.display.value!=DisplayStyle.Flex);
        public void BindSelectionLock(Func<bool> getter,Action<bool> setter){selectionLocked=getter;setSelectionLocked=setter;selectionLockPreference.SetEnabled(true);RefreshSelectionLock();}
        public void RefreshSelectionLock(){if(selectionLocked!=null)selectionLockPreference.SetValueWithoutNotify(selectionLocked());}
        private void SetSettingsOpen(bool open){settingsPanel.style.display=open?DisplayStyle.Flex:DisplayStyle.None;if(open){settingsSelection.text=relationshipDetail.text;settingsPanel.Focus();}}
        public void RestoreDefaults()
        {
            if(RestoreDefaultsRequested!=null)RestoreDefaultsRequested();
            else configure(()=>QuantumGraphPreferences.RestoreDefaults(settings));
            paused=false;pauseExpansion.SetValueWithoutNotify(false);SetSettingsOpen(false);
        }
        private void ConfigurePresentation()
        {
            RefreshSelectionLock();
            showAuxiliary.SetValueWithoutNotify(settings.GraphShowAuxiliary);
            auxiliaryUi.style.display=settings.GraphShowAuxiliary?DisplayStyle.Flex:DisplayStyle.None;
            CompactControls.style.display=settings.GraphShowAuxiliary?DisplayStyle.None:DisplayStyle.Flex;
            var searchParent=settings.GraphShowAuxiliary?auxiliarySearchRow:CompactControls;
            if(search.parent!=searchParent)searchParent.Insert(settings.GraphShowAuxiliary?0:1,search);
            settingsSelection.text=relationshipDetail.text;
            UpdateCompactStatus();
        }
        private void UpdateCompactStatus()
        {
            bool pending=Provider.PendingChanges>0,busy=Provider.Busy;
            compactUpdates.style.display=pending||busy?DisplayStyle.Flex:DisplayStyle.None;
            compactUpdates.text=pending?Provider.PendingChanges+" updates":"Loading…";compactUpdates.tooltip=Provider.Status+"\n"+Provider.ChangeSummary;
            compactUpdates.SetEnabled(pending&&!busy);
        }
        public void Bind(WheelScope scope,bool wholeProject,InspectorWheelSettings value)
        {
            settings=value;investigation.SetValueWithoutNotify(settings.GraphInvestigationLock);
            entire.EnableInClassList("quantum-hidden",scope.Kind!=WheelScopeKind.ProjectFolder);entire.SetValueWithoutNotify(wholeProject);
            string key=QuantumGraphDataProvider.KeyFor(scope,wholeProject,settings);
            if(contextKey==key)return;
            var rekey=scope.Object!=null&&Document?.Root?.Target==scope.Object?QuantumGraphWindowHandoff.Capture(this):null;
            ComponentInspector.Hide();ComponentInspector.Reopen();
            SaveState();contextKey=key;handoff=null;handedOff.Clear();overviewDragOffsets.Clear();preparing=null;cameraMoving=false;Motion.Reset();Previews.Clear();Provider.Open(scope,wholeProject,settings);Selection.Clear();selectedId=null;pendingAssetPath=null;
            var saved=QuantumGraphStatePersistence.instance.Get(Document.Key);query=saved?.Search??"";search.SetValueWithoutNotify(query);
            Camera.Pan=saved?.Pan??Vector2.zero;Camera.Zoom=saved?.Zoom??1;initialFit=saved==null||!saved.HasViewport;
            if(!float.IsFinite(Camera.Zoom)||!float.IsFinite(Camera.Pan.x)||!float.IsFinite(Camera.Pan.y)){Camera.Pan=Vector2.zero;Camera.Zoom=1;initialFit=true;}
            zoomOverview.Target(Camera.Zoom,settings.EffectiveGraphMotion,EditorApplication.timeSinceStartup,true);
            selectedId=saved?.Selected;restoreSelection=!string.IsNullOrEmpty(selectedId);if(restoreSelection)Selection.Add(selectedId);
            geometryKey=GeometryKey();dataKey=DataKey();previousMotion=settings.EffectiveGraphMotion;motionChoice.SetValueWithoutNotify(settings.GraphMotion);
            ConfigureAppearance();Layout.Arrange(Document,settings);Motion.Commit(Document,EditorApplication.timeSinceStartup,settings.EffectiveGraphMotion,true);UpdateMatches();UpdateViews();dirtyState=true;
            if(rekey!=null)RestoreHandoff(rekey);
            if(initialFit&&!Provider.Busy)Fit();
        }
        private string GeometryKey()=>settings.GraphNodeWidth+"|"+settings.GraphFontSize+"|"+settings.GraphNodeGap+"|"+settings.GraphMinimumClusterRadius+"|"+settings.GraphBaseDistance+"|"+settings.GraphSafetyGap+"|"+settings.GraphBranchLength;
        private string DataKey()=>settings.GraphOverlays+"|"+settings.GraphProjectGrouping+"|"+string.Join(";",settings.GraphCategoryRules.Select(r=>r==null?"":r.Enabled+"|"+r.Match+"|"+r.Pattern+"|"+r.Category+"|"+r.AdditionalTags));
        public void Configure(InspectorWheelSettings value)
        {
            settings=value;investigation.SetValueWithoutNotify(settings.GraphInvestigationLock);motionChoice.SetValueWithoutNotify(settings.GraphMotion);Provider.Configure(value);
            paletteChoice.SetValueWithoutNotify(settings.GraphPalette);
            ConfigureAppearance();
            EnableInClassList("graph-light",settings.GraphPalette==QuantumGraphPalette.Light||settings.GraphPalette==QuantumGraphPalette.Editor&&!EditorGUIUtility.isProSkin);
            if(Document==null)return;
            if(geometryKey!=GeometryKey()){geometryKey=GeometryKey();CommitLayout(true);}
            if(dataKey!=DataKey()){dataKey=DataKey();ApplyUpdates();}
            if(previousMotion!=settings.EffectiveGraphMotion){previousMotion=settings.EffectiveGraphMotion;Motion.Commit(Document,EditorApplication.timeSinceStartup,previousMotion,false);if(cameraMoving){Camera.Pan=cameraTarget;Camera.Zoom=zoomTarget;cameraMoving=false;}}
            UpdateViews();
        }
        private void ConfigureAppearance()
        {
            ConfigurePresentation();
            wheelFontScale.SetValueWithoutNotify(settings.OrbitalFontScale);nodeFontSize.SetValueWithoutNotify(settings.GraphFontSize);buttonFontSize.SetValueWithoutNotify(settings.ButtonFontSize);
            QuantumInspectorTypography.ApplyButtons(this,settings.ButtonFontSize);
            EnableInClassList("graph-cosmic",settings.GraphPalette==QuantumGraphPalette.CosmicNebula);
            qualityChoice.SetValueWithoutNotify(settings.GraphQuality);ambientChoice.SetValueWithoutNotify(settings.GraphAmbient);
            dotSize.SetValueWithoutNotify(settings.GraphDotSize);
            lockedAmbientChoice.SetValueWithoutNotify(settings.GraphAmbientWhileLocked);
            lockedAmbientChoice.EnableInClassList("quantum-hidden",!settings.GraphAmbient||!settings.GraphInvestigationLock);
            backdrop.Configure(settings);backdrop.Tick(EditorApplication.timeSinceStartup,!suspended&&settings.AmbientEnabled);
            if(settings.EffectiveGraphMotion!=QuantumGraphMotionMode.Full){selectionStarted=-1;selectionFeedback=0;}
        }
        public void SuspendPresentation(bool value)
        {
            suspended=value;
            if(value)ComponentInspector.Hide();
            if(value){CancelInteraction?.Invoke();if(Document!=null)Motion.Commit(Document,EditorApplication.timeSinceStartup,QuantumGraphMotionMode.Off,false);if(cameraMoving){Camera.Pan=cameraTarget;Camera.Zoom=zoomTarget;}cameraMoving=false;zoomOverview.Target(Camera.Zoom,settings.EffectiveGraphMotion,EditorApplication.timeSinceStartup,true);selectionStarted=-1;selectionFeedback=0;backdrop.Tick(0,false);}
            else needsDraw=true;
        }
        public void Refresh(bool projectChanged=false)
        {
            if(Document==null)return;
            if(settings.GraphInvestigationLock){Provider.CheckPending(projectChanged);UpdateViews();return;}
            Provider.Refresh(projectChanged);CommitLayout(false);UpdateMatches();UpdateViews();
        }
        public void ApplyUpdates(){if(Document==null)return;Provider.ApplyPending();CommitLayout(true);UpdateMatches();Changed();}
        private void CommitLayout(bool deliberate,bool force=false)
        {
            if(Document.Nodes.Count>128)
            {preparing=Document.Nodes.Values.ToArray();prepareIndex=0;prepareGeneration=Document.Generation;prepareRevision=Document.Revision;prepareDeliberate=deliberate;prepareForce=force;return;}
            FinishLayout(deliberate,force);
        }
        private void FinishLayout(bool deliberate,bool force)
        {
            RestoreHandoffNodes();
            Motion.Sample(EditorApplication.timeSinceStartup,Document.Generation);
            Layout.Arrange(Document,settings,force);
            Motion.Commit(Document,EditorApplication.timeSinceStartup,settings.EffectiveGraphMotion,deliberate||!settings.GraphInvestigationLock);
        }
        public void RestoreHandoff(QuantumGraphWindowHandoff value)
        {
            if(value==null||Document?.Root==null||value.Project!=Document.Project)return;
            if(value.Playing&&!EditorApplication.isPlaying)return;
            bool same=value.Project?value.Folder==Document.Root.AssetPath:value.Root!=null&&value.Root==Document.Root.Target;
            if(!same)return;
            handoff=value;handedOff.Clear();Camera.Pan=value.Pan;Camera.Zoom=value.Zoom;cameraMoving=initialFit=false;
            handoffEntries.Clear();
            foreach(var entry in value.Nodes)
                handoffEntries[HandoffKey(entry.Target,entry.Owner,entry.Family,entry.Property,entry.Id,entry.OwnerPath,entry.Kind)]=entry;
            RestoreHandoffNodes();Layout.Arrange(Document,settings);Motion.Direct(Document);Layout.Reindex(Document);Changed();
        }
        private void RestoreHandoffNodes()
        {
            if(handoff==null)return;
            foreach(var node in Document.Nodes.Values.ToArray())
            {
                if(handedOff.Contains(node.Id))continue;
                QuantumGraphNode parent=null;if(node.ParentId!=null)Document.Nodes.TryGetValue(node.ParentId,out parent);var owner=parent?.Target;
                if(node.Kind==QuantumGraphNodeKind.Missing||!handoffEntries.TryGetValue(HandoffKey(node.Target,owner,node.FamilyKey,node.PropertyPath,node.Id,owner==null?parent?.Id:null,node.Kind),out var saved))continue;
                node.Position=saved.Position;node.OverviewOffset=saved.OverviewOffset;node.Size=saved.Size;node.Positioned=true;node.Manual=saved.Manual;node.Pinned=saved.Pinned;node.CustomSize=saved.CustomSize;node.Slot=saved.Slot;node.Expanded=saved.Expanded;
                if(saved.Selected){Selection.Add(node.Id);selectedId=node.Id;restoreSelection=true;}
                handedOff.Add(node.Id);if(node.Expanded&&node.CanExpand&&!node.Loaded)Provider.Expand(node);
            }
        }
        private static (Object,Object,string,string,string,QuantumGraphNodeKind) HandoffKey(Object target,Object owner,string family,string property,string path,string ownerPath,QuantumGraphNodeKind kind)
            =>kind==QuantumGraphNodeKind.Cluster?(null,owner,family,ownerPath,null,kind):kind==QuantumGraphNodeKind.Folder||kind==QuantumGraphNodeKind.Asset?(null,null,null,property,path,kind):(target,null,null,property,null,kind);
        public void Tick(float dt)
        {
            if(Document==null||detached||suspended||resolvedStyle.display==DisplayStyle.None)return;
            double now=EditorApplication.timeSinceStartup;
            ComponentInspector.Tick();
            backdrop.Tick(now,settings.AmbientEnabled);
            foreach(var view in shown.Values)view.TickAppearance(now,settings.EffectiveGraphMotion);
            if(selectionStarted>=0)
            {selectionFeedback=Mathf.Clamp01((float)((now-selectionStarted)/.52));graphArt.MarkDirtyRepaint();if(selectionFeedback>=1){selectionStarted=-1;selectionFeedback=0;}}
            bool draw=needsDraw;needsDraw=false;
            if(!paused&&Provider.Tick())
            {
                // Coalesce progressive discovery into one arrangement at the end of this batch.
                if(!Provider.Busy){CommitLayout(true);UpdateMatches();dirtyState=true;draw=true;if(initialFit)Fit();}
                else {progress.text=Provider.Status;UpdateCompactStatus();}
            }
            if(preparing!=null&&!Provider.Busy)
            {
                if(prepareGeneration!=Document.Generation)preparing=null;
                else if(prepareRevision!=Document.Revision)CommitLayout(prepareDeliberate,prepareForce);
                else
                {
                    var slice=System.Diagnostics.Stopwatch.StartNew();
                    while(prepareIndex<preparing.Length&&slice.Elapsed.TotalMilliseconds<settings.GraphWorkMilliseconds)QuantumGraphNodeSizing.Apply(preparing[prepareIndex++],settings);
                    MaximumPreparationSliceMs=Math.Max(MaximumPreparationSliceMs,slice.Elapsed.TotalMilliseconds);
                    if(prepareIndex==preparing.Length){preparing=null;FinishLayout(prepareDeliberate,prepareForce);draw=true;}
                    else progress.text="Preparing label bounds "+prepareIndex+" / "+preparing.Length+" — graph data is already searchable";
                }
            }
            if(restoreSelection&&Selected!=null){restoreSelection=false;select(Selected);}
            RevealPendingAsset();
            draw|=Motion.Sample(now,Document.Generation);
            draw|=SampleCamera(now);
            draw|=zoomOverview.Sample(now);
            draw|=Previews.Tick(now,Provider);
            if(draw)UpdateViews();
            if(dirtyState&&!Provider.Busy&&EditorApplication.timeSinceStartup-lastSave>2)SaveState();
        }
        public void Fit()
        {
            if(Document==null||!(Viewport.contentRect.width>20)||!(Viewport.contentRect.height>20))return;
            zoomOverview.ResetPacking();
            var destination=new QuantumGraphViewportController();destination.Fit(QuantumGraphLayoutEngine.Bounds(Document),Viewport.contentRect.size);
            FocusCamera(destination.Pan,destination.Zoom,!initialFit);initialFit=false;Changed();
        }
        public void CenterSelected(){if(Selected==null)return;Reveal(Selected);float zoom=Mathf.Max(1,Camera.Zoom);FocusCamera(-Selected.Position*zoom,zoom,true);Changed();}
        private void FocusCamera(Vector2 pan,float zoom,bool animate)
        {
            double now=EditorApplication.timeSinceStartup;SampleCamera(now);cameraFrom=Camera.Pan;zoomFrom=Camera.Zoom;cameraTarget=pan;zoomTarget=zoom;cameraStarted=now;
            cameraMoving=animate&&settings.EffectiveGraphMotion==QuantumGraphMotionMode.Full;
            if(!cameraMoving){Camera.Pan=pan;Camera.Zoom=zoom;}
        }
        private bool SampleCamera(double now)
        {
            if(!cameraMoving)return false;float t=Mathf.Clamp01((float)((now-cameraStarted)/.34));float eased=1-Mathf.Pow(1-t,3);
            Camera.Pan=Vector2.LerpUnclamped(cameraFrom,cameraTarget,eased);Camera.Zoom=Mathf.LerpUnclamped(zoomFrom,zoomTarget,eased);
            if(t>=1)cameraMoving=false;return true;
        }
        public void RevealAsset(string path){pendingAssetPath=path;RevealPendingAsset();}
        private void RevealPendingAsset()
        {
            if(string.IsNullOrEmpty(pendingAssetPath)||Document==null||Provider.Busy)return;
            string id=QuantumGraphIdentity.AssetId(pendingAssetPath);pendingAssetPath=null;
            if(!Document.Nodes.TryGetValue(id,out var node))return;
            Select(node);initialFit=false;CenterSelected();
        }
        public void AutoLayout(bool reset)
        {
            if(Document==null)return;
            if(reset)foreach(var node in Document.Nodes.Values.Where(n=>!n.Pinned)){node.Manual=false;node.OverviewOffset=Vector2.zero;node.Positioned=node.Id==Document.RootId;if(node.Id==Document.RootId)node.Position=Vector2.zero;}
            CommitLayout(true,true);Changed();
        }
        public void SetClusters(bool expanded)
        {
            if(Document==null)return;
            foreach(var cluster in Document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Cluster))cluster.Expanded=expanded;
            CommitLayout(true);Changed();
        }
        public void Toggle(QuantumGraphNode node)
        {
            if(node==null)return;
            if(node.Expanded&&node.Loaded)Provider.Collapse(node);else Provider.Expand(node);
            CommitLayout(true);Changed();
        }
        public void Select(QuantumGraphNode node,bool additive=false)
        {
            ComponentInspector.Reopen();
            if(node==null){if(!additive){Selection.Clear();selectedId=null;}Changed();return;}
            restoreSelection=false;
            if(!additive)Selection.Clear();
            if(additive&&Selection.Contains(node.Id)){Selection.Remove(node.Id);selectedId=Selection.FirstOrDefault();}
            else{Selection.Add(node.Id);selectedId=node.Id;select(node);selectionStarted=settings.EffectiveGraphMotion==QuantumGraphMotionMode.Full?EditorApplication.timeSinceStartup:-1;selectionFeedback=0;}
            var relations=Document.Edges.Values.Where(e=>e.From==node.Id||e.To==node.Id).Select(e=>e.Relation+": "+Document.Nodes[e.From].Label+" → "+Document.Nodes[e.To].Label+" ("+e.Label+")").ToArray();
            relationshipDetail.text=node.Label+(node.Pinned?" · PIN":"")+(node.Kind==QuantumGraphNodeKind.Missing?" · MISSING":QuantumGraphDot.IsDisabled(node)?" · OFF":"")+"\n"+(relations.Length==0?"No discovered relationships for this node":string.Join("\n",relations.Take(3))+(relations.Length>3?"\n+"+(relations.Length-3)+" more — right-click for the full relationship list":""));
            relationshipDetail.tooltip=node.Label+"\n"+string.Join("\n",relations);
            settingsSelection.text=relationshipDetail.text;settingsSelection.tooltip=relationshipDetail.tooltip;
            Changed();
        }
        public void DoubleClick(QuantumGraphNode node)
        {
            if(node==null)return;
            Select(node);
            if(zoomOverview.Blend>.5f&&!QuantumGraphOverview.Summary(node,Document))CenterSelected();
        }
        public QuantumGraphNode Hit(Vector2 local)
        {
            if(Document==null)return null;
            var dot=pointMarks.Where(p=>(p.Point-local).sqrMagnitude<=Mathf.Pow(Mathf.Max(5,p.Radius+2),2)).OrderBy(p=>(p.Point-local).sqrMagnitude).FirstOrDefault();
            if(dot.Node!=null)return dot.Node;
            if(zoomOverview.Blend>.5f)
            {
                var header=shown.Values.Where(v=>screenAnchors.TryGetValue(v.Node.Id,out var anchor)&&new Rect(anchor-v.Node.Size*v.DisplayScale*.5f,v.Node.Size*v.DisplayScale).Contains(local)).Select(v=>v.Node).FirstOrDefault();
                if(header!=null)return header;
                return zoomOverview.Groups.Values.Where(g=>g.Bounds.width>0&&g.Bounds.Contains(local)&&(g.Node.Kind==QuantumGraphNodeKind.Cluster||g.Node.Kind==QuantumGraphNodeKind.Folder)).Select(g=>g.Node).FirstOrDefault();
            }
            var world=Camera.ToWorld(local,Viewport.contentRect.size);
            var hit=(Motion.Active?inView:Layout.Spatial.Query(new Rect(world-Vector2.one*2,Vector2.one*4))).Where(n=>Document.Presented(n)&&new Rect(n.DisplayPosition-n.Size*.5f,n.Size).Contains(world)).OrderBy(n=>(n.DisplayPosition-world).sqrMagnitude).FirstOrDefault();
            return hit??Document.Nodes.Values.Where(n=>n.Kind==QuantumGraphNodeKind.Cluster&&Document.Presented(n)&&n.DrawRadius>0&&(n.DrawPosition-world).sqrMagnitude<=n.DrawRadius*n.DrawRadius).OrderBy(n=>n.DrawRadius).FirstOrDefault();
        }
        public HashSet<QuantumGraphNode> DragSet(QuantumGraphNode node)
        {
            var items=new HashSet<QuantumGraphNode>();
            foreach(var id in Selection.Contains(node.Id)?Selection:new HashSet<string>{node.Id})
            {
                if(!Document.Nodes.TryGetValue(id,out var selected))continue;items.Add(selected);
                if(selected.Kind==QuantumGraphNodeKind.Cluster)
                {
                    foreach(var item in Document.Nodes.Values.Where(n=>n.ClusterId==selected.Id))items.Add(item);
                    foreach(var item in Document.Descendants(selected.Id))items.Add(item);
                }
                else if(selected.Kind==QuantumGraphNodeKind.Folder)
                    foreach(var item in Document.Descendants(selected.Id))items.Add(item);
            }
            return items;
        }
        public void BeginDirect(IEnumerable<QuantumGraphNode> nodes=null)
        {
            SampleCamera(EditorApplication.timeSinceStartup);cameraMoving=false;if(Document==null||nodes==null)return;
            var moving=nodes.ToArray();
            if(zoomOverview.Blend>.001f)foreach(var node in moving)
                if(!QuantumGraphOverview.Summary(node,Document)&&screenAnchors.TryGetValue(node.Id,out var anchor))overviewDragOffsets[node.Id]=(anchor-Camera.ToScreen(node.DrawPosition,Viewport.contentRect.size))/(Camera.Zoom*zoomOverview.Blend);
            Motion.TakeControl(Document,moving,EditorApplication.timeSinceStartup);
        }
        public void MoveDirect(IEnumerable<QuantumGraphNode> nodes,Vector2 screenDelta)
        {
            var delta=screenDelta/Camera.Zoom;
            foreach(var node in nodes)
            {
                node.Position+=delta;node.Pinned=node.Manual=node.Positioned=true;
                if(QuantumGraphOverview.Summary(node,Document))node.OverviewOffset+=delta;
            }
            Moved();
        }
        public void Moved(bool nodes=true){initialFit=false;cameraMoving=false;if(nodes){foreach(var node in Document.Nodes.Values.Where(n=>n.Manual)){node.PresentedPosition=node.Position;node.HasPresentation=true;}Layout.Reindex(Document);}Changed();}
        public void ResizeNode(QuantumGraphNode node,Vector2 requestedSize)
        {
            if(node==null||Document==null||!Document.Nodes.TryGetValue(node.Id,out var current)||current!=node)return;
            node.CustomSize=true;node.Size=QuantumGraphNodeSizing.Fit(node.Label,settings,requestedSize);
            QuantumGraphNodeSizing.Apply(node,settings);
            Layout.Reindex(Document);Changed();
        }
        public void RestoreNodeSize(QuantumGraphNode node)
        {node.CustomSize=false;QuantumGraphNodeSizing.Apply(node,settings);CommitLayout(true);Changed();SaveState();}
        internal void FinishResize(QuantumGraphNode node)
        {
            if(Document==null||!Document.Nodes.TryGetValue(node.Id,out var current)||current!=node)return;
            CommitLayout(true);Changed();SaveState();
        }
        public void Changed(){dirtyState=true;UpdateViews();}
        private void UpdateMatches()
        {
            matches.Clear();if(Document==null)return;
            var terms=(query??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(ComponentDiscoveryService.Normalize).ToArray();
            if(terms.Length==0)return;
            foreach(var node in Document.Nodes.Values)if(terms.All(t=>(node.SearchText??"").Contains(t)))matches.Add(node.Id);
        }
        private void NextMatch()
        {
            if(matches.Count==0)return;var ids=matches.OrderBy(x=>x,StringComparer.Ordinal).ToList();int index=ids.IndexOf(selectedId);
            var node=Document.Nodes[ids[(index+1)%ids.Count]];Reveal(node);Select(node);CenterSelected();
        }
        private void Reveal(QuantumGraphNode node)
        {
            var ancestor=node;
            while(ancestor.ParentId!=null&&Document.Nodes.TryGetValue(ancestor.ParentId,out ancestor))ancestor.Expanded=true;
            if(node.ClusterId!=null&&Document.Nodes.TryGetValue(node.ClusterId,out var cluster))cluster.Expanded=true;
            CommitLayout(true);
        }
        private void Menu(QuantumGraphNode node,ContextualMenuPopulateEvent e)
        {
            e.menu.AppendAction("Inspect",_=>Select(node));
            e.menu.AppendAction("Focus",_=>{Select(node);CenterSelected();});
            foreach(var relation in Document.Edges.Values.Where(link=>link.From==node.Id||link.To==node.Id))
            {var edge=relation;e.menu.AppendAction("Relationships/"+edge.Relation+"/"+edge.Label+" → "+Document.Nodes[edge.From==node.Id?edge.To:edge.From].Label,_=>{var other=Document.Nodes[edge.From==node.Id?edge.To:edge.From];Reveal(other);Select(other);CenterSelected();});}
            if(node.CanExpand)e.menu.AppendAction(node.Expanded?"Collapse":"Expand",_=>Toggle(node));
            e.menu.AppendAction(node.Pinned?"Unpin position":"Pin position",_=>{node.Pinned=!node.Pinned;if(!node.Pinned)node.Manual=false;Changed();});
            e.menu.AppendAction("Enlarge node",_=>{ResizeNode(node,node.Size*1.2f);FinishResize(node);});
            e.menu.AppendAction("Restore node size",_=>RestoreNodeSize(node));
            if(node.Kind==QuantumGraphNodeKind.GameObject||node.Kind==QuantumGraphNodeKind.Folder)
                e.menu.AppendAction("Make graph root",_=>navigate(node),_=>settings.GraphInvestigationLock?DropdownMenuAction.Status.Disabled:DropdownMenuAction.Status.Normal);
            if(node.Kind==QuantumGraphNodeKind.GameObject)
            {
                e.menu.AppendAction("Show child GameObjects",_=>{Provider.RevealHierarchy(node);CommitLayout(true);Changed();});
                if(node.Id==Document.RootId){e.menu.AppendAction("Add component",_=>componentAction(true));e.menu.AppendAction("Remove component",_=>componentAction(false));}
            }
            if(node.Kind==QuantumGraphNodeKind.Cluster||node.Kind==QuantumGraphNodeKind.Folder)return;
            e.menu.AppendAction("Reveal serialized references",_=>{Provider.RevealReferences(node,QuantumGraphOverlays.SerializedReferences);Changed();});
            if(!string.IsNullOrEmpty(node.AssetPath))e.menu.AppendAction("Reveal direct asset dependencies",_=>{Provider.RevealReferences(node,QuantumGraphOverlays.AssetDependencies);Changed();});
            e.menu.AppendAction("Reveal prefab source",_=>{Provider.RevealReferences(node,QuantumGraphOverlays.PrefabSources);Changed();});
            e.menu.AppendAction("Open native Inspector",_=>{var target=Provider.Resolve(node);if(target!=null)EditorUtility.OpenPropertyEditor(target);});
            e.menu.AppendAction("Ping in Unity",_=>{var target=Provider.Resolve(node);if(target!=null)EditorGUIUtility.PingObject(target);});
            e.menu.AppendAction("Bookmark",_=>{var target=Provider.Resolve(node);if(target!=null)bookmark(target);});
            if(node.Target is Component component&&node.Kind==QuantumGraphNodeKind.Component)e.menu.AppendAction("Rename alias / appearance",_=>ComponentAliasEditor.Open(component));
        }
        private void UpdateViews()
        {
            if(Document==null||!(Viewport.contentRect.width>1)||!(Viewport.contentRect.height>1)||!float.IsFinite(Camera.Zoom))return;
            var watch=System.Diagnostics.Stopwatch.StartNew();ViewUpdates++;Previews.BeginVisible();
            zoomOverview.Target(Camera.Zoom,settings.EffectiveGraphMotion,EditorApplication.timeSinceStartup);
            float blend=zoomOverview.Blend;overview=blend>=.5f;
            if(blend<=0){overviewDragOffsets.Clear();zoomOverview.ResetPacking();Viewport.tooltip="";}
            else zoomOverview.Prepare(Document,settings.GraphFontSize,Viewport.contentRect.size,settings.GraphDotSize);
            detail=detail?Camera.Zoom>.9f:Camera.Zoom>1.05f;
            var size=Viewport.contentRect.size;var top=Camera.ToWorld(new Vector2(-120,-120),size);var bottom=Camera.ToWorld(size+Vector2.one*120,size);
            inView=Layout.Spatial.Query(Rect.MinMaxRect(top.x,top.y,bottom.x,bottom.y));var keep=new HashSet<string>();
            if(Motion.Active)foreach(var node in Document.Nodes.Values)if(Document.Presented(node)&&new Rect(top,bottom-top).Overlaps(new Rect(node.DisplayPosition-node.Size*.5f,node.Size)))inView.Add(node);
            if(blend>0)foreach(var group in zoomOverview.Groups.Values)
            {
                inView.Add(group.Node);foreach(var member in group.Members)inView.Add(member);
            }
            if(blend>0)zoomOverview.Arrange(Camera,size,inView,Document.RootId);
            screenAnchors.Clear();var labelRects=new List<Rect>();
            string selectedCluster=Selected?.Kind==QuantumGraphNodeKind.Cluster?Selected.Id:Selected?.ClusterId;
            foreach(var node in inView.OrderBy(n=>n.Id==Document.RootId?0:QuantumGraphOverview.Summary(n,Document)?1:2).ThenBy(n=>n.Id,StringComparer.Ordinal))
            {
                if(!Document.Presented(node))continue;
                bool summary=QuantumGraphOverview.Summary(node,Document);
                bool projectCard=Document.Project&&node.Kind==QuantumGraphNodeKind.Asset&&node.ParentId==Document.RootId&&Document.Root.MemberCount<=24;
                float zoom=QuantumGraphNodeSizing.DisplayScale(summary?Mathf.Max(.6f,Camera.Zoom):Camera.Zoom,settings.GraphFontSize);
                var anchor=Camera.ToScreen(node.DisplayPosition,size);
                if(blend>0&&summary&&zoomOverview.Groups.TryGetValue(node.Id,out var group))
                {
                    if(group.Bounds.width>0)
                    {
                        labelRects.Add(group.Bounds);
                        anchor=Vector2.Lerp(anchor,group.HeaderCenter,blend);
                    }
                    else
                    {
                    var dimensions=zoomOverview.Size(group);var origin=Camera.ToScreen(node.DrawPosition-node.OverviewOffset,size);
                    var candidate=new Vector2(Mathf.Clamp(origin.x,dimensions.x*.5f,Mathf.Max(dimensions.x*.5f,size.x-dimensions.x*.5f)),Mathf.Clamp(origin.y,dimensions.y*.5f,Mathf.Max(dimensions.y*.5f,size.y-dimensions.y*.5f)));int attempt=0;
                    while((labelRects.Any(r=>r.Overlaps(new Rect(candidate-dimensions*.5f,dimensions)))||candidate.x<dimensions.x*.5f||candidate.x>size.x-dimensions.x*.5f||candidate.y<dimensions.y*.5f||candidate.y>size.y-dimensions.y*.5f)&&attempt<64)
                    {attempt++;float angle=attempt*2.399963f;candidate=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Mathf.Sqrt(attempt)*24;}
                    if(attempt>=64)
                    {
                        float best=float.PositiveInfinity;
                        for(float y=dimensions.y*.5f;y<=size.y-dimensions.y*.5f;y+=dimensions.y+4)
                            for(float x=dimensions.x*.5f;x<=size.x-dimensions.x*.5f;x+=dimensions.x+4)
                            {var slot=new Vector2(x,y);if(!labelRects.Any(r=>r.Overlaps(new Rect(slot-dimensions*.5f,dimensions)))&&(slot-origin).sqrMagnitude<best){candidate=slot;best=(slot-origin).sqrMagnitude;}}
                    }
                    var bounds=new Rect(candidate-dimensions*.5f,dimensions);labelRects.Add(bounds);zoomOverview.PlaceFallback(group,bounds,Camera);bounds=group.Bounds;
                    anchor=Vector2.Lerp(anchor,group.HeaderCenter,blend);
                    }
                    zoom=Mathf.Lerp(zoom,QuantumGraphNodeSizing.DisplayScale(.6f,settings.GraphFontSize)*group.Scale,blend);
                }
                else if(blend>0&&!summary)
                {
                    if(overviewDragOffsets.TryGetValue(node.Id,out var offset))anchor+=offset*Camera.Zoom*blend;
                    else if(zoomOverview.Points.TryGetValue(node.Id,out var dot))anchor=Vector2.Lerp(anchor,dot,blend);
                }
                screenAnchors[node.Id]=anchor;
                if(!summary&&blend>=.999f)continue;
                keep.Add(node.Id);
                if(!summary)zoom*=Mathf.Lerp(1,.28f,blend);
                if(node.Id==Document.RootId||detail||projectCard||Selection.Contains(node.Id))Previews.RequestVisible(node);
                if(!shown.TryGetValue(node.Id,out var view))
                {view=pool.Count>0?pool.Pop():new QuantumGraphNodeView(Toggle,Menu,status,this);shown[node.Id]=view;Viewport.Add(view);}
                view.Bind(node,anchor,zoom,settings.GraphFontSize,Selection.Contains(node.Id),matches.Contains(node.Id),!string.IsNullOrEmpty(query),node.Id==selectedCluster);
                view.ConfigureAppearance(settings,QuantumNebulaDrawing.Tint(node,Document,settings),node.Id==Document.RootId,Selection.Contains(node.Id));
                view.SetSemanticZoom(blend,summary,blend>0&&zoomOverview.Groups.TryGetValue(node.Id,out var summaryGroup)?summaryGroup.Members.Count:0);
                if(!settings.GraphShowAuxiliary)view.SetCleanSummary(node.Id==Document.RootId);
                view.SetDetail(detail||projectCard||Selection.Contains(node.Id));
                view.EnableInClassList("graph-compact",zoom<.65f);
            }
            foreach(var key in shown.Keys.Where(k=>!keep.Contains(k)).ToArray()){var view=shown[key];view.RemoveFromHierarchy();pool.Push(view);shown.Remove(key);}
            visibleEdges.Clear();
            var aggregates=new Dictionary<string,QuantumGraphEdge>();
            foreach(var edge in Document.Edges.Values)
            {
                if(!Document.Nodes.TryGetValue(edge.From,out var from)||!Document.Nodes.TryGetValue(edge.To,out var to)||!from.Active||!to.Active)continue;
                if(!from.Positioned&&!from.HasPresentation||!to.Positioned&&!to.HasPresentation)continue;
                var actualFrom=from;var actualTo=to;
                if(!Document.Presented(from))from=Document.Representative(from);
                if(!Document.Presented(to))to=Document.Representative(to);
                if(overview)
                {
                    if(from!=null&&!Selection.Contains(from.Id))from=zoomOverview.Representative(from,Document);
                    if(to!=null&&!Selection.Contains(to.Id))to=zoomOverview.Representative(to,Document);
                }
                if(from==null||to==null||from==to)continue;
                var rendered=edge;
                if(from!=actualFrom||to!=actualTo)
                {
                    string key=from.Id+"|"+to.Id+"|"+edge.Relation;
                    if(aggregates.TryGetValue(key,out var aggregate)){aggregate.Count++;continue;}
                    rendered=new QuantumGraphEdge{From=from.Id,To=to.Id,Relation=edge.Relation,Label=edge.Label,Overlay=edge.Overlay,Color=edge.Color};aggregates.Add(key,rendered);
                }
                var a=screenAnchors.TryGetValue(from.Id,out var fromPoint)?fromPoint:Camera.ToScreen(from.DisplayPosition,size);var b=screenAnchors.TryGetValue(to.Id,out var toPoint)?toPoint:Camera.ToScreen(to.DisplayPosition,size);
                if(Mathf.Max(a.x,b.x)>=0&&Mathf.Min(a.x,b.x)<=size.x&&Mathf.Max(a.y,b.y)>=0&&Mathf.Min(a.y,b.y)<=size.y)visibleEdges.Add(rendered);
            }
            progress.text=(string.IsNullOrEmpty(query)?"":matches.Count+" matches · ")+Provider.Status+(Provider.PendingChanges>0?" · "+Provider.PendingChanges+" pending changes":"")+(Layout.UnresolvedOverlaps>0?" · "+Layout.UnresolvedOverlaps+" overlaps retained (locked/pinned)":"");
            if(Layout.SpacingCheckLimited)progress.text+=" · spacing check limit reached";
            progress.tooltip=progress.text+"\n"+Provider.ChangeSummary+"\n"+string.Join("\n",aggregates.Values.Select(e=>e.Relation+": "+e.Count+" connections · "+Document.Nodes[e.From].Label+" → "+Document.Nodes[e.To].Label));graphArt.MarkDirtyRepaint();
            UpdateCompactStatus();
            int badgeIndex=0;
            foreach(var edge in visibleEdges.Where(e=>settings.GraphShowAuxiliary&&e.Count>1))
            {
                if(badgeIndex==edgeBadges.Count){var badge=new Label{pickingMode=PickingMode.Ignore};badge.AddToClassList("graph-edge-count");edgeBadges.Add(badge);Viewport.Add(badge);}
                var label=edgeBadges[badgeIndex++];label.style.display=DisplayStyle.Flex;
                var a=screenAnchors.TryGetValue(edge.From,out var first)?first:Camera.ToScreen(Document.Nodes[edge.From].DisplayPosition,size);
                var b=screenAnchors.TryGetValue(edge.To,out var second)?second:Camera.ToScreen(Document.Nodes[edge.To].DisplayPosition,size);
                var point=Vector2.Lerp(a,b,.55f);
                bool fits=false;
                var normal=new Vector2(-(b-a).y,(b-a).x).normalized;
                for(int attempt=0;attempt<12;attempt++)
                {
                    var candidate=point+normal*((attempt%2==0?1:-1)*(attempt/2)*18);
                    var rect=new Rect(candidate,new Vector2(112,18));
                    if(candidate.x>=0&&candidate.y>=0&&rect.xMax<size.x&&rect.yMax<size.y&&!shown.Values.Any(v=>screenAnchors.TryGetValue(v.Node.Id,out var anchor)&&new Rect(anchor-v.Node.Size*v.DisplayScale*.5f,v.Node.Size*v.DisplayScale).Overlaps(rect)))
                    {point=candidate;fits=true;break;}
                }
                label.style.display=fits?DisplayStyle.Flex:DisplayStyle.None;label.style.left=point.x;label.style.top=point.y;
                label.text="×"+edge.Count+" "+edge.Relation;label.tooltip=edge.Count+" actual "+edge.Relation+" edges; expand/reveal to inspect exact endpoints.";
            }
            for(int i=badgeIndex;i<edgeBadges.Count;i++)edgeBadges[i].style.display=DisplayStyle.None;
            pointMarks.Clear();
            foreach(var node in inView)
            {
                if(blend<=0||QuantumGraphOverview.Summary(node,Document)||!Document.Presented(node))continue;
                Vector2 point=screenAnchors.TryGetValue(node.Id,out var dotPoint)?dotPoint:Camera.ToScreen(node.DrawPosition,size);
                if(point.x<0||point.y<0||point.x>size.x||point.y>size.y)continue;
                var tint=QuantumNebulaDrawing.Tint(node,Document,settings);bool disabled=QuantumGraphDot.IsDisabled(node);tint.a*=node.Opacity*blend*(disabled?.65f:1);
                if(!string.IsNullOrEmpty(query)&&!matches.Contains(node.Id)&&!Selection.Contains(node.Id))tint.a*=.4f;
                pointMarks.Add(new QuantumGraphDot{Node=node,Point=point,Color=tint,Radius=(zoomOverview.Radii.TryGetValue(node.Id,out var radius)?radius:2.5f)*settings.GraphDotSize,Selected=Selection.Contains(node.Id),Matched=matches.Contains(node.Id),Disabled=disabled});
            }
            UpdateComponentInspector();
            QuantumInspectorTypography.ApplyButtons(Viewport,settings.ButtonFontSize);
            MaximumViewMilliseconds=Math.Max(MaximumViewMilliseconds,watch.Elapsed.TotalMilliseconds);
        }
        internal bool IsInspectorTarget(IEventHandler target)
        {
            var element=target as VisualElement;
            return element!=null&&(element==ComponentInspector||ComponentInspector.Contains(element));
        }
        private void UpdateComponentInspector()
        {
            var node=Selected;
            if(suspended||detached||node==null||!Document.Presented(node)){ComponentInspector.Hide();return;}
            var anchor=screenAnchors.TryGetValue(node.Id,out var point)?point:Camera.ToScreen(node.DisplayPosition,Viewport.contentRect.size);
            var halfSize=shown.TryGetValue(node.Id,out var visual)?node.Size*visual.DisplayScale*.5f:Vector2.one*6;
            ComponentInspector.Present(node,anchor,halfSize,Viewport.contentRect.size);
        }
        private void Draw(MeshGenerationContext context)
        {
            if(Document==null||!(Viewport.contentRect.width>1)||!(Viewport.contentRect.height>1)||!float.IsFinite(Camera.Zoom))return;
            var size=Viewport.contentRect.size;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            UnityEngine.Profiling.Profiler.BeginSample("Quantum Inspector.Graph Art");
            var p=context.painter2D;
            DrawMetrics.Initialization=Math.Max(DrawMetrics.Initialization,watch.Elapsed.TotalMilliseconds);
            if(settings.GraphPalette==QuantumGraphPalette.CosmicNebula&&Document.Root!=null&&shown.TryGetValue(Document.RootId,out var ownerView)&&screenAnchors.TryGetValue(Document.RootId,out var ownerPoint))
                QuantumNebulaDrawing.Owner(p,ownerPoint,Document.Root.Size*ownerView.DisplayScale,settings.Nebula.Rendering,Selection.Contains(Document.RootId),settings.GraphQuality!=QuantumGraphQuality.Low&&visibleEdges.Count<300);
            zoomOverview.Draw(p,Document,Camera,size,settings);
            QuantumGraphEdgeRenderer.Draw(context,Document,Camera,size,settings,visibleEdges,screenAnchors,Selection,selectionFeedback,DrawMetrics,1-zoomOverview.Blend,zoomOverview.Blend<=0);
            long pointsStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            QuantumGraphDot.Draw(context,pointMarks);
            ComponentInspector.DrawLink(p);
            DrawMetrics.Points=Math.Max(DrawMetrics.Points,QuantumGraphDrawMetrics.Milliseconds(pointsStarted));
            UnityEngine.Profiling.Profiler.EndSample();MaximumDrawMilliseconds=Math.Max(MaximumDrawMilliseconds,watch.Elapsed.TotalMilliseconds);
        }
        public void SaveState()
        {
            if(Document==null)return;
            QuantumGraphStatePersistence.instance.Store(new QuantumSavedGraph{Key=Document.Key,Selected=selectedId,Search=query,Pan=Camera.Pan,Zoom=Camera.Zoom,HasViewport=!initialFit,
                Nodes=Document.Nodes.Values.Select(n=>new QuantumSavedNode{Id=n.Id,Position=n.Position,OverviewOffset=n.OverviewOffset,Slot=n.Slot,Size=n.Size,CustomSize=n.CustomSize,SizeVersion=1,Manual=n.Manual,Pinned=n.Pinned,Expanded=n.Expanded,Positioned=n.Positioned}).ToList()});
            dirtyState=false;lastSave=EditorApplication.timeSinceStartup;
        }
        public void Dispose(){ComponentInspector.Dispose();CancelInteraction?.Invoke();SaveState();CancelInteraction=null;preparing=null;Motion.Reset();Previews.Clear();Provider.Dispose();backdrop.Dispose();detached=true;}
    }

    internal sealed class QuantumGraphManipulator:PointerManipulator
    {
        private readonly QuantumGraphView view;
        private int pointer=-1;
        private Vector2 start,last;
        private bool moved,additive;
        private QuantumGraphNode pressed;
        private HashSet<QuantumGraphNode> moving;
        private Dictionary<QuantumGraphNode,(Vector2 position,Vector2 overviewOffset,bool pinned,bool manual)> originals;
        private Vector2 originalPan;
        private double previousClickTime;
        private string previousClick;
        public QuantumGraphManipulator(QuantumGraphView view){this.view=view;}
        protected override void RegisterCallbacksOnTarget()
        {
            view.CancelInteraction+=Cancel;
            target.RegisterCallback<PointerDownEvent>(Down);target.RegisterCallback<PointerMoveEvent>(Move);target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCaptureOutEvent>(Lost);target.RegisterCallback<WheelEvent>(Wheel);
        }
        protected override void UnregisterCallbacksFromTarget()
        {
            view.CancelInteraction-=Cancel;
            target.UnregisterCallback<PointerDownEvent>(Down);target.UnregisterCallback<PointerMoveEvent>(Move);target.UnregisterCallback<PointerUpEvent>(Up);
            target.UnregisterCallback<PointerCaptureOutEvent>(Lost);target.UnregisterCallback<WheelEvent>(Wheel);
        }
        private void Down(PointerDownEvent e)
        {
            if(view.IsInspectorTarget(e.target)||pointer>=0||(e.button!=0&&e.button!=2))return;
            var element=e.target as VisualElement;if(element is Button||element?.GetFirstAncestorOfType<Button>()!=null)return;
            pointer=e.pointerId;start=last=e.position;moved=false;additive=e.shiftKey||e.ctrlKey||e.commandKey;
            var nodeView=element as QuantumGraphNodeView??element?.GetFirstAncestorOfType<QuantumGraphNodeView>();
            pressed=e.button==2||e.altKey?null:nodeView?.Node??view.Hit(target.WorldToLocal(e.position));
            moving=pressed!=null?view.DragSet(pressed):null;
            originalPan=view.Camera.Pan;originals=moving?.ToDictionary(n=>n,n=>(n.Position,n.OverviewOffset,n.Pinned,n.Manual));
            target.CapturePointer(pointer);target.Focus();e.StopPropagation();
        }
        private void Move(PointerMoveEvent e)
        {
            if(e.pointerId!=pointer)return;
            if(e.pressedButtons==0){Release();return;}
            if(!moved&&Vector2.Distance(start,e.position)>3){moved=true;if(pressed!=null&&!view.Selection.Contains(pressed.Id))view.Select(pressed,additive);view.BeginDirect(moving);}
            if(moved)
            {
                Vector2 delta=(Vector2)e.position-last;
                if(pressed==null)view.Camera.Pan+=delta;
                else view.MoveDirect(moving,delta);
                if(pressed==null)view.Moved(false);
            }
            last=e.position;e.StopPropagation();
        }
        private void Up(PointerUpEvent e)
        {
            if(e.pointerId!=pointer)return;
            if(!moved)
            {
                view.Select(pressed,additive);
                double now=EditorApplication.timeSinceStartup;
                if(pressed!=null&&pressed.Id==previousClick&&now-previousClickTime<.35)view.DoubleClick(pressed);
                previousClick=pressed?.Id;previousClickTime=now;
            }
            Release();e.StopPropagation();
        }
        private void Lost(PointerCaptureOutEvent e){if(e.pointerId==pointer)Release();}
        private void Cancel()
        {
            if(pointer<0)return;
            view.Camera.Pan=originalPan;
            if(originals!=null)foreach(var pair in originals){pair.Key.Position=pair.Value.position;pair.Key.OverviewOffset=pair.Value.overviewOffset;pair.Key.Pinned=pair.Value.pinned;pair.Key.Manual=pair.Value.manual;pair.Key.PresentedPosition=pair.Value.position;}
            view.Moved();Release();
        }
        private void Release(){int id=pointer;pointer=-1;if(id>=0&&target.HasPointerCapture(id))target.ReleasePointer(id);if(moved)view.SaveState();pressed=null;moving=null;originals=null;}
        private void Wheel(WheelEvent e){if(view.IsInspectorTarget(e.target))return;view.Camera.ZoomAt(target.WorldToLocal(e.mousePosition),target.contentRect.size,e.delta.y);view.Moved(false);e.StopPropagation();}
    }
}
