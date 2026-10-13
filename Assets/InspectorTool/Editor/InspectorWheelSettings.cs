using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    [Flags]
    public enum ResearchLayouts { None = 0, Overview = 1, Details = 2, [InspectorName("Relationship preview")] Graph = 4, References = 8, Runtime = 16, History = 32 }
    [Flags]
    public enum RelationshipKinds { None = 0, Hierarchy = 1, Serialized = 2, Dependency = 4, Event = 8, Instrumented = 16, ScriptableObject = 32, All = 63 }
    public enum WheelNodeSize { Compact, Standard, Large }

    [CreateAssetMenu(menuName = "Fight Song/Quantum Inspector Wheel Settings")]
    public sealed class InspectorWheelSettings : ScriptableObject
    {
        public const string DefaultPath = "Assets/Scripts/FightSong/InspectorTool/Editor/QuantumInspectorWheelSettings.asset";
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
        public bool GraphInvestigationLock=true;
        public bool GraphIncludePackages;
        public QuantumProjectGrouping GraphProjectGrouping;
        public QuantumGraphOverlays GraphOverlays=QuantumGraphOverlays.None;
        [Range(120,360)] public float GraphNodeWidth=180;
        [Range(10,20)] public float GraphFontSize=13;
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
        private void OnValidate() {MaximumVisibleNodes=Mathf.Clamp(MaximumVisibleNodes,5,8);Revision++;}

        public static InspectorWheelSettings LoadOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InspectorWheelSettings>(DefaultPath);
            if (asset != null) return asset;
            asset = CreateInstance<InspectorWheelSettings>();
            AssetDatabase.CreateAsset(asset, DefaultPath);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
