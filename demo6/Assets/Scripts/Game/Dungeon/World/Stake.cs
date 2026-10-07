using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 권양기 말뚝(3차 초안 2-7, 매판 새 탐험 1차 2-3·2-4). 처음 닿으면(1.6유닛) 켜진다(경험치 3U는 PlayerProgress, 발견 주머니 안에서).
    /// 켠 말뚝에 닿으면 쓰러졌을 때 다시 설 곳이 된다. 시작 말뚝(DungeonRoot가 정한 LastStakeId)은 바구니로 내려온 자리라 첫 프레임에 켠다.
    /// 역할은 칸 조각으로 정한다(MapAnchors.RoleOf): 승강장 말뚝은 켜면 영구(꾸러미 LitLandings → ProfileDone),
    /// 계단 앞 말뚝은 이번 원정에만 켜지고, 켜면 아래층 승강장까지 바구니 줄이 닿는다(꾸러미 RopeDepth, 영구).
    /// F 메뉴(창 "stake"): 승강장 = ① 바구니로 올라가기 ② 같은 층 다른 켠 말뚝으로 ③ 닫기.
    /// 계단 앞 = ① 한 층 더 내려가기(계단 아래가 오우거 굴이면 '굴로 내려가기') ② 바구니로 올라가기 ③ 같은 층 다른 켠 말뚝으로 ④ 닫기. '원정 다시 시작'은 없앴다.
    /// 보스방 앞 쉼터 말뚝(굴 앞 말뚝, 전투·보스 문서 3-8)은 켜면 꾸러미 보스 기록에 영구로 남고(BossLedger) 메뉴는 승강장과 같다.
    /// 처치 뒤 보스방의 줄 끝 말뚝(OgreDen.EndStakeId)은 올라가기 글이 '바구니로 올라가기 — 시험판은 여기까지.'다.
    /// </summary>
    public sealed class Stake : Interactable
    {
        public const string ModalName = "stake";
        const float TouchRadius = 1.6f;
        const float PanelWidth = 600f;
        const float ButtonHeight = 40f;
        const float RowStep = 48f;

        // 2-2·2-3 글(인물 이름 없음).
        const string HeadNote = "같은 층 켠 말뚝 사이를 밧줄로 오간다. 잠시 어둠이 내리고, 원정은 이어진다.";
        const string AscendText = "바구니로 올라가기 — 오늘은 여기까지.";
        const string AscendNote = "밤사이 갱도가 다시 울릴 것이다. 돌로 쌓은 곳만 남는다. 능력·레벨·장비는 남는다.";
        /// <summary>
        /// 바닥에 남은 것(2-4, 검토 1차 Q3·2차 7-7): 골드·강화석·장비를 떠나며 저절로 거둔다. 가방이 차면 희귀 이상은 넘침 칸으로,
        /// 넘침 칸까지 차서 희귀 이상이 남으면 떠나기 전에 묻는다(DungeonRoot.AskBeforeLeaving).
        /// </summary>
        const string AscendWarning = "바닥의 골드·강화석·장비는 떠나며 거둬 간다.";
        const string DescendText = "한 층 더 내려가기";
        const string DescendBlocked = "아래는 아직 막혀 있다";
        /// <summary>굴 앞 말뚝을 처음 켬(첫 처치 전): 승강장 고르기에 '보스방 앞'이 생긴다(전투·보스 문서 3-8).</summary>
        const string BossFrontLitLine = "이제 권양기에서 '보스방 앞'으로 바로 내려올 수 있다.";

        static readonly Color PostWood = new Color(0.4f, 0.3f, 0.2f);
        static readonly Color PostBase = new Color(0.25f, 0.21f, 0.17f);
        static readonly Color IronRing = new Color(0.45f, 0.45f, 0.47f);
        static readonly Color GlowColor = new Color(1f, 0.88f, 0.6f, 0.55f);
        static readonly Color GlowCore = new Color(1f, 0.95f, 0.8f, 0.9f);

        string _id;
        string _label;
        string _cellName;
        StakeRole _role = StakeRole.Middle;
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
        /// <summary>승강장 / 계단 앞 / 가운데(칸 조각으로 정함).</summary>
        public StakeRole Role => _role;

        public static Stake Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("Stake " + f.Id, pos);
            var stake = go.AddComponent<Stake>();
            stake._id = f.Id;
            stake._label = string.IsNullOrEmpty(f.Label) ? "권양기 말뚝" : f.Label;
            stake._cellName = cell != null ? cell.Name : "";
            stake._role = cell != null ? MapAnchors.RoleOf(cell.Map) : StakeRole.Middle;
            stake._seed = Random.value * 10f;
            stake.Build(pos);
            var state = WorldProps.State;
            if (state != null)
            {
                // 꾸러미에서 켠 승강장(영구)은 처음부터 끝낸 것으로 등록된다. 같은 층 말뚝 목록에도 넣는다.
                bool done = state.IsDone(f.Id);
                if (done && !state.ActiveStakes.Contains(f.Id)) state.ActiveStakes.Add(f.Id);
                if (done || state.ActiveStakes.Contains(f.Id)) stake.ShowActive();
            }
            return stake;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Base", new Vector2(0f, 0.1f), new Vector2(0.45f, 0.45f), ShapeSprites.Circle, PostBase, order, false);
            WorldProps.Shape(transform, "Post", new Vector2(0f, 0.1f), new Vector2(0.3f, 0.3f), ShapeSprites.Square, PostWood, order + 1, false);
            WorldProps.Shape(transform, "Ring", new Vector2(0.05f, 0.1f), new Vector2(0.22f, 0.22f), ShapeSprites.Ring, IronRing, order + 2, false);
            WorldProps.Shape(transform, "Rope", new Vector2(0.25f, 0.11f), new Vector2(0.38f, 0.045f), ShapeSprites.Square, PostBase, order + 2, false, 12f);
            _glow = WorldProps.Shape(transform, "Glow", new Vector2(0f, 0.1f), new Vector2(1.3f, 1.3f), WorldProps.SoftDot, GlowColor, order + 3, true);
            _core = WorldProps.Shape(transform, "GlowCore", new Vector2(0f, 0.1f), new Vector2(0.22f, 0.22f), ShapeSprites.Circle, GlowCore, order + 4, true);
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
                // Visual light center follows the overhead cap; gameplay root, radius and intensity are unchanged.
                _light.transform.localPosition = new Vector3(0f, 0.1f, 0f);
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
            if (_role == StakeRole.StairsFront) HangRope();
            if (_role == StakeRole.BossFront) LightBossFront();
            DungeonEvents.RaiseStakeLit(WorldProps.Floor, _role == StakeRole.StairsFront);
        }

        /// <summary>
        /// 보스방 앞 쉼터 말뚝(굴 앞 말뚝)을 켬: 꾸러미 보스 기록에 영구로 적는다(BossLedger). 처음 적었고 아직 첫 처치 전이면
        /// 승강장 고르기에 '보스방 앞'이 생겼다는 글을 한 줄 띄운다.
        /// </summary>
        void LightBossFront()
        {
            var carry = ProfileCarry.Ensure();
            if (BossLedger.LightStake(carry, _id) && BossLedger.FrontLandingOpen(carry)) DungeonEvents.Say(BossFrontLitLine);
        }

        /// <summary>
        /// 계단 앞 말뚝을 켬: 아래층이 시험판에 있으면 바구니 줄이 그 층 승강장까지 닿는다(꾸러미 RopeDepth, 영구).
        /// 줄이 처음 더 깊이 닿을 때만 RopeExtended를 알린다(같은 깊이로 다시 켜면 글만).
        /// 계단 아래가 오우거 굴이면 줄은 늘지 않는다(굴은 계단으로 걸어서 간다).
        /// </summary>
        void HangRope()
        {
            int below = WorldProps.Floor + 1;
            if (!FloorRecipe.Exists(below))
            {
                DungeonEvents.Say(OgreDen.DescendsToDen(WorldProps.Floor)
                    ? "줄이 더 내려가지 않는다 — 계단 아래 굴은 걸어서 간다."
                    : "줄이 더 내려가지 않는다 — 아래는 다음 시험에서.");
                return;
            }
            var carry = ProfileCarry.Ensure();
            bool extended = below > carry.RopeDepth;
            if (extended) carry.RopeDepth = below;
            DungeonEvents.Say($"줄을 아래로 늘어뜨렸다 — 이제 바구니가 {below}층 승강장까지 내려온다.");
            if (extended) DungeonEvents.RaiseRopeExtended(below);
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
            var prevColor = GUI.color;
            DungeonUi.Begin();
            GUI.depth = -20;

            bool stairs = _role == StakeRole.StairsFront;
            string here = string.IsNullOrEmpty(_cellName) ? _label : _label + " (" + _cellName + ")";
            float titleHeight=Mathf.Max(36f,DungeonUi.Title.CalcHeight(new GUIContent("권양기 말뚝 — " + here),PanelWidth-112f));
            float extraHeader=Mathf.Max(0f,titleHeight-36f);
            int rows = Mathf.Max(1, _others.Count);
            float height = 82f                                   // 제목·머리 설명
                           + (stairs ? RowStep : 0f)              // 한 층 더 내려가기
                           + RowStep                              // 쉬기(원정에 한 번, 묶음 5-2)
                           + ButtonHeight + 8f + 40f + 24f + 18f  // 바구니로 올라가기·설명·경고
                           + 28f + rows * RowStep + 8f            // 같은 층 다른 켠 말뚝
                           + ButtonHeight + 18f + extraHeader;    // 여백·긴 제목
            var r = new Rect((DungeonUi.Width - PanelWidth) * 0.5f, (DungeonUi.Height - height) * 0.5f, PanelWidth, height);
            DungeonUi.Box(r, 0.93f);
            float x = r.x + 20f;
            float w = PanelWidth - 40f;
            GUI.Label(new Rect(x, r.y + 14f, w-72f, titleHeight), "권양기 말뚝 — " + here, DungeonUi.Title);
            GUI.Label(new Rect(x, r.y + 52f+extraHeader, w, 28f), HeadNote, DungeonUi.Small);

            float y = r.y + 88f+extraHeader;
            bool descend = false;
            if (stairs)
            {
                // 계단 아래가 오우거 굴이면 '굴로 내려가기'(같은 원정, 계단과 같음).
                string descendText = root.DescendsToDen ? OgreDen.StairsPrompt : DescendText;
                if (root.CanDescend) descend = GUI.Button(new Rect(x, y, w, ButtonHeight), descendText);
                else
                {
                    GUI.color = DungeonUi.BoneDim;
                    GUI.Label(new Rect(x, y + 8f, w, 24f), descendText + " — " + DescendBlocked, DungeonUi.Label);
                    GUI.color = prevColor;
                }
                y += RowStep;
            }

            // 쉬기(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5-2): 원정마다 한 번 체력·물약 가득.
            bool rest = false;
            if (root.CanRest) rest = GUI.Button(new Rect(x, y, w, ButtonHeight), DownRules.RestLabel);
            else
            {
                GUI.color = DungeonUi.BoneDim;
                GUI.Label(new Rect(x, y + 8f, w, 24f), DownRules.RestUsed, DungeonUi.Label);
                GUI.color = prevColor;
            }
            y += RowStep;

            // 보스 처치 뒤 줄 끝 말뚝(BossArena가 만듦)은 시험판 끝 글.
            string ascendText = _id == OgreDen.EndStakeId ? OgreDen.EndStakeText : root.AscendsToTown ? TownScript.StakeAscendLabel : AscendText;
            bool ascend = GUI.Button(new Rect(x, y, w, ButtonHeight), ascendText);
            y += ButtonHeight + 8f;
            GUI.Label(new Rect(x, y, w, 40f), AscendNote, DungeonUi.Small);
            y += 40f;
            GUI.color = DungeonUi.Rust;
            GUI.Label(new Rect(x, y, w, 22f), AscendWarning, DungeonUi.Small);
            GUI.color = prevColor;
            y += 24f + 18f;

            GUI.Label(new Rect(x, y, w, 22f), "같은 층 다른 켠 말뚝으로", DungeonUi.Bold);
            y += 28f;
            string travelTo = null;
            if (_others.Count == 0)
            {
                GUI.Label(new Rect(x, y + 8f, w, 22f), "이 층에 불 켜진 말뚝이 아직 이것뿐이다.", DungeonUi.Small);
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

            y += 8f;
            bool close = DungeonUi.CloseButton(r);
            GUI.Label(new Rect(x,y,w,28f),"[Esc] 닫기",DungeonUi.Small);
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;

            if (rest)
            {
                CloseMenu();
                if (root) root.RestAtStake();
            }
            else if (descend)
            {
                CloseMenu();
                if (root) root.Descend();
            }
            else if (ascend)
            {
                CloseMenu();
                if (root) root.Ascend();
            }
            else if (travelTo != null)
            {
                CloseMenu();
                if (root) root.TravelToStake(travelTo);
            }
            else if (close)
            {
                CloseMenu();
            }
        }
    }
}
