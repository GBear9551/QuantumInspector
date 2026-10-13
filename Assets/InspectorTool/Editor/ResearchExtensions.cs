using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public readonly struct ResearchRelationship
    {
        public readonly Object Source, Target;
        public readonly string Path;
        public readonly RelationshipKinds Kind;
        public ResearchRelationship(Object source,Object target,string path,RelationshipKinds kind)
        { Source=source;Target=target;Path=path;Kind=kind; }
    }
    public readonly struct ResearchMeasurement
    {
        public readonly string Name, Value;
        public ResearchMeasurement(string name,string value) { Name=name;Value=value; }
    }
    // Implement in an Editor assembly. Providers adapt existing runtime instrumentation without
    // adding components, polling arbitrary properties, or creating runtime build dependencies.
    public interface IInspectorWheelRuntimeProvider
    {
        string Name { get; }
        IEnumerable<ResearchMeasurement> Sample(GameObject target);
        IEnumerable<ResearchRelationship> Relationships(GameObject target);
    }
    public interface IInspectorWheelMetadataProvider
    {
        IEnumerable<string> Tags(Component component);
    }
    public static class InspectorWheelExtensions
    {
        public static readonly List<IInspectorWheelRuntimeProvider> RuntimeProviders = new List<IInspectorWheelRuntimeProvider>();
        public static readonly List<IInspectorWheelMetadataProvider> MetadataProviders = new List<IInspectorWheelMetadataProvider>();
    }
}
