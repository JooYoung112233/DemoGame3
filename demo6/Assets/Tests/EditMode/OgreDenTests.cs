using System;
using System.Collections.Generic;
using System.Linq;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 2층 계단 아래 '오우거 굴'(전투·보스 문서 3-6·3-8, 묶음 7): 손 지도 P-X, 고정 칸, 합격 규칙(일부러 깬 지도는 떨어짐), 꾸러미 보스 기록
    /// (손실 수·원정 경계·null 안전), 보상 묶음(첫 처치·다시 잡음·씨앗), 보스방 자리 간격(손 계산과 맞춤), 자리 id 머리말, 카메라 크기.
    /// </summary>
    public sealed class OgreDenTests
    {
        /// <summary>걸을 수 있는 안쪽 반폭·반높이(칸 28×16, 벽 두께 1 → x ±13.5, y ±7.5). 문서 '안쪽 26×14'는 OgreDen.InnerHalf*.</summary>
        const float WalkHalfWidth = 13.5f;
        const float WalkHalfHeight = 7.5f;
        const float Eps = 1e-4f;

        [Test]
        public void DenMapParsesAndPasses()
        {
            var map = OgreDen.Build();
            Assert.AreEqual(2, map.Cells.Count);
            var front = MapAnchors.FindBossFront(map);
            var room = MapAnchors.FindBossRoom(map);
            Assert.IsNotNull(front);
            Assert.IsNotNull(room);
            Assert.AreEqual(OgreDen.Floor, map.Floor);
            Assert.AreEqual(front.X + 1, room.X, "쉼터 P 오른쪽에 보스방 X");
            Assert.AreEqual(OgreDen.DoorSide, room.EdgeTo(front).SideFrom(room));
            var report = FloorRules.CheckDen(map);
            Assert.IsTrue(report.Passed, report.ToString());
        }

        [Test]
        public void GenerateDenIsFixedHandMap()
        {
            var a = FloorGenerator.GenerateDen(OgreDen.Floor);
            var b = FloorGenerator.GenerateDen(OgreDen.Floor);
            Assert.IsTrue(a.HandMap);
            Assert.AreEqual(a.Glyphs, b.Glyphs, "늘 같은 굴");
            Assert.IsTrue(a.Report.Passed, a.Report.ToString());
            Assert.AreEqual(OgreDen.Name, a.Name);
            Assert.AreEqual(0UL, a.Seed);
            foreach (int floor in new[] { 0, 1, 3, 99 })
                Assert.DoesNotThrow(() => FloorGenerator.GenerateDen(floor), floor + "층으로 불러도 예외 없음");
        }

        [Test]
        public void DenCellsAreFixedStone()
        {
            var map = OgreDen.Build();
            Assert.IsTrue(MapAnchors.IsFixed(MapAnchors.FindBossRoom(map)));
            Assert.IsTrue(MapAnchors.IsFixed(MapAnchors.FindBossFront(map)));
            Assert.AreEqual(StakeRole.BossFront, MapAnchors.RoleOf(MapAnchors.FindBossFront(map)));
            Assert.AreEqual(OgreDen.FrontStakeId, MapAnchors.StakeIn(MapAnchors.FindBossFront(map)).Id);
        }

        [Test]
        public void FloorTwoStairsLeadToDen()
        {
            Assert.IsTrue(FloorRecipe.HasDenBelow(2));
            Assert.IsFalse(FloorRecipe.HasDenBelow(1));
            Assert.IsTrue(OgreDen.InDungeon);
            Assert.IsTrue(OgreDen.DescendsToDen(2));
            Assert.IsFalse(OgreDen.DescendsToDen(1));
            Assert.AreEqual(BossRules.StartX, OgreDen.BossStart.X, 1e-4, "시작 자리는 BossRules와 같다");
        }

        // ── 꾸러미 보스 기록(BossLedger) ──

        [Test]
        public void LedgerFirstKillRepeatAndPresence()
        {
            var c = CarryData.NewProfile(7UL);
            Assert.IsTrue(BossLedger.Present(c, OgreDen.BossId, 1));
            Assert.IsFalse(BossLedger.FrontLandingOpen(c), "굴 앞 말뚝을 켜기 전");
            Assert.IsTrue(BossLedger.LightStake(c, OgreDen.FrontStakeId));
            Assert.IsFalse(BossLedger.LightStake(c, OgreDen.FrontStakeId), "두 번째는 새로 켠 것이 아님");
            Assert.IsTrue(BossLedger.FrontLandingOpen(c));
            Assert.IsTrue(BossLedger.RecordKill(c, OgreDen.BossId, 3), "첫 처치");
            Assert.IsFalse(BossLedger.Present(c, OgreDen.BossId, 3), "같은 원정에는 다시 안 나옴");
            Assert.IsTrue(BossLedger.Present(c, OgreDen.BossId, 4), "다음 원정에는 다시 나옴");
            Assert.IsFalse(BossLedger.FrontLandingOpen(c), "첫 처치 뒤 '보스방 앞'은 사라짐");
            Assert.IsFalse(BossLedger.RecordKill(c, OgreDen.BossId, 4), "다시 잡음");
            Assert.AreEqual(2, BossLedger.Kills(c, OgreDen.BossId));
            Assert.IsFalse(BossLedger.RecordKill(null, OgreDen.BossId, 1), "꾸러미 없음");
        }

        [Test]
        public void LedgerCountsLossesPerBoss()
        {
            var c = CarryData.NewProfile(1UL);
            Assert.AreEqual(0, BossLedger.Losses(c, OgreDen.BossId));
            BossLedger.RecordLoss(c, OgreDen.BossId);
            BossLedger.RecordLoss(c, OgreDen.BossId);
            BossLedger.RecordLoss(c, "other");
            Assert.AreEqual(2, BossLedger.Losses(c, OgreDen.BossId));
            Assert.AreEqual(1, BossLedger.Losses(c, "other"), "보스마다 따로 셈");
            Assert.IsFalse(BossLedger.Cleared(c, OgreDen.BossId), "쓰러짐은 처치가 아님");
            Assert.IsTrue(BossLedger.Present(c, OgreDen.BossId, 1), "쓰러져도 보스는 그대로 남음(다시 도전)");

            Assert.IsTrue(BossLedger.RecordKill(c, OgreDen.BossId, 1), "쓰러진 뒤의 처치도 첫 처치");
            Assert.AreEqual(2, BossLedger.Losses(c, OgreDen.BossId), "처치해도 쓰러진 횟수는 남음");
            BossLedger.RecordLoss(c, OgreDen.BossId);
            Assert.AreEqual(3, BossLedger.Losses(c, OgreDen.BossId));
            Assert.AreEqual(1, BossLedger.Kills(c, OgreDen.BossId), "쓰러짐이 처치 수를 바꾸지 않음");

            var back = CarryData.FromText(c.ToText());
            Assert.AreEqual(3, BossLedger.Losses(back, OgreDen.BossId), "꾸러미 글 왕복");
            Assert.AreEqual(1, BossLedger.Losses(back, "other"));
        }

        [Test]
        public void LedgerPresenceFollowsExpeditionBoundary()
        {
            var c = CarryData.NewProfile(2UL);
            c.Expedition = 3;
            Assert.AreEqual(0, BossLedger.LastKillExpedition(c, OgreDen.BossId));
            Assert.IsFalse(BossLedger.KilledThisExpedition(c, OgreDen.BossId, 0), "잡은 적 없으면 원정 0도 '이번에 잡음'이 아님");
            Assert.IsTrue(BossLedger.RecordKill(c, OgreDen.BossId, c.Expedition));
            Assert.AreEqual(3, BossLedger.LastKillExpedition(c, OgreDen.BossId));
            Assert.IsTrue(BossLedger.KilledThisExpedition(c, OgreDen.BossId, 3));
            Assert.IsFalse(BossLedger.KilledThisExpedition(c, OgreDen.BossId, 2));
            Assert.IsFalse(BossLedger.Present(c, OgreDen.BossId, 3), "같은 원정");

            // 장면을 다시 지어도(꾸러미 글 왕복) 같은 원정이면 쓰러진 채.
            var rebuilt = CarryData.FromText(c.ToText());
            Assert.IsFalse(BossLedger.Present(rebuilt, OgreDen.BossId, 3), "장면을 다시 지어도 같은 원정에는 한 번");
            Assert.IsTrue(BossLedger.Present(rebuilt, OgreDen.BossId, 4), "바구니로 올라가 밤을 지나면 다시 나옴");

            // 다음 원정에 다시 잡으면 '다시 잡을 때'이고, 마지막 처치 원정이 옮겨 간다.
            Assert.IsFalse(BossLedger.RecordKill(c, OgreDen.BossId, 4));
            Assert.AreEqual(4, BossLedger.LastKillExpedition(c, OgreDen.BossId));
            Assert.IsFalse(BossLedger.Present(c, OgreDen.BossId, 4));
            Assert.IsTrue(BossLedger.Present(c, OgreDen.BossId, 5));
            Assert.IsTrue(BossLedger.Present(c, OgreDen.BossId, 3), "지난 원정 번호로 물으면 이번 기록과 다름(마지막 처치만 남김)");
            Assert.AreEqual(2, BossLedger.Kills(c, OgreDen.BossId));

            Assert.IsTrue(BossLedger.Present(c, "other", 4), "다른 보스는 따로");
            Assert.AreEqual(0, BossLedger.Kills(c, "other"));
        }

        [Test]
        public void LedgerIsNullSafe()
        {
            Assert.AreEqual(0, BossLedger.Kills(null, OgreDen.BossId));
            Assert.IsFalse(BossLedger.Cleared(null, OgreDen.BossId));
            Assert.AreEqual(0, BossLedger.LastKillExpedition(null, OgreDen.BossId));
            Assert.IsFalse(BossLedger.KilledThisExpedition(null, OgreDen.BossId, 1));
            Assert.IsTrue(BossLedger.Present(null, OgreDen.BossId, 1), "꾸러미 없음 = 처음(전투 시험장)");
            Assert.AreEqual(0, BossLedger.Losses(null, OgreDen.BossId));
            Assert.DoesNotThrow(() => BossLedger.RecordLoss(null, OgreDen.BossId));
            Assert.IsFalse(BossLedger.RecordKill(null, OgreDen.BossId, 1));
            Assert.IsFalse(BossLedger.StakeLit(null, OgreDen.FrontStakeId));
            Assert.IsFalse(BossLedger.LightStake(null, OgreDen.FrontStakeId));
            Assert.IsFalse(BossLedger.FrontLandingOpen(null));

            var c = CarryData.NewProfile(4UL);
            Assert.IsFalse(BossLedger.RecordKill(c, null, 1), "보스 id 없음");
            Assert.IsFalse(BossLedger.RecordKill(c, "", 1));
            BossLedger.RecordLoss(c, null);
            BossLedger.RecordLoss(c, "");
            Assert.IsFalse(BossLedger.LightStake(c, null));
            Assert.IsFalse(BossLedger.LightStake(c, ""));
            Assert.AreEqual(0, BossLedger.Kills(c, null));
            Assert.AreEqual(0, BossLedger.Losses(c, ""));
            Assert.IsFalse(BossLedger.StakeLit(c, null));
            Assert.AreEqual(0, c.BossKills.Count + c.BossKillExpedition.Count + c.BossLosses.Count + c.BossStakes.Count, "빈 id는 아무것도 적지 않음");
        }

        // ── 합격 규칙(FloorRules.CheckDen): 일부러 깬 글자 지도 ──

        /// <summary>글자 지도('/'는 줄바꿈, 시험 이름에 줄바꿈이 들어가지 않게)와 범례(없으면 굴 범례)로 지도를 짓는다.</summary>
        static FloorMap Den(string glyphs, CellDef[] legend = null) =>
            FloorMap.Parse(OgreDen.Floor, OgreDen.Name, glyphs.Replace('/', '\n'), legend ?? OgreDen.Legend());

        static CellDef Def(CellDef[] legend, char glyph) => legend.Single(d => d.Glyph == glyph);

        static void AssertFails(FloorMap map, string expected, bool only = false)
        {
            var report = FloorRules.CheckDen(map);
            Assert.IsFalse(report.Passed, "떨어져야 함\n" + report);
            Assert.IsTrue(report.Failures.Any(f => f.Contains(expected)), $"'{expected}' 실패 줄이 없음\n{report}");
            if (only) Assert.AreEqual(1, report.Failures.Count, "이 규칙 하나만 떨어져야 함\n" + report);
        }

        [TestCase("X-P")]
        [TestCase("P/|/X")]
        [TestCase("X/|/P")]
        public void CheckDenFailsWhenDoorIsNotOnTheLeft(string glyphs)
        {
            AssertFails(Den(glyphs), "쪽이어야 함", true);
        }

        [Test]
        public void CheckDenFailsWithoutFrontStake()
        {
            var legend = OgreDen.Legend();
            var p = Def(legend, OgreDen.FrontGlyph);
            p.Features = p.Features.Where(f => f.Kind != FeatureKind.Stake).ToArray();
            AssertFails(Den(OgreDen.Glyphs, legend), "굴 앞 말뚝 없음", true);
        }

        [Test]
        public void CheckDenFailsWithWrongFrontStakeId()
        {
            var legend = OgreDen.Legend();
            Def(legend, OgreDen.FrontGlyph).Features.Single(f => f.Kind == FeatureKind.Stake).Id = "f2.P.stake";
            AssertFails(Den(OgreDen.Glyphs, legend), "굴 앞 말뚝 id f2.P.stake", true);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void CheckDenFailsUnlessExactlyOneBossSpot(int bosses)
        {
            var legend = OgreDen.Legend();
            var x = Def(legend, OgreDen.RoomGlyph);
            var boss = x.Features.Single(f => f.Kind == FeatureKind.Boss);
            var features = new List<CellFeature>();
            for (int i = 0; i < bosses; i++)
                features.Add(new CellFeature
                {
                    Kind = FeatureKind.Boss, Id = OgreDen.BossFeatureId + (i > 0 ? i.ToString() : ""), Local = new Offset(boss.Local.X - 3f * i, 0f),
                    Label = boss.Label, Param = boss.Param,
                });
            x.Features = features.ToArray();
            AssertFails(Den(OgreDen.Glyphs, legend), $"보스 자리 {bosses}개", true);
        }

        [TestCase("P#X")]
        [TestCase("P:X")]
        [TestCase("P=X")]
        [TestCase("P.X")]
        public void CheckDenFailsWhenThePathIsBroken(string glyphs)
        {
            var map = Den(glyphs);
            AssertFails(map, "열린 길로 이웃해야 함");
            AssertFails(map, "1/2칸만 닿음");
        }

        [TestCase(FeatureKind.Stake)]
        [TestCase(FeatureKind.Stairs)]
        [TestCase(FeatureKind.WoodChest)]
        [TestCase(FeatureKind.IronChest)]
        [TestCase(FeatureKind.Group)]
        [TestCase(FeatureKind.Nest)]
        public void CheckDenFailsWithLevelThingsInTheBossRoom(FeatureKind kind)
        {
            var legend = OgreDen.Legend();
            var x = Def(legend, OgreDen.RoomGlyph);
            x.Features = x.Features.Concat(new[] { new CellFeature { Kind = kind, Id = "ogre.X.extra", Local = new Offset(10f, 4f) } }).ToArray();
            AssertFails(Den(OgreDen.Glyphs, legend), "보스방에는", true);
        }

        [Test]
        public void CheckDenFailsOnCellCountsDuplicateIdsAndNoMap()
        {
            AssertFails(null, "굴 지도 없음", true);
            AssertFails(Den("P"), "보스방 0칸");
            AssertFails(Den("X"), "보스방 앞 쉼터 0칸");
            var twoRooms = Den("P-X-X");
            AssertFails(twoRooms, "보스방 2칸");
            AssertFails(twoRooms, "자리 id " + OgreDen.BossFeatureId + ": 겹침");

            var legend = OgreDen.Legend();
            Def(legend, OgreDen.FrontGlyph).Features.Single(f => f.Kind == FeatureKind.Scrawl).Id = OgreDen.FrontLampId1;
            AssertFails(Den(OgreDen.Glyphs, legend), "자리 id " + OgreDen.FrontLampId1 + ": 겹침", true);

            var blank = OgreDen.Legend();
            Def(blank, OgreDen.FrontGlyph).Features.Single(f => f.Kind == FeatureKind.Scrawl).Id = "";
            AssertFails(Den(OgreDen.Glyphs, blank), "자리 id 비어 있음", true);
        }

        // ── 보상 묶음(BossLoot, 3-8 시험판 표) ──

        [Test]
        public void FirstClearBundleMatchesDocTable()
        {
            Assert.AreEqual(3, BossLoot.ItemLevel(OgreDen.Floor), "아이템 레벨 = 층 + 1");
            bool sawUncommon = false;
            for (int seed = 1; seed <= 300; seed++)
            {
                var b = BossLoot.Roll(OgreDen.Floor, true, new Pcg32Random((ulong)seed));
                Assert.AreEqual(3, b.Gear.Count, $"씨앗 {seed}: 장비 3개");
                Assert.IsTrue(b.Gear.All(g => g.Grade >= Grade.Uncommon), $"씨앗 {seed}: 일반 0");
                Assert.IsTrue(b.Gear.Any(g => g.Grade >= Grade.Rare), $"씨앗 {seed}: 희귀 이상 1개 보장");
                Assert.IsTrue(b.Gear.All(g => g.ItemLevel == 3), $"씨앗 {seed}: 아이템 레벨 3");
                Assert.AreEqual(10, b.Stones, $"씨앗 {seed}: 강화석");
                Assert.AreEqual(40, b.Gold, $"씨앗 {seed}: 골드 합");
                Assert.IsTrue(b.GoldPiles.All(g => g > 0));
                if (b.Gear.Skip(1).Any(g => g.Grade == Grade.Uncommon)) sawUncommon = true;
            }
            Assert.IsTrue(sawUncommon, "보장 칸 말고는 고급도 나온다(희귀 이상만 나오는 것이 아님)");
        }

        [Test]
        public void RepeatKillBundleMatchesDocTable()
        {
            for (int seed = 1; seed <= 300; seed++)
            {
                var b = BossLoot.Roll(OgreDen.Floor, false, new Pcg32Random((ulong)seed));
                Assert.AreEqual(1, b.Gear.Count, $"씨앗 {seed}: 장비 1개");
                Assert.GreaterOrEqual(b.Gear[0].Grade, Grade.Uncommon, $"씨앗 {seed}: 고급 이상");
                Assert.AreEqual(3, b.Gear[0].ItemLevel);
                Assert.AreEqual(4, b.Stones, $"씨앗 {seed}: 강화석");
                Assert.AreEqual(15, b.Gold, $"씨앗 {seed}: 골드 합");
            }
        }

        static string Text(LootBundle b) =>
            string.Join(",", b.Gear.Select(CarryData.GearText)) + "|" + b.Stones + "|" + string.Join(",", b.GoldPiles);

        [Test]
        public void SameSeedMakesSameBundleAndSeedsDiffer()
        {
            const ulong salt = 0xDEADBEEFCAFEF00DUL;
            ulong seed = BossLoot.Seed(salt, 3, OgreDen.BossId);
            foreach (bool first in new[] { true, false })
            {
                var a = BossLoot.Roll(OgreDen.Floor, first, new Pcg32Random(seed));
                var b = BossLoot.Roll(OgreDen.Floor, first, new Pcg32Random(seed));
                Assert.AreEqual(Text(a), Text(b), "같은 씨앗이면 같은 묶음(장비 글까지)");
            }
            Assert.AreEqual(seed, BossLoot.Seed(salt, 3, OgreDen.BossId), "같은 원정·같은 프로필이면 같은 씨앗");
            var seeds = new[]
            {
                seed,
                BossLoot.Seed(salt, 4, OgreDen.BossId),
                BossLoot.Seed(salt, 3, "other"),
                BossLoot.Seed(salt + 1, 3, OgreDen.BossId),
                BossLoot.Seed(0UL, 3, OgreDen.BossId),
            };
            Assert.AreEqual(seeds.Length, seeds.Distinct().Count(), "원정·보스 id·소금마다 다른 씨앗");
            Assert.AreNotEqual(Text(BossLoot.Roll(OgreDen.Floor, true, new Pcg32Random(seeds[0]))),
                Text(BossLoot.Roll(OgreDen.Floor, true, new Pcg32Random(seeds[1]))), "다음 원정은 다른 묶음");
            Assert.DoesNotThrow(() => BossLoot.Seed(salt, 3, null));
        }

        [Test]
        public void NullRandomGivesEmptyBundle()
        {
            Assert.IsTrue(BossLoot.Roll(OgreDen.Floor, true, null).Empty);
            Assert.IsTrue(BossLoot.Roll(OgreDen.Floor, false, null).Empty);
        }

        // ── 보스방 자리(3-6 표, 손 계산과 맞춤) ──

        /// <summary>점과 정사각 기둥(가운데 c, 반변 half) 사이 거리.</summary>
        static double ToSquare(Offset p, Offset c, float half)
        {
            double gx = Math.Max(0.0, Math.Abs(p.X - c.X) - half), gy = Math.Max(0.0, Math.Abs(p.Y - c.Y) - half);
            return Math.Sqrt(gx * gx + gy * gy);
        }

        [Test]
        public void PillarGapsLetTheBossThrough()
        {
            float d = BossRules.Diameter;
            float half = OgreDen.PillarSize * 0.5f;
            var pillars = OgreDen.Pillars;
            Assert.AreEqual(4, pillars.Length);
            Assert.AreEqual(2.4f, d, Eps, "문서 지름");
            foreach (var p in pillars)
            {
                Assert.GreaterOrEqual(WalkHalfHeight - (Math.Abs(p.Y) + half), d - Eps, $"기둥 ({p.X}, {p.Y})과 위아래 벽 틈");
                Assert.GreaterOrEqual(WalkHalfWidth - (Math.Abs(p.X) + half), d - Eps, $"기둥 ({p.X}, {p.Y})과 옆 벽 틈");
            }
            Assert.AreEqual(3.2f, WalkHalfHeight - (3.5f + half), Eps, "벽과 틈 3.2(OgreDen 주석)");
            for (int i = 0; i < pillars.Length; i++)
            for (int j = i + 1; j < pillars.Length; j++)
            {
                // 두 정사각 사이 거리 = 한 기둥 가운데에서 (반변 × 2)로 키운 다른 기둥까지 거리.
                double gap = ToSquare(pillars[i], pillars[j], OgreDen.PillarSize);
                Assert.GreaterOrEqual(gap, d - Eps, $"기둥 {i}·{j} 사이 틈");
            }
            Assert.AreEqual(8.5f, pillars[2].X - pillars[0].X, Eps, "기둥 사이 가로 8.5");
            Assert.AreEqual(7f, pillars[0].Y - pillars[1].Y, Eps, "기둥 사이 세로 7");
        }

        [Test]
        public void ArenaSpotsSitInsideTheRoomAndOffThePillars()
        {
            var spots = new List<(string name, Offset at, double clear)>
            {
                ("보스 시작", OgreDen.BossStart, BossRules.Diameter * 0.5),
                ("줄 끝 말뚝", OgreDen.EndStake, 0.5),
                ("문 안쪽", OgreDen.PlayerInside, 0.5),
                ("바구니 장식", OgreDen.BasketDecor, 0.5),
                ("사슬 장식", OgreDen.ChainDecor, 0.5),
                ("뼈 장식", OgreDen.BoneDecor, 0.5),
                ("벽 자국 장식", OgreDen.GnawDecor, 0.5),
            };
            for (int i = 0; i < OgreDen.Lamps.Length; i++) spots.Add(("등잔 " + i, OgreDen.Lamps[i], 0.5));
            for (int i = 0; i < OgreDen.RatHoles.Length; i++) spots.Add(("쥐 구멍 " + i, OgreDen.RatHoles[i], 0.5));
            float half = OgreDen.PillarSize * 0.5f;
            foreach (var s in spots)
            {
                Assert.LessOrEqual(Math.Abs(s.at.X), OgreDen.InnerHalfWidth + Eps, s.name + ": 안쪽 26×14 밖(x)");
                Assert.LessOrEqual(Math.Abs(s.at.Y), OgreDen.InnerHalfHeight + Eps, s.name + ": 안쪽 26×14 밖(y)");
                foreach (var p in OgreDen.Pillars)
                    Assert.GreaterOrEqual(ToSquare(s.at, p, half), s.clear - Eps, $"{s.name}: 기둥 ({p.X}, {p.Y})에 묻힘");
            }
            Assert.AreEqual(Side.Left, OgreDen.DoorSide);
            Assert.AreEqual(-OgreDen.InnerHalfWidth, OgreDen.Door.X, Eps, "문은 왼쪽 벽");
            Assert.AreEqual(0f, OgreDen.Door.Y, Eps, "왼쪽 벽 가운데");
            Assert.Greater(OgreDen.PlayerInside.X, OgreDen.Door.X, "문 안쪽으로 밀어 넣음");
            Assert.Less(Math.Abs(OgreDen.PlayerInside.Y), OgreDen.DoorWidth * 0.5f, "문 폭 안");
            Assert.Greater(OgreDen.EndStake.X, 0f, "줄 끝 말뚝은 X 오른쪽 벽 가까이");
            Assert.AreEqual(BossRules.StartX, OgreDen.BossStart.X, Eps);
            Assert.AreEqual(0f, OgreDen.BossStart.Y, Eps);
            Assert.AreEqual(0f, OgreDen.BossFacingDeg, Eps, "오른쪽 벽을 봄(문 쪽에 등)");
        }

        [Test]
        public void PhaseTwoLampsOutAreDiagonal()
        {
            Assert.AreEqual(4, OgreDen.Lamps.Length);
            var outs = OgreDen.Phase2LampsOut;
            Assert.AreEqual(2, outs.Length);
            Assert.AreNotEqual(outs[0], outs[1]);
            foreach (int i in outs) Assert.That(i, Is.InRange(0, OgreDen.Lamps.Length - 1));
            var a = OgreDen.Lamps[outs[0]];
            var b = OgreDen.Lamps[outs[1]];
            Assert.AreNotEqual(0, Math.Sign(a.X) * Math.Sign(a.Y) * Math.Sign(b.X) * Math.Sign(b.Y), "가운데 줄 위 등잔이 아님");
            Assert.AreNotEqual(Math.Sign(a.X), Math.Sign(b.X), "x 부호 반대");
            Assert.AreNotEqual(Math.Sign(a.Y), Math.Sign(b.Y), "y 부호 반대");
            Assert.AreEqual(0.3f, OgreDen.LampsOutDelay, Eps);
            Assert.Less(OgreDen.LampRelightHold, OgreDen.LampHold, "꺼진 등잔 다시 켜기가 더 빠름(0.5초)");
        }

        [Test]
        public void RatHolesMatchBossRules()
        {
            var holes = OgreDen.RatHoles;
            Assert.AreEqual(4, holes.Length);
            Assert.AreEqual(2, holes.Count(h => Math.Abs(h.X - BossRules.RatHoleNearX) < Eps), "가까운 쪽 둘");
            Assert.AreEqual(2, holes.Count(h => Math.Abs(h.X - BossRules.RatHoleFarX) < Eps), "먼 쪽 둘");
            foreach (var h in holes) Assert.AreEqual(OgreDen.InnerHalfHeight, Math.Abs(h.Y), Eps, "위아래 벽 틈");
            foreach (float x in new[] { BossRules.RatHoleNearX, BossRules.RatHoleFarX })
            {
                Assert.AreEqual(1, holes.Count(h => Math.Abs(h.X - x) < Eps && h.Y > 0f), x + " 위");
                Assert.AreEqual(1, holes.Count(h => Math.Abs(h.X - x) < Eps && h.Y < 0f), x + " 아래");
            }
        }

        [Test]
        public void PieceSlotsFollowTheDenTable()
        {
            CollectionAssert.AreEqual(OgreDen.Pillars, PieceSlots.PillarsIn(PieceKind.BossRoom, -1).ToArray(), "보스방 기둥 = 굴 표");
            CollectionAssert.AreEqual(OgreDen.Pillars, PieceSlots.PillarsIn(PieceKind.BossRoom, 3).ToArray(), "기둥 벌 번호는 쓰지 않음");
            Assert.AreEqual(0, PieceSlots.PillarsIn(PieceKind.BossFront, -1).Count);
            Assert.AreEqual(0, PieceSlots.SlotsFor(PieceKind.BossRoom, -1).Count, "생성기는 굴 조각을 쓰지 않음");
            Assert.AreEqual(0, PieceSlots.SlotsFor(PieceKind.BossFront, -1).Count);
        }

        // ── 자리 id ──

        [Test]
        public void LegendIdsArePrefixedAndNeverClashWithFloorTwo()
        {
            var ids = OgreDen.Legend().SelectMany(d => d.Features).Select(f => f.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            foreach (var id in ids.Concat(new[] { OgreDen.EndStakeId, OgreDen.ArenaLampIdPrefix + "1" }))
            {
                StringAssert.StartsWith("ogre.", id);
                Assert.IsNull(FloorRecipe.FindOnceItem(id), id + ": 한 번 받는 것 id와 겹침");
            }
            CollectionAssert.DoesNotContain(ids, OgreDen.EndStakeId, "줄 끝 말뚝은 자리 표시가 아님(BossArena가 만듦)");
            Assert.AreNotEqual(FloorRules.LandingStakeId(OgreDen.Floor), OgreDen.FrontStakeId);

            var recipe = FloorRecipe.For(OgreDen.Floor);
            var inputs = new List<GeneratorInput>
            {
                new GeneratorInput { Floor = OgreDen.Floor, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 2, HasPickaxe = true },
            };
            for (int seed = 1; seed <= 60; seed++)
                inputs.Add(new GeneratorInput
                {
                    Floor = OgreDen.Floor, Seed = (ulong)seed * 7919UL, FirstVisit = seed % 2 == 0, DeepestFloor = 2, HasPickaxe = true,
                    Night = (NightEvent)(seed % 5),
                });
            var floorTwo = new HashSet<string>();
            foreach (var input in inputs)
                foreach (var f in FloorGenerator.Generate(input).Legend.SelectMany(d => d.Features))
                    floorTwo.Add(f.Id);
            Assert.IsFalse(floorTwo.Any(id => id.StartsWith("ogre.", StringComparison.Ordinal)), "생성 2층에 굴 id 없음");
            CollectionAssert.IsEmpty(floorTwo.Intersect(ids), "굴 id와 생성 2층 id가 겹침");
        }

        [TestCase(16f / 9f, 8.0f)]
        [TestCase(16f / 10f, 8.59f)]
        [TestCase(21f / 9f, 8.0f)]
        [TestCase(4f / 3f, 10.31f)]
        public void CameraSizeMatchesDoc(float aspect, float expected)
        {
            Assert.AreEqual(expected, OgreDen.CameraSize(aspect), 0.01f);
        }

        /// <summary>고정 카메라가 걸을 수 있는 안쪽 27×15 전체(벽에 붙은 플레이어 반지름 0.4 포함)를 담는다. 문서 26×14 식은 16:10·4:3에서 좌우 0.25씩 잘랐다.</summary>
        [TestCase(16f / 9f)]
        [TestCase(16f / 10f)]
        [TestCase(21f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(5f / 4f)]
        public void CameraShowsWholeWalkableRoom(float aspect)
        {
            Assert.AreEqual(WalkHalfWidth, OgreDen.WalkHalfWidth, Eps);
            Assert.AreEqual(WalkHalfHeight, OgreDen.WalkHalfHeight, Eps);
            float halfH = OgreDen.CameraSize(aspect);
            float halfW = halfH * aspect;
            Assert.GreaterOrEqual(halfW + Eps, OgreDen.WalkHalfWidth + 0.25f, "좌우");
            Assert.GreaterOrEqual(halfH + Eps, OgreDen.WalkHalfHeight + 0.5f, "위아래");
        }
    }
}
