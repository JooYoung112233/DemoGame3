using System;
using Demo6.Core.Save;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 저장·처음 화면·멈춤 창 1차 12-3 SaveRulesTests(1~7·9): 저장하는 판(3-1), 마을에서만(3-4), 떠나는 중, 꼭 써야 하는 저장과 창 닫힘 묶기(3-3),
    /// 도착 저장(3-2), 처음 화면을 띄울 때(4-1), 오프닝 전에는 어떤 까닭이든 쓰지 않음(3-2, 16장 2).
    /// </summary>
    public sealed class SaveRulesTests
    {
        static readonly SaveReason[] AllReasons = (SaveReason[])Enum.GetValues(typeof(SaveReason));

        static readonly SaveReason[] ForcedReasons =
        {
            SaveReason.Arrival, SaveReason.Departure, SaveReason.PauseToTitle, SaveReason.PauseQuit, SaveReason.AppQuit,
        };

        /// <summary>처음 화면에서 시작한 판, 마을, 창 없음, 마지막 저장 뒤 10초.</summary>
        static SaveGate TownGate() => new SaveGate { InTown = true, FromTitle = true, SinceLastWrite = 10 };

        // 1
        [Test]
        public void NotSavingSessionsSkipEveryReason()
        {
            Assert.IsTrue(SaveRules.SavingSession(true, false, false));
            Assert.IsFalse(SaveRules.SavingSession(false, false, false), "처음 화면에서 시작하지 않음(바로 Play)");
            Assert.IsFalse(SaveRules.SavingSession(true, true, false), "시험 메뉴 판");
            Assert.IsFalse(SaveRules.SavingSession(true, false, true), "F1을 쓴 판");

            var notTitle = TownGate();
            notTitle.FromTitle = false;
            var test = TownGate();
            test.TestSession = true;
            var dev = TownGate();
            dev.DevPanelUsed = true;
            foreach (var gate in new[] { notTitle, test, dev })
                foreach (var reason in AllReasons)
                    Assert.AreEqual(SaveVerdict.SkipNotSaving, SaveRules.Decide(reason, gate), reason.ToString());

            // 마을이 아니고 떠나는 중이어도 '저장하는 판 아님'이 먼저다.
            dev.InTown = false;
            dev.Leaving = true;
            Assert.AreEqual(SaveVerdict.SkipNotSaving, SaveRules.Decide(SaveReason.Departure, dev));
        }

        // 2
        [Test]
        public void OutsideTownNeverWrites()
        {
            var g = TownGate();
            g.InTown = false;
            foreach (var reason in AllReasons)
                Assert.AreEqual(SaveVerdict.SkipNotTown, SaveRules.Decide(reason, g), reason + ": 던전·계단·원정 중은 쓰지 않음");
            g.Leaving = true;
            Assert.AreEqual(SaveVerdict.SkipNotTown, SaveRules.Decide(SaveReason.Departure, g), "출발 저장도 마을이 아니면 안 씀");
        }

        // 3
        [Test]
        public void LeavingSkipsAllButDeparture()
        {
            var g = TownGate();
            g.Leaving = true;
            Assert.AreEqual(SaveVerdict.SkipLeaving, SaveRules.Decide(SaveReason.AppQuit, g), "떠나는 중 창 닫기는 안 씀(출발 직전 저장이 있다)");
            Assert.AreEqual(SaveVerdict.SkipLeaving, SaveRules.Decide(SaveReason.WindowClosed, g));
            Assert.AreEqual(SaveVerdict.Write, SaveRules.Decide(SaveReason.Departure, g));
            foreach (var reason in AllReasons)
                if (reason != SaveReason.Departure)
                    Assert.AreEqual(SaveVerdict.SkipLeaving, SaveRules.Decide(reason, g), reason.ToString());
        }

        // 4
        [Test]
        public void ForcedReasonsWriteEvenWithWindowFadeOrZeroGap()
        {
            foreach (var reason in AllReasons)
                Assert.AreEqual(reason != SaveReason.WindowClosed, SaveRules.Forced(reason), reason.ToString());

            var g = TownGate();
            g.ModalOpen = true;
            g.Busy = true;
            g.SinceLastWrite = 0;
            foreach (var reason in ForcedReasons)
                Assert.AreEqual(SaveVerdict.Write, SaveRules.Decide(reason, g), reason.ToString());
        }

        // 5
        [Test]
        public void WindowClosedWaitsForWindowFadeAndGap()
        {
            var modal = TownGate();
            modal.ModalOpen = true;
            Assert.AreEqual(SaveVerdict.Wait, SaveRules.Decide(SaveReason.WindowClosed, modal), "창이 열려 있음");
            var busy = TownGate();
            busy.Busy = true;
            Assert.AreEqual(SaveVerdict.Wait, SaveRules.Decide(SaveReason.WindowClosed, busy), "밝아지는 중");
            var soon = TownGate();
            soon.SinceLastWrite = 1;
            Assert.AreEqual(SaveVerdict.Wait, SaveRules.Decide(SaveReason.WindowClosed, soon), "1초 뒤");
            soon.SinceLastWrite = 2.99;
            Assert.AreEqual(SaveVerdict.Wait, SaveRules.Decide(SaveReason.WindowClosed, soon));
            soon.SinceLastWrite = SaveRules.MinGapSeconds;
            Assert.AreEqual(3.0, SaveRules.MinGapSeconds);
            Assert.AreEqual(SaveVerdict.Write, SaveRules.Decide(SaveReason.WindowClosed, soon), "3초 뒤");
            Assert.AreEqual(SaveVerdict.Write, SaveRules.Decide(SaveReason.WindowClosed, TownGate()));
            var never = TownGate();
            never.SinceLastWrite = double.MaxValue;
            Assert.AreEqual(SaveVerdict.Write, SaveRules.Decide(SaveReason.WindowClosed, never), "아직 쓴 적 없음");
        }

        // 6
        [Test]
        public void SaveOnArrivalOnlyForNewBasketArrival()
        {
            Assert.IsFalse(SaveRules.SaveOnArrival(TownArrivalKind.NewPlay, false), "새 플레이");
            Assert.IsFalse(SaveRules.SaveOnArrival(TownArrivalKind.NewPlay, true));
            Assert.IsFalse(SaveRules.SaveOnArrival(TownArrivalKind.Basket, true), "같은 도착");
            Assert.IsTrue(SaveRules.SaveOnArrival(TownArrivalKind.Basket, false), "바구니 새 도착");
        }

        // 7
        [Test]
        public void ShowTitleRule()
        {
            Assert.IsTrue(SaveRules.ShowTitle(false, false, false), "차가운 시작(꾸러미 없음)");
            Assert.IsFalse(SaveRules.ShowTitle(false, true, false), "꾸러미 있음(바로 Play한 던전에서 올라옴)");
            Assert.IsFalse(SaveRules.ShowTitle(false, false, true), "시험 판(꾸러미 없음)");
            Assert.IsTrue(SaveRules.ShowTitle(true, true, false), "처음 화면 요청");
            Assert.IsTrue(SaveRules.ShowTitle(true, true, true), "요청이면 시험 판이어도");
        }

        // 9
        [Test]
        public void BeforeOpeningSkipsEveryReason()
        {
            var g = TownGate();
            g.OpeningPending = true;
            foreach (var reason in AllReasons)
                Assert.AreEqual(SaveVerdict.SkipBeforeOpening, SaveRules.Decide(reason, g), reason + ": 오프닝 전에는 꼭 써야 하는 저장도 안 씀");

            var waiting = g;
            waiting.ModalOpen = true;
            waiting.SinceLastWrite = 0;
            Assert.AreEqual(SaveVerdict.SkipBeforeOpening, SaveRules.Decide(SaveReason.WindowClosed, waiting), "기다림보다 먼저");

            var leaving = g;
            leaving.Leaving = true;
            foreach (var reason in AllReasons)
                Assert.AreEqual(SaveVerdict.SkipBeforeOpening, SaveRules.Decide(reason, leaving), reason + ": 떠나는 중보다 먼저");

            var notSaving = g;
            notSaving.FromTitle = false;
            Assert.AreEqual(SaveVerdict.SkipNotSaving, SaveRules.Decide(SaveReason.AppQuit, notSaving), "저장하는 판 아님이 먼저");
            var notTown = g;
            notTown.InTown = false;
            Assert.AreEqual(SaveVerdict.SkipNotTown, SaveRules.Decide(SaveReason.PauseQuit, notTown), "마을 아님이 먼저");
        }
    }
}
