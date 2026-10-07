using System;
using System.Collections.Generic;
using System.IO;
using Demo6.Core.Dungeon;
using Demo6.Core.Save;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>처음 화면이 본 저장 상태(기획/저장-처음화면-멈춤창-1차.md 4-3·4-7).</summary>
    public enum SaveProbeState
    {
        /// <summary>저장 파일이 없다(확인 없이 새로 시작).</summary>
        None,
        /// <summary>본 파일을 읽었다.</summary>
        Ready,
        /// <summary>본 파일이 상해 백업(.bak)을 읽었다(' (바로 앞 저장)').</summary>
        FromBackup,
        /// <summary>파일은 있으나 둘 다 읽지 못했다(이어하기 없음).</summary>
        Broken,
        /// <summary>더 새 판에서 만든 저장이라 읽지 않는다(이어하기 없음).</summary>
        Newer,
    }

    /// <summary>처음 화면이 한 번 본 저장(꾸러미를 깔지 않은 채, 4-3).</summary>
    public sealed class SaveProbe
    {
        public SaveProbeState State;
        public SaveHeader Header;
        public CarryData Carry;
        /// <summary>이어하기 아래 작은 줄('원정 3번째 준비 · 레벨 4 · 놀이 1시간 2분 · 10월 7일 21:30 저장', 4-3). 읽지 못했으면 null.</summary>
        public string ContinueLine;
        /// <summary>본 파일이나 백업이 하나라도 있다(새로 시작 전에 묻기, 4-5).</summary>
        public bool AnyFile;
    }

    /// <summary>SaveTown 결과. Skipped(저장하는 판 아님·마을 아님·오프닝 전·떠나는 중)는 실패가 아니다(5-5).</summary>
    public enum SaveOutcome
    {
        Written,
        /// <summary>창 닫힘 저장인데 꾸러미 글이 마지막으로 쓴 글과 같아 쓰지 않았다(3-3).</summary>
        Unchanged,
        /// <summary>창 닫힘 저장을 미뤘다(창 열림·밝아지는 중·3초 안, 3-3). 부르는 쪽이 다음 프레임에 다시 부른다.</summary>
        Waiting,
        Skipped,
        Failed,
    }

    /// <summary>
    /// 저장을 실제로 읽고 쓰는 곳(기획/저장-처음화면-멈춤창-1차.md 2-1·2-7·3장·4-4·4-5·4-7). 저장은 마을에서만 한다(사용자 결정 2026-10-07).
    /// 파일 한 칸: Application.persistentDataPath/save/slot1.txt(에디터는 editor-slot1.txt — 에디터 검증이 실제 게임 저장을 덮지 않게, 2-1).
    /// 쓰기·읽기·복사본 남기기는 Core SaveSlot, 글 모양은 SaveFile, 언제 쓰나는 SaveRules.Decide, 화면 글은 SaveText(작성자 A).
    /// 저장 직전에 TownRoot.CaptureCarry로 마을에서 바꾼 장비·스킬을 꾸러미에 담는다(2-7).
    /// 부르는 곳: 마을 자동 저장(TownAutoSave), 권양기 출발 직전(TownRoot.Depart), 멈춤 창 '저장하고 …'(GameFlow), 처음 화면(Probe·이어하기·새로 시작).
    /// 정적 값은 플레이를 새로 시작할 때(SubsystemRegistration) 비운다(도메인 다시 불러오기 꺼짐).
    /// </summary>
    public static class GameSave
    {
        static SaveSlot _slot;
        /// <summary>마지막으로 쓴(또는 이어하기로 읽은) 꾸러미 글. 창 닫힘 저장의 '같으면 안 씀'(3-3).</summary>
        static string _lastBody;
        /// <summary>마지막으로 쓴(또는 이어하기로 읽은) 실제 시각(Time.realtimeSinceStartup). 쓴 적 없으면 음수.</summary>
        static float _lastWriteRealtime = -1f;
        /// <summary>마을이 밝아진 뒤 띄울 알림(이어서 알림·읽지 못해 새로 시작, 4-8). TakeArrivalNotice가 꺼내 비운다.</summary>
        static string[] _arrivalNotice;
        /// <summary>쓰기 실패 알림을 이미 띄웠다(실패가 이어지는 동안 한 번, 2-6).</summary>
        static bool _failNoticeShown;
        /// <summary>'F1이라 이제 저장하지 않는다' 알림을 이미 띄웠다(판마다 한 번, 3-1).</summary>
        static bool _devWarned;
        /// <summary>창 닫힘 저장을 마지막으로 '안 씀'으로 콘솔에 적은 판정. 같은 까닭이 이어지면 다시 적지 않는다(저장하지 않는 판에서 창을 닫을 때마다 한 줄씩 쌓이지 않게).</summary>
        static SaveVerdict? _quietSkip;

        /// <summary>이번 판의 마지막 저장 시각(UTC). 이어하기면 읽은 머리 값으로 시작, 아직 없으면 null(멈춤 창 아래 줄, 5-3).</summary>
        public static DateTime? LastSavedUtc { get; private set; }
        /// <summary>마지막 저장이 권양기 출발 직전 저장(trip=1)인가(던전 멈춤 창 확인 글, 5-4).</summary>
        public static bool LastSaveWasTrip { get; private set; }
        /// <summary>마지막 쓰기 실패 까닭(성공하면 null, 멈춤 창 아래 줄, 5-3).</summary>
        public static string LastError { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _slot = null;
            _lastBody = null;
            _lastWriteRealtime = -1f;
            _arrivalNotice = null;
            _failNoticeShown = false;
            _devWarned = false;
            _quietSkip = null;
            LastSavedUtc = null;
            LastSaveWasTrip = false;
            LastError = null;
        }

        /// <summary>저장 칸 이름(2-1). 에디터는 따로 둔다.</summary>
        public static string SlotName => Application.isEditor ? "editor-slot1" : "slot1";

        /// <summary>저장 칸(처음 쓸 때 만든다). 폴더는 persistentDataPath/save.</summary>
        public static SaveSlot Slot
        {
            get
            {
                if (_slot == null) _slot = new SaveSlot(Path.Combine(Application.persistentDataPath, "save"), SlotName);
                return _slot;
            }
        }

        /// <summary>이번 판이 저장하는 판인가(처음 화면에서 시작 · 시험 메뉴 판 아님 · 이번 판에 F1을 쓰지 않음, 3-1).</summary>
        public static bool SavingSession => SaveRules.SavingSession(GameSession.FromTitle, TestLaunchSession.Active, DevPanelUsed);

        /// <summary>
        /// 이번 판(처음 화면에서 시작한 때부터)에 F1 시험 패널 손잡이·단추를 썼다(3-1). TestRunFlag.Reasons의 DevPanel 까닭은 플레이 전체 표시라 쓰지 않는다.
        /// </summary>
        static bool DevPanelUsed => TestRunFlag.DevPanelNotes > GameSession.DevPanelMark;

        // ── 처음 화면(4-3·4-4·4-5·4-7) ─────────────────────────

        /// <summary>
        /// 저장을 읽어 본다(꾸러미를 깔지 않는다). 본 파일 → 백업 차례로 읽고(SaveSlot.Load), 읽은 쪽에 따라 Ready·FromBackup,
        /// 못 읽었으면 더 새 판이면 Newer, 파일이 있으면 Broken, 없으면 None. 예외는 잡아 Broken.
        /// </summary>
        public static SaveProbe Probe()
        {
            var p = new SaveProbe { State = SaveProbeState.None };
            try
            {
                var r = Slot.Load();
                if (r == null) return p;
                p.AnyFile = r.AnyFile;
                if (r.Ok && r.Carry != null)
                {
                    bool fromBackup = r.Source == SaveLoadSource.Backup;
                    var h = r.Header ?? new SaveHeader();
                    p.State = fromBackup ? SaveProbeState.FromBackup : SaveProbeState.Ready;
                    p.Header = h;
                    p.Carry = r.Carry;
                    p.ContinueLine = SaveText.ContinueLine(h, r.Carry, LocalTime(h), fromBackup);
                }
                else if (r.Newer) p.State = SaveProbeState.Newer;
                else if (r.AnyFile) p.State = SaveProbeState.Broken;
                else p.State = SaveProbeState.None;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[저장] 저장 파일을 읽다가 오류가 났다 — 읽지 못한 저장으로 본다: " + e.Message);
                p.State = SaveProbeState.Broken;
                p.Header = null;
                p.Carry = null;
                p.ContinueLine = null;
                p.AnyFile = true;
            }
            return p;
        }

        /// <summary>
        /// 이어하기(4-4의 3·4): 저장을 다시 읽어 꾸러미를 깐다. 읽을 수 있는 저장이 아니면 false(아무것도 바꾸지 않음).
        /// 백업에서 읽었으면 먼저 상한 본 파일을 '-broken-' 이름으로 복사해 남긴다 — 다음 저장이 상한 본을 .bak으로 밀어 넣으므로(2-4의 3).
        /// 꾸러미·도착 쪽지를 비우고 깐 뒤 저장하는 판을 시작한다(놀이 시간은 머리 값부터). 마을이 밝아지면 '이어서' 알림을 띄운다(4-8).
        /// 장면 불러오기는 부르는 쪽(GameFlow.Continue)이 한다.
        /// </summary>
        public static bool InstallForContinue()
        {
            var p = Probe();
            if (p.State != SaveProbeState.Ready && p.State != SaveProbeState.FromBackup) return false;
            if (p.Carry == null) return false;
            bool fromBackup = p.State == SaveProbeState.FromBackup;
            var header = p.Header ?? new SaveHeader();
            if (fromBackup)
            {
                var kept = KeepCopies("broken");
                Debug.Log("[저장] 본 파일이 상해 바로 앞 저장(백업)으로 이어한다 — 상한 파일을 남김: " + string.Join(", ", kept));
            }
            ProfileCarry.Clear();
            TownTravel.Clear();
            ProfileCarry.Install(p.Carry);
            GameSession.BeginFromTitle(header);
            LastSavedUtc = header.SavedUtc == default(DateTime) ? (DateTime?)null : header.SavedUtc;
            LastSaveWasTrip = header.Trip;
            LastError = null;
            _lastBody = SaveFile.BodyOf(p.Carry);
            _lastWriteRealtime = Time.realtimeSinceStartup;
            _arrivalNotice = SaveText.ContinueNotice(header, fromBackup, LocalTime(header));
            _failNoticeShown = false;
            _devWarned = false;
            _quietSkip = null;
            Debug.Log($"[저장] 이어하기 — 원정 {p.Carry.Expedition} · 레벨 {p.Carry.Level} · 놀이 {header.PlaySeconds}초" +
                      (header.Trip ? " · 출발 직전 저장" : "") + (fromBackup ? " · 백업" : "") + " · " + SafeMainPath());
            return true;
        }

        /// <summary>
        /// 새로 시작(4-5의 2·3): 원래 파일을 복사해 남긴 뒤 꾸러미를 비우고 새 프로필로 저장하는 판을 시작한다(놀이 시간 0).
        /// 읽지 못한 저장·더 새 판 저장이면 '-broken-'으로 남기고 마을에서 알린다. 읽을 수 있는 저장이면 '-old-'로 남긴다
        /// (.bak은 다음 저장 두 번이면 새 판 것으로 바뀌므로, 4-5). 지금 저장은 오프닝 대화가 끝날 때 처음 덮어쓴다(3-2 '오프닝 전').
        /// 장면 불러오기는 부르는 쪽(GameFlow.NewGame)이 한다.
        /// </summary>
        public static void InstallNewGame()
        {
            var p = Probe();
            _arrivalNotice = null;
            if (p.State == SaveProbeState.Broken || p.State == SaveProbeState.Newer)
            {
                var kept = KeepCopies("broken");
                if (kept.Count > 0) _arrivalNotice = new[] { SaveText.BrokenNewStart(Path.GetFileName(kept[0])) };
                Debug.Log("[저장] 읽지 못한 저장을 남기고 새로 시작한다: " + (kept.Count > 0 ? string.Join(", ", kept) : "(남긴 것 없음)"));
            }
            else if (p.State == SaveProbeState.Ready || p.State == SaveProbeState.FromBackup)
            {
                var kept = KeepCopies("old");
                Debug.Log("[저장] 새로 시작 — 예전 저장을 남김: " + (kept.Count > 0 ? string.Join(", ", kept) : "(남긴 것 없음)"));
            }
            ProfileCarry.Clear();
            TownTravel.Clear();
            ProfileCarry.Ensure();
            GameSession.BeginFromTitle(null);
            LastSavedUtc = null;
            LastSaveWasTrip = false;
            LastError = null;
            _lastBody = null;
            _lastWriteRealtime = -1f;
            _failNoticeShown = false;
            _devWarned = false;
            _quietSkip = null;
        }

        // ── 마을 저장(3장) ──────────────────────────────────

        /// <summary>
        /// 마을에서 저장한다(3-2 시점, 3-3 묶기, 3-4 저장하지 않는 곳). 무엇을 할지는 SaveRules.Decide가 정한다:
        /// 저장하는 판 아님 → 마을 아님 → 오프닝 전 → 떠나는 중(출발 저장 빼고) → 꼭 써야 하는 저장이면 씀 → 창 열림·밝아지는 중·3초 안이면 기다림 → 씀.
        /// 마을인지는 TownRoot.Instance가 있고 DungeonRoot.Instance가 없음으로 본다(장면이 바뀌는 프레임에 둘이 겹쳐도 던전 쪽이면 막음, 3-4).
        /// 새로 시작한 판은 오프닝을 보기 전에는 어떤 까닭이든 쓰지 않는다(3-2). 쓰기 직전에 TownRoot.CaptureCarry로 마을 몫을 꾸러미에 담는다(2-7).
        /// 창 닫힘 저장은 꾸러미 글이 마지막으로 쓴 글과 같으면 쓰지 않는다(3-3). 실패해도 놀이는 그대로이고 알림은 실패가 이어지는 동안 한 번(2-6).
        /// 예외를 밖으로 내지 않는다(권양기 출발·게임 창 닫기에서 불리므로).
        /// </summary>
        public static SaveOutcome SaveTown(SaveReason reason)
        {
            TownRoot root;
            SaveVerdict verdict;
            try
            {
                root = TownRoot.Instance;
                // 문은 Ensure를 부르지 않는다 — 마을이 아닐 때 새 프로필을 만들지 않게.
                var carryNow = ProfileCarry.Data;
                var gate = new SaveGate
                {
                    InTown = root && DungeonRoot.Instance == null && !GameSession.TitleShowing,
                    FromTitle = GameSession.FromTitle,
                    TestSession = TestLaunchSession.Active,
                    DevPanelUsed = DevPanelUsed,
                    OpeningPending = carryNow == null || !TownSave.SeenScene(carryNow, TownScript.OpeningId),
                    Leaving = root && root.Leaving,
                    Busy = root && root.Busy && !root.Leaving,
                    ModalOpen = DungeonUi.ModalOpen,
                    SinceLastWrite = _lastWriteRealtime < 0f ? 1e9 : Time.realtimeSinceStartup - _lastWriteRealtime,
                };
                verdict = SaveRules.Decide(reason, gate);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[저장] 저장할지 정하지 못했다({reason}) — 쓰지 않는다: {e.Message}");
                return SaveOutcome.Skipped;
            }

            switch (verdict)
            {
                case SaveVerdict.Write:
                    // Decide는 마을이 아니면 쓰지 않는다. 그래도 루트가 없으면 쓰지 않는다(CaptureCarry를 부를 곳이 없음).
                    if (!root)
                    {
                        Debug.Log($"[저장] 안 씀 {reason} — 마을 루트가 없음");
                        return SaveOutcome.Skipped;
                    }
                    break;
                case SaveVerdict.Wait:
                    return SaveOutcome.Waiting;
                default:
                    if (verdict == SaveVerdict.SkipNotSaving) WarnIfDevPanelUsed();
                    // 창 닫힘은 같은 까닭이 이어지면 한 번만 적는다(맞추기: 저장하지 않는 판에서 창을 닫을 때마다 콘솔이 쌓이지 않게). 꼭 써야 하는 저장은 늘 적는다.
                    if (reason != SaveReason.WindowClosed || _quietSkip != verdict)
                        Debug.Log($"[저장] 안 씀 {reason} — {SkipName(verdict)}");
                    if (reason == SaveReason.WindowClosed) _quietSkip = verdict;
                    return SaveOutcome.Skipped;
            }

            string body;
            SaveHeader header;
            SaveWriteResult r;
            try
            {
                var carry = ProfileCarry.Ensure();
                root.CaptureCarry(carry);
                body = SaveFile.BodyOf(carry);
                if (reason == SaveReason.WindowClosed && body == _lastBody) return SaveOutcome.Unchanged;
                header = new SaveHeader
                {
                    SavedUtc = DateTime.UtcNow,
                    PlaySeconds = (long)GameSession.PlaySeconds,
                    Trip = reason == SaveReason.Departure,
                };
                r = Slot.Write(SaveFile.Compose(carry, header));
            }
            catch (Exception e)
            {
                // 화면 글(LastError)은 한국어만, 예외 형식 이름은 콘솔 경고에만(2-6).
                return Fail(reason, "저장 글을 만들지 못했다", $"{e.GetType().Name}: {e.Message}");
            }

            if (r == null || !r.Ok) return Fail(reason, r != null && !string.IsNullOrEmpty(r.Error) ? r.Error : "까닭을 모른다", r?.Detail);

            _lastBody = body;
            _lastWriteRealtime = Time.realtimeSinceStartup;
            LastSavedUtc = header.SavedUtc;
            LastSaveWasTrip = header.Trip;
            LastError = null;
            _failNoticeShown = false;
            _quietSkip = null;
            Debug.Log($"[저장] {reason} · {SafeMainPath()}");
            return SaveOutcome.Written;
        }

        /// <summary>쓰기 실패: 까닭을 남기고 알림은 실패가 이어지는 동안 한 번(2-6). 놀이는 그대로 이어지고 다음 저장 때 다시 쓴다.</summary>
        static SaveOutcome Fail(SaveReason reason, string error, string detail)
        {
            LastError = error;
            Debug.LogWarning($"[저장] 실패 {reason} — {error}" + (string.IsNullOrEmpty(detail) ? "" : " · " + detail));
            if (!_failNoticeShown)
            {
                _failNoticeShown = true;
                DungeonEvents.Say(SaveText.SaveFailed);
            }
            return SaveOutcome.Failed;
        }

        /// <summary>
        /// 처음 화면에서 시작한 판에서 F1을 썼으면 '이제 저장하지 않는다'를 한 번 알린다(판마다 한 번, 3-1).
        /// 저장이 '저장하는 판 아님'으로 빠질 때와 마을 자동 저장(TownAutoSave)이 매 프레임 부른다(F1을 쓴 바로 뒤에 알리게).
        /// </summary>
        internal static void WarnIfDevPanelUsed()
        {
            if (_devWarned || !GameSession.FromTitle || !DevPanelUsed) return;
            _devWarned = true;
            DungeonEvents.Say(SaveText.NotSavingDevPanel);
            Debug.Log("[저장] 이번 판에 F1 시험 패널을 써서 이제 저장하지 않는다");
        }

        /// <summary>마을이 밝아진 뒤 띄울 알림을 꺼낸다(한 번, 꺼내면 비움, 4-8). 없으면 null.</summary>
        public static string[] TakeArrivalNotice()
        {
            var lines = _arrivalNotice;
            _arrivalNotice = null;
            return lines;
        }

        /// <summary>시험 메뉴 '저장 파일 글 불러오기'(8-3): 본 파일 글, 없으면 백업 글, 둘 다 없으면 null(예외도 null).</summary>
        public static string ReadRawTextForTest()
        {
            try
            {
                return Slot.ReadRaw();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[저장] 저장 파일 글을 읽지 못했다: " + e.Message);
                return null;
            }
        }

        // ── 돕는 것 ─────────────────────────────────────────

        /// <summary>원래 파일을 복사해 남긴다(SaveSlot.KeepCopies, 이 기기 시각 이름). 예외는 잡고 만든 것만 돌려준다.</summary>
        static List<string> KeepCopies(string tag)
        {
            try
            {
                return Slot.KeepCopies(DateTime.Now, tag) ?? new List<string>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[저장] 원래 저장 파일을 복사해 남기지 못했다({tag}): " + e.Message);
                return new List<string>();
            }
        }

        /// <summary>저장 시각을 이 기기 시각으로(시각이 없는 머리는 기본값 그대로 — SaveText가 그 마디를 뺀다).</summary>
        static DateTime LocalTime(SaveHeader h) => h == null || h.SavedUtc == default(DateTime) ? default(DateTime) : h.SavedUtc.ToLocalTime();

        static string SafeMainPath()
        {
            try
            {
                return Slot.MainPath;
            }
            catch (Exception)
            {
                return "(경로 모름)";
            }
        }

        static string SkipName(SaveVerdict v)
        {
            switch (v)
            {
                case SaveVerdict.SkipNotSaving: return "저장하는 판이 아님";
                case SaveVerdict.SkipNotTown: return "마을이 아님";
                case SaveVerdict.SkipBeforeOpening: return "오프닝 전";
                case SaveVerdict.SkipLeaving: return "떠나는 중";
                default: return v.ToString();
            }
        }
    }
}
