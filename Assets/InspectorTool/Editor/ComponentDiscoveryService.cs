using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FightSong.InspectorTool
{
    public enum ComponentCategory { Transform, Rendering, Physics, Audio, Script, System, Missing }
    public sealed class ComponentEntry:OrbitalEntry { }
    public sealed class ComponentDiscoveryService
    {
        public List<ComponentEntry> Discover(GameObject target, ReferenceGraphService graph, bool references, ComponentAliasDatabase aliases=null)
        {
            var entries=new List<ComponentEntry>();
            if(target==null)return entries;
            var components=target.GetComponents<Component>();
            for(int i=0;i<components.Length;i++)
            {
                var component=components[i];
                var category=Category(component);
                var entry=new ComponentEntry {Component=component, Ordinal=i,Category=category,CategoryName=category.ToString(),
                    Name=component!=null?ObjectNames.NicifyVariableName(component.GetType().Name):"Missing script"};
                entry.TypeName=component!=null?component.GetType().Name:"Missing script";
                var alias=(aliases!=null?aliases:ComponentAliasDatabase.Shared).Get(component);
                if(!string.IsNullOrEmpty(alias?.Name))entry.Name=alias.Name;
                entry.Icon=alias?.Icon;
                entry.CustomColor=alias!=null&&alias.UseColor?alias.Color:(Color?)null;
                string metadata="";
                if(component!=null)
                {
                    foreach(var provider in InspectorWheelExtensions.MetadataProviders)
                        try { metadata+=" "+string.Join(" ",provider.Tags(component)??Array.Empty<string>()); }
                        catch(Exception ex) { metadata+=" [metadata provider: "+ex.GetType().Name+"]"; }
                    if(component is MonoBehaviour behaviour)
                    {
                        var script=MonoScript.FromMonoBehaviour(behaviour);
                        if(script!=null)metadata+=" "+script.name;
                    }
                    if(references)
                        metadata+=" "+string.Join(" ",graph.Outgoing(component).Where(e=>e.Target!=null).Select(e=>e.Target.name));
                }
                entry.SearchText=Normalize(entry.Name+" "+entry.CategoryName+" "+component?.GetType().FullName+" "+metadata);
                entries.Add(entry);
            }
            return entries;
        }
        public static List<T> Filter<T>(IReadOnlyList<T> entries,string query) where T:OrbitalEntry
        {
            var terms=(query??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).ToArray();
            return entries.Where(e=>terms.All(t=>e.SearchText.Contains(t))).ToList();
        }
        public static string Normalize(string value) => new string((value??"").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        public static ComponentCategory Category(Component component)
        {
            if(component==null)return ComponentCategory.Missing;
            if(component is Transform)return ComponentCategory.Transform;
            if(component is Renderer || component is MeshFilter || component is Camera || component is Light)return ComponentCategory.Rendering;
            if(component is Collider || component is Collider2D || component is Rigidbody || component is Rigidbody2D || component is Joint || component is Joint2D)return ComponentCategory.Physics;
            if(component is AudioSource || component is AudioListener)return ComponentCategory.Audio;
            return component is MonoBehaviour ? ComponentCategory.Script : ComponentCategory.System;
        }
    }
}
