namespace Demo6.Core.Save
{
    /// <summary>
    /// 처음 화면·멈춤 창 화면 글 한 곳(기획/저장-처음화면-멈춤창-1차.md 4-3·4-7·4-8·5-3·5-4, UnityEngine 없음). 글은 모두 초안이다.
    /// 시각 글(예: '10월 7일 21:30')은 부르는 쪽(Game)이 SaveText.When으로 만들어 넘긴다 — 이 파일은 다른 Core 파일을 부르지 않는다.
    /// 저장은 마을에서만 한다(사용자 결정 2026-10-07). 그래서 던전 멈춤 창은 '갱도에서는 저장하지 않는다'를 보이고, 떠나기 전에 '이번 원정은 남지 않는다'를 묻는다.
    /// </summary>
    public static class ShellText
    {
        // ── 처음 화면 (4-3·4-8) ──

        /// <summary>처음 화면 제목(바탕체 큰 글자). 사용자가 제목 그림(Resources/UI/Title/title_logo)을 넣으면 그림으로 바뀐다(10장).</summary>
        public const string GameTitle = "버려진 갱도";

        public const string Continue = "이어하기";
        public const string NewGame = "새로 시작";
        public const string Settings = "설정";
        public const string TestMenu = "시험 메뉴 (개발용)";
        public const string Quit = "끝내기";
        /// <summary>하위 화면·확인에서 앞 화면으로(처음 화면·멈춤 창 같이 씀).</summary>
        public const string Back = "돌아가기";

        /// <summary>처음 화면 단추 글.</summary>
        public static string TitleItemLabel(TitleItem item)
        {
            switch (item)
            {
                case TitleItem.Continue: return Continue;
                case TitleItem.NewGame: return NewGame;
                case TitleItem.Settings: return Settings;
                case TitleItem.TestMenu: return TestMenu;
                default: return Quit;
            }
        }

        // ── 새로 시작 확인 (4-5·4-8) ──

        public const string NewGameTitle = "새로 시작할까?";
        /// <summary>확인 창의 '예' 단추 글.</summary>
        public const string NewGameConfirm = "새로 시작";

        /// <summary>읽을 수 있는 저장이 있을 때 본문. 예전 저장은 -old- 복사본으로 남는다(4-5) — '.bak이 남는다'는 약속은 하지 않는다.</summary>
        public static string NewGameBody(int expedition, int level)
            => $"지금 저장(원정 {expedition}번째 준비 · 레벨 {level})은 오프닝 대화가 끝날 때 덮어쓴다. 예전 저장은 저장 폴더에 따로 복사해 남겨 둔다.";

        /// <summary>요약을 만들 꾸러미가 없을 때 본문.</summary>
        public const string NewGameBodyPlain = "지금 저장은 오프닝 대화가 끝날 때 덮어쓴다. 예전 저장은 저장 폴더에 따로 복사해 남겨 둔다.";

        /// <summary>읽지 못한 저장·더 새 판 저장일 때 본문(4-8).</summary>
        public const string NewGameBodyUnreadable = "읽지 못한 저장 파일은 따로 복사해 남겨 둔다.";

        // ── 저장을 읽지 못할 때 (4-7) ──

        public const string SaveBroken = "저장 파일을 읽지 못했다 — 새로 시작하면 원래 파일은 따로 남겨 둔다.";
        public const string SaveNewer = "더 새 판에서 만든 저장이라 읽지 않는다 — 새로 시작하면 원래 파일은 따로 남겨 둔다.";
        /// <summary>이어하기를 눌렀는데 그사이 저장을 읽지 못했을 때(GameFlow.Continue 실패).</summary>
        public const string ContinueFailed = "저장을 읽지 못했다.";

        /// <summary>에디터·개발용 빌드에서 처음 화면 맨 아래 흐린 줄(4-3, 시험하는 사람이 파일을 찾게).</summary>
        public static string SaveFolderLine(string path) => "저장 폴더: " + path;

