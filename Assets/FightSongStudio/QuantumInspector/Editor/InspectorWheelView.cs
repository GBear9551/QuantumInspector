using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    public sealed class InspectorWheelView : VisualElement, IDisposable
    {
        public readonly VisualElement Stage=new VisualElement(), Hub=new VisualElement();
        private readonly ComponentNodeView[] pool=new ComponentNodeView[8];
        private readonly Vector2[] idealCenters=new Vector2[8];
        public ComponentNodeView[] Nodes {get;private set;}
        public readonly WheelSearchField Search=new WheelSearchField();
        public readonly Button Back,Forward,ChildrenButton,AddComponentButton,RemoveComponentButton,ParentButton,InspectComponentButton,PingButton;
        private readonly OrbitalComponentInspector componentInspector;
        private Component focusedComponent;
        private bool inspectorRequested;
        private float inspectorProgress;
        private readonly float[] absorption=new float[8];
        public bool ComponentInspectorOpen=>inspectorRequested;
        public bool ComponentInspectorTransitioning=>inspectorProgress>0&&inspectorProgress<1;
        internal bool OrbitInputBlocked=>inspectorRequested||inspectorProgress>0;
        internal OrbitalComponentInspector ComponentInspector=>componentInspector;
        private readonly VisualElement componentActions=new VisualElement(),focusGhost=new VisualElement();
        private readonly Label ghostTitle=new Label(),ghostType=new Label();
        private readonly Image ghostIcon=new Image();
        private readonly HashSet<string> removable=new HashSet<string>();
        private bool removalMode,projectContext,flightActive;
        private string presentationKey,selectedProperty;
        private float arrival,arrivalVelocity,arrivalMultiplier=1,flightElapsed;
        private Vector2 flightStart;
        private readonly Label objectName=new Label(),subtitle=new Label(),count=new Label(),symbol=new Label("◈");
        private readonly InspectorWheelController controller;
        private InspectorWheelSettings settings;
        private List<OrbitalEntry> filtered=new List<OrbitalEntry>();
        private List<ResearchRelationship> links=new List<ResearchRelationship>();
        private UnityEngine.Object selected;
        private float clock,flash;
        private double motionTime;
        private float stageScale=1;
        private float displaySlotAngle,spacingVelocity,displayNodeScale,scaleVelocity;
        private float BaseDiameter=>QuantumInspectorTypography.WheelDiameter(settings.NodeSize,settings.OrbitalFontScale);
        private float HubDiameter=>234*Mathf.Clamp(settings.OrbitalFontScale,1,3)*Mathf.Max(1,Mathf.Clamp(settings.ButtonFontSize,10,24)/12f);
        private float appliedHubScale=-1,appliedHubButtonFont=-1;
        public float NodeDiameter=>BaseDiameter*displayNodeScale;
        private float MotionMargin=>settings.ReducedMotion?0:Mathf.Clamp01(settings.AnimationIntensity)*(13+Mathf.Clamp(settings.BobAmplitude,0,6)+Mathf.Clamp(settings.MicroDrift,0,3));
        public float Radius=>Mathf.Max(settings.OrbitRadius,HubDiameter*.5f+15+NodeDiameter*.535f+MotionMargin,
            (NodeDiameter*1.07f+2*MotionMargin+10)/(2*Mathf.Sin(Mathf.PI/controller.SlotCount)));
        public InspectorWheelView(InspectorWheelController controller,InspectorWheelSettings settings,Action<ComponentNodeView> clicked,
            Action<ComponentNodeView,ContextualMenuPopulateEvent> menu,Action<string> hover,Action<int> history,Action<string> search,Action toggleChildren=null,Action addComponent=null,Action removeComponent=null,Action parent=null,Action pingGameObject=null)
        {
            this.controller=controller;this.settings=settings;
            controller.SetCapacity(settings.NodeCapacity);displaySlotAngle=(float)controller.SlotAngle;displayNodeScale=settings.OrbitalNodeScale;
            AddToClassList("quantum-wheel");focusable=true;
            Stage.AddToClassList("quantum-stage");Add(Stage);
            Stage.generateVisualContent+=Paint;
            Hub.AddToClassList("quantum-hub");Stage.Add(Hub);
            symbol.AddToClassList("hub-symbol");objectName.AddToClassList("hub-name");subtitle.AddToClassList("hub-subtitle");count.AddToClassList("hub-count");
            Hub.Add(symbol);Hub.Add(objectName);Hub.Add(subtitle);
            ParentButton=new Button(()=>parent?.Invoke()){name="hubParent",text="↑ Parent"};ParentButton.AddToClassList("hub-parent");Hub.Add(ParentButton);
            Search.AddToClassList("quantum-search");Search.tooltip="Filter alias, class, category, script, metadata, or referenced names (when enabled)";
            Hub.Add(Search);Hub.Add(count);
            InspectComponentButton=new Button(()=>SetComponentInspector(true)){name="hubInspectComponent",text="Inspect component",tooltip="Gather the parameters into an editable component inspector"};
            InspectComponentButton.AddToClassList("hub-inspect-component");InspectComponentButton.AddToClassList("quantum-hidden");Hub.Add(InspectComponentButton);
            Search.RegisterCallback<ChangeEvent<string>>(e=>search(e.newValue));
            ChildrenButton=new Button(()=>toggleChildren?.Invoke()){name="childrenToggle",text="Show children"};ChildrenButton.AddToClassList("hub-switch");Hub.Add(ChildrenButton);
            componentActions.AddToClassList("hub-component-actions");Hub.Add(componentActions);
            AddComponentButton=new Button(()=>addComponent?.Invoke()){name="hubAddComponent",text="+ Add",tooltip="Add Component — browse installed components"};
            RemoveComponentButton=new Button(()=>removeComponent?.Invoke()){name="hubRemoveComponent",text="− Remove",tooltip="Remove Component — choose an exact instance on the wheel"};
            componentActions.Add(AddComponentButton);componentActions.Add(RemoveComponentButton);
            var navigation=new VisualElement();navigation.AddToClassList("hub-history");Hub.Add(navigation);
            Back=new Button(()=>history(-1)){text="←",tooltip="Back to previous object, component and search"};
            Forward=new Button(()=>history(1)){text="→",tooltip="Forward to next inspection location"};
            PingButton=new Button(()=>pingGameObject?.Invoke()){name="hubPingGameObject",text="Ping",tooltip="Highlight this GameObject in the Hierarchy without changing selection"};
            PingButton.AddToClassList("quantum-hidden");
            navigation.Add(Back);navigation.Add(PingButton);navigation.Add(Forward);
            for(int i=0;i<pool.Length;i++){pool[i]=new ComponentNodeView(clicked,menu,hover);Stage.Add(pool[i]);pool[i].AddToClassList("quantum-hidden");}
            focusGhost.AddToClassList("quantum-focus-ghost");focusGhost.AddToClassList("quantum-hidden");focusGhost.pickingMode=PickingMode.Ignore;
            ghostTitle.AddToClassList("node-name");ghostType.AddToClassList("node-meta");ghostIcon.AddToClassList("node-icon");
            ghostTitle.pickingMode=ghostType.pickingMode=ghostIcon.pickingMode=PickingMode.Ignore;
            focusGhost.Add(ghostIcon);focusGhost.Add(ghostTitle);focusGhost.Add(ghostType);Stage.Add(focusGhost);
            componentInspector=new OrbitalComponentInspector(history,()=>SetComponentInspector(false));Add(componentInspector);
            RegisterCallback<DetachFromPanelEvent>(e=>{if(e.target==this)Dispose();});
            Nodes=pool.Take(controller.SlotCount).ToArray();
            this.AddManipulator(new WheelRotationManipulator(this,controller,()=>this.settings,clicked));
            RegisterCallback<GeometryChangedEvent>(_=>Layout());
            RegisterCallback<KeyDownEvent>(e=>{
                if(OrbitInputBlocked)return;
                if(!this.settings.KeyboardNavigation || e.target is TextField || (e.target as VisualElement)?.GetFirstAncestorOfType<TextField>()!=null)return;
                if(e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.RightArrow)
                {controller.Rotate(e.keyCode==KeyCode.RightArrow?-1:1,this.settings.ReducedMotion);e.StopPropagation();}
            });
        }
        public void Configure(InspectorWheelSettings value)
        {
            settings=value;controller.SetCapacity(settings.NodeCapacity);
            if(Nodes.Length!=controller.SlotCount)Nodes=pool.Take(controller.SlotCount).ToArray();
            for(int i=controller.SlotCount;i<pool.Length;i++)pool[i].Bind(null,null,settings.NodeSize);
            if(settings.ReducedMotion||settings.AnimationIntensity==0){displaySlotAngle=(float)controller.SlotAngle;displayNodeScale=settings.OrbitalNodeScale;}
            Layout();RefreshNodes();
        }
        public void SetRelationships(List<ResearchRelationship> value) {links=value;Stage.MarkDirtyRepaint();}
        public void Present(WheelScope scope,List<OrbitalEntry> entries,int total,List<ResearchRelationship> relationships)
        {
            filtered=entries;projectContext=scope.Kind==WheelScopeKind.ProjectFolder;
            selected=projectContext?scope.Asset:scope.Component;selectedProperty=scope.SelectedProperty;links=relationships;
            string key=scope.Kind+"|"+(scope.Object!=null?scope.Object.GetEntityId().ToString():"")+"|"+
                (scope.IsComponentFocus&&scope.Component!=null?scope.Component.GetEntityId().ToString():"")+"|"+scope.PropertyPath+"|"+scope.FolderPath;
            if(key!=presentationKey)
            {
                ResetComponentInspector();presentationKey=key;EndFlight();
                flash=settings.ReducedMotion?0:settings.AnimationIntensity*settings.NavigationGlow*(projectContext?settings.ProjectSelectionIntensity:1);
                AnimateArrival(projectContext?settings.ProjectSelectionIntensity:1);
            }
            focusedComponent=scope.IsComponentFocus?scope.Component:null;
            InspectComponentButton.EnableInClassList("quantum-hidden",focusedComponent==null);
            objectName.text=scope.Title;objectName.tooltip=scope.Title;
            subtitle.text=scope.IsComponentFocus&&scope.Component!=null?scope.Component.GetType().Name:scope.Kind==WheelScopeKind.ProjectFolder?"PROJECT FOLDER":scope.Kind==WheelScopeKind.SceneRoots?"UNITY HIERARCHY":scope.Object!=null?"GameObject · "+scope.Object.tag:scope.EntryLabel;
            subtitle.tooltip=scope.IsComponentFocus&&scope.Component!=null?scope.Component.GetType().FullName:scope.Breadcrumb;
            symbol.text=scope.Kind==WheelScopeKind.ProjectFolder?"▱":"◇";
            count.text=entries.Count==0?(total==0?"EMPTY SCOPE":"NO MATCHES")+$" · 0 / {total}":$"{entries.Count} / {total} {scope.EntryLabel}";
            count.EnableInClassList("quantum-empty",entries.Count==0);
            bool children=scope.Kind==WheelScopeKind.Children;
            PingButton.EnableInClassList("quantum-hidden",scope.Object==null||(scope.Kind!=WheelScopeKind.Components&&!children));
            ChildrenButton.EnableInClassList("quantum-hidden",scope.Object==null||(!children&&scope.Object.transform.childCount==0)||(scope.Kind!=WheelScopeKind.Components&&scope.Kind!=WheelScopeKind.Children));
            ChildrenButton.text=children?"Show components":"Show children ("+(scope.Object!=null?scope.Object.transform.childCount:0)+")";
            componentActions.EnableInClassList("quantum-hidden",scope.Kind!=WheelScopeKind.Components);
            ParentButton.EnableInClassList("quantum-hidden",!scope.IsComponentFocus);
            ParentButton.text=scope.Kind==WheelScopeKind.ComponentFocus?"↑ "+(scope.Object!=null?scope.Object.name:"GameObject"):"↑ Parent parameter";
            ParentButton.tooltip=scope.Kind==WheelScopeKind.ComponentFocus?"Return to GameObject components":"Return to the enclosing parameter or component";
            RefreshNodes();
        }
        public void Tick(float dt)
        {
            if(!settings.ReducedMotion)clock+=dt*settings.AnimationIntensity;
            motionTime+=dt;
            flash=Mathf.Max(0,flash-dt*2);
            if(!OrbitInputBlocked)controller.Tick(dt,settings);
            TickTransitions(dt);
            if(settings.ReducedMotion||settings.AnimationIntensity==0){displaySlotAngle=(float)controller.SlotAngle;displayNodeScale=settings.OrbitalNodeScale;spacingVelocity=scaleVelocity=0;}
            else
            {
                float remaining=Mathf.Clamp(dt,0,.1f),strength=Mathf.Clamp(settings.SpringStrength,40,400),damping=2*Mathf.Sqrt(strength)*Mathf.Max(.8f,settings.SpringDamping);
                while(remaining>0)
                {
                    float step=Mathf.Min(remaining,1f/120);
                    OrbitalNodeMotion.Spring(ref displaySlotAngle,ref spacingVelocity,(float)controller.SlotAngle,strength,damping,step);
                    OrbitalNodeMotion.Spring(ref displayNodeScale,ref scaleVelocity,settings.OrbitalNodeScale,strength,damping,step);remaining-=step;
                }
                displaySlotAngle=Mathf.Clamp(displaySlotAngle,45,72);
                displayNodeScale=Mathf.Clamp(displayNodeScale,.85f,1);
                if(Mathf.Abs(displaySlotAngle-(float)controller.SlotAngle)<.001f&&Mathf.Abs(spacingVelocity)<.01f){displaySlotAngle=(float)controller.SlotAngle;spacingVelocity=0;}
                if(Mathf.Abs(displayNodeScale-settings.OrbitalNodeScale)<.00001f&&Mathf.Abs(scaleVelocity)<.0001f){displayNodeScale=settings.OrbitalNodeScale;scaleVelocity=0;}
            }
            TickComponentInspector(dt);Layout();RefreshNodes(dt);Stage.MarkDirtyRepaint();
        }
        private void Layout()
        {
            float hubScale=HubDiameter/234;
            Hub.style.left=Hub.style.top=320-HubDiameter*.5f;Hub.style.width=Hub.style.height=HubDiameter;
            Hub.style.borderTopLeftRadius=Hub.style.borderTopRightRadius=Hub.style.borderBottomLeftRadius=Hub.style.borderBottomRightRadius=HubDiameter*.5f;
            objectName.style.fontSize=19*Mathf.Clamp(settings.OrbitalFontScale,1,3);objectName.style.maxHeight=48*hubScale;
            subtitle.style.fontSize=11*Mathf.Clamp(settings.OrbitalFontScale,1,3);subtitle.style.maxWidth=HubDiameter-44;
            count.style.fontSize=12*Mathf.Clamp(settings.OrbitalFontScale,1,3);
            symbol.style.fontSize=14*Mathf.Clamp(settings.OrbitalFontScale,1,3);Search.style.width=178*hubScale;
            float fontScale=Mathf.Clamp(settings.OrbitalFontScale,1,3);
            if(appliedHubScale!=fontScale||appliedHubButtonFont!=settings.ButtonFontSize)
            {
                appliedHubScale=fontScale;appliedHubButtonFont=settings.ButtonFontSize;
                Hub.style.paddingLeft=Hub.style.paddingRight=Hub.style.paddingTop=Hub.style.paddingBottom=14*fontScale;
                foreach(var button in Hub.Query<Button>().ToList())
                {
                    if(button.ClassListContains("search-clear"))continue;
                    button.style.fontSize=settings.ButtonFontSize*fontScale;
                    button.style.minHeight=(settings.ButtonFontSize+10)*fontScale;
                    button.style.paddingTop=button.style.paddingBottom=2*fontScale;
                    button.style.paddingLeft=button.style.paddingRight=7*fontScale;
                    button.style.marginLeft=button.style.marginRight=button.style.marginTop=button.style.marginBottom=2*fontScale;
                    button.style.maxWidth=174*hubScale;
                }
                Search.SetScale(fontScale);
            }
            float extent=Mathf.Max(620,2*(Radius+NodeDiameter*.535f+MotionMargin+12));
            float scale=Mathf.Clamp(Mathf.Min(contentRect.width,contentRect.height)/extent,.01f,2);
            stageScale=scale;
            // Dynamic transforms position the orbital stage; appearance remains in USS.
            Stage.style.scale=new Scale(new Vector3(scale,scale,1));
            Stage.style.translate=new Translate((contentRect.width-640*scale)*.5f,(contentRect.height-640*scale)*.5f);
            componentInspector.Present(inspectorProgress,contentRect.size,HubDiameter*stageScale,inspectorRequested);
        }
        private void RefreshNodes(float dt=0)
        {
            float diameter=BaseDiameter;
            float radius=Radius;
            float separation=NodeDiameter*1.07f+MotionMargin*2+8;
            for(int i=0;i<Nodes.Length;i++)
            {
                int index=controller.IndexForSlot(i);
                var entry=index>=0&&index<filtered.Count?filtered[index]:null;
                float angle=(float)(i*displaySlotAngle+controller.Position*controller.SlotAngle-90)*Mathf.Deg2Rad;
                Vector2 radial=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                idealCenters[i]=new Vector2(320,320)+radial*radius;
                for(int j=0;j<i;j++)
                    if(Nodes[j].Entry!=null&&Vector2.Distance(idealCenters[i],idealCenters[j])<separation){entry=null;break;}
                var node=Nodes[i];node.Bind(entry,selected,settings.NodeSize,selectedProperty,settings.OrbitalFontScale);
                node.EnableInClassList("removable",removalMode&&entry!=null&&removable.Contains(entry.Key));
                node.EnableInClassList("removal-blocked",removalMode&&entry!=null&&!removable.Contains(entry.Key));
                node.Motion.Tick(dt,controller.Position,entry?.Key,i,radial,node.Hovered||node.Focused,node.IsSelected,motionTime,settings,
                    projectContext?settings.ProjectSelectionIntensity:1,1-Mathf.Clamp01(Mathf.Abs(arrival)*4));
                node.Center=new Vector2(320,320)+radial*(radius-arrival*Mathf.Clamp(settings.NodeArrivalDistance,0,100)*arrivalMultiplier*settings.AnimationIntensity)+node.Motion.Offset;
                node.style.opacity=flightActive&&flightElapsed<settings.FocusTransitionDuration*.45f?0:Mathf.Clamp01(1-Mathf.Max(0,arrival)*.8f);
                float merge=Absorption(inspectorProgress,i,Nodes.Length);absorption[i]=merge;
                var center=new Vector2(320,320);var offset=node.Center-center;
                node.Center=center+offset*(1-merge)+new Vector2(-offset.y,offset.x)*Mathf.Sin(merge*Mathf.PI)*.18f;
                node.style.opacity=node.style.opacity.value*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1,merge)));
                node.SetEnabled(!OrbitInputBlocked);
                node.style.translate=new Translate(node.Center.x-diameter*.5f,node.Center.y-diameter*.5f);
                node.style.scale=new Scale(Vector3.one*(node.Motion.Scale*displayNodeScale*Mathf.Lerp(1,.12f,merge)));
            }
        }
        internal static float Absorption(float progress,int slot,int slots)
        {
            float delay=slots<=1?0:slot*.14f/(slots-1);
            float t=Mathf.InverseLerp(delay,.66f+delay,progress);
            return t*t*t*(t*(t*6-15)+10);
        }
        public void SetComponentInspector(bool open)
        {
            if(open&&focusedComponent==null)return;
            inspectorRequested=open;EndFlight();arrival=arrivalVelocity=0;
            controller.RestorePosition(controller.Position);
            componentInspector.SetComponent(focusedComponent);
            if(open)componentInspector.BringToFront();
            TickComponentInspector(0);Layout();RefreshNodes();
            if(!open)InspectComponentButton.Focus();
        }
        public void Dispose(){ResetComponentInspector();}
        private void ResetComponentInspector()
        {
            inspectorRequested=false;inspectorProgress=0;
            componentInspector.Dispose();Hub.SetEnabled(true);Hub.style.opacity=1;
            componentInspector.Present(0,contentRect.size,HubDiameter*stageScale,false);
        }
        private void TickComponentInspector(float dt)
        {
            if(OrbitInputBlocked&&focusedComponent==null)ResetComponentInspector();
            float previous=inspectorProgress;
            float target=inspectorRequested?1:0;
            if(settings.ReducedMotion||settings.AnimationIntensity<=0||!settings.FocusTransitions)inspectorProgress=target;
            else inspectorProgress=Mathf.MoveTowards(inspectorProgress,target,Mathf.Max(0,dt)/Mathf.Clamp(settings.FocusTransitionDuration*1.6f,.55f,1.2f));
            if(OrbitInputBlocked)
            {
                Hub.style.opacity=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.45f,inspectorProgress));
                Hub.SetEnabled(false);
            }
            else{Hub.style.opacity=1;Hub.SetEnabled(true);if(previous>0)InspectComponentButton.Focus();}
            componentInspector.Back.SetEnabled(Back.enabledSelf);componentInspector.Forward.SetEnabled(Forward.enabledSelf);
            componentInspector.RefreshEditor();
        }
        public void SetRemovalMode(bool enabled)
        {
            removalMode=enabled;removable.Clear();
            if(enabled)foreach(var entry in filtered)
                if(entry.Kind==OrbitalEntryKind.Component&&entry.Component!=null&&ComponentCatalogService.CanRemove(entry.Component)==null)removable.Add(entry.Key);
            RemoveComponentButton.EnableInClassList("active-tab",enabled);
        }
        public void AnimateArrival(float multiplier=1)
        {
            arrivalMultiplier=Mathf.Clamp01(multiplier);arrivalVelocity=0;
            arrival=settings.ReducedMotion||settings.AnimationIntensity<=0||!settings.FocusTransitions?0:1;
        }
        public void AnimateFocus(OrbitalEntry entry,Vector2 origin)
        {
            if(entry==null||settings.ReducedMotion||settings.AnimationIntensity<=0||!settings.FocusTransitions)return;
            flightStart=origin;flightElapsed=0;flightActive=true;AnimateArrival();
            ghostTitle.text=entry.Name;ghostType.text=entry.TypeName;ghostIcon.image=entry.Icon;
            ghostIcon.EnableInClassList("quantum-hidden",entry.Icon==null);
            focusGhost.RemoveFromClassList("quantum-hidden");TickTransitions(0);
        }
        private void EndFlight(){flightActive=false;focusGhost.AddToClassList("quantum-hidden");Hub.style.opacity=1;}
        private void TickTransitions(float dt)
        {
            if(settings.ReducedMotion||settings.AnimationIntensity<=0||!settings.FocusTransitions){arrival=arrivalVelocity=0;EndFlight();return;}
            float duration=Mathf.Clamp(settings.FocusTransitionDuration,.2f,1.2f);
            if(flightActive)
            {
                flightElapsed+=dt;float t=Mathf.Clamp01(flightElapsed/duration);
                float ease=t*t*(3-2*t);
                Vector2 point=Vector2.Lerp(flightStart,new Vector2(320,320),ease);
                focusGhost.style.translate=new Translate(point.x-56,point.y-56);
                focusGhost.style.scale=new Scale(Vector3.one*Mathf.Lerp(displayNodeScale,1.9f*HubDiameter/234,ease));
                focusGhost.style.opacity=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,t));
                Hub.style.opacity=.4f+.6f*ease;
                if(t>=1)EndFlight();
            }
            if(flightActive&&flightElapsed<duration*.45f)return;
            float remaining=Mathf.Clamp(dt,0,.1f),strength=Mathf.Clamp(settings.SpringStrength,40,400),damping=2*Mathf.Sqrt(strength)*Mathf.Clamp(settings.SpringDamping,.45f,1.5f);
            while(remaining>0)
            {
                float step=Mathf.Min(remaining,1f/120);
                OrbitalNodeMotion.Spring(ref arrival,ref arrivalVelocity,0,strength,damping,step);remaining-=step;
            }
            arrival=Mathf.Clamp(arrival,-.12f,1);
            if(Mathf.Abs(arrival)<.0001f&&Mathf.Abs(arrivalVelocity)<.001f)arrival=arrivalVelocity=0;
        }
        private Color Alpha(Color color,float alpha) {color.a=alpha*(1-inspectorProgress);return color;}
        private static void Line(Painter2D p,Vector2 a,Vector2 b,Color color,float width=1)
        {p.strokeColor=color;p.lineWidth=width;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();}
        private static void Ring(Painter2D p,Vector2 center,float radius,Color color,float width=1,float start=0,float end=360)
        {p.strokeColor=color;p.lineWidth=width;p.BeginPath();p.Arc(center,radius,Angle.Degrees(start),Angle.Degrees(end));p.Stroke();}
        private void Paint(MeshGenerationContext context)
        {
            if(contentRect.width<1||settings==null)return;
            var p=context.painter2D;Vector2 center=new Vector2(320,320);
            var cyan=settings.OrbitColor;var violet=settings.AccentColor;
            float radius=Radius;
            if(settings.ShowGrid)
                for(int x=32;x<640;x+=32){Line(p,new Vector2(x,16),new Vector2(x,624),Alpha(cyan,.035f));Line(p,new Vector2(16,x),new Vector2(624,x),Alpha(cyan,.035f));}
            Ring(p,center,radius,Alpha(cyan,.035f*settings.GlowStrength),12);
            Ring(p,center,radius,Alpha(cyan,.32f));
            Ring(p,center,radius+12,Alpha(cyan,.08f));
            Ring(p,center,HubDiameter*.5f+20,Alpha(violet,.18f),1);
            Ring(p,center,HubDiameter*.5f+7,Alpha(cyan,.4f+flash*.4f),2,clock*12,clock*12+110);
            Ring(p,center,HubDiameter*.5f+13,Alpha(violet,.5f),2,180-clock*8,245-clock*8);
            for(int i=0;i<60;i++)
            {
                float a=i*6*Mathf.Deg2Rad;Vector2 dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Line(p,center+dir*(radius+23),center+dir*(radius+(i%5==0?32:27)),Alpha(cyan,i%5==0?.4f:.14f));
            }
            foreach(var node in Nodes)
            {
                if(node.Entry==null)continue;
                var tint=node.Color;
                Vector2 dir=(node.Center-center).normalized;
                Vector2 start=center+dir*(HubDiameter*.5f+17),end=node.Center-dir*(NodeDiameter*.5f*node.Motion.Scale);
                Line(p,start,end,Alpha(tint,.06f*settings.GlowStrength),7);
                Line(p,start,end,Alpha(tint,node.IsSelected?.9f:node.Hovered||node.Focused?.7f:.35f),node.IsSelected?2:1);
                if(node.Motion.Ripple>0)
                {
                    Vector2 ripple=Vector2.Lerp(end,start,1-node.Motion.Ripple);
                    Ring(p,ripple,2+(1-node.Motion.Ripple)*4,Alpha(tint,node.Motion.Ripple),2);
                }
                if(settings.AnimationIntensity>0&&!settings.ReducedMotion)
                {
                    Vector2 photon=Vector2.Lerp(start,end,Mathf.Repeat(clock*.3f+node.Entry.Ordinal*.19f,1));
                    Ring(p,photon,2,Alpha(tint,.85f),2);
                }
                float nodeScale=Mathf.Lerp(1,.12f,absorption[Array.IndexOf(Nodes,node)]);
                float size=(NodeDiameter*.5f+2)*nodeScale;
                float pulse=settings.ReducedMotion?1:1+Mathf.Sin(clock*2+node.Entry.Ordinal)*.012f*settings.AnimationIntensity;
                float glow=node.IsSelected?.32f:node.Hovered||node.Focused?.24f:.09f;
                Ring(p,node.Center,size*pulse*node.Motion.Scale,Alpha(tint,glow*settings.GlowStrength),6);
                Ring(p,node.Center,(NodeDiameter*.5f+1)*node.Motion.Scale*nodeScale,Alpha(tint,.85f),1.5f);
            }
            if(settings.ShowRelationshipOverlay)
                foreach(var edge in links)
                {
                    if((settings.Relationships&edge.Kind)==0)continue;
                    ComponentNodeView from=null,to=null;
                    foreach(var node in Nodes)
                    {if(node.Entry?.Component!=null&&node.Entry.Component==edge.Source)from=node;if(node.Entry?.Component!=null&&node.Entry.Component==edge.Target)to=node;}
                    if(from==null||to==null||from==to)continue;
                    var tint=ResearchLayoutManager.LinkColor(edge.Kind);
                    p.strokeColor=Alpha(tint,.45f);p.lineWidth=1.3f;p.BeginPath();p.MoveTo(from.Center);
                    p.QuadraticCurveTo(center,to.Center);p.Stroke();
                    Vector2 tip=Vector2.Lerp(center,to.Center,.73f),d=(to.Center-center).normalized;
                    Line(p,tip,tip-d*9+new Vector2(-d.y,d.x)*4,tint);Line(p,tip,tip-d*9-new Vector2(-d.y,d.x)*4,tint);
                }
            if(settings.ShowParticles&&!settings.ReducedMotion&&settings.AnimationIntensity>0)
                for(int i=0;i<16;i++)
                {
                    float a=(i*137.5f+clock*(i%2==0?2:-2))*Mathf.Deg2Rad;
                    Vector2 point=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(150+i*8%140);
                    Ring(p,point,1,Alpha(i%2==0?cyan:violet,.25f),1.5f);
                }
        }
    }

    internal sealed class WheelRotationManipulator : PointerManipulator
    {
        private readonly InspectorWheelView view;
        private readonly InspectorWheelController controller;
        private readonly Func<InspectorWheelSettings> settings;
        private readonly Action<ComponentNodeView> clicked;
        private int pointer=-1;
        private float previousAngle;
        private Vector2 initial;
        private bool moved;
        private double lastTime;
        private ComponentNodeView pressed;
        public WheelRotationManipulator(InspectorWheelView view,InspectorWheelController controller,Func<InspectorWheelSettings> settings,Action<ComponentNodeView> clicked)
        {this.view=view;this.controller=controller;this.settings=settings;this.clicked=clicked;}
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(Down);
            target.RegisterCallback<PointerMoveEvent>(Move);
            target.RegisterCallback<PointerUpEvent>(Up);
            target.RegisterCallback<PointerCaptureOutEvent>(Lost);
            target.RegisterCallback<WheelEvent>(Wheel);
        }
        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(Down);target.UnregisterCallback<PointerMoveEvent>(Move);
            target.UnregisterCallback<PointerUpEvent>(Up);target.UnregisterCallback<PointerCaptureOutEvent>(Lost);target.UnregisterCallback<WheelEvent>(Wheel);
        }
        private float Angle(Vector2 position) {var p=view.Stage.WorldToLocal(position)-new Vector2(320,320);return Mathf.Atan2(p.y,p.x)*Mathf.Rad2Deg;}
        private void Down(PointerDownEvent e)
        {
            if(view.OrbitInputBlocked||e.button!=0||pointer>=0||view.Hub.worldBound.Contains(e.position))return;
            pointer=e.pointerId;initial=e.position;previousAngle=Angle(e.position);lastTime=EditorApplication.timeSinceStartup;moved=false;
            pressed=e.target as ComponentNodeView ?? (e.target as VisualElement)?.GetFirstAncestorOfType<ComponentNodeView>();
            controller.BeginDrag();target.CapturePointer(pointer);target.Focus();e.StopPropagation();
        }
        private void Move(PointerMoveEvent e)
        {
            if(e.pointerId!=pointer)return;
            if(view.OrbitInputBlocked){Release(false);return;}
            if((e.pressedButtons&1)==0){Release(false);return;}
            if(Vector2.Distance(initial,e.position)>4)moved=true;
            float angle=Angle(e.position);double now=EditorApplication.timeSinceStartup;
            if(moved)controller.Drag(Mathf.DeltaAngle(previousAngle,angle)/controller.SlotAngle,now-lastTime);
            previousAngle=angle;lastTime=now;e.StopPropagation();
        }
        private void Up(PointerUpEvent e)
        {if(e.pointerId!=pointer)return;var item=pressed;bool click=!moved;Release(!click);if(click&&item!=null)clicked(item);e.StopPropagation();}
        private void Lost(PointerCaptureOutEvent e) {if(e.pointerId==pointer)Release(false);}
        private void Release(bool momentum)
        {
            int id=pointer;pointer=-1;
            controller.EndDrag(momentum&&settings().Momentum&&!settings().ReducedMotion,settings().SnapToSlots);
            if(id>=0&&target.HasPointerCapture(id))target.ReleasePointer(id);
            pressed=null;
        }
        private void Wheel(WheelEvent e)
        {if(view.OrbitInputBlocked)return;controller.Rotate(-Math.Sign(e.delta.y)*settings().ScrollSensitivity,settings().ReducedMotion);e.StopPropagation();}
    }
}
