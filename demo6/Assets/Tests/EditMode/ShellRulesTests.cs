using System.Linq;
using Demo6.Core.Save;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 처음 화면·멈춤 창·Esc 규칙(기획/저장-처음화면-멈춤창-1차.md 12-4의 1~6): Esc 표 한 칸씩, 멈춤 창이 다른 창보다 먼저,
    /// 멈춤 창 항목(마을 저장함 / 마을 저장 안 함·던전·전투 시험장 / 처음 화면·없음), 떠나기 확인, 처음 화면 단추 차례, 새로 시작 묻기·시험 메뉴 규칙.
    /// 화면 글(ShellText)은 멈춤 창 아래 줄과 떠나기 확인 글 고르기만 본다(5-3·5-4).
    /// </summary>
    public sealed class ShellRulesTests
    {
        static EscState At(ShellPlace place) => new EscState { Place = place };

        // ── 1. Esc 표(5-1) 한 칸씩 ──

        [Test]
        public void EscapeTableRowByRow()
        {
            // 1 불러오는 중: 무엇이 열려 있어도 없음.
            var loading = At(ShellPlace.Town);
            loading.Loading = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(loading), "불러오는 중");
            loading.PauseOpen = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(loading), "불러오는 중에는 멈춤 창도 닫지 않음");
            var loadingTitle = At(ShellPlace.Title);
            loadingTitle.Loading = true;
            loadingTitle.TitleSubOpen = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(loadingTitle), "불러오는 중에는 처음 화면 하위 창도 그대로");

            // 2 처음 화면: 설정·확인이 열려 있으면 그것만 닫음, 아니면 없음.
            var title = At(ShellPlace.Title);
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(title), "처음 화면 첫 화면");
            title.TitleSubOpen = true;
            Assert.AreEqual(EscAction.TitleBack, ShellRules.OnEscape(title), "처음 화면 설정·확인");

            // 3 멈춤 창 안의 설정·지난 알림·확인 → 첫 화면으로.
            var pauseSub = At(ShellPlace.Town);
            pauseSub.PauseOpen = true;
            pauseSub.PauseSubOpen = true;
            Assert.AreEqual(EscAction.PauseBack, ShellRules.OnEscape(pauseSub), "멈춤 창 안 창");

            // 4 멈춤 창 첫 화면 → 닫기.
            var pause = At(ShellPlace.Dungeon);
            pause.PauseOpen = true;
            Assert.AreEqual(EscAction.ClosePause, ShellRules.OnEscape(pause), "멈춤 창");

            // 5 다른 창 → 없음(그 창이 스스로 닫는다).
            var other = At(ShellPlace.Town);
            other.OtherModalOpen = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(other), "다른 창");

            // 6 곳 없음(시험 메뉴 장면 등) → 없음.
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(At(ShellPlace.None)), "곳 없음");

            // 7 입력 막힘·창 없이 멈춤·밝아지는 중·층에 들어서기 전 → 없음.
            var blocked = At(ShellPlace.Town);
            blocked.InputBlocked = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(blocked), "입력 막힘(떠나는 암전·밤 카드 대기)");
            var busy = At(ShellPlace.Town);
            busy.SceneBusy = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(busy), "마을이 밝아지는 중");
            var combatPaused = At(ShellPlace.CombatTest);
            combatPaused.SceneBusy = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(combatPaused), "전투 시험장 '멈춤' 단추로 멈춘 동안");

            // 8 그 밖 → 멈춤 창 열기(마을·던전·전투 시험장).
            Assert.AreEqual(EscAction.OpenPause, ShellRules.OnEscape(At(ShellPlace.Town)), "마을");
            Assert.AreEqual(EscAction.OpenPause, ShellRules.OnEscape(At(ShellPlace.Dungeon)), "던전");
            Assert.AreEqual(EscAction.OpenPause, ShellRules.OnEscape(At(ShellPlace.CombatTest)), "전투 시험장");
        }

        [Test]
        public void TitleNeverOpensPause()
        {
            var title = At(ShellPlace.Title);
            title.InputBlocked = false;
            title.SceneBusy = false;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(title), "처음 화면에서는 멈춤 창이 열리지 않음");
            title.PauseOpen = true;
            Assert.AreEqual(EscAction.None, ShellRules.OnEscape(title), "처음 화면이 멈춤 창보다 먼저");
        }

        // ── 2. 멈춤 창이 다른 창보다 먼저 ──

        [Test]
        public void PauseComesBeforeOtherModal()
        {
            var s = At(ShellPlace.Town);
            s.PauseOpen = true;
            s.OtherModalOpen = true;
            Assert.AreEqual(EscAction.ClosePause, ShellRules.OnEscape(s), "멈춤 창 쪽이 먼저");
            s.PauseSubOpen = true;
            Assert.AreEqual(EscAction.PauseBack, ShellRules.OnEscape(s), "멈춤 창 안 창도 먼저");

            var guarded = At(ShellPlace.Dungeon);
            guarded.PauseOpen = true;
            guarded.InputBlocked = true;
            guarded.SceneBusy = true;
            Assert.AreEqual(EscAction.ClosePause, ShellRules.OnEscape(guarded), "멈춤 창이 열려 있으면 막힘·바쁨보다 먼저 닫는다");
        }

        // ── 3. 멈춤 창 항목(5-3) ──

        [Test]
        public void PauseItemsByPlace()
        {
            var saving = new[] { PauseItem.Resume, PauseItem.Settings, PauseItem.Notices, PauseItem.SaveAndTitle, PauseItem.SaveAndQuit };
            var leave = new[] { PauseItem.Resume, PauseItem.Settings, PauseItem.Notices, PauseItem.ToTitle, PauseItem.Quit };

            CollectionAssert.AreEqual(saving, ShellRules.PauseItems(ShellPlace.Town, true).ToArray(), "마을·저장하는 판");
            CollectionAssert.AreEqual(leave, ShellRules.PauseItems(ShellPlace.Town, false).ToArray(), "마을·저장하지 않는 판");
            CollectionAssert.AreEqual(leave, ShellRules.PauseItems(ShellPlace.Dungeon, true).ToArray(), "던전(저장하는 판도 저장 단추 없음)");
            CollectionAssert.AreEqual(leave, ShellRules.PauseItems(ShellPlace.Dungeon, false).ToArray(), "던전·저장하지 않는 판");
            CollectionAssert.AreEqual(leave, ShellRules.PauseItems(ShellPlace.CombatTest, false).ToArray(), "전투 시험장");
            CollectionAssert.AreEqual(leave, ShellRules.PauseItems(ShellPlace.CombatTest, true).ToArray(), "전투 시험장(판과 상관없음)");
            Assert.AreEqual(0, ShellRules.PauseItems(ShellPlace.Title, true).Count, "처음 화면");
            Assert.AreEqual(0, ShellRules.PauseItems(ShellPlace.None, false).Count, "곳 없음");
        }

        // ── 4. 떠나기 확인(5-4) ──

        [Test]
        public void ConfirmBeforeLeavingCases()
        {
            Assert.IsTrue(ShellRules.ConfirmBeforeLeaving(ShellPlace.Dungeon, true), "던전·저장하는 판 — 이번 원정은 남지 않는다");
            Assert.IsTrue(ShellRules.ConfirmBeforeLeaving(ShellPlace.Dungeon, false), "던전·저장하지 않는 판");
            Assert.IsTrue(ShellRules.ConfirmBeforeLeaving(ShellPlace.Town, false), "마을·저장하지 않는 판");
            Assert.IsFalse(ShellRules.ConfirmBeforeLeaving(ShellPlace.Town, true), "마을·저장하는 판은 '저장하고 …'라 묻지 않음");
            Assert.IsFalse(ShellRules.ConfirmBeforeLeaving(ShellPlace.CombatTest, false), "전투 시험장은 묻지 않음");
            Assert.IsFalse(ShellRules.ConfirmBeforeLeaving(ShellPlace.CombatTest, true), "전투 시험장은 묻지 않음");
        }

        // ── 5. 처음 화면 단추 차례(4-3) ──

        [Test]
        public void TitleItemsOrder()
        {
            CollectionAssert.AreEqual(
                new[] { TitleItem.Continue, TitleItem.NewGame, TitleItem.Settings, TitleItem.TestMenu, TitleItem.Quit },
                ShellRules.TitleItems(true, true).ToArray(), "이어하기 가능·개발");
            CollectionAssert.AreEqual(
                new[] { TitleItem.NewGame, TitleItem.Settings, TitleItem.Quit },
                ShellRules.TitleItems(false, false).ToArray(), "저장 없음·정식 빌드");

            foreach (bool canContinue in new[] { false, true })
            foreach (bool dev in new[] { false, true })
            {
                var items = ShellRules.TitleItems(canContinue, dev);
                string label = $"이어하기 {canContinue} · 개발 {dev}";
                Assert.AreEqual(canContinue, items[0] == TitleItem.Continue, label + ": 이어하기는 가능할 때만, 늘 첫째");
                Assert.AreEqual(canContinue, items.Contains(TitleItem.Continue), label + ": 이어하기");
                Assert.AreEqual(dev, items.Contains(TitleItem.TestMenu), label + ": 시험 메뉴는 개발일 때만");
                Assert.IsTrue(items.Contains(TitleItem.NewGame) && items.Contains(TitleItem.Settings), label + ": 새로 시작·설정은 늘");
                Assert.AreEqual(TitleItem.Quit, items[items.Count - 1], label + ": 끝내기는 늘 마지막");
                Assert.AreEqual(items.Count, items.Distinct().Count(), label + ": 같은 단추 두 번 없음");
            }
        }

        // ── 6. 새로 시작 묻기·시험 메뉴 ──

        [Test]
        public void NewGameAskAndTestMenuRules()
        {
            Assert.IsTrue(ShellRules.AskBeforeNewGame(true), "저장 파일이 있으면(읽지 못한 것 포함) 묻는다");
            Assert.IsFalse(ShellRules.AskBeforeNewGame(false), "파일이 없으면 확인 없이 새로 시작");

            Assert.IsTrue(ShellRules.ShowTestMenu(isEditor: true, isDevBuild: false), "에디터");
            Assert.IsTrue(ShellRules.ShowTestMenu(isEditor: false, isDevBuild: true), "개발용 빌드");
            Assert.IsFalse(ShellRules.ShowTestMenu(isEditor: false, isDevBuild: false), "정식 빌드에는 시험 메뉴로 갈 길이 없다");
        }

        // ── 화면 글 고르기(5-3·5-4) ──

        [Test]
        public void PauseFooterPicksLine()
        {
            const string when = "10월 7일 21:30";
            Assert.AreEqual("마지막 저장: " + when, ShellText.PauseFooter(ShellPlace.Town, true, when, false, null), "마을·저장하는 판");
            Assert.AreEqual(ShellText.NotSavedYet, ShellText.PauseFooter(ShellPlace.Town, true, null, false, null), "오프닝 전(아직 쓴 적 없음)");
            StringAssert.StartsWith("저장 실패 — 디스크에 쓰지 못했다.", ShellText.PauseFooter(ShellPlace.Town, true, when, false, "디스크에 쓰지 못했다"), "실패가 남아 있으면 실패 줄이 먼저");
            Assert.AreEqual(ShellText.NotSavingSession, ShellText.PauseFooter(ShellPlace.Town, false, when, false, null), "마을·저장하지 않는 판");

            string dungeon = ShellText.PauseFooter(ShellPlace.Dungeon, true, when, true, null);
            StringAssert.StartsWith("갱도에서는 저장하지 않는다", dungeon, "던전·저장하는 판");
            StringAssert.Contains("마을 출발 직전 (" + when + ")", dungeon, "출발 직전 저장");
            StringAssert.Contains("마지막 저장: 마을 (" + when + ")", ShellText.PauseFooter(ShellPlace.Dungeon, true, when, false, "실패"), "출발 저장 실패 뒤 마지막 저장은 마을");
            Assert.AreEqual(ShellText.NotSavingSession, ShellText.PauseFooter(ShellPlace.Dungeon, false, when, true, null), "던전·저장하지 않는 판");
            Assert.AreEqual(ShellText.NotSavingSession, ShellText.PauseFooter(ShellPlace.CombatTest, false, null, false, null), "전투 시험장");
            Assert.IsNull(ShellText.PauseFooter(ShellPlace.Title, true, when, false, null), "처음 화면");
            Assert.IsNull(ShellText.PauseFooter(ShellPlace.None, false, null, false, null), "곳 없음");
        }

        [Test]
        public void LeaveConfirmTexts()
        {
            const string when = "10월 7일 21:30";
            Assert.AreEqual("이번 원정은 남지 않는다", ShellText.LeaveConfirmTitle(ShellPlace.Dungeon, true));
            StringAssert.Contains("권양기를 떠나기 직전(" + when + ")", ShellText.LeaveConfirmBody(ShellPlace.Dungeon, true, when, true));
            StringAssert.StartsWith("마지막 저장: 마을 (" + when + ")", ShellText.LeaveConfirmBody(ShellPlace.Dungeon, true, when, false), "출발 저장이 없으면 마을 저장");
            Assert.AreEqual(ShellText.NoSaveBody, ShellText.LeaveConfirmBody(ShellPlace.Dungeon, true, null, false), "저장 없음");
            Assert.AreEqual("이 판은 저장하지 않는다", ShellText.LeaveConfirmTitle(ShellPlace.Town, false));
            Assert.AreEqual("이 판은 저장하지 않는다", ShellText.LeaveConfirmTitle(ShellPlace.Dungeon, false));
            Assert.AreEqual(ShellText.NotSavingBody, ShellText.LeaveConfirmBody(ShellPlace.Dungeon, false, when, true));
            Assert.AreEqual("처음 화면으로", ShellText.LeaveConfirmButton(true));
            Assert.AreEqual("끝내기", ShellText.LeaveConfirmButton(false));

            Assert.AreEqual("디스크에 쓰지 못했다. 그래도 끝낼까?", ShellText.SaveFailedBody("디스크에 쓰지 못했다.", false), "끝 마침표를 한 번만");
            StringAssert.StartsWith("까닭을 알 수 없다", ShellText.SaveFailedBody(null, true));
            Assert.AreEqual("그래도 끝내기", ShellText.SaveFailedButton(false));
            Assert.AreEqual("그래도 처음 화면으로", ShellText.SaveFailedButton(true));
        }

        [Test]
        public void EveryItemHasLabel()
        {
            foreach (PauseItem item in System.Enum.GetValues(typeof(PauseItem)))
                Assert.IsFalse(string.IsNullOrEmpty(ShellText.PauseItemLabel(item)), item.ToString());
            foreach (TitleItem item in System.Enum.GetValues(typeof(TitleItem)))
                Assert.IsFalse(string.IsNullOrEmpty(ShellText.TitleItemLabel(item)), item.ToString());
            Assert.AreEqual("저장하고 처음 화면으로", ShellText.PauseItemLabel(PauseItem.SaveAndTitle));
            Assert.AreEqual("시험 메뉴 (개발용)", ShellText.TitleItemLabel(TitleItem.TestMenu));
            Assert.AreEqual("저장 폴더: C:/save", ShellText.SaveFolderLine("C:/save"));
        }

        [Test]
        public void QuestWindowOpensOnlyInTownOrDungeonWhenFree()
        {
            var free = new EscState { Place = ShellPlace.Town };
            Assert.IsTrue(ShellRules.CanOpenQuests(free));
            free.Place = ShellPlace.Dungeon;
            Assert.IsTrue(ShellRules.CanOpenQuests(free));
            Assert.IsFalse(ShellRules.CanOpenQuests(new EscState { Place = ShellPlace.Title }));
            Assert.IsFalse(ShellRules.CanOpenQuests(new EscState { Place = ShellPlace.CombatTest }));
            Assert.IsFalse(ShellRules.CanOpenQuests(new EscState { Place = ShellPlace.Town, PauseOpen = true }));
            Assert.IsFalse(ShellRules.CanOpenQuests(new EscState { Place = ShellPlace.Town, OtherModalOpen = true }));
            Assert.IsFalse(ShellRules.CanOpenQuests(new EscState { Place = ShellPlace.Dungeon, InputBlocked = true }));
            Assert.IsFalse(ShellRules.CanOpenQuests(new EscState { Place = ShellPlace.Dungeon, SceneBusy = true }));
            Assert.IsFalse(ShellRules.CanOpenQuests(new EscState { Place = ShellPlace.Town, Loading = true }));
        }
    }
}
