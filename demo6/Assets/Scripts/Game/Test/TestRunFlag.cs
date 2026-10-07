using Demo6.Core.TestStart;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 이번 플레이의 '시험 판' 표시(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q7 '판정 기록에 시험 손잡이를 쓴 판 표시').
    /// 붙는 곳: 시험 메뉴로 정식 새 판과 다르게 시작(TestLaunchSession.Begin, TestStartPreset.IsPlainStart가 아님), F1 시험 패널 손잡이·단추(무기 종류 단추 포함)를 씀
    /// (던전 ExplorationLog·마을 TownDebugPanel. F1 패널은 Unity 편집기에서만 열린다 — DevPanelGate, 기획/키-배치-1차.md 0장 5),
    /// 판을 쉽게 바꾸는 시험 값(무적·버팀 룬 시험·세 행동 모두 버팀·돌충이 약하게·처형 회복·처형 문턱)이 기본과 다름(NoteTestTuning, 반박 검토).
    /// Tuning은 플레이를 다시 시작해도 남아서(앞 플레이 F1·전투 시험장 패널에서 켠 무적 등) 이번 플레이의 손잡이 사용만 보면 놓치므로, 아래 표시를 읽을 때마다 값을 함께 본다.
    /// 읽는 곳: 판정 기록(ExplorationLog 탐험 기록·층 기록·복사 글), 보스 기록(OgreBrain), 마을 방문 기록(TownRoot). 장면을 바꿔도 남고, 플레이를 새로 시작할 때만 비운다(도메인 다시 불러오기가 꺼져 있음).
    /// </summary>
    public static class TestRunFlag
    {
        static readonly TestRunMark Mark = new TestRunMark();

        /// <summary>이번 플레이가 시험 판인가(읽을 때 기본과 다른 시험 값도 본다).</summary>
        public static bool Marked
        {
            get
            {
                NoteTestTuning();
                return Mark.Any;
            }
        }

        /// <summary>모은 까닭.</summary>
        public static TestRunReason Reasons
        {
            get
            {
                NoteTestTuning();
                return Mark.Reasons;
            }
        }

        /// <summary>판정 기록 표시(예: '시험 판(F1 시험 패널)'). 시험 판이 아니면 빈 글.</summary>
        public static string Label
        {
            get
            {
                NoteTestTuning();
                return Mark.Label();
            }
        }

        /// <summary>
        /// F1 시험 패널 사용 횟수(Note에 DevPanel이 들어올 때마다 +1, 새로 붙었는지와 상관없이). 저장이 '이번 판에 F1을 썼나'를 가르는 데만 쓴다
        /// (GameSession.DevPanelMark와 견줌, 저장·처음 화면·멈춤 창 1차 3-1·8-5). 판정 기록 표시(Marked·Reasons·Label)와는 따로다.
        /// </summary>
        public static int DevPanelNotes { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Mark.Clear();
            DevPanelNotes = 0;
        }

        /// <summary>까닭을 더한다. 새로 붙었을 때만 콘솔에 한 줄 남긴다.</summary>
        public static void Note(TestRunReason reason)
        {
            if ((reason & TestRunReason.DevPanel) != 0) DevPanelNotes++;
            if (Mark.Add(reason)) Debug.Log($"[시험 판] {TestRunMark.ReasonName(reason)} — 이번 판을 {TestRunMark.Title}으로 기록한다");
        }

        /// <summary>
        /// 판을 쉽게 바꾸는 시험 값이 지금 기본과 다르면 까닭 '기본과 다른 시험 값'을 더한다(4장 Q7 반박 검토, TestToolRules.ChangedTestTuning).
        /// 한 번 붙으면 이번 플레이 동안 지우지 않는다(값을 기본으로 되돌려도 그때까지 쉬운 값으로 돌았으므로). 플레이 중이 아니면 보지 않는다.
        /// </summary>
        public static void NoteTestTuning()
        {
            if (!Application.isPlaying || Mark.Has(TestRunReason.TestTuning)) return;
            var values = CurrentTestTuning();
            if (!TestToolRules.AnyChangedTestTuning(values)) return;
            if (Mark.Add(TestRunReason.TestTuning))
                Debug.Log($"[시험 판] {TestRunMark.ReasonName(TestRunReason.TestTuning)}({string.Join("·", TestToolRules.ChangedTestTuning(values))}) — 이번 판을 {TestRunMark.Title}으로 기록한다");
        }

        /// <summary>지금 Tuning의 판을 쉽게 바꾸는 시험 값.</summary>
        static TestTuningValues CurrentTestTuning() => new TestTuningValues
        {
            Invincible = Tuning.Invincible,
            SuperArmorRune = Tuning.TestSuperArmorRune,
            SuperArmorAllActs = Tuning.SuperArmorAllActs,
            SoftBoar = Tuning.SoftBoar,
            ExecutionHeal = Tuning.ExecutionHealOn,
            ThresholdMedium = Tuning.ExecuteThresholdMedium,
            ThresholdHeavy = Tuning.ExecuteThresholdHeavy,
            ThresholdElite = Tuning.ExecuteThresholdElite,
        };
    }
}
