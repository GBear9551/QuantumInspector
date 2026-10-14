using System;

namespace FightSong.InspectorTool
{
    /// <summary>Reusable physical slots crossing one recycle gate. Independent of the data provider.</summary>
    public sealed class InspectorWheelController
    {
        public int SlotCount {get;private set;}=5;
        public double SlotAngle=>360.0/SlotCount;
        public void SetCapacity(int capacity)
        {
            capacity=Math.Max(5,Math.Min(8,capacity));
            double ratio=(double)capacity/SlotCount;
            Position*=ratio;Goal*=ratio;Velocity*=ratio;SlotCount=capacity;
        }
        public double Position { get; private set; }
        public double Goal { get; private set; }
        public double Velocity { get; private set; }
        public bool Dragging { get; private set; }
        public int Count { get; private set; }
        public void SetCount(int count){Count=Math.Max(0,count);}
        public void ContinueMomentum(double velocity){Velocity=double.IsNaN(velocity)||double.IsInfinity(velocity)?0:Math.Max(-12,Math.Min(12,velocity));}
        private double inputSettle;
        public void RestorePosition(double value) { Position=Goal=double.IsNaN(value)||double.IsInfinity(value)?0:value;Velocity=inputSettle=0;Dragging=false; }
        public void Reveal(int index) { if(index>=0&&index<Count)RestorePosition(-index); }
        public void Reset(int count) { Count = Math.Max(0,count); Position = Goal = Velocity = inputSettle = 0; Dragging = false; }
        public static int Wrap(long value, int count) => count <= 0 ? -1 : (int)((value % count + count) % count);
        public int IndexForSlot(int slot)
        {
            if (slot < 0 || slot >= Math.Min(Count,SlotCount)) return -1;
            if (Count <= SlotCount) return slot;
            long cycle = (long)Math.Floor((slot + Position) / SlotCount);
            return Wrap(slot - SlotCount * cycle,Count);
        }
        public void Rotate(double steps, bool immediate)
        {
            Velocity = 0;
            Goal += steps;
            inputSettle = .15;
            if (immediate) Position = Goal;
        }
        public void BeginDrag() { Dragging = true; Velocity = 0; Goal = Position; }
        public void Drag(double steps, double seconds)
        {
            Position += steps; Goal = Position;
            Velocity = Math.Max(-12,Math.Min(12,steps / Math.Max(.008,seconds)));
        }
        public void EndDrag(bool momentum, bool snap)
        {
            Dragging = false;
            if (!momentum) Velocity = 0;
            Goal = snap && Math.Abs(Velocity) < .03 ? Math.Round(Position) : Position;
        }
        public bool Tick(double seconds, InspectorWheelSettings settings)
        {
            if (Dragging || Count == 0) return false;
            double before = Position;
            seconds = Math.Min(.05,Math.Max(0,seconds));
            inputSettle = Math.Max(0,inputSettle-seconds);
            if(inputSettle==0 && settings.SnapToSlots && Math.Abs(Velocity)<.03)
                Goal=Math.Round(Goal,MidpointRounding.AwayFromZero);
            if (!settings.ReducedMotion && settings.Momentum && Math.Abs(Velocity) > .03)
            {
                Position += Velocity * seconds;
                Velocity *= Math.Exp(-settings.MomentumFriction * seconds);
                Goal = settings.SnapToSlots ? Math.Round(Position) : Position;
            }
            else
            {
                Velocity = 0;
                Position = settings.ReducedMotion ? Goal : Position + (Goal-Position)*(1-Math.Exp(-settings.Smoothing*seconds));
                if (Math.Abs(Goal-Position)<.0001) Position=Goal;
            }
            // Preserve both labels and angular positions while avoiding eventual numeric drift.
            if (Math.Abs(Position) > 100000 && Math.Abs(Goal-Position)<1)
            {
                double period = Math.Max(1,Count)*SlotCount;
                double offset = Math.Truncate(Position/period)*period;
                Position-=offset; Goal-=offset;
            }
            return before != Position;
        }
    }
}
