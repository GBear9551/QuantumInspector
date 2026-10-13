using System;
using UnityEngine;

namespace FightSong.InspectorTool
{
    public static class QuantumClusterClassifier
    {
        public static QuantumClusterCategory Classify(Type type,InspectorWheelSettings settings,out string tags)
        {
            tags="";if(type==null)return QuantumClusterCategory.Other;
            if(settings.GraphCategoryRules!=null)foreach(var rule in settings.GraphCategoryRules)
            {
                if(rule==null||!rule.Enabled||string.IsNullOrWhiteSpace(rule.Pattern))continue;
                bool match=rule.Match==QuantumRuleMatch.NamespacePrefix?(type.Namespace??"").StartsWith(rule.Pattern,StringComparison.Ordinal):
                    rule.Match==QuantumRuleMatch.TypeName?(type.FullName==rule.Pattern||type.Name==rule.Pattern):MatchesBase(type,rule.Pattern);
                if(match){tags=rule.AdditionalTags;return rule.Category;}
            }
            string space=type.Namespace??"",name=type.Name;
            if(InheritsNamespace(type,"Unity.Netcode","Unity.Networking","Mirror","Photon","FishNet"))return QuantumClusterCategory.Networking;
            if(InheritsNamespace(type,"UnityEngine.UI","UnityEngine.EventSystems","TMPro")||typeof(Canvas).IsAssignableFrom(type)||typeof(CanvasRenderer).IsAssignableFrom(type)||typeof(RectTransform).IsAssignableFrom(type))return QuantumClusterCategory.UI;
            if(typeof(Animator).IsAssignableFrom(type)||typeof(Animation).IsAssignableFrom(type)||space.StartsWith("UnityEngine.Animations",StringComparison.Ordinal))return QuantumClusterCategory.Animation;
            if(typeof(Collider).IsAssignableFrom(type)||typeof(Collider2D).IsAssignableFrom(type)||typeof(Rigidbody).IsAssignableFrom(type)||typeof(Rigidbody2D).IsAssignableFrom(type)||typeof(Joint).IsAssignableFrom(type)||typeof(Joint2D).IsAssignableFrom(type)||typeof(CharacterController).IsAssignableFrom(type)||typeof(Cloth).IsAssignableFrom(type)||typeof(Effector2D).IsAssignableFrom(type))return QuantumClusterCategory.Physics;
            if(typeof(AudioBehaviour).IsAssignableFrom(type)||space=="UnityEngine.Audio"||(!typeof(MonoBehaviour).IsAssignableFrom(type)&&name.StartsWith("Audio",StringComparison.Ordinal)))return QuantumClusterCategory.Audio;
            if(typeof(Renderer).IsAssignableFrom(type)||typeof(MeshFilter).IsAssignableFrom(type)||typeof(Camera).IsAssignableFrom(type)||typeof(Light).IsAssignableFrom(type)||typeof(ParticleSystem).IsAssignableFrom(type)||space.StartsWith("UnityEngine.Rendering",StringComparison.Ordinal))return QuantumClusterCategory.Rendering;
            return typeof(MonoBehaviour).IsAssignableFrom(type)?QuantumClusterCategory.Scripting:QuantumClusterCategory.Other;
        }
        private static bool InheritsNamespace(Type type,params string[] prefixes)
        {
            for(var current=type;current!=null;current=current.BaseType)
                foreach(var prefix in prefixes)
                    if(current.Namespace==prefix||(current.Namespace??"").StartsWith(prefix+".",StringComparison.Ordinal))return true;
            return false;
        }
        private static bool MatchesBase(Type type,string pattern){for(var t=type;t!=null;t=t.BaseType)if(t.FullName==pattern||t.Name==pattern)return true;return false;}
        public static Color Tint(QuantumClusterCategory category)=>category switch
        {
            QuantumClusterCategory.Scripting=>new Color(.72f,.48f,1),QuantumClusterCategory.Physics=>new Color(.5f,.93f,.55f),
            QuantumClusterCategory.Rendering=>new Color(.25f,.6f,1),QuantumClusterCategory.Audio=>new Color(1,.73f,.3f),
            QuantumClusterCategory.Animation=>new Color(1,.45f,.72f),QuantumClusterCategory.UI=>new Color(.3f,.95f,.87f),
            QuantumClusterCategory.Networking=>new Color(.4f,.75f,1),_=>new Color(.7f,.76f,.87f)
        };
    }
}
