using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Demo6.Game
{
    /// <summary>
    /// 바구니로 올라간 뒤의 화면들(매판 새 탐험 1차 2-2·2-3, IMGUI 임시판): 결과 창 → 밤 카드(검은 화면 2~3초, 이 뒤에서 장면을 다시 불러옴)
    /// → 승강장 고르기(기본 = 줄 끝) → 내려감. DungeonRoot가 부르고 끝나면 콜백으로 돌려준다.
    /// 밤 카드는 장면을 다시 불러오는 동안 끊기지 않게 옛 장면이 ShowNight로 시작하고 새 장면이 ContinueNight로 남은 시간을 잇는다.
    /// 모든 그리기·시간은 실제 시간(창이 시간을 멈춤)이고, GUI.depth는 DungeonRoot 검은 막(-100)보다 위다.
    /// 단추·키 입력은 OnGUI·Update에서 받아 다음 Update에서 한 번만 처리하고, 콜백은 창을 닫은 뒤에 부른다(콜백 안에서 다음 화면을 열 수 있게).
    /// 화면 배치·반응형 다듬기는 Unity 개발 단계로 둔다(기능만).
    /// </summary>
    public sealed class NightCard : MonoBehaviour
    {
        public const string ResultModal = "result";
        public const string LandingModal = "landing";
        /// <summary>밤 카드 동안 여는 창(시간 멈춤·입력 막기). 장면을 다시 불러오면 새 DungeonRoot가 창 상태를 비운다.</summary>
        public const string NightModal = "night";
        /// <summary>밤 카드 길이(초, 실제 시간). 장면을 다시 불러오는 시간이 이 안에 들어야 한다(6장 위험 6).</summary>
        public const float NightSeconds = 2.5f;
        /// <summary>밤 카드가 검게 다 덮는 시간(실제 시간). 첫 줄도 이 안에 다 떠올라, 덮인 프레임이 멈춰도 글이 보인다.</summary>
        public const float CoverSeconds = 0.3f;

        const int GuiDepth = -200;
        /// <summary>둘째 줄부터 떠오르기 시작하는 때(밤 카드 시작부터, 초)와 줄 사이 간격·떠오르는 데 걸리는 시간.</summary>
        const float LineStart = 0.75f;
        const float LineGap = 0.6f;
        const float LineFade = 0.7f;
        /// <summary>끝나기 전 글이 사라지는 시간(검은 화면은 그대로).</summary>
        const float TextFadeOut = 0.35f;
        const float LineHeight = 54f;
        /// <summary>OnGUI가 한 번도 그리지 못해도(게임 화면이 가려짐 등) 이만큼 지나면 덮인 것으로 본다.</summary>
        const float CoverGrace = 0.2f;
        const float ResultWidth = 720f;
        const float LandingWidth = 860f;
        const float Pad = 24f;
        const float ButtonHeight = 44f;
        const float RowGap = 8f;
        const string ResultTitle = "바구니로 올라왔다";
        const string LandingTitle = "갱도 입구 — 어디서 내려갈까";
        const string LandingDesc = "켠 승강장 하나를 고른다. 기본은 가장 깊은 곳('줄 끝').";
        const string LandingFoot = "숫자 키로 고른다. Enter = 기본(테두리 친 줄).";
        /// <summary>키 칸: 0 Enter, 1 숫자판 Enter, 2~10 숫자 1~9, 11~19 숫자판 1~9.</summary>
        const int KeySlots = 20;

        /// <summary>멀리서 긁는 소리를 내는 때(밤 길이에 대한 몫). 한 번에 두 번 '슥슥'.</summary>
        static readonly float[] ScratchAt = { 0.38f, 0.52f, 0.7f, 0.84f };
        static readonly Color NightLineColor = new Color(0.86f, 0.8f, 0.68f, 1f);
        static readonly Color NightSubColor = new Color(0.7f, 0.64f, 0.55f, 1f);

        /// <summary>
        /// 옛 장면 ShowNight가 시작한 때와 끝 시각. 새 장면 ContinueNight가 같은 흐름(둘째 줄이 떠오르는 때)을 잇는다.
        /// 장면을 다시 불러와도 남아야 하므로 정적 값이고, 플레이를 새로 시작할 때 비운다(DungeonRoot.ResetStatics에 넣지 않음).
        /// </summary>
        static float s_nightStart = -1f;
        static float s_nightUntil = -1f;

        public static NightCard Instance { get; private set; }

        /// <summary>결과 창·밤 카드·승강장 고르기 가운데 하나가 떠 있다.</summary>
        public bool Showing { get; private set; }

        enum Mode
        {
            None,
            Results,
            Night,
            Landing,
        }

        struct Cue
        {
            public float At;
            public SfxKind Kind;
            public float Volume;
            public float Pitch;
        }

        Mode _mode;
        /// <summary>이 화면이 연 창 이름(다른 창이 열려 있어 못 열었으면 null, 시간도 멈추지 않음).</summary>
        string _modal;
        int _openedFrame;
        /// <summary>마지막으로 화면 전체를 검게 그린 프레임(결과 창 → 밤 카드로 넘어갈 때 다시 밝아지지 않게).</summary>
        int _blackFrame = -10;
        readonly bool[] _keyDown = new bool[KeySlots];
        readonly List<Cue> _cues = new List<Cue>();

        // 결과 창.
        string _resultLine = "";
        string _resultGain = "";
        Action _onClose;
        bool _clickedClose;

        // 밤 카드.
        string[] _lines = Array.Empty<string>();
        float _nightStart;
        float _nightUntil;
        bool _fromBlack;
        Action _onCovered;
        Action _onDone;
        bool _coverDrawn;
        bool _coveredFired;

        // 승강장 고르기.
        readonly List<LandingOption> _options = new List<LandingOption>();
        readonly List<string> _rowText = new List<string>();
        int _defaultIndex = -1;
        int _defaultFloor = 1;
        Action<int> _onPick;
        int _clickedRow = -1;

        GUIStyle _lineStyle;
        GUIStyle _subStyle;
        GUIStyle _rowStyle;
        GUIStyle _rowDefaultStyle;
        readonly GUIContent _measure = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            s_nightStart = -1f;
            s_nightUntil = -1f;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            // 새 장면의 카드가 이미 자리를 잡았으면(장면을 다시 불러오는 순서에 따라) 그 창을 건드리지 않는다.
            if (Instance != this) return;
            Instance = null;
            if (_modal != null) DungeonUi.Close(_modal);
            _modal = null;
        }

        /// <summary>
        /// 밤 카드 글(2-2 표, 글자 그대로): 첫 원정(밤 없음) "바구니가 덜컹이며 올라간다.",
        /// 첫 귀환의 밤 "바구니가 덜컹이며 올라간다." / "그날 밤, 아홉 해 만에 갱도가 울렸다.", 그 뒤 "밤새 갱도가 울렸다." + 밤 사건 한 줄.
        /// </summary>
        public static string[] NightLines(NightEvent night)
        {
            switch (night)
            {
                case NightEvent.FirstNight:
                    return new[] { "바구니가 덜컹이며 올라간다.", "그날 밤, 아홉 해 만에 갱도가 울렸다." };
                case NightEvent.Collapse:
                    return new[] { "밤새 갱도가 울렸다.", "새벽까지 흙 쏟아지는 소리가 길게 났다." };
                case NightEvent.RatBurrow:
                    return new[] { "밤새 갱도가 울렸다.", "아래서 긁는 소리가 그치지 않았다." };
                case NightEvent.Upheaval:
                    return new[] { "밤새 갱도가 울렸다.", "크게 울린 밤이다. 묻혔던 것이 올라왔을지 모른다." };
                // 거센 울림(1-2층 탐험 맛 1차 4-9): 글은 마을 카드와 같은 TownNight.RumbleLine 한 곳에서.
                case NightEvent.Rumble:
                    return new[] { "밤새 갱도가 울렸다.", Demo6.Core.Town.TownNight.RumbleLine };
                default:
                    return new[] { "바구니가 덜컹이며 올라간다." };
            }
        }

        // ── 여는 곳(DungeonRoot 흐름이 부름) ─────────────────────

        /// <summary>
        /// 결과 창(창 "result", 시간 멈춤, 검은 화면 위 판): "바구니로 올라왔다", 결과 한 줄, 이번 원정 강화석·골드·가장 깊이.
        /// [밤을 보낸다 (Enter)]로 닫으면 창을 닫은 뒤 onClose.
        /// </summary>
        public void ShowResults(ExpeditionSummary summary, Action onClose)
        {
            Open(Mode.Results, ResultModal);
            var s = summary ?? new ExpeditionSummary();
            _resultLine = s.Line();
            _resultGain = $"이번 원정 강화석 +{s.StonesGained} · 골드 +{s.GoldGained} · 가장 깊이 {s.DeepestFloorThisTrip}층";
            _onClose = onClose;
            _clickedClose = false;
        }

        /// <summary>
        /// 밤 카드 시작(옛 장면). 0.3초 동안 검게 덮으며 첫 줄을 띄우고, 덮인 화면을 한 번 그린 다음 프레임에 onCovered를 한 번 부른다
        /// (여기서 DungeonRoot가 장면을 다시 불러옴 — 불러오는 동안 화면이 멈춰도 검은 화면과 첫 줄이 보인다). 둘째 줄부터는 천천히 떠오른다.
        /// 끝 시각은 Time.realtimeSinceStartup + seconds로, 새 장면이 TripPlan.NightUntil로 이어 받는다.
        /// 장면을 다시 불러오지 않으면(시험) 끝 시각까지 보이고 창을 닫는다. 소리: 낙석 소리를 낮게 늘인 울림과 멀리서 긁는 소리(기존 Sfx만).
        /// </summary>
        public void ShowNight(string[] lines, float seconds, Action onCovered)
        {
            // 결과 창처럼 이미 검은 화면이었으면 다시 밝아지지 않게 처음부터 덮는다.
            bool fromBlack = Time.frameCount - _blackFrame <= 2;
            Open(Mode.Night, NightModal);
            float now = Time.realtimeSinceStartup;
            _lines = lines ?? Array.Empty<string>();
            _nightStart = now;
            _nightUntil = now + Mathf.Max(0f, seconds);
            _fromBlack = fromBlack;
            _onCovered = onCovered;
            _onDone = null;
            _coverDrawn = false;
            _coveredFired = false;
            s_nightStart = _nightStart;
            s_nightUntil = _nightUntil;

            // 낮은 울림: 벽 무너지는 소리를 낮게 늘이고, 낙석 소리를 더 낮게 겹쳐 꼬리를 길게.
            AddCue(now, SfxKind.WallBreak, 0.7f, 0.4f);
            AddCue(now + 0.22f, SfxKind.Break, 0.45f, 0.3f);
            AddScratches(_nightStart, _nightUntil, now + 0.3f);
        }

        /// <summary>
        /// 밤 카드 잇기(새 장면, DungeonRoot.Awake 뒤). 처음부터 검은 화면에 같은 글을 until까지 보이고(지났으면 다음 프레임에) 창을 닫은 뒤 onDone.
        /// 옛 장면이 시작한 때를 알면 둘째 줄이 떠오르던 흐름을 그대로 잇는다. 울림 꼬리를 작게 한 번 더 낸다.
        /// </summary>
        public void ContinueNight(string[] lines, float until, Action onDone)
        {
            Open(Mode.Night, NightModal);
            float now = Time.realtimeSinceStartup;
            _lines = lines ?? Array.Empty<string>();
            _nightUntil = until;
            float start = s_nightStart >= 0f && Mathf.Abs(s_nightUntil - until) < 0.05f ? s_nightStart : until - NightSeconds;
            // 덮는 일은 옛 장면에서 끝났다: 첫 줄은 처음부터 다 보이게.
            _nightStart = Mathf.Min(start, now - CoverSeconds);
            _fromBlack = true;
            _onCovered = null;
            _onDone = onDone;
            _coverDrawn = true;
            _coveredFired = true;
            s_nightStart = -1f;
            s_nightUntil = -1f;

            // 울림 꼬리는 바로 낸다(끝 시각이 지났어도 창이 닫히며 지워지지 않게).
            Sfx.PlayScaled(SfxKind.WallBreak, 0.32f, 0.33f);
            AddScratches(start, until, now + 0.25f);
        }

        /// <summary>
        /// 승강장 고르기(창 "landing", 검은 화면 위 판, 2-3 단계 3). 줄마다 층·이름·권장 레벨·측량 장 수·명패·(줄 끝)·(처음).
        /// 기본 줄(defaultFloor, 없으면 줄 끝)을 테두리로 강조하고, 숫자 키·Enter(기본)·단추로 고른다. 고르면 창을 닫은 뒤 onPick(층, '보스방 앞' 줄은 OgreDen.PickCode).
        /// 목록이 비어 있으면 경고를 남기고 다음 프레임에 기본 층으로 고른다.
        /// </summary>
        public void ShowLandingPicker(IReadOnlyList<LandingOption> options, int defaultFloor, Action<int> onPick)
        {
            Open(Mode.Landing, LandingModal);
            _options.Clear();
            _rowText.Clear();
            if (options != null)
                for (int i = 0; i < options.Count; i++)
                    _options.Add(options[i]);
            _defaultFloor = defaultFloor;
            _onPick = onPick;
            _clickedRow = -1;
            _defaultIndex = DefaultIndex(_options, defaultFloor);
            for (int i = 0; i < _options.Count; i++) _rowText.Add(RowText(i + 1, _options[i]));
            if (_options.Count == 0) Debug.LogWarning($"[밤 카드] 승강장 목록이 비어 있어 기본 층({defaultFloor}층)으로 고른다");
        }

        /// <summary>시험용: 결과 창의 [밤을 보낸다] 단추를 누른 것과 같다(다음 Update에서 닫힘).</summary>
        public void PressResultButton()
        {
            if (_mode == Mode.Results) _clickedClose = true;
        }

        /// <summary>시험용: 승강장 고르기의 index번째 줄(0부터) 단추를 누른 것과 같다(다음 Update에서 고름).</summary>
        public void PressLandingRow(int index)
        {
            if (_mode == Mode.Landing && index >= 0 && index < _options.Count) _clickedRow = index;
        }

        /// <summary>기본 줄: 기본 층(줄 끝) 줄, 없으면 줄 끝 표시 줄, 그것도 없으면 마지막 층 줄. '보스방 앞' 줄은 기본이 되지 않는다.</summary>
        public static int DefaultIndex(IReadOnlyList<LandingOption> options, int defaultFloor)
        {
            for (int i = 0; i < options.Count; i++)
                if (!options[i].Den && options[i].Floor == defaultFloor) return i;
            for (int i = 0; i < options.Count; i++)
                if (!options[i].Den && options[i].RopeEnd) return i;
            for (int i = options.Count - 1; i >= 0; i--)
                if (!options[i].Den) return i;
            return options.Count - 1;
        }

        /// <summary>
        /// "[n] 제{층}층 — {이름} · 권장 레벨 {lv} · 측량 {s}/3" + 명패 찾음/못 찾음(명패 없는 층은 생략) + (줄 끝) + (처음).
        /// '보스방 앞' 줄은 "[n] 제2층 바닥 — 보스방 앞 · 권장 레벨 3"(측량·명패 없음).
        /// </summary>
        public static string RowText(int n, LandingOption o)
        {
            if (o.Den) return $"[{n}] 제{o.Floor}층 바닥 — {o.Name} · 권장 레벨 {o.RecommendedLevel}";
            string text = $"[{n}] 제{o.Floor}층 — {o.Name} · 권장 레벨 {o.RecommendedLevel} · 측량 {o.SurveySheets}/3";
            if (o.NameplateFound.HasValue) text += o.NameplateFound.Value ? " · 명패 찾음" : " · 명패 못 찾음";
            if (o.RopeEnd) text += " (줄 끝)";
            if (o.FirstVisit) text += " (처음)";
            return text;
        }

        // ── 열고 닫기 ────────────────────────────────────────

        /// <summary>새 화면을 연다. 떠 있던 화면은 콜백 없이 닫는다.</summary>
        void Open(Mode mode, string modal)
        {
            Abandon();
            _mode = mode;
            _openedFrame = Time.frameCount;
            _modal = DungeonUi.TryOpen(modal) ? modal : null;
            if (_modal == null) Debug.LogWarning($"[밤 카드] 다른 창({DungeonUi.Modal})이 열려 있어 '{modal}' 창 없이 그린다(시간이 멈추지 않음)");
            Showing = true;
            SnapKeys();
        }

        /// <summary>창을 닫고 상태를 비운다(콜백은 부르지 않음).</summary>
        void Abandon()
        {
            if (_modal != null) DungeonUi.Close(_modal);
            _modal = null;
            _mode = Mode.None;
            Showing = false;
            _onClose = null;
            _onCovered = null;
            _onDone = null;
            _onPick = null;
            _clickedClose = false;
            _clickedRow = -1;
            _cues.Clear();
        }

        void AddCue(float at, SfxKind kind, float volume, float pitch) =>
            _cues.Add(new Cue { At = at, Kind = kind, Volume = volume, Pitch = pitch });

        /// <summary>멀리서 긁는 소리: 작은 자갈 소리를 높게 두 번씩('슥슥'). notBefore 앞이나 끝 0.1초 안의 것은 뺀다.</summary>
        void AddScratches(float start, float until, float notBefore)
        {
            float length = until - start;
            if (length <= 0f) return;
            for (int i = 0; i < ScratchAt.Length; i++)
            {
                float at = start + length * ScratchAt[i];
                if (at < notBefore || at > until - 0.1f) continue;
                float pitch = 1.65f + 0.12f * i;
                AddCue(at, SfxKind.Footstep, i % 2 == 0 ? 0.38f : 0.45f, pitch);
                AddCue(at + 0.08f, SfxKind.Footstep, 0.3f, pitch * 1.1f);
            }
        }

        void PlayCues()
        {
            if (_cues.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _cues.Count; i++)
            {
                var c = _cues[i];
                if (c.At > now) continue;
                Sfx.PlayScaled(c.Kind, c.Volume, c.Pitch);
                _cues.RemoveAt(i);
                i--;
            }
        }

        // ── 입력·흐름 ───────────────────────────────────────

        void Update()
        {
            if (_mode == Mode.None) return;
            PlayCues();
            switch (_mode)
            {
                case Mode.Results:
                    UpdateResults();
                    break;
                case Mode.Night:
                    UpdateNight();
                    break;
                case Mode.Landing:
                    UpdateLanding();
                    break;
            }
        }

        void UpdateResults()
        {
            bool enter = false;
            var kb = Keyboard.current;
            if (kb != null && Time.frameCount > _openedFrame) enter = Pressed(kb, 0) | Pressed(kb, 1);
            if (!enter && !_clickedClose) return;
            var callback = _onClose;
            Abandon();
            callback?.Invoke();
        }

        void UpdateNight()
        {
            float now = Time.realtimeSinceStartup;
            bool covered = _coverDrawn || now - _nightStart >= CoverSeconds + CoverGrace;
            if (!_coveredFired && (covered || now >= _nightUntil))
            {
                _coveredFired = true;
                var covering = _onCovered;
                _onCovered = null;
                covering?.Invoke();
                // 콜백이 장면을 다시 불러오거나 다른 화면을 열었으면 여기서 멈춘다.
                if (this == null || _mode != Mode.Night) return;
            }
            if (now < _nightUntil) return;
            var done = _onDone;
            Abandon();
            done?.Invoke();
        }

        void UpdateLanding()
        {
            int pick = _clickedRow;
            if (_options.Count == 0) pick = -1;
            else
            {
                var kb = Keyboard.current;
                if (kb != null && Time.frameCount > _openedFrame)
                {
                    bool enter = Pressed(kb, 0) | Pressed(kb, 1);
                    for (int n = 1; n <= 9; n++)
                    {
                        bool digit = Pressed(kb, 1 + n) | Pressed(kb, 10 + n);
                        if (digit && pick < 0 && n - 1 < _options.Count) pick = n - 1;
                    }
                    if (enter && pick < 0) pick = _defaultIndex;
                }
                if (pick < 0) return;
            }
            // '보스방 앞' 줄은 층 번호 대신 OgreDen.PickCode를 넘긴다.
            int floor = pick >= 0 && pick < _options.Count ? ProfileCarry.PickValue(_options[pick]) : _defaultFloor;
            var callback = _onPick;
            Abandon();
            callback?.Invoke(floor);
        }

        /// <summary>
        /// 눌림: 이번 프레임에 눌렸거나, 지난 검사 때 떼어 있던 키가 지금 눌려 있다(시험 eval이 프레임 사이에 넣은 입력도 한 번 받는다).
        /// 화면을 열 때 지금 눌린 키를 적어 두므로 누른 채로 들어온 키는 받지 않는다.
        /// </summary>
        bool Pressed(Keyboard kb, int slot)
        {
            var key = KeyAt(kb, slot);
            bool down = key != null && key.isPressed;
            bool was = _keyDown[slot];
            _keyDown[slot] = down;
            return (down && !was) || (key != null && key.wasPressedThisFrame);
        }

        void SnapKeys()
        {
            var kb = Keyboard.current;
            for (int i = 0; i < KeySlots; i++)
            {
                var key = kb != null ? KeyAt(kb, i) : null;
                _keyDown[i] = key != null && key.isPressed;
            }
        }

        static KeyControl KeyAt(Keyboard kb, int slot)
        {
            if (slot == 0) return kb.enterKey;
            if (slot == 1) return kb.numpadEnterKey;
            if (slot <= 10) return DigitKey(kb, slot - 1);
            return NumpadKey(kb, slot - 10);
        }

        static KeyControl DigitKey(Keyboard kb, int n)
        {
            switch (n)
            {
                case 1: return kb.digit1Key;
                case 2: return kb.digit2Key;
                case 3: return kb.digit3Key;
                case 4: return kb.digit4Key;
                case 5: return kb.digit5Key;
                case 6: return kb.digit6Key;
                case 7: return kb.digit7Key;
                case 8: return kb.digit8Key;
                case 9: return kb.digit9Key;
                default: return null;
            }
        }

        static KeyControl NumpadKey(Keyboard kb, int n)
        {
            switch (n)
            {
                case 1: return kb.numpad1Key;
                case 2: return kb.numpad2Key;
                case 3: return kb.numpad3Key;
                case 4: return kb.numpad4Key;
                case 5: return kb.numpad5Key;
                case 6: return kb.numpad6Key;
                case 7: return kb.numpad7Key;
                case 8: return kb.numpad8Key;
                case 9: return kb.numpad9Key;
                default: return null;
            }
        }

        // ── 그리기 ───────────────────────────────────────────

        void EnsureStyles()
        {
            if (_lineStyle != null) return;
            _lineStyle = new GUIStyle(DungeonUi.Display) { fontSize = 34 };
            _subStyle = new GUIStyle(DungeonUi.Subtitle) { fontSize = 24 };
            _rowStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset(14, 10, 6, 6),
            };
            _rowDefaultStyle = new GUIStyle(_rowStyle);
            _rowDefaultStyle.normal.textColor = DungeonUi.Ember;
            _rowDefaultStyle.hover.textColor = DungeonUi.Ember;
            _rowDefaultStyle.focused.textColor = DungeonUi.Ember;
        }

        void OnGUI()
        {
            if (_mode == Mode.None) return;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = GuiDepth;
            switch (_mode)
            {
                case Mode.Results:
                    DrawResults();
                    break;
                case Mode.Night:
                    DrawNight();
                    break;
                case Mode.Landing:
                    DrawLanding();
                    break;
            }
        }

        void FillBlack(float alpha)
        {
            DungeonUi.Fill(new Rect(0f, 0f, DungeonUi.Width, DungeonUi.Height), new Color(0f, 0f, 0f, alpha));
            if (alpha >= 0.999f && Event.current.type == EventType.Repaint) _blackFrame = Time.frameCount;
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>글 높이(줄바꿈 포함). 공용 글꼴 크기가 바뀌어도 판이 글을 자르지 않게 잰다.</summary>
        float TextHeight(GUIStyle style, string text, float width)
        {
            _measure.text = text;
            return Mathf.Ceil(style.CalcHeight(_measure, width));
        }

        /// <summary>승강장 줄 단추 높이(긴 줄은 두 줄로 접힘, 최소 ButtonHeight).</summary>
        float RowHeight(int index, float width) => Mathf.Max(ButtonHeight, TextHeight(_rowStyle, _rowText[index], width));

        void DrawResults()
        {
            FillBlack(1f);
            float w = ResultWidth - Pad * 2f;
            float titleH = Mathf.Max(40f, TextHeight(DungeonUi.Title, ResultTitle, w-60f));
            float lineH = TextHeight(DungeonUi.Label, _resultLine, w);
            float gainH = TextHeight(DungeonUi.Label, _resultGain, w);
            float h = Pad + titleH + 12f + lineH + 6f + gainH + 20f + ButtonHeight + Pad;
            var r = new Rect((DungeonUi.Width - ResultWidth) * 0.5f, (DungeonUi.Height - h) * 0.5f, ResultWidth, h);
            DungeonUi.Box(r, 0.95f);
            float x = r.x + Pad;
            float y = r.y + Pad;
            GUI.Label(new Rect(x, y, w-60f, titleH), ResultTitle, DungeonUi.Title);
            if (DungeonUi.CloseButton(r, "닫고 밤을 보낸다 · Enter")) _clickedClose = true;
            y += titleH + 12f;
            GUI.Label(new Rect(x, y, w, lineH), _resultLine, DungeonUi.Label);
            y += lineH + 6f;
            GUI.Label(new Rect(x, y, w, gainH), _resultGain, DungeonUi.Label);
            y += gainH + 20f;
            if (GUI.Button(new Rect(x, y, w, ButtonHeight), "밤을 보낸다 (Enter)")) _clickedClose = true;
        }

        /// <summary>밤 카드: 검은 화면이 0.3초에 덮이고(이미 검었으면 바로), 첫 줄은 같이, 둘째 줄부터는 천천히 떠오른다. 끝 0.35초에 글만 사라진다.</summary>
        void DrawNight()
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - _nightStart;
            float cover = _fromBlack ? 1f : Smooth01(elapsed / CoverSeconds);
            FillBlack(cover);

            float fadeOut = Mathf.Clamp01((_nightUntil - now) / TextFadeOut);
            float w = DungeonUi.Width;
            float y = DungeonUi.Height * 0.46f - _lines.Length * LineHeight * 0.5f;
            for (int i = 0; i < _lines.Length; i++)
            {
                float a = i == 0 ? Smooth01(elapsed / CoverSeconds) : Smooth01((elapsed - LineStart - (i - 1) * LineGap) / LineFade);
                a *= fadeOut;
                var c = i == 0 ? NightLineColor : NightSubColor;
                DungeonUi.ShadowLabel(new Rect(0f, y + i * LineHeight, w, LineHeight), _lines[i], i == 0 ? _lineStyle : _subStyle,
                    new Color(c.r, c.g, c.b, a));
            }

            // 다 덮이고 첫 줄이 다 떠오른 화면을 실제로 그렸다 → 다음 Update에서 onCovered(장면을 다시 불러와도 이 화면이 남는다).
            if (Event.current.type == EventType.Repaint && elapsed >= CoverSeconds) _coverDrawn = true;
        }

        void DrawLanding()
        {
            FillBlack(1f);
            float w = LandingWidth - Pad * 2f;
            float titleH = TextHeight(DungeonUi.Title, LandingTitle, w);
            float descH = TextHeight(DungeonUi.Small, LandingDesc, w);
            float footH = TextHeight(DungeonUi.Small, LandingFoot, w);
            string empty = _options.Count == 0 ? $"켠 승강장이 없다 — 제{_defaultFloor}층으로 내려간다." : null;
            float rowsH = 0f;
            if (empty != null) rowsH = TextHeight(DungeonUi.Small, empty, w) + RowGap;
            for (int i = 0; i < _options.Count; i++) rowsH += RowHeight(i, w) + RowGap;
            float h = Pad + titleH + 6f + descH + 16f + rowsH + 6f + footH + Pad;
            var r = new Rect((DungeonUi.Width - LandingWidth) * 0.5f, (DungeonUi.Height - h) * 0.5f, LandingWidth, h);
            DungeonUi.Box(r, 0.95f);
            float x = r.x + Pad;
            float y = r.y + Pad;
            GUI.Label(new Rect(x, y, w, titleH), LandingTitle, DungeonUi.Title);
            y += titleH + 6f;
            GUI.Label(new Rect(x, y, w, descH), LandingDesc, DungeonUi.Small);
            y += descH + 16f;

            if (empty != null)
            {
                float eh = TextHeight(DungeonUi.Small, empty, w);
                GUI.Label(new Rect(x, y, w, eh), empty, DungeonUi.Small);
                y += eh + RowGap;
            }
            for (int i = 0; i < _options.Count; i++)
            {
                float rh = RowHeight(i, w);
                var row = new Rect(x, y, w, rh);
                bool isDefault = i == _defaultIndex;
                if (isDefault) DungeonUi.Outline(new Rect(row.x - 3f, row.y - 3f, row.width + 6f, row.height + 6f), DungeonUi.Ember, 2f);
                if (GUI.Button(row, _rowText[i], isDefault ? _rowDefaultStyle : _rowStyle)) _clickedRow = i;
                y += rh + RowGap;
            }
            y += 6f;
            var prev = GUI.color;
            GUI.color = DungeonUi.BoneDim;
            GUI.Label(new Rect(x, y, w, footH), LandingFoot, DungeonUi.Small);
            GUI.color = prev;
        }
    }
}
