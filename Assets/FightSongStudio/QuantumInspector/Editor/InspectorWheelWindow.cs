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
    public sealed class InspectorWheelWindow:EditorWindow
    {
        public static string StylePath=>AssetDatabase.GUIDToAssetPath("802c743417758e14caea778717cb3bf3");
        [SerializeField] private InspectorWheelSettings settings;
        [SerializeField] private GameObject inspected;
        [SerializeField] private Component selectedComponent;
        [SerializeField] private bool pinned;
        [SerializeField] private bool graphMode,entireProjectGraph;
        [SerializeField] private bool wheelInformationVisible;
        private Button wheelInformationButton,bookmarksButton;
        private QuantumGraphView quantumGraph;
        [SerializeField] private QuantumGraphWindowHandoff graphHandoff;
        private WheelScope graphDetailsScope;
        private Button wheelModeButton,graphModeButton,historyBack,historyForward;
        public QuantumGraphView GraphWorkspace=>quantumGraph;
        public bool GraphMode=>graphMode;
        private bool projectGraphDirty;
        private bool windowVisible=true;
        private bool graphPreferencesLoaded;
        [SerializeField] private string query="";
        [SerializeField] private InspectorNavigationTab activeTab;
        [SerializeField] private WheelNavigationHistory hierarchyNavigation=new WheelNavigationHistory();
        [SerializeField] private WheelNavigationHistory projectNavigation=new WheelNavigationHistory();
        private readonly InspectorWheelController controller=new InspectorWheelController();
        private readonly ReferenceGraphService graph=new ReferenceGraphService();
        private readonly IOrbitalDataProvider[] providers={new ComponentOrbitalProvider(),new HierarchyOrbitalProvider(),new ProjectOrbitalProvider(),new ParameterOrbitalProvider()};
        private List<OrbitalEntry> all=new List<OrbitalEntry>(),filtered=new List<OrbitalEntry>();
        private InspectorWheelView wheel;
        private ResearchLayoutManager layouts;
        private ResearchSplitView researchSplit;
        private Label status,breadcrumb;
        private VisualElement compactNavigation;
        private VisualElement wheelToolbar,navigationBar,navigationTabs,modeTabs;
        private VisualElement[] navigationControls,modeControls;
        private Button compactHierarchy,compactProject;
        private Button projectTab,hierarchyTab,parentButton;
        private Toggle selectionLock;
        private bool dirty,loadingBookmark,suppressSelection;
        private bool followingProjectItem;
        private string pendingProjectItem,lastProjectItem;
        private Object projectItemSelectionSnapshot;
        private Object synchronizedSelection;
        private Object observedSelection;
        private int settingsRevision;
        private double lastTick,lastRefresh,lastRuntime;
        private WheelNavigationHistory History=>activeTab==InspectorNavigationTab.Project?projectNavigation:hierarchyNavigation;
        public WheelScope CurrentScope=>History.Current;
        public InspectorNavigationTab ActiveTab=>activeTab;
        public GameObject InspectedObject=>inspected;
        public Component SelectedComponent=>selectedComponent;
        public InspectorWheelController Controller=>controller;
        public InspectorWheelView Wheel=>wheel;
        public int FilteredCount=>filtered.Count;
        public bool Pinned {get=>pinned;set{pinned=value;selectionLock?.SetValueWithoutNotify(value);}}
        public IReadOnlyList<OrbitalEntry> Entries=>filtered;

        [MenuItem("Tools/Quantum Inspector/Open",false,0)]
        public static InspectorWheelWindow Open()
        {
            var window=Resources.FindObjectsOfTypeAll<InspectorWheelWindow>().FirstOrDefault();
            if(window==null)return QuantumInspectorWorkspace.OpenDefault();
            window.Show();window.Focus();return window;
        }
        [MenuItem("Tools/Quantum Inspector/Settings",false,20)]
        private static void OpenSettingsFromMenu()=>Open().OpenSettings();
        private void EnsureNavigation()
        {
            if(hierarchyNavigation.Current==null)
            {
                var initial=inspected!=null?inspected:!pinned?Selection.activeGameObject:null;
                hierarchyNavigation.Visit(new WheelScope{Kind=initial!=null?WheelScopeKind.Components:WheelScopeKind.SceneRoots,Object=initial,Component=selectedComponent,Query=query});
            }
            if(projectNavigation.Current==null)projectNavigation.Visit(new WheelScope{Kind=WheelScopeKind.ProjectFolder});
        }
        private void OnEnable()
        {
            titleContent=new GUIContent("Quantum Inspector");
            EnsureNavigation();
            Selection.selectionChanged+=SelectionChanged;EditorApplication.update+=UpdateWheel;
            EditorApplication.projectWindowItemOnGUI+=ObserveProjectItem;
            EditorApplication.hierarchyChanged+=Invalidate;EditorApplication.projectChanged+=ProjectChanged;
            EditorApplication.playModeStateChanged+=PlayModeChanged;Undo.undoRedoPerformed+=Invalidate;
            ComponentAliasDatabase.Changed+=Invalidate;ObjectChangeEvents.changesPublished+=ObjectChanged;
            lastTick=EditorApplication.timeSinceStartup;
        }
        private void OnDisable()
        {
            if(graphMode)graphHandoff=QuantumGraphWindowHandoff.Capture(quantumGraph);
            CaptureLocation();
            Selection.selectionChanged-=SelectionChanged;EditorApplication.update-=UpdateWheel;
            EditorApplication.projectWindowItemOnGUI-=ObserveProjectItem;
            EditorApplication.update-=FlushProjectItem;pendingProjectItem=null;
            EditorApplication.hierarchyChanged-=Invalidate;EditorApplication.projectChanged-=ProjectChanged;
            EditorApplication.playModeStateChanged-=PlayModeChanged;Undo.undoRedoPerformed-=Invalidate;
            ComponentAliasDatabase.Changed-=Invalidate;ObjectChangeEvents.changesPublished-=ObjectChanged;
            graph.CancelScan();quantumGraph?.Dispose();quantumGraph=null;layouts?.Dispose();wheel?.Dispose();wheel=null;
        }
        private void OnBecameVisible(){windowVisible=true;quantumGraph?.SuspendPresentation(!graphMode);}
        private void OnBecameInvisible(){windowVisible=false;quantumGraph?.SuspendPresentation(true);}
        private void PlayModeChanged(PlayModeStateChange state){quantumGraph?.SuspendPresentation(true);graph.Invalidate();Invalidate();quantumGraph?.SuspendPresentation(!windowVisible||!graphMode);}
        private void ObjectChanged(ref ObjectChangeEventStream stream)=>Invalidate();
        private void Invalidate()=>dirty=true;
        private void ProjectChanged(){foreach(var provider in providers)provider.Invalidate();projectGraphDirty=true;Invalidate();}
        public void CreateGUI()
        {
            if(graphMode&&quantumGraph?.Document!=null)graphHandoff=QuantumGraphWindowHandoff.Capture(quantumGraph);
            EnsureNavigation();quantumGraph?.Dispose();layouts?.Dispose();wheel?.Dispose();rootVisualElement.Clear();
            if(settings==null)settings=InspectorWheelSettings.LoadOrCreate();
            if(!graphPreferencesLoaded&&EditorUtility.IsPersistent(settings)){QuantumGraphPreferences.Load(settings);graphPreferencesLoaded=true;}
            var style=AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);if(style!=null&&!rootVisualElement.styleSheets.Contains(style))rootVisualElement.styleSheets.Add(style);
            rootVisualElement.AddToClassList("quantum-root");
            var header=new VisualElement();header.AddToClassList("quantum-header");rootVisualElement.Add(header);
            var brand=new VisualElement();brand.AddToClassList("quantum-brand");header.Add(brand);
            var eyebrow=new Label("FIGHT SONG STUDIO  /  RESEARCH SYSTEMS");eyebrow.AddToClassList("quantum-eyebrow");brand.Add(eyebrow);
            var title=new Label("QUANTUM INSPECTOR");title.AddToClassList("quantum-title");brand.Add(title);
            selectionLock=new Toggle("Lock selection"){value=pinned,name="selectionLock",tooltip="Keep the current view when selecting folders, assets or objects in Unity. Unlock to follow the current selection."};
            selectionLock.RegisterValueChangedCallback(e=>{pinned=e.newValue;if(!pinned)FollowLatestSelection();});header.Add(selectionLock);selectionLock.AddToClassList("graph-header-auxiliary");
            var layoutMenu=new ToolbarMenu{text="Layouts"};
            foreach(ResearchLayouts layout in Enum.GetValues(typeof(ResearchLayouts)))
            {
                if(layout==ResearchLayouts.None)continue;
                var captured=layout;
                layoutMenu.menu.AppendAction(layout==ResearchLayouts.Graph?"Relationship preview":layout.ToString(),_=>{ChangeSettings(()=>settings.Layouts^=captured);if(!graphMode&&(settings.Layouts&captured)!=0)SetWheelInformationVisible(true);},
                    _=>!graphMode&&(captured==ResearchLayouts.Details||captured==ResearchLayouts.History)?DropdownMenuAction.Status.Hidden:
                        (settings.Layouts&captured)!=0?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);
            }
            layoutMenu.menu.AppendSeparator();
            layoutMenu.menu.AppendAction("Wheel only",_=>{if(!graphMode)SetWheelInformationVisible(false);else ChangeSettings(()=>settings.Layouts=ResearchLayouts.None);});
            header.Add(layoutMenu);layoutMenu.AddToClassList("graph-header-auxiliary");
            var researchMenu=new ToolbarMenu{text="Wheel links"};
            researchMenu.menu.AppendAction("Show overlay",_=>ChangeSettings(()=>settings.ShowRelationshipOverlay=!settings.ShowRelationshipOverlay),
                _=>settings.ShowRelationshipOverlay?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);
            researchMenu.menu.AppendAction("Search referenced names",_=>ChangeSettings(()=>settings.SearchReferences=!settings.SearchReferences),
                _=>settings.SearchReferences?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);
            foreach(RelationshipKinds kind in new[]{RelationshipKinds.Hierarchy,RelationshipKinds.Serialized,RelationshipKinds.ScriptableObject,RelationshipKinds.Dependency,RelationshipKinds.Event,RelationshipKinds.Instrumented})
            {var captured=kind;researchMenu.menu.AppendAction(kind.ToString(),_=>ChangeSettings(()=>settings.Relationships^=captured),_=>(settings.Relationships&captured)!=0?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);}
            header.Add(researchMenu);researchMenu.AddToClassList("graph-header-auxiliary");
            wheelInformationButton=new Button(()=>SetWheelInformationVisible(!wheelInformationVisible)){name="wheelInformationToggle"};header.Add(wheelInformationButton);
            bookmarksButton=new Button(ShowBookmarks){name="bookmarksButton",text="Bookmarks",tooltip="Load a bookmarked object or component into the wheel; Back returns to your previous inspection."};header.Add(bookmarksButton);
            var settingsButton=new Button(()=>{if(graphMode)quantumGraph.ToggleSettings();else OpenSettings();}){text="Settings",tooltip="Graph settings, auxiliary UI and factory defaults."};settingsButton.AddToClassList("graph-header-auxiliary");header.Add(settingsButton);
            var refreshButton=new Button(()=>{foreach(var provider in providers)provider.Invalidate();graph.Invalidate();RefreshData();}){text="Refresh",tooltip="Rebuild component and reference caches"};refreshButton.AddToClassList("graph-header-auxiliary");header.Add(refreshButton);
            compactNavigation=new VisualElement{name="graphMainControls"};compactNavigation.AddToClassList("graph-header-compact");header.Add(compactNavigation);
            compactHierarchy=new Button(()=>SwitchTab(InspectorNavigationTab.Hierarchy)){text="Hierarchy"};compactProject=new Button(()=>SwitchTab(InspectorNavigationTab.Project)){text="Project"};
            compactNavigation.Add(compactHierarchy);compactNavigation.Add(compactProject);compactNavigation.Add(new Button(()=>SetGraphMode(false)){text="Wheel"});

            var navigation=new VisualElement();navigationBar=navigation;navigation.AddToClassList("quantum-navigation");rootVisualElement.Add(navigation);
            var tabs=new VisualElement();navigationTabs=tabs;tabs.AddToClassList("quantum-tabs");navigation.Add(tabs);
            projectTab=new Button(()=>SwitchTab(InspectorNavigationTab.Project)){text="Project",name="projectTab"};
            hierarchyTab=new Button(()=>SwitchTab(InspectorNavigationTab.Hierarchy)){text="Hierarchy",name="hierarchyTab"};
            projectTab.AddToClassList("navigation-tab");hierarchyTab.AddToClassList("navigation-tab");tabs.Add(projectTab);tabs.Add(hierarchyTab);
            parentButton=new Button(GoToParent){text="↑ Parent",name="parentScope"};tabs.Add(parentButton);
            tabs.Add(new Button(()=>{if(activeTab==InspectorNavigationTab.Project)OpenProjectFolder("Assets");else ShowSceneRoots();}){text="⌂ Roots",tooltip="Assets folder or loaded scene roots"});
            var unityProject=new Button(()=>EditorApplication.ExecuteMenuItem("Window/General/Project")){text="Unity Project"};unityProject.AddToClassList("wheel-native-shortcut");tabs.Add(unityProject);
            var unityHierarchy=new Button(()=>EditorApplication.ExecuteMenuItem("Window/General/Hierarchy")){text="Unity Hierarchy"};unityHierarchy.AddToClassList("wheel-native-shortcut");tabs.Add(unityHierarchy);
            var unityInspector=new Button(()=>OpenNativeInspector(graphMode&&quantumGraph?.Selected!=null?quantumGraph.Provider.Resolve(quantumGraph.Selected):CurrentScope.Asset!=null?CurrentScope.Asset:selectedComponent!=null?(Object)selectedComponent:inspected)){text="Unity Inspector"};unityInspector.AddToClassList("wheel-native-shortcut");tabs.Add(unityInspector);
            var modes=new VisualElement();modeTabs=modes;modes.AddToClassList("quantum-tabs");navigation.Add(modes);
            wheelModeButton=new Button(()=>SetGraphMode(false)){text="Orbital Wheel",name="wheelMode"};
            graphModeButton=new Button(()=>SetGraphMode(true)){text="Clustered Graph",name="graphMode"};
            modes.Add(wheelModeButton);modes.Add(graphModeButton);
            historyBack=new Button(()=>MoveHistory(-1)){text="← Back"};historyForward=new Button(()=>MoveHistory(1)){text="Forward →"};modes.Add(historyBack);modes.Add(historyForward);
            navigationControls=tabs.Children().ToArray();modeControls=modes.Children().ToArray();
            breadcrumb=new Label();breadcrumb.AddToClassList("quantum-breadcrumb");navigation.Add(breadcrumb);
            wheelToolbar=new VisualElement{name="wheelHeaderControls"};wheelToolbar.AddToClassList("wheel-header-controls");header.Add(wheelToolbar);
            var wheelTools=new ToolbarMenu{text="Tools",name="wheelTools",tooltip="Selection lock, layouts, settings and Unity windows"};wheelTools.AddToClassList("wheel-tools");wheelToolbar.Add(wheelTools);
            wheelTools.menu.AppendAction("Lock selection",_=>{Pinned=!Pinned;if(!Pinned)SelectionChanged();},_=>Pinned?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);
            wheelTools.menu.AppendAction("Settings",_=>OpenSettings());
            wheelTools.menu.AppendAction("Refresh",_=>{foreach(var provider in providers)provider.Invalidate();graph.Invalidate();RefreshData();});
            wheelTools.menu.AppendSeparator();
            wheelTools.menu.AppendAction("Unity windows/Project",_=>EditorApplication.ExecuteMenuItem("Window/General/Project"));
            wheelTools.menu.AppendAction("Unity windows/Hierarchy",_=>EditorApplication.ExecuteMenuItem("Window/General/Hierarchy"));
            wheelTools.menu.AppendAction("Unity windows/Inspector",_=>OpenNativeInspector(CurrentScope.Asset!=null?CurrentScope.Asset:selectedComponent!=null?(Object)selectedComponent:inspected));
            foreach(ResearchLayouts layout in Enum.GetValues(typeof(ResearchLayouts)))
            {
                if(layout==ResearchLayouts.None||layout==ResearchLayouts.Details||layout==ResearchLayouts.History)continue;
                var captured=layout;
                wheelTools.menu.AppendAction("Additional panels/"+(layout==ResearchLayouts.Graph?"Relationship preview":layout.ToString()),_=>{ChangeSettings(()=>settings.Layouts^=captured);if((settings.Layouts&captured)!=0)SetWheelInformationVisible(true);},_=> (settings.Layouts&captured)!=0?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);
            }
            wheelTools.menu.AppendAction("Wheel links/Show overlay",_=>ChangeSettings(()=>settings.ShowRelationshipOverlay=!settings.ShowRelationshipOverlay),_=>settings.ShowRelationshipOverlay?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);
            wheelTools.menu.AppendAction("Wheel links/Search referenced names",_=>ChangeSettings(()=>settings.SearchReferences=!settings.SearchReferences),_=>settings.SearchReferences?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);
            foreach(RelationshipKinds kind in new[]{RelationshipKinds.Hierarchy,RelationshipKinds.Serialized,RelationshipKinds.ScriptableObject,RelationshipKinds.Dependency,RelationshipKinds.Event,RelationshipKinds.Instrumented})
            {var captured=kind;wheelTools.menu.AppendAction("Wheel links/"+kind,_=>ChangeSettings(()=>settings.Relationships^=captured),_=>(settings.Relationships&captured)!=0?DropdownMenuAction.Status.Checked:DropdownMenuAction.Status.Normal);}
            header.RegisterCallback<GeometryChangedEvent>(e=>UpdateWheelHeader(e.newRect.width));
            var body=new ResearchSplitView(settings.PreferredResearchPanelWidth,width=>ChangeSettings(()=>settings.ResearchPanelWidth=width));researchSplit=body;rootVisualElement.Add(body);
            wheel=new InspectorWheelView(controller,settings,NodeClicked,NodeMenu,SetStatus,MoveHistory,SetQuery,ToggleChildren,BeginAddComponent,BeginRemoveComponent,GoToParent,()=>{if(CurrentScope.Object!=null)EditorGUIUtility.PingObject(CurrentScope.Object);});
            var workspace=new VisualElement();workspace.AddToClassList("quantum-workspace");body.Add(workspace);workspace.Add(wheel);
            quantumGraph=new QuantumGraphView(settings,SelectGraphNode,NavigateGraphNode,Bookmark,
                value=>{entireProjectGraph=value;graphDetailsScope=null;Present();},
                add=>{if(inspected==null)return;Inspect(inspected);graphDetailsScope=null;if(add)BeginAddComponent();else BeginRemoveComponent();},
                ConfigureGraph,SetStatus);workspace.Add(quantumGraph);
            quantumGraph.RestoreDefaultsRequested=RestoreDefaultSettings;quantumGraph.OpenPresetRequested=OpenSettings;compactNavigation.Add(quantumGraph.CompactControls);
            quantumGraph.BindSelectionLock(()=>Pinned,value=>{Pinned=value;if(!value)FollowLatestSelection();});
            quantumGraph.SearchChanged=value=>{if(inspected!=null){SyncSelection(inspected);QuantumNativeInspectorBridge.Search(inspected,value);}};
            layouts=new ResearchLayoutManager(NavigateFromDetails,Bookmark,StartIncoming,LoadBookmark,SelectHistory);body.Add(layouts.Root);layouts.Configure(settings);
            layouts.AddComponent=AddComponent;layouts.ConfirmRemoval=ConfirmRemoval;layouts.CancelAction=CancelComponentAction;
            researchSplit.Configure(settings);
            var footer=new VisualElement();footer.AddToClassList("quantum-footer");rootVisualElement.Add(footer);
            status=new Label();status.AddToClassList("quantum-status");footer.Add(status);
            var hint=new Label("WHEEL: SCROLL TO ROTATE  ·  GRAPH: DRAG TO PAN / MOVE · SCROLL TO ZOOM · SHIFT-CLICK TO GROUP · F TO FIT");hint.AddToClassList("quantum-hint");footer.Add(hint);
            var credit=new Label("Developed by Gary Lougheed & Astra"){name="developerCredit"};
            credit.AddToClassList("quantum-credit");rootVisualElement.Add(credit);
            QuantumInspectorWindowResize.Install(this);
            settingsRevision=settings.Revision;ApplyScope();if(graphMode)quantumGraph.RestoreHandoff(graphHandoff);
        }
        private void ChangeSettings(Action change)
        {
            Undo.RecordObject(settings,"Configure Quantum Inspector");change();settings.Revision++;EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);ApplySettings();
        }
        private void ApplySettings()
        {
            settingsRevision=settings.Revision;wheel.Configure(settings);quantumGraph.Configure(settings);layouts.Configure(settings);researchSplit.Configure(settings);
            ConfigureChrome();
            CurrentScope.Rotation=controller.Position;CurrentScope.Capacity=controller.SlotCount;
            graph.InvalidateCachedData();RefreshData(false);
        }
        private void ConfigureGraph(Action change)
        {
            var overlays=settings.GraphOverlays;float nodeFont=settings.GraphFontSize,buttonFont=settings.ButtonFontSize,wheelScale=settings.OrbitalFontScale;
            Undo.RecordObject(settings,"Configure graph");change();
            if(nodeFont!=settings.GraphFontSize||buttonFont!=settings.ButtonFontSize||wheelScale!=settings.OrbitalFontScale)
            {settings.Revision++;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);ApplySettings();return;}
            if(overlays!=settings.GraphOverlays){settings.Revision++;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);}
            QuantumGraphPreferences.Save(settings);quantumGraph.Configure(settings);ConfigureChrome();SetStatus("");
        }
        private void ConfigureChrome()
        {
            bool clean=graphMode&&!settings.GraphShowAuxiliary;
            rootVisualElement.EnableInClassList("graph-clean",clean);
            rootVisualElement.EnableInClassList("wheel-mode",!graphMode);
            if(wheelToolbar!=null)
            {
                if(!graphMode&&projectTab.parent!=wheelToolbar)
                {
                    int index=0;
                    foreach(var control in navigationControls.Concat(modeControls))wheelToolbar.Insert(index++,control);
                    wheelToolbar.Insert(index++,wheelInformationButton);wheelToolbar.Insert(index,bookmarksButton);
                }
                else if(graphMode&&projectTab.parent!=navigationTabs)
                {
                    foreach(var control in navigationControls)navigationTabs.Add(control);
                    foreach(var control in modeControls)modeTabs.Add(control);
                    wheelToolbar.parent.Add(bookmarksButton);
                }
                UpdateWheelHeader(rootVisualElement.contentRect.width);
            }
            var visibleLayouts=graphMode?settings.Layouts:settings.Layouts|ResearchLayouts.Details|ResearchLayouts.History;
            layouts?.Configure(settings,visibleLayouts);
            researchSplit?.Configure(settings,graphMode?clean:!wheelInformationVisible,visibleLayouts);
            if(wheelInformationButton!=null)
            {
                wheelInformationButton.EnableInClassList("quantum-hidden",graphMode);
                wheelInformationButton.text=wheelInformationVisible?"Hide Info":"Show Info";
                wheelInformationButton.tooltip="Show or hide Component details and Bookmarks & history alongside the Orbital Wheel.";
            }
            compactHierarchy?.EnableInClassList("active-tab",activeTab==InspectorNavigationTab.Hierarchy);
            compactProject?.EnableInClassList("active-tab",activeTab==InspectorNavigationTab.Project);
            quantumGraph?.RefreshSelectionLock();
            QuantumInspectorTypography.ApplyButtons(rootVisualElement,settings.ButtonFontSize);
        }
        private void UpdateWheelHeader(float width)
        {
            bool compact=!graphMode&&width<1100;
            rootVisualElement.EnableInClassList("wheel-compact",compact);
            rootVisualElement.EnableInClassList("wheel-narrow",!graphMode&&width<700);
            if(wheelModeButton==null)return;
            wheelModeButton.text=compact?"Wheel":"Orbital Wheel";wheelModeButton.tooltip="Orbital Wheel";
            graphModeButton.text=compact?"Graph":"Clustered Graph";graphModeButton.tooltip="Clustered Graph";
            historyBack.text=compact?"←":"← Back";historyForward.text=compact?"→":"Forward →";
            historyBack.tooltip="Back to the previous inspection";historyForward.tooltip="Forward to the next inspection";
        }
        public void SetWheelInformationVisible(bool visible)
        {
            wheelInformationVisible=visible;
            if(!visible&&!graphMode)layouts?.Actions.Reset();
            Present();
        }
        public void RestoreDefaultSettings()
        {
            Undo.RecordObject(settings,"Restore Quantum Inspector defaults");settings.RestoreFactoryDefaults();QuantumGraphPreferences.RestoreDefaults(settings);
            Pinned=false;
            EditorUtility.SetDirty(settings);if(EditorUtility.IsPersistent(settings))AssetDatabase.SaveAssetIfDirty(settings);
            ApplySettings();observedSelection=Selection.activeObject;
        }
        public void OpenSettings()
        {
            synchronizedSelection=settings;Selection.activeObject=settings;EditorGUIUtility.PingObject(settings);
            EditorUtility.OpenPropertyEditor(settings);
        }
        private void SelectionChanged()
        {
            lastProjectItem=null;
            observedSelection=Selection.activeObject;
            if(synchronizedSelection!=null&&Selection.activeObject==synchronizedSelection){synchronizedSelection=null;return;}
            synchronizedSelection=null;
            if(pinned||loadingBookmark||suppressSelection||wheel==null)return;
            var selection=Selection.activeObject;
            if(graphMode&&settings.GraphInvestigationLock&&!EditorUtility.IsPersistent(selection)&&!(selection is GameObject))return;
            if(selection is Component selectedPart){FocusComponent(selectedPart);return;}
            if(selection!=null&&EditorUtility.IsPersistent(selection))
            {
                string path=AssetDatabase.GetAssetPath(selection);
                if(string.IsNullOrEmpty(path))return;
                FollowProjectItem(path,selection);
                return;
            }
            if(Selection.activeGameObject!=null)
            {
                // Inspector edits and focusing the native Inspector must not pull a focused component back out.
                if(!graphMode&&CurrentScope.IsComponentFocus&&CurrentScope.Object==Selection.activeGameObject)return;
                Inspect(Selection.activeGameObject);
                if(graphMode&&quantumGraph.Document?.Root!=null)quantumGraph.Select(quantumGraph.Document.Root);
            }
            else if(Selection.activeObject==null&&activeTab==InspectorNavigationTab.Hierarchy)ShowSceneRoots();
        }
        // Some Editor integrations defer selectionChanged while the Editor is unfocused.
        // Compare object identity only; never rebuild an unchanged context each tick.
        internal void ObserveSelection()
        {if(observedSelection!=Selection.activeObject)SelectionChanged();}
        private void ObserveProjectItem(string guid,Rect selectionRect)
            =>HandleProjectItemEvent(guid,selectionRect,Event.current);
        internal void HandleProjectItemEvent(string guid,Rect selectionRect,Event e)
        {
            if(e==null||e.rawType!=EventType.MouseDown||e.button!=0||!selectionRect.Contains(e.mousePosition))return;
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(string.IsNullOrEmpty(path))return;
            pendingProjectItem=path;
            EditorApplication.update-=FlushProjectItem;
            EditorApplication.update+=FlushProjectItem;
        }
        internal void FlushProjectItem()
        {
            EditorApplication.update-=FlushProjectItem;
            string path=pendingProjectItem;pendingProjectItem=null;
            if(this==null||wheel==null||string.IsNullOrEmpty(path))return;
            lastProjectItem=path;projectItemSelectionSnapshot=Selection.activeObject;
            observedSelection=Selection.activeObject;
            if(!pinned&&!loadingBookmark&&!suppressSelection)FollowProjectItem(path,Selection.activeObject);
        }
        private void FollowLatestSelection()
        {
            if(!string.IsNullOrEmpty(lastProjectItem)&&projectItemSelectionSnapshot==Selection.activeObject)
                FollowProjectItem(lastProjectItem,Selection.activeObject);
            else SelectionChanged();
        }
        private void FollowProjectItem(string path,Object selection)
        {
            var asset=selection!=null&&AssetDatabase.GetAssetPath(selection)==path?selection:AssetDatabase.LoadMainAssetAtPath(path);
            if(asset==null)return;
            followingProjectItem=true;
            try
            {
                entireProjectGraph=false;
                if(AssetDatabase.IsValidFolder(path))OpenProjectFolder(path);
                else OpenProjectAsset(asset,path);
                if(graphMode)quantumGraph.RevealAsset(path);
            }
            finally{followingProjectItem=false;}
        }
        private void CaptureLocation()
        {
            if(CurrentScope==null||wheel==null)return;
            CurrentScope.Query=query;CurrentScope.Rotation=controller.Position;CurrentScope.Capacity=controller.SlotCount;
            if(CurrentScope.Kind==WheelScopeKind.Components)CurrentScope.Component=selectedComponent;
        }
        private void ApplyScope()
        {
            if(wheel==null)return;
            var scope=CurrentScope;
            if(!scope.Available){History.Visit(Fallback(scope),true);scope=CurrentScope;}
            layouts.Actions.Reset();graphDetailsScope=null;
            inspected=scope.Kind==WheelScopeKind.ProjectFolder?null:scope.Object;
            selectedComponent=scope.Kind==WheelScopeKind.Components||scope.IsComponentFocus?scope.Component:null;
            query=scope.Query??"";wheel.Search.SetValueWithoutNotify(query);
            double rotation=scope.Rotation*controller.SlotCount/Mathf.Clamp(scope.Capacity,5,8);
            graph.Invalidate();controller.Reset(0);RefreshData();controller.RestorePosition(rotation);
            scope.Capacity=controller.SlotCount;scope.Rotation=rotation;Present();
            if(graphMode&&scope.IsComponentFocus&&scope.Component!=null)
            {var node=quantumGraph.Document.Nodes.Values.FirstOrDefault(n=>n.Kind==QuantumGraphNodeKind.Component&&n.Target==scope.Component);if(node!=null){quantumGraph.Select(node);quantumGraph.Provider.Expand(node);quantumGraph.CenterSelected();}}
        }
        private void PushScope(WheelScope next,bool addHistory=true)
        {
            CaptureLocation();next.Capacity=controller.SlotCount;History.Visit(next,!addHistory);ApplyScope();
        }
        public void SwitchTab(InspectorNavigationTab tab)
        {
            EnsureNavigation();if(activeTab==tab)return;
            CaptureLocation();activeTab=tab;ApplyScope();
        }
        public void Inspect(GameObject target,bool addHistory=true)
        {
            SwitchTab(InspectorNavigationTab.Hierarchy);
            if(target==null){PushScope(new WheelScope{Kind=WheelScopeKind.SceneRoots},addHistory);return;}
            if(CurrentScope.Kind==WheelScopeKind.Components&&CurrentScope.Object==target){if(all.Count==0)RefreshData();return;}
            PushScope(new WheelScope{Kind=WheelScopeKind.Components,Object=target},addHistory);
        }
        public void ToggleChildren()
        {
            if(activeTab!=InspectorNavigationTab.Hierarchy||inspected==null||CurrentScope.IsComponentFocus)return;
            if(CurrentScope.Kind!=WheelScopeKind.Children&&inspected.transform.childCount==0)return;
            PushScope(new WheelScope{Kind=CurrentScope.Kind==WheelScopeKind.Children?WheelScopeKind.Components:WheelScopeKind.Children,Object=inspected});
        }
        public void ShowSceneRoots(){SwitchTab(InspectorNavigationTab.Hierarchy);PushScope(new WheelScope{Kind=WheelScopeKind.SceneRoots});}
        public void GoToParent()
        {
            var scope=CurrentScope;
            if(scope.IsComponentFocus)
            {
                string parent=SerializedParameterService.ParentPath(scope.PropertyPath);
                if(scope.Kind==WheelScopeKind.ComponentFocus)PushScope(new WheelScope{Kind=WheelScopeKind.Components,Object=scope.Object,Component=scope.Component});
                else PushScope(new WheelScope{Kind=string.IsNullOrEmpty(parent)?WheelScopeKind.ComponentFocus:WheelScopeKind.Property,Object=scope.Object,Component=scope.Component,PropertyPath=parent});
                return;
            }
            if(scope.Kind==WheelScopeKind.ProjectFolder)
            {
                if(scope.FolderPath!="Assets")OpenProjectFolder(System.IO.Path.GetDirectoryName(scope.FolderPath)?.Replace('\\','/'));
            }
            else if(scope.Object!=null&&scope.Object.transform.parent!=null)PushScope(new WheelScope{Kind=WheelScopeKind.Children,Object=scope.Object.transform.parent.gameObject});
            else ShowSceneRoots();
        }
        public void OpenProjectFolder(string path)
        {
            if(string.IsNullOrEmpty(path)||!AssetDatabase.IsValidFolder(path))return;
            SwitchTab(InspectorNavigationTab.Project);
            PushScope(new WheelScope{Kind=WheelScopeKind.ProjectFolder,Folder=path,FolderGuid=AssetDatabase.AssetPathToGUID(path)});
        }
        private void OpenProjectAsset(Object asset,string path)
        {
            SwitchTab(InspectorNavigationTab.Project);
            string folder=System.IO.Path.GetDirectoryName(path)?.Replace('\\','/');
            PushScope(new WheelScope{Kind=WheelScopeKind.ProjectFolder,Folder=folder,FolderGuid=AssetDatabase.AssetPathToGUID(folder),Asset=asset});
            controller.Reveal(filtered.FindIndex(e=>e.AssetPath==path));CaptureLocation();Present();
        }
        public void Navigate(Object destination)
        {
            if(destination==null)return;
            var gameObject=destination as GameObject??(destination as Component)?.gameObject;
            if(destination is Component focused){FocusComponent(focused);return;}
            if(gameObject!=null)
            {
                SwitchTab(InspectorNavigationTab.Hierarchy);
                PushScope(new WheelScope{Kind=WheelScopeKind.Components,Object=gameObject,Component=destination as Component});
                controller.Reveal(filtered.FindIndex(e=>e.Component==selectedComponent));CaptureLocation();Present();
                SyncSelection(gameObject);EditorGUIUtility.PingObject(gameObject);
            }
            else
            {
                string path=AssetDatabase.GetAssetPath(destination);
                if(string.IsNullOrEmpty(path)){OpenNativeInspector(destination);return;}
                if(AssetDatabase.IsValidFolder(path)){OpenProjectFolder(path);return;}
                OpenProjectAsset(destination,path);SyncSelection(destination);
            }
        }
        private void SyncSelection(Object target)
        {
            if(pinned||target==null)return;
            synchronizedSelection=target;suppressSelection=true;try{Selection.activeObject=target;}finally{suppressSelection=false;}
            if(target is Component component)QuantumNativeInspectorBridge.Reveal(component);
        }
        private void OpenNativeInspector(Object target)
        {if(target!=null){synchronizedSelection=target;suppressSelection=true;try{Selection.activeObject=target;EditorGUIUtility.PingObject(target);}finally{suppressSelection=false;}}EditorApplication.ExecuteMenuItem("Window/General/Inspector");if(target is Component component)QuantumNativeInspectorBridge.Reveal(component);}
        public void MoveHistory(int direction)
        {
            CaptureLocation();if(!History.Move(direction))return;ApplyScope();
            SyncSelection(activeTab==InspectorNavigationTab.Project?CurrentScope.Asset:inspected);
        }
        private void SelectHistory(int index){CaptureLocation();if(History.Select(index)){ApplyScope();SyncSelection(activeTab==InspectorNavigationTab.Project?CurrentScope.Asset:inspected);}}
        public void SetQuery(string value)
        {
            query=value??"";CurrentScope.Query=query;wheel?.Search.SetValueWithoutNotify(query);
            filtered=ComponentDiscoveryService.Filter(all,query);controller.Reset(filtered.Count);Present();
            if(CurrentScope.Kind==WheelScopeKind.Components&&inspected!=null)
            {SyncSelection(inspected);QuantumNativeInspectorBridge.Search(inspected,query,filtered.Select(entry=>entry.Component));}
        }
        private void RefreshData(bool refreshGraph=true)
        {
            if(wheel==null||settings==null)return;
            dirty=false;lastRefresh=EditorApplication.timeSinceStartup;
            if(!CurrentScope.Available)
            {if(graphMode&&settings.GraphInvestigationLock){quantumGraph?.Refresh(projectGraphDirty);projectGraphDirty=false;return;}History.Visit(Fallback(CurrentScope),true);ApplyScope();return;}
            if(CurrentScope.IsComponentFocus)
            {
                string path=CurrentScope.PropertyPath;
                while(!SerializedParameterService.Exists(CurrentScope.Component,path))path=SerializedParameterService.ParentPath(path);
                if(path!=CurrentScope.PropertyPath)
                {PushScope(new WheelScope{Kind=string.IsNullOrEmpty(path)?WheelScopeKind.ComponentFocus:WheelScopeKind.Property,Object=inspected,Component=selectedComponent,PropertyPath=path},false);return;}
                if(!SerializedParameterService.Exists(CurrentScope.Component,CurrentScope.SelectedProperty))CurrentScope.SelectedProperty="";
            }
            if(CurrentScope.Kind==WheelScopeKind.ProjectFolder&&CurrentScope.Asset!=null&&
                System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(CurrentScope.Asset))?.Replace('\\','/')!=CurrentScope.FolderPath)CurrentScope.Asset=null;
            graph.PropertyLimit=settings.MaxPropertiesPerObject;
            var discovered=providers.First(p=>p.Supports(CurrentScope.Kind)).Discover(CurrentScope,graph,settings);
            if(layouts.Actions.Mode!=ComponentActionMode.Browse&&!all.Select(e=>e.Key).SequenceEqual(discovered.Select(e=>e.Key)))layouts.Actions.Revision++;
            all=discovered;
            filtered=ComponentDiscoveryService.Filter(all,query);
            if(controller.Count!=filtered.Count)controller.SetCount(filtered.Count);
            if(selectedComponent==null||selectedComponent.gameObject!=inspected)selectedComponent=null;
            CurrentScope.Component=selectedComponent;
            if(refreshGraph)quantumGraph?.Refresh(projectGraphDirty);projectGraphDirty=false;Present();
        }
        private void Present()
        {
            if(wheel==null)return;
            ConfigureChrome();
            var links=settings.ShowRelationshipOverlay&&CurrentScope.Kind==WheelScopeKind.Components?graph.ForObject(inspected):new List<ResearchRelationship>();
            wheel.Present(CurrentScope,filtered,all.Count,links);
            wheel.SetRemovalMode(layouts.Actions.Mode==ComponentActionMode.Remove);
            wheel.Back.SetEnabled(History.CanMove(-1));wheel.Forward.SetEnabled(History.CanMove(1));
            projectTab.EnableInClassList("active-tab",activeTab==InspectorNavigationTab.Project);hierarchyTab.EnableInClassList("active-tab",activeTab==InspectorNavigationTab.Hierarchy);
            parentButton.SetEnabled(CurrentScope.Kind==WheelScopeKind.ProjectFolder?CurrentScope.FolderPath!="Assets":CurrentScope.Kind!=WheelScopeKind.SceneRoots);
            breadcrumb.text=CurrentScope.Breadcrumb;breadcrumb.tooltip=breadcrumb.text;
            wheel.EnableInClassList("quantum-hidden",graphMode);quantumGraph.EnableInClassList("quantum-hidden",!graphMode);
            wheelModeButton.EnableInClassList("active-tab",!graphMode);graphModeButton.EnableInClassList("active-tab",graphMode);
            historyBack.SetEnabled(History.CanMove(-1));historyForward.SetEnabled(History.CanMove(1));
            if(graphMode)quantumGraph.Bind(CurrentScope,entireProjectGraph,settings);
            if(graphDetailsScope!=null&&quantumGraph.Selected==null)graphDetailsScope=null;
            layouts.Present(graphMode&&graphDetailsScope!=null?graphDetailsScope:CurrentScope,graph,History);SetStatus("");
        }
        public void SetGraphMode(bool value)
        {
            graphMode=value;quantumGraph?.SuspendPresentation(!value||!windowVisible);graphDetailsScope=null;layouts.Actions.Reset();Present();
            if(value&&quantumGraph.Selected!=null)SelectGraphNode(quantumGraph.Selected);
        }
        private void SelectGraphNode(QuantumGraphNode node)
        {
            if(node==null)return;
            var target=quantumGraph.Provider.Resolve(node);
            if(target!=null&&!followingProjectItem){synchronizedSelection=target;Selection.activeObject=target;}
            if(target is Component selectedNativeComponent)QuantumNativeInspectorBridge.Reveal(selectedNativeComponent);
            if(layouts.Actions.Mode==ComponentActionMode.Remove&&node.Kind==QuantumGraphNodeKind.Component&&target is Component removable)
            {
                if(removable.gameObject!=inspected){SetStatus("Select a component attached to the graph's root GameObject for removal.");return;}
                layouts.Actions.PendingRemoval=removable;layouts.Actions.Revision++;graphDetailsScope=null;Present();return;
            }
            layouts.Actions.Reset();
            if(node.Kind==QuantumGraphNodeKind.Parameter)
                graphDetailsScope=new WheelScope{Kind=WheelScopeKind.ComponentFocus,Component=target as Component,Object=(target as Component)?.gameObject,Asset=target is Component?null:target,SelectedProperty=node.PropertyPath};
            else if(target is Component component)
                graphDetailsScope=new WheelScope{Kind=WheelScopeKind.ComponentFocus,Component=component,Object=component.gameObject};
            else if(node.Kind==QuantumGraphNodeKind.Cluster||node.Kind==QuantumGraphNodeKind.World)
                graphDetailsScope=new WheelScope{Kind=activeTab==InspectorNavigationTab.Project?WheelScopeKind.ProjectFolder:WheelScopeKind.Components,Object=inspected,Folder=CurrentScope.FolderPath};
            else if(node.Kind==QuantumGraphNodeKind.Asset&&activeTab==InspectorNavigationTab.Project)
                graphDetailsScope=new WheelScope{Kind=WheelScopeKind.ProjectFolder,Folder=CurrentScope.FolderPath,Asset=target};
            else if(target is GameObject go)
                graphDetailsScope=new WheelScope{Kind=WheelScopeKind.Components,Object=go};
            else graphDetailsScope=new WheelScope{Kind=WheelScopeKind.ProjectFolder,Folder=CurrentScope.FolderPath,Asset=target};
            if(node.Kind==QuantumGraphNodeKind.Parameter)ShowDetails();
            Present();SetStatus(node.Label+" · "+node.Subtitle+" · "+(node.AssetPath??node.PropertyPath??""));
        }
        private void NavigateGraphNode(QuantumGraphNode node)
        {
            graphDetailsScope=null;
            if(node.Kind==QuantumGraphNodeKind.Folder){entireProjectGraph=false;OpenProjectFolder(node.AssetPath);}
            else {var target=quantumGraph.Provider.Resolve(node);if(target!=null)Navigate(target);}
        }
        private void NavigateFromDetails(Object target)
        {
            if(graphMode&&settings.GraphInvestigationLock&&target!=null)
            {
                var node=quantumGraph.Provider.Investigate(target,quantumGraph.Selected??quantumGraph.Document.Root);
                quantumGraph.Layout.Arrange(quantumGraph.Document,settings);quantumGraph.Select(node);quantumGraph.CenterSelected();return;
            }
            Navigate(target);
        }
        private void NodeClicked(ComponentNodeView node)
        {
            if(node.Entry==null)return;
            node.Motion.Click(settings,activeTab==InspectorNavigationTab.Project?settings.ProjectSelectionIntensity:1);
            var entry=node.Entry;Vector2 origin=node.Center;
            bool focus=entry.Kind==OrbitalEntryKind.Component&&entry.Component!=null&&layouts.Actions.Mode!=ComponentActionMode.Remove;
            ActivateEntry(entry);
            if(focus)wheel.AnimateFocus(entry,origin);
        }
        public void ActivateEntry(OrbitalEntry entry)
        {
            if(entry==null)return;
            if(entry.Kind==OrbitalEntryKind.Folder){OpenProjectFolder(entry.AssetPath);return;}
            if(entry.Kind==OrbitalEntryKind.GameObject){Navigate(entry.Target);return;}
            if(entry.Kind==OrbitalEntryKind.Asset)
            {
                CurrentScope.Asset=AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);Present();SyncSelection(CurrentScope.Asset);
                SetStatus(string.Join(" · ",AssetInformation.Describe(CurrentScope.Asset).Take(3)));return;
            }
            if(entry.Kind==OrbitalEntryKind.Parameter)
            {
                if(entry.CanExpand)PushScope(new WheelScope{Kind=WheelScopeKind.Property,Object=inspected,Component=selectedComponent,PropertyPath=entry.PropertyPath});
                else {CurrentScope.SelectedProperty=entry.PropertyPath;ShowDetails();Present();}
                if(entry.CanExpand)ShowDetails();
                return;
            }
            if(entry.Component==null){SetStatus("Missing script slot. Repair the reference in Unity's Inspector.");return;}
            if(layouts.Actions.Mode==ComponentActionMode.Remove)
            {selectedComponent=entry.Component;CurrentScope.Component=selectedComponent;layouts.Actions.PendingRemoval=entry.Component;layouts.Actions.Message="";layouts.Actions.Revision++;Present();return;}
            FocusComponent(entry.Component);
        }
        private static WheelScope Fallback(WheelScope scope)=>new WheelScope{Kind=scope.Object!=null?WheelScopeKind.Components:WheelScopeKind.SceneRoots,Object=scope.Object};
        public void FocusComponent(Component component)
        {
            if(component==null)return;
            QuantumNativeInspectorBridge.Reveal(component);
            SwitchTab(InspectorNavigationTab.Hierarchy);
            if(CurrentScope.Kind==WheelScopeKind.ComponentFocus&&CurrentScope.Component==component)return;
            double rotation=controller.Position,velocity=controller.Velocity;
            PushScope(new WheelScope{Kind=WheelScopeKind.ComponentFocus,Object=component.gameObject,Component=component,Rotation=rotation});
            controller.ContinueMomentum(settings.ReducedMotion?0:velocity);
            SyncSelection(graphMode?(Object)component:component.gameObject);
        }
        private void ShowDetails()
        {
            if(!graphMode){SetWheelInformationVisible(true);return;}
            if((settings.Layouts&ResearchLayouts.Details)==0)ChangeSettings(()=>settings.Layouts|=ResearchLayouts.Details);
        }
        public void BeginAddComponent()
        {
            if(CurrentScope.Kind!=WheelScopeKind.Components||inspected==null)return;
            ShowDetails();layouts.Actions.Reset();layouts.Actions.Mode=ComponentActionMode.Add;Present();
        }
        public void BeginRemoveComponent()
        {
            if(CurrentScope.Kind!=WheelScopeKind.Components||inspected==null)return;
            ShowDetails();layouts.Actions.Reset();layouts.Actions.Mode=ComponentActionMode.Remove;Present();
        }
        public void CancelComponentAction(){layouts.Actions.Reset();Present();}
        public void AddComponent(Type type)
        {
            if(layouts.Actions.Mode!=ComponentActionMode.Add||CurrentScope.Kind!=WheelScopeKind.Components)return;
            var added=ComponentCatalogService.Add(inspected,type,out string failure);
            layouts.Actions.LastAdded=added;layouts.Actions.Message=failure??"";layouts.Actions.Revision++;
            graph.InvalidateCachedData();RefreshData();
            if(added!=null){SetQuery("");controller.Reveal(filtered.FindIndex(e=>e.Component==added));wheel.AnimateArrival();SetStatus(type.Name+" added · Focus new component is available in the catalog.");}
        }
        public void ConfirmRemoval()
        {
            if(layouts.Actions.Mode!=ComponentActionMode.Remove)return;
            var component=layouts.Actions.PendingRemoval;
            bool removed=ComponentCatalogService.Remove(component,out string failure);
            layouts.Actions.Message=failure??"Component removed. Undo restores it.";layouts.Actions.Revision++;
            if(removed){layouts.Actions.PendingRemoval=null;if(selectedComponent==null)CurrentScope.Component=null;}
            graph.InvalidateCachedData();RefreshData();if(removed)wheel.AnimateArrival();
        }
        private void NodeMenu(ComponentNodeView node,ContextualMenuPopulateEvent e)
        {
            var entry=node.Entry;if(entry==null)return;
            if(entry.Kind==OrbitalEntryKind.Parameter)
            {
                e.menu.AppendAction(entry.CanExpand?"Explore child parameters":"Edit serialized parameter",_=>ActivateEntry(entry));
                e.menu.AppendAction("Copy property path",_=>EditorGUIUtility.systemCopyBuffer=entry.PropertyPath);
                e.menu.AppendAction("Open native Inspector",_=>EditorUtility.OpenPropertyEditor(entry.Target));
                return;
            }
            if(entry.Kind!=OrbitalEntryKind.Component)
            {
                e.menu.AppendAction(entry.Kind==OrbitalEntryKind.Asset?"Select asset":"Explore",_=>ActivateEntry(entry));
                Object target=entry.Target!=null?entry.Target:AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);
                if(target!=null)
                {
                    e.menu.AppendAction("Locate in Unity Inspector",_=>OpenNativeInspector(target));
                    e.menu.AppendAction("Ping in Unity",_=>EditorGUIUtility.PingObject(target));
                    e.menu.AppendAction("Bookmark",_=>Bookmark(target));
                    if(entry.Kind==OrbitalEntryKind.Asset&&target is GameObject prefab)e.menu.AppendAction("Inspect prefab components",_=>Navigate(prefab));
                    if(entry.Kind==OrbitalEntryKind.Asset&&target is SceneAsset)
                        e.menu.AppendAction("Open scene",_=>
                        {
                            if(!InspectorSceneNavigation.TryOpen(AssetDatabase.GetAssetPath(target),out string failure))
                                SetStatus(failure??"Scene change cancelled.");
                        },_=>EditorApplication.isPlayingOrWillChangePlaymode?DropdownMenuAction.Status.Disabled:DropdownMenuAction.Status.Normal);
                    else if(entry.Kind==OrbitalEntryKind.Asset)e.menu.AppendAction("Open asset in Unity",_=>AssetDatabase.OpenAsset(target));
                }
                return;
            }
            var component=entry.Component;if(component==null)return;
            e.menu.AppendAction("Rename alias / appearance",_=>ComponentAliasEditor.Open(component));
            e.menu.AppendAction("Inspect component",_=>{FocusComponent(component);ShowDetails();});
            e.menu.AppendAction("Locate in Unity Inspector",_=>OpenNativeInspector(component));
            e.menu.AppendAction("Reveal serialized references",_=>{FocusComponent(component);ShowDetails();ChangeSettings(()=>settings.Layouts|=ResearchLayouts.References);});
            e.menu.AppendAction("Trace incoming and outgoing",_=>{FocusComponent(component);ShowDetails();ChangeSettings(()=>settings.Layouts|=ResearchLayouts.References|ResearchLayouts.Graph);StartIncoming();});
            e.menu.AppendAction("Bookmark component",_=>Bookmark(component));
            e.menu.AppendAction("Copy component metadata",_=>EditorGUIUtility.systemCopyBuffer=ComponentAliasDatabase.DisplayName(component)+"\n"+component.GetType().FullName+"\n"+component.gameObject.name+"\n"+component.gameObject.scene.path+"\n"+GlobalObjectId.GetGlobalObjectIdSlow(component));
        }
        private void ShowBookmarks()
        {
            var menu=new GenericMenu();
            Object target=activeTab==InspectorNavigationTab.Project?CurrentScope.Asset:inspected;
            if(target!=null)menu.AddItem(new GUIContent("Bookmark current object or asset"),false,()=>Bookmark(target));
            if(selectedComponent!=null)menu.AddItem(new GUIContent("Bookmark selected component"),false,()=>Bookmark(selectedComponent));
            menu.AddItem(new GUIContent("Show bookmarks and history panel"),false,()=>{if(!graphMode)SetWheelInformationVisible(true);else ChangeSettings(()=>settings.Layouts|=ResearchLayouts.History);});
            menu.AddSeparator("");
            var saved=InspectorWheelSession.instance.Bookmarks;
            if(saved.Count==0)menu.AddDisabledItem(new GUIContent("No bookmarks yet"));
            for(int i=0;i<saved.Count;i++)
            {
                var bookmark=saved[i];
                menu.AddItem(new GUIContent((i+1)+" · "+InspectorWheelSession.Label(bookmark).Replace('/','∕')),false,()=>LoadBookmark(bookmark));
            }
            menu.ShowAsContext();
        }
        public void LoadBookmark(InspectorWheelSession.Bookmark bookmark)
        {
            if(bookmark==null)return;
            try
            {
                loadingBookmark=true;
                var destination=InspectorWheelSession.Load(bookmark.Id,out var failure);
                if(destination==null){SetStatus(failure);return;}
                Navigate(destination);SetStatus("Bookmark loaded · use ← to return within this tab.");
            }
            catch(Exception ex){SetStatus("Could not load bookmark: "+ex.Message);}
            finally{loadingBookmark=false;}
        }
        private void Bookmark(Object target)
        {
            bool saved=InspectorWheelSession.instance.Add(target);layouts.History();
            SetStatus(saved?"Bookmark saved.":"Save the object's scene first. Transient Play Mode objects cannot be persisted as bookmarks.");
        }
        private void StartIncoming()
        {
            if(CurrentScope.Kind!=WheelScopeKind.Components&&!CurrentScope.IsComponentFocus){SetStatus("Select a GameObject component to scan incoming scene references.");return;}
            graph.StartIncoming(selectedComponent!=null?(Object)selectedComponent:inspected);layouts.Relationships();SetStatus(graph.ScanStatus);
        }
        private void SetStatus(string message)
        {
            if(status==null)return;
            string text=string.IsNullOrEmpty(message)&&graphMode?quantumGraph.Document?.Root?.Label+"  /  "+(settings.GraphInvestigationLock?"INVESTIGATION LOCK":"GRAPH EXPLORATION")+"  /  Right-click nodes for actions":string.IsNullOrEmpty(message)?CurrentScope.Title+"  /  "+filtered.Count+" matches  /  "+(pinned?"PINNED":activeTab==InspectorNavigationTab.Project?"PROJECT ASSETS":"SELECTION SYNC"):message;
            status.text=text.Replace('\r',' ').Replace('\n',' ');status.tooltip=text;
        }
        private void UpdateWheel()
        {
            if(wheel==null||settings==null||EditorApplication.isCompiling||!windowVisible)return;
            double now=EditorApplication.timeSinceStartup;
            if(now-lastTick<1.0/Mathf.Max(15,settings.FramesPerSecond))return;
            float dt=(float)Math.Min(.1,now-lastTick);lastTick=now;
            ObserveSelection();
            if(settingsRevision!=settings.Revision)ApplySettings();
            if(!CurrentScope.Available&&!(graphMode&&settings.GraphInvestigationLock))RefreshData();
            if(dirty&&!graph.Scanning&&now-lastRefresh>.35){graph.InvalidateCachedData();RefreshData();}
            if(graph.Tick(settings.IncomingScanBatch)){layouts.Relationships();SetStatus(graph.ScanStatus);}
            if(graphMode)quantumGraph.Tick(dt);else wheel.Tick(dt);
            if(now-lastRuntime>.5)
            {
                QuantumInspectorTypography.ApplyButtons(rootVisualElement,settings.ButtonFontSize);
                layouts.Runtime();
                if(CurrentScope.Kind==WheelScopeKind.Components&&EditorApplication.isPlaying&&InspectorWheelExtensions.RuntimeProviders.Count>0)
                {wheel.SetRelationships(graph.ForObject(inspected));layouts.Relationships();}
                lastRuntime=now;
            }
        }
    }
}
