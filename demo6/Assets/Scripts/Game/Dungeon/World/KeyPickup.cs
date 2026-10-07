using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 광업소 사무실 열쇠(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 나-9, 결정 D3 '나'): 오우거 유해 곁에 떨어진 '광부 열쇠 꾸러미에서 떨어진 사무실 열쇠 하나'.
    /// F로 주우면 State.HasKey가 켜져 1층 광업소 자물쇠 문(LockedDoor)과 금고(Safe)를 쓸 수 있다. 7층 한길의 열쇠와는 따로다(그 열쇠는 8층 사무실 몫).
    /// 열쇠는 꾸러미 'key' 줄로 남고 죽어도 잃지 않는다. 아직 열쇠가 없을 때만 BossReward가 놓는다. 새 그림 없이 도형(놋쇠 고리·열쇠 대)으로 그린다.
    /// </summary>
    public sealed class KeyPickup : Interactable
    {
        public const string Id = "den.office_key";
        public const string Label = "광업소 사무실 열쇠";
        const float GlintRange = 8f;

        static readonly Color Brass = new Color(0.72f, 0.58f, 0.3f);
        static readonly Color BrassDark = new Color(0.45f, 0.35f, 0.17f);
        static readonly Color GlintColor = new Color(1f, 0.95f, 0.75f, 0.9f);
        static readonly Color GainText = new Color(1f, 0.92f, 0.7f);

        bool _taken;
        float _seed;
        SpriteRenderer _glint;

        public override string Prompt => "열쇠 줍기";
        public override bool Available => !_taken;

        /// <summary>열쇠를 놓는다(이미 열쇠가 있으면 놓지 않고 null).</summary>
        public static KeyPickup Place(Vector2 pos)
        {
            var state = WorldProps.State;
            var carry = ProfileCarry.Data;
            if ((state != null && state.HasKey) || (carry != null && carry.HasKey)) return null;
            var go = WorldProps.Root("KeyPickup", pos);
            var key = go.AddComponent<KeyPickup>();
            key._seed = Random.value * 10f;
            key.Build(pos);
            return key;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Ring", new Vector2(-0.18f, 0f), new Vector2(0.28f, 0.28f), ShapeSprites.Ring, Brass, order, false);
            WorldProps.Shape(transform, "Shaft", new Vector2(0.12f, 0f), new Vector2(0.42f, 0.07f), ShapeSprites.Square, Brass, order, false);
            WorldProps.Shape(transform, "Bit", new Vector2(0.3f, -0.06f), new Vector2(0.07f, 0.12f), ShapeSprites.Square, BrassDark, order + 1, false);
            _glint = WorldProps.Shape(transform, "Glint", new Vector2(0.05f, 0.08f), new Vector2(0.3f, 0.3f), WorldProps.SoftDot, GlintColor, order + 2, true);
            _glint.enabled = false;
        }

        public override void Interact()
        {
            if (_taken) return;
            var state = WorldProps.State;
            if (state != null)
            {
                state.HasKey = true;
                state.Complete(Id, DiscoveryKind.Ability, Position, Label);
            }
            Sfx.Play(SfxKind.Pickup);
            WorldOverlay.Text(Position + Vector2.up * 1.1f, Label, GainText);
            DungeonEvents.Say("광부 열쇠 꾸러미에서 떨어진 열쇠 하나 — 1층 광업소 사무실 문이 맞을 것 같다");
            _taken = true;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_taken || !_glint) return;
            bool show = WorldProps.PlayerDistance(Position) <= GlintRange;
            _glint.enabled = show;
            if (!show) return;
            float s = Mathf.Sin(Time.unscaledTime * 2.3f + _seed);
            var c = GlintColor;
            c.a = Mathf.Clamp01(s * 1.3f - 0.1f) * GlintColor.a;
            _glint.color = c;
        }
    }
}
