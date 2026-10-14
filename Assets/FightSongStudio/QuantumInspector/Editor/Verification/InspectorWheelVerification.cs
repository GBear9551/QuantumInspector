using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FightSong.InspectorTool
{
    /// <summary>Explicit, isolated checks; no scene assets or gameplay state are changed.</summary>
    public static class InspectorWheelVerification
    {
        public static object Run()
        {
            var settings=ScriptableObject.CreateInstance<InspectorWheelSettings>();
            var scene=EditorSceneManager.NewPreviewScene();
            var window=InspectorWheelWindow.Open();
            var original=window.InspectedObject;
            var originalQuery=window.Wheel.Search.value;
            bool originalPin=window.Pinned;
            int cases=0;
            try
            {
                for(int count=0;count<=41;count++)
                {
                    var wheel=new InspectorWheelController();wheel.Reset(count);
                    var visited=new HashSet<int>();
                    for(int step=0;step<500;step++)
                    {
                        wheel.Rotate(step<250?-.37:.61,true);
                        var visible=Enumerable.Range(0,5).Select(wheel.IndexForSlot).Where(i=>i>=0).ToArray();
                        Check(visible.Length==Math.Min(5,count),"Incorrect visible count.");
                        Check(visible.Distinct().Count()==visible.Length,"Duplicate orbital entry.");
                        Check(visible.All(i=>i<count),"Index out of range.");
                        foreach(int item in visible)visited.Add(item);
                    }
                    Check(visited.Count==count,"Cycling skipped components.");cases++;
                }
                var motion=new InspectorWheelController();motion.Reset(12);
                motion.Rotate(-.5,false);motion.Rotate(-.5,false);
                for(int i=0;i<150;i++)motion.Tick(.02,settings);
                Check(Math.Abs(motion.Position+1)<.001,"Fractional scroll did not accumulate and snap.");
                motion.BeginDrag();motion.Drag(.4,.04);motion.EndDrag(true,true);
                for(int i=0;i<400;i++)motion.Tick(.02,settings);
                Check(Math.Abs(motion.Position-Math.Round(motion.Position))<.001,"Momentum did not settle.");
                var a=new GameObject("Quantum test A");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(a,scene);
                var b=new GameObject("Quantum referenced B");b.transform.SetParent(a.transform);
                var c=new GameObject("Quantum history C");c.transform.SetParent(a.transform);
                var d=new GameObject("Quantum history D");d.transform.SetParent(a.transform);
                a.AddComponent<MeshFilter>();a.AddComponent<MeshRenderer>();a.AddComponent<BoxCollider>();
                a.AddComponent<SphereCollider>();a.AddComponent<CapsuleCollider>();a.AddComponent<AudioSource>();
                a.AddComponent<Rigidbody>();a.AddComponent<Light>();a.AddComponent<Camera>();
                var joint=a.AddComponent<FixedJoint>();var body=b.AddComponent<Rigidbody>();joint.connectedBody=body;
                var history=new ObjectInspectionHistory();history.Visit(a);history.Visit(b);history.Visit(c);
                Check(history.Move(-1)==b,"Back history failed.");Check(history.Move(1)==c,"Forward history failed.");
                history.Move(-1);history.Visit(d);Check(!history.CanMove(1)&&history.Entries.Last()==d,"History branch failed.");
                history.Visit(d);Check(history.Entries.Count==3,"Duplicate history entry.");
                var graph=new ReferenceGraphService();
                var discovery=new ComponentDiscoveryService();
                var entries=discovery.Discover(a,graph,true);
                Check(entries.Count==11,"Discovery omitted components.");
                Check(ComponentDiscoveryService.Filter(entries,"mesh").Count==2,"Class filtering failed.");
                Check(ComponentDiscoveryService.Filter(entries,"PHYSICS").Count==5,"Category filtering failed.");
                Check(ComponentDiscoveryService.Filter(entries,"quantum referenced").Any(e=>e.Component==joint),"Reference name filtering failed.");
                Check(ComponentDiscoveryService.Filter(entries,"no-such-component").Count==0,"Empty state filtering failed.");
                Check(graph.Outgoing(joint).Any(e=>e.Target==body&&e.Kind==RelationshipKinds.Serialized),"Outgoing serialized link missing.");
                graph.StartIncoming(body);
                while(graph.Scanning)graph.Tick(200);
                Check(graph.Incoming.Any(e=>e.Source==joint),"Incoming serialized link missing.");
                int before=graph.Incoming.Count;graph.InvalidateCachedData();
                Check(graph.Incoming.Count==before,"Cache invalidation erased incoming research.");
                joint.connectedBody=null;graph.InvalidateCachedData();
                Check(!graph.Outgoing(joint).Any(e=>e.Target==body),"Reference cache was not invalidated.");
                window.Pinned=true;window.Inspect(a,false);window.SetQuery("");
                var nodes=window.Wheel.Nodes.ToArray();
                window.Wheel.Tick(.04f);
                Check(window.FilteredCount==11&&nodes.Count(n=>n.Entry!=null)==5,"Window discovery failed.");
                window.Controller.Rotate(-17,true);window.Wheel.Tick(.04f);
                Check(nodes.SequenceEqual(window.Wheel.Nodes),"Nodes were recreated during rotation.");
                Check(nodes.Select(n=>n.Entry.Key).Distinct().Count()==5,"Window duplicated components.");
                window.SetQuery("collider");window.Wheel.Tick(.04f);
                Check(nodes.Count(n=>n.Entry!=null)==3,"Short filter duplicated nodes.");
                window.SetQuery("no-such-component");window.Wheel.Tick(.04f);
                Check(nodes.All(n=>n.Entry==null),"Empty filter left nodes visible.");
                return new {passed=true,componentCountCases=cases,rotationSamples=cases*500,history=true,filtering=true,
                    serializedReferences=true,incomingScan=true,nodeReuse=true};
            }
            finally
            {
                window.Inspect(original,false);window.SetQuery(originalQuery);window.Pinned=originalPin;
                EditorSceneManager.ClosePreviewScene(scene);Object.DestroyImmediate(settings);
            }
        }
        private static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
    }
}
