using System;
using System.Collections.Generic;
using System.Linq;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 룬 홈(소켓)·룬(기획/세-무기-우클릭-소켓-1차.md 6장, 7-4): 칸 수 표(부위 + 등급, 무기 9종 같음), 굴림은 난수 늘 2번, 확률 표(‰),
    /// 같은 룬 두 번 금지, 무기 아닌 부위 0칸, 카드 룬 홈 줄(SocketLine), 묶음에 붙이기(AddTo), 주머니 상한, 효과 범위(Works), 장비가 룬을 들고 다님.
    /// </summary>
    public sealed class RuneRulesTests
    {
        const string Sa = RuneTable.SuperArmorId;

        /// <summary>정해 둔 값을 차례로 돌려주고 부른 횟수를 센다.</summary>
        sealed class ScriptedRandom : IRandom
        {
            readonly int[] _values;
            int _next;
            public int Calls;

            public ScriptedRandom(params int[] values) => _values = values;

            public double NextDouble()
            {
                Calls++;
                return 0.0;
            }

            public int NextInt(int min, int max)
            {
                Calls++;
                int v = _values.Length > 0 ? _values[Math.Min(_next, _values.Length - 1)] : min;
                _next++;
                return Math.Max(min, Math.Min(max - 1, v));
            }
        }

        static readonly RuneSource[] Sources = (RuneSource[])Enum.GetValues(typeof(RuneSource));
        static readonly Grade[] Grades = (Grade[])Enum.GetValues(typeof(Grade));

        [Test]
        public void SocketCountByPartAndGrade()
        {
            foreach (GearPart part in Enum.GetValues(typeof(GearPart)))
                foreach (var grade in Grades)
                {
                    int expected = part != GearPart.Weapon ? 0 : grade >= Grade.Epic ? 2 : 1;
                    Assert.AreEqual(expected, RuneRules.SocketCount(part, grade), part + " " + grade);
                }
            Assert.AreEqual(1, RuneRules.SocketCount(GearPart.Weapon, Grade.Common), "시작 무기(일반)도 1칸");
            Assert.AreEqual(1, RuneRules.SocketCount(GearPart.Weapon, Grade.Rare));
            Assert.AreEqual(2, RuneRules.SocketCount(GearPart.Weapon, Grade.Epic));
            Assert.AreEqual(2, RuneRules.SocketCount(GearPart.Weapon, Grade.Legendary));
            Assert.AreEqual(RuneRules.MaxSockets, RuneRules.SocketCount(GearPart.Weapon, Grade.Legendary));

            // 무기 9종 모두 같다(종류 무관) → 종류를 바꿔도(WithBase) 룬이 넘치지 않는다.
            foreach (var b in GearBaseTable.ForPart(GearPart.Weapon))
                foreach (var grade in Grades)
                    Assert.AreEqual(RuneRules.SocketCount(GearPart.Weapon, grade), new GearItem(b.Id, grade, 3, 1000).SocketCount, b.Id + " " + grade);
            Assert.AreEqual(1, GearItem.Starting(GearSlot.Weapon).SocketCount, "시작 장검 1칸");
            Assert.AreEqual(0, GearItem.Starting(GearSlot.Armor).SocketCount);
        }

        [Test]
        public void RollAlwaysUsesTwoRandomNumbers()
        {
            foreach (var source in Sources)
                foreach (int first in new[] { 0, 2, 3, 299, 300, 999 })
                {
                    var rng = new ScriptedRandom(first, 0);
                    RuneRules.Roll(source, rng);
                    Assert.AreEqual(2, rng.Calls, source + " 첫 값 " + first);
                    var add = new ScriptedRandom(first, 0);
                    RuneRules.AddTo(null, source, add);
                    Assert.AreEqual(2, add.Calls, "AddTo(묶음 null)도 2번 " + source);
                }

            // 같은 씨앗: 한 번 굴린 뒤의 다음 값 = 새 난수에서 2번 쓴 뒤의 다음 값(나오든 안 나오든 소모가 같음).
            foreach (var source in Sources)
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var a = new Pcg32Random(seed, RuneRules.KillStream);
                    var b = new Pcg32Random(seed, RuneRules.KillStream);
                    RuneRules.Roll(source, a);
                    b.NextInt(0, 1000);
                    b.NextInt(0, 1000);
                    Assert.AreEqual(b.NextUInt(), a.NextUInt(), source + " 씨앗 " + seed);
                }
        }

        [Test]
        public void DropPermilleTableMatchesDoc()
        {
            var expected = new Dictionary<RuneSource, int>
            {
                { RuneSource.Rat, 3 },
                { RuneSource.Archer, 15 },
                { RuneSource.Boar, 25 },
                { RuneSource.Elite, 150 },
                { RuneSource.NestClear, 80 },
                { RuneSource.WoodChest, 15 },
                { RuneSource.IronChest, 100 },
                { RuneSource.Safe, 500 },
                { RuneSource.BossFirst, 1000 },
                { RuneSource.BossRepeat, 300 },
            };
            Assert.AreEqual(Sources.Length, expected.Count, "표에 모든 출처");
            foreach (var kv in expected)
            {
                Assert.AreEqual(kv.Value, RuneRules.DropPermille(kv.Key), kv.Key.ToString());
                // 첫 난수 < ‰이면 나오고, ‰ 이상이면 안 나온다(경계).
                if (kv.Value > 0) Assert.AreEqual(Sa, RuneRules.Roll(kv.Key, new ScriptedRandom(kv.Value - 1, 0)), kv.Key + " 경계 안");
                if (kv.Value < 1000) Assert.IsNull(RuneRules.Roll(kv.Key, new ScriptedRandom(kv.Value, 0)), kv.Key + " 경계 밖");
            }
            for (int v = 0; v < 1000; v += 37) Assert.AreEqual(Sa, RuneRules.Roll(RuneSource.BossFirst, new ScriptedRandom(v, 0)), "오우거 첫 처치는 확정");

            // 실제 난수로 어림(10만 번): 금고 50%·쇠 궤짝 10%·굴쥐 0.3% 근처.
            var rng = new Pcg32Random(12345UL, RuneRules.ChestStream);
            int safe = 0, iron = 0, rat = 0;
            const int n = 100000;
            for (int i = 0; i < n; i++)
            {
                if (RuneRules.Roll(RuneSource.Safe, rng) != null) safe++;
                if (RuneRules.Roll(RuneSource.IronChest, rng) != null) iron++;
                if (RuneRules.Roll(RuneSource.Rat, rng) != null) rat++;
            }
            Assert.AreEqual(500.0, safe * 1000.0 / n, 8.0, "금고 500‰");
            Assert.AreEqual(100.0, iron * 1000.0 / n, 4.0, "쇠 궤짝 100‰");
            Assert.AreEqual(3.0, rat * 1000.0 / n, 0.8, "굴쥐 3‰");
            Assert.AreEqual(RuneRules.ChestStream, 43UL);
            Assert.AreEqual(RuneRules.KillStream, 29UL);
            Assert.AreEqual(RuneRules.BossStream, 47UL);
        }

        [Test]
        public void KillSourcesMapToRuneSources()
        {
            Assert.AreEqual(RuneSource.Rat, RuneRules.SourceOf(KillSource.Rat));
            Assert.AreEqual(RuneSource.Archer, RuneRules.SourceOf(KillSource.Archer));
            Assert.AreEqual(RuneSource.Boar, RuneRules.SourceOf(KillSource.Boar));
            Assert.AreEqual(RuneSource.NestClear, RuneRules.SourceOf(KillSource.NestClear));
            Assert.AreEqual(RuneSource.Elite, RuneRules.SourceOf(KillSource.Elite));
        }

        [Test]
        public void SameRuneCannotBeInsertedTwice()
        {
            Assert.IsTrue(RuneRules.CanInsert(GearPart.Weapon, Grade.Epic, null, Sa));
            Assert.IsTrue(RuneRules.CanInsert(GearPart.Weapon, Grade.Epic, new string[0], Sa));
            Assert.IsFalse(RuneRules.CanInsert(GearPart.Weapon, Grade.Epic, new[] { Sa }, Sa), "영웅 2칸이어도 같은 룬은 하나만");
            Assert.IsFalse(RuneRules.CanInsert(GearPart.Weapon, Grade.Common, new[] { Sa }, Sa), "빈 홈 없음");
            Assert.IsFalse(RuneRules.CanInsert(GearPart.Weapon, Grade.Epic, null, "rune_unknown"), "모르는 룬");
            Assert.IsFalse(RuneRules.CanInsert(GearPart.Weapon, Grade.Epic, null, null));

            var epic = new GearItem(GearBaseTable.Greatsword, Grade.Epic, 3, 1000, runes: new[] { Sa, Sa, "rune_unknown", "" });
            CollectionAssert.AreEqual(new[] { Sa }, epic.Runes.ToArray(), "같은 룬 두 번·모르는 id는 거름");
            Assert.AreEqual(1, epic.FreeSockets);
            Assert.IsTrue(epic.HasRune(Sa));
            Assert.IsFalse(epic.HasRune(null));

            var kept = new List<string>();
            var overflow = new List<string>();
            Assert.AreEqual(1, RuneRules.Fit(GearPart.Weapon, Grade.Common, new[] { "rune_unknown", Sa, Sa }, kept, overflow));
            CollectionAssert.AreEqual(new[] { Sa }, kept);
            CollectionAssert.AreEqual(new[] { Sa }, overflow, "아는 룬인데 못 들어간 것만 넘침(모르는 id는 버림)");
        }

        [Test]
        public void NonWeaponPartsHaveNoSockets()
        {
            foreach (GearPart part in Enum.GetValues(typeof(GearPart)))
            {
                if (part == GearPart.Weapon) continue;
                Assert.IsFalse(RuneRules.CanInsert(part, Grade.Legendary, null, Sa), part.ToString());
                var b = GearBaseTable.FirstOf(part);
                var item = new GearItem(b.Id, Grade.Legendary, 5, 1000, runes: new[] { Sa });
                Assert.AreEqual(0, item.SocketCount, b.Id);
                Assert.AreEqual(0, item.Runes.Count, b.Id + ": 홈이 없어 룬을 거름");
                Assert.IsNull(GearNaming.SocketLine(item), b.Id + ": 카드 줄 없음");
                Assert.AreEqual("", GearNaming.SocketMarks(item));
                var overflow = new List<string>();
                RuneRules.Fit(part, Grade.Legendary, new[] { Sa }, new List<string>(), overflow);
                CollectionAssert.AreEqual(new[] { Sa }, overflow, b.Id + ": 넘침(주머니로)");
            }
        }

        [Test]
        public void SocketLineAndMarks()
        {
            var plain = new GearItem(GearBaseTable.Longsword, Grade.Common, 1, 1000);
            Assert.AreEqual("룬 홈 ◇ · 비어 있음", GearNaming.SocketLine(plain));
            Assert.AreEqual("◇", GearNaming.SocketMarks(plain));
            var one = plain.WithRunes(new[] { Sa });
            Assert.AreEqual("룬 홈 ◆ · 버팀 룬", GearNaming.SocketLine(one));
            var epic = new GearItem(GearBaseTable.Greatsword, Grade.Epic, 3, 1000, runes: new[] { Sa });
            Assert.AreEqual("룬 홈 ◆◇ · 버팀 룬", GearNaming.SocketLine(epic));
            Assert.AreEqual("◆◇", GearNaming.SocketMarks(epic));
            Assert.AreEqual("룬 홈 ◇◇ · 비어 있음", GearNaming.SocketLine(epic.WithRunes(null)));
            Assert.IsNull(GearNaming.SocketLine(null));
            Assert.AreEqual("희귀 대검", new GearItem(GearBaseTable.Greatsword, Grade.Rare, 2, 1000, runes: new[] { Sa }).DisplayName, "이름에는 룬을 붙이지 않음");
            CollectionAssert.DoesNotContain(GearNaming.CardLines(epic), GearNaming.SocketLine(epic), "CardLines는 그대로(룬 줄은 가방 카드가 따로 넣음)");
        }

        [Test]
        public void AddToAppendsRuneToBundle()
        {
            var bundle = new LootBundle();
            Assert.IsTrue(bundle.Empty);
            Assert.IsNull(RuneRules.AddTo(bundle, RuneSource.Rat, new ScriptedRandom(3, 0)), "굴쥐 3‰: 3이면 안 나옴");
            Assert.AreEqual(0, bundle.Runes.Count);
            Assert.IsTrue(bundle.Empty);
            Assert.AreEqual(Sa, RuneRules.AddTo(bundle, RuneSource.Rat, new ScriptedRandom(2, 0)));
            CollectionAssert.AreEqual(new[] { Sa }, bundle.Runes);
            Assert.IsFalse(bundle.Empty, "룬만 있어도 빈 묶음이 아님");
            RuneRules.AddTo(bundle, RuneSource.BossFirst, new ScriptedRandom(999, 0));
            Assert.AreEqual(2, bundle.Runes.Count, "붙인 차례로 쌓임");

            // 장비 굴림 함수는 룬을 넣지 않는다(기존 난수 차례 그대로).
            var kill = LootRules.RollKill(KillSource.Elite, 2, new Pcg32Random(7UL, 23UL));
            var chest = LootRules.RollChest(true, 1, "", new Pcg32Random(7UL, 41UL));
            var safe = LootRules.RollSafe(2, new Pcg32Random(7UL, 41UL));
            Assert.AreEqual(0, kill.Runes.Count + chest.Runes.Count + safe.Runes.Count);
        }

        [Test]
        public void PouchCapsAtNinetyNine()
        {
            var pouch = new Dictionary<string, int>();
            Assert.AreEqual(3, RuneRules.PouchAdd(pouch, Sa, 3));
            Assert.AreEqual(0, RuneRules.PouchAdd(pouch, "rune_unknown", 5), "모르는 룬은 안 들어감");
            Assert.AreEqual(0, RuneRules.PouchAdd(pouch, Sa, 0));
            Assert.AreEqual(0, RuneRules.PouchAdd(pouch, Sa, -4));
            Assert.IsFalse(pouch.ContainsKey("rune_unknown"));
            Assert.AreEqual(RuneRules.PouchCap - 3, RuneRules.PouchAdd(pouch, Sa, 500), "99까지만");
            Assert.AreEqual(99, pouch[Sa]);
            Assert.AreEqual(0, RuneRules.PouchAdd(pouch, Sa), "가득");
            Assert.IsTrue(RuneRules.PouchTake(pouch, Sa));
            Assert.AreEqual(98, pouch[Sa]);
            var one = new Dictionary<string, int> { { Sa, 1 } };
            Assert.IsTrue(RuneRules.PouchTake(one, Sa));
            Assert.IsFalse(one.ContainsKey(Sa), "0이 되면 줄을 지움");
            Assert.IsFalse(RuneRules.PouchTake(one, Sa), "없으면 못 뺌");
        }

        [Test]
        public void SuperArmorRuneWorksOnlyOnGreatsword()
        {
            foreach (var b in GearBaseTable.ForPart(GearPart.Weapon))
            {
                bool charge = WeaponActRules.KindOf(b.Id) == WeaponActKind.Charge;
                bool anyAct = WeaponActRules.KindOf(b.Id) != WeaponActKind.None;
                Assert.AreEqual(charge, RuneRules.Works(Sa, b.Id), b.Id);
                Assert.AreEqual(anyAct, RuneRules.Works(Sa, b.Id, true), b.Id + " 세 행동 모두 버팀(시험 손잡이)");
            }
            Assert.IsTrue(RuneRules.Works(Sa, GearBaseTable.Greatsword));
            Assert.IsFalse(RuneRules.Works(Sa, GearBaseTable.Longsword));
            Assert.IsFalse(RuneRules.Works("rune_unknown", GearBaseTable.Greatsword));
        }

        [Test]
        public void GearCarriesRunesThroughCopies()
        {
            var g = new GearItem(GearBaseTable.Greatsword, Grade.Rare, 2, 1050, 1, null, null, 0, new[] { Sa });
            CollectionAssert.AreEqual(new[] { Sa }, g.WithEnhance(3).Runes.ToArray(), "강화해도 룬 그대로");
            var swapped = g.WithBase(GearBaseTable.Twinblades);
            Assert.AreEqual(GearBaseTable.Twinblades, swapped.BaseId);
            CollectionAssert.AreEqual(new[] { Sa }, swapped.Runes.ToArray(), "종류를 바꿔도 룬 그대로");
            Assert.AreSame(g, g.WithBase(GearBaseTable.PlateArmor), "부위가 다르면 그대로");
            Assert.AreEqual(0, g.WithRunes(null).Runes.Count);
            Assert.AreEqual(g.Attack, g.WithRunes(null).Attack, "룬은 능력치에 들지 않음");
            Assert.AreEqual(GearMath.ItemScore(g), GearMath.ItemScore(g.WithRunes(null)), "장비 점수에도 들지 않음");
            StringAssert.Contains("◆버팀 룬", g.ToString());
            StringAssert.DoesNotContain("◆", g.WithRunes(null).ToString(), "룬 없는 글은 그대로");
            Assert.AreEqual(0, new GearItem(GearBaseTable.Greatsword, Grade.Rare, 2, 1050).Runes.Count, "기본 빈 홈");
        }

        [Test]
        public void RuneTableContract()
        {
            Assert.AreEqual("rune_superarmor", Sa);
            var def = RuneTable.Get(Sa);
            Assert.AreSame(RuneTable.SuperArmor, def);
            Assert.AreEqual("버팀 룬", def.Name);
            Assert.AreEqual("버팀", def.ShortName);
            Assert.AreEqual(0x5FD6C8, def.Rgb);
            Assert.AreEqual(RuneEffect.SuperArmor, def.Effect);
            Assert.IsNull(RuneTable.Get("rune_unknown"));
            Assert.IsNull(RuneTable.Get(null));
            Assert.IsTrue(RuneTable.Has(new[] { "x", Sa }, RuneEffect.SuperArmor));
            Assert.IsFalse(RuneTable.Has(null, RuneEffect.SuperArmor));
            Assert.AreEqual(99, RuneRules.PouchCap);
            Assert.AreEqual(2, RuneRules.MaxSockets);
            StringAssert.DoesNotContain("소켓", def.Name + def.EffectLine, "게임 안 말은 '룬 홈'");
        }
    }
}
