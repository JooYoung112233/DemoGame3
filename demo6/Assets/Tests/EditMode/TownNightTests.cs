using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 기획/마을-의뢰-첫판.md 9-3 TownNightTests(1-7 표): 원정·밤 사건별 카드 글과 초, 원정 번호·밤 사건을 마을에서 바꾸지 않음(11장 위험 4).
    /// </summary>
    public sealed class TownNightTests
    {
        [Test]
        public void AscendCard()
        {
            CollectionAssert.AreEqual(new[] { "바구니가 덜컹이며 올라간다." }, TownNight.AscendLines());
            Assert.AreEqual(1.4f, TownNight.AscendSeconds);
        }

        [Test]
        public void FirstDepartureHasNoNight()
        {
            var c = CarryData.NewProfile(1UL);
            var d = TownNight.Departure(c);
            CollectionAssert.AreEqual(new[] { "바구니가 덜컹이며 내려간다." }, d.Lines);
            Assert.AreEqual(1.6f, d.Seconds);
            Assert.IsFalse(d.HasNight);
            Assert.IsTrue(d.FirstStart);
        }

        [Test]
        public void FirstNightAfterFirstReturn()
        {
            var c = CarryData.NewProfile(1UL);
            TownNight.AdvanceForAscend(c);
            Assert.AreEqual(2, c.Expedition);
            Assert.AreEqual(NightEvent.FirstNight, c.Night);
            var d = TownNight.Departure(c);
            CollectionAssert.AreEqual(new[] { "그날 밤, 아홉 해 만에 갱도가 울렸다.", "새벽, 바구니가 덜컹이며 내려간다." }, d.Lines);
            Assert.AreEqual(2.5f, d.Seconds);
            Assert.IsTrue(d.HasNight);
            Assert.IsFalse(d.FirstStart);
        }

        [TestCase(NightEvent.Collapse, "새벽까지 흙 쏟아지는 소리가 길게 났다.")]
        [TestCase(NightEvent.RatBurrow, "아래서 긁는 소리가 그치지 않았다.")]
        [TestCase(NightEvent.Upheaval, "크게 울린 밤이다. 묻혔던 것이 올라왔을지 모른다.")]
        [TestCase(NightEvent.Rumble, "땅 밑에서 큰 것이 몸을 뒤척였다.")]
        public void LaterNightsUseEventLine(NightEvent night, string line)
        {
            var c = CarryData.NewProfile(1UL);
            c.Expedition = 5;
            c.Night = night;
            var d = TownNight.Departure(c);
            CollectionAssert.AreEqual(new[] { "밤새 갱도가 울렸다.", line }, d.Lines, "매판 1차 2-2 글 그대로");
            Assert.AreEqual(2.5f, d.Seconds);
            Assert.IsTrue(d.HasNight);
            Assert.IsFalse(d.FirstStart);
            Assert.AreEqual(line, TownNight.EventLine(night));
        }

        [Test]
        public void AdvanceForAscendMatchesSeeds()
        {
            var c = CarryData.NewProfile(0xC0FFEEUL);
            for (int i = 0; i < 6; i++)
            {
                int before = c.Expedition;
                TownNight.AdvanceForAscend(c);
                Assert.AreEqual(before + 1, c.Expedition, "올라가기 한 번에 +1");
                Assert.AreEqual(ExpeditionSeeds.NightBefore(c.ProfileSalt, c.Expedition), c.Night);
                Assert.AreEqual(ExpeditionSeeds.NightBefore(c.ProfileSalt, c.Expedition - 1), TownNight.LastNight(c), "지난밤");
            }
        }

        /// <summary>
        /// 1-2층 탐험 맛 1차 10장 시험 14(4-9): 원정 3부터 밤 사건 넷(무너짐·새 쥐굴·드러남·거센 울림)이 고르게 나온다.
        /// 원정 3~402(400번)에서 넷이 각각 20~30%. 원정 1은 밤 없음, 원정 2는 첫 귀환의 밤 그대로.
        /// 400번의 ±5%p는 약 ±2.3 표준편차라 아무 소금이나 넣으면 가끔 벗어난다. 그래서 소금을 고정했고, 이 넷은 미리 셈해 본 값이다.
        /// </summary>
        [TestCase(1UL)]
        [TestCase(0xC0FFEEUL)]
        [TestCase(0x5EED_1234UL)]
        [TestCase(0xABCDEFUL)]
        public void LaterNightsSpreadOverFourEvents(ulong salt)
        {
            Assert.AreEqual(NightEvent.None, ExpeditionSeeds.NightBefore(salt, 1), "원정 1은 밤 없음");
            Assert.AreEqual(NightEvent.FirstNight, ExpeditionSeeds.NightBefore(salt, 2), "원정 2는 첫 귀환의 밤");
            var events = new[] { NightEvent.Collapse, NightEvent.RatBurrow, NightEvent.Upheaval, NightEvent.Rumble };
            var counts = new int[events.Length];
            const int first = 3, last = 402, total = last - first + 1;
            for (int expedition = first; expedition <= last; expedition++)
            {
                int i = System.Array.IndexOf(events, ExpeditionSeeds.NightBefore(salt, expedition));
                Assert.GreaterOrEqual(i, 0, "원정 " + expedition + ": 원정 3부터는 네 가지 가운데 하나");
                counts[i]++;
            }
            for (int i = 0; i < events.Length; i++)
            {
                string at = $"소금 {salt}: {events[i]} {counts[i]}/{total}";
                Assert.GreaterOrEqual(counts[i] * 100, 20 * total, at);
                Assert.LessOrEqual(counts[i] * 100, 30 * total, at);
            }
        }

        [Test]
        public void TownNeverChangesExpeditionOrNight()
        {
            var c = CarryData.NewProfile(0x55UL);
            TownArrivalRules.Apply(c, TownArrivalKind.NewPlay);
            TalkDirector.Skip(TownScript.Opening, 0, c);
            TownNight.AdvanceForAscend(c);
            TownNight.AdvanceForAscend(c);
            int expedition = c.Expedition;
            var night = c.Night;

            TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            foreach (var npc in NpcTable.All)
                foreach (var s in TalkDirector.Build(npc.Id, c).Scenes)
                    TalkDirector.Skip(s, 0, c);
            string before = c.ToText();
            TownNight.Departure(c);
            Assert.AreEqual(before, c.ToText(), "카드 고르기는 꾸러미를 바꾸지 않음");
            int departures = TownSave.Departures(c);
            var d = TownNight.Depart(c);
            Assert.AreEqual(departures + 1, TownSave.Departures(c), "출발은 town.departures만 +1");
            Assert.AreEqual(expedition, c.Expedition, "원정 번호 그대로");
            Assert.AreEqual(night, c.Night, "밤 사건 그대로");
            Assert.IsTrue(d.HasNight);
        }
    }
}
