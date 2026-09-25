using System;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Colours of one slot state (Inspector).
    [Serializable] public sealed class FieldSlotStyle
    {
        [Tooltip("칸 바탕")] public Color Background = new Color(.99f, .95f, .85f, 1);
        [Tooltip("칸 테두리")] public Color Border = new Color(.9f, .7f, .28f, 1);
        [Tooltip("동그라미 안 (알파 0 = 빈 동그라미)")] public Color Token = new Color(.95f, .76f, .32f, 1);
        [Tooltip("동그라미 테두리")] public Color TokenRing = new Color(.06f, .07f, .07f, 1);
        [Tooltip("동그라미 테두리를 점선으로")] public bool DashedRing;
        [Tooltip("그림 색")] public Color Glyph = new Color(.06f, .07f, .07f, 1);
        [Tooltip("첫 줄 글자 색")] public Color Title = new Color(.1f, .09f, .08f, 1);
        [Tooltip("둘째 줄 글자 색")] public Color Detail = new Color(.38f, .31f, .2f, 1);
    }

    // One member card's action slot on the site board (기획/탐험-화면정리와-행동칸-1차.md · 시안 01~03): a token with the action
    // glyph and two lines — used ('<할 일>' · '행동 완료'), free ('행동 남음', red dashed), down, paused. FieldMemberActionSlots
    // writes it every turn; FieldQuickAssign uses the selection outline and the card input. Layout = the card prefab
    // (BuildMemberActionSlot.Run: anchored to the card bottom, stretched across). A copy of the card (the encounter view
    // duplicates the arrival cards) hides its slot, outline and input by itself.
    [DisallowMultipleComponent]
    public sealed class FieldMemberActionSlot : MonoBehaviour
    {
        public enum State { Hidden, Used, Free, Down, Paused }
        public ExpeditionMemberCard Card;
        [Header("칸")]
        public Graphic Background;
        public FieldFrameGraphic Frame;
        public SegmentRingGraphic TokenFill, TokenRing;
        public ActionGlyph Glyph;
        public Text Title, Detail;
        [Header("선택")]
        [Tooltip("고른 대원 카드의 금색 테두리")] public FieldFrameGraphic SelectFrame;
        [Tooltip("카드 전체를 덮는 입력 (누르기 · 끌기)")] public FieldMemberCardInput Input;
        [Header("상태별 색")]
        public FieldSlotStyle Used = new FieldSlotStyle();
        public FieldSlotStyle Free = new FieldSlotStyle { Background = new Color(1f, .95f, .91f, 1), Border = new Color(.78f, .24f, .19f, 1), Token = new Color(0, 0, 0, 0), TokenRing = new Color(.78f, .24f, .19f, 1), DashedRing = true, Title = new Color(.74f, .2f, .15f, 1), Detail = new Color(.16f, .14f, .12f, 1) };
        public FieldSlotStyle Down = new FieldSlotStyle { Background = new Color(.88f, .87f, .84f, 1), Border = new Color(.52f, .51f, .48f, 1), Token = new Color(.6f, .59f, .56f, 1), TokenRing = new Color(.4f, .39f, .37f, 1), Title = new Color(.36f, .35f, .33f, 1), Detail = new Color(.42f, .41f, .39f, 1) };
        public FieldSlotStyle Paused = new FieldSlotStyle { Background = new Color(.97f, .92f, .86f, 1), Border = new Color(.71f, .27f, .18f, 1), Token = new Color(.84f, .8f, .72f, 1), TokenRing = new Color(.71f, .27f, .18f, 1), Title = new Color(.71f, .27f, .18f, 1), Detail = new Color(.3f, .26f, .2f, 1) };
        [Tooltip("테두리 두께 (px)")] [Min(.5f)] public float BorderThickness = 3;
        [Tooltip("점선 동그라미 칸 수")] [Min(3)] public int Dashes = 12;
        [Tooltip("동그라미 테두리 두께 (px)")] [Min(.5f)] public float RingThickness = 2.5f;

        public State Current { get; private set; } = State.Hidden;
        public string TitleText => Title ? Title.text : "";
        public string DetailText => Detail ? Detail.text : "";
        public ActionGlyph.Kind GlyphKind => Glyph && Glyph.gameObject.activeSelf ? Glyph.Glyph : ActionGlyph.Kind.None;
        public bool Selected => SelectFrame && SelectFrame.gameObject.activeSelf;
        // The card lives in the arrival panel's member row (not a copy in another view).
        public bool Live { get { var a = Card ? Card.GetComponentInParent<ExpeditionArrivalPanel>(true) : null; return a && a.MemberContent && Card.transform.parent == a.MemberContent; } }

        void Start() { if (!Live) Detach(); }
        // A copy of the card: no slot, no outline, no input.
        public void Detach() { Hide(); SetSelected(false); if (Input) Input.SetHit(false); }

        public void Show(State state, ActionGlyph.Kind glyph, string title, string detail)
        {
            if (state == State.Hidden) { Hide(); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var st = state == State.Used ? Used : state == State.Free ? Free : state == State.Down ? Down : Paused;
            if (Current != state)
            {
                if (Background) Background.color = st.Background;
                if (Frame) Frame.SetStyle(st.Border, BorderThickness, 0);
                if (TokenFill) { TokenFill.color = st.Token; TokenFill.enabled = st.Token.a > .001f; }
                if (TokenRing)
                {
                    // Solid: one ring in `color`; dashed: every cell is a 'rest' cell (RestColor × color).
                    TokenRing.color = st.TokenRing; TokenRing.RestColor = Color.white; TokenRing.Thickness = RingThickness;
                    TokenRing.GapDegrees = st.DashedRing ? 360f / Mathf.Max(3, Dashes) * .45f : 0; TokenRing.Set(st.DashedRing ? Mathf.Max(3, Dashes) : 0, 0, 0);
                    TokenRing.SetVerticesDirty();
                }
                if (Glyph) Glyph.color = st.Glyph;
                if (Title) Title.color = st.Title;
                if (Detail) Detail.color = st.Detail;
                Current = state;
            }
            if (Glyph) { bool on = glyph != ActionGlyph.Kind.None; if (Glyph.gameObject.activeSelf != on) Glyph.gameObject.SetActive(on); if (on) Glyph.Glyph = glyph; }
            if (Title && Title.text != title) Title.text = title;
            if (Detail) { string d = detail ?? ""; if (Detail.text != d) Detail.text = d; bool on = d.Length > 0; if (Detail.gameObject.activeSelf != on) Detail.gameObject.SetActive(on); }
        }
        public void Hide() { Current = State.Hidden; if (gameObject.activeSelf) gameObject.SetActive(false); }
        public void SetSelected(bool on) { if (SelectFrame && SelectFrame.gameObject.activeSelf != on) SelectFrame.gameObject.SetActive(on); }
    }
}
