using System;
using System.Collections.Generic;

namespace Demo6.Core.Save
{
    /// <summary>지금 화면이 어디인가(저장·처음 화면·멈춤 창 1차 5-1). Game의 GameShell이 장면 루트를 보고 채운다.</summary>
    public enum ShellPlace { None, Title, Town, Dungeon, CombatTest }

    /// <summary>Esc 한 번에 할 일(5-1 표). None이면 GameShell은 아무것도 하지 않고, 열린 창이 있으면 그 창이 스스로 Esc를 본다.</summary>
    public enum EscAction { None, OpenPause, PauseBack, ClosePause, TitleBack }

    /// <summary>
    /// Esc를 판단할 때의 상태(5-1). Game이 매 프레임 맨 처음(GameShell, 실행 차례 −1000)에 채운다 — 이때는 아직 어느 창도 이번 Esc를 보지 않았다.
    /// OtherModalOpen = 멈춤 창이 아닌 DungeonUi 창이 열려 있음. InputBlocked = 창 없이 입력이 막힘(떠나는 암전·밤 카드 대기).
    /// SceneBusy = 창 없이 시간이 멈춤(전투 시험장 '멈춤' 단추 등)·마을이 밝아지는 중·던전 층에 들어서기 전.
    /// </summary>
    public struct EscState
    {
        public ShellPlace Place;
        public bool Loading, TitleSubOpen, PauseOpen, PauseSubOpen, OtherModalOpen, InputBlocked, SceneBusy;
    }

    /// <summary>멈춤 창 첫 화면 단추(5-3 표).</summary>
    public enum PauseItem { Resume, Settings, Notices, SaveAndTitle, SaveAndQuit, ToTitle, Quit }

    /// <summary>처음 화면 단추(4-3 표).</summary>
    public enum TitleItem { Continue, NewGame, Settings, TestMenu, Quit }

    /// <summary>
    /// 처음 화면·멈춤 창·Esc 규칙(기획/저장-처음화면-멈춤창-1차.md 4-3·5-1·5-3·5-4, UnityEngine 없음).
    /// Esc는 한 곳(GameShell)이 받아 이 규칙 하나로 정한다. 창을 닫은 그 프레임에는 멈춤 창이 열리지 않는다(차례 5에서 '다른 창이 열려 있다'로 물러남).
    /// 멈춤 창 항목: 마을·저장하는 판만 '저장하고 …', 그 밖(마을 저장 안 함·던전·전투 시험장)은 '처음 화면으로'·'끝내기'.
    /// 저장은 마을에서만 한다(사용자 결정 2026-10-07) — 던전에서 떠나면 이번 원정이 남지 않으므로 묻는다(5-4).
    /// </summary>
    public static class ShellRules
    {
        /// <summary>멈춤 창의 DungeonUi 창 이름(5-3). 창은 모두 DungeonUi.TryOpen으로 연다(5-1 '앞으로 지킬 것').</summary>
        public const string PauseModal = "pause";

        static readonly IReadOnlyList<PauseItem> TownSavingItems = Array.AsReadOnly(new[]
        {
            PauseItem.Resume, PauseItem.Settings, PauseItem.Notices, PauseItem.SaveAndTitle, PauseItem.SaveAndQuit,
        });

        static readonly IReadOnlyList<PauseItem> LeaveItems = Array.AsReadOnly(new[]
        {
            PauseItem.Resume, PauseItem.Settings, PauseItem.Notices, PauseItem.ToTitle, PauseItem.Quit,
        });

        static readonly IReadOnlyList<PauseItem> NoPauseItems = Array.AsReadOnly(new PauseItem[0]);

        /// <summary>처음 화면 단추 묶음 넷(이어하기 있음 1 | 시험 메뉴 있음 2). 늘 같은 목록이라 처음 한 번 만든다.</summary>
        static readonly IReadOnlyList<TitleItem>[] TitleSets = BuildTitleSets();

