using Demo6.Core.Save;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 이번 플레이의 판 상태(기획/저장-처음화면-멈춤창-1차.md 3-1·4-1·4-2). 장면을 바꿔도 남고, 플레이를 새로 시작할 때(SubsystemRegistration)만 비운다.
    /// '저장하는 판'은 처음 화면의 '이어하기'·'새로 시작'으로 시작한 판(FromTitle)이다. 시험 메뉴·바로 Play 판은 FromTitle이 거짓이라 저장하지 않는다.
    /// 처음 화면 붙잡기: TownRoot.Awake가 SceneStatics.Reset 바로 뒤에 HoldTownForTitle을 묻고, 참이면 마을을 짓지 않고 꺼진다.
    /// 처음 화면 그리기·장면 불러오기는 GameShell·TitleScreen·GameFlow(작성자 C)가 한다. 이 파일은 상태만 들고 있다.
    /// </summary>
    public static class GameSession
    {
        /// <summary>처음 화면 '이어하기'·'새로 시작'으로 시작한 판(저장하는 판의 첫 조건, 3-1).</summary>
        public static bool FromTitle { get; private set; }
        /// <summary>'처음 화면으로'를 골랐다(다음 Town Awake가 처음 화면으로 붙잡는다, 4-1). HoldTownForTitle이 읽고 비운다.</summary>
        public static bool TitleRequested { get; private set; }
        /// <summary>지금 처음 화면이 떠 있다(마을은 짓지 않고 꺼져 있음, 4-2).</summary>
        public static bool TitleShowing { get; private set; }
        /// <summary>놀이 시간(초). 이어하기면 저장 머리 값부터, 새로 시작이면 0부터. 멈춤 창이 닫혀 있는 동안 실제 시간으로 센다(2-2 playtime).</summary>
        public static double PlaySeconds { get; private set; }
        /// <summary>이어하기로 읽은 저장 머리(새로 시작이면 null).</summary>
        public static SaveHeader Loaded { get; private set; }
        /// <summary>
        /// 이번 판을 시작할 때의 TestRunFlag.DevPanelNotes(F1 사용 횟수). 'F1을 썼다' = DevPanelNotes > DevPanelMark(3-1 '이번 판').
        /// TestRunFlag의 DevPanel 까닭은 플레이 전체 표시라, 그것만 보면 앞 시험 판의 F1 사용이 새 판의 저장까지 막는다.
        /// </summary>
        public static int DevPanelMark { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            FromTitle = false;
            TitleRequested = false;
            TitleShowing = false;
            PlaySeconds = 0;
            Loaded = null;
            DevPanelMark = 0;
        }

        /// <summary>'처음 화면으로'(멈춤 창·시험 메뉴): 다음에 Town을 불러오면 처음 화면이 뜬다(5-5).</summary>
        public static void RequestTitle() => TitleRequested = true;

        /// <summary>
        /// TownRoot.Awake가 묻는다(4-2): 처음 화면을 띄울 때면 참(마을을 짓지 않는다).
        /// 규칙은 SaveRules.ShowTitle — 처음 화면 요청 || (꾸러미 없음 && 시험 판 아님)(4-1). 게임을 켜서 첫 장면 Town이 열리면 꾸러미가 없어 뜬다.
        /// 요청은 여기서 비운다(이어하기·새로 시작 뒤 Town을 다시 불러오면 꾸러미가 있어 뜨지 않는다).
        /// </summary>
        public static bool HoldTownForTitle()
        {
            bool show = SaveRules.ShowTitle(TitleRequested, ProfileCarry.Data != null, TestLaunchSession.Active);
            TitleRequested = false;
            TitleShowing = show;
            return show;
        }

        /// <summary>
        /// 저장하는 판을 시작한다(이어하기·새로 시작, 4-4의 4·4-5의 3). 놀이 시간은 읽은 머리 값부터(새로 시작이면 0).
        /// F1 사용 기준값을 지금 값으로 적어 앞 시험 판의 F1 사용이 이 판의 저장을 막지 않게 한다(3-1).
        /// F1 '오우거 굴 있음 가정'은 장면을 바꿔도 남으므로 끈다 — 안 끄면 앞 시험 판의 가정이 새 판의 의뢰 계산(QuestCtx)에 남아 저장될 수 있다(2-7).
        /// </summary>
        public static void BeginFromTitle(SaveHeader loaded)
        {
            FromTitle = true;
            TitleShowing = false;
            Loaded = loaded;
            PlaySeconds = loaded != null ? loaded.PlaySeconds : 0;
            DevPanelMark = TestRunFlag.DevPanelNotes;
            TownRoot.OgreDenAssumed = false;
        }

        /// <summary>저장하는 판을 끝낸다(처음 화면으로·끝내기·시험 메뉴로, 5-5·8-1). 끝낸 뒤의 저장은 '저장하는 판 아님'으로 빠진다.</summary>
        public static void EndSession()
        {
            FromTitle = false;
            Loaded = null;
            TitleShowing = false;
        }

        /// <summary>
        /// 놀이 시간 세기(GameShell이 매 프레임 부름). 저장하는 판에서, 처음 화면이 아니고 멈춤 창이 닫혀 있을 때만 센다(5-3).
        /// 한 프레임에 0.25초보다 많이 세지 않는다(멈췄던 창·장면 불러오기 끊김을 놀이 시간에 넣지 않게).
        /// </summary>
        public static void TickPlayTime(float unscaledDeltaTime, bool paused)
        {
            if (!FromTitle || TitleShowing || paused) return;
            if (unscaledDeltaTime <= 0f) return;
            PlaySeconds += Mathf.Min(unscaledDeltaTime, 0.25f);
        }
    }
}
