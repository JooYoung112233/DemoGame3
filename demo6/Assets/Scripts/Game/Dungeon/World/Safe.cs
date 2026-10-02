using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 광업소 금고(3차 초안 2-5, 4-2 한길의 열쇠). 1층 열쇠는 M0b에서 얻을 수 없어 잠긴 채 이유만 보인다.
    /// 열쇠가 있으면(시험 패널 등) F 2초 → 강화석 2 + 아이템 레벨 ÷ 2, 골드 15 + 6 × 아이템 레벨, 소리 반경 12.
    /// 장비 2개는 보상 모듈(작업자 A)의 몫이라 여기서는 주지 않는다. 아이템 레벨은 M0b에서 층과 같다.
    /// </summary>
    public sealed class Safe : Interactable
    {
        const float Hold = 2f;
        const float NoiseRadius = 12f;

        static readonly Color Steel = new Color(0.33f, 0.35f, 0.38f);
        static readonly Color SteelDark = new Color(0.2f, 0.21f, 0.23f);
        static readonly Color Dial = new Color(0.62f, 0.6f, 0.52f);
        static readonly Color GainText = new Color(1f, 0.9f, 0.6f);

        string _id;
        string _label;
        bool _opened;
        SpriteRenderer _door;
        SpriteRenderer _inside;

        public override string Prompt => "금고 열기";
        public override float HoldSeconds => Hold;
        public override float Range => 1.9f;
        public override bool Available => !_opened;
        public override string BlockedReason => WorldProps.HasKey ? null : "광업소 금고 — 열쇠가 필요하다";

        public static Safe Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("Safe " + f.Id, pos);
            var safe = go.AddComponent<Safe>();
            safe._id = f.Id;
            safe._label = string.IsNullOrEmpty(f.Label) ? "광업소 금고" : f.Label;
            safe.Build(pos);
            var state = WorldProps.State;
            if (state != null && state.IsDone(f.Id)) safe.ShowOpened();
            return safe;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Body", Vector2.zero, new Vector2(1.3f, 1.0f), ShapeSprites.Square, SteelDark, order, false);
            _inside = WorldProps.Shape(transform, "Inside", new Vector2(0f, 0f), new Vector2(1.0f, 0.75f), ShapeSprites.Square, new Color(0.06f, 0.06f, 0.07f), order + 1, false);
            _inside.enabled = false;
            _door = WorldProps.Shape(transform, "Door", Vector2.zero, new Vector2(1.1f, 0.85f), ShapeSprites.Square, Steel, order + 2, false);
            WorldProps.Shape(_door.transform, "Dial", new Vector2(0.15f, 0.05f), new Vector2(0.3f, 0.36f), ShapeSprites.Ring, Dial, order + 3, false);
            WorldProps.Shape(_door.transform, "Handle", new Vector2(-0.25f, 0.05f), new Vector2(0.08f, 0.35f), ShapeSprites.Square, Dial, order + 3, false);
            WorldProps.SolidBox(transform, "Collider", Vector2.zero, new Vector2(1.3f, 0.95f));
        }

        public override void Interact()
        {
            var state = WorldProps.State;
            if (_opened || state == null || !state.HasKey) return;
            int itemLevel = WorldProps.Floor;
            int stones = 2 + itemLevel / 2;
            int gold = 15 + 6 * itemLevel;
            state.Stones += stones;
            state.Gold += gold;
            Vector2 pos = transform.position;
            Sfx.Play(SfxKind.Chest);
            WorldOverlay.Text(pos + Vector2.up * 1.2f, "강화석 +" + stones + " · 골드 +" + gold, GainText);
            DungeonEvents.RaiseNoise(pos, NoiseRadius);
            state.Complete(_id, DiscoveryKind.Safe, pos, _label);
            DungeonEvents.Say("광업소 금고를 열었다");
            ShowOpened();
        }

        void ShowOpened()
        {
            _opened = true;
            if (_inside) _inside.enabled = true;
            if (_door)
            {
                // 문이 옆으로 열린 모습(시험용 도형).
                _door.transform.localPosition = new Vector3(-0.85f, 0f, 0f);
                _door.transform.localScale = new Vector3(0.35f, 0.85f, 1f);
            }
        }
    }
}
