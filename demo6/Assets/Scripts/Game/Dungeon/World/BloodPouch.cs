using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 쓰러짐 대가 손잡이(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5, F1 던전 패널). 기본값은 결정 D1 '다'.
    /// </summary>
    public static class DownTuning
    {
        /// <summary>다시 설 때 체력 60%·물약 남은 수(끄면 예전처럼 가득).</summary>
        public static bool Penalty = true;
        public static float HpFraction = DownRules.RespawnHpFraction;
        /// <summary>주머니 몫(0이면 떨구지 않음).</summary>
        public static float PouchShare = DownRules.PouchShare;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Penalty = true;
            HpFraction = DownRules.RespawnHpFraction;
            PouchShare = DownRules.PouchShare;
        }
    }

    /// <summary>
    /// 피 묻은 주머니(묶음 5-4): 쓰러진 자리에 이번 원정 강화석·골드 몫을 담아 떨군다. F로 주우면 지갑(DungeonState)에 되돌린다.
    /// 한 번에 하나만 있다: 줍기 전에 또 쓰러지면 앞 주머니는 사라진다(Drop이 지움). 층을 떠나면 장면과 함께 사라진다.
    /// 새 그림 없이 도형(검붉은 자루·끈)으로 그리고, 8유닛 안에서 은은히 깜빡인다.
    /// </summary>
    public sealed class BloodPouch : Interactable
    {
        const float GlintRange = 8f;
        static readonly Color Sack = new Color(0.36f, 0.12f, 0.1f);
        static readonly Color SackDark = new Color(0.2f, 0.06f, 0.05f);
        static readonly Color Tie = new Color(0.62f, 0.5f, 0.3f);
        static readonly Color GlintColor = new Color(1f, 0.55f, 0.45f, 0.8f);
        static readonly Color GainText = new Color(1f, 0.82f, 0.7f);

        public static BloodPouch Current { get; private set; }

        int _stones;
        int _gold;
        bool _taken;
        SpriteRenderer _glint;

        public int Stones => _stones;
        public int Gold => _gold;
        public override string Prompt => "피 묻은 주머니 줍기";
        public override bool Available => !_taken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>앞 주머니가 있으면 지우고(사라졌다는 알림) 새 주머니를 놓는다.</summary>
        public static BloodPouch Drop(Vector2 pos, int stones, int gold)
        {
            if (Current)
            {
                Destroy(Current.gameObject);
                Current = null;
                DungeonEvents.Say(DownRules.PouchLostLine);
            }
            if (stones <= 0 && gold <= 0) return null;
            var go = WorldProps.Root("BloodPouch", pos);
            var pouch = go.AddComponent<BloodPouch>();
            pouch._stones = stones;
            pouch._gold = gold;
            pouch.Build(pos);
            Current = pouch;
            return pouch;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Sack", new Vector2(0f, -0.02f), new Vector2(0.46f, 0.38f), ShapeSprites.Circle, Sack, order, false);
            WorldProps.Shape(transform, "Shade", new Vector2(0.06f, -0.08f), new Vector2(0.3f, 0.2f), ShapeSprites.Circle, SackDark, order + 1, false);
            WorldProps.Shape(transform, "Tie", new Vector2(0f, 0.17f), new Vector2(0.2f, 0.07f), ShapeSprites.Square, Tie, order + 2, false);
            _glint = WorldProps.Shape(transform, "Glint", new Vector2(0f, 0.05f), new Vector2(0.55f, 0.55f), WorldProps.SoftDot, GlintColor, order + 3, true);
            _glint.enabled = false;
        }

        public override void Interact()
        {
            if (_taken) return;
            _taken = true;
            var state = WorldProps.State;
            if (state != null)
            {
                state.Stones += _stones;
                state.Gold += _gold;
            }
            Sfx.Play(SfxKind.Pickup);
            WorldOverlay.Text(Position + Vector2.up * 1.1f, "되찾음", GainText);
            DungeonEvents.Say(DownRules.PouchTakeLine(_stones, _gold));
            if (Current == this) Current = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_taken || !_glint) return;
            bool show = WorldProps.PlayerDistance(Position) <= GlintRange;
            _glint.enabled = show;
            if (!show) return;
            var c = GlintColor;
            c.a = (0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.6f)) * GlintColor.a;
            _glint.color = c;
        }
    }
}
