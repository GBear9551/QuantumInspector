using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    /// <summary>Only Unity-visible serialized fields. Entries retain paths, never disposable SerializedProperty handles.</summary>
    public static class SerializedParameterService
    {
        public static bool Exists(Object owner,string path)
        {
            if(owner==null)return false;
            if(string.IsNullOrEmpty(path))return true;
            using(var serialized=new SerializedObject(owner))return serialized.FindProperty(path)!=null;
        }
        public static string Label(Object owner,string path)
        {
            if(owner==null)return "Missing parameter";
            using(var serialized=new SerializedObject(owner))return serialized.FindProperty(path)?.displayName??"Missing parameter";
        }
        public static bool Expandable(SerializedProperty p)=>p!=null&&((p.isArray&&p.propertyType!=SerializedPropertyType.String)||
            ((p.propertyType==SerializedPropertyType.Generic||p.propertyType==SerializedPropertyType.ManagedReference)&&p.hasVisibleChildren));
        public static string ParentPath(string path)
        {
            if(string.IsNullOrEmpty(path))return "";
            int array=path.LastIndexOf(".Array.",StringComparison.Ordinal);
            if(array>=0&&path.IndexOf('.',array+7)<0)return path.Substring(0,array);
            int dot=path.LastIndexOf('.');return dot<0?"":path.Substring(0,dot);
        }
        public static List<OrbitalEntry> Children(Object owner,string parent,int limit=10000)
        {
            var result=new List<OrbitalEntry>();
            foreach(var entry in Enumerate(owner,parent)){if(result.Count>=limit)break;result.Add(entry);}
            return result;
        }
        public static IEnumerable<OrbitalEntry> Enumerate(Object owner,string parent)
        {
            if(owner==null)yield break;
            using(var serialized=new SerializedObject(owner))
            {
                serialized.UpdateIfRequiredOrScript();int ordinal=0;
                if(!string.IsNullOrEmpty(parent))
                {
                    var root=serialized.FindProperty(parent);if(root==null||!Expandable(root))yield break;
                    if(root.isArray)
                    {
                        yield return Entry(owner,serialized.FindProperty(parent+".Array.size"),ordinal++);
                        for(int i=0;i<root.arraySize;i++)yield return Entry(owner,root.GetArrayElementAtIndex(i),ordinal++);
                        yield break;
                    }
                    var cursor=root.Copy();var end=root.GetEndProperty();
                    if(cursor.NextVisible(true))do
                    {
                        if(SerializedProperty.EqualContents(cursor,end)||cursor.depth<=root.depth)break;
                        yield return Entry(owner,cursor,ordinal++);
                    }while(cursor.NextVisible(false));
                }
                else
                {
                    var enabled=owner is Behaviour?serialized.FindProperty("m_Enabled"):null;
                    if(enabled!=null)yield return Entry(owner,enabled,ordinal++);
                    var cursor=serialized.GetIterator();
                    if(cursor.NextVisible(true))do
                    {if(enabled==null||cursor.propertyPath!="m_Enabled")yield return Entry(owner,cursor,ordinal++);}
                    while(cursor.NextVisible(false));
                }
            }
        }
        private static OrbitalEntry Entry(Object owner,SerializedProperty p,int ordinal)
        {
            bool expand=Expandable(p),editable=p.editable&&p.propertyPath!="m_Script";
            string type=p.isArray&&p.propertyType!=SerializedPropertyType.String?"Collection · "+p.arraySize:p.propertyType.ToString();
            return new OrbitalEntry{Kind=OrbitalEntryKind.Parameter,Target=owner,PropertyPath=p.propertyPath,
                Name=p.displayName,TypeName=type,CategoryName="Serialized parameter",Category=ComponentCategory.System,
                Glyph=expand?"{ }":p.propertyType==SerializedPropertyType.ObjectReference?"↗":"ƒ",CanExpand=expand,Editable=editable,
                CustomColor=expand?new Color(.7f,.5f,1):new Color(.3f,.85f,.95f),Ordinal=ordinal,
                Description=p.propertyPath+" · "+(editable?"Serialized · editable":"Serialized · read only")+(expand?" · click to explore children":""),
                SearchText=ComponentDiscoveryService.Normalize(p.displayName+" "+p.propertyPath+" "+type+" serialized parameter")};
        }
    }
    internal sealed class ParameterOrbitalProvider:IOrbitalDataProvider
    {
        public bool Supports(WheelScopeKind kind)=>kind==WheelScopeKind.ComponentFocus||kind==WheelScopeKind.Property;
        public List<OrbitalEntry> Discover(WheelScope scope,ReferenceGraphService graph,InspectorWheelSettings settings)=>
            SerializedParameterService.Children(scope.Component,scope.PropertyPath,settings.MaxPropertiesPerObject);
        public void Invalidate(){}
    }
}
