using System;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Display-only snapshot supplied by the skill resource system. No cost, recovery or default balance values.</summary>
    public readonly struct HudSkillResource
    {
        public readonly string ShortLabel;
        public readonly float Current;
        public readonly float Maximum;
        public float Fraction => Mathf.Clamp01(Current / Maximum);

        public HudSkillResource(string shortLabel, float current, float maximum)
        {
            ShortLabel = shortLabel;
            Current = current;
            Maximum = maximum;
        }

        /// <summary>Bind to the real system; return null while unavailable. Unbind when that system is destroyed.</summary>
        public static Func<PlayerController, HudSkillResource?> Provider { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetProvider() => Provider = null;

        internal static HudSkillResource? Read(PlayerController player)
        {
            var value = Provider?.Invoke(player);
            if (!value.HasValue) return null;
            var v = value.Value;
            if (string.IsNullOrWhiteSpace(v.ShortLabel) || !Finite(v.Current) || !Finite(v.Maximum) || v.Maximum <= 0f || v.Current < 0f || v.Current > v.Maximum)
                return null;
            return value;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static partial class UiV45
    {
        // Same span as the six combat actions. Bag count remains above the separate utility buttons.
        public static Rect GuardRect => new Rect(ActionRect(-1).x + 2f, ActionRect(-1).y - 22f, ActionRect(4).xMax - ActionRect(-1).x - 4f, 9f);
        public static Rect GuardHitRect => new Rect(GuardRect.x - 5f, GuardRect.y - 3f, GuardRect.width + 10f, GuardRect.height + 6f);
    }
}
