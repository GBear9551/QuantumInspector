using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    /// <summary>Only visible assets enter this bounded retry queue. Geometry never depends on the returned texture.</summary>
    public sealed class QuantumGraphPreviewCache
    {
        private sealed class Request
        {
            public QuantumGraphNode Node;
            public int Attempts;
            public double Next;
            public bool Done;
        }
        private readonly Dictionary<string,Request> cache=new Dictionary<string,Request>();
        private readonly HashSet<string> relevant=new HashSet<string>();
        public int Pending {get;private set;}
        public int Requests {get;private set;}
        public void Clear(){cache.Clear();relevant.Clear();Pending=Requests=0;}
        public void BeginVisible(){relevant.Clear();Pending=0;}
        public void RequestVisible(QuantumGraphNode node)
        {
            bool asset=node.Kind==QuantumGraphNodeKind.Asset&&!string.IsNullOrEmpty(node.AssetPath);
            bool owner=node.Kind==QuantumGraphNodeKind.GameObject&&node.Target!=null;
            if(!asset&&!owner)return;
            relevant.Add(node.Id);
            if(cache.TryGetValue(node.Id,out var known)){if(!known.Done)Pending++;return;}
            node.Icon=asset?AssetDatabase.GetCachedIcon(node.AssetPath) as Texture2D:AssetPreview.GetMiniThumbnail(node.Target);
            // Scene GameObject previews can synchronously render a whole scene on this Editor.
            // Keep its honest type icon; persistent prefab owners use the bounded preview queue.
            if(owner&&!EditorUtility.IsPersistent(node.Target))
            {node.PreviewState=QuantumGraphPreviewState.Fallback;cache.Add(node.Id,new Request{Node=node,Done=true});return;}
            node.PreviewState=QuantumGraphPreviewState.Pending;
            cache.Add(node.Id,new Request{Node=node});
            Pending++;
        }
        public bool Tick(double now,QuantumGraphDataProvider provider)
        {
            if(Pending==0)return false;
            bool changed=false;int budget=4;Pending=0;
            foreach(var pair in cache)
            {
                var request=pair.Value;
                if(request.Done||!relevant.Contains(pair.Key))continue;
                Pending++;
                if(request.Next>now||budget<=0)continue;
                budget--;Requests++;
                var target=provider.Resolve(request.Node);
                if(target==null){request.Done=true;request.Node.PreviewState=QuantumGraphPreviewState.Fallback;Pending--;changed=true;continue;}
                var preview=AssetPreview.GetAssetPreview(target);
                if(preview!=null){request.Node.Icon=preview;request.Node.PreviewState=QuantumGraphPreviewState.Ready;request.Done=true;changed=true;}
                else
                {
                    request.Node.Icon=AssetPreview.GetMiniThumbnail(target)??request.Node.Icon;
                    request.Attempts++;request.Next=now+.2;
                    request.Done=request.Attempts>=12||(!AssetPreview.IsLoadingAssetPreview(target.GetInstanceID())&&request.Attempts>=2);
                    if(request.Done)request.Node.PreviewState=QuantumGraphPreviewState.Fallback;
                    changed=true;
                }
                if(request.Done)Pending--;
            }
            return changed;
        }
    }
}
