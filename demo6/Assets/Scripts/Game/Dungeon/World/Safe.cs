using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 광업소 금고(3차 초안 2-5, 4-2 한길의 열쇠). 1층 열쇠는 M0b에서 얻을 수 없어 잠긴 채 이유만 보인다.
    /// 열쇠가 있으면(오우거 유해 곁 열쇠 — 시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 나-9, 또는 시험 패널) F 2초 → LootRules.RollSafe:
    /// 장비 2, 강화석 2 + 아이템 레벨 ÷ 2, 골드 15 + 6 × 아이템 레벨을 플레이어 쪽으로 흩뿌린다(줍기는 LootPickup). 소리 반경 12. 아이템 레벨은 층과 같다.
    /// 룬(기획/세-무기-우클릭-소켓-1차.md 6-3): 500‰. 금고 씨앗(궤짝과 같은 (원정, id) 씨앗 ^ 프로필 소금) + RuneRules.ChestStream으로 굴려 플레이어 쪽으로 튀어나온다.
    /// </summary>
    public sealed class Safe : Interactable
    {
        const float Hold = 2f;
        const float NoiseRadius = 12f;
        /// <summary>금고 보상 난수 흐름(궤짝·룬 흐름과 겹치지 않음).</summary>
        const ulong SafeStream = 73;

        static readonly Color Steel = new Color(0.33f, 0.35f, 0.38f);
        static readonly Color SteelDark = new Color(0.2f, 0.21f, 0.23f);
        static readonly Color Dial = new Color(0.62f, 0.6f, 0.52f);
        static readonly Color GainText = new Color(1f, 0.9f, 0.6f);

        string _id;
        string _label;
        bool _opened;
        SpriteRenderer _door;
        Transform _pngDoorCarrier;
        SpriteRenderer _pngDoor;
        SpriteRenderer _pngControls;
        Sprite _pngOpenedDoor;
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
            if (TryBuildPng(pos)) return;
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Body", Vector2.zero, new Vector2(1.3f, 1.0f), ShapeSprites.Square, SteelDark, order, false);
            _inside = WorldProps.Shape(transform, "Inside", new Vector2(0f, 0f), new Vector2(1.0f, 0.75f), ShapeSprites.Square, new Color(0.06f, 0.06f, 0.07f), order + 1, false);
            _inside.enabled = false;
            _door = WorldProps.Shape(transform, "Door", Vector2.zero, new Vector2(1.1f, 0.85f), ShapeSprites.Square, Steel, order + 2, false);
            // Top-mounted dial stays circular after the parent lid scale; no upright front control panel.
            WorldProps.Shape(_door.transform, "Dial", new Vector2(0.15f, 0.05f), new Vector2(0.28f / 1.1f, 0.28f / 0.85f), ShapeSprites.Ring, Dial, order + 3, false);
            WorldProps.Shape(_door.transform, "Handle", new Vector2(-0.25f, 0.05f), new Vector2(0.28f / 1.1f, 0.07f / 0.85f), ShapeSprites.Square, Dial, order + 3, false);
            WorldProps.SolidBox(transform, "Collider", Vector2.zero, new Vector2(1.3f, 0.95f));
        }

        bool TryBuildPng(Vector2 pos)
        {
            if (!PropsV062Art.TryLoad(new[] {"safe-base", "safe-lid", "safe-lid-open", "safe-controls"}, out var sprites)) return false;
            int order = WorldProps.SortY(pos.y);
            PropsV062Art.Part(transform, "Body", sprites[0], Vector2.zero, new Vector2(1.3f, 1f), Color.white, order);
            var lid = PropsV062Art.Part(transform, "Door", sprites[1], Vector2.zero, new Vector2(1.1f, .85f), Color.white, order + 2);
            _pngDoorCarrier = lid.Carrier;
            _pngDoor = lid.Renderer;
            _pngOpenedDoor = sprites[2];
            // Keep the dial circular: fit the actual combined art aspect within the old pair's extent.
            var controlsSize = PropsV062Art.FitSize(sprites[3], new Vector2(.72f, .28f));
            _pngControls = PropsV062Art.Part(_pngDoorCarrier, "Controls", sprites[3], new Vector2(-.05f, .05f),
                new Vector2(controlsSize.x / 1.1f, controlsSize.y / .85f), Color.white, order + 3).Renderer;
            _pngControls.flipX = true; // The approved module has its dial on the left; preserve the existing dial-right layout.
            WorldProps.SolidBox(transform, "Collider", Vector2.zero, new Vector2(1.3f, .95f));
            return true;
        }

        public override void Interact()
        {
            var state = WorldProps.State;
            if (_opened || state == null || !state.HasKey) return;
            int itemLevel = WorldProps.Floor;
            Vector2 pos = transform.position;
            // 금고 보상(3차 초안 2-5): 장비 2 + 강화석 + 골드(LootRules.RollSafe). 궤짝과 같은 (원정, id) 씨앗 ^ 프로필 소금, 금고 흐름.
            ulong salt = state.RunSalt * 0x9E3779B97F4A7C15UL;
            var rng = new Pcg32Random(LootRules.ChestSeed(state.Expedition, _id ?? _label) ^ salt, SafeStream);
            var inventory = Inventory.Instance;
            var bundle = LootRules.RollSafe(itemLevel, rng, inventory ? inventory.RollContext() : null);
            var player = PlayerController.Instance;
            LootSpawner.Spawn(bundle, pos, player ? player.Position - pos : Vector2.down, 0.1f);
            Sfx.Play(SfxKind.Chest);
            WorldOverlay.Text(pos + Vector2.up * 1.2f, "장비 " + bundle.Gear.Count + " · 강화석 " + bundle.Stones + " · 골드 " + GoldTotal(bundle), GainText);
            DungeonEvents.RaiseNoise(pos, NoiseRadius);
            state.Complete(_id, DiscoveryKind.Safe, pos, _label);
            DungeonEvents.Say("광업소 금고가 무겁게 열린다");
            ShowOpened();
            DropRune(state, pos);
        }

        /// <summary>금고 룬(500‰, 난수 늘 2번). 나오면 플레이어 쪽으로 흩뿌린다(줍기는 LootPickup).</summary>
        void DropRune(DungeonState state, Vector2 pos)
        {
            ulong salt = state.RunSalt * 0x9E3779B97F4A7C15UL;
            var rng = new Pcg32Random(LootRules.ChestSeed(state.Expedition, _id ?? _label) ^ salt, RuneRules.ChestStream);
            var bundle = new LootBundle();
            if (RuneRules.AddTo(bundle, RuneSource.Safe, rng) == null) return;
            var player = PlayerController.Instance;
            LootSpawner.Spawn(bundle, pos, player ? player.Position - pos : Vector2.down, 0.1f);
        }

        static int GoldTotal(LootBundle bundle)
        {
            int n = 0;
            foreach (var g in bundle.GoldPiles) n += g;
            return n;
        }

        void ShowOpened()
        {
            _opened = true;
            if (_inside) _inside.enabled = true;
            if (_pngDoorCarrier)
            {
                _pngDoorCarrier.localPosition = new Vector3(-.785f, 0f, 0f);
                _pngDoorCarrier.localScale = new Vector3(.35f, .85f, 1f);
                PropsV062Art.SetSprite(_pngDoor, _pngOpenedDoor);
                if (_pngControls) _pngControls.enabled = false; // Top controls are hidden when the lid is edge-on.
            }
            if (_door)
            {
                // Open top lid seen edge-on; retain the existing opening transform and state.
                _door.transform.localPosition = new Vector3(-0.85f, 0f, 0f);
                _door.transform.localScale = new Vector3(0.35f, 0.85f, 1f);
            }
        }
    }
}
