using System.Linq;
using Demo6.Core.Dungeon;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 문틈 엿듣기 글(기획/1-2층-탐험-맛-1차.md 4-4, 10장 시험 11): 빈 칸은 '조용하다', 위험한 차례(정예 먼저), 두 마디까지, 숫자 없음.
    /// </summary>
    public sealed class DoorListenRulesTests
    {
        [Test]
        public void NumbersFromDesign()
        {
            Assert.AreEqual(1.0f, DoorListenRules.HoldSeconds);
            Assert.AreEqual(2.4f, DoorListenRules.Range);
            Assert.AreEqual(15f, DoorListenRules.Cooldown);
            Assert.AreEqual(0.3f, DoorListenRules.MaxSpeed);
            Assert.AreEqual(2, DoorListenRules.MaxPhrases);
            Assert.AreEqual(3, DoorListenRules.ManyRats);
        }

        [Test]
        public void EmptyIsQuiet()
        {
            var empty = new ListenTally();
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(0, DoorListenRules.Phrases(empty).Count);
            Assert.AreEqual("문틈 너머 — 조용하다", DoorListenRules.Line(empty));
            Assert.AreEqual(ExploreText.ListenQuiet, DoorListenRules.Line(empty));
            Assert.IsFalse(new ListenTally { Nests = 1 }.IsEmpty);
            Assert.IsFalse(new ListenTally { Rats = 1 }.IsEmpty);
        }

        [Test]
        public void EliteComesFirst()
        {
            var t = new ListenTally { Elites = 1, Rats = 2, Archers = 1, Patrolling = 1, Awake = 1, Eating = 3, Nests = 1, Boars = 1 };
            CollectionAssert.AreEqual(new[] { "거친 숨소리(큰 놈)", "숨죽인 기척" }, DoorListenRules.Phrases(t).ToArray());
            Assert.AreEqual("문틈 너머 — 거친 숨소리(큰 놈), 숨죽인 기척", DoorListenRules.Line(t));
        }

        [Test]
        public void OrderFollowsDanger()
        {
            // 차례: 정예 → 깨어 있음 → 순찰 → 궁수 → 돌충이 → 굴쥐 → 둥지 → 먹는 중.
            Assert.AreEqual("문틈 너머 — 오가는 발소리, 껍데기 긁는 소리",
                DoorListenRules.Line(new ListenTally { Boars = 1, Rats = 2, Patrolling = 3 }), "문서 3장 예: 순찰하는 돌충이");
            Assert.AreEqual("문틈 너머 — 시위 삐걱임, 껍데기 긁는 소리",
                DoorListenRules.Line(new ListenTally { Boars = 1, Archers = 1, Rats = 2, Eating = 4 }));
            Assert.AreEqual("문틈 너머 — 껍데기 긁는 소리, 작은 발톱 소리",
                DoorListenRules.Line(new ListenTally { Boars = 1, Rats = 2 }));
            Assert.AreEqual("문틈 너머 — 발톱 소리 여럿, 씹는 소리",
                DoorListenRules.Line(new ListenTally { Rats = 4, Eating = 4 }), "굴쥐 3 이상은 '여럿'");
            Assert.AreEqual("문틈 너머 — 작은 발톱 소리, 흙 속 꿈틀거림",
                DoorListenRules.Line(new ListenTally { Rats = 2, Nests = 1 }));
            Assert.AreEqual("문틈 너머 — 흙 속 꿈틀거림",
                DoorListenRules.Line(new ListenTally { Nests = 1 }), "한 마디면 한 마디만");
            Assert.AreEqual("문틈 너머 — 거친 숨소리(큰 놈), 작은 발톱 소리",
                DoorListenRules.Line(new ListenTally { Elites = 1, Rats = 2 }), "단단한 정예 돌충이 + 굴쥐 2");
            Assert.AreEqual("문틈 너머 — 거친 숨소리(큰 놈), 발톱 소리 여럿",
                DoorListenRules.Line(new ListenTally { Elites = 1, Rats = 3 }), "문서 4-4 예");
            Assert.AreEqual("문틈 너머 — 숨죽인 기척, 오가는 발소리",
                DoorListenRules.Line(new ListenTally { Archers = 1, Rats = 3, Patrolling = 4, Awake = 2 }));
        }

        [Test]
        public void AtMostTwoPhrasesAndNoNumbers()
        {
            var rng = new Pcg32Random(0xD00DUL, 7);
            string[] all =
            {
                ExploreText.PhraseElite, ExploreText.PhraseAwake, ExploreText.PhrasePatrol, ExploreText.PhraseArcher, ExploreText.PhraseBoar,
                ExploreText.PhraseManyRats, ExploreText.PhraseFewRats, ExploreText.PhraseNest, ExploreText.PhraseEating,
            };
            for (int i = 0; i < 2000; i++)
            {
                var t = new ListenTally
                {
                    Rats = rng.NextInt(0, 9),
                    Boars = rng.NextInt(0, 3),
                    Archers = rng.NextInt(0, 3),
                    Elites = rng.NextInt(0, 4) == 0 ? 1 : 0,
                    Nests = rng.NextInt(0, 3) == 0 ? 1 : 0,
                    Eating = rng.NextInt(0, 6),
                    Patrolling = rng.NextInt(0, 4) == 0 ? rng.NextInt(1, 5) : 0,
                    Awake = rng.NextInt(0, 5) == 0 ? rng.NextInt(1, 4) : 0,
                };
                var phrases = DoorListenRules.Phrases(t);
                Assert.LessOrEqual(phrases.Count, DoorListenRules.MaxPhrases, "두 마디까지");
                Assert.AreEqual(t.IsEmpty, phrases.Count == 0);
                CollectionAssert.AllItemsAreUnique(phrases);
                foreach (var p in phrases) CollectionAssert.Contains(all, p);
                string line = DoorListenRules.Line(t);
                StringAssert.StartsWith(ExploreText.ListenHead, line);
                Assert.IsFalse(line.Any(char.IsDigit), "숫자를 쓰지 않음: " + line);
                StringAssert.DoesNotContain("멧돼지", line);
                // 차례가 문서와 같다: 앞 마디가 늘 뒤 마디보다 위험한 쪽.
                for (int k = 1; k < phrases.Count; k++)
                    Assert.Less(Rank(phrases[k - 1]), Rank(phrases[k]), line);
            }
        }

        /// <summary>문서 4-4 차례(굴쥐 두 글은 같은 자리).</summary>
        static int Rank(string phrase)
        {
            switch (phrase)
            {
                case ExploreText.PhraseElite: return 0;
                case ExploreText.PhraseAwake: return 1;
                case ExploreText.PhrasePatrol: return 2;
                case ExploreText.PhraseArcher: return 3;
                case ExploreText.PhraseBoar: return 4;
                case ExploreText.PhraseManyRats:
                case ExploreText.PhraseFewRats: return 5;
                case ExploreText.PhraseNest: return 6;
                case ExploreText.PhraseEating: return 7;
                default: return 99;
            }
        }
    }
}
