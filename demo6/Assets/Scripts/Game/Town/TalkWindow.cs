using System;
using System.Collections.Generic;
using System.Text;
using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Demo6.Game
{
    /// <summary>
    /// 대화창(기획/마을-의뢰-첫판.md 3-2·5장·6-3, 꾸러미 3, IMGUI 임시판). 주민에게 말을 걸면 TalkDirector.Build로 이어 볼 장면들
    /// (반응 → 보고 → 받기, 없으면 반복 대사)을 받아 차례로 튼다. 창 "talk"(DungeonUi.TryOpen: 시간 멈춤·입력 막기)를 열므로 키는
    /// Keyboard·Mouse에서 직접 읽는다. 마을에는 위협이 없어 시간이 멈춰도 문제없다.
    /// 화자 규칙: 줄마다 이름표는 그 줄이 시작될 때 TalkDirector.Label로 한 번 정하고(한 줄 안에서는 바뀌지 않음), 줄을 넘기는 순간
    /// TalkDirector.PassLine이 이름 인지를 꾸러미에 적는다. 이름은 이 창에서 SpeakerIdentity를 거친 이름표로만 보인다(문장 속 이름은 대본 그대로).
    /// 글은 초당 40자(실제 시간)로 찍는다. F·Space·마우스 왼쪽(Enter)으로 넘기고, 찍는 중에 누르면 그 줄을 다 보인다.
    /// 0.4초 넘게 누르고 있으면 초당 6마디로 넘긴다(선택지·보상 줄에서는 멈춤). 창을 연 F처럼 열 때 눌려 있던 키는 한 번 떼어야 듣는다.
    /// 선택지는 [1][2][3]이나 단추로 고르고, 고르면 같은 화자의 대답 한 줄이 나온다(이야기는 갈라지지 않음).
    /// Esc → "이 장면 건너뛰기? [Enter] 예 / [Esc] 아니오". 건너뛰면 TalkDirector.Skip이 남은 줄의 이름 공개와 끝 효과(받기·보고·본 장면)를
    /// 끝까지 본 것과 똑같이 넣고, 요약 한 줄과 보상 줄을 보인 뒤 1.5초 동안 그 주민들 머리 위 ▼를 차례로 띄운다.
    /// 장면이 끝나면 TalkDirector.End(보상은 여기서 꾸러미에 바로 들어감)의 보상 줄을 창에 보이고, 마을 알림(품삯 — …)은 창을 닫을 때
    /// DungeonEvents.Say로 띄운다. GUI.depth −50(도착 카드 −60 아래, 권양기·의뢰 목록 창 위). 화면 배치·반응형 다듬기는 Unity 개발 단계로 둔다.
    /// </summary>
    public sealed class TalkWindow : MonoBehaviour
    {
        public const string ModalName = "talk";
        /// <summary>글 찍는 빠르기(초당 글자, 실제 시간).</summary>
        public const float CharsPerSecond = 40f;
        /// <summary>설정 '대사 글 바로 보이기'(저장·처음 화면·멈춤 창 1차 6-1). 켜면 대사 글을 한 번에 다 보인다. SettingsStore가 게임을 켤 때 넣는다.</summary>
        public static bool InstantText;
        /// <summary>이만큼 누르고 있으면 빨리 넘기기(초).</summary>
        public const float HoldDelay = 0.4f;
        /// <summary>빨리 넘기기 빠르기(초당 마디).</summary>
        public const float HoldRate = 6f;
        /// <summary>건너뛴 뒤 ▼를 차례로 띄우는 시간(초, 실제 시간).</summary>
        public const float ArrowSeconds = 1.5f;

        const int GuiDepth = -50;
        const float MaxWidth = 760f;
        const float SideGap = 16f;
        const float BottomGap = 26f;
        const float Pad = 22f;
        const float MinTextHeight = 58f;
        const float ChoiceHeight = 40f;
        const float ChoiceGap = 6f;
        const float FootHeight = 24f;
        const float TabHeight = 34f;
        const string FootLine = "F · Space 넘기기    Esc 건너뛰기";
        const string FootChoice = "숫자 키로 고르기    Esc 건너뛰기";
        const string FootResult = "F · Space 계속";

        /// <summary>나레이션 글(흐린 베이지).</summary>
        static readonly Color NarrationColor = new Color(0.78f, 0.72f, 0.60f, 1f);
        static readonly Color SpeechColor = new Color(0.9f, 0.86f, 0.77f, 1f);
        static readonly Color NameColor = new Color(0.88f, 0.77f, 0.57f, 1f);
        static readonly Color RewardColor = new Color(0.9f, 0.7f, 0.4f, 1f);
        static readonly Color FootColor = new Color(0.69f, 0.66f, 0.60f, 0.8f);

        /// <summary>키 칸: 0 F, 1 Space, 2 Enter, 3 숫자판 Enter, 4 Esc, 5~7 숫자 1~3, 8~10 숫자판 1~3, 11 마우스 왼쪽.</summary>
        const int KeySlots = 12;
        const int KeyF = 0, KeySpace = 1, KeyEnter = 2, KeyPadEnter = 3, KeyEsc = 4, KeyDigit = 5, KeyPad = 8, KeyMouse = 11;

        enum Phase
        {
            Closed,
            /// <summary>대사 한 줄(찍는 중이거나 다 보임).</summary>
            Line,
            /// <summary>선택지 줄을 다 찍고 고르기를 기다림.</summary>
            Choice,
            /// <summary>고른 선택지의 대답 한 줄.</summary>
            Reply,
            /// <summary>장면 끝 보상 줄·건너뛰기 요약.</summary>
            Result,
        }

        public static TalkWindow Instance { get; private set; }

        /// <summary>
        /// F1 시험 '오우거 굴 있음 가정'(굴이 없는 빌드에서 받기·문구만 볼 때). 켜면 대화의 의뢰 다시 계산이 오우거 굴이 있는 것처럼 한다.
        /// 지금은 2층 계단 아래 굴이 있어 QuestContext.Live만으로 열리고 실제 처치로 센다.
        /// 장면을 바꿔도 남고(ResetStatics가 지우지 않음), 플레이를 새로 시작하면 꺼진다.
        /// </summary>
        public static bool AssumeOgreDen { get; set; }

        /// <summary>대화의 의뢰 다시 계산 조건: 지금 던전 상태(QuestContext.Live) 또는 시험 가정.</summary>
        public static QuestContext Context => AssumeOgreDen ? new QuestContext(true) : QuestContext.Live;

        /// <summary>대화창이 열려 있다.</summary>
        public static bool AnyOpen => Instance && Instance.IsOpen;

        /// <summary>머리 위 ▼를 띄울 주민: 대화 중이면 지금 줄의 화자(나레이션이면 없음), 건너뛴 뒤 1.5초 동안은 장면 화자를 차례로. 없으면 null.</summary>
        public static string ArrowNpcId => Instance ? Instance.CurrentArrow : null;

        public bool IsOpen => _phase != Phase.Closed;
        /// <summary>이번 대화를 연 주민(오프닝은 춘삼이 아닌 옥금에게 말을 걸어도 열린다).</summary>
        public string NpcId { get; private set; }
        /// <summary>지금 장면(닫혀 있으면 null).</summary>
        public TalkScene Scene => IsOpen && _sceneIndex < _scenes.Count ? _scenes[_sceneIndex] : null;
        /// <summary>지금 줄 번호(0부터).</summary>
        public int LineIndex => _lineIndex;
        /// <summary>지금 줄의 이름표(줄이 시작될 때 정함, 나레이션·보상 줄은 빈 글).</summary>
        public string CurrentLabel => _phase == Phase.Line || _phase == Phase.Choice || _phase == Phase.Reply ? _label : "";
        /// <summary>건너뛰기 확인을 묻는 중이다.</summary>
        public bool Confirming => _confirm;
        /// <summary>이 장면에서 대화창이 열려 있던 시간 합(초, 실제 시간). 방문 기록(대화 합 60초 이하)에 쓴다.</summary>
        public float TalkSeconds { get; private set; }
        /// <summary>이 장면에서 연 대화 수.</summary>
        public int Conversations { get; private set; }

        /// <summary>장면 하나가 끝남(끝까지 봤거나 건너뜀). 받기·보고·공개·보상은 이미 꾸러미에 들어갔다.</summary>
        public event Action<TalkResult> SceneEnded;
        /// <summary>대화창이 닫힘(알림을 띄운 뒤).</summary>
        public event Action Closed;

        Phase _phase;
        readonly List<TalkScene> _scenes = new List<TalkScene>();
        int _sceneIndex;
        int _lineIndex;
        string _label = "";
        string _text = "";
        bool _narration;
        float _typeStart;
        bool _typedFull;
        bool _confirm;
        int _openedFrame;
        float _openedAt;
        int _pendingChoice = -1;
        int _pendingConfirm;
        readonly List<string> _resultLines = new List<string>();
        readonly List<bool> _resultReward = new List<bool>();
        readonly List<string> _notices = new List<string>();
        readonly List<string> _sceneLog = new List<string>();
        readonly List<string> _arrowIds = new List<string>();
        float _arrowStart = -99f;
        int _rewardsThisTalk;

        readonly bool[] _down = new bool[KeySlots];
        readonly bool[] _armed = new bool[KeySlots];
        float _holdSince = -1f;
        float _nextHoldTick;

        GUIStyle _speech;
        GUIStyle _narrate;
        GUIStyle _name;
        GUIStyle _foot;
        GUIStyle _choice;
        GUIStyle _prompt;
        Font _stylesFont;
        readonly GUIContent _measure = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            AssumeOgreDen = false;
        }

        /// <summary>장면 정적 비우기(SceneStatics.Reset이 부름). AssumeOgreDen(F1 가정)은 남긴다.</summary>
        public static void ResetStatics() => Instance = null;

        /// <summary>이 장면의 대화창(없으면 만든다).</summary>
        public static TalkWindow Ensure()
        {
            if (Instance) return Instance;
            var found = FindAnyObjectByType<TalkWindow>();
            if (found) return Instance = found;
            return new GameObject("TalkWindow").AddComponent<TalkWindow>();
        }

        void Awake()
        {
            if (Instance && Instance != this) Debug.LogWarning("[대화] 대화창이 둘이다. 나중 것을 쓴다");
            Instance = this;
        }

        void OnDestroy()
        {
            if (_phase != Phase.Closed && DungeonUi.Modal == ModalName) DungeonUi.Close(ModalName);
            _phase = Phase.Closed;
            if (Instance == this) Instance = null;
        }

        // ── 여는 곳 ─────────────────────────────────────────

        /// <summary>주민에게 말을 건다(TalkDirector.Build → 장면들을 차례로). 이미 열려 있거나 다른 창이 열려 있으면 false.</summary>
        public bool Talk(string npcId)
        {
            if (IsOpen || !NpcTable.Exists(npcId)) return false;
            var plan = TalkDirector.Build(npcId, ProfileCarry.Ensure(), Context);
            return Play(plan);
        }

        /// <summary>장면 목록을 튼다(꾸러미 2: 오프닝은 TalkDirector.Build(NpcTable.Gate, carry)의 결과를 넘김). 장면이 없으면 false.</summary>
        public bool Play(TalkPlan plan)
        {
            if (plan == null) return false;
            return Open(plan.NpcId, plan.Scenes);
        }

        /// <summary>장면 하나를 튼다(시험 패널 '오프닝 다시' 등). npcId가 없으면 장면 주인.</summary>
        public bool Play(TalkScene scene, string npcId = null)
        {
            if (scene == null) return false;
            return Open(string.IsNullOrEmpty(npcId) ? scene.OwnerNpcId : npcId, new[] { scene });
        }

        bool Open(string npcId, IEnumerable<TalkScene> scenes)
        {
            if (IsOpen) return false;
            _scenes.Clear();
            foreach (var s in scenes)
                if (s != null && s.Lines.Length > 0) _scenes.Add(s);
            if (_scenes.Count == 0) return false;
            if (!DungeonUi.TryOpen(ModalName))
            {
                _scenes.Clear();
                return false;
            }
            NpcId = npcId ?? "";
            _openedFrame = Time.frameCount;
            _openedAt = Time.unscaledTime;
            _notices.Clear();
            _sceneLog.Clear();
            _rewardsThisTalk = 0;
            _confirm = false;
            _pendingChoice = -1;
            _pendingConfirm = 0;
            _holdSince = -1f;
            Conversations++;
            SnapKeys();
            StartScene(0);
            return true;
        }

        /// <summary>이 주민이 이번 대화에 나오나(말을 건 주민이거나 지금 장면의 화자). 주민이 플레이어 쪽으로 돌 때 쓴다.</summary>
        public bool Involves(string npcId)
        {
            if (!IsOpen || string.IsNullOrEmpty(npcId)) return false;
            if (NpcId == npcId) return true;
            var scene = Scene;
            if (scene == null) return false;
            foreach (var l in scene.Lines)
                if (l.SpeakerId == npcId) return true;
            return false;
        }

        // ── 시험 입구(F1·eval) ────────────────────────────────

        /// <summary>넘기기 한 번(F·Space·클릭과 같다): 찍는 중이면 다 보이고, 다 보였으면 다음 줄(선택지 줄은 고르기를 기다림).</summary>
        public void Advance()
        {
            if (_confirm) return;
            switch (_phase)
            {
                case Phase.Line:
                case Phase.Reply:
                    if (!Typed)
                    {
                        _typedFull = true;
                        if (_phase == Phase.Line && HasChoices) _phase = Phase.Choice;
                        return;
                    }
                    if (_phase == Phase.Line && HasChoices)
                    {
                        _phase = Phase.Choice;
                        return;
                    }
                    PassLine();
                    return;
                case Phase.Result:
                    NextScene();
                    return;
            }
        }

        /// <summary>선택지 고르기(0부터). 고르기를 기다리는 중이 아니거나 번호가 없으면 false.</summary>
        public bool Choose(int index)
        {
            var line = CurrentLine;
            if (_confirm || _phase != Phase.Choice || line == null || index < 0 || index >= line.Choices.Length) return false;
            _phase = Phase.Reply;
            _text = line.Choices[index].Reply ?? "";
            StartTyping();
            return true;
        }

        /// <summary>Esc와 같다: 건너뛰기 확인을 묻는다(보상 줄에서는 넘기기).</summary>
        public void RequestSkip()
        {
            if (_phase == Phase.Result)
            {
                NextScene();
                return;
            }
            if (_phase == Phase.Line || _phase == Phase.Choice || _phase == Phase.Reply) _confirm = true;
        }

        /// <summary>건너뛰기 확인에 답한다(예 = 이 장면 건너뛰기).</summary>
        public void AnswerSkip(bool yes)
        {
            if (!_confirm) return;
            _confirm = false;
            if (yes) SkipScene();
        }

        /// <summary>시험: 남은 장면을 모두 건너뛰고(효과는 끝까지 본 것과 같음) 창을 닫는다.</summary>
        public void SkipAll()
        {
            int guard = 0;
            while (IsOpen && guard++ < 64)
            {
                _confirm = false;
                if (_phase == Phase.Result) NextScene();
                else SkipScene();
            }
        }

        // ── 흐름 ───────────────────────────────────────────

        TalkLine CurrentLine
        {
            get
            {
                var scene = Scene;
                return scene != null && _lineIndex >= 0 && _lineIndex < scene.Lines.Length ? scene.Lines[_lineIndex] : null;
            }
        }

        bool HasChoices
        {
            get
            {
                var line = CurrentLine;
                return line != null && line.Choices.Length > 0;
            }
        }

        int VisibleChars => _typedFull || InstantText ? _text.Length : Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - _typeStart) * CharsPerSecond), 0, _text.Length);

        bool Typed => VisibleChars >= _text.Length;

        bool HoldMode => _holdSince >= 0f && Time.unscaledTime - _holdSince >= HoldDelay;

        void StartScene(int index)
        {
            _sceneIndex = index;
            _lineIndex = 0;
            _sceneLog.Add(_scenes[index].Id);
            StartLine();
        }

        void StartLine()
        {
            // 줄 단계를 먼저 둔다: 창을 막 연 첫 줄은 아직 Closed라 CurrentLine(→ Scene → IsOpen)이 null이 되어 글·이름표가 빈 채로 나왔다.
            _phase = Phase.Line;
            var line = CurrentLine;
            _label = TalkDirector.Label(line, ProfileCarry.Ensure());
            _narration = line == null || line.IsNarration;
            _text = line != null ? line.Text ?? "" : "";
            StartTyping();
        }

        void StartTyping()
        {
            _typeStart = Time.unscaledTime;
            // 빨리 넘기는 중이면 새 줄을 처음부터 다 보인다(넘기는 박자가 초당 6마디).
            _typedFull = HoldMode;
        }

        /// <summary>줄을 넘긴다: 이름 공개를 적고 다음 줄, 마지막 줄이면 장면 끝 효과.</summary>
        void PassLine()
        {
            var carry = ProfileCarry.Ensure();
            TalkDirector.PassLine(CurrentLine, carry);
            _lineIndex++;
            var scene = Scene;
            if (scene != null && _lineIndex < scene.Lines.Length)
            {
                StartLine();
                return;
            }
            var result = RunEffects(() => TalkDirector.End(scene, carry, Context));
            ShowResult(result);
        }

        /// <summary>이 장면 건너뛰기: 남은 공개와 끝 효과를 끝까지 본 것과 같게 넣고 요약·보상 줄을 보인 뒤 ▼를 차례로.</summary>
        void SkipScene()
        {
            if (_phase != Phase.Line && _phase != Phase.Choice && _phase != Phase.Reply) return;
            var scene = Scene;
            var carry = ProfileCarry.Ensure();
            int from = _lineIndex;
            var result = RunEffects(() => TalkDirector.Skip(scene, from, carry, Context));
            _arrowIds.Clear();
            _arrowIds.AddRange(result.Speakers);
            _arrowStart = Time.unscaledTime;
            ShowResult(result);
        }

        /// <summary>
        /// 장면 끝 효과를 넣는다. 마을에서는 꾸러미와 PlayerProgress(K 스킬 창)를 맞추는 일을 TownRoot.SyncProgress 한 곳에 맡긴다:
        /// 효과(보상)가 꾸러미에 들어간 뒤 곧바로 불러, 마을에서 쓴 스킬 점수를 한 번만 빼고 보상 몫을 다시 푼다. 여기서 ExportTo로 꾸러미를
        /// 먼저 바꾸면 SyncProgress의 기준값이 낡아 쓴 점수가 두 번 빠진다(반복 대사 한 번에 1점을 잃음).
        /// TownRoot가 없을 때만 옛 길(꾸러미와 맞으면 먼저 담고, 보상이 들어갔으면 다시 풀기)을 쓴다.
        /// </summary>
        static TalkResult RunEffects(Func<TalkResult> effects)
        {
            var town = TownRoot.Instance;
            if (town)
            {
                var townResult = effects() ?? new TalkResult();
                town.SyncProgress();
                return townResult;
            }
            var carry = ProfileCarry.Ensure();
            var progress = PlayerProgress.Instance;
            bool synced = progress && progress.TotalXp == carry.TotalXp;
            if (synced) progress.ExportTo(carry);
            var result = effects() ?? new TalkResult();
            if (synced && result.Rewards.Count > 0) progress.ImportFrom(carry);
            return result;
        }

        void ShowResult(TalkResult result)
        {
            _resultLines.Clear();
            _resultReward.Clear();
            if (result.Skipped && !string.IsNullOrEmpty(result.Summary))
            {
                _resultLines.Add(result.Summary);
                _resultReward.Add(false);
            }
            foreach (var line in result.Lines)
            {
                _resultLines.Add(line);
                _resultReward.Add(true);
            }
            _notices.AddRange(result.Notices);
            if (result.Rewards.Count > 0)
            {
                _rewardsThisTalk += result.Rewards.Count;
                bool leveled = false;
                foreach (var r in result.Rewards) leveled |= r.LeveledUp;
                Sfx.Play(leveled ? SfxKind.LevelUp : SfxKind.Pickup);
            }
            QuestHud.Refresh();
            try
            {
                SceneEnded?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (!IsOpen) return;
            if (_resultLines.Count > 0)
            {
                _phase = Phase.Result;
                _label = "";
                return;
            }
            NextScene();
        }

        void NextScene()
        {
            if (_sceneIndex + 1 < _scenes.Count)
            {
                StartScene(_sceneIndex + 1);
                return;
            }
            CloseWindow();
        }

        /// <summary>창을 닫고 모아 둔 마을 알림을 띄운다.</summary>
        void CloseWindow()
        {
            _phase = Phase.Closed;
            _confirm = false;
            DungeonUi.Close(ModalName);
            float seconds = Time.unscaledTime - _openedAt;
            Debug.Log($"[대화] {NpcId}: {string.Join(" → ", _sceneLog)} · {seconds:0.0}초 · 보상 {_rewardsThisTalk} · 이 장면 대화 합 {TalkSeconds:0.0}초");
            foreach (var n in _notices)
                if (!string.IsNullOrEmpty(n)) DungeonEvents.Say(n);
            _notices.Clear();
            _scenes.Clear();
            QuestHud.Refresh();
            Closed?.Invoke();
            // 무진과 이야기를 마치면 배울 기술이 있을 때 배우기 창(기획/스킬-자원-트리-1차.md 2장, 점수가 있어야 배운다).
            if (NpcId == NpcTable.Trainer) SkillPanel.OpenTeach();
        }

        string CurrentArrow
        {
            get
            {
                if (_phase == Phase.Line || _phase == Phase.Choice || _phase == Phase.Reply)
                {
                    var line = CurrentLine;
                    if (line != null && !line.IsNarration) return line.SpeakerId;
                }
                int n = _arrowIds.Count;
                if (n == 0) return null;
                float t = Time.unscaledTime - _arrowStart;
                if (t < 0f || t >= ArrowSeconds) return null;
                return _arrowIds[Mathf.Clamp((int)(t / (ArrowSeconds / n)), 0, n - 1)];
            }
        }

        // ── 입력 ───────────────────────────────────────────

        void Update()
        {
            if (_phase == Phase.Closed) return;
            TalkSeconds += Time.unscaledDeltaTime;
            if (DungeonUi.Modal != ModalName)
            {
                // 장면을 바꾸는 정적 비우기 등으로 창이 사라졌다. 효과 없이 닫는다(받기·보고는 장면 끝에서만 들어감).
                Debug.LogWarning($"[대화] 창 '{ModalName}'이 밖에서 닫혀 대화를 멈춘다(지금 창: {DungeonUi.Modal})");
                _phase = Phase.Closed;
                _confirm = false;
                _scenes.Clear();
                return;
            }
            if (_phase == Phase.Line && HasChoices && Typed) _phase = Phase.Choice;

            bool listen = Time.frameCount > _openedFrame;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool f = Poll(KeyF, kb, mouse, out bool fDown);
            bool space = Poll(KeySpace, kb, mouse, out bool spaceDown);
            bool enter = Poll(KeyEnter, kb, mouse, out bool enterDown) | Poll(KeyPadEnter, kb, mouse, out bool padDown);
            bool esc = Poll(KeyEsc, kb, mouse, out _);
            bool click = Poll(KeyMouse, kb, mouse, out bool clickDown);
            int digit = -1;
            for (int i = 0; i < 3; i++)
            {
                bool d = Poll(KeyDigit + i, kb, mouse, out _) | Poll(KeyPad + i, kb, mouse, out _);
                if (d && digit < 0) digit = i;
            }
            if (!listen) return;

            if (_confirm)
            {
                int answer = _pendingConfirm;
                _pendingConfirm = 0;
                if (enter || answer > 0) AnswerSkip(true);
                else if (esc || answer < 0) AnswerSkip(false);
                _holdSince = -1f;
                return;
            }
            if (_pendingChoice >= 0)
            {
                int pick = _pendingChoice;
                _pendingChoice = -1;
                Choose(pick);
                return;
            }
            if (esc)
            {
                RequestSkip();
                _holdSince = -1f;
                return;
            }
            if (_phase == Phase.Choice)
            {
                if (digit >= 0) Choose(digit);
                _holdSince = -1f;
                return;
            }

            // 넘기기: 새로 누른 키 하나에 한 번. 누르고 있으면 0.4초 뒤부터 초당 6마디(선택지·보상 줄에서는 멈춤).
            bool held = (fDown && _armed[KeyF]) || (spaceDown && _armed[KeySpace]) || (enterDown && _armed[KeyEnter]) ||
                        (padDown && _armed[KeyPadEnter]) || (clickDown && _armed[KeyMouse]);
            bool pressed = f || space || enter || click;
            if (!held) _holdSince = -1f;
            if (pressed)
            {
                if (_holdSince < 0f) _holdSince = Time.unscaledTime;
                _nextHoldTick = Time.unscaledTime + HoldDelay;
                Advance();
                return;
            }
            if (held && HoldMode && (_phase == Phase.Line || _phase == Phase.Reply) && Time.unscaledTime >= _nextHoldTick)
            {
                _nextHoldTick = Time.unscaledTime + 1f / HoldRate;
                if (_phase == Phase.Line && HasChoices)
                {
                    _typedFull = true;
                    _phase = Phase.Choice;
                    return;
                }
                _typedFull = true;
                PassLine();
            }
        }

        /// <summary>
        /// 키 하나 읽기: 새로 눌렸으면 true(이번 프레임에 눌렀다 뗀 것도 한 번). 창을 열 때 눌려 있던 키는 한 번 뗀 뒤에야 듣는다
        /// (대화를 연 F를 첫 줄 넘기기로 쓰지 않음, 밤 카드와 같은 방식).
        /// </summary>
        bool Poll(int slot, Keyboard kb, Mouse mouse, out bool down)
        {
            var c = Control(slot, kb, mouse);
            down = c != null && c.isPressed;
            bool was = _down[slot];
            _down[slot] = down;
            if (!down) _armed[slot] = true;
            if (!_armed[slot]) return false;
            return (down && !was) || (c != null && c.wasPressedThisFrame);
        }

        void SnapKeys()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            for (int i = 0; i < KeySlots; i++)
            {
                var c = Control(i, kb, mouse);
                bool down = c != null && c.isPressed;
                _down[i] = down;
                _armed[i] = !down;
            }
        }

        static ButtonControl Control(int slot, Keyboard kb, Mouse mouse)
        {
            if (slot == KeyMouse) return mouse?.leftButton;
            if (kb == null) return null;
            switch (slot)
            {
                case KeyF: return kb.fKey;
                case KeySpace: return kb.spaceKey;
                case KeyEnter: return kb.enterKey;
                case KeyPadEnter: return kb.numpadEnterKey;
                case KeyEsc: return kb.escapeKey;
                case KeyDigit: return kb.digit1Key;
                case KeyDigit + 1: return kb.digit2Key;
                case KeyDigit + 2: return kb.digit3Key;
                case KeyPad: return kb.numpad1Key;
                case KeyPad + 1: return kb.numpad2Key;
                case KeyPad + 2: return kb.numpad3Key;
                default: return null;
            }
        }

        // ── 그리기 ───────────────────────────────────────────

        void EnsureStyles()
        {
            var font = DungeonUi.Label.font;
            if (_speech != null && _stylesFont == font) return;
            _stylesFont = font;
            _speech = new GUIStyle(DungeonUi.Label) { fontSize = 21, wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft };
            _speech.normal.textColor = Color.white;
            _narrate = new GUIStyle(_speech) { fontStyle = FontStyle.Italic, alignment = TextAnchor.UpperCenter };
            if (DungeonUi.Serif) _narrate.font = DungeonUi.Serif;
            _name = new GUIStyle(DungeonUi.Bold) { fontSize = 20, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow };
            _name.normal.textColor = Color.white;
            _foot = new GUIStyle(DungeonUi.Small) { fontSize = 14, alignment = TextAnchor.MiddleRight, wordWrap = false };
            _foot.normal.textColor = Color.white;
            _choice = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset(14, 10, 6, 6) };
            _prompt = new GUIStyle(DungeonUi.Center) { fontSize = 18, wordWrap = true };
            _prompt.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (_phase == Phase.Closed) return;
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = GuiDepth;

            float width = Mathf.Min(MaxWidth, DungeonUi.Width - SideGap * 2f);
            float textWidth = width - Pad * 2f;
            bool result = _phase == Phase.Result;
            bool plain = result || _narration;
            var style = plain ? _narrate : _speech;

            // 상자 높이는 줄 전체로 정한다(찍는 동안 상자가 늘지 않게).
            string full = result ? ResultText() : _text;
            _measure.text = full;
            float textHeight = Mathf.Max(MinTextHeight, style.CalcHeight(_measure, textWidth));
            var line = CurrentLine;
            int choices = _phase == Phase.Choice && line != null ? line.Choices.Length : 0;
            float height = Pad + textHeight + (choices > 0 ? 10f + choices * (ChoiceHeight + ChoiceGap) : 0f) + FootHeight + Pad * 0.5f;
            var box = new Rect((DungeonUi.Width - width) * 0.5f, DungeonUi.Height - height - BottomGap, width, height);

            // 나레이션·보상 줄은 이름표 없는 다른 모양 상자(얇은 쇠 테), 대사는 철제 틀 + 왼쪽 위 이름표.
            if (plain)
            {
                DungeonUi.Fill(box, new Color(0.02f, 0.019f, 0.018f, 0.9f));
                DungeonUi.Outline(box, DungeonUi.IronEdge, 1f);
            }
            else
            {
                DungeonUi.Box(box, 0.94f);
                if (!string.IsNullOrEmpty(_label))
                {
                    _measure.text = _label;
                    float tabWidth = Mathf.Max(64f, _name.CalcSize(_measure).x + 36f);
                    var tab = new Rect(box.x + 18f, box.y - TabHeight + 6f, tabWidth, TabHeight);
                    DungeonUi.Box(tab, 0.96f);
                    DungeonUi.ShadowLabel(new Rect(tab.x + 18f, tab.y, tab.width - 18f, tab.height), _label, _name, NameColor);
                }
            }

            var textRect = new Rect(box.x + Pad, box.y + Pad, textWidth, textHeight);
            if (result) DrawResult(textRect);
            else
            {
                string shown = _text.Substring(0, VisibleChars);
                DungeonUi.ShadowLabel(textRect, shown, style, _narration ? NarrationColor : SpeechColor);
            }

            float y = textRect.yMax + 10f;
            if (choices > 0)
            {
                for (int i = 0; i < choices; i++)
                {
                    var r = new Rect(box.x + Pad, y, textWidth, ChoiceHeight);
                    if (GUI.Button(r, "[" + (i + 1) + "] " + line.Choices[i].Label, _choice) && !_confirm) _pendingChoice = i;
                    y += ChoiceHeight + ChoiceGap;
                }
            }

            string foot = result ? FootResult : _phase == Phase.Choice ? FootChoice : FootLine;
            DungeonUi.ShadowLabel(new Rect(box.x + Pad, box.yMax - FootHeight - 6f, textWidth, FootHeight), foot, _foot, FootColor);

            if (_confirm) DrawConfirm(box);

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        string ResultText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _resultLines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_resultLines[i]);
            }
            return sb.ToString();
        }

        void DrawResult(Rect r)
        {
            float y = r.y;
            for (int i = 0; i < _resultLines.Count; i++)
            {
                _measure.text = _resultLines[i];
                float h = _narrate.CalcHeight(_measure, r.width);
                DungeonUi.ShadowLabel(new Rect(r.x, y, r.width, h), _resultLines[i], _narrate, _resultReward[i] ? RewardColor : NarrationColor);
                y += h;
            }
        }

        /// <summary>건너뛰기 확인: 대화 상자 위 작은 판. [Enter] 예 / [Esc] 아니오, 단추로도 고른다.</summary>
        void DrawConfirm(Rect box)
        {
            const float w = 520f;
            const float h = 108f;
            var r = new Rect(box.center.x - w * 0.5f, box.y - h - 40f, w, h);
            DungeonUi.Box(r, 0.97f);
            DungeonUi.ShadowLabel(new Rect(r.x + 16f, r.y + 12f, w - 32f, 30f), TownScript.SkipPrompt, _prompt, SpeechColor);
            float bw = 150f;
            if (GUI.Button(new Rect(r.center.x - bw - 8f, r.y + 56f, bw, 38f), "예")) _pendingConfirm = 1;
            if (GUI.Button(new Rect(r.center.x + 8f, r.y + 56f, bw, 38f), "아니오")) _pendingConfirm = -1;
        }
    }
}