        /// <summary>
        /// Esc 한 번에 할 일(5-1 표 차례): ① 불러오는 중 → 없음 ② 처음 화면 → 설정·확인이 열려 있으면 그것만 닫음, 아니면 없음
        /// ③ 멈춤 창 안의 설정·지난 알림·확인 → 첫 화면으로 ④ 멈춤 창 첫 화면 → 닫기 ⑤ 다른 창 → 없음(그 창이 스스로 닫는다)
        /// ⑥ 마을·던전·전투 시험장이 아님 → 없음 ⑦ 입력 막힘·창 없이 멈춤·밝아지는 중·층에 들어서기 전 → 없음 ⑧ 그 밖 → 멈춤 창 열기.
        /// </summary>
        /// <summary>
        /// J 의뢰 창을 열 수 있나(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 가-3): 마을·던전에서만, 불러오는 중·멈춤 창·다른 창·입력 막힘·바쁨(암전 등)이 아닐 때.
        /// 처음 화면·전투 시험장에서는 열지 않는다. 닫기는 열린 창이 스스로 J·Esc를 본다.
        /// </summary>
        public static bool CanOpenQuests(EscState s) =>
            (s.Place == ShellPlace.Town || s.Place == ShellPlace.Dungeon) &&
            !s.Loading && !s.PauseOpen && !s.OtherModalOpen && !s.InputBlocked && !s.SceneBusy;

        public static EscAction OnEscape(EscState s)
        {
            if (s.Loading) return EscAction.None;
            if (s.Place == ShellPlace.Title) return s.TitleSubOpen ? EscAction.TitleBack : EscAction.None;
            if (s.PauseOpen) return s.PauseSubOpen ? EscAction.PauseBack : EscAction.ClosePause;
            if (s.OtherModalOpen) return EscAction.None;
            if (s.Place == ShellPlace.None) return EscAction.None;
            if (s.InputBlocked || s.SceneBusy) return EscAction.None;
            return EscAction.OpenPause;
        }

        /// <summary>
        /// 멈춤 창 첫 화면 단추(5-3 표, 차례 고정). 마을·저장하는 판 = 계속·설정·지난 알림·저장하고 처음 화면으로·저장하고 끝내기.
        /// 마을·저장하지 않는 판, 던전, 전투 시험장 = 계속·설정·지난 알림·처음 화면으로·끝내기. 처음 화면·곳 없음 = 빈 목록.
        /// </summary>
        public static IReadOnlyList<PauseItem> PauseItems(ShellPlace place, bool savingSession)
        {
            switch (place)
            {
                case ShellPlace.Town: return savingSession ? TownSavingItems : LeaveItems;
                case ShellPlace.Dungeon:
                case ShellPlace.CombatTest: return LeaveItems;
                default: return NoPauseItems;
            }
        }

        /// <summary>
        /// '처음 화면으로'·'끝내기' 전에 한 번 묻는가(5-4): 던전은 늘(저장하는 판이면 '이번 원정은 남지 않는다', 아니면 '이 판은 저장하지 않는다'),
        /// 마을은 저장하지 않는 판일 때만. 마을·저장하는 판은 '저장하고 …'라 묻지 않고, 전투 시험장은 묻지 않는다.
        /// </summary>
        public static bool ConfirmBeforeLeaving(ShellPlace place, bool savingSession)
            => place == ShellPlace.Dungeon || (place == ShellPlace.Town && !savingSession);

        /// <summary>처음 화면 단추(4-3, 차례 고정): [이어하기(읽을 수 있는 저장이 있을 때)], 새로 시작, 설정, [시험 메뉴(에디터·개발용 빌드)], 끝내기.</summary>
        public static IReadOnlyList<TitleItem> TitleItems(bool canContinue, bool showTestMenu)
            => TitleSets[(canContinue ? 1 : 0) | (showTestMenu ? 2 : 0)];

        /// <summary>'새로 시작' 전에 묻는가(4-3·4-5): 저장 파일이 하나라도 있으면(읽지 못한 것 포함) 묻는다.</summary>
        public static bool AskBeforeNewGame(bool anySaveFile) => anySaveFile;

        /// <summary>처음 화면 '시험 메뉴' 단추를 보이는가(4-3·8-4): Unity 에디터이거나 개발용 빌드일 때만. 정식 빌드에서는 시험 메뉴로 갈 길이 없다.</summary>
        public static bool ShowTestMenu(bool isEditor, bool isDevBuild) => isEditor || isDevBuild;

        static IReadOnlyList<TitleItem>[] BuildTitleSets()
        {
            var sets = new IReadOnlyList<TitleItem>[4];
            for (int i = 0; i < sets.Length; i++)
            {
                var list = new List<TitleItem>(5);
                if ((i & 1) != 0) list.Add(TitleItem.Continue);
                list.Add(TitleItem.NewGame);
                list.Add(TitleItem.Settings);
                if ((i & 2) != 0) list.Add(TitleItem.TestMenu);
                list.Add(TitleItem.Quit);
                sets[i] = list.AsReadOnly();
            }
            return sets;
        }
    }
}
