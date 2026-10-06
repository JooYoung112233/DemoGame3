using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Damage number / short result word. Pops, drifts up and fades on unscaled time.
    public sealed class BattleFloatingText : MonoBehaviour
    {
        public Text Label;
        public AnimationCurve Pop = new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.12f, 1.18f), new Keyframe(.26f, 1), new Keyframe(1, .96f));
        public AnimationCurve Fade = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.62f, 1), new Keyframe(1, 0));
        static readonly System.Random cosmetic = new System.Random(311);
        Vector2 start; float rise, duration, scale, age = -1, sway;
        public void Play(string text, Color color, Vector2 at, float size, float risePixels, float seconds)
        {
            Label.text = text; Label.color = color; start = at; rise = risePixels; duration = Mathf.Max(.05f, seconds); scale = size; age = 0;
            sway = (float)(cosmetic.NextDouble() * 28 - 14); Apply();
        }
        void Update() { if (age < 0) return; age += Time.unscaledDeltaTime; Apply(); if (age >= duration) Destroy(gameObject); }
        void Apply()
        {
            float u = Mathf.Clamp01(age / duration), ease = 1 - (1 - u) * (1 - u);
            ((RectTransform)transform).anchoredPosition = start + new Vector2(sway * ease, rise * ease);
            transform.localScale = Vector3.one * scale * Pop.Evaluate(u);
            var c = Label.color; c.a = Fade.Evaluate(u); Label.color = c;
        }
    }
}
