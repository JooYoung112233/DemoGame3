using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // One glowing target while a member is chosen (기획/탐험-화면정리와-행동칸-1차.md · 시안 02): a gold pin over the target's
    // marker with the action glyph (돋보기 수색 · 귀 문에 귀 대기 · 눈 흔적 관찰), or, when an assignment bubble already stands
    // there, only a pulsing gold ring around that bubble. The root pivot is the pin's tail tip. Code-drawn, never takes clicks:
    // FieldQuickAssign / FieldPawnBoard hit-test HitRect / the halo / the caption themselves. Parts and sizes are the prefab layout.
    // 말 놓기 (기획/탐험-말놓기-조작-재설계.md · 시안 01): the pin can carry a short caption under it ('수색 2턴 · 소음 0') and be
    // shown as a place the held pawn cannot take now: grey, a padlock instead of the glyph, no pulse, the reason as its caption.
    [DisallowMultipleComponent]
    public sealed class FieldTargetGlow : MonoBehaviour
    {
        [Tooltip("핀 묶음 (핀 · 원판 · 그림) · 피벗 = 꼬리 끝")] public RectTransform Visual;
        [Tooltip("핀 모양")] public Graphic Pin;
        [Tooltip("핀 머리 안 종이 원판")] public Graphic Disc;
        [Tooltip("행동 그림")] public ActionGlyph Glyph;
        [Tooltip("맥동하는 고리 (핀 둘레 또는 말풍선 둘레)")] public SegmentRingGraphic Halo;
        [Tooltip("누르는 자리 (핀일 때)")] public RectTransform HitRect;
        [Header("설명 · 못 놓는 자리 (말 놓기)")]
        [Tooltip("핀 아래 짧은 설명 (예: '수색 2턴 · 소음 0', '지렛대 필요') · 비우면 설명 없음")] public Text Caption;
        [Tooltip("설명 바탕 (글 길이에 맞춰 늘어나는 종이 · 비우면 글의 부모)")] public Graphic CaptionPaper;
        [Tooltip("못 놓는 자리의 자물쇠 그림 (행동 그림 대신)")] public Graphic Lock;
        [Tooltip("설명이 화면 가장자리에서 떨어지는 거리 (px) · 넘치면 옆으로 밀어 넣음")] [Min(0)] public float CaptionEdgeMargin = 12;
        [Header("색")]
        [Tooltip("핀 색")] public Color PinColor = new Color(.96f, .75f, .28f, 1);
        [Tooltip("원판 색")] public Color DiscColor = new Color(.98f, .95f, .86f, 1);
        [Tooltip("그림 색")] public Color GlyphColor = new Color(.06f, .07f, .07f, 1);
        [Tooltip("고리 색 (알파 = 가장 진할 때)")] public Color HaloColor = new Color(1f, .8f, .32f, .95f);
        [Tooltip("못 놓는 자리 · 핀 색")] public Color DisabledPinColor = new Color(.55f, .55f, .53f, 1);
        [Tooltip("못 놓는 자리 · 원판 색")] public Color DisabledDiscColor = new Color(.86f, .85f, .82f, 1);
        [Tooltip("못 놓는 자리 · 자물쇠 색")] public Color DisabledGlyphColor = new Color(.24f, .24f, .23f, 1);
        [Tooltip("설명 글자 색")] public Color CaptionColor = new Color(.97f, .94f, .86f, 1);
        [Tooltip("못 놓는 자리 · 설명 글자 색")] public Color DisabledCaptionColor = new Color(.86f, .85f, .82f, 1);
        [Header("움직임")]
        [Tooltip("나타날 때 커지는 시간 (초 · 실제 시간)")] [Min(0)] public float PopSeconds = .18f;
        [Tooltip("고리 맥동 속도")] [Min(0)] public float PulseSpeed = 4.2f;
        [Tooltip("고리 맥동 크기 (배율)")] [Range(0, .3f)] public float PulseScale = .1f;
        [Tooltip("고리 맥동 때 가장 옅은 알파 (배율)")] [Range(0, 1)] public float PulseFade = .35f;
        [Tooltip("포인터가 올라왔을 때 크기 (배율)")] [Range(1, 1.5f)] public float HoverScale = 1.15f;
        [Tooltip("말풍선 둘레 고리: 말풍선 고리보다 바깥으로 (px)")] [Min(0)] public float AroundPad = 8;

        public string Key { get; private set; } = "";
        public bool Around { get; private set; }
        public bool Hover { get; set; }
        public bool Visible => gameObject.activeSelf;
        // False: a place the held pawn cannot take now (grey, padlock, its reason as the caption).
        public bool Enabled { get; private set; } = true;
        public ActionGlyph.Kind GlyphKind => Glyph ? Glyph.Glyph : ActionGlyph.Kind.None;
        public string CaptionText => Caption && Caption.gameObject.activeSelf ? Caption.text : "";
        public RectTransform Rect => (RectTransform)transform;
        // What a press must fall in: the pin, or the ring around a bubble.
        public RectTransform HitArea => Around ? (Halo ? Halo.rectTransform : Rect) : HitRect ? HitRect : Rect;
        // The caption's paper (a press on it counts as the pin), null while no caption shows.
        public RectTransform CaptionArea
        {
            get
            {
                if (!Caption || !Caption.gameObject.activeInHierarchy) return null;
                var paper = CaptionPaper ? CaptionPaper.rectTransform : Caption.rectTransform.parent as RectTransform;
                return paper && paper.gameObject.activeInHierarchy ? paper : Caption.rectTransform;
            }
        }

        Vector2 haloHome, haloSize, captionHome; Vector3 visualScale = Vector3.one; bool homeRead; float shownAt; RectTransform captionPaper;

        void ReadHome()
        {
            if (homeRead) return; homeRead = true;
            if (Halo) { haloHome = Halo.rectTransform.anchoredPosition; haloSize = Halo.rectTransform.sizeDelta; }
            if (Visual) { var s = Visual.localScale; if (s.x != 0 && s.y != 0) visualScale = s; }
            captionPaper = CaptionPaper ? CaptionPaper.rectTransform : Caption ? Caption.rectTransform.parent as RectTransform : null;
            if (captionPaper) captionHome = captionPaper.anchoredPosition;
        }
        // A pin with its tail tip at `tip` (this node's parent space).
        public void ShowPin(string key, ActionGlyph.Kind glyph, Vector2 tip) => ShowPin(key, glyph, tip, null, true);
        // A pin with a caption under it; `enabled` false = the grey padlock pin of a place the pawn cannot take (caption = the reason).
        public void ShowPin(string key, ActionGlyph.Kind glyph, Vector2 tip, string caption, bool enabled)
        {
            ReadHome(); Enabled = enabled; Begin(key);
            Around = false; SetParts(true); if (Glyph) Glyph.Glyph = glyph;
            if (Halo) { var h = Halo.rectTransform; if (h.anchoredPosition != haloHome) h.anchoredPosition = haloHome; if (h.sizeDelta != haloSize) h.sizeDelta = haloSize; }
            SetCaption(caption);
            Place(tip);
        }
        // Only the ring, around a bubble ring `ring` (this node's parent space).
        public void ShowAround(string key, ActionGlyph.Kind glyph, Rect ring)
        {
            ReadHome(); Enabled = true; Begin(key);
            Around = true; SetParts(false); if (Glyph) Glyph.Glyph = glyph; SetCaption(null);
            float size = Mathf.Max(ring.width, ring.height) + 2 * AroundPad;
            if (Halo) { var h = Halo.rectTransform; var dim = new Vector2(size, size); if (h.sizeDelta != dim) h.sizeDelta = dim; Place(ring.center - h.anchoredPosition); }
            else Place(ring.center);
        }
        public void Hide() { Key = ""; Hover = false; if (gameObject.activeSelf) gameObject.SetActive(false); }

        void Begin(string key)
        {
            bool fresh = !gameObject.activeSelf || key != Key; Key = key;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (fresh) { shownAt = Time.unscaledTime; Hover = false; }
            if (Pin) Pin.color = Enabled ? PinColor : DisabledPinColor; if (Disc) Disc.color = Enabled ? DiscColor : DisabledDiscColor;
            if (Glyph) { Glyph.color = GlyphColor; if (Glyph.gameObject.activeSelf != Enabled) Glyph.gameObject.SetActive(Enabled); }
            if (Lock) { Lock.color = DisabledGlyphColor; if (Lock.gameObject.activeSelf == Enabled) Lock.gameObject.SetActive(!Enabled); }
            if (Halo && Halo.enabled != Enabled) Halo.enabled = Enabled;
        }
        void SetCaption(string caption)
        {
            if (!Caption) return;
            bool on = !string.IsNullOrEmpty(caption);
            var holder = CaptionPaper ? CaptionPaper.gameObject : Caption.gameObject;
            if (holder.activeSelf != on) holder.SetActive(on);
            if (!Caption.gameObject.activeSelf && on) Caption.gameObject.SetActive(true);
            if (!on) return;
            if (Caption.text != caption) Caption.text = caption;
            var c = Enabled ? CaptionColor : DisabledCaptionColor; if (Caption.color != c) Caption.color = c;
        }
        void SetParts(bool pin) { if (Visual && Visual.gameObject.activeSelf != pin) Visual.gameObject.SetActive(pin); }
        void Place(Vector2 at) { var p = new Vector3(at.x, at.y, 0); if (Rect.localPosition != p) Rect.localPosition = p; }

        void Update()
        {
            float t = Time.unscaledTime - shownAt;
            float pop = PopSeconds <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / PopSeconds));
            float hover = Hover ? HoverScale : 1;
            if (Visual) { var s = visualScale * pop * hover; s.z = visualScale.z; Visual.localScale = s; }
            if (Halo && Halo.enabled)
            {
                float wave = .5f - .5f * Mathf.Cos(t * PulseSpeed);
                float scale = pop * (1 + PulseScale * wave) * (Around ? 1 : hover);
                Halo.rectTransform.localScale = new Vector3(scale, scale, 1);
                var c = HaloColor; c.a *= Mathf.Lerp(1, PulseFade, wave); Halo.color = c;
            }
        }
        // Keep the caption inside the screen: slide it sideways (from its prefab place) when it would cross the canvas edge.
        void LateUpdate()
        {
            var paper = CaptionArea; if (!paper || !homeRead || paper != captionPaper) return;
            var canvas = paper.GetComponentInParent<Canvas>(); if (!canvas) return; var root = (RectTransform)canvas.rootCanvas.transform;
            var corners = new Vector3[4]; paper.GetWorldCorners(corners);
            float minX = root.InverseTransformPoint(corners[0]).x, maxX = root.InverseTransformPoint(corners[2]).x;
            float ratio = (maxX - minX) / Mathf.Max(1e-3f, paper.rect.width); if (ratio < 1e-3f) return; // root units per parent unit
            float now = (paper.anchoredPosition.x - captionHome.x) * ratio; minX -= now; maxX -= now;
            var r = root.rect; float m = CaptionEdgeMargin, shift = 0;
            if (maxX > r.xMax - m) shift = r.xMax - m - maxX;
            if (minX + shift < r.xMin + m) shift = r.xMin + m - minX;
            var want = new Vector2(captionHome.x + shift / ratio, captionHome.y);
            if ((paper.anchoredPosition - want).sqrMagnitude > .01f) paper.anchoredPosition = want;
        }
        void OnDisable() { if (Visual && homeRead) Visual.localScale = visualScale; if (captionPaper && homeRead) captionPaper.anchoredPosition = captionHome; }
    }
}
