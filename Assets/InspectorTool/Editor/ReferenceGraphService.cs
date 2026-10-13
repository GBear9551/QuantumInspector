using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public sealed class ReferenceGraphService
    {
        private readonly Dictionary<Object,List<ResearchRelationship>> cache=new Dictionary<Object,List<ResearchRelationship>>();
        public readonly List<ResearchRelationship> Incoming=new List<ResearchRelationship>();
        private Component[] scan=Array.Empty<Component>();
        private int scanIndex;
        private Object scanTarget;
        public int PropertyLimit=10000;
        public bool Truncated {get;private set;}
        private bool incomingStale;
        public bool Scanning => scanIndex<scan.Length;
        public string ScanStatus => Scanning ? $"Scanning loaded objects {scanIndex:N0}/{scan.Length:N0}" : scanTarget!=null ? $"Loaded-scene scan complete · {Incoming.Count} incoming links"+(incomingStale?" · scene changed; rescan to refresh":"") : "Incoming references: scan on demand";
        public void Invalidate() { cache.Clear();Truncated=false;CancelScan();Incoming.Clear();scanTarget=null; }
        public void InvalidateCachedData() {cache.Clear();Truncated=false;if(scanTarget!=null)incomingStale=true;}
        public void CancelScan() { scan=Array.Empty<Component>();scanIndex=0; }
        public IReadOnlyList<ResearchRelationship> Outgoing(Object source)
        {
            if(source==null)return Array.Empty<ResearchRelationship>();
            if(cache.TryGetValue(source,out var cached))return cached;
            var result=new List<ResearchRelationship>();
            try
            {
                using(var serialized=new SerializedObject(source))
                {
                    var property=serialized.GetIterator();
                    int visited=0;
                    while(property.Next(true))
                    {
                        if(++visited>PropertyLimit) { Truncated=true;break; }
                        if(property.propertyType!=SerializedPropertyType.ObjectReference || property.propertyPath=="m_Script" || property.propertyPath=="m_GameObject")continue;
                        var target=property.objectReferenceValue;
                        if(target==null)continue;
                        // Persistent UnityEvent targets are serialized; runtime listeners are not inferred.
                        var kind=property.propertyPath.Contains("m_PersistentCalls.m_Calls") ? RelationshipKinds.Event :
                            target is ScriptableObject ? RelationshipKinds.ScriptableObject : RelationshipKinds.Serialized;
                        result.Add(new ResearchRelationship(source,target,property.propertyPath,kind));
                    }
                }
                if(source is Component component)
                    foreach(RequireComponent requirement in component.GetType().GetCustomAttributes(typeof(RequireComponent),true))
                        foreach(var type in new[]{requirement.m_Type0,requirement.m_Type1,requirement.m_Type2})
                        {
                            if(type==null)continue;
                            var required=component.GetComponent(type);
                            if(required!=null)result.Add(new ResearchRelationship(component,required,"RequireComponent("+type.Name+")",RelationshipKinds.Dependency));
                        }
            }
            catch(Exception ex) when(ex is MissingReferenceException || ex is ArgumentException || ex is InvalidOperationException) { }
            cache[source]=result;
            return result;
        }
        public List<ResearchRelationship> ForObject(GameObject target, Component component=null)
        {
            var result=new List<ResearchRelationship>();
            if(target==null)return result;
            if(component!=null)result.AddRange(Outgoing(component));
            else foreach(var item in target.GetComponents<Component>())if(item!=null)result.AddRange(Outgoing(item));
            if(target.transform.parent!=null)result.Add(new ResearchRelationship(target,target.transform.parent.gameObject,"parent",RelationshipKinds.Hierarchy));
            foreach(Transform child in target.transform)result.Add(new ResearchRelationship(target,child.gameObject,"child",RelationshipKinds.Hierarchy));
            if(EditorApplication.isPlaying)
                foreach(var provider in InspectorWheelExtensions.RuntimeProviders)
                    try
                    {
                        foreach(var link in provider.Relationships(target)??Array.Empty<ResearchRelationship>())
                            if(link.Source!=null&&link.Target!=null)result.Add(new ResearchRelationship(link.Source,link.Target,provider.Name+": "+link.Path,RelationshipKinds.Instrumented));
                    }
                    catch(Exception) { /* Provider failures must not interrupt inspection. */ }
            return result;
        }
        public void StartIncoming(Object target)
        {
            Incoming.Clear();scanTarget=target;scanIndex=0;incomingStale=false;
            scan=target==null ? Array.Empty<Component>() : Resources.FindObjectsOfTypeAll<Component>()
                .Where(c=>c!=null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
                    && (c.hideFlags&HideFlags.DontSave)==0).ToArray();
        }
        public bool Tick(int budget)
        {
            if(!Scanning)return false;
            if(scanTarget==null){CancelScan();return true;}
            int end=Math.Min(scan.Length,scanIndex+Math.Max(1,budget));
            for(;scanIndex<end;scanIndex++)
            {
                var source=scan[scanIndex]; if(source==null)continue;
                foreach(var link in Outgoing(source))
                    if(link.Target==scanTarget || (scanTarget is GameObject go && link.Target is Component c && c.gameObject==go))
                        Incoming.Add(link);
            }
            return true;
        }
    }
}