        // ── 멈춤 창 (5-3) ──

        public const string PauseTitle = "멈춤";
        public const string Resume = "계속";
        public const string Notices = "지난 알림";
        public const string SaveAndTitle = "저장하고 처음 화면으로";
        public const string SaveAndQuit = "저장하고 끝내기";
        public const string ToTitle = "처음 화면으로";

        /// <summary>멈춤 창 단추 글.</summary>
        public static string PauseItemLabel(PauseItem item)
        {
            switch (item)
            {
                case PauseItem.Resume: return Resume;
                case PauseItem.Settings: return Settings;
                case PauseItem.Notices: return Notices;
                case PauseItem.SaveAndTitle: return SaveAndTitle;
                case PauseItem.SaveAndQuit: return SaveAndQuit;
                case PauseItem.ToTitle: return ToTitle;
                default: return Quit;
            }
        }

        // ── 멈춤 창 아래 흐린 줄 (5-3) ──

        /// <summary>마을·저장하는 판의 오프닝 전(새로 시작 뒤 아직 한 번도 쓰지 않음). '저장하고 …'는 저장 없이 그대로 진행한다(3-2 '오프닝 전').</summary>
        public const string NotSavedYet = "아직 저장하지 않았다 — 오프닝 대화가 끝나면 처음 저장한다.";
        /// <summary>저장하지 않는 판(시험 메뉴·바로 Play·F1을 쓴 판, 3-1).</summary>
        public const string NotSavingSession = "이 판은 저장하지 않는다(시험 판).";

        public static string LastSavedLine(string when) => "마지막 저장: " + when;

        public static string SaveErrorLine(string reason) => "저장 실패 — " + Reason(reason) + ". 다음 저장 때 다시 해 본다.";

        /// <summary>던전·저장하는 판: 출발 직전 저장이면 '마을 출발 직전 (시각)', 마을 저장이면 '마을 (시각)', 저장이 없으면 그렇게 적는다.</summary>
        public static string DungeonLine(string lastSavedWhen, bool lastSaveWasTrip)
        {
            const string head = "갱도에서는 저장하지 않는다 — ";
            if (string.IsNullOrEmpty(lastSavedWhen)) return head + "아직 저장이 없다.";
            return head + (lastSaveWasTrip ? "마지막 저장: 마을 출발 직전 (" : "마지막 저장: 마을 (") + lastSavedWhen + ")";
        }

        /// <summary>
        /// 멈춤 창 아래 흐린 줄(5-3). 저장하지 않는 판은 어디서나 '이 판은 저장하지 않는다(시험 판).'
        /// 마을·저장하는 판: 실패가 남아 있으면 실패 줄, 아직 쓴 적이 없으면(오프닝 전) '아직 저장하지 않았다 …', 아니면 '마지막 저장: (시각)'.
        /// 던전·저장하는 판: '갱도에서는 저장하지 않는다 — 마지막 저장: …'. 전투 시험장은 저장하지 않는 곳이라 저장하지 않는 판 글. 처음 화면·곳 없음은 null.
        /// lastSavedWhen은 SaveText.When으로 만든 글(없으면 null), lastError는 GameSave.LastError(없으면 null).
        /// </summary>
        public static string PauseFooter(ShellPlace place, bool savingSession, string lastSavedWhen, bool lastSaveWasTrip, string lastError)
        {
            switch (place)
            {
                case ShellPlace.Town:
                    if (!savingSession) return NotSavingSession;
                    if (!string.IsNullOrEmpty(lastError)) return SaveErrorLine(lastError);
                    return string.IsNullOrEmpty(lastSavedWhen) ? NotSavedYet : LastSavedLine(lastSavedWhen);
                case ShellPlace.Dungeon:
                    return savingSession ? DungeonLine(lastSavedWhen, lastSaveWasTrip) : NotSavingSession;
                case ShellPlace.CombatTest:
                    return NotSavingSession;
                default:
                    return null;
            }
        }

