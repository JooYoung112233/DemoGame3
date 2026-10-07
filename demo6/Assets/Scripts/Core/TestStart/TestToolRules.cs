using System;
using System.Collections.Generic;
using Demo6.Core.Combat;

namespace Demo6.Core.TestStart
{
    /// <summary>
    /// 시험 도구 규칙(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q1·Q7, 기획/키-배치-1차.md 정정판, UnityEngine 없음). Game이 지금 상태(편집기인가·시험 값)를 넘긴다.
    /// Q1 숫자키 무기 바꾸기는 게임에서 뺐다(키 배치 1차 0장 2): 던전·전투 시험장 모두 숫자키 1~9로 무기가 바뀌지 않는다.
    /// 무기 종류 바꾸기(9종)는 F1 시험 패널의 마우스 단추로 한다(0장 3). 시험 메뉴의 시작 무기 고르기는 그대로다.
    /// Q7 F1 시험 패널: Unity 편집기에서만 연다. 만든 게임(개발용 빌드 포함)에서는 F1이 아무 일도 하지 않고, 정식 빌드 확인 창도 없다(0장 5).
    /// Q7 남는 시험 값(반박 검토): 판을 쉽게 바꾸는 시험 값(TestTuningValues)이 기본과 다르면 그 판도 시험 판이다(ChangedTestTuning).
    /// </summary>
    public static class TestToolRules
    {
        /// <summary>F1 시험 패널(마을·던전·전투 시험장)을 여는가: Unity 편집기에서만(키 배치 1차 0장 5). 만든 게임(개발용 빌드 포함)에서는 열지 않는다.</summary>
        public static bool PanelAllowed(bool isEditor) => isEditor;

        /// <summary>처형 문턱을 같은 값으로 보는 차이(시험 패널 밀대는 1% 단위).</summary>
        const float ThresholdEpsilon = 0.0005f;

        /// <summary>판을 쉽게 바꾸는 시험 값 가운데 기본과 다른 것이 있나(Q7 반박 검토). 없으면 false.</summary>
        public static bool AnyChangedTestTuning(TestTuningValues v)
        {
            var d = TestTuningValues.Defaults;
            return v.Invincible != d.Invincible
                   || v.SuperArmorRune != d.SuperArmorRune
                   || v.SuperArmorAllActs != d.SuperArmorAllActs
                   || v.SoftBoar != d.SoftBoar
                   || v.ExecutionHeal != d.ExecutionHeal
                   || Math.Abs(v.ThresholdMedium - d.ThresholdMedium) > ThresholdEpsilon
                   || Math.Abs(v.ThresholdHeavy - d.ThresholdHeavy) > ThresholdEpsilon
                   || Math.Abs(v.ThresholdElite - d.ThresholdElite) > ThresholdEpsilon;
        }

        /// <summary>
        /// 기본과 다른 시험 값의 이름(시험 패널 글과 같음, 차례 고정). 모두 기본이면 빈 목록(Q7 반박 검토).
        /// Game은 Tuning에서 값을 채워 넘기고, 하나라도 있으면 이번 판을 '시험 판(기본과 다른 시험 값)'으로 적는다(TestRunFlag).
        /// </summary>
        public static List<string> ChangedTestTuning(TestTuningValues v)
        {
            var d = TestTuningValues.Defaults;
            var names = new List<string>();
            if (v.Invincible != d.Invincible) names.Add("무적");
            if (v.SuperArmorRune != d.SuperArmorRune) names.Add("버팀 룬 시험");
            if (v.SuperArmorAllActs != d.SuperArmorAllActs) names.Add("세 행동 모두 버팀");
            if (v.SoftBoar != d.SoftBoar) names.Add("돌충이 약하게");
            if (v.ExecutionHeal != d.ExecutionHeal) names.Add("처형 회복");
            if (Math.Abs(v.ThresholdMedium - d.ThresholdMedium) > ThresholdEpsilon) names.Add("처형 문턱 보통");
            if (Math.Abs(v.ThresholdHeavy - d.ThresholdHeavy) > ThresholdEpsilon) names.Add("처형 문턱 무거움");
            if (Math.Abs(v.ThresholdElite - d.ThresholdElite) > ThresholdEpsilon) names.Add("처형 문턱 정예");
            return names;
        }
    }

