using UnityEngine;

namespace FightSong.InspectorTool
{
    /// <summary>Presentation-only springs. They never change component indices or navigation history.</summary>
    public sealed class OrbitalNodeMotion
    {
        public float Scale { get; private set; } = 1;
        public float Ripple { get; private set; }
        public Vector2 Offset { get; private set; }
        private string key;
        private double previousPosition;
        private float tangent, tangentVelocity, lift, liftVelocity, scaleVelocity;
        private bool wasSelected;

        public void Click(InspectorWheelSettings settings,float multiplier=1)
        {
            if (settings.ReducedMotion || settings.AnimationIntensity <= 0) return;
            float reaction=settings.AnimationIntensity*settings.ClickReaction*Mathf.Clamp01(multiplier);
            Scale -= .05f * reaction;
            scaleVelocity = -.35f * reaction;
            Ripple = reaction;
        }

        public void Tick(float dt, double position, string identity, int slot, Vector2 radial,
            bool hovered, bool selected, double time, InspectorWheelSettings settings,float reactionMultiplier=1,float idleMultiplier=1)
        {
            float intensity = settings.ReducedMotion ? 0 : Mathf.Clamp01(settings.AnimationIntensity);
            bool changed = key != identity;
            float travel = changed ? 0 : (float)(position - previousPosition);
            previousPosition = position;
            key = identity;
            if (intensity == 0 || identity == null)
            {
                tangent = tangentVelocity = lift = liftVelocity = scaleVelocity = Ripple = 0;
                Scale = 1; Offset = Vector2.zero; wasSelected = selected;
                return;
            }
            if (changed)
            {
                // Recycling a slot adds a small arrival response without resetting its orbital momentum.
                lift = -4 * intensity; liftVelocity = 0;
                Scale = 1 - .055f * intensity; scaleVelocity = 0;
            }
            if (selected && !wasSelected) scaleVelocity += .22f * intensity * reactionMultiplier;
            wasSelected = selected;
            // A bounded tangent lag gives a moving wheel weight, then overshoots as the spring returns.
            tangent = Mathf.Clamp(tangent - Mathf.Clamp(travel * 48, -10, 10) * intensity, -9 * intensity, 9 * intensity);
            float targetLift = (hovered ? 4 : selected ? 2 : 0) * intensity * reactionMultiplier;
            float targetScale = 1 + (hovered ? .038f : selected ? .018f : 0) * intensity * reactionMultiplier;
            float remaining = Mathf.Clamp(dt, 0, .1f);
            float stiffness = Mathf.Clamp(settings.SpringStrength, 40, 400);
            float damping = 2 * Mathf.Sqrt(stiffness) * Mathf.Clamp(settings.SpringDamping, .45f, 1.5f);
            // Small integration steps remain stable at 15 FPS and after an editor stall.
            while (remaining > 0)
            {
                float step = Mathf.Min(remaining, 1f / 120);
                Spring(ref tangent, ref tangentVelocity, 0, stiffness, damping, step);
                Spring(ref lift, ref liftVelocity, targetLift, stiffness, damping, step);
                float scale = Scale;
                Spring(ref scale, ref scaleVelocity, targetScale, stiffness, damping, step);
                Scale = Mathf.Clamp(scale, .9f, 1.07f);
                remaining -= step;
            }
            Ripple = Mathf.Max(0, Ripple - dt * 2.5f);
            float phase = slot * 2.399963f; // Stable phases; no random changes after every repaint.
            float t = (float)(time % 10000) * Mathf.Clamp(settings.BobSpeed, .2f, 2);
            float bob = Mathf.Sin(t + phase) * Mathf.Clamp(settings.BobAmplitude, 0, 6) * intensity;
            float drift = Mathf.Sin(t * .61f + phase * 1.7f) * Mathf.Clamp(settings.MicroDrift, 0, 3) * intensity;
            Vector2 along = new Vector2(-radial.y, radial.x);
            Offset = radial * lift + Vector2.up * (bob*idleMultiplier) + along * (tangent + drift*idleMultiplier);
        }

        internal static void Spring(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
        {
            velocity += ((target - value) * stiffness - damping * velocity) * dt;
            value += velocity * dt;
        }
    }
}