        // ── 떠나기 확인 (5-4) ──

        public const string TripLostTitle = "이번 원정은 남지 않는다";
        const string TripLostTail = "지금 끝내면 이번 원정에서 얻은 장비·재화·경험치·의뢰 진행이 모두 사라진다.";
        public const string NoSaveBody = "저장이 없다 — 지금 끝내면 처음부터 다시 해야 한다.";
        public const string NotSavingTitle = "이 판은 저장하지 않는다";
        public const string NotSavingBody = "시험으로 시작했거나 F1 시험 손잡이를 쓴 판이다.";

        /// <summary>던전·저장하는 판의 확인 본문(5-4). 마지막 저장이 출발 직전이 아니면(출발 저장 실패 등) 첫 줄을 '마지막 저장: 마을 (시각)'으로, 저장이 없으면 그렇게 적는다.</summary>
        public static string TripLostBody(string lastSavedWhen, bool lastSaveWasTrip)
        {
            if (string.IsNullOrEmpty(lastSavedWhen)) return NoSaveBody;
            if (lastSaveWasTrip) return "마지막 저장은 마을에서 권양기를 떠나기 직전(" + lastSavedWhen + ")이다. " + TripLostTail;
            return "마지막 저장: 마을 (" + lastSavedWhen + ")\n" + TripLostTail;
        }

        /// <summary>떠나기 확인 제목(ShellRules.ConfirmBeforeLeaving이 참일 때). 저장하지 않는 판이면 '이 판은 저장하지 않는다', 던전·저장하는 판이면 '이번 원정은 남지 않는다'. 그 밖은 null.</summary>
        public static string LeaveConfirmTitle(ShellPlace place, bool savingSession)
        {
            if (!savingSession) return NotSavingTitle;
            return place == ShellPlace.Dungeon ? TripLostTitle : null;
        }

        /// <summary>떠나기 확인 본문(LeaveConfirmTitle과 짝).</summary>
        public static string LeaveConfirmBody(ShellPlace place, bool savingSession, string lastSavedWhen, bool lastSaveWasTrip)
        {
            if (!savingSession) return NotSavingBody;
            return place == ShellPlace.Dungeon ? TripLostBody(lastSavedWhen, lastSaveWasTrip) : null;
        }

        /// <summary>떠나기 확인의 '예' 단추 글: '처음 화면으로' 또는 '끝내기'.</summary>
        public static string LeaveConfirmButton(bool toTitle) => toTitle ? ToTitle : Quit;

        // ── '저장하고 …'에서 저장 실패 (2-6·5-4) ──

        public const string SaveFailedTitle = "저장하지 못했다";
        public const string QuitAnyway = "그래도 끝내기";
        public const string TitleAnyway = "그래도 처음 화면으로";

        /// <summary>저장 실패 확인 본문: '{까닭}. 그래도 끝낼까?'(처음 화면으로 가는 길이면 '그래도 처음 화면으로 갈까?').</summary>
        public static string SaveFailedBody(string reason, bool toTitle)
            => Reason(reason) + (toTitle ? ". 그래도 처음 화면으로 갈까?" : ". 그래도 끝낼까?");

        /// <summary>저장 실패 확인의 '예' 단추 글.</summary>
        public static string SaveFailedButton(bool toTitle) => toTitle ? TitleAnyway : QuitAnyway;

        /// <summary>까닭 글을 문장 가운데에 넣게 다듬는다(끝 마침표·공백을 뗌, 비었으면 '까닭을 알 수 없다').</summary>
        static string Reason(string reason)
        {
            string r = reason == null ? "" : reason.Trim().TrimEnd('.', ' ');
            return r.Length > 0 ? r : "까닭을 알 수 없다";
        }
    }
}