    /// <summary>
    /// 판을 쉽게(정식과 다르게) 바꾸는 시험 값(4장 Q7 반박 검토). Game이 Tuning에서 채운다(TestRunFlag).
    /// Tuning은 도메인 다시 불러오기가 꺼져 있어 플레이를 다시 시작해도 남으므로, 앞 플레이의 F1·전투 시험장 패널에서 바꾼 값이 정식 판에 그대로 쓰일 수 있다.
    /// 넣지 않는 것: 걸음·웅크림 배율(판정 기록 글에 적어 견주는 값), 전투 시험장에서만 쓰는 장비 손잡이(TestLegendOn·TestCrit… — 던전은 쓰지 않음),
    /// 3차/M0a 값(던전·마을이 들어올 때 3차로 맞춤).
    /// </summary>
    public struct TestTuningValues
    {
        public bool Invincible;
        /// <summary>버팀 룬 시험(Tuning.TestSuperArmorRune).</summary>
        public bool SuperArmorRune;
        /// <summary>세 행동 모두 버팀(Tuning.SuperArmorAllActs, 가진 버팀 룬이 세 무기 모두에 듣게 함).</summary>
        public bool SuperArmorAllActs;
        /// <summary>돌충이 약하게(Tuning.SoftBoar, 코드 이름은 멧돼지 그대로).</summary>
        public bool SoftBoar;
        /// <summary>처형 회복(Tuning.ExecutionHealOn).</summary>
        public bool ExecutionHeal;
        /// <summary>무너짐 처형 문턱 보통·무거움·정예(Tuning.ExecuteThreshold…).</summary>
        public float ThresholdMedium;
        public float ThresholdHeavy;
        public float ThresholdElite;

        /// <summary>기본값(Tuning.ResetToDefaults와 같음).</summary>
        public static TestTuningValues Defaults => new TestTuningValues
        {
            ExecutionHeal = ExecutionRule.HealDefaultOn,
            ThresholdMedium = ExecutionRule.ThresholdMedium,
            ThresholdHeavy = ExecutionRule.ThresholdHeavy,
            ThresholdElite = ExecutionRule.ThresholdElite,
        };
    }

    /// <summary>'시험 판'이 된 까닭(여럿이 겹칠 수 있음, 4장 Q7). 이름 차례 = 표시 차례.</summary>
    [Flags]
    public enum TestRunReason
    {
        None = 0,
        /// <summary>시험 메뉴로 정식 새 판과 다르게 시작함(TestStartPreset.IsPlainStart가 아님: 위치·단계·레벨·장비·시험 손잡이).</summary>
        Launcher = 1,
        /// <summary>F1 시험 패널 손잡이·단추를 씀(무기 종류 단추 포함, 패널은 편집기에서만 열림 — 키 배치 1차 0장 3·5).</summary>
        DevPanel = 2,
        // 4는 옛 '숫자키 무기' 자리(키 배치 1차 0장 2에서 뺌). 남은 값의 숫자는 그대로 둔다.
        /// <summary>판을 쉽게 바꾸는 시험 값이 기본과 다름(TestTuningValues: 앞 플레이에서 남은 무적 등, 반박 검토).</summary>
        TestTuning = 8,
    }

    /// <summary>
    /// 한 판의 '시험 판' 표시(4장 Q7 '판정 기록에 시험 손잡이를 쓴 판 표시'). 까닭을 모아 두고 판정 기록이 Label로 적는다.
    /// 한 번 붙은 까닭은 Clear 전까지 지우지 않는다(Game은 플레이를 새로 시작할 때만 비움).
    /// </summary>
    public sealed class TestRunMark
    {
        /// <summary>판정 기록에 적는 이름.</summary>
        public const string Title = "시험 판";

        static readonly TestRunReason[] Order = { TestRunReason.Launcher, TestRunReason.DevPanel, TestRunReason.TestTuning };
        const TestRunReason Known = TestRunReason.Launcher | TestRunReason.DevPanel | TestRunReason.TestTuning;

        /// <summary>모은 까닭(없으면 None).</summary>
        public TestRunReason Reasons { get; private set; }

        /// <summary>시험 판인가(까닭이 하나라도 있음).</summary>
        public bool Any => Reasons != TestRunReason.None;

        /// <summary>까닭을 더한다(모르는 비트는 버림). 새로 붙은 까닭이 있으면 true.</summary>
        public bool Add(TestRunReason reason)
        {
            var before = Reasons;
            Reasons |= reason & Known;
            return Reasons != before;
        }

        /// <summary>그 까닭이 모두 붙어 있나(None은 false).</summary>
        public bool Has(TestRunReason reason) => reason != TestRunReason.None && (Reasons & reason) == reason;

        public void Clear() => Reasons = TestRunReason.None;

        /// <summary>판정 기록 표시(예: '시험 판(시험 메뉴 시작·F1 시험 패널)'). 시험 판이 아니면 빈 글.</summary>
        public string Label()
        {
            if (!Any) return "";
            var names = new List<string>();
            foreach (var r in Order)
                if ((Reasons & r) != 0) names.Add(ReasonName(r));
            return Title + "(" + string.Join("·", names) + ")";
        }

        /// <summary>까닭 하나의 이름.</summary>
        public static string ReasonName(TestRunReason reason)
        {
            switch (reason)
            {
                case TestRunReason.Launcher: return "시험 메뉴 시작";
                case TestRunReason.DevPanel: return "F1 시험 패널";
                case TestRunReason.TestTuning: return "기본과 다른 시험 값";
                default: return reason.ToString();
            }
        }
    }
}
