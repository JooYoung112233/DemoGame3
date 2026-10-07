using Demo6.Core.Save;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 장면 오가기 한 곳(기획/저장-처음화면-멈춤창-1차.md 4-4·4-5·5-5·8-1, 11-7 'C'). 처음 화면·멈춤 창·시험 메뉴·F1 패널이 부른다.
    /// 이어하기·새로 시작: 시험 메뉴 판을 끝내고 지난 알림을 비운 뒤 꾸러미를 깔고(GameSave) Town을 다시 불러온다 — 마을은 원래 흐름 그대로 지어진다.
    /// 처음 화면으로: (마을·저장하는 판이면 저장) → 멈춤 창 닫기 → 판 끝 → 시험 메뉴 판 끝 → 꾸러미·도착 쪽지 비움 → 처음 화면 요청 → Town 다시 불러오기
    /// (TownRoot.Awake의 GameSession.HoldTownForTitle이 붙잡고 처음 화면이 뜬다).
    /// 끝내기: (마을·저장하는 판이면 저장) → 판 끝 → Application.Quit(에디터는 Play 멈춤). 판을 먼저 끝내므로 곧이어 오는 창 닫기 저장(AppQuit)은
    /// '저장하는 판 아님'으로 빠진다 — 안 그러면 방금 쓴 본 파일이 .bak으로 밀려 백업이 본과 같은 글이 된다(5-5, 16장 5).
    /// 저장은 마을에서만 한다(사용자 결정 2026-10-07): 던전에서 떠나면 저장하지 않고 이번 원정은 남지 않는다(D2 가).
    /// '저장하고 …'에서 저장 결과가 '실패'일 때만 false를 돌려준다(아무것도 바꾸지 않음). '안 씀'(오프닝 전·저장하지 않는 판)은 그대로 진행한다.
    /// </summary>
    public static class GameFlow
    {
        /// <summary>GameFlow가 장면을 불러오기 시작했고 아직 다 불러오지 않았다(이 동안 Esc는 아무 일도 하지 않음, 5-1 차례 1).</summary>
        public static bool Loading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Loading = false;

        /// <summary>새 장면을 다 불러왔다(GameShell이 SceneManager.sceneLoaded에서 부름).</summary>
        internal static void SceneLoaded() => Loading = false;

        /// <summary>
        /// 처음 화면 '이어하기'(4-4): 시험 메뉴 판 끝 → 지난 알림 비움 → 저장을 읽어 꾸러미를 깔고 저장하는 판을 시작(GameSave.InstallForContinue) → Town 다시 불러오기.
        /// 그사이 저장을 읽지 못하면 처음 화면에 '저장을 읽지 못했다.'를 보이고 다시 살펴보게 한다.
        /// </summary>
        public static void Continue()
        {
            if (Loading) return;
            TestLaunchSession.End();
            NoticeLog.Clear();
            if (!GameSave.InstallForContinue())
            {
                Debug.LogWarning("[처음 화면] 이어하기: 저장을 읽지 못했다");
                TitleScreen.NoteContinueFailed();
                return;
            }
            Load(SceneTravel.TownPath);
        }

        /// <summary>처음 화면 '새로 시작'(4-5): 시험 메뉴 판 끝 → 지난 알림 비움 → 원래 파일을 복사해 남기고 새 프로필로 저장하는 판을 시작(GameSave.InstallNewGame) → Town 다시 불러오기.</summary>
        public static void NewGame()
        {
            if (Loading) return;
            TestLaunchSession.End();
            NoticeLog.Clear();
            GameSave.InstallNewGame();
            Load(SceneTravel.TownPath);
        }

        /// <summary>
        /// 처음 화면으로(5-5, 시험 메뉴 '처음 화면으로'도 saveFirst 거짓으로 부름). saveFirst이고 마을·저장하는 판이면 먼저 저장한다 — 실패면 false(아무것도 바꾸지 않음).
        /// 던전에서는 저장하지 않는다(이번 원정은 남지 않음). 이미 장면을 바꾸는 중이면 한 번 더 하지 않고 true.
        /// </summary>
        public static bool LeaveToTitle(bool saveFirst)
        {
            if (Loading) return true;
            if (saveFirst && !SaveBeforeLeaving(SaveReason.PauseToTitle)) return false;
            PauseMenu.Close();
            GameSession.EndSession();
            TestLaunchSession.End();
            ProfileCarry.Clear();
            TownTravel.Clear();
            GameSession.RequestTitle();
            Load(SceneTravel.TownPath);
            return true;
        }

        /// <summary>
        /// 끝내기(5-5). saveFirst이고 마을·저장하는 판이면 먼저 저장한다 — 실패면 false(아무것도 바꾸지 않음).
        /// 그 뒤 판을 끝내고(GameSession.EndSession — 곧 올 TownAutoSave.OnApplicationQuit 저장이 '저장하는 판 아님'으로 빠지게) 게임을 끈다. 에디터는 Play를 멈춘다.
        /// </summary>
        public static bool Quit(bool saveFirst)
        {
            if (saveFirst && !SaveBeforeLeaving(SaveReason.PauseQuit)) return false;
            GameSession.EndSession();
            Debug.Log("[처음 화면] 끝내기");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            return true;
        }

        /// <summary>
        /// F1 패널마다 '시험 메뉴로 (저장하지 않음)'·처음 화면 '시험 메뉴'(8-1): 멈춤 창·열린 창 닫기 → 판 끝 → 시험 메뉴 판 끝 → 꾸러미·도착 쪽지 비움 → 시험 메뉴 장면.
        /// 저장하지 않는다. 시험 메뉴 장면은 장면 정적 비우기를 부르지 않으므로 열린 창(시간 멈춤·입력 막힘)을 여기서 닫는다.
        /// </summary>
        public static void GoToTestLauncher()
        {
            if (Loading) return;
            PauseMenu.Close();
            if (DungeonUi.Modal != null) DungeonUi.Close(DungeonUi.Modal);
            GameSession.EndSession();
            TestLaunchSession.End();
            ProfileCarry.Clear();
            TownTravel.Clear();
            Load(TestLaunchSession.LauncherPath);
        }

        /// <summary>
        /// '저장하고 …'의 저장(5-5): 마을이고 저장하는 판일 때만 쓴다. Failed만 실패다 — Skipped(오프닝 전 등)·Unchanged·Written은 그대로 진행.
        /// 마을이 아니거나 저장하지 않는 판이면 쓰지 않고 진행.
        /// </summary>
        static bool SaveBeforeLeaving(SaveReason reason)
        {
            if (GameShell.Place != ShellPlace.Town || !GameSave.SavingSession) return true;
            return GameSave.SaveTown(reason) != SaveOutcome.Failed;
        }

        static void Load(string path)
        {
            Loading = true;
            if (SceneTravel.Load(path)) return;
            Loading = false;
            Debug.LogWarning($"[처음 화면] {path}를 불러오지 못했다");
        }
    }
}
