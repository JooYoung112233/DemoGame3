using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 주민 한 명(기획/마을-의뢰-첫판.md 3-1·5-7·6-2·6-3, 꾸러미 3). 도형 임시 그림: 몸 원(반지름 0.42) + 머리 원(0.22, 몸보다 어둡게)
    /// + 바라보는 쪽 작은 삼각형 + 표식(지팡이·망치와 앞치마·긴 막대). 3유닛 안에 플레이어가 오면 그쪽으로 돌고, 늘 숨쉰다(크기 1.00~1.03).
    /// 충돌체는 두지 않는다(주민 사이를 지나갈 수 있음). 앞뒤는 Y 정렬(자리를 옮기지 않으므로 만들 때 한 번 정함).
    /// 머리 위 표시(OnGUI): 받을 의뢰 '!'(따뜻한 흰색, 움직이지 않음), 보고할 의뢰 '…'(베이지, 1.2초 주기 밝기 0.6~1.0), 둘 다면 '…'.
    /// 대화 중 말하는 주민은 ▼만 보인다(다른 표시는 감춤). <b>'?'는 이름표 전용이라 머리 위 표시로 절대 쓰지 않는다.</b> 머리 위 이름표도 띄우지 않는다.
    /// 바크: 반경 3 안에 들어오면 머리 위 1.5초, 같은 주민은 20초에 한 번, 이름표 없음(TalkDirector.BarkFor).
    /// F: 상호작용(InteractionSystem)으로 TalkWindow를 연다. 안내 글은 TalkDirector.Hint('F 춘삼 · 보고', 'F 말 걸기 · 의뢰')를 거친다.
    /// 화면 배치·반응형 다듬기는 Unity 개발 단계로 둔다(기능만).
    /// </summary>
    public sealed class TownNpc : Interactable
    {
        /// <summary>머리 위 표시 높이(유닛, 6-2).</summary>
        public const float MarkerHeight = 1.6f;
        /// <summary>바크 글 높이(유닛). 표시 위에 뜬다.</summary>
        public const float BarkHeight = 2.35f;
        /// <summary>'…' 밝기 주기(초)와 밝기 범위(6-2).</summary>
        public const float ReportPulseSeconds = 1.2f;
        public const float ReportPulseMin = 0.6f;
        /// <summary>숨쉬기 주기(초)와 크기 폭(1.00~1.03).</summary>
        const float BreathSeconds = 3.2f;
        const float BreathAmount = 0.03f;
        /// <summary>도는 빠르기(도/초).</summary>
        const float TurnSpeed = 420f;
        const float BodyRadius = 0.42f;
        const float HeadRadius = 0.22f;
        const int GuiDepth = 6;

        /// <summary>받을 의뢰 '!' 색(6-2: 따뜻한 흰색).</summary>
        public static readonly Color OfferColor = new Color(1f, 0.95f, 0.82f, 1f);
        /// <summary>보고할 의뢰 '…' 색(6-2: 베이지).</summary>
        public static readonly Color ReportColor = new Color(0.86f, 0.78f, 0.60f, 1f);
        /// <summary>말하는 주민 ▼ 색.</summary>
        public static readonly Color SpeakerColor = new Color(1f, 0.95f, 0.82f, 1f);
        static readonly Color BarkText = new Color(0.9f, 0.86f, 0.77f, 1f);
        static readonly Color BarkFill = new Color(0.03f, 0.028f, 0.026f, 0.82f);

        static readonly List<TownNpc> Live = new List<TownNpc>();
        static GUIStyle s_markStyle;
        static GUIStyle s_arrowStyle;
        static GUIStyle s_barkStyle;
        static readonly GUIContent s_measure = new GUIContent();

        /// <summary>지금 장면의 주민들.</summary>
        public static IReadOnlyList<TownNpc> Residents => Live;

        /// <summary>내부 ID(npc.gate 등). 화면에는 쓰지 않는다.</summary>
        public string NpcId { get; private set; } = "";
        public NpcDef Def { get; private set; }
        /// <summary>바라보는 쪽(단위 벡터).</summary>
        public Vector2 Facing { get; private set; } = Vector2.down;
        /// <summary>지금 보이는 바크 글(없으면 null). 시험 확인용.</summary>
        public string BarkShowing => Time.unscaledTime < _barkUntil ? _barkText : null;
        /// <summary>지금 머리 위 표시(대화 중 ▼는 따로). 시험 확인용.</summary>
        public QuestMarker Marker => Book != null ? Book.Marker(NpcId) : QuestMarker.None;

        Transform _breath;
        Transform _pivot;
        Vector2 _home;
        float _angle = -90f;
        float _seed;
        string _barkText;
        float _barkUntil = -1f;
        float _nextBark;
        bool _wasNear;
        QuestBook _book;

        public override float Range => TownLayout.NpcRadius;
        public override Vector2 Position => _home;
        public override bool Available => !TalkWindow.AnyOpen;

        /// <summary>F 안내: TalkDirector.Hint('F 춘삼 · 보고')에서 앞의 'F '를 뺀 몫(InteractionSystem이 '[F] '를 붙임).</summary>
        public override string Prompt
        {
            get
            {
                string hint = TalkDirector.Hint(NpcId, ProfileCarry.Ensure());
                // 무진에게 배울 기술이 있으면 ' · 기술'(점수·레벨·먼저 칸이 맞을 때만).
                if (NpcId == NpcTable.Trainer && PlayerProgress.Instance && PlayerProgress.Instance.AnyTrainerNode) hint += " · 기술";
                return hint.StartsWith("F ") ? hint.Substring(2) : hint;
            }
        }

        /// <summary>꾸러미가 바뀌면(새 프로필) 다시 만든다. 상태는 모두 꾸러미에 있어 매번 만들어도 같다.</summary>
        QuestBook Book
        {
            get
            {
                var carry = ProfileCarry.Ensure();
                if (_book == null || !ReferenceEquals(_book.Carry, carry)) _book = new QuestBook(carry);
                return _book;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Live.Clear();
            s_markStyle = null;
        }

        /// <summary>
        /// 장면 정적 비우기(SceneStatics.Reset이 부름). 사라진 주민만 목록에서 뺀다(부르는 차례가 Spawn 뒤여도 새 장면 주민은 남음).
        /// F 목록(Interactable.All)은 Interactable.ResetStatics가 따로 비운다.
        /// </summary>
        public static void ResetResidents() => Live.RemoveAll(n => !n);

        /// <summary>
        /// 주민을 만든다(꾸러미 3 약속 TownNpc.Spawn(npcId, 자리)). 표에 없는 id면 null.
        /// parent가 있으면 그 아래에 둔다(자리는 월드 좌표).
        /// </summary>
        public static TownNpc Spawn(string npcId, Vector2 position, Transform parent = null)
        {
            var def = NpcTable.Get(npcId);
            if (def == null)
            {
                Debug.LogWarning($"[주민] 표에 없는 주민 id '{npcId}'");
                return null;
            }
            var go = new GameObject("Npc " + def.Id);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var npc = go.AddComponent<TownNpc>();
            npc.Init(def, position);
            return npc;
        }

        /// <summary>표의 평소 자리(TownLayout.NpcHome)에 만든다.</summary>
        public static TownNpc Spawn(string npcId, Transform parent = null)
        {
            var home = TownLayout.NpcHome(npcId);
            return Spawn(npcId, new Vector2(home.X, home.Y), parent);
        }

        /// <summary>표의 주민 셋을 모두 평소 자리에 만든다.</summary>
        public static List<TownNpc> SpawnAll(Transform parent = null)
        {
            var list = new List<TownNpc>();
            foreach (var def in NpcTable.All)
            {
                var npc = Spawn(def.Id, parent);
                if (npc) list.Add(npc);
            }
            return list;
        }

        /// <summary>이 장면의 주민(없으면 null).</summary>
        public static TownNpc Find(string npcId)
        {
            for (int i = 0; i < Live.Count; i++)
                if (Live[i] && Live[i].NpcId == npcId) return Live[i];
            return null;
        }

        /// <summary>
        /// 거리와 쿨다운을 따지지 않고 바크를 띄운다(새 플레이 0.5초 뒤 춘삼 "어이, 이쪽!", 첫 귀환 도착 카드를 닫은 뒤 옥금 "살아 왔네.").
        /// 화면 밖 주민이면 바크 글을 화면 가장자리에 붙여 보인다. 그 주민이 없으면 false.
        /// </summary>
        public static bool BarkNow(string npcId, string text = null)
        {
            var npc = Find(npcId);
            if (!npc) return false;
            npc.Bark(text);
            return true;
        }

        void Init(NpcDef def, Vector2 position)
        {
            Def = def;
            NpcId = def.Id;
            _home = position;
            _seed = Random.value * 10f;
            Build();
            ApplyFacing();
        }

        /// <summary>도형을 만든다. 몸·머리는 숨쉬기 마디(_breath), 삼각형·표식·앞치마는 바라보는 쪽 마디(_pivot, +x가 앞)에 단다.</summary>
        void Build()
        {
            if (NpcId == NpcTable.Smith && TownNpcIdleV057.Attach(transform, "blacksmith", Vector2.zero)) return;
            var body = Hex(Def.BodyHex, new Color(0.42f, 0.35f, 0.28f));
            var head = new Color(body.r * 0.68f, body.g * 0.68f, body.b * 0.68f, 1f);
            var mark = Hex(Def.MarkHex, new Color(0.75f, 0.7f, 0.6f));
            int order = WorldProps.SortY(_home.y);

            _breath = new GameObject("Breath").transform;
            _breath.SetParent(transform, false);
            _pivot = new GameObject("Facing").transform;
            _pivot.SetParent(_breath, false);

            WorldProps.Shape(_breath, "Body", Vector2.zero, Vector2.one * BodyRadius * 2f, ShapeSprites.Circle, body, order, false);
            if (!string.IsNullOrEmpty(Def.ApronHex))
                WorldProps.Shape(_pivot, "Apron", new Vector2(0.2f, 0f), new Vector2(0.2f, 0.52f), ShapeSprites.Square, Hex(Def.ApronHex, body), order + 1, false);
            WorldProps.Shape(_pivot, "Head", new Vector2(0.04f, 0f), Vector2.one * HeadRadius * 2f, ShapeSprites.Circle, head, order + 3, false);
            // 바라보는 쪽 삼각형은 어두운 땅에서도 보이게 몸과 표식 사이 색.
            WorldProps.Shape(_pivot, "Nose", new Vector2(BodyRadius + 0.07f, 0f), new Vector2(0.16f, 0.16f), ShapeSprites.Triangle, Color.Lerp(body, mark, 0.5f), order + 1, false);

            // 표식은 오른손 쪽(앞이 +x일 때 −y)에 든다.
            switch (Def.Mark)
            {
                case NpcMark.Staff:
                    WorldProps.Stroke(_pivot, "Staff", new Vector2(-0.3f, -0.44f), new Vector2(0.62f, -0.44f), 0.05f, mark, order + 2, false);
                    break;
                case NpcMark.Hammer:
                    WorldProps.Stroke(_pivot, "HammerGrip", new Vector2(-0.04f, -0.42f), new Vector2(0.42f, -0.42f), 0.06f, mark, order + 2, false);
                    WorldProps.Stroke(_pivot, "HammerHead", new Vector2(0.44f, -0.56f), new Vector2(0.44f, -0.28f), 0.12f, mark, order + 2, false);
                    break;
                case NpcMark.Pole:
                    WorldProps.Stroke(_pivot, "Pole", new Vector2(-0.7f, -0.46f), new Vector2(0.95f, -0.46f), 0.06f, mark, order + 2, false);
                    break;
            }
        }

        static Color Hex(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!Live.Contains(this)) Live.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Live.Remove(this);
        }

        public override void Interact()
        {
            var window = TalkWindow.Ensure();
            if (window && !window.Talk(NpcId))
            {
                // 할 말이 없어도 무진은 배울 기술이 있으면 배우기 창을 연다(기획/스킬-자원-트리-1차.md 2장).
                if (NpcId == NpcTable.Trainer && SkillPanel.OpenTeach()) return;
                Debug.LogWarning($"[주민] {NpcId} 대화 창을 열지 못했다(다른 창: {DungeonUi.Modal})");
            }
        }

        /// <summary>바크를 띄운다(text가 없으면 TalkDirector.BarkFor). 쿨다운(20초)을 다시 건다.</summary>
        public void Bark(string text = null)
        {
            text = string.IsNullOrEmpty(text) ? TalkDirector.BarkFor(NpcId, ProfileCarry.Ensure()) : text;
            if (string.IsNullOrEmpty(text)) return;
            _barkText = text;
            _barkUntil = Time.unscaledTime + TownScript.BarkSeconds;
            _nextBark = Time.unscaledTime + TownScript.BarkCooldown;
        }

        void Update()
        {
            var player = PlayerController.Instance;
            float dist = player ? (player.Position - _home).magnitude : float.PositiveInfinity;
            bool talking = TalkWindow.AnyOpen;
            bool inTalk = talking && TalkWindow.Instance.Involves(NpcId);

            // 돌기: 3유닛 안이거나 이 주민이 대화에 나오면 플레이어 쪽, 아니면 평소 방향(남쪽). 대화 중에는 시간이 멈춰도 돈다.
            Vector2 want = Vector2.down;
            if (player && (dist <= TownLayout.NpcTurnRadius || inTalk) && dist > 0.01f) want = (player.Position - _home) / dist;
            float target = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
            _angle = Mathf.MoveTowardsAngle(_angle, target, TurnSpeed * Time.unscaledDeltaTime);
            ApplyFacing();

            float breath = 1f + BreathAmount * (0.5f + 0.5f * Mathf.Sin((Time.unscaledTime + _seed) * Mathf.PI * 2f / BreathSeconds));
            if (_breath) _breath.localScale = new Vector3(breath, breath, 1f);

            // 바크: 반경 안으로 들어올 때 한 번(서 있는 동안 되풀이하지 않음), 같은 주민은 20초에 한 번. 창이 열려 있으면 띄우지 않는다.
            bool near = dist <= TownScript.BarkRadius;
            if (near && !_wasNear && !talking && !DungeonUi.ModalOpen && Time.unscaledTime >= _nextBark) Bark();
            _wasNear = near;
        }

        void ApplyFacing()
        {
            if (_pivot) _pivot.localRotation = Quaternion.Euler(0f, 0f, _angle);
            float r = _angle * Mathf.Deg2Rad;
            Facing = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        /// <summary>머리 위 표시·▼·바크. 누르는 것이 없어 그리기 사건에서만 그린다.</summary>
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = GuiDepth;
            var markAt = DungeonUi.WorldToGui(_home + Vector2.up * MarkerHeight);
            if (markAt != null)
            {
                string arrow = TalkWindow.ArrowNpcId;
                if (arrow == NpcId) DrawGlyph(markAt.Value, "▼", s_arrowStyle, SpeakerColor);
                else
                {
                    var marker = Marker;
                    if (marker == QuestMarker.Report)
                    {
                        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / ReportPulseSeconds);
                        var c = ReportColor * Mathf.Lerp(ReportPulseMin, 1f, t);
                        c.a = 1f;
                        DrawGlyph(markAt.Value, QuestMarkers.Glyph(marker), s_markStyle, c);
                    }
                    else if (marker == QuestMarker.Offer) DrawGlyph(markAt.Value, QuestMarkers.Glyph(marker), s_markStyle, OfferColor);
                }
            }
            // 창(대화·도착 카드·밝아짐 막)이 떠 있으면 GUI 차례로 그 아래에 가려진다. 새 바크는 창이 없을 때만 시작한다(Update).
            if (Time.unscaledTime < _barkUntil) DrawBark();
        }

        static void DrawGlyph(Vector2 at, string glyph, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(glyph)) return;
            DungeonUi.ShadowLabel(new Rect(at.x - 30f, at.y - 24f, 60f, 44f), glyph, style, color);
        }

        /// <summary>바크 글: 작은 검은 판 위 뼈색 글, 끝 0.3초 동안 흐려진다. 화면 밖이면 가장자리에 붙인다.</summary>
        void DrawBark()
        {
            var at = DungeonUi.WorldToGui(_home + Vector2.up * BarkHeight);
            if (at == null) return;
            s_measure.text = _barkText;
            var size = s_barkStyle.CalcSize(s_measure);
            float w = size.x + 24f;
            float h = Mathf.Max(30f, size.y + 10f);
            var r = DungeonUi.KeepOnScreen(new Rect(at.Value.x - w * 0.5f, at.Value.y - h, w, h));
            float a = Mathf.Clamp01((_barkUntil - Time.unscaledTime) / 0.3f);
            DungeonUi.Fill(r, new Color(BarkFill.r, BarkFill.g, BarkFill.b, BarkFill.a * a));
            DungeonUi.ShadowLabel(r, _barkText, s_barkStyle, new Color(BarkText.r, BarkText.g, BarkText.b, a));
        }

        static void EnsureStyles()
        {
            if (s_markStyle != null && s_markStyle.font == DungeonUi.Center.font) return;
            s_markStyle = new GUIStyle(DungeonUi.Center) { fontSize = 34, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            s_markStyle.normal.textColor = Color.white;
            s_arrowStyle = new GUIStyle(s_markStyle) { fontSize = 24 };
            s_barkStyle = new GUIStyle(DungeonUi.Center) { fontSize = 17, wordWrap = false, clipping = TextClipping.Overflow };
            s_barkStyle.normal.textColor = Color.white;
        }
    }
}
