using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public enum QuantumGraphNodeKind { GameObject, Component, Parameter, Folder, Asset, Cluster, World }
    public enum QuantumClusterCategory { Scripting, Physics, Rendering, Audio, Animation, UI, Networking, Other }
    public enum QuantumRuleMatch { TypeName, NamespacePrefix, BaseType }
    public enum QuantumProjectGrouping { FolderHierarchy, AssetType, ScriptCategory }
    [Flags] public enum QuantumGraphOverlays { None=0, SerializedReferences=1, AssetDependencies=2, PrefabSources=4 }
    [Serializable] public sealed class QuantumClusterRule
    {
        public bool Enabled=true;
        public QuantumRuleMatch Match;
        public string Pattern="";
        public QuantumClusterCategory Category=QuantumClusterCategory.Scripting;
        public string AdditionalTags="";
    }
    public sealed class QuantumGraphNode
    {
        public string Id,ParentId,ClusterId,Label,Subtitle,Glyph,AssetPath,PropertyPath,SearchText;
        public QuantumGraphNodeKind Kind;
        public Object Target;
        public Color Color;
        public Texture2D Icon;
        public Vector2 Position,Size=new Vector2(180,64);
        public bool Positioned,Manual,Pinned,Expanded,Loaded,Loading,CanExpand,Active=true,ReferenceOnly;
        public float Radius;
        public int MemberCount;
        public Vector2 DisplayPosition=>Kind==QuantumGraphNodeKind.Cluster&&Expanded?Position-Vector2.up*(Radius+38):Position;
        public Rect Bounds=>new Rect(DisplayPosition-Size*.5f,Size);
    }
    public sealed class QuantumGraphEdge
    {
        public string From,To,Label;
        public bool Overlay;
        public Color Color;
        public string Key=>From+"|"+To+"|"+Label;
    }
    public sealed class QuantumGraphDocument
    {
        public string Key,RootId;
        public bool Project,EntireProject;
        public readonly Dictionary<string,QuantumGraphNode> Nodes=new Dictionary<string,QuantumGraphNode>();
        public readonly Dictionary<string,QuantumGraphEdge> Edges=new Dictionary<string,QuantumGraphEdge>();
        public readonly Dictionary<string,HashSet<string>> Children=new Dictionary<string,HashSet<string>>();
        public int Revision;
        public QuantumGraphNode Root=>RootId!=null&&Nodes.TryGetValue(RootId,out var n)?n:null;
        public QuantumGraphNode Upsert(QuantumGraphNode entry)
        {
            if(Nodes.TryGetValue(entry.Id,out var old))
            {
                if(old.ParentId!=entry.ParentId&&old.ParentId!=null)
                {
                    if(Children.TryGetValue(old.ParentId,out var siblings))siblings.Remove(old.Id);
                    foreach(var key in Edges.Where(e=>!e.Value.Overlay&&e.Value.From==old.ParentId&&e.Value.To==old.Id).Select(e=>e.Key).ToArray())Edges.Remove(key);
                }
                old.ParentId=entry.ParentId;old.ClusterId=entry.ClusterId;old.Label=entry.Label;old.Subtitle=entry.Subtitle;
                old.Target=entry.Target;old.AssetPath=entry.AssetPath;old.PropertyPath=entry.PropertyPath;old.Kind=entry.Kind;
                old.Color=entry.Color;old.Glyph=entry.Glyph;old.Icon=entry.Icon;old.SearchText=entry.SearchText;old.CanExpand=entry.CanExpand;old.Active=true;
                entry=old;
            }
            else Nodes.Add(entry.Id,entry);
            if(entry.ParentId!=null)
            {if(!Children.TryGetValue(entry.ParentId,out var list))Children[entry.ParentId]=list=new HashSet<string>();list.Add(entry.Id);}
            Revision++;return entry;
        }
        public void Reparent(QuantumGraphNode node,string parent)
        {
            if(node.Id==RootId||node.ParentId==parent)return;
            var ancestor=parent;var visited=new HashSet<string>();
            while(ancestor!=null&&Nodes.TryGetValue(ancestor,out var entry))
            {if(ancestor==node.Id||!visited.Add(ancestor))return;ancestor=entry.ParentId;}
            if(node.ParentId!=null&&Children.TryGetValue(node.ParentId,out var old))old.Remove(node.Id);
            foreach(var key in Edges.Where(e=>e.Value.From==node.ParentId&&e.Value.To==node.Id&&(e.Value.Label=="Folder child"||e.Value.Label=="Hierarchy")).Select(e=>e.Key).ToArray())Edges.Remove(key);
            node.ParentId=parent;node.ClusterId=null;
            if(parent!=null){if(!Children.TryGetValue(parent,out var siblings))Children[parent]=siblings=new HashSet<string>();siblings.Add(node.Id);}
            Revision++;
        }
        public void Link(string from,string to,string label,Color color,bool overlay=false)
        {
            if(from==to)return;
            var edge=new QuantumGraphEdge{From=from,To=to,Label=label,Color=color,Overlay=overlay};
            Edges[edge.Key]=edge;Revision++;
        }
        public IEnumerable<QuantumGraphNode> DirectChildren(string id)=>Children.TryGetValue(id,out var ids)?ids.Where(Nodes.ContainsKey).Select(k=>Nodes[k]):Enumerable.Empty<QuantumGraphNode>();
        public IEnumerable<QuantumGraphNode> Descendants(string id)
        {
            var pending=new Stack<string>();var seen=new HashSet<string>();pending.Push(id);
            while(pending.Count>0)
            {
                var next=pending.Pop();if(!seen.Add(next))continue;
                foreach(var n in DirectChildren(next)){yield return n;pending.Push(n.Id);}
            }
        }
        public bool Visible(QuantumGraphNode node)
        {
            if(!node.Active)return false;
            var parent=node;int guard=0;
            while(parent.ParentId!=null&&Nodes.TryGetValue(parent.ParentId,out parent))
                if(!parent.Active||!parent.Expanded||++guard>Nodes.Count)return false;
            return node.ClusterId==null||node.Id==node.ClusterId||!Nodes.TryGetValue(node.ClusterId,out var cluster)||cluster.Expanded;
        }
        public void RemoveBranch(string id)
        {
            var ids=Descendants(id).Select(n=>n.Id).Append(id).ToHashSet();
            foreach(var key in ids){Nodes.Remove(key);Children.Remove(key);}
            foreach(var group in Children.Values)group.ExceptWith(ids);
            foreach(var key in Edges.Where(e=>ids.Contains(e.Value.From)||ids.Contains(e.Value.To)).Select(e=>e.Key).ToArray())Edges.Remove(key);
            Revision++;
        }
    }
    public static class QuantumGraphIdentity
    {
        public static string ObjectId(Object target)=>target==null?"missing":InspectorObjectIdentity.TryGet(target,out var id)?id.ToString():"session:"+target.GetEntityId();
        public static string AssetId(string path){string guid=AssetDatabase.AssetPathToGUID(path);return "asset:"+(string.IsNullOrEmpty(guid)?path:guid);}
        public static uint Hash(string value){uint hash=2166136261;foreach(char c in value??"")hash=(hash^c)*16777619;return hash;}
    }
}
