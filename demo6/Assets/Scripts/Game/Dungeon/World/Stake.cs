using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 권양기 말뚝(3차 초안 2-7). 처음 닿으면(1.6유닛) 켜지고 영구로 남는다(경험치 3U는 PlayerProgress). 켠 말뚝에 닿으면 쓰러졌을 때 다시 설 곳이 된다.
    /// 시작 말뚝(DungeonRoot가 정한 LastStakeId)은 바구니로 내려온 자리라 첫 프레임에 켠다.
    /// F 메뉴(창 "stake"): ① 다른 켠 말뚝으로(1초 암전, 원정 계속) ② 원정 다시 시작(M0b의 귀환: 적·광맥을 다시 놓음) ③ 닫기.
    /// 마을로·저장하고 끝내기는 M0b에서 뺀다.
    /// </summary>
    public sealed class Stake : Interactable
    {
        public const string ModalName = "stake";
        const float TouchRadius = 1.6f;
        const float PanelWidth = 580f;
        const float ButtonHeight = 40f;
        const float RowStep = 48f;

        static readonly Color PostWood = new Color(0.4f, 0.3f, 0.2f);
        static readonly Color PostBase = new Color(0.25f, 0.21f, 0.17f);
        static readonly Color IronRing = new Color(0.45f, 0.45f, 0.47f);
        static readonly Color GlowColor = new Color(1f, 0.88f, 0.6f, 0.55f);
        static readonly Color GlowCore = new Color(1f, 0.95f, 0.8f, 0.9f);

        string _id;
        string _label;
        string _cellName;
        bool _active;
        bool _inside;
        bool _menuOpen;
        float _seed;
        SpriteRenderer _glow;
        SpriteRenderer _core;
        Light2D _light;
        readonly List<string> _others = new List<string>();

        public override string Prompt => "권양기 말뚝";
        /// <summary>켜기 전에는 메뉴가 없다(닿으면 켜짐).</summary>
        public override bool Available => _active;
        public bool Active => _active;
        public string Id => _id;

        public static Stake Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("Stake " + f.Id, pos);
            var stake = go.AddComponent<Stake>();
            stake._id = f.Id;
            stake._label = string.IsNullOrEmpty(f.Label) ? "권양기 말뚝" : f.Label;
            stake._cellName = cell != null ? cell.Name : "";
            stake._seed = Random.value * 10f;
            stake.Build(pos);
            var state = WorldProps.State;
            if (state != null && (state.IsDone(f.Id) || state.ActiveStakes.Contains(f.Id))) stake.ShowActive();
            return stake;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Base", new Vector2(0f, -0.05f), new Vector2(0.75f, 0.28f), ShapeSprites.Circle, PostBase, order, false);
            WorldProps.Shape(transform, "Post", new Vector2(0f, 0.5f), new Vector2(0.3f, 1.1f), ShapeSprites.Square, PostWood, order + 1, false);
            WorldProps.Shape(transform, "Ring", new Vector2(0f, 0.95f), new Vector2(0.42f, 0.42f), ShapeSprites.Ring, IronRing, order + 2, false);
            WorldProps.Shape(transform, "Rope", new Vector2(0.12f, 0.62f), new Vector2(0.05f, 0.6f), ShapeSprites.Square, PostBase, order + 2, false, 12f);
            _glow = WorldProps.Shape(transform, "Glow", new Vector2(0f, 0.95f), new Vector2(1.3f, 1.3f), WorldProps.SoftDot, GlowColor, order + 3, true);
            _core = WorldProps.Shape(transform, "GlowCore", new Vector2(0f, 0.95f), new Vector2(0.22f, 0.22f), ShapeSprites.Circle, GlowCore, order + 4, true);
            _glow.enabled = false;
            _core.enabled = false;
            WorldProps.SolidBox(transform, "Collider", new Vector2(0f, 0.1f), new Vector2(0.45f, 0.45f));
        }

        /// <summary>켠 모습(빛을 무시하는 작은 불빛 + 약한 점 조명).</summary>
        void ShowActive()
        {
            _active = true;
            if (_glow) _glow.enabled = true;
            if (_core) _core.enabled = true;
            if (!_light)
            {
                _light = DungeonLighting.Point(transform, "Stake glow", 0.4f, 2.4f, 0.35f, DungeonLighting.LampColor, false);
                _light.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            }
        }

        void Start()
        {
            // 시작 말뚝: 바구니로 내려온 자리라 바로 켠다(계약서: 첫 프레임에 켜져도 됨).
            var state = WorldProps.State;
            if (!_active && state != null && state.LastStakeId == _id) Activate(state);
        }

        void Activate(DungeonState state)
        {
            ShowActive();
            Sfx.Play(SfxKind.Stake);
            if (!state.ActiveStakes.Contains(_id)) state.ActiveStakes.Add(_id);
            state.LastStakeId = _id;
            state.Complete(_id, DiscoveryKind.Stake, transform.position, _label);
            DungeonEvents.Say("녹슨 권양기가 삐걱인다 — 쓰러지면 여기서 다시 선다");
        }

        void Update()
        {
            if (_menuOpen)
            {
                if (DungeonUi.Modal != ModalName) _menuOpen = false;
                else
                {
                    var kb = Keyboard.current;
                    if (kb != null && kb.escapeKey.wasPressedThisFrame) CloseMenu();
                }
            }

            var state = WorldProps.State;
            var player = PlayerController.Instance;
            if (state != null && player && !player.IsDown)
            {
                bool inside = (player.Position - (Vector2)transform.position).sqrMagnitude <= TouchRadius * TouchRadius;
                if (inside && !_inside)
                {
                    if (!_active) Activate(state);
                    else state.LastStakeId = _id;
                }
                _inside = inside;
            }

            if (_active && _glow)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f + _seed);
                var c = GlowColor;
                c.a = GlowColor.a * (0.7f + 0.3f * pulse);
                _glow.color = c;
                _glow.transform.localScale = Vector3.one * (1.2f + 0.15f * pulse);
            }
        }

        public override void Interact()
        {
            if (!_active) return;
            var state = WorldProps.State;
            if (state != null) state.LastStakeId = _id;
            if (DungeonUi.TryOpen(ModalName)) _menuOpen = true;
        }

        void CloseMenu()
        {
            _menuOpen = false;
            DungeonUi.Close(ModalName);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_menuOpen) CloseMenu();
        }

        string StakeName(DungeonState state, string id)
        {
            if (state.OneTime.TryGetValue(id, out var e))
            {
                string label = string.IsNullOrEmpty(e.Label) ? "권양기 말뚝" : e.Label;
                return e.Cell != null ? label + " (" + e.Cell.Name + ")" : label;
            }
            return id;
        }

        void OnGUI()
        {
            if (!_menuOpen) return;
            if (DungeonUi.Modal != ModalName)
            {
                _menuOpen = false;
                return;
            }
            var root = DungeonRoot.Instance;
            var state = root ? root.State : null;
            if (state == null) return;

            _others.Clear();
            foreach (var id in state.ActiveStakes)
                if (id != _id) _others.Add(id);

            var prevMatrix = GUI.matrix;
            DungeonUi.Begin();
            GUI.depth = -20;

            int rows = Mathf.Max(1, _others.Count);
            float height = 104f + rows * RowStep + 20f + RowStep + 44f + RowStep + 16f;
            var r = new Rect((DungeonUi.Width - PanelWidth) * 0.5f, (DungeonUi.Height - height) * 0.5f, PanelWidth, height);
            DungeonUi.Box(r, 0.93f);
            float x = r.x + 20f;
            float w = PanelWidth - 40f;
            string here = string.IsNullOrEmpty(_cellName) ? _label : _label + " (" + _cellName + ")";
            GUI.Label(new Rect(x, r.y + 12f, w, 30f), "권양기 말뚝 — " + here, DungeonUi.Title);
            GUI.Label(new Rect(x, r.y + 46f, w, 22f), "밧줄을 타고 켠 말뚝 사이를 오간다. 잠시 어둠이 내리고, 원정은 이어진다.", DungeonUi.Small);

            float y = r.y + 76f;
            GUI.Label(new Rect(x, y, w, 22f), "다른 켠 말뚝으로", DungeonUi.Bold);
            y += 28f;
            string travelTo = null;
            if (_others.Count == 0)
            {
                GUI.Label(new Rect(x, y + 8f, w, 22f), "불 켜진 말뚝이 아직 이것뿐이다.", DungeonUi.Small);
                y += RowStep;
            }
            else
            {
                foreach (var id in _others)
                {
                    if (GUI.Button(new Rect(x, y, w, ButtonHeight), "이동: " + StakeName(state, id))) travelTo = id;
                    y += RowStep;
                }
            }

            y += 20f;
            bool restart = GUI.Button(new Rect(x, y, w, ButtonHeight), "원정 다시 시작 — 쓰러뜨린 것들과 광맥이 돌아온다");
            y += RowStep;
            GUI.Label(new Rect(x, y - 4f, w, 40f), "상처를 싸매고 물약을 채운 뒤 이 말뚝에서 다시 내려간다. 지도, 켠 말뚝·등잔, 연 곳, 능력, 레벨은 남는다.", DungeonUi.Small);
            y += 44f;
            bool close = GUI.Button(new Rect(x, y, w, ButtonHeight), "닫기 (Esc)");
            GUI.matrix = prevMatrix;

            if (travelTo != null)
            {
                CloseMenu();
                if (root) root.TravelToStake(travelTo);
            }
            else if (restart)
            {
                CloseMenu();
                if (root) root.RestartExpedition(_id);
            }
            else if (close)
            {
                CloseMenu();
            }
        }
    }
}
