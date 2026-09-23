using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Band across the board that names whose phase begins (우리 차례 / 적의 차례).
    // It never takes clicks; texts, colours and timing are Inspector-editable on the prefab.
    public sealed class BattlePhaseBanner : MonoBehaviour
    {
        public RectTransform Plate;
        public CanvasGroup Group;
        public Image Band, TopStripe, BottomStripe, Marker;
        public Text Title, Detail;
        public Color AllyColor = new Color(.46f, .76f, .62f), EnemyColor = new Color(.86f, .38f, .3f);
        public Color AllyInk = new Color(.12f, .3f, .24f), EnemyInk = new Color(.5f, .14f, .1f);
        [Min(0)] public float SlideIn = .2f, Hold = .6f, FadeOut = .24f, Travel = 120;
        Vector2 home; bool homeKnown;

        public void Hide() { StopAllCoroutines(); if (Group) Group.alpha = 0; gameObject.SetActive(false); }
        // speed divides every duration (review captures and tests run faster).
        public IEnumerator Play(bool enemy, string title, string detail, float speed = 1)
        {
            if (!homeKnown && Plate) { home = Plate.anchoredPosition; homeKnown = true; }
            gameObject.SetActive(true);
            var color = enemy ? EnemyColor : AllyColor;
            if (TopStripe) TopStripe.color = color; if (BottomStripe) BottomStripe.color = color; if (Marker) Marker.color = color;
            if (Title) { Title.text = title; Title.color = enemy ? EnemyInk : AllyInk; }
            if (Detail) { Detail.text = detail; Detail.gameObject.SetActive(!string.IsNullOrEmpty(detail)); }
            float side = enemy ? 1 : -1, k = Mathf.Max(.1f, speed);
            // Enemies sweep in from their side of the board (right), allies from theirs (left).
            for (float t = 0; t < SlideIn / k; t += Time.unscaledDeltaTime) { float u = t * k / Mathf.Max(.001f, SlideIn), e = 1 - (1 - u) * (1 - u); Set(e, side * Travel * (1 - e)); yield return null; }
            Set(1, 0);
            for (float t = 0; t < Hold / k; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < FadeOut / k; t += Time.unscaledDeltaTime) { float u = t * k / Mathf.Max(.001f, FadeOut); Set(1 - u, -side * Travel * .35f * u * u); yield return null; }
            Set(0, 0); gameObject.SetActive(false);
        }
        void Set(float alpha, float offset)
        {
            if (Group) Group.alpha = alpha;
            if (Plate) Plate.anchoredPosition = home + new Vector2(offset, 0);
        }
    }
}
