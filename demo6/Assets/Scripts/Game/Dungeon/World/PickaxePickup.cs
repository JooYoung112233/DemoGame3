using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 쓰러진 광부 자리의 곡괭이(3차 초안 4-2 탐험 능력, 1층 숨은 방 H, 약 4분). F로 주우면 State.HasPickaxe가 켜져
    /// 금 간 벽(K·V)과 광맥을 쓸 수 있다. 능력은 죽어도 잃지 않는다. 경험치는 이야기 물건과 같은 3U(PlayerProgress).
    /// 쓰러진 광부 그림자는 남고 곡괭이만 사라진다. 곡괭이 날 끝에 빛을 무시하는 작은 반짝임(8유닛 안).
    /// </summary>
    public sealed class PickaxePickup : Interactable
    {
        const float GlintRange = 8f;

        static readonly Color MinerCloth = new Color(0.25f, 0.23f, 0.21f);
        static readonly Color MinerHelmet = new Color(0.36f, 0.33f, 0.27f);
        static readonly Color Handle = new Color(0.5f, 0.36f, 0.22f);
        static readonly Color IronHead = new Color(0.56f, 0.57f, 0.6f);
        static readonly Color GlintColor = new Color(0.95f, 0.95f, 1f, 0.9f);
        static readonly Color GainText = new Color(1f, 0.92f, 0.7f);

        string _id;
        string _label;
        bool _taken;
        float _seed;
        Transform _pickaxe;
        SpriteRenderer _glint;

        public override string Prompt => "곡괭이 줍기";
        public override bool Available => !_taken;

        public static PickaxePickup Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("PickaxePickup " + f.Id, pos);
            var pick = go.AddComponent<PickaxePickup>();
            pick._id = f.Id;
            pick._label = string.IsNullOrEmpty(f.Label) ? "곡괭이" : f.Label;
            pick._seed = Random.value * 10f;
            pick.Build(pos);
            var state = WorldProps.State;
            if (state != null && (state.HasPickaxe || state.IsDone(f.Id))) pick.Hide();
            return pick;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            // 쓰러진 광부(옆으로 누운 그림자): 몸, 다리, 머리와 안전모.
            var miner = new GameObject("FallenMiner").transform;
            miner.SetParent(transform, false);
            miner.localPosition = new Vector3(-0.7f, 0.1f, 0f);
            WorldProps.Shape(miner, "Body", Vector2.zero, new Vector2(1.3f, 0.55f), ShapeSprites.Circle, MinerCloth, order, false, 8f);
            WorldProps.Shape(miner, "Legs", new Vector2(-0.85f, -0.12f), new Vector2(0.8f, 0.32f), ShapeSprites.Square, MinerCloth, order, false, 14f);
            WorldProps.Shape(miner, "Head", new Vector2(0.78f, 0.12f), new Vector2(0.42f, 0.42f), ShapeSprites.Circle, MinerCloth, order + 1, false);
            WorldProps.Shape(miner, "Helmet", new Vector2(0.86f, 0.2f), new Vector2(0.44f, 0.26f), ShapeSprites.Circle, MinerHelmet, order + 2, false);

            // 곡괭이: 자루 + 쇠 날.
            _pickaxe = new GameObject("Pickaxe").transform;
            _pickaxe.SetParent(transform, false);
            _pickaxe.localPosition = new Vector3(0.75f, -0.35f, 0f);
            _pickaxe.localRotation = Quaternion.Euler(0f, 0f, 35f);
            WorldProps.Shape(_pickaxe, "Handle", Vector2.zero, new Vector2(1.1f, 0.1f), ShapeSprites.Square, Handle, order + 3, false);
            WorldProps.Shape(_pickaxe, "Head", new Vector2(0.5f, 0f), new Vector2(0.14f, 0.72f), ShapeSprites.Square, IronHead, order + 4, false);
            WorldProps.Shape(_pickaxe, "Tip", new Vector2(0.5f, 0.42f), new Vector2(0.2f, 0.14f), ShapeSprites.Triangle, IronHead, order + 4, false, 90f);
            _glint = WorldProps.Shape(_pickaxe, "Glint", new Vector2(0.5f, 0.3f), new Vector2(0.3f, 0.3f), WorldProps.SoftDot, GlintColor, order + 5, true);
            _glint.enabled = false;
        }

        public override Vector2 Position => _pickaxe ? (Vector2)_pickaxe.position : (Vector2)transform.position;

        public override void Interact()
        {
            if (_taken) return;
            var state = WorldProps.State;
            if (state != null)
            {
                state.HasPickaxe = true;
                state.Complete(_id, DiscoveryKind.Ability, Position, _label);
            }
            Sfx.Play(SfxKind.Pickup);
            WorldOverlay.Text(Position + Vector2.up * 1.1f, "곡괭이", GainText);
            DungeonEvents.Say("피 묻은 낡은 곡괭이 — 금 간 벽과 광맥을 깰 수 있다");
            Hide();
        }

        void Hide()
        {
            _taken = true;
            if (_pickaxe) _pickaxe.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_taken || !_glint) return;
            bool show = WorldProps.PlayerDistance(Position) <= GlintRange;
            _glint.enabled = show;
            if (!show) return;
            float s = Mathf.Sin(Time.unscaledTime * 2.1f + _seed);
            var c = GlintColor;
            c.a = Mathf.Clamp01(s * 1.3f - 0.1f) * GlintColor.a;
            _glint.color = c;
        }
    }
}
