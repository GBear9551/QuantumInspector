using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    public static class QuantumGraphEdgeVerification
    {
        private struct StrokeMetrics { public int Vertices; public float Height; }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static StrokeMetrics Render(QuantumGraphDocument graph,InspectorWheelSettings settings,QuantumGraphEdge[] edges,float zoom=1,bool selected=false)
        {
            var image=ScriptableObject.CreateInstance<VectorImage>();
            try
            {
                using(var painter=new Painter2D())
                {
                    QuantumGraphEdgeRenderer.Draw(painter,graph,new QuantumGraphViewportController{Zoom=zoom},new Vector2(800,500),settings,
                        edges,new Dictionary<string,Vector2>(),selected?new HashSet<string>{"root"}:new HashSet<string>(),0);
                    painter.SaveToVectorImage(image);
                }
                using(var serialized=new SerializedObject(image))
                {
                    var vertices=serialized.FindProperty("vertices");float low=float.PositiveInfinity,high=float.NegativeInfinity;
                    for(int i=0;i<vertices.arraySize;i++)
                    {
                        float y=vertices.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value.y;
                        low=Mathf.Min(low,y);high=Mathf.Max(high,y);
                    }
                    return new StrokeMetrics{Vertices=vertices.arraySize,Height=vertices.arraySize>0?high-low:0};
                }
            }
            finally{Object.DestroyImmediate(image);}
        }
        public static object Run()
        {
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            var restored=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            try
            {
                settings.ReducedMotion=true;
                var graph=new QuantumGraphDocument{RootId="root"};
                graph.Upsert(new QuantumGraphNode{Id="root",Position=new Vector2(-150,0),Expanded=true});
                graph.Upsert(new QuantumGraphNode{Id="child",ParentId="root",Position=new Vector2(150,0),Expanded=true});
                var solid=new QuantumGraphEdge{From="root",To="child",Color=Color.cyan};
                var overlay=new QuantumGraphEdge{From="root",To="child",Color=Color.magenta,Overlay=true};
                var solidOnly=Render(graph,settings,new[]{solid});
                var dashedOnly=Render(graph,settings,new[]{overlay});
                var mixed=Render(graph,settings,new[]{overlay,solid});
                Check(solidOnly.Vertices>0,"Solid graph links generated no geometry.");
                Check(dashedOnly.Vertices>solidOnly.Vertices,"Reference links lost their dash geometry.");
                Check(mixed.Vertices==solidOnly.Vertices+dashedOnly.Vertices,"Reference links suppressed solid links in a mixed graph.");
                Check(Render(graph,settings,new[]{solid,overlay}).Vertices==mixed.Vertices,"Edge insertion order changed rendering.");
                var contained=new QuantumGraphEdge{From="root",To="child",Color=Color.cyan,Relation=QuantumGraphRelation.Contains};
                var reference=new QuantumGraphEdge{From="root",To="child",Color=Color.magenta,Relation=QuantumGraphRelation.SerializedReference};
                Check(Render(graph,settings,new[]{contained}).Vertices>solidOnly.Vertices,"Containment lost its directional marker.");
                Check(Render(graph,settings,new[]{reference}).Vertices==dashedOnly.Vertices,"Serialized reference style depended on its discovery route instead of its type.");
                settings.GraphLinkThickness=1;var thin=Render(graph,settings,new[]{solid});
                settings.GraphLinkThickness=5;var thick=Render(graph,settings,new[]{solid});
                Check(thick.Height>thin.Height+3,"Graph Link Thickness did not change rendered stroke width.");
                Check(Render(graph,settings,new[]{solid},1,true).Height>thick.Height+1,"Selected links lost their thickness emphasis.");
                float[] zooms={.15f,.3f,1,2};
                foreach(float zoom in zooms)
                {
                    settings.GraphLinkThickness=1;var zoomThin=Render(graph,settings,new[]{solid},zoom);
                    settings.GraphLinkThickness=5;var zoomThick=Render(graph,settings,new[]{solid},zoom);
                    Check(zoomThin.Vertices>0&&Mathf.Abs((zoomThick.Height-zoomThin.Height)-(thick.Height-thin.Height))<.2f,
                        "Zoom changed the configured screen-space link thickness.");
                }
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(settings),restored);
                Check(restored.GraphLinkThickness==5,"Link thickness did not survive serialization.");
                graph.Root.Position=new Vector2(-1000000,0);graph.Nodes["child"].Position=new Vector2(1000000,0);
                var distant=Render(graph,settings,new[]{reference});
                Check(distant.Vertices>0&&distant.Vertices<16000,"Distant reference paths exceeded the viewport geometry budget.");
                graph.Root.Expanded=false;
                Check(Render(graph,settings,new[]{solid,overlay}).Vertices==0,"Collapsed branches still drew links.");
                return new{passed=true,solidVertices=solidOnly.Vertices,dashedVertices=dashedOnly.Vertices,mixedVertices=mixed.Vertices,
                    thinHeight=thin.Height,thickHeight=thick.Height,zoomLevels=zooms.Length,selectionEmphasis=true,serializedThickness=restored.GraphLinkThickness,collapsedLinksHidden=true,distantPathVertices=distant.Vertices};
            }
            finally{Object.DestroyImmediate(settings);Object.DestroyImmediate(restored);}
        }
    }
}
