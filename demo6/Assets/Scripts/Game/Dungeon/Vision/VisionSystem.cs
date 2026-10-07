using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 좀보이드식 시야(사용자 요청 2026-10-03 "범위 빛 밖이 보이네, 좀보이드 같은 것처럼 처리해 줘").
    /// ① 벽이 시야를 가린다: 플레이어에서 광선을 쏴 시야 다각형을 만든다(벽 면은 0.45 들어가 보이게).
    ///    고른 간격 광선에 더해 벽·기둥 모서리를 정확히 겨냥한 광선을 쏴, 걸을 때 그림자 경계가 떨리지 않게 한다.
    ///    시야와 문 1차 3장 보는 칸 가두기: 광선은 '보는 칸' 경계 0.5 밖(벽 바깥 면, SightRules.ClipPad)까지만 간다. 문 건너편은 문틀 끝에서 끊기고
    ///    안 가 본 다음 방은 넘어가기 전까지 검다. 보는 칸은 몸이 경계를 0.3 넘게 지나야 바뀌고(문턱 3-2) 광선보다 먼저 정한다.
    ///    원점이 넓힌 칸 사각형 경계 위·밖이면(순간 이동 사이) ExitDistance가 0이라 그 프레임은 가두지 않는다(장막이 검게 빠지지 않게).
    ///    보는 칸 사각형 네 귀도 모서리 광선으로 쏜다. 바뀐 뒤 0.25초 동안 부채꼴 반경이 몸 둘레 반경에서 펼쳐진다(3-3).
    /// ② 바라보는 쪽(커서) 부채꼴 130°·14유닛 + 몸 둘레 2.5유닛은 늘 본다. 부채꼴 옆면은 10°에 걸쳐 부드럽게 줄어든다.
    ///    웅크리면(결정 ③, PlayerController.Crouching) 반각 40°·반경 10·몸 둘레 2.0(Tuning.Crouch*)으로 0.15초에 걸쳐 좁아진다.
    ///    광선 길이·모서리 광선·덮개는 모두 지금(섞인) 반경을 쓴다.
    /// ③ 안 가 본 곳은 완전히 검게(기억 안개), 가 봤지만 지금 안 보이는 곳은 어둡게 덮는다(덮개 0.70, 1-3, 기억으로만 보이는 지형).
    ///    기억 안개는 0.25유닛 칸에 '봤다'를 적고, 바뀐 곳만 5×5로 흐려 올려 경계가 계단처럼 보이지 않게 한다.
    ///    시야와 문 1차 5-3 살핌: 칸마다 걸을 수 있는 바닥 가운데 '봤다'가 된 수를 세어 75%가 차면 DungeonEvents.CellSwept(새 칸 경험치)를 알린다.
    /// ④ 적은 부채꼴 안(반각 + 5° + 몸 각도, 한 번 보인 적은 6° 더)이면서 시야 다각형 안일 때만 보인다(시야와 문 1차 4-1·4-2).
    ///    부채꼴 옆면에서 다각형이 줄어드는 곳은 줄이지 않은 시야(벽·보는 칸·부채꼴 반경)로 잰다(6° 여유가 살게).
    ///    몸 둘레 2.5 원은 바닥·벽만 보여 주므로 등 뒤 적은 등잔 빛 안이어도 안 보인다. 보는 칸이 보스방이면 방 안 360°(4-7, 벽·돌기둥은 가림).
    ///    나를 때린 안 보이는 적은 0.8초, 내가 직접 친 적은 0.5초 보인다(4-3). 보여도 빛(등잔 흐린 반경·켠 벽 등잔) 밖이면 몸 대신 눈 두 점만(3차 초안 2-4).
    /// 예고 그림은 주인 적이 안 보이면 그리지 않는다(4-5, Telegraph). 빛기둥·투사체는 지워지지 않고 시야 밖에서는 어둡게만 보인다(공정 규칙).
    /// 깜빡임(8-2): 장막 속성 블록·MeshRenderer·그리는 차례(5000·14000)와 '광선 3개보다 적으면 지난 장막 유지'는 그대로다. 칸 가두기는 광선 길이만 줄인다.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class VisionSystem : MonoBehaviour
    {
        /// <summary>서 있을 때 시야(ExplorePace): 반경 14, 몸 둘레 2.5, 부채꼴 반각 65°. 웅크리면 CurrentViewRadius 등이 Tuning.Crouch*로 섞인다.</summary>
        public const float ViewRadius = ExplorePace.ViewRadius;
        public const float NearRadius = ExplorePace.NearRadius;
        public const float ConeHalfAngle = ExplorePace.ConeHalfAngle;
        /// <summary>부채꼴 옆면이 14 → 2.5로 줄어드는 각도 폭(칼로 자른 경계 대신 부드럽게).</summary>
        const float ConeSoft = 10f;
        /// <summary>벽에 닿은 광선을 벽 안으로 더 들여 벽 면이 보이게(벽 두께 1, 기둥 1.2보다 작게).</summary>
        const float WallReveal = 0.45f;
        const float ConeStep = 0.6f;
        const float RearStep = 3f;
        /// <summary>모서리 광선을 모서리 양옆으로 이만큼 비켜 쏜다(도).</summary>
        const float CornerNudge = 0.05f;
        const float Feather = 0.9f;
        /// <summary>가 본 곳 덮개(1-3, 예전 0.62).</summary>
        const float VeilAlpha = ExplorePace.VeilAlpha;
        const float FarRadius = 45f;
        /// <summary>기억 안개 칸 크기.</summary>
        const float MemoryCell = 0.25f;
        /// <summary>기억 안개 흐림 반경(칸). 2 = 5×5.</summary>
        const int BlurRadius = 2;
        const float TurnSpeed = 540f;
        /// <summary>어둡게 덮기: 바닥·벽·물체·예고·효과 위, 눈빛(20000) 아래.</summary>
        const int VeilOrder = 5000;
        /// <summary>기억 안개: 안 가 본 곳을 덮는 맨 위 판.</summary>
        const int FogOrder = 14000;
        /// <summary>보는 칸 밖에서 숨긴 그대로인 적은 이 간격(실제 초)마다만 렌더러를 다시 모은다(시야와 문 1차 8-1).</summary>
        const float HiddenRefreshSeconds = 0.25f;
        /// <summary>드러남·숨김 기록에서 죽거나 지워진 적을 비우는 간격(실제 초).</summary>
        const float PruneSeconds = 2f;

        public static VisionSystem Instance { get; private set; }

        /// <summary>시험 패널: 끄면 전부 보인다(안개·덮개·적 숨기기 없음).</summary>
        public bool VisionOn { get; set; } = true;
        /// <summary>시험 패널: 끄면 360°로 지금 시야 반경(서 있으면 14, 웅크리면 10)까지 본다.</summary>
        public bool ConeOn { get; set; } = true;
        /// <summary>시험 패널(시야와 문 1차 13장 '보는 칸 밖 가리기'): 끄면 시야가 보는 칸에 갇히지 않는다(문 건너편·옆 칸도 광선이 닿는 만큼, 예전과 같음).</summary>
        public bool RoomClipOn { get; set; } = true;
        /// <summary>시험 패널('적은 부채꼴 안만 보임'): 끄면 적도 시야 다각형 안이면 보인다(4-1 이전과 같음).</summary>
        public bool EnemyConeOn { get; set; } = true;
        /// <summary>시험 패널('칸 살핌 경험치'): 끄면 새 칸 경험치를 들어설 때 준다(5-3). 끄는 순간 들어갔지만 못 살핀 칸은 바로 준다.</summary>
        public bool SweepXpOn { get; set; } = true;
        /// <summary>보는 칸(3-2): 시야가 갇히는 칸. 몸이 경계를 0.3 넘게 지나야 바뀐다. 이름 알림·작은 지도가 쓰는 DungeonRoot.CurrentCell과 따로 돈다.</summary>
        public DungeonCell ViewCell => _viewCell;
        public Vector2 Origin => _origin;
        public float LookDeg => _lookDeg;
        /// <summary>웅크림 섞기(0 = 서 있음, 1 = 웅크림, 0.15초에 걸쳐 바뀜).</summary>
        public float CrouchBlend => _crouchBlend;
        /// <summary>지금 시야 반경·몸 둘레·부채꼴 반각(웅크림 섞기 포함). 광선·덮개가 이 값을 쓴다.</summary>
        public float CurrentViewRadius => _viewRadius;
        public float CurrentNearRadius => _nearRadius;
        public float CurrentConeHalfAngle => _coneHalfAngle;

        /// <summary>시야를 가리지 않는 작은 물체 충돌체(말뚝·광맥·금고). 벽 레이어지만 좀보이드의 가구처럼 시야는 통과시킨다.</summary>
        static readonly HashSet<Collider2D> SeeThrough = new HashSet<Collider2D>();
        /// <summary>WallBetween이 쓰는 광선 결과 칸(값을 남기지 않는 빈 그릇이라 다시 불러오기 없이도 안전).</summary>
        static readonly List<RaycastHit2D> LineHits = new List<RaycastHit2D>(8);
        static readonly int ColorId = Shader.PropertyToID("_Color");
        /// <summary>첫 급습 안내(시야와 문 1차 4-9)를 이번 플레이(게임을 켠 동안)에 띄웠는가. 장면을 다시 불러와도 남고 플레이 시작 때만 되돌린다.</summary>
        static bool _ambushHinted;

        /// <summary>숨긴 적의 렌더러를 마지막으로 모은 기록(8-1): 다음에 다시 모을 실제 시각, 그때 빛 무시 표식을 남겼나.</summary>
        struct HiddenStamp
        {
            public float Next;
            public bool Markers;
        }

        readonly List<float> _rayAngle = new List<float>(1024);
        readonly List<float> _rayDist = new List<float>(1024);
        readonly List<float> _sample = new List<float>(1024);
        readonly List<RaycastHit2D> _hits = new List<RaycastHit2D>(8);
        readonly List<Renderer> _renderers = new List<Renderer>(16);
        readonly List<Vector2> _litLamps = new List<Vector2>(8);
        readonly List<Vector2> _corners = new List<Vector2>(1024);
        readonly List<Collider2D> _wallCols = new List<Collider2D>(512);
        readonly List<Vector3> _verts = new List<Vector3>(4096);
        readonly List<Color32> _colors = new List<Color32>(4096);
        readonly List<int> _tris = new List<int>(16384);

        ContactFilter2D _wallFilter;
        Vector2 _origin;
        float _lookDeg;
        bool _hasLook;
        /// <summary>커서가 몸 가까이라 바라보는 쪽을 붙잡고 있는가(0.6 안에서 켜고 1.0 밖에서 끔).</summary>
        bool _cursorNear;
        float _crouchBlend;
        float _viewRadius = ViewRadius;
        float _nearRadius = NearRadius;
        float _coneHalfAngle = ConeHalfAngle;
        bool _cornersDirty = true;
        Rect _worldBounds;

        Mesh _veilMesh;
        MeshRenderer _veilRenderer;

        Texture2D _fogTex;
        Sprite _fogSpriteAsset;
        /// <summary>칸마다 봤는가(0/1).</summary>
        byte[] _seen;
        /// <summary>올리는 안개 알파(흐린 결과, 255 = 검음).</summary>
        byte[] _fogAlpha;
        int[] _rowSum;
        SpriteRenderer _fogSprite;
        Vector2 _fogMin;
        int _fogW;
        int _fogH;
        int _dirtyMinX, _dirtyMinY, _dirtyMaxX, _dirtyMaxY;
        bool _fogDirty;

        // ── 시야와 문 1차 ──
        /// <summary>DungeonRoot가 지은 세계(Init에서 읽음). 없으면 보는 칸·살핌 없이 예전처럼 돈다.</summary>
        DungeonWorld _world;
        /// <summary>보는 칸(3-2)과 바뀐 실제 시각(3-3 펼침).</summary>
        DungeonCell _viewCell;
        float _viewChangedAt = -999f;
        /// <summary>이 프레임 칸 가두기 사각형(보는 칸 + ClipPad)과 가두는가(원점이 사각형 안일 때만, 3-1).</summary>
        Rect _clipRect;
        bool _clipping;
        /// <summary>이 프레임 부채꼴 반경(새 칸 펼침 포함, 3-3). 펼침이 끝나면 _viewRadius와 같다.</summary>
        float _coneRadius = ViewRadius;
        /// <summary>적이 부채꼴과 관계없이 보이는 끝 실제 시각(4-3 맞으면 드러남).</summary>
        readonly Dictionary<Enemy, float> _revealUntil = new Dictionary<Enemy, float>();
        readonly Dictionary<Enemy, HiddenStamp> _hiddenStamps = new Dictionary<Enemy, HiddenStamp>();
        readonly List<Enemy> _staleEnemies = new List<Enemy>();
        float _nextPrune;
        /// <summary>살핌(5-3): 기억 안개 칸마다 걸을 수 있는 바닥이면 칸 번호(World.Cells 차례), 아니면 −1. 장면마다 한 번 만든다.</summary>
        short[] _sweepCell;
        readonly Dictionary<DungeonCell, int> _cellNumber = new Dictionary<DungeonCell, int>();
        /// <summary>칸마다 '봤다'가 된 바닥 수, 살핌에 필요한 수(75%), 다 찼나.</summary>
        int[] _sweepSeen;
        int[] _sweepNeeded;
        bool[] _sweepFull;
        /// <summary>미루기가 켜진 채 처음 들어가 새 칸 경험치가 살핌으로 미뤄진 칸, 이미 알린 칸(장면에 한 번).</summary>
        bool[] _sweepOwed;
        bool[] _sweepRaised;
        /// <summary>다 찼지만 아직 알리지 않은 칸 번호.</summary>
        readonly List<int> _sweepPending = new List<int>();
        /// <summary>지난 프레임에 새 칸 경험치를 미뤘나(VisionOn && SweepXpOn). 꺼지는 프레임에 미룬 칸을 바로 준다.</summary>
        bool _deferWasOn = true;

        public static void RegisterSeeThrough(Collider2D col)
        {
            if (col) SeeThrough.Add(col);
        }

        public static void ResetStatics() => SeeThrough.Clear();

        /// <summary>첫 급습 안내는 한 판(게임을 켠 동안)에 한 번이라 장면 비우기(ResetStatics)가 아니라 플레이 시작 때만 되돌린다(도메인 다시 불러오기 꺼짐).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAmbushHint() => _ambushHinted = false;

        /// <summary>
        /// 두 점 사이를 벽이 막는가(말뚝·광맥·금고처럼 시야를 가리지 않는 작은 물체는 무시). 벽 등잔 빛 판정과
        /// 싸움 판정(ExploreWalk.AwakeEnemyInSight)이 같은 규칙을 쓴다.
        /// </summary>
        public static bool WallBetween(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (!(len >= 0.001f)) return false;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Layers.WallMask);
            int n = Physics2D.Raycast(from, d / len, filter, LineHits, len);
            for (int i = 0; i < n; i++)
                if (LineHits[i].collider && !SeeThrough.Contains(LineHits[i].collider)) return true;
            return false;
        }

        void Awake()
        {
            Instance = this;
            _wallFilter = new ContactFilter2D { useTriggers = false };
            _wallFilter.SetLayerMask(Layers.WallMask);

            var veil = new GameObject("Vision veil");
            veil.transform.SetParent(transform, false);
            _veilMesh = new Mesh { name = "Vision veil" };
            _veilMesh.MarkDynamic();
            veil.AddComponent<MeshFilter>().sharedMesh = _veilMesh;
            _veilRenderer = veil.AddComponent<MeshRenderer>();
            _veilRenderer.sharedMaterial = RenderMaterials.Unlit;
            _veilRenderer.sortingOrder = VeilOrder;
            // 스프라이트 셰이더를 MeshRenderer로 그리면 SRP Batcher가 unity_SpriteColor·unity_SpriteProps를 채우지 않아
            // 장막이 규칙 없이 빠졌다 그려졌다 했다(2026-10-04 화면 픽셀 재현: 주변광↔부채꼴).
            // 속성 블록을 붙이면 이 렌더러만 Batcher 밖에서 그려져 두 값이 늘 기본값이다.
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, Color.white);
            _veilRenderer.SetPropertyBlock(block);
            // 판자벽·금 간 벽이 열리면 모서리가 바뀐다.
            DungeonEvents.EdgeOpened += OnEdgeOpened;
            // 시야와 문 1차: 맞으면 드러남(4-3)·첫 급습 안내(4-9), 새 칸 경험치를 살핌으로 미룬 칸(5-3).
            CombatEvents.PlayerDamaged += OnPlayerDamaged;
            CombatEvents.PlayerDealtDamage += OnPlayerDealt;
            DungeonEvents.CellEntered += OnCellEntered;
            // 4-6 등 뒤 기척·깨는 기척·예고 기척(지금 있는 합성음, 새 소리 파일 없음).
            if (!GetComponent<BehindSounds>()) gameObject.AddComponent<BehindSounds>();
        }

        void OnDestroy()
        {
            DungeonEvents.EdgeOpened -= OnEdgeOpened;
            CombatEvents.PlayerDamaged -= OnPlayerDamaged;
            CombatEvents.PlayerDealtDamage -= OnPlayerDealt;
            DungeonEvents.CellEntered -= OnCellEntered;
            if (Instance == this) Instance = null;
            if (_veilMesh) Destroy(_veilMesh);
            if (_fogSpriteAsset) Destroy(_fogSpriteAsset);
            if (_fogTex) Destroy(_fogTex);
        }

        void OnEdgeOpened(DungeonEdge edge) => _cornersDirty = true;

        /// <summary>DungeonRoot가 지도를 만든 뒤 부른다: 지도 전체를 덮는 기억 안개 판을 만든다.</summary>
        public void Init(Rect worldBounds)
        {
            _worldBounds = worldBounds;
            _fogMin = worldBounds.min - Vector2.one;
            _fogW = Mathf.CeilToInt((worldBounds.width + 2f) / MemoryCell);
            _fogH = Mathf.CeilToInt((worldBounds.height + 2f) / MemoryCell);
            // 알파만 쓰는 1바이트 판(검은색으로 칠해 그린다).
            _fogTex = new Texture2D(_fogW, _fogH, TextureFormat.Alpha8, false)
            {
                name = "Vision memory",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _seen = new byte[_fogW * _fogH];
            _fogAlpha = new byte[_fogW * _fogH];
            _rowSum = new int[_fogW * _fogH];
            for (int i = 0; i < _fogAlpha.Length; i++) _fogAlpha[i] = 255;
            _fogTex.SetPixelData(_fogAlpha, 0);
            _fogTex.Apply(false);
            var go = new GameObject("Vision memory fog");
            go.transform.SetParent(transform, false);
            go.transform.position = _fogMin;
            _fogSprite = go.AddComponent<SpriteRenderer>();
            _fogSpriteAsset = Sprite.Create(_fogTex, new Rect(0, 0, _fogW, _fogH), Vector2.zero, 1f / MemoryCell);
            _fogSprite.sprite = _fogSpriteAsset;
            _fogSprite.color = Color.black;
            _fogSprite.sortingOrder = FogOrder;
            RenderMaterials.MakeUnlit(_fogSprite);
            _cornersDirty = true;
            // 시야와 문 1차: 보는 칸(3-2)·살핌(5-3)은 지은 세계를 읽는다(DungeonRoot가 World를 지은 뒤 Init을 부른다).
            _world = DungeonRoot.Instance ? DungeonRoot.Instance.World : null;
            BuildSweepTable();
        }

        /// <summary>시야 다각형 안인가(pad만큼 넉넉하게). 시야를 끄면 늘 참.</summary>
        public bool IsVisible(Vector2 p, float pad = 0f)
        {
            if (!VisionOn || _rayAngle.Count < 3) return true;
            Vector2 to = p - _origin;
            float d = to.magnitude;
            if (d <= pad) return true;
            return d - pad <= LimitAt(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 보는 칸 경계 + ClipPad(0.5) 안인가(시야와 문 1차 3-1, 칸 가두기를 꺼도 같은 뜻). 보는 칸을 아직 못 정했으면 참.
        /// 등 뒤 기척(BehindSounds)·'우적' 글(H2)이 읽는다.
        /// </summary>
        public bool InViewCell(Vector2 p)
        {
            if (_viewCell == null) return true;
            var b = _viewCell.Bounds;
            float pad = SightRules.ClipPad;
            return p.x >= b.xMin - pad && p.x <= b.xMax + pad && p.y >= b.yMin - pad && p.y <= b.yMax + pad;
        }

        /// <summary>
        /// 새 칸 경험치를 살핌으로 미루는가(5-3): 시야·살핌 손잡이가 켜졌고 보스방이 아닌 칸. PlayerProgress가 새 칸 발견 때 읽는다.
        /// 살핌 표가 없으면(세계 없이 Init) 미루지 않는다(미룬 경험치를 줄 길이 없으므로).
        /// </summary>
        public bool DefersCellXp(DungeonCell cell) =>
            VisionOn && SweepXpOn && cell != null && _sweepCell != null && SightRules.SweepApplies(cell.Piece);

        /// <summary>그 칸 살핌 진행(0~1, 1 = 살폈다: 걸을 수 있는 바닥의 75%를 봄, 5-3). 시험 패널 읽기 줄·지도가 읽는다.</summary>
        public float SweepProgress(DungeonCell cell)
        {
            if (cell == null || _sweepCell == null || !_cellNumber.TryGetValue(cell, out int c)) return 0f;
            if (_sweepNeeded[c] <= 0) return 1f;
            return Mathf.Clamp01((float)_sweepSeen[c] / _sweepNeeded[c]);
        }

        /// <summary>적을 seconds(실제 초) 동안 부채꼴과 관계없이 보이게 한다(4-3). 벽 가림·빛 규칙은 그대로다. 이미 더 길게 드러나 있으면 그대로 둔다.</summary>
        public void Reveal(Enemy e, float seconds)
        {
            if (!e || !(seconds > 0f)) return;
            float until = Time.unscaledTime + seconds;
            if (_revealUntil.TryGetValue(e, out float old) && old >= until) return;
            _revealUntil[e] = until;
        }

        /// <summary>그 방향 시야 다각형 경계까지 거리(이웃 광선 사이를 직선으로 이음).</summary>
        float LimitAt(float angleDeg)
        {
            float start = _rayAngle[0];
            float rel = Mathf.Repeat(angleDeg - start, 360f) + start;
            int hi = _rayAngle.BinarySearch(rel);
            if (hi >= 0) return _rayDist[hi];
            hi = ~hi;
            int lo = hi - 1;
            float aLo, aHi, dLo, dHi;
            if (hi >= _rayAngle.Count)
            {
                aLo = _rayAngle[_rayAngle.Count - 1];
                dLo = _rayDist[_rayAngle.Count - 1];
                aHi = _rayAngle[0] + 360f;
                dHi = _rayDist[0];
            }
            else
            {
                aLo = _rayAngle[lo];
                dLo = _rayDist[lo];
                aHi = _rayAngle[hi];
                dHi = _rayDist[hi];
            }
            float t = aHi - aLo > 0.0001f ? (rel - aLo) / (aHi - aLo) : 0f;
            return Mathf.Lerp(dLo, dHi, t);
        }

        void LateUpdate()
        {
            // 5-3: 미루기가 꺼지는 프레임(시야·살핌 손잡이)에 들어갔지만 못 살핀 칸의 경험치를 바로 준다.
            bool defer = VisionOn && SweepXpOn;
            if (_deferWasOn && !defer) PayOwedSweeps();
            _deferWasOn = defer;
            var player = PlayerController.Instance;
            if (!player) return;
            // 3-2: 보는 칸은 광선보다 먼저 정한다. 시야를 꺼도 따라가게 둔다(시험 패널 읽기 줄·등 뒤 기척이 읽음).
            UpdateViewCell(player.transform.position);
            bool on = VisionOn;
            if (_veilRenderer) _veilRenderer.enabled = on;
            if (_fogSprite) _fogSprite.enabled = on;
            if (!on)
            {
                ShowAllEnemies();
                return;
            }
            if (_cornersDirty) CollectCorners();
            // 그림·등잔·카메라가 따르는 보간된 위치(물리 위치를 쓰면 50Hz로 끊겨 움직인다).
            _origin = player.transform.position;
            UpdateCrouch(player);
            if (!TimeScaleService.Paused) UpdateLook(player);
            UpdateClip();
            BuildRays();
            MarkMemory();
            FlushSweeps();
            BuildVeil();
            if (_fogDirty) UploadFog();
            UpdateEnemies();
        }

        /// <summary>
        /// 보는 칸(시야와 문 1차 3-2): 몸이 보던 칸 경계를 SwitchPad(0.3)보다 더 벗어나면 몸이 있는 칸으로 바꾼다(문턱에서 오가도 그대로).
        /// 몸이 어느 칸에도 없으면(순간 이동 사이) 보던 칸 그대로. 광선 원점과 같은 보간 위치로 재야 원점이 늘 넓힌 칸 사각형(ClipPad 0.5) 안에 있다.
        /// 처음 정할 때(장면 시작)는 펼치지 않는다.
        /// </summary>
        void UpdateViewCell(Vector2 at)
        {
            if (_world == null) return;
            if (_viewCell != null)
            {
                var b = _viewCell.Bounds;
                if (SightRules.KeepViewCell(at.x, at.y, b.xMin, b.yMin, b.xMax, b.yMax)) return;
            }
            var cell = _world.CellAt(at);
            if (cell == null || cell == _viewCell) return;
            _viewChangedAt = _viewCell == null ? -999f : Time.unscaledTime;
            _viewCell = cell;
        }

        /// <summary>
        /// 이 프레임 칸 가두기(3-1)와 새 칸 펼침(3-3). 사각형은 보는 칸을 ClipPad만큼 넓힌 것(경계 벽의 바깥 면)이다.
        /// 원점이 그 사각형 경계 위·밖이면 SightRules.ExitDistance가 0을 내므로 그 프레임은 가두지 않는다(장막이 검게 빠지지 않게, 물결 1 검토).
        /// 펼침은 부채꼴 반경만 몸 둘레 반경에서 늘린다(몸 둘레는 줄이지 않음). 가두기를 끄면 예전과 같다(펼침도 없음).
        /// </summary>
        void UpdateClip()
        {
            _clipping = false;
            _coneRadius = _viewRadius;
            if (!RoomClipOn || _viewCell == null) return;
            var b = _viewCell.Bounds;
            float pad = SightRules.ClipPad;
            _clipRect = Rect.MinMaxRect(b.xMin - pad, b.yMin - pad, b.xMax + pad, b.yMax + pad);
            _clipping = _origin.x > _clipRect.xMin && _origin.x < _clipRect.xMax && _origin.y > _clipRect.yMin && _origin.y < _clipRect.yMax;
            _coneRadius = SightRules.RevealRadius(Time.unscaledTime - _viewChangedAt, _nearRadius, _viewRadius);
        }

        /// <summary>
        /// 웅크리면 시야가 좁아진다(결정 ③): 반각 65° → Tuning.CrouchConeHalfAngle(40°), 반경 14 → CrouchViewRadius(10),
        /// 몸 둘레 2.5 → CrouchNearRadius(2.0). CrouchRules.BlendSeconds(0.15초, unscaled)에 걸쳐 부드럽게 바뀐다.
        /// </summary>
        void UpdateCrouch(PlayerController player)
        {
            _crouchBlend = CrouchRules.StepBlend(_crouchBlend, player.Crouching, Time.unscaledDeltaTime);
            _viewRadius = CrouchRules.Blend(ViewRadius, Tuning.CrouchViewRadius, _crouchBlend);
            _nearRadius = CrouchRules.Blend(NearRadius, Tuning.CrouchNearRadius, _crouchBlend);
            _coneHalfAngle = CrouchRules.Blend(ConeHalfAngle, Tuning.CrouchConeHalfAngle, _crouchBlend);
        }

        /// <summary>
        /// 바라보는 쪽 = 커서 방향. 너무 튀지 않게 초당 540°까지만 돈다. 카메라가 몸을 화면 가운데에 두므로(2026-10-04 깜빡임 수정)
        /// ① 커서가 몸 0.6 안으로 들어오면 방향을 붙잡고 1.0 밖으로 나가야 다시 따른다(ExplorePace.CursorNear).
        /// ② '가까움'은 몸과 화면 가운데(카메라 중심) 중 가까운 쪽으로 잰다. 구르기(지연 최대 약 1.2)·내딛기·넉백에 카메라가 늦게 따라오는 동안
        ///    화면 가운데에 둔 커서가 몸에서 1유닛 넘게 떨어져 방향이 뒤집히던 것을 막는다(마우스를 움직이지 않으면 화면 가운데 거리는 그대로다).
        ///    휘두르기·구르기 중에도 커서를 따른다(이어 치는 동안 부채꼴이 멈춰 옆 적이 안 보이지 않게).
        /// ③ 커서·각도가 NaN이면 지난 방향을 그대로 둔다.
        /// 커서가 없으면(마우스 없음) 몸이 향한 쪽을 따른다.
        /// </summary>
        void UpdateLook(PlayerController player)
        {
            if (float.IsNaN(_lookDeg) || float.IsInfinity(_lookDeg)) _hasLook = false;
            Vector2 dir = player.FacingDirection;
            var cam = Camera.main;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (cam && mouse != null)
            {
                Vector3 sp = mouse.position.ReadValue();
                sp.z = -cam.transform.position.z;
                Vector2 cursor = cam.ScreenToWorldPoint(sp);
                Vector2 toCursor = cursor - _origin;
                // 보스방 고정 카메라(DungeonCamera.IsFixed)는 화면 가운데가 방 가운데라 몸과 상관없다: 몸 거리만 쓴다(NaN).
                var rig = DungeonRoot.Instance ? DungeonRoot.Instance.CameraRig : null;
                float fromCenter = rig && rig.IsFixed ? float.NaN : (cursor - (Vector2)cam.transform.position).magnitude;
                _cursorNear = ExplorePace.CursorNear(_cursorNear, ExplorePace.CursorNearDistance(toCursor.magnitude, fromCenter));
                if (_hasLook && _cursorNear) return;
                if (!_cursorNear) dir = toCursor;
            }
            if (!(dir.sqrMagnitude >= 0.0001f)) return;
            float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (float.IsNaN(target) || float.IsInfinity(target)) return;
            if (!_hasLook)
            {
                _lookDeg = target;
                _hasLook = true;
                return;
            }
            _lookDeg = Mathf.MoveTowardsAngle(_lookDeg, target, TurnSpeed * Time.unscaledDeltaTime);
        }

        /// <summary>시야를 막는 벽·기둥·문 막이의 모서리(축 정렬 상자 네 귀). 벽이 열리면 다시 모은다.</summary>
        void CollectCorners()
        {
            _cornersDirty = false;
            _corners.Clear();
            _wallCols.Clear();
            var area = _worldBounds;
            Physics2D.OverlapArea(area.min - Vector2.one * 2f, area.max + Vector2.one * 2f, _wallFilter, _wallCols);
            foreach (var col in _wallCols)
            {
                if (!col || SeeThrough.Contains(col)) continue;
                var b = col.bounds;
                _corners.Add(new Vector2(b.min.x, b.min.y));
                _corners.Add(new Vector2(b.min.x, b.max.y));
                _corners.Add(new Vector2(b.max.x, b.min.y));
                _corners.Add(new Vector2(b.max.x, b.max.y));
            }
        }

        /// <summary>
        /// 그 방향 최대 시야 거리: 부채꼴 안 14, 밖 2.5, 옆면 10°에 걸쳐 부드럽게(웅크리면 10 / 2.0, 반각 40°).
        /// 부채꼴 반경은 새 칸 펼침(시야와 문 1차 3-3) 동안 몸 둘레 반경에서 늘어난다(_coneRadius).
        /// </summary>
        float MaxRadius(float angleDeg)
        {
            if (!ConeOn) return _coneRadius;
            float off = Mathf.Abs(Mathf.DeltaAngle(angleDeg, _lookDeg));
            float t = Mathf.Clamp01((off - _coneHalfAngle) / ConeSoft);
            return Mathf.Lerp(_coneRadius, _nearRadius, t * t * (3f - 2f * t));
        }

        void BuildRays()
        {
            _sample.Clear();
            float look = _lookDeg;
            float start = look - 180f;
            float wide = _coneHalfAngle + ConeSoft;
            for (float a = start; a < start + 360f - 0.001f;)
            {
                _sample.Add(a);
                bool inCone = !ConeOn || Mathf.Abs(Mathf.DeltaAngle(a, look)) <= wide;
                a += inCone ? ConeStep : RearStep;
            }
            // 모서리 광선: 모서리 바로 앞뒤로 쏴 그림자 경계를 정확히 잡는다.
            float reach = _viewRadius + 1f;
            foreach (var c in _corners)
            {
                // 시야와 문 1차 3-1: 가두는 동안 보는 칸 사각형 밖 모서리에는 광선이 닿지 않으므로 쏘지 않는다(광선 수를 줄임).
                if (_clipping && !NearClipRect(c)) continue;
                AddCornerRays(c, start, reach);
            }
            if (_clipping)
            {
                // 3-1: 보는 칸 사각형 네 귀도 모서리 광선으로 쏴 칸 경계가 곧게 잘리게 한다.
                AddCornerRays(new Vector2(_clipRect.xMin, _clipRect.yMin), start, reach);
                AddCornerRays(new Vector2(_clipRect.xMin, _clipRect.yMax), start, reach);
                AddCornerRays(new Vector2(_clipRect.xMax, _clipRect.yMin), start, reach);
                AddCornerRays(new Vector2(_clipRect.xMax, _clipRect.yMax), start, reach);
            }
            _sample.Sort();
            _rayAngle.Clear();
            _rayDist.Clear();
            float last = float.NegativeInfinity;
            foreach (float a in _sample)
            {
                if (a - last < 0.01f || a >= start + 360f || a < start) continue;
                last = a;
                Ray(a, MaxRadius(a));
            }
        }

        /// <summary>모서리 c 바로 앞뒤(±CornerNudge)로 광선 각도를 더한다. 닿지 않는 먼 모서리는 건너뛴다.</summary>
        void AddCornerRays(Vector2 c, float start, float reach)
        {
            Vector2 to = c - _origin;
            float d2 = to.sqrMagnitude;
            if (d2 > reach * reach || d2 < 0.0001f) return;
            float a = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            if (Mathf.Sqrt(d2) > MaxRadius(a) + 1f) return;
            float rel = Mathf.Repeat(a - start, 360f) + start;
            _sample.Add(rel - CornerNudge);
            _sample.Add(rel + CornerNudge);
        }

        /// <summary>칸 가두기 사각형 안이거나 경계 위인가(벽 바깥 면 모서리가 경계 위에 있어 조금 넉넉하게).</summary>
        bool NearClipRect(Vector2 p)
        {
            const float eps = 0.05f;
            return p.x >= _clipRect.xMin - eps && p.x <= _clipRect.xMax + eps && p.y >= _clipRect.yMin - eps && p.y <= _clipRect.yMax + eps;
        }

        void Ray(float angleDeg, float maxR)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            if (_clipping)
            {
                // 시야와 문 1차 3-1: 보는 칸 경계 0.5 밖(벽 바깥 면)까지만 간다. 0은 원점이 사각형 위·밖이라는 뜻이라 자르지 않는다.
                float exit = SightRules.ExitDistance(_origin.x, _origin.y, dir.x, dir.y, _clipRect.xMin, _clipRect.yMin, _clipRect.xMax, _clipRect.yMax);
                if (exit > 0f && exit < maxR) maxR = exit;
            }
            float d = maxR;
            int n = Physics2D.Raycast(_origin, dir, _wallFilter, _hits, maxR);
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (!h.collider || SeeThrough.Contains(h.collider)) continue;
                d = Mathf.Min(d, h.distance + WallReveal);
            }
            _rayAngle.Add(angleDeg);
            _rayDist.Add(Mathf.Min(maxR, d));
        }

        /// <summary>시야에 든 칸을 '봤다'로 적는다(흐림은 올릴 때).</summary>
        void MarkMemory()
        {
            if (_seen == null) return;
            float step = MemoryCell * 0.7f;
            for (int i = 0; i < _rayAngle.Count; i++)
            {
                float r = _rayAngle[i] * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
                float len = _rayDist[i];
                for (float t = 0f; t <= len; t += step) See(_origin + dir * t);
                See(_origin + dir * len);
            }
        }

        void See(Vector2 p)
        {
            int x = Mathf.FloorToInt((p.x - _fogMin.x) / MemoryCell);
            int y = Mathf.FloorToInt((p.y - _fogMin.y) / MemoryCell);
            if (x < 0 || y < 0 || x >= _fogW || y >= _fogH) return;
            int i = y * _fogW + x;
            if (_seen[i] != 0) return;
            _seen[i] = 1;
            // 시야와 문 1차 5-3 살핌: 걸을 수 있는 바닥이면 그 칸의 본 수를 올린다.
            if (_sweepCell != null) CountSweep(_sweepCell[i]);
            if (!_fogDirty)
            {
                _fogDirty = true;
                _dirtyMinX = _dirtyMaxX = x;
                _dirtyMinY = _dirtyMaxY = y;
                return;
            }
            if (x < _dirtyMinX) _dirtyMinX = x;
            if (x > _dirtyMaxX) _dirtyMaxX = x;
            if (y < _dirtyMinY) _dirtyMinY = y;
            if (y > _dirtyMaxY) _dirtyMaxY = y;
        }

        /// <summary>
        /// 살핌 표(시야와 문 1차 5-3, 장면마다 한 번): 기억 안개 칸 가운데가 칸 안쪽(Inner)이면 그 칸 번호, 벽·바위·기둥·문 막이(World.WallRects) 안이면 −1.
        /// 칸마다 걸을 수 있는 바닥 수와 살핌에 필요한 수(SightRules.SweepNeeded, 75%)를 센다. 사각형을 칸 범위로 칠해 한 번에 만든다.
        /// </summary>
        void BuildSweepTable()
        {
            _sweepCell = null;
            _cellNumber.Clear();
            _sweepPending.Clear();
            if (_world == null || _seen == null) return;
            var cells = _world.Cells;
            int n = Mathf.Min(cells.Count, short.MaxValue);
            var table = new short[_fogW * _fogH];
            for (int i = 0; i < table.Length; i++) table[i] = -1;
            for (int c = 0; c < n; c++)
            {
                _cellNumber[cells[c]] = c;
                FillSweep(table, cells[c].Inner, (short)c);
            }
            var walls = _world.WallRects;
            if (walls != null)
                for (int i = 0; i < walls.Count; i++) FillSweep(table, walls[i], -1);
            _sweepSeen = new int[n];
            _sweepNeeded = new int[n];
            _sweepFull = new bool[n];
            _sweepOwed = new bool[n];
            _sweepRaised = new bool[n];
            var walkable = new int[n];
            foreach (short v in table)
                if (v >= 0) walkable[v]++;
            for (int c = 0; c < n; c++)
            {
                _sweepNeeded[c] = SightRules.SweepNeeded(walkable[c]);
                // 걸을 바닥이 없는 칸은 처음부터 다 살핀 것으로 친다(SightRules.SweepDone과 같은 뜻).
                if (_sweepNeeded[c] > 0) continue;
                _sweepFull[c] = true;
                _sweepPending.Add(c);
            }
            _sweepCell = table;
        }

        /// <summary>가운데가 사각형 안(Rect.Contains처럼 왼·아래 경계 포함)인 기억 안개 칸에 값을 칠한다.</summary>
        void FillSweep(short[] table, Rect r, short value)
        {
            int x0 = Mathf.Max(0, Mathf.CeilToInt((r.xMin - _fogMin.x) / MemoryCell - 0.5f));
            int x1 = Mathf.Min(_fogW - 1, Mathf.CeilToInt((r.xMax - _fogMin.x) / MemoryCell - 0.5f) - 1);
            int y0 = Mathf.Max(0, Mathf.CeilToInt((r.yMin - _fogMin.y) / MemoryCell - 0.5f));
            int y1 = Mathf.Min(_fogH - 1, Mathf.CeilToInt((r.yMax - _fogMin.y) / MemoryCell - 0.5f) - 1);
            for (int y = y0; y <= y1; y++)
            {
                int row = y * _fogW;
                for (int x = x0; x <= x1; x++) table[row + x] = value;
            }
        }

        /// <summary>새로 '봤다'가 된 바닥 하나를 그 칸에 센다. 살핌에 필요한 수가 차면 알릴 차례를 기다린다(FlushSweeps).</summary>
        void CountSweep(int c)
        {
            if (c < 0) return;
            _sweepSeen[c]++;
            if (_sweepFull[c] || _sweepSeen[c] < _sweepNeeded[c]) return;
            _sweepFull[c] = true;
            _sweepPending.Add(c);
        }

        /// <summary>
        /// 다 살핀 칸을 알린다(5-3, MarkMemory 뒤): 미루기가 켜진 채 처음 들어가 경험치가 미뤄진 칸만 장면에 한 번 DungeonEvents.RaiseCellSwept.
        /// 아직 안 들어간 칸(가두기를 끄고 문 너머로 다 본 칸)은 들어설 때까지 기다리고, 들어설 때 경험치를 받은 칸(보스방·미루기 꺼짐)은 버린다.
        /// </summary>
        void FlushSweeps()
        {
            for (int k = _sweepPending.Count - 1; k >= 0; k--)
            {
                int c = _sweepPending[k];
                if (!_sweepRaised[c])
                {
                    var cell = _world.Cells[c];
                    if (!cell.Visited) continue;
                    if (_sweepOwed[c] && DefersCellXp(cell)) RaiseSwept(c);
                    else if (_sweepOwed[c]) continue;
                }
                _sweepPending.RemoveAt(k);
            }
        }

        /// <summary>미루기가 꺼지는 순간: 미뤄 둔 채 아직 못 살핀 칸의 경험치를 바로 준다(5-3 '미뤄 둔 칸은 미루기가 꺼지는 순간 준다').</summary>
        void PayOwedSweeps()
        {
            if (_sweepCell == null) return;
            for (int c = 0; c < _sweepOwed.Length; c++)
                if (_sweepOwed[c] && !_sweepRaised[c]) RaiseSwept(c);
        }

        void RaiseSwept(int c)
        {
            _sweepRaised[c] = true;
            DungeonEvents.RaiseCellSwept(_world.Cells[c]);
        }

        /// <summary>
        /// 처음 들어선 칸의 새 칸 경험치가 살핌으로 미뤄졌는가를 적는다. 같은 프레임 DungeonRoot가 Discovered(PlayerProgress가 같은 DefersCellXp로 미룸)
        /// 바로 뒤에 CellEntered를 알리므로 둘의 판정이 갈리지 않는다.
        /// </summary>
        void OnCellEntered(DungeonCell cell, bool first)
        {
            if (!first || cell == null || !_cellNumber.TryGetValue(cell, out int c)) return;
            if (DefersCellXp(cell)) _sweepOwed[c] = true;
        }

        /// <summary>바뀐 구역(+흐림 반경)만 5×5 상자로 흐려 알파를 다시 계산하고 판을 올린다.</summary>
        void UploadFog()
        {
            _fogDirty = false;
            int k = BlurRadius;
            int x0 = Mathf.Max(0, _dirtyMinX - k), x1 = Mathf.Min(_fogW - 1, _dirtyMaxX + k);
            int y0 = Mathf.Max(0, _dirtyMinY - k), y1 = Mathf.Min(_fogH - 1, _dirtyMaxY + k);
            // 가로 합(세로로 k칸 더 넓게).
            int ry0 = Mathf.Max(0, y0 - k), ry1 = Mathf.Min(_fogH - 1, y1 + k);
            for (int y = ry0; y <= ry1; y++)
            {
                int row = y * _fogW;
                for (int x = x0; x <= x1; x++)
                {
                    int s = 0;
                    int a = Mathf.Max(0, x - k), b = Mathf.Min(_fogW - 1, x + k);
                    for (int xx = a; xx <= b; xx++) s += _seen[row + xx];
                    _rowSum[row + x] = s;
                }
            }
            float norm = 1f / ((2 * k + 1) * (2 * k + 1));
            for (int y = y0; y <= y1; y++)
            {
                int a = Mathf.Max(ry0, y - k), b = Mathf.Min(ry1, y + k);
                for (int x = x0; x <= x1; x++)
                {
                    int s = 0;
                    for (int yy = a; yy <= b; yy++) s += _rowSum[yy * _fogW + x];
                    float v = Mathf.Clamp01(s * norm * 1.6f);
                    v = v * v * (3f - 2f * v);
                    _fogAlpha[y * _fogW + x] = (byte)Mathf.RoundToInt((1f - v) * 255f);
                }
            }
            _fogTex.SetPixelData(_fogAlpha, 0);
            _fogTex.Apply(false);
        }

        /// <summary>시야 다각형 바깥(가장자리 0.9유닛은 점점 짙게)을 덮는 고리 모양 판. 광선이 3개보다 적은 프레임은 지난 판을 그대로 둔다(덮개가 한 프레임 사라져 번쩍이지 않게).</summary>
        void BuildVeil()
        {
            int n = _rayAngle.Count;
            if (n < 3) return;
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
            byte veilA = (byte)Mathf.RoundToInt(VeilAlpha * 255f);
            var clear = new Color32(0, 0, 0, 0);
            var dark = new Color32(0, 0, 0, veilA);
            for (int i = 0; i < n; i++)
            {
                float r = _rayAngle[i] * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
                float d = _rayDist[i];
                _verts.Add(_origin + dir * d);
                _verts.Add(_origin + dir * (d + Feather));
                _verts.Add(_origin + dir * FarRadius);
                _colors.Add(clear);
                _colors.Add(dark);
                _colors.Add(dark);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = i * 3, a1 = a0 + 1, a2 = a0 + 2;
                int b0 = j * 3, b1 = b0 + 1, b2 = b0 + 2;
                _tris.Add(a0); _tris.Add(b1); _tris.Add(b0);
                _tris.Add(a0); _tris.Add(a1); _tris.Add(b1);
                _tris.Add(a1); _tris.Add(b2); _tris.Add(b1);
                _tris.Add(a1); _tris.Add(a2); _tris.Add(b2);
            }
            _veilMesh.Clear();
            _veilMesh.SetVertices(_verts);
            _veilMesh.SetColors(_colors);
            _veilMesh.SetTriangles(_tris, 0);
            _veilMesh.bounds = new Bounds(new Vector3(_origin.x, _origin.y, 0f), new Vector3(FarRadius * 2f, FarRadius * 2f, 10f));
        }

        /// <summary>
        /// 적 보이기(시야와 문 1차 4-1·4-2·4-7): 부채꼴 안(반각 + 5° + 몸 각도, 한 번 보인 적은 6° 더)이면서 시야 다각형 안이거나
        /// 드러난 동안(4-3)만 VisionInSight다. 몸 둘레 2.5 원은 바닥·벽만 보여 주므로 등 뒤 적은 그 안이어도 숨는다.
        /// 부채꼴 옆면에서 다각형이 줄어 각도 여유보다 먼저 닫히는 곳은 줄이지 않은 시야(InOpenSight)로 다시 잰다(4-2, 물결 2 고침).
        /// 보는 칸이 보스방이면 방 안 360°(4-7 ①안): 다각형 밖이어도 보는 칸 안이고 벽·돌기둥이 가리지 않으면 보인다(물결 2 고침).
        /// 부채꼴·적 부채꼴 손잡이를 끄면 다각형 안이면 보인다. 보여도 빛 밖이면 숨긴다(눈 두 점은 DarkVision이 VisionInSight로 띄움).
        /// </summary>
        void UpdateEnemies()
        {
            var lighting = DungeonLighting.Instance;
            var player = PlayerController.Instance;
            float lampR = lighting ? lighting.DimRadius : DungeonLighting.LampDim;
            bool dark = !lighting || lighting.DarknessOn;
            float now = Time.unscaledTime;
            bool enemyCone = ConeOn && EnemyConeOn;
            bool bossView = _viewCell != null && !SightRules.ConeApplies(_viewCell.Piece);
            bool coneRule = enemyCone && !bossView;
            bool room360 = enemyCone && bossView;
            if (now >= _nextPrune) PruneEnemyMaps(now);
            CollectLitLamps();
            foreach (var e in Enemy.All)
            {
                if (!e) continue;
                Vector2 pos = e.Position;
                float radius = e.Radius;
                bool inSight = IsVisible(pos, radius);
                if (coneRule)
                {
                    Vector2 to = pos - _origin;
                    float dist = to.magnitude;
                    float off = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, _lookDeg));
                    bool cone = dist <= SightRules.NearEnemyRadius
                        || SightRules.ConeSees(e.VisionInSight, off, _coneHalfAngle, SightRules.AngularRadius(radius, dist));
                    // 4-2: 다각형 반경은 반각에서 반각 + 10°까지 14 → 2.5로 줄어, ConeSees의 들어옴(+5°)·나감(+6°) 경계보다 먼저 닫힌다
                    //      (거리 4·몸 0.4면 약 73°). 부채꼴 판정이 참인데 다각형 밖이면 줄이지 않은 시야로 다시 재서 각도는 ConeSees 하나로 정한다.
                    if (cone && !inSight) inSight = InOpenSight(pos, dist, radius);
                    inSight = inSight && (cone || Revealed(e, now));
                }
                else if (room360 && !inSight)
                {
                    // 4-7 ①안: 방 안 적은 등 뒤에서도 보인다. 돌기둥·벽이 가리면 안 보이고, 빛 규칙은 아래 그대로라
                    //      빛 안이면 덮개 아래 어둡게, 빛 밖이면 눈 두 점만 보인다.
                    inSight = InViewCell(pos) && !WallBetween(_origin, pos);
                }
                bool lit = !dark || IsLit(pos, player ? (Vector2)player.transform.position : _origin, lampR);
                e.VisionInSight = inSight;
                bool hidden = !inSight || !lit;
                // 쓰러지는 몸(처치 날림, 스스로 빛남 2-4 ②)은 시야 안이거나 보이던 적이면 끝까지 보인다.
                if (e.Dead) hidden = e.VisionHidden && !inSight;
                SetHidden(e, hidden, now);
            }
        }

        bool Revealed(Enemy e, float now) => _revealUntil.Count > 0 && _revealUntil.TryGetValue(e, out float until) && now < until;

        /// <summary>
        /// 부채꼴 옆면으로 줄이지 않은 시야 안인가(4-2 적 판정 전용): 몸 가장자리가 부채꼴 반경(새 칸 펼침·웅크림 포함) 안,
        /// 칸을 가두는 중이면 넓힌 칸 사각형 안, 벽이 가리지 않음. 광선을 하나 더 쏘므로 부채꼴 판정이 참인데 다각형 밖인 적에게만 쓴다.
        /// </summary>
        bool InOpenSight(Vector2 p, float dist, float pad)
        {
            if (dist - pad > _coneRadius) return false;
            if (_clipping && !_clipRect.Contains(p)) return false;
            return !WallBetween(_origin, p);
        }

        /// <summary>드러남·숨김 기록을 가끔 비운다: 지워진 적, 죽은 적과 끝난 드러남.</summary>
        void PruneEnemyMaps(float now)
        {
            _nextPrune = now + PruneSeconds;
            _staleEnemies.Clear();
            foreach (var kv in _revealUntil)
                if (!kv.Key || kv.Key.Dead || now >= kv.Value) _staleEnemies.Add(kv.Key);
            foreach (var e in _staleEnemies) _revealUntil.Remove(e);
            _staleEnemies.Clear();
            foreach (var kv in _hiddenStamps)
                if (!kv.Key) _staleEnemies.Add(kv.Key);
            foreach (var e in _staleEnemies) _hiddenStamps.Remove(e);
            _staleEnemies.Clear();
        }

        /// <summary>
        /// 맞으면 드러남(4-3): 맞은 쪽(PlayerController.LastHitFrom)에서 몸 가장자리까지 HurtRevealReach(0.3) 안 가장 가까운 산 적을 0.8초 보인다.
        /// 근접 공격은 때린 적 가운데가 맞은 쪽이라 그 적이 걸린다. 화살·덫은 맞은 쪽이 화살·덫 자리라 쏜 적은 드러나지 않고,
        /// 다른 적도 몸 가장자리가 그 자리 0.3 안에 붙어 있지 않으면 걸리지 않는다.
        /// 그 적이 안 보이던 적이면 이번 플레이 처음 한 번 첫 급습 안내(4-9).
        /// </summary>
        void OnPlayerDamaged(int amount)
        {
            var player = PlayerController.Instance;
            if (!player) return;
            Vector2 from = player.LastHitFrom;
            Enemy hitter = null;
            float best = float.MaxValue;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead) continue;
                float gap = Vector2.Distance(e.Position, from) - e.Radius;
                if (gap > SightRules.HurtRevealReach || gap >= best) continue;
                best = gap;
                hitter = e;
            }
            if (!hitter) return;
            // VisionInSight는 지난 LateUpdate 값이다(맞기 직전 화면에 보였나).
            bool unseen = !hitter.VisionInSight;
            Reveal(hitter, SightRules.RevealOnHurtSeconds);
            if (!unseen || _ambushHinted || !VisionOn) return;
            _ambushHinted = true;
            DungeonEvents.Say(SightRules.AmbushHint);
        }

        /// <summary>내가 직접 친 적(기본 공격·회오리·검풍)은 0.5초 보인다(4-3). 출혈·전설 효과·벽 박기 피해는 드러내지 않는다.</summary>
        void OnPlayerDealt(DamageDealt d)
        {
            if (!d.Target) return;
            if (d.Source == DamageSource.Basic || d.Source == DamageSource.Whirlwind || d.Source == DamageSource.SwordWave)
                Reveal(d.Target, SightRules.RevealOnHitSeconds);
        }

        bool IsLit(Vector2 p, Vector2 player, float lampR)
        {
            if ((p - player).sqrMagnitude <= lampR * lampR) return true;
            float wallR = DungeonLighting.WallLampDim;
            foreach (var l in _litLamps)
                if ((p - l).sqrMagnitude <= wallR * wallR && !Blocked(l, p)) return true;
            return false;
        }

        /// <summary>두 점 사이를 벽이 막는가(시야를 가리지 않는 작은 물체는 무시). 벽 등잔 빛도 벽에 그림자가 진다.</summary>
        static bool Blocked(Vector2 from, Vector2 to) => WallBetween(from, to);

        void CollectLitLamps()
        {
            _litLamps.Clear();
            foreach (var it in Interactable.All)
                if (it is WallLamp lamp && lamp && lamp.Lit) _litLamps.Add(lamp.Position);
                else if (it is ArenaLamp arena && arena && arena.Lit) _litLamps.Add(arena.Position);
        }

        /// <summary>
        /// 몸을 숨기거나 보인다. 숨겨도 시야 안이면 빛을 무시하는 표식(정예 흰 테두리, 돌충이 기절 고리)은 남긴다(3차 초안 2-4 공정 규칙).
        /// 숨긴 그대로인 적은 새로 붙은 그림도 숨기려고 렌더러를 다시 모으는데, 보는 칸 밖(대개 다른 칸) 적은 0.25초마다만 모은다(시야와 문 1차 8-1).
        /// </summary>
        void SetHidden(Enemy e, bool hidden, float now)
        {
            if (e.VisionHidden == hidden && !hidden) return;
            var unlit = RenderMaterials.Unlit;
            bool keepMarkers = hidden && e.VisionInSight && !e.Dead;
            if (hidden && e.VisionHidden && _hiddenStamps.TryGetValue(e, out var stamp) && stamp.Markers == keepMarkers
                && now < stamp.Next && !InViewCell(e.Position))
                return;
            _hiddenStamps[e] = new HiddenStamp { Next = now + HiddenRefreshSeconds, Markers = keepMarkers };
            e.VisionHidden = hidden;
            _renderers.Clear();
            e.GetComponentsInChildren(true, _renderers);
            foreach (var r in _renderers)
            {
                if (!r) continue;
                bool off = hidden && !(keepMarkers && unlit && r.sharedMaterial == unlit);
                if (r.forceRenderingOff != off) r.forceRenderingOff = off;
            }
        }

        void ShowAllEnemies()
        {
            foreach (var e in Enemy.All)
            {
                if (!e) continue;
                e.VisionInSight = true;
                if (!e.VisionHidden) continue;
                e.VisionHidden = false;
                _renderers.Clear();
                e.GetComponentsInChildren(true, _renderers);
                foreach (var r in _renderers)
                    if (r) r.forceRenderingOff = false;
            }
        }
    }
}
