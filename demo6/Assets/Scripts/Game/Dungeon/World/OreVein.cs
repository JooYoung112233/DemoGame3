using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 광맥(3차 초안 2-5, 원정마다). 빛을 받는 바위에 빛을 무시하는 반짝임이 14유닛 안에서 보인다(2-8 '광맥 반짝임 14').
    /// 곡괭이로 F 1.8초 → 강화석 1 + 층 ÷ 4(내림). 캐는 소리(반경 12)가 가장 가까운 무리 하나를 깨운다.
    /// 원정마다 생기는 것이라 State.Complete 대신 Discovered(Ore)를 직접 알린다(경험치 없음, 기록용).
    /// '원정 다시 시작'(ExpeditionRestarted)이면 되살아난다.
    /// </summary>
    public sealed class OreVein : Interactable
    {
        const float Hold = 1.8f;
        const float GlintRange = 14f;
        const float NoiseRadius = 12f;
        const int Glints = 4;

        static readonly Color RockColor = new Color(0.36f, 0.33f, 0.31f);
        static readonly Color RockSpent = new Color(0.2f, 0.185f, 0.17f);
        static readonly Color VeinColor = new Color(0.5f, 0.6f, 0.7f);
        static readonly Color GlintColor = new Color(0.78f, 0.9f, 1f, 1f);
        static readonly Color StoneText = new Color(0.72f, 0.86f, 1f);

        static readonly Vector2[] GlintSpots =
        {
            new Vector2(-0.35f, 0.15f), new Vector2(0.2f, 0.28f), new Vector2(0.38f, -0.1f), new Vector2(-0.1f, -0.18f),
        };

        string _label;
        bool _depleted;
        float _seed;
        SpriteRenderer _rock;
        SpriteRenderer _rockTop;
        SpriteRenderer[] _veins;
        SpriteRenderer[] _glints;

        public override string Prompt => "광맥 캐기";
        public override float HoldSeconds => Hold;
        public override float Range => 1.9f;
        public override bool Available => !_depleted;
        public override string BlockedReason => WorldProps.HasPickaxe ? null : "곡괭이가 필요하다";
        public bool Depleted => _depleted;

        public static OreVein Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("OreVein " + f.Id, pos);
            var ore = go.AddComponent<OreVein>();
            ore._label = string.IsNullOrEmpty(f.Label) ? "광맥" : f.Label;
            ore._seed = Random.value * 10f;
            ore.Build(pos);
            return ore;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            _rock = WorldProps.Shape(transform, "Rock", Vector2.zero, new Vector2(1.5f, 1.05f), ShapeSprites.Circle, RockColor, order, false);
            _rockTop = WorldProps.Shape(transform, "RockTop", new Vector2(-0.12f, 0.12f), new Vector2(0.9f, 0.6f), ShapeSprites.Circle, Color.Lerp(RockColor, Color.white, 0.12f), order + 1, false);
            _veins = new SpriteRenderer[3];
            _veins[0] = WorldProps.Stroke(transform, "Vein", new Vector2(-0.5f, 0.1f), new Vector2(0.05f, 0.25f), 0.08f, VeinColor, order + 2, false);
            _veins[1] = WorldProps.Stroke(transform, "Vein", new Vector2(0.05f, 0.25f), new Vector2(0.45f, -0.12f), 0.08f, VeinColor, order + 2, false);
            _veins[2] = WorldProps.Stroke(transform, "Vein", new Vector2(-0.2f, -0.25f), new Vector2(0.2f, -0.05f), 0.07f, VeinColor, order + 2, false);
            _glints = new SpriteRenderer[Glints];
            for (int i = 0; i < Glints; i++)
            {
                _glints[i] = WorldProps.Shape(transform, "Glint", GlintSpots[i], new Vector2(0.22f, 0.22f), WorldProps.SoftDot, GlintColor, order + 3, true);
                _glints[i].enabled = false;
            }
            WorldProps.SolidBox(transform, "Collider", Vector2.zero, new Vector2(1.3f, 0.85f));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            DungeonEvents.ExpeditionRestarted += Restore;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DungeonEvents.ExpeditionRestarted -= Restore;
        }

        public override void Interact()
        {
            var state = WorldProps.State;
            if (_depleted || state == null || !state.HasPickaxe) return;
            _depleted = true;
            int stones = 1 + WorldProps.Floor / 4;
            state.Stones += stones;
            Vector2 pos = transform.position;
            WorldOverlay.Text(pos + Vector2.up * 1.1f, "강화석 +" + stones, StoneText);
            Sfx.Play(SfxKind.Pick);
            var player = PlayerController.Instance;
            Vector2 dir = player ? pos - player.Position : Vector2.up;
            WorldDebris.Burst(pos, new Vector2(1.1f, 0.7f), -dir, RockColor, VeinColor, 10);
            DungeonEvents.RaiseNoise(pos, NoiseRadius);
            DungeonEvents.RaiseDiscovered(DiscoveryKind.Ore, pos, _label);
            ApplyLook();
        }

        /// <summary>원정 다시 시작: 광맥이 다시 놓인다(3차 초안 2-7).</summary>
        void Restore()
        {
            _depleted = false;
            ApplyLook();
        }

        void ApplyLook()
        {
            if (_rock) _rock.color = _depleted ? RockSpent : RockColor;
            if (_rockTop) _rockTop.color = _depleted ? RockSpent : Color.Lerp(RockColor, Color.white, 0.12f);
            if (_veins != null)
                foreach (var v in _veins)
                    if (v) v.enabled = !_depleted;
            if (_depleted && _glints != null)
                foreach (var g in _glints)
                    if (g) g.enabled = false;
        }

        void Update()
        {
            if (_glints == null) return;
            bool show = !_depleted && WorldProps.PlayerDistance(transform.position) <= GlintRange;
            float t = Time.unscaledTime + _seed;
            for (int i = 0; i < _glints.Length; i++)
            {
                var g = _glints[i];
                if (!g) continue;
                g.enabled = show;
                if (!show) continue;
                // 반짝임이 번갈아 켜졌다 꺼진다.
                float s = Mathf.Sin(t * 2.4f + i * 1.9f);
                float a = Mathf.Clamp01(s * 1.4f - 0.2f);
                var c = GlintColor;
                c.a = a * 0.9f;
                g.color = c;
                g.transform.localScale = Vector3.one * (0.14f + 0.12f * a);
            }
        }
    }
}
