using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public enum ComponentActionMode { Browse, Add, Remove }
    public sealed class ComponentActionContext
    {
        public ComponentActionMode Mode;
        public Component PendingRemoval,LastAdded;
        public string Message="";
        public int Revision;
        public void Reset(){Mode=ComponentActionMode.Browse;PendingRemoval=LastAdded=null;Message="";Revision++;}
    }

    /// <summary>Context editors are independent of the orbital presentation and its animation clock.</summary>
    internal sealed class RecursiveDetailsView:IDisposable
    {
        private readonly VisualElement root;
        private SerializedObject serialized;
        public RecursiveDetailsView(VisualElement root){this.root=root;}
        private void Text(string value,string style="research-line"){var label=new Label(value);label.AddToClassList(style);root.Add(label);}
        private Button Button(string label,Action action){var button=new Button(action){text=label};root.Add(button);return button;}
        public void Build(WheelScope scope,ComponentActionContext context,Action<Type> add,Action remove,Action cancel,Action<Object> navigate,Action<Object> bookmark)
        {
            if(context.Mode==ComponentActionMode.Add)
            {Catalog(scope.Object,context,add,cancel,navigate);return;}
            if(context.Mode==ComponentActionMode.Remove)
            {
                Text("REMOVE COMPONENT","research-value");
                Text("Select a component node to review its removal restrictions and dependencies.");
                var component=context.PendingRemoval;
                if(component!=null)
                {
                    Text(ComponentAliasDatabase.DisplayName(component),"research-value");
                    Text(component.GetType().FullName+" · instance "+component.GetEntityId());
                    var reason=ComponentCatalogService.CanRemove(component);
                    Text(reason??"Remove this exact component instance? Its serialized values will be removed. Undo restores it.",reason!=null?"quantum-empty":"research-line");
                    if(PrefabUtility.IsPartOfPrefabInstance(component))Text("This changes the prefab instance. Inherited removals are recorded as prefab overrides.");
                    Button("Confirm removal",remove).SetEnabled(reason==null);
                }
                if(!string.IsNullOrEmpty(context.Message))Text(context.Message,"quantum-empty");
                Button("Cancel removal",cancel);return;
            }
            if(!scope.IsComponentFocus||(scope.Component==null&&scope.Asset==null))
            {Text("Select a component or parameter node to inspect it. In Graph Mode, + expands a node; the root context menu has Add / Remove Component. In Wheel Mode, those actions are in the central hub.");return;}
            Object focused=scope.Component!=null?(Object)scope.Component:scope.Asset;
            Text(focused is Component named?ComponentAliasDatabase.DisplayName(named):focused.name,"research-value");
            Text(focused.GetType().FullName);
            string path=string.IsNullOrEmpty(scope.SelectedProperty)?scope.PropertyPath:scope.SelectedProperty;
            if(string.IsNullOrEmpty(path))
            {
                if(focused is Component aliased)root.Add(new ComponentAliasFields(aliased));
                Text("Choose a parameter node to edit it. Expand collections and nested structures to explore their children.");
                Text("The wheel exposes Unity-serialized fields. Runtime measurements belong to Runtime Research and are read only.");
            }
            else
            {
                serialized=new SerializedObject(focused);serialized.UpdateIfRequiredOrScript();
                var property=serialized.FindProperty(path);
                if(property==null){Text("This property no longer exists. Use Parent to return.","quantum-empty");return;}
                Text(property.displayName,"research-value");Text(path);
                bool editable=property.editable&&path!="m_Script";
                Text(editable?"Serialized field · edits support Undo and prefab overrides.":"Serialized field · read only.");
                var field=new PropertyField(property.Copy()){name="parameterEditor",bindingPath=path};
                field.AddToClassList("quantum-property-editor");field.SetEnabled(editable);root.Add(field);field.Bind(serialized);
                if(property.propertyType==SerializedPropertyType.ObjectReference)
                    Button("Explore referenced object",()=>
                    {
                        if(focused==null)return;
                        using(var latest=new SerializedObject(focused))
                        {var reference=latest.FindProperty(path);if(reference!=null&&reference.objectReferenceValue!=null)navigate(reference.objectReferenceValue);}
                    });
                if(property.isArray&&property.propertyType!=SerializedPropertyType.String)Text("Collection elements use Unity's current indices. Resizing or reordering changes those indices.");
            }
            Button("Open native Inspector",()=>EditorUtility.OpenPropertyEditor(focused));
            Button("Bookmark object",()=>bookmark(focused));
        }
        private void Catalog(GameObject target,ComponentActionContext context,Action<Type> add,Action cancel,Action<Object> navigate)
        {
            Text("ADD COMPONENT","research-value");Text(target!=null?"Install on "+target.name:"Missing GameObject");
            var failure=ComponentCatalogService.CanModify(target);
            if(failure!=null){Text(failure,"quantum-empty");Button("Close catalog",cancel);return;}
            if(context.LastAdded!=null)
            {
                var added=context.LastAdded;Text("Added "+ComponentAliasDatabase.DisplayName(added));
                Button("Focus new component",()=>navigate(added));
            }
            if(!string.IsNullOrEmpty(context.Message))Text(context.Message,"quantum-empty");
            var search=new WheelSearchField{name="componentCatalogSearch",tooltip="Search installed type, namespace, or category"};search.AddToClassList("catalog-search");root.Add(search);
            var categories=new List<string>{"All","Audio","Scripts","Rendering","Physics","UI","Animation","Networking / Netcode","Other"};
            var category=new DropdownField("Category",categories,0);root.Add(category);
            var count=new Label();count.AddToClassList("research-line");root.Add(count);
            var results=new List<CatalogComponent>();
            var list=new ListView{fixedItemHeight=60,selectionType=SelectionType.None,name="componentCatalog"};
            list.AddToClassList("component-catalog");root.Add(list);
            list.makeItem=()=>{var button=new Button();button.clicked+=()=>{if(button.userData is CatalogComponent item)add(item.Type);};return button;};
            list.bindItem=(element,index)=>
            {
                var button=(Button)element;var item=results[index];button.userData=item;
                string blocked=ComponentCatalogService.CanAdd(target,item.Type);
                button.text=item.Name+"\n"+item.Category;
                string requirements=string.Join(", ",ComponentCatalogService.Requirements(item.Type).Select(t=>t.Name));
                button.tooltip=item.Type.FullName+(requirements.Length>0?"\nRequires: "+requirements:"")+(blocked!=null?"\n"+blocked:"");button.SetEnabled(blocked==null);
            };
            void Refresh()
            {
                var tokens=(search.value??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(ComponentDiscoveryService.Normalize).ToArray();
                results=ComponentCatalogService.All.Where(c=>(category.value=="All"||c.Category==category.value)&&tokens.All(t=>c.Search.Contains(t))).ToList();
                list.itemsSource=results;list.Rebuild();count.text=results.Count+" installed types · hover for requirements";
            }
            search.RegisterValueChangedCallback(_=>Refresh());category.RegisterValueChangedCallback(_=>Refresh());Refresh();
            Button("Close catalog",cancel);
        }
        public void Dispose(){root.Unbind();serialized?.Dispose();serialized=null;}
    }
}
