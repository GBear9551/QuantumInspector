using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.InspectorTool
{
    [Serializable]
    public sealed class QuantumNebulaTokens
    {
        public Color Space=Hex(0x070B18),Void=Hex(0x030610),Indigo=Hex(0x152C58),Violet=Hex(0x6941A8),Teal=Hex(0x235F72);
        public Color Text=Hex(0xE6EFFF),Supporting=Hex(0x9BABC5),Surface=Hex(0x0C1327),SurfaceLight=Hex(0x152034);
        public Color Scripts=Hex(0x68F6C7),Rendering=Hex(0x8EADFF),Physics=Hex(0xFFC787),Audio=Hex(0xCE98FF),Neutral=Hex(0xB3BFD5);
        internal static Color Hex(int value)=>new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f);
        public Color Family(string key)=>key=="Scripting"?Scripts:key=="Rendering"?Rendering:key=="Physics"?Physics:key=="Audio"?Audio:Neutral;
        internal int TextureKey {get{unchecked{return ((((Space.GetHashCode()*31+Void.GetHashCode())*31+Indigo.GetHashCode())*31+Violet.GetHashCode())*31+Teal.GetHashCode());}}}
    }

    // Personal choices never dirty the shared settings preset. Preset token editing remains explicit.
    internal static class QuantumGraphPreferences
    {
        private const string Prefix="FightSong.QuantumInspector.Cosmic.";
        public static void Load(InspectorWheelSettings settings)
        {
            settings.GraphInvestigationLock=EditorPrefs.GetBool(Prefix+"Lock",false);
            settings.GraphMotion=(QuantumGraphMotionMode)Mathf.Clamp(EditorPrefs.GetInt(Prefix+"Motion",0),0,2);
            settings.GraphPalette=(QuantumGraphPalette)Mathf.Clamp(EditorPrefs.GetInt(Prefix+"Palette",3),0,3);
            settings.GraphQuality=(QuantumGraphQuality)Mathf.Clamp(EditorPrefs.GetInt(Prefix+"Quality",1),0,2);
            settings.GraphAmbient=EditorPrefs.GetBool(Prefix+"Ambient",false);
            settings.GraphAmbientWhileLocked=EditorPrefs.GetBool(Prefix+"LockedAmbient",false);
            if(EditorPrefs.HasKey(Prefix+"DotSize"))settings.GraphDotSize=EditorPrefs.GetFloat(Prefix+"DotSize",2);
            if(EditorPrefs.HasKey(Prefix+"AuxiliaryUI"))settings.GraphShowAuxiliary=EditorPrefs.GetBool(Prefix+"AuxiliaryUI",false);
        }
        public static void Save(InspectorWheelSettings settings)
        {
            EditorPrefs.SetBool(Prefix+"Lock",settings.GraphInvestigationLock);EditorPrefs.SetInt(Prefix+"Motion",(int)settings.GraphMotion);
            EditorPrefs.SetInt(Prefix+"Palette",(int)settings.GraphPalette);EditorPrefs.SetInt(Prefix+"Quality",(int)settings.GraphQuality);
            EditorPrefs.SetBool(Prefix+"Ambient",settings.GraphAmbient);EditorPrefs.SetBool(Prefix+"LockedAmbient",settings.GraphAmbientWhileLocked);
            EditorPrefs.SetFloat(Prefix+"DotSize",settings.GraphDotSize);
            EditorPrefs.SetBool(Prefix+"AuxiliaryUI",settings.GraphShowAuxiliary);
        }
        public static void RestoreDefaults(InspectorWheelSettings settings)
        {
            settings.GraphShowAuxiliary=false;settings.GraphInvestigationLock=false;settings.GraphMotion=QuantumGraphMotionMode.Full;
            settings.GraphPalette=QuantumGraphPalette.CosmicNebula;settings.GraphQuality=QuantumGraphQuality.Balanced;
            settings.GraphAmbient=settings.GraphAmbientWhileLocked=false;settings.GraphDotSize=2;Save(settings);
        }
    }

    // One bounded texture per viewport, generated outside generateVisualContent. No Unity random state.
    internal sealed class QuantumNebulaBackdrop:VisualElement,IDisposable
    {
        private Texture2D texture;
        private int key;
        private bool active;
        private double lastFrame;
        public double GenerationMilliseconds {get;private set;}
        public QuantumNebulaBackdrop(){pickingMode=PickingMode.Ignore;AddToClassList("graph-nebula-backdrop");}
        public void Configure(InspectorWheelSettings settings)
        {
            active=settings.GraphPalette==QuantumGraphPalette.CosmicNebula;
            style.display=active?DisplayStyle.Flex:DisplayStyle.None;
            int next=settings.Nebula.TextureKey^(int)settings.GraphQuality;
            if(!active||texture!=null&&next==key)return;
            Dispose();key=next;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            int width=settings.GraphQuality==QuantumGraphQuality.Low?128:settings.GraphQuality==QuantumGraphQuality.Cinematic?384:256,height=width/2;
            var pixels=new Color32[width*height];var t=settings.Nebula;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=x/(float)(width-1),v=y/(float)(height-1);
                float warp=Mathf.PerlinNoise(u*3.5f+13.7f,v*3.5f+8.3f);
                float grain=.65f+.35f*Mathf.PerlinNoise(u*11+29,v*11+71);
                float teal=Mathf.Exp(-((u-.22f)*(u-.22f)*12+(v-.54f+.11f*warp)*(v-.54f+.11f*warp)*11));
                float violet=Mathf.Exp(-((u-.81f)*(u-.81f)*18+(v-.39f-.12f*warp)*(v-.39f-.12f*warp)*12));
                float middle=1-.55f*Mathf.Exp(-((u-.5f)*(u-.5f)*40+(v-.5f)*(v-.5f)*10));
                Color color=Color.Lerp(t.Space,t.Indigo,.17f*warp);
                color=Color.Lerp(color,t.Teal,teal*warp*grain*.38f*middle);
                color=Color.Lerp(color,t.Violet,violet*warp*grain*.3f*middle);
                float vignette=Mathf.Clamp01((new Vector2(u-.5f,v-.5f).magnitude-.28f)*1.2f);
                pixels[y*width+x]=Color.Lerp(color,t.Void,vignette*.65f);
            }
            var random=new System.Random(7919);int stars=settings.GraphQuality==QuantumGraphQuality.Cinematic?100:55;
            for(int i=0;i<stars;i++)
            {int index=random.Next(pixels.Length);pixels[index]=Color.Lerp((Color)pixels[index],t.Supporting,.15f+(float)random.NextDouble()*.24f);}
            texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Quantum Inspector cached nebula",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);style.backgroundImage=new StyleBackground(texture);
            GenerationMilliseconds=watch.Elapsed.TotalMilliseconds;
        }
        public void Tick(double now,bool ambient)
        {
            if(!active)return;
            if(!ambient){if(lastFrame!=0){style.translate=new Translate(0,0);lastFrame=0;}return;}
            if(now-lastFrame<1.0/20)return;lastFrame=now;
            style.translate=new Translate((float)Math.Sin(now*Math.PI/16)*4,(float)Math.Cos(now*Math.PI/19)*3);
        }
        public void Dispose(){if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);texture=null;}
    }

    internal static class QuantumNebulaDrawing
    {
        internal static Color Alpha(Color color,float alpha){color.a=alpha;return color;}
        internal static Color Tint(QuantumGraphNode node,QuantumGraphDocument graph,InspectorWheelSettings settings)
        {
            if(settings.GraphPalette!=QuantumGraphPalette.CosmicNebula)return node.Color;
            if(node.Kind==QuantumGraphNodeKind.Missing)return QuantumNebulaTokens.Hex(0xFF939E);
            if(node.Kind==QuantumGraphNodeKind.Cluster)return settings.Nebula.Family(node.FamilyKey);
            if(node.ClusterId!=null&&graph.Nodes.TryGetValue(node.ClusterId,out var family))
                return node.Color==family.Color?settings.Nebula.Family(family.FamilyKey):node.Color;
            return settings.Nebula.Rendering;
        }
        internal static void Enclosure(Painter2D p,Vector2 center,float radius,Color tint,uint seed,bool elaborate,float opacity=1)
        {
            p.lineWidth=1;p.strokeColor=Alpha(tint,.21f*opacity);p.fillColor=Alpha(tint,.032f*opacity);
            p.BeginPath();
            const int steps=48;
            for(int i=0;i<=steps;i++)
            {
                float angle=i*Mathf.PI*2/steps;
                // Expand beyond the solver's measured circle, never cut into member/label bounds.
                float r=radius+(elaborate?4+3*Mathf.Sin(angle*3+(seed%71)):0);
                var point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;
                if(i==0)p.MoveTo(point);else p.LineTo(point);
            }
            p.ClosePath();p.Fill();p.Stroke();
        }
        internal static void Owner(Painter2D p,Vector2 center,Vector2 dimensions,Color tint,bool selected,bool glow)
        {
            float radius=dimensions.magnitude*.5f+12;
            if(glow)for(int i=3;i>0;i--){p.strokeColor=Alpha(tint,(selected?.04f:.018f)*i);p.lineWidth=8;p.BeginPath();p.Arc(center,radius+i*4,Angle.Degrees(0),Angle.Degrees(360));p.Stroke();}
            p.lineWidth=1;p.strokeColor=Alpha(tint,selected?.75f:.38f);
            for(int i=0;i<3;i++){p.BeginPath();p.Arc(center,radius+(i%2)*5,Angle.Degrees(i*120+12),Angle.Degrees(i*120+92));p.Stroke();}
        }
    }
}
