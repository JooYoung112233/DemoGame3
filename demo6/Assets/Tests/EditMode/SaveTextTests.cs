using System;
using Demo6.Core.Dungeon;
using Demo6.Core.Save;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 저장·처음 화면·멈춤 창 1차 12-3의 8 SaveTextTests: 시각 '10월 7일 21:30', 놀이 시간, 이어하기 줄(4-3), 이어서 알림(4-8),
    /// 읽지 못해 새로 시작 알림, 실패·F1 알림 글(2-6·3-1).
    /// </summary>
    public sealed class SaveTextTests
    {
        static readonly DateTime Local = new DateTime(2026, 10, 7, 21, 30, 0);

        static SaveHeader Head(bool trip) =>
            new SaveHeader { SavedUtc = new DateTime(2026, 10, 7, 12, 30, 0, DateTimeKind.Utc), PlaySeconds = 3725, Trip = trip };

        static CarryData Carry()
        {
            var c = CarryData.NewProfile(1UL);
            c.Expedition = 3;
            c.Level = 4;
            return c;
        }

        [Test]
        public void WhenAndPlayTime()
        {
            Assert.AreEqual("10월 7일 21:30", SaveText.When(Local));
            Assert.AreEqual("1월 5일 09:05", SaveText.When(new DateTime(2026, 1, 5, 9, 5, 59)), "월·일 앞 0 없음, 시:분 두 자리");

            Assert.AreEqual("1분 미만", SaveText.PlayTime(30));
            Assert.AreEqual("1분 미만", SaveText.PlayTime(0));
            Assert.AreEqual("1분 미만", SaveText.PlayTime(-5));
            Assert.AreEqual("1분", SaveText.PlayTime(60));
            Assert.AreEqual("2분", SaveText.PlayTime(125));
            Assert.AreEqual("59분", SaveText.PlayTime(3599));
            Assert.AreEqual("1시간 2분", SaveText.PlayTime(3725));
            Assert.AreEqual("1시간 0분", SaveText.PlayTime(3600));
        }

        [Test]
        public void ContinueLineShowsExpeditionLevelTripAndBackup()
        {
            Assert.AreEqual("원정 3번째 준비 · 레벨 4 · 놀이 1시간 2분 · 10월 7일 21:30 저장", SaveText.ContinueLine(Head(false), Carry(), Local, false));
            Assert.AreEqual("원정 3번째 준비 · 레벨 4 · 놀이 1시간 2분 · 10월 7일 21:30 저장 · 원정 도중 끝냄", SaveText.ContinueLine(Head(true), Carry(), Local, false));
            Assert.AreEqual("원정 3번째 준비 · 레벨 4 · 놀이 1시간 2분 · 10월 7일 21:30 저장 (바로 앞 저장)", SaveText.ContinueLine(Head(false), Carry(), Local, true));
            Assert.AreEqual("원정 3번째 준비 · 레벨 4 · 놀이 1시간 2분 · 10월 7일 21:30 저장 · 원정 도중 끝냄 (바로 앞 저장)",
                SaveText.ContinueLine(Head(true), Carry(), Local, true));

            var noTime = new SaveHeader { PlaySeconds = 125 };
            Assert.AreEqual("원정 3번째 준비 · 레벨 4 · 놀이 2분", SaveText.ContinueLine(noTime, Carry(), Local, false), "시각 없음(머리 없는 글)이면 시각 마디를 뺀다");
        }

        [Test]
        public void ContinueNoticeAddsTripAndBackupLines()
        {
            CollectionAssert.AreEqual(new[] { "이어서 한다 — 10월 7일 21:30 마을 저장." }, SaveText.ContinueNotice(Head(false), false, Local));

            var trip = SaveText.ContinueNotice(Head(true), false, Local);
            Assert.AreEqual(2, trip.Length);
            Assert.AreEqual("지난번엔 갱도 안에서 끝냈다. 권양기를 떠나기 직전으로 돌아왔다 — 그 원정에서 얻은 것은 남지 않았다.", trip[1]);

            var backup = SaveText.ContinueNotice(Head(false), true, Local);
            CollectionAssert.AreEqual(new[] { "이어서 한다 — 10월 7일 21:30 마을 저장.", "저장 파일이 상해 바로 앞 저장을 불러왔다." }, backup);

            var both = SaveText.ContinueNotice(Head(true), true, Local);
            CollectionAssert.AreEqual(new[] { "이어서 한다 — 10월 7일 21:30 마을 저장.", SaveText.TripNotice, SaveText.BackupNotice }, both);

            CollectionAssert.AreEqual(new[] { "이어서 한다." }, SaveText.ContinueNotice(new SaveHeader(), false, Local), "시각 없음");
        }

        [Test]
        public void BrokenNewStartAndFixedNotices()
        {
            Assert.AreEqual("저장을 읽지 못해 새로 시작했다. 원래 파일은 slot1-broken-20261007-213012.txt로 남겼다.",
                SaveText.BrokenNewStart("slot1-broken-20261007-213012.txt"));
            Assert.AreEqual("저장을 읽지 못해 새로 시작했다. 원래 파일은 slot1-broken-20261007-213012-bak.txt로 남겼다.",
                SaveText.BrokenNewStart(System.IO.Path.Combine("save", "slot1-broken-20261007-213012-bak.txt")), "경로면 이름만");
            Assert.AreEqual("저장을 읽지 못해 새로 시작했다. 원래 파일은 예전 저장으로 남겼다.", SaveText.BrokenNewStart("예전 저장"), "받침 있으면 '으로'");

            Assert.AreEqual("저장하지 못했다 — 다음 저장 때 다시 해 본다.", SaveText.SaveFailed);
            Assert.AreEqual("F1 시험 손잡이를 쓴 판이라 이제 저장하지 않는다.", SaveText.NotSavingDevPanel);
        }
    }
}
