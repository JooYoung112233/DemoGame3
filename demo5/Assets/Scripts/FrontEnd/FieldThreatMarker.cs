using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // A small paper tag on a door (footsteps behind it, the next step coming in) or above the thing itself.
    // Room: the room whose screen shows this marker. From: the neighbouring room behind that door (-1 for a tag that follows a standee).
    public sealed class FieldThreatMarker : MonoBehaviour
    {
        public int Room = -1, From = -1;
        public RectTransform Root;
        public Image Paper, Icon;
        public Text Label;
        public enum Tone { Quiet, Soon, Warning }
        public Color Warning = new Color(.86f, .38f, .3f), Quiet = new Color(.93f, .9f, .8f), WarningInk = new Color(.99f, .95f, .88f), QuietInk = new Color(.2f, .18f, .15f);
        [Tooltip("문에 귀 대기로 들은 '곧 들어옴'")] public Color SoonPaper = new Color(.95f, .74f, .38f), SoonInk = new Color(.2f, .14f, .08f);
        [Min(0)] public float PulseSpeed = 5, PulseScale = .08f;
        bool urgent;
        public void Show(string text, bool warning) => Show(text, warning ? Tone.Warning : Tone.Quiet);
        public void Show(string text, Tone tone)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            urgent = tone == Tone.Warning;
            if (Label) { Label.text = text; Label.color = tone == Tone.Warning ? WarningInk : tone == Tone.Soon ? SoonInk : QuietInk; }
            if (Paper) Paper.color = tone == Tone.Warning ? Warning : tone == Tone.Soon ? SoonPaper : Quiet;
        }
        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); }
        void Update()
        {
            var r = Root ? Root : (RectTransform)transform;
            r.localScale = Vector3.one * (urgent ? 1 + PulseScale * (.5f + .5f * Mathf.Sin(Time.unscaledTime * PulseSpeed)) : 1);
        }
    }
}
