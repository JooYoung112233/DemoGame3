using Demo6.Core.Town;

namespace Demo6.Core.Save
{
    /// <summary>저장하는 까닭(저장·처음 화면·멈춤 창 1차 3-2 시점). WindowClosed만 기다릴 수 있고 나머지는 '꼭 써야 하는 저장'(3-3).</summary>
    public enum SaveReason
    {
        /// <summary>마을 도착 처리 직후(바구니 새 도착만, SaveOnArrival).</summary>
        Arrival,
        /// <summary>마을 창(대화·가방·스킬·모루·의뢰 목록 등)이 닫힘. 3초 묶기·같으면 안 씀.</summary>
        WindowClosed,
        /// <summary>권양기 출발 직전(trip=1).</summary>
        Departure,
        /// <summary>멈춤 창 '저장하고 처음 화면으로'.</summary>
        PauseToTitle,
        /// <summary>멈춤 창 '저장하고 끝내기'.</summary>
        PauseQuit,
        /// <summary>게임 창을 닫음(Alt+F4·창 닫기, 에디터 Play 끝).</summary>
        AppQuit,
    }

    /// <summary>저장 판정(3-6): 쓴다 / 기다린다 / 안 씀(저장하는 판 아님 · 마을 아님 · 오프닝 전 · 떠나는 중).</summary>
    public enum SaveVerdict
    {
        Write,
        Wait,
        SkipNotSaving,
        SkipNotTown,
        SkipBeforeOpening,
        SkipLeaving,
    }

    /// <summary>
    /// 저장 판정에 쓰는 지금 형편(3-1·3-3·3-4). Game(GameSave)이 채운다.
    /// SinceLastWrite는 마지막으로 쓴 뒤 흐른 실제 초다 — 아직 한 번도 쓰지 않았으면 큰 값(double.MaxValue 등)을 넣는다(기본 0이면 '방금 씀'으로 본다).
    /// </summary>
    public struct SaveGate
    {
        /// <summary>마을이다(TownRoot.Instance가 있고 DungeonRoot.Instance가 없음, 3-4).</summary>
        public bool InTown;
        /// <summary>처음 화면의 '이어하기'·'새로 시작'으로 시작한 판(3-1).</summary>
        public bool FromTitle;
        /// <summary>시험 메뉴로 시작한 판(TestLaunchSession.Active).</summary>
        public bool TestSession;
        /// <summary>이번 판에 F1 시험 패널을 썼다(TestRunFlag.DevPanelNotes > GameSession.DevPanelMark).</summary>
        public bool DevPanelUsed;
        /// <summary>꾸러미가 아직 오프닝을 보지 않음(새로 시작한 판의 오프닝 전, 3-2).</summary>
        public bool OpeningPending;
        /// <summary>권양기로 떠나는 중(밤 카드).</summary>
        public bool Leaving;
        /// <summary>마을이 밝아지는 중 등 저장을 미룰 때(3-3).</summary>
        public bool Busy;
        /// <summary>창이 열려 있다(DungeonUi.Modal).</summary>
        public bool ModalOpen;
        /// <summary>마지막으로 쓴 뒤 흐른 초.</summary>
        public double SinceLastWrite;
    }

    /// <summary>
    /// 언제 저장하나(저장·처음 화면·멈춤 창 1차 3장 — 마을에서만, 3-6 규칙 함수)와 처음 화면을 띄울지(4-1).
    /// 판정 차례: 저장하는 판이 아니면 안 씀 → 마을이 아니면 안 씀 → 오프닝 전이면 안 씀(까닭과 상관없이 — 새로 시작 확인 글 약속, 4-5)
    /// → 떠나는 중이고 출발 저장이 아니면 안 씀 → 꼭 써야 하는 저장이면 쓴다 → 창이 열림·밝아지는 중·3초 안이면 기다린다 → 쓴다.
    /// </summary>
    public static class SaveRules
    {
        /// <summary>창 닫힘 저장 사이 최소 간격(초, 3-3).</summary>
        public const double MinGapSeconds = 3.0;

        /// <summary>기다리지 않고 같아도 쓰는 '꼭 써야 하는 저장'(창 닫힘만 아님, 3-3).</summary>
        public static bool Forced(SaveReason r) => r != SaveReason.WindowClosed;

        /// <summary>저장하는 판(3-1): 처음 화면에서 시작했고, 시험 메뉴 판이 아니고, 이번 판에 F1을 쓰지 않았다.</summary>
        public static bool SavingSession(bool fromTitle, bool testSession, bool devPanelUsed) => fromTitle && !testSession && !devPanelUsed;

        /// <summary>저장 판정(3-6 차례 그대로).</summary>
        public static SaveVerdict Decide(SaveReason reason, SaveGate gate)
        {
            if (!SavingSession(gate.FromTitle, gate.TestSession, gate.DevPanelUsed)) return SaveVerdict.SkipNotSaving;
            if (!gate.InTown) return SaveVerdict.SkipNotTown;
            if (gate.OpeningPending) return SaveVerdict.SkipBeforeOpening;
            if (gate.Leaving && reason != SaveReason.Departure) return SaveVerdict.SkipLeaving;
            if (Forced(reason)) return SaveVerdict.Write;
            if (gate.ModalOpen || gate.Busy || gate.SinceLastWrite < MinGapSeconds) return SaveVerdict.Wait;
            return SaveVerdict.Write;
        }

        /// <summary>도착 처리 직후 저장(3-2): 새 플레이 도착과 이미 처리한 같은 도착은 빼고 쓴다.</summary>
        public static bool SaveOnArrival(TownArrivalKind kind, bool repeated) => kind != TownArrivalKind.NewPlay && !repeated;

        /// <summary>처음 화면을 띄울지(4-1): 처음 화면 요청이 있거나, 꾸러미가 없고 시험 판이 아닐 때.</summary>
        public static bool ShowTitle(bool titleRequested, bool hasProfile, bool testSession) => titleRequested || (!hasProfile && !testSession);
    }
}
