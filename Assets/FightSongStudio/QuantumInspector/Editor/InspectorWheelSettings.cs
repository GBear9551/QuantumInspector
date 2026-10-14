using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace FightSong.InspectorTool
{
    [Flags]
    public enum ResearchLayouts { None = 0, Overview = 1, Details = 2, [InspectorName("Relationship preview")] Graph = 4, References = 8, Runtime = 16, History = 32 }
    [Flags]
    public enum RelationshipKinds { None = 0, Hierarchy = 1, Serialized = 2, Dependency = 4, Event = 8, Instrumented = 16, ScriptableObject = 32, All = 63 }
    public enum WheelNodeSize { Compact, Standard, Large }
    public enum QuantumGraphPalette { Editor, Dark, Light, CosmicNebula }
    public enum QuantumGraphQuality { Low, Balanced, Cinematic }

    [CreateAssetMenu(menuName = "Quantum Inspector/Settings", fileName = "QuantumInspectorWheelSettings")]
    public sealed class InspectorWheelSettings : ScriptableObject
    {
        public static string CleanDefaultsPath => AssetDatabase.GUIDToAssetPath("13d446c97f7601345a49d9a27399adde");
        public static string DefaultPath
        {
            get
            {
                var path = AssetDatabase.GUIDToAssetPath("3435383ee11b2a54cbd69ee5ade6b7f8");
                // Git packages are immutable; keep editable data in the consuming project.
                return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal)
                    ? path : "Assets/QuantumInspector/Editor/QuantumInspectorWheelSettings.asset";
            }
        }
        [Header("Font sizes")]
        [InspectorName("Orbital wheel font scale"),Tooltip("Multiplier for the wheel preset: 1 = 100%, 1.5 = 150%, 2 = 200%, 3 = 300%. Titles, details, icons and nodes scale proportionally.")]
        [Range(1,3)] public float OrbitalFontScale=1;
        [InspectorName("Graph node font size"),Tooltip("Information on graph nodes. Nodes grow with the text. The Orbital Wheel uses its own proportional scale above.")]
        [Range(10,24)] public float GraphFontSize=13;
        [Tooltip("Independent font size for Quantum Inspector buttons. Saved with this settings preset.")]
        [Range(10,24)] public float ButtonFontSize=12;
        [Header("Layout")]
        [Tooltip("Orbital Wheel capacity only. Graph Mode has no fixed node limit.")]
        [Range(5,8)] public int MaximumVisibleNodes=5;
        public int NodeCapacity=>Mathf.Clamp(MaximumVisibleNodes,5,8);
        public float OrbitalNodeScale=>1f-(NodeCapacity-5)*.05f;
        public WheelNodeSize NodeSize = WheelNodeSize.Standard;
        [Tooltip("Requested radius; the wheel increases it when needed for hub and node clearance.")]
        [Range(185, 245)] public float OrbitRadius = 220;
        public ResearchLayouts Layouts = ResearchLayouts.Details;
        public bool WideResearchPanel;
        [Tooltip("Research panel width in editor pixels. Zero uses the standard/wide preset. Dragging the divider saves a custom width here.")]
        [Min(0)] public float ResearchPanelWidth;
        public float PreferredResearchPanelWidth=>ResearchPanelWidth>0?Mathf.Max(240,ResearchPanelWidth):WideResearchPanel?420:320;
        [Header("Graph workspace")]
        [Tooltip("Freeze automatic arrangement and queue content changes. Project assets, folders and GameObject selections still switch the graph unless Lock Unity selection is enabled. Apply Updates accepts queued changes; Arrange is explicit.")]
        [SerializeField, FormerlySerializedAs("GraphInvestigationLock")] private bool graphLockDefault;
        [SerializeField, FormerlySerializedAs("GraphMotion")] private QuantumGraphMotionMode graphMotionDefault=QuantumGraphMotionMode.Full;
        [SerializeField, FormerlySerializedAs("GraphPalette")] private QuantumGraphPalette graphPaletteDefault=QuantumGraphPalette.CosmicNebula;
        [NonSerialized] private bool? graphLockOverride;
        [NonSerialized] private QuantumGraphMotionMode? graphMotionOverride;
        [NonSerialized] private QuantumGraphPalette? graphPaletteOverride;
        public bool GraphInvestigationLock {get=>graphLockOverride??graphLockDefault;set=>graphLockOverride=value;}
        public QuantumGraphMotionMode GraphMotion {get=>graphMotionOverride??graphMotionDefault;set=>graphMotionOverride=value;}
        public QuantumGraphPalette GraphPalette {get=>graphPaletteOverride??graphPaletteDefault;set=>graphPaletteOverride=value;}
        [NonSerialized] public QuantumGraphQuality GraphQuality=QuantumGraphQuality.Balanced;
        [NonSerialized] public bool GraphAmbient;
        [NonSerialized] public bool GraphAmbientWhileLocked;
        [SerializeField,Range(1,6),Tooltip("Default overview dot size multiplier. 2 doubles the original diameter. The graph toolbar provides a personal override.")]
        private float graphDotSizeDefault=2;
        [NonSerialized] private float? graphDotSizeOverride;
        public float GraphDotSize {get=>Mathf.Clamp(graphDotSizeOverride??graphDotSizeDefault,1,6);set=>graphDotSizeOverride=float.IsFinite(value)?Mathf.Clamp(value,1,6):2;}
        [SerializeField,Tooltip("Show navigation help, relationship information and research panels around Graph Mode. Off gives the clean, larger graph view.")]
        private bool graphShowAuxiliaryDefault;
        [NonSerialized] private bool? graphShowAuxiliaryOverride;
        public bool GraphShowAuxiliary {get=>graphShowAuxiliaryOverride??graphShowAuxiliaryDefault;set=>graphShowAuxiliaryOverride=value;}
        public void RestoreFactoryDefaults()
        {
            var defaults=CreateInstance<InspectorWheelSettings>();string originalName=name;
            try{EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(defaults),this);name=originalName;graphLockOverride=graphShowAuxiliaryOverride=null;graphMotionOverride=null;graphPaletteOverride=null;graphDotSizeOverride=null;Revision++;}
            finally{DestroyImmediate(defaults);}
        }
        public bool AmbientEnabled=>GraphPalette==QuantumGraphPalette.CosmicNebula&&EffectiveGraphMotion==QuantumGraphMotionMode.Full&&GraphAmbient&&(!GraphInvestigationLock||GraphAmbientWhileLocked);
        public QuantumNebulaTokens Nebula=new QuantumNebulaTokens();
        public QuantumGraphMotionMode EffectiveGraphMotion=>ReducedMotion&&GraphMotion==QuantumGraphMotionMode.Full?QuantumGraphMotionMode.Reduced:GraphMotion;
        public bool GraphIncludePackages;
        public QuantumProjectGrouping GraphProjectGrouping;
        public QuantumGraphOverlays GraphOverlays=QuantumGraphOverlays.None;
        [Range(120,360)] public float GraphNodeWidth=180;
        [Tooltip("Clustered Graph link width in editor pixels. Stays constant while zooming; selected links are emphasized.")]
        [Range(.5f,6)] public float GraphLinkThickness=2;
        [Range(12,100)] public float GraphNodeGap=28;
        [Range(80,300)] public float GraphMinimumClusterRadius=140;
        [Range(80,350)] public float GraphBaseDistance=150;
        [Range(16,160)] public float GraphSafetyGap=55;
        [Range(160,450)] public float GraphBranchLength=240;
        [Range(16,400)] public int GraphItemsPerTick=80;
        [Range(1,12)] public float GraphWorkMilliseconds=4;
        [Tooltip("First matching rule wins. Reclassifies presentation only; never changes components.")]
        public List<QuantumClusterRule> GraphCategoryRules=new List<QuantumClusterRule>();
        [Header("Navigation")]
        public bool SnapToSlots = true;
        public bool Momentum = true;
        public bool KeyboardNavigation = true;
        [Range(.1f, 2)] public float ScrollSensitivity = .5f;
        [Range(3, 25)] public float Smoothing = 12;
        [Range(2, 20)] public float MomentumFriction = 7;
        [Header("Research")]
        public bool SearchReferences;
        public bool ShowRelationshipOverlay = true;
        public RelationshipKinds Relationships = RelationshipKinds.All;
        [Range(100, 50000)] public int MaxPropertiesPerObject = 10000;
        [Range(5, 200)] public int IncomingScanBatch = 35;
        [Header("Presentation")]
        public bool ReducedMotion;
        [Range(0, 1)] public float AnimationIntensity = .65f;
        [Header("Selection and focus transitions")]
        [Tooltip("Additional motion multiplier for Project selections. Zero keeps selection highlighting without impact.")]
        [Range(0,1)] public float ProjectSelectionIntensity = .2f;
        [Range(0,1)] public float ClickReaction = .45f;
        [Range(0,1)] public float NavigationGlow = .2f;
        public bool FocusTransitions = true;
        [Range(.2f,1.2f)] public float FocusTransitionDuration = .55f;
        [Tooltip("How far new nodes spring outward on navigation, before idle floating resumes.")]
        [Range(0,100)] public float NodeArrivalDistance = 60;
        [Header("Floating node motion")]
        [Tooltip("Maximum idle displacement in unscaled wheel pixels.")]
        [Range(0, 6)] public float BobAmplitude = 2.6f;
        [Range(.2f, 2)] public float BobSpeed = .65f;
        [Range(0, 3)] public float MicroDrift = 1.1f;
        [Tooltip("Spring stiffness. Higher values return nodes to their orbit faster.")]
        [Range(40, 400)] public float SpringStrength = 150;
        [Tooltip("Damping ratio: below 1 allows a small overshoot; 1 is critically damped.")]
        [Range(.45f, 1.5f)] public float SpringDamping = .68f;
        [Header("Lighting")]
        [Range(0, 2)] public float GlowStrength = 1;
        public bool ShowGrid = true;
        public bool ShowParticles = true;
        [Range(15, 60)] public int FramesPerSecond = 30;
        [ColorUsage(false)] public Color OrbitColor = new Color(.12f,.75f,1);
        [ColorUsage(false)] public Color AccentColor = new Color(.7f,.3f,1);
        [NonSerialized] public int Revision;
        private void OnValidate() {OrbitalFontScale=float.IsFinite(OrbitalFontScale)?Mathf.Clamp(OrbitalFontScale,1,3):1;GraphFontSize=float.IsFinite(GraphFontSize)?Mathf.Clamp(GraphFontSize,10,24):13;ButtonFontSize=float.IsFinite(ButtonFontSize)?Mathf.Clamp(ButtonFontSize,10,24):12;MaximumVisibleNodes=Mathf.Clamp(MaximumVisibleNodes,5,8);GraphLinkThickness=float.IsFinite(GraphLinkThickness)?Mathf.Clamp(GraphLinkThickness,.5f,6):2;Revision++;}

        public static InspectorWheelSettings LoadOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InspectorWheelSettings>(DefaultPath);
            if (asset != null) return asset;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DefaultPath));
            AssetDatabase.Refresh();
            asset = CreateInstance<InspectorWheelSettings>();
            AssetDatabase.CreateAsset(asset, DefaultPath);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
