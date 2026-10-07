using System.Collections;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Save;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 '등불골 · 갱도 마당'(기획/마을-의뢰-첫판.md 1장 흐름·2장 배치·6장 HUD). Town.unity에는 카메라와 이 컴포넌트만 두고
    /// 광장·빛·플레이어·시설·화면은 실행할 때 만든다(던전 루트와 같은 방식). 장면 사이에는 꾸러미(ProfileCarry)와 도착 쪽지(TownTravel)만 들고 온다.
    /// 흐름: Awake 첫 줄 SceneStatics.Reset → 도착 처리(TownArrivalRules.Apply, 두 번 불러도 같음, 지급 없음) → 광장·빛·시설·플레이어(새 플레이 남쪽 길 끝,
    /// 올라오면 귀환 지점) → Start에서 올라가는 카드를 남은 시간만큼 잇고 → 밝아짐(새 플레이 0.8초, 도착 0.5초) → 도착 카드(옛 결과 창 숫자) 또는
    /// 새 플레이면 0.5초 뒤 춘삼 바크 "어이, 이쪽!"과 y 36.5 줄을 넘으면 오프닝. 권양기 창에서 층을 고르면 Depart: 가방·스킬 담기 → town.departures +1 →
    /// 밤 카드(첫 출발은 내려가는 카드 1.6초, 밤 없음) → 덮이면 TripPlan을 두고 던전 장면을 불러온다.
    /// 마을에서는 싸우지 않는다: 공격 막기는 PlayerInputReader가 플레이어를 만들 때 TownRoot.Instance를 보고 켠다(꾸러미 4, 2-3).
    /// 시야·어둠 시야·기억 안개·큰 지도·던전 공기는 붙이지 않는다(2-2). 늘 저녁이다.
    /// 주민·대화창·의뢰 목록 창·목표 HUD(꾸러미 3)는 partial 자리(TownRoot.Ui.cs)로 잇는다. 그 파일이 없으면 오프닝은 건너뛰기로 넣고,
    /// 바크·물체 글·게시판은 화면 아래 알림으로 대신한다(꾸러미 2만으로 Play 확인이 되게).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed partial class TownRoot : MonoBehaviour
    {
        /// <summary>대화창 창 이름(꾸러미 3 TalkWindow가 DungeonUi.TryOpen으로 연다, 6-3). 방문마다 대화 합을 잴 때 본다.</summary>
        public const string TalkModal = "talk";
        /// <summary>밝아짐 검은 막 GUI 순서(6-6: 밤 카드 −200 > 이 막 −100 > 도착 카드 −60 > 대화창 −50 > 창 −20 > HUD 5).</summary>
        const int FadeGuiDepth = -100;

        public static TownRoot Instance { get; private set; }

        /// <summary>
        /// F1 '오우거 굴 있음 가정'. 지금은 2층 계단 아래 굴이 있어(QuestContext.Live) 가정 없이도 오우거 의뢰가 열리고 실제 처치로 센다.
        /// 가정은 굴이 없는 빌드(층 표에서 굴을 뺀 시험)에서 받기·문구만 볼 때 쓴다. 장면을 바꿔도 남고 플레이를 새로 시작하면 꺼진다.
        /// 대화창(꾸러미 3)의 같은 가정도 함께 바꾼다(SetUiOgreDen).
        /// </summary>
        public static bool OgreDenAssumed
        {
            get => s_ogreDenAssumed;
            set
            {
                s_ogreDenAssumed = value;
                SetUiOgreDen(value);
            }
        }

        static bool s_ogreDenAssumed;

        /// <summary>의뢰 계산 문맥: 지금 던전 상태(QuestContext.Live) 또는 F1 가정. Refresh·Build·End에 넘긴다(꾸러미 3도 이것을 쓴다).</summary>
        public static QuestContext QuestCtx => new QuestContext(QuestContext.Live.OgreDenReady || OgreDenAssumed);

        [SerializeField, Tooltip("빛을 무시하는 스프라이트 재질(창·등불·화덕 불빛). 비어 있으면 실행할 때 셰이더를 찾아 만든다.")]
        Material unlitMaterial;

        [SerializeField, Tooltip("던전·전투 시험장과 공유하는 그림·소리 묶음. 빈 칸은 도형으로 표시한다.")]
        CombatArtSet projectArt;

        public CombatArtSet ProjectArt => projectArt;

        public PlayerController Player { get; private set; }
        public Inventory Inventory { get; private set; }
        public PlayerProgress Progress { get; private set; }
        public TownProps Props { get; private set; }
        public TownLighting Lighting { get; private set; }
        public TownCamera CameraRig { get; private set; }
        public TownHud Hud { get; private set; }
        public TownArrivalCard ArrivalCard { get; private set; }
        public MineGate Gate { get; private set; }
        public NightCard Night => _nightCard;

        /// <summary>이번 장면에 어떻게 왔나(쪽지가 없고 첫 방문이면 새 플레이).</summary>
        public TownArrivalKind ArrivalType { get; private set; } = TownArrivalKind.NewPlay;
        /// <summary>도착 처리 결과(도착 카드·바크가 본다).</summary>
        public TownArrivalResult Arrival { get; private set; }
        /// <summary>던전에서 가져온 도착 쪽지(새 플레이·같은 마을 다시 불러오기면 null).</summary>
        public TownArrivalNote Note { get; private set; }
        /// <summary>장면을 불러오는 데 걸린 실제 시간(초, 옛 장면이 불러오기를 부른 때 → 이 장면 Start). 새 플레이면 -1.</summary>
        public float LoadSeconds { get; private set; } = -1f;

        /// <summary>권양기로 떠나는 중(밤 카드·장면 불러오기).</summary>
        public bool Leaving => _leaving;
        /// <summary>밝아지는 중이거나 떠나는 중(주민 F·바크를 미룬다).</summary>
        public bool Busy => _fade > 0.001f || _leaving || !_arrived;
        /// <summary>이번 방문에 머문 실제 시간(초, 장면 Start부터).</summary>
        public float VisitSeconds => _visitStart >= 0f ? Time.realtimeSinceStartup - _visitStart : 0f;
        /// <summary>이번 방문 대화 합(초, 대화창이 열린 실제 시간).</summary>
        public float TalkSeconds => _talkSeconds;

        NightCard _nightCard;
        float _fade = 1f;
        bool _arrived;
        bool _leaving;
        float _visitStart = -1f;
        float _talkSeconds;
        /// <summary>새 플레이 춘삼 바크 시각(실제 시간, 없으면 -1).</summary>
        float _barkAt = -1f;
        /// <summary>y 36.5 줄을 넘으면 오프닝(새 플레이·F1 '오프닝 다시').</summary>
        bool _openingLineArmed;
        /// <summary>오프닝을 틀어야 함(창이 비면 연다).</summary>
        bool _openingWanted;
        bool _uiChecked;
        bool _hasUi;
        // 꾸러미 레벨 몫을 마지막으로 맞춘 값(SyncProgress).
        int _syncXp;
        int _syncLevel;
        int _syncCarryPoints;
        int _syncProgressPoints;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            s_ogreDenAssumed = false;
        }

        /// <summary>장면 정적(SceneStatics.Reset이 부름): 마을 루트와 꾸러미 3 화면 정적(대화창·의뢰 목록 창·주민 목록). F1 가정은 남긴다.</summary>
        public static void ResetStatics()
        {
            Instance = null;
            ResetUiStatics();
        }

        void Awake()
        {
            SceneStatics.Reset();
            // 처음 화면 동안 마을을 짓지 않고 멈춰 둔다(꾸러미·도착 처리·플레이어·HUD 모두 만들지 않음). 처음 화면은 GameShell의 TitleScreen이 그린다(저장·처음 화면·멈춤 창 1차 4-2).
            if (GameSession.HoldTownForTitle())
            {
                enabled = false;
                return;
            }
            Instance = this;
            Tuning.Ruleset = CombatRuleset.V3;
            RenderMaterials.SetUnlit(unlitMaterial);
            ArtRuntime.Mode = projectArt && projectArt.HasAnyContent ? ArtMode.Project : ArtMode.Shapes;
            Layers.ApplyCollisionMatrix();

            gameObject.AddComponent<TimeScaleService>();
            gameObject.AddComponent<WorldOverlay>();
            gameObject.AddComponent<Sfx>();
            new GameObject("HitEffects").AddComponent<HitEffects>();

            // 도착 처리(1-4 단계 4): 쪽지가 없고 첫 방문이면 새 플레이. 쪽지 없이 다시 불러온 마을(F1)은 같은 도착이라 방문 수가 오르지 않는다.
            var carry = ProfileCarry.Ensure();
            ProfileCarry.SetTrip(null);
            Note = TownTravel.Take();
            if (Note != null) ArrivalType = Note.Kind;
            else ArrivalType = TownSave.Visits(carry) == 0 ? TownArrivalKind.NewPlay : TownArrivalKind.Basket;
            if (Note == null) ExpeditionLedger.Clear(); // 쪽지 없이 열린 마을(새 플레이·이어하기·F1): 세던 원정은 남지 않는다
            Arrival = TownArrivalRules.Apply(carry, ArrivalType, QuestCtx);
            Debug.Log($"[마을] 도착 {ArrivalType} · 원정 {carry.Expedition} · 방문 {Arrival.Visits}" + (Arrival.Repeated ? " (같은 도착)" : "") +
                      $" · 명패 +{Arrival.NewTags} (등불 {Arrival.LitLamps}/{TownLayout.LampCount})" +
                      (Arrival.Opened.Count > 0 ? " · 받을 수 있음 " + string.Join(", ", Arrival.Opened) : "") +
                      (Arrival.OpeningPending ? " · 오프닝 전" : ""));

            // 광장·빛·시설(2장).
            Props = TownProps.Build(transform, Arrival.LitLamps);
            Lighting = gameObject.AddComponent<TownLighting>();
            Lighting.Init(Arrival.LitLamps);
            Gate = MineGate.Create(transform);
            TownFacility.Create(transform, TownLayout.Anvil);
            TownFacility.Create(transform, TownLayout.Board);
            TownFacility.Create(transform, TownLayout.Shutter);
            TownFacility.Create(transform, TownLayout.Cart);

            // 기능 모듈. 각자 Awake에서 사건을 구독한다(정적 비우기 뒤라 안전).
            gameObject.AddComponent<InteractionSystem>();
            Inventory = gameObject.AddComponent<Inventory>();
            Progress = gameObject.AddComponent<PlayerProgress>();
            _nightCard = gameObject.AddComponent<NightCard>();
            Hud = gameObject.AddComponent<TownHud>();
            ArrivalCard = gameObject.AddComponent<TownArrivalCard>();
            gameObject.AddComponent<TownDebugPanel>();
            // 마을 자동 저장: 도착 처리 직후·창이 닫힐 때·게임 창을 닫을 때(저장·처음 화면·멈춤 창 1차 3-2).
            gameObject.AddComponent<TownAutoSave>();
            // 던전과 같은 정수리 시점 겉모습(그림 칸은 ArtRuntime이 고름).
            gameObject.AddComponent<TopDownView>();

            Vector2 start = ArrivalType == TownArrivalKind.NewPlay ? ToVector(TownLayout.NewPlayStart) : ToVector(TownLayout.ReturnPoint);
            Player = PlayerController.Create(start);
            Player.ApplyBaseline(1);
            // 걷기 6.5 × 걷기 배율 0.80 = 초당 5.2(2-1). 마을에는 '아는 길' 풀림이 없어 그대로 남는다.
            Player.SpeedOverride = TownLayout.SpeedOverride;
            Lighting.AttachPlayer(Player.transform);
            // 레벨·스킬·장비를 꾸러미에서 덮어쓴다(던전 ProfileCarry.Apply와 같은 차례: 레벨 → 장비). 체력·물약은 마을에서 쓰지 않는다.
            Inventory.Init(Player);
            Progress.Init(Player);
            Progress.ImportFrom(carry);
            Inventory.ImportFrom(carry);
            SnapshotProgress(carry);

            var cam = Camera.main;
            if (cam)
            {
                var old = cam.GetComponent<DungeonCamera>();
                if (old) old.enabled = false;
                CameraRig = cam.GetComponent<TownCamera>();
                if (!CameraRig) CameraRig = cam.gameObject.AddComponent<TownCamera>();
                CameraRig.Bind(Player.transform);
            }

            // 꾸러미 3: 대화창·의뢰 목록 창·목표 HUD, 주민 3명.
            AddTownUi();
            SpawnResidents();
            if (!HasTownUi) Debug.Log("[마을] 주민·대화창(꾸러미 3)이 아직 이어지지 않았다 — 오프닝은 건너뛰기로 넣고 바크·물체 글은 알림으로 대신한다");

            _openingLineArmed = ArrivalType == TownArrivalKind.NewPlay && !TownSave.SeenScene(carry, TownScript.OpeningId);
            _fade = 1f;
        }

        /// <summary>올라가는 카드가 남았으면 이어 보여 주고(검은 화면), 끝나면 밝아진다. 장면 불러오기 시간을 잰다.</summary>
        void Start()
        {
            LoadSeconds = SceneTravel.TakeLoadSeconds();
            if (LoadSeconds >= 0f) Debug.Log($"[마을] 장면 불러오기 {LoadSeconds * 1000f:0}ms ({ArrivalType})");
            _visitStart = Time.realtimeSinceStartup;
            var note = Note;
            if (note != null && _nightCard && note.CardLines != null && note.CardLines.Length > 0 && note.CardUntil > Time.realtimeSinceStartup)
                _nightCard.ContinueNight(note.CardLines, note.CardUntil, AfterCard);
            else AfterCard();
        }

        void AfterCard()
        {
            if (this == null) return;
            if (ArrivalType == TownArrivalKind.NewPlay && _openingLineArmed) _barkAt = Time.realtimeSinceStartup + TownLayout.NewPlayBarkDelay;
            StartCoroutine(ArriveRoutine());
        }

        IEnumerator ArriveRoutine()
        {
            if (CameraRig) CameraRig.Snap();
            float seconds = ArrivalType == TownArrivalKind.NewPlay ? TownLayout.NewPlayFadeIn : TownLayout.ArrivalFadeIn;
            yield return Fade(0f, seconds);
            _arrived = true;
            if (Note != null) ShowArrivalCard();
            // 쪽지 없이 다시 불러온 마을(F1 등)인데 오프닝을 못 봤으면 바로 튼다(줄을 넘을 길이 없다).
            else if (ArrivalType != TownArrivalKind.NewPlay) RequestOpening();
        }

        IEnumerator Fade(float to, float seconds)
        {
            float speed = 1f / Mathf.Max(0.05f, seconds);
            while (!Mathf.Approximately(_fade, to))
            {
                _fade = Mathf.MoveTowards(_fade, to, Mathf.Min(Time.unscaledDeltaTime, 0.1f) * speed);
                yield return null;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (DungeonUi.Modal == TalkModal) _talkSeconds += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            if (_leaving) return;
            var carry = ProfileCarry.Ensure();
            SyncProgress();
            TryShowKnotCard(carry);
            bool seen = TownSave.SeenScene(carry, TownScript.OpeningId);
            if (_barkAt >= 0f && Time.realtimeSinceStartup >= _barkAt)
            {
                _barkAt = -1f;
                if (!seen) Bark(NpcTable.Gate, TalkDirector.BarkFor(NpcTable.Gate, carry));
            }
            if (seen)
            {
                _openingLineArmed = false;
                _openingWanted = false;
                return;
            }
            if (_openingLineArmed && !_openingWanted && Player && Player.Position.y >= TownLayout.OpeningLineY && !TalkOpen()) _openingWanted = true;
            UpdateOpening(carry);
        }

        // ── 도착 카드(6-4) ──────────────────────────────────

        /// <summary>
        /// '첫 매듭' 카드(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 나-6): 무진 마무리 장면(TownScript.OgreAfter)을 본 뒤, 대화·창이 모두 닫히고 마을이 한가할 때 한 번.
        /// 띄운 기록은 이정표(TownScript.KnotMilestone)로 꾸러미에 남는다(창이 닫힐 때 자동 저장).
        /// </summary>
        void TryShowKnotCard(CarryData carry)
        {
            if (!ArrivalCard || ArrivalCard.Showing || carry == null) return;
            if (!TownSave.SeenScene(carry, TownScript.OgreAfterId) || TownSave.HasMilestone(carry, TownScript.KnotMilestone)) return;
            if (Busy || TalkOpen() || DungeonUi.ModalOpen) return;
            Demo6.Core.Loot.Grade? best = null;
            foreach (var g in carry.Equipment)
                if (g != null && (!best.HasValue || g.Grade > best.Value)) best = g.Grade;
            var book = new QuestBook(carry);
            var lines = TownScript.KnotCardLines(Mathf.Max(1, carry.Expedition - 1), GameSession.PlaySeconds,
                BossLedger.Losses(carry, OgreDen.BossId), TownArrivalRules.NameplatesHeld(carry), book.SurveyTotal, QuestBook.SurveyMax, best);
            TownSave.SetMilestone(carry, TownScript.KnotMilestone);
            ArrivalCard.Show(TownScript.KnotTitle, lines, TownScript.KnotClose, null, 1);
        }

        void ShowArrivalCard()
        {
            if (!ArrivalCard) return;
            var carry = ProfileCarry.Ensure();
            var lines = new List<string>();
            var s = Note != null ? Note.Summary : null;
            if (s != null)
            {
                // 옛 결과 창 숫자 그대로(NightCard.ShowResults와 같은 두 줄).
                lines.Add(s.Line());
                lines.Add(TownArrivalCard.GainLine(s));
            }
            // 떠나며 거둔 바닥 장비 한 줄(검토 1차 Q3, FloorSweepNote). 뼈색 진한 줄로 함께 보인다.
            string swept = FloorSweepNote.TakeForTown();
            if (swept != null) lines.Add(swept);
            // 이번 원정에서 남은 것(묶음 3 가-4, ExpeditionLedger).
            string kept = ExpeditionLedger.TakeLine(carry);
            if (kept != null) lines.Add(kept);
            int strong = lines.Count;
            lines.AddRange(TownArrivalRules.CardLines(carry, Arrival));
            ArrivalCard.Show(TownScript.ArrivalTitle, lines, TownScript.ArrivalClose, OnArrivalCardClosed, strong);
        }

        /// <summary>도착 카드를 닫음: 오프닝을 못 봤으면(DungeonTest 바로 Play 시험 경로) 귀환 지점에서 오프닝, 첫 귀환이면 옥금 바크(1-4 단계 6, 1-6).</summary>
        void OnArrivalCardClosed()
        {
            if (this == null || _leaving) return;
            var carry = ProfileCarry.Ensure();
            if (!TownSave.SeenScene(carry, TownScript.OpeningId))
            {
                Debug.LogWarning("[마을] 오프닝을 보지 않은 채 올라왔다(DungeonTest 바로 Play 시험 경로) — 귀환 지점에서 오프닝을 튼다");
                RequestOpening();
                return;
            }
            if (Arrival != null && Arrival.FirstReturn) Bark(NpcTable.Smith, TalkDirector.BarkFor(NpcTable.Smith, carry));
        }

        // ── 오프닝(1-2·5-1) ─────────────────────────────────

        /// <summary>오프닝을 틀어 달라고 한다(창이 비고 밝아진 뒤 연다). 이미 봤으면 아무것도 하지 않는다.</summary>
        public void RequestOpening()
        {
            if (TownSave.SeenScene(ProfileCarry.Ensure(), TownScript.OpeningId)) return;
            _openingWanted = true;
        }

        void UpdateOpening(CarryData carry)
        {
            if (!_openingWanted || !_arrived || _fade > 0.001f || DungeonUi.ModalOpen || TalkOpen()) return;
            if (!HasTownUi)
            {
                _openingWanted = false;
                _openingLineArmed = false;
                SkipOpeningWithoutUi(carry);
                return;
            }
            bool started = false;
            PlayTalk(NpcTable.Gate, ref started);
            if (started) _openingWanted = false;
        }

        /// <summary>대화창이 없을 때(꾸러미 3 전): 오프닝을 건너뛰기로 넣는다(이름 공개·의뢰 받기·본 장면이 끝까지 본 것과 같음). 요약·알림만 띄운다.</summary>
        void SkipOpeningWithoutUi(CarryData carry)
        {
            var r = TalkDirector.Skip(TownScript.Opening, 0, carry, QuestCtx);
            if (!string.IsNullOrEmpty(r.Summary)) DungeonEvents.Say(r.Summary);
            foreach (var n in r.Notices) DungeonEvents.Say(n);
            Debug.Log("[마을] 대화창이 없어 오프닝을 건너뛰기로 넣었다 — 받음 " + string.Join(", ", r.Accepted) + " · 공개 " + string.Join(", ", r.Revealed));
        }

        // ── 화면 잇기(꾸러미 3이 있으면 그쪽, 없으면 알림) ─────────

        bool HasTownUi
        {
            get
            {
                if (!_uiChecked)
                {
                    _uiChecked = true;
                    DetectTownUi(ref _hasUi);
                }
                return _hasUi;
            }
        }

        /// <summary>대화창이 열려 있는가.</summary>
        public bool TalkOpen()
        {
            bool open = DungeonUi.Modal == TalkModal;
            CheckTalkOpen(ref open);
            return open;
        }

        /// <summary>주민 머리 위 바크(이름표 없음). 주민 그림이 없으면 화면 아래 알림으로 대신한다.</summary>
        public void Bark(string npcId, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            bool shown = false;
            ShowBark(npcId, text, ref shown);
            if (!shown) DungeonEvents.Say(text);
        }

        /// <summary>물체·시설 글(나레이션 모양, 5-8). 대화창이 없으면 화면 아래 알림으로 대신한다.</summary>
        public void Narrate(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            bool shown = false;
            ShowNarration(text, ref shown);
            if (!shown) DungeonEvents.Say(text);
        }

        /// <summary>의뢰 게시판 F(6-5): 의뢰 목록 창. 창이 없으면 게시판 첫 줄을 알림으로 띄우고 목록은 로그로 남긴다.</summary>
        public void OpenQuestBoard()
        {
            bool opened = false;
            OpenQuestList(ref opened);
            if (opened) return;
            var board = TownScript.Object(TownScript.ObjBoard);
            if (board != null) DungeonEvents.Say(board.Text);
            foreach (var row in new QuestBook(ProfileCarry.Ensure()).ListRows()) Debug.Log("[마을] 의뢰 목록: " + row.Line());
        }

        // ── 꾸러미 3 잇는 자리(partial, 구현은 TownRoot.Ui.cs). 구현이 없으면 부르는 곳이 사라지고 위 대신 길로 돈다 ──
        /// <summary>꾸러미 3 화면 정적 비우기(장면을 바꿀 때).</summary>
        static partial void ResetUiStatics();
        /// <summary>대화창의 F1 '오우거 굴 있음 가정'을 같이 바꾼다.</summary>
        static partial void SetUiOgreDen(bool on);
        /// <summary>꾸러미 3 화면이 이어졌으면 present = true.</summary>
        partial void DetectTownUi(ref bool present);
        /// <summary>대화창·의뢰 목록 창·목표 HUD(마을 모드)를 이 물체에 붙인다(Awake, 플레이어를 만든 뒤).</summary>
        partial void AddTownUi();
        /// <summary>주민 3명을 평소 자리(TownLayout.NpcHome)에 세운다.</summary>
        partial void SpawnResidents();
        /// <summary>그 주민과의 대화(TalkDirector.Build)를 대화창에 연다. 열었으면 started = true(다른 창이 열려 있으면 false → 다음 프레임에 다시).</summary>
        partial void PlayTalk(string npcId, ref bool started);
        /// <summary>나레이션 한 줄(이름표 없는 상자).</summary>
        partial void ShowNarration(string text, ref bool shown);
        /// <summary>주민 머리 위 바크(반경과 상관없이 지금).</summary>
        partial void ShowBark(string npcId, string text, ref bool shown);
        /// <summary>의뢰 목록 창 열기.</summary>
        partial void OpenQuestList(ref bool opened);
        /// <summary>대화창이 열려 있으면 open = true.</summary>
        partial void CheckTalkOpen(ref bool open);

        // ── 떠나기(1-3) ──────────────────────────────────────

        /// <summary>
        /// 권양기 출발(1-3 단계 4): 가방·스킬을 꾸러미에 담고 → town.departures +1 → 카드(첫 출발은 내려가는 카드 1.6초, 그 뒤는 밤 카드 2.5초) →
        /// 화면이 덮이면 TripPlan{고른 층, 원정 1이면 FirstStart 아니면 Basket, 카드 끝 시각·글, 승강장 고르기 없음}을 두고 던전 장면을 불러온다.
        /// 던전 쪽(DungeonRoot.Awake·Start)은 쪽지를 읽어 검은 화면에서 시작하고 카드를 이어 보인 뒤 바로 내려선다.
        /// 창을 먼저 닫고 부른다(다른 창이 열려 있으면 밤 카드 창이 안 열림). 떠나는 중이거나 던전 장면이 없으면 false.
        /// floor가 '보스방 앞'(OgreDen.PickCode)이고 아직 열려 있으면(BossLedger.FrontLandingOpen) 오우거 굴 쪽지(TripPlan.Den)로 내려간다.
        /// </summary>
        public bool Depart(int floor)
        {
            if (_leaving) return false;
            if (!CanReachDungeon()) return false;
            var carry = ProfileCarry.Ensure();
            CaptureCarry(carry);
            // 권양기 출발 직전 저장(trip=1). 출발 수 +1(TownNight.Depart) 앞이라 원정 도중 끄면 이 상태로 마을에서 다시 시작한다. 실패해도 출발은 그대로(저장·처음 화면·멈춤 창 1차 3-2·3-5).
            GameSave.SaveTown(SaveReason.Departure);
            // '보스방 앞'(OgreDen.PickCode): 굴 앞 말뚝을 켰고 첫 처치 전이면 굴 장면으로(층 번호는 굴이 딸린 층). 아니면 고른 층(없으면 줄 끝).
            bool den = OgreDen.IsPick(floor) && BossLedger.FrontLandingOpen(carry);
            var d = TownNight.Depart(carry);
            var trip = new TripPlan
            {
                Floor = den ? OgreDen.Floor : PickFloor(carry, floor),
                Arrival = d.FirstStart ? ArrivalKind.FirstStart : ArrivalKind.Basket,
                PickLanding = false,
                Den = den,
            };
            LogVisit(carry, trip, d.HasNight ? "밤 카드" : "첫 출발");
            _leaving = true;
            if (_nightCard && d.Seconds > 0f)
            {
                trip.NightLines = d.Lines;
                trip.NightUntil = Time.realtimeSinceStartup + d.Seconds;
                _nightCard.ShowNight(d.Lines, d.Seconds, () => LoadDungeon(trip));
            }
            else LoadDungeon(trip);
            return true;
        }

        /// <summary>F1 '바로 던전으로': 카드 없이 줄 끝 층으로 바로(바구니 도착). 출발 수는 세지 않는다.</summary>
        public bool DepartDirect()
        {
            if (_leaving || !CanReachDungeon()) return false;
            var carry = ProfileCarry.Ensure();
            CaptureCarry(carry);
            var trip = new TripPlan { Floor = carry.RopeEnd(FloorRecipe.MaxTestFloor), Arrival = ArrivalKind.Basket, PickLanding = false };
            LogVisit(carry, trip, "F1 바로");
            _leaving = true;
            LoadDungeon(trip);
            return true;
        }

        bool CanReachDungeon()
        {
            if (SceneTravel.CanLoad(SceneTravel.DungeonPath)) return true;
            Debug.LogWarning($"[마을] 던전 장면({SceneTravel.DungeonPath})을 불러올 수 없어 떠나지 않는다 — 빌드 목록을 확인");
            DungeonEvents.Say("갱도 아래로 내려갈 수 없다 — 던전 장면을 찾지 못했다.");
            return false;
        }

        /// <summary>
        /// 떠나기 전에 마을 가방에서 바꾼 장비·스킬을 꾸러미에 담는다(11장 위험 5). 의뢰 보상으로 늘어난 레벨 몫을 먼저 맞춘다.
        /// 저장(GameSave.SaveTown)도 저장 직전에 부른다(저장·처음 화면·멈춤 창 1차 2-7).
        /// </summary>
        public void CaptureCarry(CarryData carry)
        {
            SyncProgress();
            if (Inventory) Inventory.ExportTo(carry);
            if (Progress) Progress.ExportTo(carry);
            // 맞춘 값을 꾸러미 값으로 새로 적는다 — 안 하면 마을에 머무는 동안 저장한 다음 프레임에 SyncProgress가 쓴 스킬 점수를 한 번 더 빼서 점수를 잃는다(2-7 주의).
            SnapshotProgress(carry);
        }

        /// <summary>
        /// 의뢰 보상(QuestBook.Report → QuestRewards.Grant)은 꾸러미의 경험치·레벨·스킬 점수에 바로 더해진다. 마을의 PlayerProgress는 도착 때 꾸러미에서 푼 값을
        /// 들고 있으므로, 그대로 두면 출발 때 ExportTo가 낡은 값으로 덮어써 보상 경험치가 사라진다. 꾸러미 몫이 바뀌었으면 여기서 PlayerProgress에 다시 푼다:
        /// 마을에서 K로 쓴 스킬 점수·랭크는 먼저 꾸러미에 옮기고(쓴 점수만큼 빼기), 그 뒤 ImportFrom(레벨 체력·스킬 보정도 다시 넣음). 알림은 보상 쪽이 이미 띄웠다.
        /// 매 프레임 불러도 바뀐 것이 없으면 아무것도 하지 않는다.
        /// </summary>
        public void SyncProgress()
        {
            if (!Progress) return;
            var carry = ProfileCarry.Ensure();
            if (carry.TotalXp == _syncXp && carry.Level == _syncLevel && carry.SkillPoints == _syncCarryPoints) return;
            var mine = new CarryData();
            Progress.ExportTo(mine);
            int spent = Mathf.Max(0, _syncProgressPoints - mine.SkillPoints);
            carry.SkillRanks = mine.SkillRanks;
            carry.SkillNodes.Clear();
            carry.SkillNodes.UnionWith(mine.SkillNodes);
            carry.SkillPoints = Mathf.Max(0, carry.SkillPoints - spent);
            Progress.ImportFrom(carry);
            SnapshotProgress(carry);
        }

        void SnapshotProgress(CarryData carry)
        {
            _syncXp = carry.TotalXp;
            _syncLevel = carry.Level;
            _syncCarryPoints = carry.SkillPoints;
            _syncProgressPoints = Progress ? Progress.SkillPoints : carry.SkillPoints;
        }

        static int PickFloor(CarryData carry, int floor) => FloorRecipe.Exists(floor) ? floor : carry.RopeEnd(FloorRecipe.MaxTestFloor);

        void LoadDungeon(TripPlan trip)
        {
            if (this == null) return;
            trip.RequestedRealtime = Time.realtimeSinceStartup;
            ProfileCarry.SetTrip(trip);
            if (SceneTravel.Load(SceneTravel.DungeonPath)) return;
            Debug.LogError("[마을] 던전 장면을 불러오지 못했다 — 마을에 남는다");
            ProfileCarry.SetTrip(null);
            _leaving = false;
        }

        /// <summary>방문 기록 한 줄(1-8 TownVisitLog): 머문 시간·대화 합을 Play에서 잰다(체류 4분 이하, 대화 합 60초 이하). 시험 판이면 끝에 표시(검토 1차 Q7).</summary>
        void LogVisit(CarryData carry, TripPlan trip, string how)
        {
            Debug.Log($"[TownVisitLog] 방문 {TownSave.Visits(carry)} · 머문 {VisitSeconds:0.0}초 · 대화 합 {_talkSeconds:0.0}초 · " +
                      $"출발 {TownSave.Departures(carry)} → 제{trip.Floor}층{(trip.Den ? " 바닥 " + OgreDen.Name : "")} {trip.Arrival} ({how})" +
                      (TestRunFlag.Marked ? " · " + TestRunFlag.Label : ""));
        }

        // ── F1 시험 손잡이(TownDebugPanel) ────────────────────

        /// <summary>등불 수를 꾸러미(맡긴 명패)에서 다시 읽어 그림·빛에 넣는다.</summary>
        public void RefreshLamps()
        {
            int lit = TownLayout.LitLamps(TownSave.Tags(ProfileCarry.Ensure()));
            if (Props) Props.SetLitLamps(lit);
            if (Lighting) Lighting.SetLitLamps(lit);
        }

        /// <summary>도착 처리를 다시 돌린다(같은 도착이라 방문 수는 그대로, 새로 받은 명패만 맡김). 명패 줄이 있으면 알린다.</summary>
        public void ReapplyArrival()
        {
            var carry = ProfileCarry.Ensure();
            var kind = ArrivalType == TownArrivalKind.NewPlay ? TownArrivalKind.NewPlay : TownArrivalKind.Basket;
            var r = TownArrivalRules.Apply(carry, kind, QuestCtx);
            string tag = TownScript.NameplateLine(r.NewTags, r.LitLamps);
            if (tag != null) DungeonEvents.Say(tag);
            RefreshLamps();
        }

        /// <summary>F1 '오프닝 다시': 의뢰·이름·장면을 지우고 남쪽 길 끝으로 돌아가 새 플레이처럼 바크 → y 36.5 줄에서 오프닝.</summary>
        public void RestartOpening()
        {
            if (_leaving) return;
            var carry = ProfileCarry.Ensure();
            TownSave.ClearStory(carry);
            new QuestBook(carry).Refresh(QuestCtx);
            if (Player) Player.Teleport(ToVector(TownLayout.NewPlayStart));
            if (CameraRig) CameraRig.Snap();
            _openingLineArmed = true;
            _openingWanted = false;
            _barkAt = Time.realtimeSinceStartup + TownLayout.NewPlayBarkDelay;
        }

        void OnGUI()
        {
            if (_fade <= 0.001f) return;
            GUI.depth = FadeGuiDepth;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static Vector2 ToVector(TownVec v) => new Vector2(v.X, v.Y);
    }
}
