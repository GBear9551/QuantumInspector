using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public enum InspectorNavigationTab { Hierarchy, Project }
    public enum WheelScopeKind { SceneRoots, Components, Children, ProjectFolder, ComponentFocus, Property }
    public enum OrbitalEntryKind { Component, GameObject, Folder, Asset, Parameter }

    public class OrbitalEntry
    {
        public Object Target;
        public Component Component { get=>Target as Component; set=>Target=value; }
        public OrbitalEntryKind Kind;
        public string Name, TypeName, CategoryName, SearchText, AssetPath, Description, Glyph;
        public ComponentCategory Category;
        public Texture2D Icon;
        public Color? CustomColor;
        public int Ordinal;
        public string PropertyPath;
        public bool CanExpand, Editable;
        public string Key=>Kind==OrbitalEntryKind.Parameter?"property:"+(Target!=null?Target.GetEntityId().ToString():"missing")+":"+PropertyPath:!string.IsNullOrEmpty(AssetPath)?"asset:"+AssetPath:Target!=null?Target.GetEntityId().ToString():"missing:"+Ordinal;
    }

    [Serializable]
    public sealed class WheelScope
    {
        public WheelScopeKind Kind;
        public GameObject Object;
        public Component Component;
        public Object Asset;
        public string Folder="Assets", FolderGuid="", Query="";
        public string PropertyPath="", SelectedProperty="";
        public double Rotation;
        public int Capacity=5;
        public bool IsComponentFocus=>Kind==WheelScopeKind.ComponentFocus||Kind==WheelScopeKind.Property;
        public bool Available=>IsComponentFocus?Component!=null&&Object!=null:Kind==WheelScopeKind.SceneRoots || Kind==WheelScopeKind.ProjectFolder || Object!=null;
        public string FolderPath
        {
            get
            {
                var moved=string.IsNullOrEmpty(FolderGuid)?"":AssetDatabase.GUIDToAssetPath(FolderGuid);
                return AssetDatabase.IsValidFolder(moved)?moved:AssetDatabase.IsValidFolder(Folder)?Folder:"Assets";
            }
        }
        public string Title=>Kind==WheelScopeKind.Property?SerializedParameterService.Label(Component,PropertyPath):Kind==WheelScopeKind.ComponentFocus?ComponentAliasDatabase.DisplayName(Component):Kind==WheelScopeKind.SceneRoots?"SCENE ROOTS":Kind==WheelScopeKind.ProjectFolder?System.IO.Path.GetFileName(FolderPath):Object!=null?Object.name:"Missing object";
        public string EntryLabel=>IsComponentFocus?"PARAMETERS":Kind==WheelScopeKind.Components?"COMPONENTS":Kind==WheelScopeKind.Children?"CHILDREN":Kind==WheelScopeKind.SceneRoots?"GAMEOBJECTS":"FOLDERS & ASSETS";
        public string Breadcrumb
        {
            get
            {
                if(Kind==WheelScopeKind.ProjectFolder)return "Project  /  "+FolderPath+"  /  Folders & assets";
                if(Kind==WheelScopeKind.SceneRoots)return "Hierarchy  /  Loaded scenes  /  Root GameObjects";
                var names=new List<string>();
                for(var t=Object!=null?Object.transform:null;t!=null;t=t.parent)names.Insert(0,t.name);
                return "Hierarchy  /  "+(Object!=null?Object.scene.name:"Missing")+"  /  "+string.Join(" / ",names)+(IsComponentFocus?"  /  "+ComponentAliasDatabase.DisplayName(Component)+(Kind==WheelScopeKind.Property?"  /  "+PropertyPath:""):"")+"  /  "+EntryLabel;
            }
        }
        public WheelScope Copy()=>new WheelScope{Kind=Kind,Object=Object,Component=Component,Asset=Asset,Folder=Folder,FolderGuid=FolderGuid,Query=Query,Rotation=Rotation,Capacity=Capacity,PropertyPath=PropertyPath,SelectedProperty=SelectedProperty};
        public bool SameLocation(WheelScope other)=>other!=null&&Kind==other.Kind&&Object==other.Object&&Component==other.Component&&Asset==other.Asset&&FolderPath==other.FolderPath&&Query==other.Query&&PropertyPath==other.PropertyPath;
    }

    [Serializable]
    public sealed class WheelNavigationHistory
    {
        [SerializeField] private List<WheelScope> entries=new List<WheelScope>();
        [SerializeField] private int cursor=-1;
        public IReadOnlyList<WheelScope> Entries=>entries;
        public int Cursor=>cursor;
        public WheelScope Current=>cursor>=0&&cursor<entries.Count?entries[cursor]:null;
        public void Visit(WheelScope scope,bool replace=false)
        {
            if(scope==null)return;
            if(replace&&Current!=null){entries[cursor]=scope.Copy();return;}
            if(scope.SameLocation(Current))return;
            if(cursor+1<entries.Count)entries.RemoveRange(cursor+1,entries.Count-cursor-1);
            entries.Add(scope.Copy());if(entries.Count>100)entries.RemoveAt(0);cursor=entries.Count-1;
        }
        private int Find(int direction)
        {
            if(direction==0)return -1;
            direction=direction<0?-1:1;
            for(int i=cursor+direction;i>=0&&i<entries.Count;i+=direction)if(entries[i].Available)return i;
            return -1;
        }
        public bool CanMove(int direction)=>Find(direction)>=0;
        public bool Move(int direction)=>Select(Find(direction));
        public bool Select(int index)
        {if(index<0||index>=entries.Count||!entries[index].Available)return false;cursor=index;return true;}
    }
}
