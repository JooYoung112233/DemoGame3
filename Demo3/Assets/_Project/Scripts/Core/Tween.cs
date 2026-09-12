using System;
using System.Collections;
using UnityEngine;

namespace Live49.Core
{
    public static class SeqTime
    {
        // Timed beats stop while the player build is unfocused so a scene never slips past unseen.
        // The Editor keeps running so the agent/CLI can drive it in the background.
        public static float Delta
        {
            get
            {
#if UNITY_EDITOR
                return Time.deltaTime;
#else
                return Application.isFocused ? Time.deltaTime : 0f;
#endif
            }
        }
    }

    public static class Tween
    {
        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // Normalised progress of a track that starts at `start` and lasts `duration`.
        public static float Seg(float t, float start, float duration)
        {
            if (duration <= 0f)
                return t >= start ? 1f : 0f;
            return Mathf.Clamp01((t - start) / duration);
        }

        public static IEnumerator Run(float duration, Action<float> step, bool smooth = true)
        {
            float t = 0f;
            step(0f);
            while (t < duration)
            {
                t += SeqTime.Delta;
                float k = Mathf.Clamp01(t / duration);
                step(smooth ? Smooth(k) : k);
                yield return null;
            }
            step(1f);
        }

        public static IEnumerator Wait(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += SeqTime.Delta;
                yield return null;
            }
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            float from = group.alpha;
            yield return Run(duration, k => group.alpha = Mathf.Lerp(from, to, k));
        }
    }
}
