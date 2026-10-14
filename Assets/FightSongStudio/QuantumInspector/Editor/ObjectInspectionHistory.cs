using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightSong.InspectorTool
{
    [Serializable]
    public sealed class ObjectInspectionHistory
    {
        [Serializable]
        public sealed class Location
        {
            public Component Component;
            public string Query="";
            public double Rotation;
        }
        [SerializeField] private List<GameObject> entries = new List<GameObject>();
        [SerializeField] private List<Location> locations = new List<Location>();
        [SerializeField] private int cursor = -1;
        public IReadOnlyList<GameObject> Entries => entries;
        public int Cursor => cursor;
        public Location Current { get { Align();return cursor>=0&&cursor<locations.Count?locations[cursor]:null; } }
        private void Align()
        {
            // Upgrade histories serialized before component/search snapshots were introduced.
            while(locations.Count<entries.Count)locations.Add(new Location());
            if(locations.Count>entries.Count)locations.RemoveRange(entries.Count,locations.Count-entries.Count);
        }
        public void Capture(Component component,string query,double rotation)
        {var state=Current;if(state==null)return;state.Component=component;state.Query=query??"";state.Rotation=rotation;}
        public void Visit(GameObject target,Component component=null,string query="",double rotation=0,bool checkpoint=false)
        {
            Align();
            if (target == null) return;
            if (cursor >= 0 && cursor < entries.Count && entries[cursor] == target &&
                (!checkpoint || (Current.Component==component&&Current.Query==query))) return;
            if (cursor+1<entries.Count)
            {entries.RemoveRange(cursor+1,entries.Count-cursor-1);locations.RemoveRange(cursor+1,locations.Count-cursor-1);}
            entries.Add(target);
            locations.Add(new Location{Component=component,Query=query??"",Rotation=rotation});
            if (entries.Count>100) {entries.RemoveAt(0);locations.RemoveAt(0);}
            cursor=entries.Count-1;
        }
        public bool CanMove(int direction) => Find(direction)>=0;
        private int Find(int direction)
        {
            if(direction==0)return -1;
            direction=direction<0?-1:1;
            for(int i=cursor+direction;i>=0&&i<entries.Count;i+=direction)
                if(entries[i]!=null) return i;
            return -1;
        }
        public GameObject Move(int direction)
        {
            int index=Find(direction);
            if(index<0)return null;
            cursor=index; return entries[cursor];
        }
    }
}
