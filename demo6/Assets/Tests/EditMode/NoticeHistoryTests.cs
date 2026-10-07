using Demo6.Core.Save;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 지난 알림 목록(기획/저장-처음화면-멈춤창-1차.md 7장, 시험 12-5의 5): 40줄 넘으면 가장 오래된 줄이 빠짐, 같은 줄·같은 곳이 이어 오면 합치고 수 +1,
    /// 곳이 다르면 따로, 빈 글·공백 무시, 비우기, 한 줄 모양 '[2층] 글 ×3'.
    /// </summary>
    public sealed class NoticeHistoryTests
    {
        [Test]
        public void FortyFirstLineDropsOldest()
        {
            var h = new NoticeHistory();
            Assert.AreEqual(40, NoticeHistory.Capacity);
            for (int i = 1; i <= 40; i++) h.Add("알림 " + i, "1층");
            Assert.AreEqual(40, h.Entries.Count);
            Assert.AreEqual("알림 1", h.Entries[0].Text);

            h.Add("알림 41", "1층");
            Assert.AreEqual(40, h.Entries.Count, "40줄까지");
            Assert.AreEqual("알림 2", h.Entries[0].Text, "가장 오래된 줄이 빠짐");
            Assert.AreEqual("알림 41", h.Entries[39].Text, "맨 끝이 가장 최근");
        }

        [Test]
        public void SameLineSamePlaceMerges()
        {
            var h = new NoticeHistory();
            h.Add("문이 잠겨 있다", "2층");
            h.Add("문이 잠겨 있다", "2층");
            h.Add("  문이 잠겨 있다 ", "2층");
            Assert.AreEqual(1, h.Entries.Count, "바로 이어 오면 한 줄");
            Assert.AreEqual(3, h.Entries[0].Count);
            Assert.AreEqual("[2층] 문이 잠겨 있다 ×3", h.Entries[0].Line());
        }

        [Test]
        public void SameLineAfterOtherLineDoesNotMerge()
        {
            var h = new NoticeHistory();
            h.Add("가", "마을");
            h.Add("나", "마을");
            h.Add("가", "마을");
            Assert.AreEqual(3, h.Entries.Count, "바로 이어 오지 않으면 따로");
            Assert.AreEqual(1, h.Entries[2].Count);
        }

        [Test]
        public void DifferentPlaceDoesNotMerge()
        {
            var h = new NoticeHistory();
            h.Add("레벨 5에 올랐다", "1층");
            h.Add("레벨 5에 올랐다", "2층");
            h.Add("레벨 5에 올랐다", null);
            Assert.AreEqual(3, h.Entries.Count, "곳이 다르면 따로");
            Assert.AreEqual("[1층] 레벨 5에 올랐다", h.Entries[0].Line());
            Assert.AreEqual("[2층] 레벨 5에 올랐다", h.Entries[1].Line());
            Assert.AreEqual("레벨 5에 올랐다", h.Entries[2].Line(), "곳이 없으면 '[ ]' 없음");
        }

        [Test]
        public void EmptyOrBlankTextIgnored()
        {
            var h = new NoticeHistory();
            h.Add(null, "1층");
            h.Add("", "1층");
            h.Add("   ", "1층");
            h.Add("\n\t", "마을");
            Assert.AreEqual(0, h.Entries.Count);
            Assert.AreEqual(0, h.Revision, "무시한 줄은 판을 바꾸지 않음");
        }

        [Test]
        public void BlankPlaceMeansNoPlace()
        {
            var h = new NoticeHistory();
            h.Add("글", "");
            h.Add("글", "  ");
            h.Add("글", null);
            Assert.AreEqual(1, h.Entries.Count, "빈 곳·공백 곳·없음은 같은 '곳 없음'");
            Assert.IsNull(h.Entries[0].Place);
            Assert.AreEqual("글 ×3", h.Entries[0].Line());
        }

        [Test]
        public void ClearEmptiesAndBumpsRevision()
        {
            var h = new NoticeHistory();
            h.Add("가", "굴");
            h.Add("가", "굴");
            int before = h.Revision;
            Assert.AreEqual(2, before, "새 줄·합침마다 판 +1");
            h.Clear();
            Assert.AreEqual(0, h.Entries.Count);
            Assert.AreEqual(before + 1, h.Revision);
            h.Add("나", "시험장");
            Assert.AreEqual(1, h.Entries.Count);
            Assert.AreEqual("[시험장] 나", h.Entries[0].Line());
        }

        [Test]
        public void LineShape()
        {
            Assert.AreEqual("[2층] 글 ×3", new NoticeEntry("글", "2층", 3).Line());
            Assert.AreEqual("[마을] 글", new NoticeEntry("글", "마을", 1).Line(), "한 번이면 ×1을 붙이지 않음");
            Assert.AreEqual("글 ×2", new NoticeEntry("글", null, 2).Line());
            Assert.AreEqual("글", new NoticeEntry("글", "", 1).Line());
        }

        /// <summary>발견 알림 글(물결 4 반박 검토): 던전 HUD 아래 알림과 같은 글, 조용한 지역 이름·층 완전 탐험은 담지 않음(null).</summary>
        [Test]
        public void DiscoveryLinesMatchHudAndSkipQuietKinds()
        {
            Assert.IsNull(DiscoveryNoticeText.Line(Demo6.Core.Dungeon.DiscoveryKind.NewCell, "무너진 갱도"), "지역 이름은 담지 않음");
            Assert.IsNull(DiscoveryNoticeText.Line(Demo6.Core.Dungeon.DiscoveryKind.FloorComplete, null), "층 완전 탐험은 담지 않음");
            Assert.AreEqual("손에 넣었다 · 곡괭이", DiscoveryNoticeText.Line(Demo6.Core.Dungeon.DiscoveryKind.Ability, "곡괭이"));
            Assert.AreEqual("숨은 방", DiscoveryNoticeText.Line(Demo6.Core.Dungeon.DiscoveryKind.HiddenRoom, "아무 이름"));
            Assert.AreEqual("지름길", DiscoveryNoticeText.Line(Demo6.Core.Dungeon.DiscoveryKind.Shortcut, null));
            Assert.AreEqual("나무 궤짝 — 삐걱이며 열렸다", DiscoveryNoticeText.Line(Demo6.Core.Dungeon.DiscoveryKind.WoodChest, null), "이름이 없으면 갈래 이름");
            Assert.AreEqual("금고", DiscoveryNoticeText.Line(Demo6.Core.Dungeon.DiscoveryKind.Safe, ""));
            foreach (Demo6.Core.Dungeon.DiscoveryKind kind in System.Enum.GetValues(typeof(Demo6.Core.Dungeon.DiscoveryKind)))
            {
                if (kind == Demo6.Core.Dungeon.DiscoveryKind.NewCell || kind == Demo6.Core.Dungeon.DiscoveryKind.FloorComplete) continue;
                Assert.IsFalse(string.IsNullOrEmpty(DiscoveryNoticeText.Line(kind, null)), kind.ToString());
            }
        }
    }
}
