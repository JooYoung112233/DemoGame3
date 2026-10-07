using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.TestStart;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 시험 도구 규칙(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q7, 기획/키-배치-1차.md 정정판): F1 시험 패널은 편집기에서만 열린다(0장 5),
    /// 숫자키 무기 손잡이를 뺀 뒤에도 옛 설정 글(weaponkeys 줄)이 오류 없이 읽힌다(0장 2), '시험 판' 표시(플레이를 넘어 남는 시험 값 포함),
    /// 정식 새 판과 같은 시작, 마을 안내 줄에 F1 없음.
    /// </summary>
    public sealed class TestToolRulesTests
    {
        // ── 숫자키 무기 손잡이 빼기(키 배치 1차 0장 2) ──

        [Test]
        public void OldWeaponKeysLineIsReadAndDropped()
        {
            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nweaponkeys=1\n", out var old), "옛 설정 글이 오류 없이 읽힘");
            Assert.IsNotNull(old);
            Assert.IsFalse(old.UsesKnobs, "옛 weaponkeys 줄은 손잡이로 치지 않음");
            Assert.IsTrue(old.IsPlainStart, "다른 손잡이가 없으면 정식 새 판과 같은 시작");
            StringAssert.DoesNotContain("weaponkeys", old.ToText(), "다시 쓴 글에 weaponkeys 줄 없음");
            Assert.AreEqual(new TestStartPreset().ToText(), old.ToText(), "기본 설정과 같은 글");
            StringAssert.DoesNotContain("weaponkeys", new TestStartPreset().ToText());

            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nweaponkeys=1\nlevel=3\ninvincible=1\n", out var mixed));
            Assert.AreEqual(3, mixed.Level, "weaponkeys 뒤 줄도 그대로 읽음");
            Assert.IsTrue(mixed.Invincible);
            StringAssert.DoesNotContain("weaponkeys", mixed.ToText());
            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nweaponkeys=maybe\n", out var bad), "읽지 못하는 값이어도 오류 없음");
            Assert.IsTrue(bad.IsPlainStart);
        }

        [Test]
        public void SummaryHasNoNumberKeys()
        {
            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nstart=Floor1\nweaponkeys=1\n", out var dungeon));
            Assert.AreEqual(TestStartAt.Floor1, dungeon.StartAt);
            StringAssert.DoesNotContain("숫자키", TestStartBuilder.Build(dungeon, 1UL).Summary());
            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nstart=CombatTest\nweaponkeys=1\n", out var combat));
            Assert.AreEqual(TestStartAt.CombatTest, combat.StartAt);
            StringAssert.DoesNotContain("숫자키", TestStartBuilder.Build(combat, 1UL).Summary());
            StringAssert.DoesNotContain("숫자키", TestStartBuilder.Build(new TestStartPreset(), 1UL).Summary());
        }

        // ── Q7 F1 시험 패널·시험 판 ──

        [Test]
        public void PanelAllowedOnlyInEditor()
        {
            Assert.IsTrue(TestToolRules.PanelAllowed(isEditor: true), "편집기에서는 연다");
            Assert.IsFalse(TestToolRules.PanelAllowed(isEditor: false), "만든 게임(개발용 빌드 포함)에서는 F1이 아무 일도 하지 않음(키 배치 1차 0장 5)");
        }

        [Test]
        public void TestRunMarkCollectsReasons()
        {
            Assert.AreEqual(1, (int)TestRunReason.Launcher, "남은 까닭의 숫자는 그대로");
            Assert.AreEqual(2, (int)TestRunReason.DevPanel);
            Assert.AreEqual(8, (int)TestRunReason.TestTuning);

            var mark = new TestRunMark();
            Assert.IsFalse(mark.Any);
            Assert.AreEqual("", mark.Label());
            Assert.IsFalse(mark.Add(TestRunReason.None), "None은 아무것도 붙이지 않음");
            Assert.IsFalse(mark.Any);

            Assert.IsTrue(mark.Add(TestRunReason.DevPanel));
            Assert.IsFalse(mark.Add(TestRunReason.DevPanel), "같은 까닭은 한 번만");
            Assert.IsTrue(mark.Any);
            Assert.AreEqual("시험 판(F1 시험 패널)", mark.Label());

            Assert.IsFalse(mark.Add((TestRunReason)4), "옛 숫자키 무기 자리(4)는 이제 모르는 비트라 버림(키 배치 1차 0장 2)");
            Assert.IsTrue(mark.Add(TestRunReason.TestTuning));
            Assert.IsTrue(mark.Add(TestRunReason.Launcher));
            Assert.AreEqual("시험 판(시험 메뉴 시작·F1 시험 패널·기본과 다른 시험 값)", mark.Label(), "표시 차례는 붙은 차례와 상관없음");
            Assert.IsTrue(mark.Has(TestRunReason.Launcher | TestRunReason.TestTuning));
            Assert.IsFalse(mark.Has(TestRunReason.None));

            Assert.IsFalse(mark.Add((TestRunReason)64), "모르는 비트는 버림");
            Assert.AreEqual("시험 판(시험 메뉴 시작·F1 시험 패널·기본과 다른 시험 값)", mark.Label());
            mark.Clear();
            Assert.IsFalse(mark.Any);
            Assert.AreEqual(TestRunReason.None, mark.Reasons);
            Assert.AreEqual("", mark.Label());
        }

        // ── Q7 반박 검토: 플레이를 넘어 남는 시험 값 ──

        [Test]
        public void DefaultTestTuningIsNotATestRun()
        {
            var d = TestTuningValues.Defaults;
            Assert.IsFalse(TestToolRules.AnyChangedTestTuning(d), "기본값은 정식 판");
            Assert.AreEqual(0, TestToolRules.ChangedTestTuning(d).Count);
            Assert.AreEqual(ExecutionRule.HealDefaultOn, d.ExecutionHeal);
            Assert.AreEqual(ExecutionRule.ThresholdMedium, d.ThresholdMedium);
            Assert.AreEqual(ExecutionRule.ThresholdHeavy, d.ThresholdHeavy);
            Assert.AreEqual(ExecutionRule.ThresholdElite, d.ThresholdElite);
            Assert.IsFalse(d.Invincible || d.SuperArmorRune || d.SuperArmorAllActs || d.SoftBoar, "시험 켜기 값은 모두 꺼짐");

            var nearly = d;
            nearly.ThresholdMedium += 0.0001f;
            Assert.IsFalse(TestToolRules.AnyChangedTestTuning(nearly), "밀대 반올림 차이는 같은 값");
        }

        [Test]
        public void EachEasyTestValueMarksTheRun()
        {
            var cases = new (string name, TestTuningValues values)[]
            {
                ("무적", With(v => { v.Invincible = true; return v; })),
                ("버팀 룬 시험", With(v => { v.SuperArmorRune = true; return v; })),
                ("세 행동 모두 버팀", With(v => { v.SuperArmorAllActs = true; return v; })),
                ("돌충이 약하게", With(v => { v.SoftBoar = true; return v; })),
                ("처형 회복", With(v => { v.ExecutionHeal = !ExecutionRule.HealDefaultOn; return v; })),
                ("처형 문턱 보통", With(v => { v.ThresholdMedium = 0.45f; return v; })),
                ("처형 문턱 무거움", With(v => { v.ThresholdHeavy = 0.35f; return v; })),
                ("처형 문턱 정예", With(v => { v.ThresholdElite = 0.30f; return v; })),
            };
            foreach (var c in cases)
            {
                Assert.IsTrue(TestToolRules.AnyChangedTestTuning(c.values), c.name);
                CollectionAssert.AreEqual(new[] { c.name }, TestToolRules.ChangedTestTuning(c.values), c.name);
            }

            var several = With(v =>
            {
                v.SoftBoar = true;
                v.Invincible = true;
                v.ThresholdElite = 0f;
                return v;
            });
            CollectionAssert.AreEqual(new[] { "무적", "돌충이 약하게", "처형 문턱 정예" }, TestToolRules.ChangedTestTuning(several), "차례는 고정");
        }

        /// <summary>기본값에서 change로 몇 개만 바꾼 값.</summary>
        static TestTuningValues With(System.Func<TestTuningValues, TestTuningValues> change) => change(TestTuningValues.Defaults);

        [Test]
        public void PlainStartIsOnlyDefaultTownFresh()
        {
            Assert.IsTrue(new TestStartPreset().IsPlainStart, "기본값 = 정식 새 판");
            Assert.IsFalse(new TestStartPreset().UsesKnobs);
            Assert.IsTrue(new TestStartPreset { Level = 1 }.IsPlainStart, "레벨 1은 새 프로필과 같음");
            Assert.IsTrue(new TestStartPreset { AutoSkills = false, GearLevel = 5, ArmorWeight = ArmorWeight.Heavy, CombatFloor = 2 }.IsPlainStart,
                "시작 장비·마을 처음이 쓰지 않는 값은 보지 않음");
            var legend = new TestStartPreset();
            legend.LegendOn[0] = true;
            Assert.IsTrue(legend.IsPlainStart, "시작 장비는 전설을 무시함");

            Assert.IsFalse(new TestStartPreset { StartAt = TestStartAt.Floor1 }.IsPlainStart);
            Assert.IsFalse(new TestStartPreset { StartAt = TestStartAt.Town, Stage = TestQuestStage.ArcherDone }.IsPlainStart);
            Assert.IsFalse(new TestStartPreset { StartAt = TestStartAt.CombatTest }.IsPlainStart);
            Assert.IsFalse(new TestStartPreset { Level = 5 }.IsPlainStart);
            Assert.IsFalse(new TestStartPreset { WeaponId = GearBaseTable.Axe }.IsPlainStart);
            Assert.IsFalse(new TestStartPreset { GearSet = TestGearSet.Rare }.IsPlainStart);

            var knobs = new[]
            {
                new TestStartPreset { Invincible = true },
                new TestStartPreset { NoEnemies = true },
                new TestStartPreset { DarknessOff = true },
                new TestStartPreset { VisionOff = true },
                new TestStartPreset { FixSeed = true },
                new TestStartPreset { AddStones = 10 },
                new TestStartPreset { AddGold = 10 },
                new TestStartPreset { AddRunes = 1 },
                new TestStartPreset { StartWeaponRune = true },
            };
            for (int i = 0; i < knobs.Length; i++)
            {
                Assert.IsTrue(knobs[i].UsesKnobs, "손잡이 " + i);
                Assert.IsFalse(knobs[i].IsPlainStart, "손잡이 " + i);
            }
        }

        [Test]
        public void TownControlsLineHasNoDevPanel()
        {
            StringAssert.DoesNotContain("F1", TownScript.ControlsLine);
            StringAssert.Contains("I 가방", TownScript.ControlsLine);
        }
    }
}
