using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class ComponentNodeView : VisualElement
    {
        public OrbitalEntry Entry {get;private set;}
        public Vector2 Center {get;set;}
        public bool IsSelected {get;private set;}
        public bool Hovered {get;private set;}
        public bool Focused {get;private set;}
        public readonly OrbitalNodeMotion Motion=new OrbitalNodeMotion();
        public Color Color=>Entry?.CustomColor??Tint(Entry?.Category??ComponentCategory.System);
        private readonly Image icon=new Image();
        private readonly Label title=new Label(), glyph=new Label(), metadata=new Label();
        private string categoryClass;
        public ComponentNodeView(Action<ComponentNodeView> clicked,Action<ComponentNodeView,ContextualMenuPopulateEvent> menu,Action<string> hover)
        {
            AddToClassList("quantum-node"); focusable=true; usageHints=UsageHints.DynamicTransform;
            glyph.AddToClassList("node-glyph");title.AddToClassList("node-name");metadata.AddToClassList("node-meta");
            glyph.pickingMode=title.pickingMode=metadata.pickingMode=PickingMode.Ignore;
            icon.AddToClassList("node-icon");icon.pickingMode=PickingMode.Ignore;
            Add(icon);Add(glyph);Add(title);Add(metadata);
            this.AddManipulator(new ContextualMenuManipulator(e=>menu(this,e)));
            RegisterCallback<PointerEnterEvent>(_=>{Hovered=true;hover(Entry==null?"":Entry.Name+" · "+Entry.TypeName+" · "+(Entry.Description??"right-click for research actions"));});
            RegisterCallback<PointerLeaveEvent>(_=>{Hovered=false;hover("");});
            RegisterCallback<FocusInEvent>(_=>Focused=true);
            RegisterCallback<FocusOutEvent>(_=>Focused=false);
            RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.Space){clicked(this);e.StopPropagation();}});
        }
        public void Bind(OrbitalEntry entry,UnityEngine.Object selected,WheelNodeSize size,string selectedPath=null,float fontScale=1)
        {
            EnableInClassList("quantum-hidden",entry==null);
            EnableInClassList("compact",size==WheelNodeSize.Compact);EnableInClassList("large",size==WheelNodeSize.Large);
            IsSelected=entry!=null&&(entry.Kind==OrbitalEntryKind.Parameter?entry.PropertyPath==selectedPath:selected!=null&&(entry.Target==selected||(!string.IsNullOrEmpty(entry.AssetPath)&&entry.AssetPath==UnityEditor.AssetDatabase.GetAssetPath(selected))));
            EnableInClassList("selected",IsSelected);
            fontScale=Mathf.Clamp(fontScale,1,3);
            float diameter=QuantumInspectorTypography.WheelDiameter(size,fontScale);
            style.width=style.height=diameter;
            style.borderTopLeftRadius=style.borderTopRightRadius=style.borderBottomLeftRadius=style.borderBottomRightRadius=diameter*.5f;
            title.style.fontSize=13*fontScale;title.style.maxHeight=34*fontScale;
            metadata.style.fontSize=10*fontScale;metadata.style.marginTop=4*fontScale;
            glyph.style.fontSize=22*fontScale;icon.style.width=icon.style.height=24*fontScale;
            style.paddingLeft=style.paddingRight=style.paddingTop=style.paddingBottom=10*fontScale;
            if(Entry==entry)return;
            Entry=entry;
            if(categoryClass!=null)RemoveFromClassList(categoryClass);
            if(entry==null){Hovered=false;Focused=false;return;}
            categoryClass="category-"+entry.Category.ToString().ToLowerInvariant();AddToClassList(categoryClass);
            title.text=entry.Name;metadata.text=entry.TypeName;
            icon.image=entry.Icon;
            icon.EnableInClassList("quantum-hidden",entry.Icon==null);
            glyph.EnableInClassList("quantum-hidden",entry.Icon!=null);
            glyph.text=entry.Glyph??(entry.Category switch {
                ComponentCategory.Transform=>"XYZ",ComponentCategory.Rendering=>"◈",ComponentCategory.Physics=>"◇",
                ComponentCategory.Audio=>"♪",ComponentCategory.Script=>"</>",ComponentCategory.Missing=>"!",_=>"◎"});
            tooltip=entry.Kind==OrbitalEntryKind.Component&&entry.Component!=null ? entry.Name+"\n"+entry.Component.GetType().FullName+"\nComponent "+(entry.Ordinal+1)+" on "+entry.Component.gameObject.name : entry.Kind==OrbitalEntryKind.Component?"Missing script reference; use Unity's Inspector to repair it.":entry.Name+"\n"+entry.TypeName+"\n"+entry.Description;
        }
        public static Color Tint(ComponentCategory category) => category switch {
            ComponentCategory.Transform=>new Color(.3f,.95f,.9f),ComponentCategory.Rendering=>new Color(.25f,.55f,1),
            ComponentCategory.Physics=>new Color(.5f,.94f,.55f),ComponentCategory.Audio=>new Color(1,.7f,.3f),
            ComponentCategory.Script=>new Color(.72f,.45f,1),ComponentCategory.Missing=>new Color(1,.35f,.45f),
            _=>new Color(.6f,.7f,.9f)};
    }
}
