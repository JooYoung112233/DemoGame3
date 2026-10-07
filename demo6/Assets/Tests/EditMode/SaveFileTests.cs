using System;
using System.Linq;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Save;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 저장·처음 화면·멈춤 창 1차 12-1 SaveFileTests: 저장 글 만들기·풀기(2-2 형식, 2-4의 5 풀기 결과, 2-5 판본과 옛 글).
    /// 왕복(꾸러미 글·머리), 원정 몫 지움(원본 그대로), 머리 없는 옛 글, 검사 값, 잘린 글, 더 새 판, 빈 글·아무 글, CRLF·끝 빈 줄,
    /// 모르는 머리 키, LF만·첫 줄, 몸 = BodyOf, 같은 도착을 두 번 세지 않음(실제 TownArrivalRules로).
    /// </summary>
    public sealed class SaveFileTests
    {
        /// <summary>레벨·장비·재화·세기를 넣은 꾸러미(CarryDataTests 방식).</summary>
        static CarryData Sample()
        {
            var d = CarryData.NewProfile(42UL);
            d.Expedition = 3;
            d.Level = 4;
            d.TotalXp = 777;
            d.SkillPoints = 1;
            d.SkillRanks = new[] { 1, 0, 2 };
            d.Equipment[(int)GearSlot.Weapon] = new GearItem(GearBaseTable.Greatsword, Grade.Rare, 2, 1050);
            d.Bag.Add(new GearItem(GearBaseTable.Twinblades, Grade.Epic, 3, 930));
            d.Stones = 9;
            d.Gold = 120;
            d.VisitedFloors.Add(1);
            d.Counters["landing_line"] = 3;
            d.Counters[TownSave.VisitsKey] = 2;
            TownSave.MarkSceneSeen(d, TownScript.OpeningId);
            return d;
        }

        static readonly DateTime When = new DateTime(2026, 10, 7, 12, 30, 12, 345, DateTimeKind.Utc);

        static SaveHeader Head(bool trip = false) => new SaveHeader { SavedUtc = When, PlaySeconds = 3725, Trip = trip };

        static SaveParseStatus Parse(string text, out SaveHeader h, out CarryData c) => SaveFile.TryParse(text, out h, out c);

        /// <summary>check 줄을 뺀 글.</summary>
        static string WithoutCheck(string text) =>
            string.Join("\n", text.Split('\n').Where(l => !l.StartsWith(SaveFile.CheckKey + "=", StringComparison.Ordinal)));

        // 1
        [Test]
        public void ComposeThenParseKeepsCarryAndHeader()
        {
            var d = Sample();
            foreach (bool trip in new[] { false, true })
            {
                string text = SaveFile.Compose(d, Head(trip));
                Assert.AreEqual(SaveParseStatus.Ok, Parse(text, out var h, out var c), "trip " + trip);
                Assert.AreEqual(d.ToText(), c.ToText(), "꾸러미 글이 같다");
                Assert.AreEqual(new DateTime(2026, 10, 7, 12, 30, 12, DateTimeKind.Utc), h.SavedUtc, "시각은 초까지(아래는 버림)");
                Assert.AreEqual(DateTimeKind.Utc, h.SavedUtc.Kind);
                Assert.AreEqual(3725, h.PlaySeconds);
                Assert.AreEqual(trip, h.Trip);
                Assert.AreEqual(SaveFile.CurrentVersion, h.Version);
                StringAssert.Contains("\nsaved=2026-10-07T12:30:12Z\n", text);
                StringAssert.Contains("\ntrip=" + (trip ? "1" : "0") + "\n", text);
            }

            var head = Head(true);
            var copy = head.Clone();
            copy.PlaySeconds = 1;
            copy.Trip = false;
            Assert.AreEqual(3725, head.PlaySeconds, "Clone은 따로");
            Assert.IsTrue(head.Trip);
            Assert.AreEqual(head.SavedUtc, copy.SavedUtc);
        }

        // 2
        [Test]
        public void ExpeditionLegIsDroppedButOriginalKeepsIt()
        {
            var d = Sample();
            d.Leg = new ExpeditionLeg { Kills = 3, GoldGained = 50 };
            d.Leg.Floors.Add(1);
            string text = SaveFile.Compose(d, Head());
            StringAssert.DoesNotContain("leg.", text, "원정 몫 줄은 늘 없다(2-2)");
            Assert.AreEqual(SaveParseStatus.Ok, Parse(text, out _, out var c));
            Assert.IsNull(c.Leg, "풀린 꾸러미에 원정 몫 없음");
            Assert.IsNotNull(d.Leg, "원래 꾸러미는 그대로");
            Assert.AreEqual(3, d.Leg.Kills);
            Assert.AreEqual(50, d.Leg.GoldGained);
            CollectionAssert.AreEqual(new[] { 1 }, d.Leg.Floors);
            Assert.AreEqual(d.Gold, c.Gold);
        }

        // 3
        [Test]
        public void HeaderlessCarryTextAndVersionOneAreOk()
        {
            var d = Sample();
            Assert.AreEqual(SaveParseStatus.Ok, Parse(d.ToText(), out var h, out var c), "머리 없는 carry v2 글");
            Assert.AreEqual(d.ToText(), c.ToText());
            Assert.AreEqual(default(DateTime), h.SavedUtc, "머리는 기본값: 시각 없음");
            Assert.AreEqual(0, h.PlaySeconds);
            Assert.IsFalse(h.Trip);
            Assert.AreEqual(SaveFile.CurrentVersion, h.Version);

            string v1 = "carry v1\n" +
                        "level=3\n" +
                        "equipped=wpn_greatsword|Rare|2|1050\n" +
                        "bag=wpn_longsword|Common|1|1000,wpn_twinblades|Epic|3|930\n" +
                        "stones=5\n";
            Assert.AreEqual(SaveParseStatus.Ok, Parse(v1, out var h1, out var c1), "판본 1 글(equipped 줄)");
            Assert.AreEqual(3, c1.Level);
            Assert.AreEqual(5, c1.Stones);
            Assert.AreEqual(GearBaseTable.Greatsword, c1.Equipment[(int)GearSlot.Weapon].BaseId, "옛 낀 무기 → 무기 자리");
            Assert.AreEqual(2, c1.Bag.Count);
            Assert.AreEqual(default(DateTime), h1.SavedUtc);
            Assert.AreEqual(CarryData.CurrentVersion, c1.Version, "풀린 꾸러미는 지금 판본");

            // 머리가 있는 저장의 몸이 판본 1이어도(check 없이) 읽는다.
            string savedV1 = "demo6 save v1\nsaved=2026-10-07T12:30:12Z\nplaytime=60\ntrip=0\n" + v1;
            Assert.AreEqual(SaveParseStatus.Ok, Parse(savedV1, out var h2, out var c2));
            Assert.AreEqual(3, c2.Level);
            Assert.AreEqual(60, h2.PlaySeconds);
        }

        // 4
        [Test]
        public void OneChangedLetterIsDamagedAndRemovingCheckSkipsTheTest()
        {
            string text = SaveFile.Compose(Sample(), Head());
            StringAssert.Contains("\ngold=120\n", text);
            string bad = text.Replace("\ngold=120\n", "\ngold=920\n");
            Assert.AreEqual(SaveParseStatus.Damaged, Parse(bad, out var h, out var c), "몸 한 글자를 바꾸면 검사 값이 틀린다");
            Assert.IsNull(h);
            Assert.IsNull(c);

            string handEdited = WithoutCheck(bad);
            StringAssert.DoesNotContain("check=", handEdited);
            Assert.AreEqual(SaveParseStatus.Ok, Parse(handEdited, out var h2, out var c2), "check 줄을 지우면 검사를 건너뜀(손으로 고친 저장)");
            Assert.AreEqual(920, c2.Gold);
            Assert.AreEqual(3725, h2.PlaySeconds);

            string emptyCheck = text.Replace("\ncheck=" + SaveFile.Checksum(SaveFile.BodyOf(Sample())) + "\n", "\ncheck=\n");
            Assert.AreEqual(SaveParseStatus.Damaged, Parse(emptyCheck, out _, out _), "빈 check 값은 맞지 않는 값");
        }

        // 5
        [Test]
        public void TruncatedTextIsDamagedOrNotSave()
        {
            string text = SaveFile.Compose(Sample(), Head());
            string half = text.Substring(0, text.Length / 2);
            Assert.Greater(half.IndexOf("\ncarry v", StringComparison.Ordinal), 0, "반은 몸 안에서 잘린다");
            Assert.AreEqual(SaveParseStatus.Damaged, Parse(half, out _, out _), "몸 안에서 잘림");

            string headOnly = text.Substring(0, text.IndexOf("check=", StringComparison.Ordinal));
            Assert.AreEqual(SaveParseStatus.Damaged, Parse(headOnly, out _, out _), "머리 안에서 잘림(꾸러미 줄 없음)");
            Assert.AreEqual(SaveParseStatus.Damaged, Parse("demo6 save v1\n", out _, out _), "첫 줄만");
            Assert.AreEqual(SaveParseStatus.Damaged, Parse(text.Substring(0, text.IndexOf("carry v", StringComparison.Ordinal) + 3), out _, out _),
                "꾸러미 줄 첫 글자들만");
            Assert.AreEqual(SaveParseStatus.NotSave, Parse("demo6 sa", out _, out _), "첫 줄 안에서 잘림");
            Assert.AreEqual(SaveParseStatus.Ok, Parse(text.Substring(0, text.Length - 1), out _, out _), "끝 줄바꿈만 빠짐은 그대로");
        }

        // 6
        [Test]
        public void NewerSaveOrCarryVersionIsNotRead()
        {
            var d = Sample();
            string text = SaveFile.Compose(d, Head());
            string newerSave = SaveFile.Magic + (SaveFile.CurrentVersion + 1) + text.Substring(text.IndexOf('\n'));
            StringAssert.StartsWith("demo6 save v2\n", newerSave);
            Assert.AreEqual(SaveParseStatus.NewerVersion, Parse(newerSave, out var h, out var c));
            Assert.IsNull(h);
            Assert.IsNull(c);

            string body = SaveFile.BodyOf(d);
            string newerBody = CarryData.Header + (CarryData.CurrentVersion + 1) + body.Substring(body.IndexOf('\n'));
            StringAssert.StartsWith("carry v3\n", newerBody);
            string withNewerCarry = "demo6 save v1\nsaved=2026-10-07T12:30:12Z\nplaytime=10\ntrip=0\ncheck=" + SaveFile.Checksum(newerBody) + "\n" + newerBody;
            Assert.AreEqual(SaveParseStatus.NewerVersion, Parse(withNewerCarry, out _, out _), "몸이 carry v3(검사 값은 맞음)");
            Assert.AreEqual(SaveParseStatus.NewerVersion, Parse(newerBody, out _, out _), "머리 없는 carry v3");
        }

        // 7
        [Test]
        public void EmptyAndOtherTexts()
        {
            foreach (var empty in new[] { null, "", "   ", "\n\r\n \t\n" })
            {
                Assert.AreEqual(SaveParseStatus.Empty, Parse(empty, out var h, out var c), "빈 글 '" + empty + "'");
                Assert.IsNull(h);
                Assert.IsNull(c);
            }
            foreach (var other in new[] { "hello world", "{\"level\":3}", "settings v1\nsound=1\n", "demo6 save vX\ncarry v2\n", "demo6 save v0\ncarry v2\n", "carry vX\nlevel=3\n" })
            {
                Assert.AreEqual(SaveParseStatus.NotSave, Parse(other, out var h, out var c), "아무 글 '" + other + "'");
                Assert.IsNull(h);
                Assert.IsNull(c);
            }
        }

        // 8
        [Test]
        public void CrlfAndTrailingBlankLinesAreOk()
        {
            var d = Sample();
            string text = SaveFile.Compose(d, Head(true));
            foreach (var variant in new[] { text.Replace("\n", "\r\n"), text + "\n\n", text.Replace("\n", "\r\n") + "\r\n\r\n", "﻿" + text, "\n" + text })
            {
                Assert.AreEqual(SaveParseStatus.Ok, Parse(variant, out var h, out var c));
                Assert.AreEqual(d.ToText(), c.ToText());
                Assert.AreEqual(3725, h.PlaySeconds);
                Assert.IsTrue(h.Trip);
            }
        }

        // 9
        [Test]
        public void UnknownHeaderKeysAreIgnoredAndBadValuesAreDefault()
        {
            string text = SaveFile.Compose(Sample(), Head(true));
            string odd = text.Replace("demo6 save v1\n", "demo6 save v1\nmood=happy\nno equals\n")
                             .Replace("\nplaytime=3725\n", "\nplaytime=abc\n");
            Assert.AreEqual(SaveParseStatus.Ok, Parse(odd, out var h, out var c), "모르는 머리 키는 건너뜀");
            Assert.AreEqual(0, h.PlaySeconds, "읽지 못한 playtime은 0");
            Assert.AreEqual(new DateTime(2026, 10, 7, 12, 30, 12, DateTimeKind.Utc), h.SavedUtc);
            Assert.IsTrue(h.Trip);
            Assert.AreEqual(120, c.Gold);

            string badTime = text.Replace("\nsaved=2026-10-07T12:30:12Z\n", "\nsaved=어제 저녁\n").Replace("\ntrip=1\n", "\ntrip=maybe\n");
            Assert.AreEqual(SaveParseStatus.Ok, Parse(badTime, out var h2, out _));
            Assert.AreEqual(default(DateTime), h2.SavedUtc, "읽지 못한 시각은 없음");
            Assert.IsFalse(h2.Trip, "읽지 못한 trip은 거짓");
            Assert.AreEqual(3725, h2.PlaySeconds);
        }

        // 10
        [Test]
        public void ComposeUsesLfOnlyAndFixedHeadLines()
        {
            string text = SaveFile.Compose(Sample(), Head());
            Assert.AreEqual(-1, text.IndexOf('\r'), "LF만");
            StringAssert.StartsWith("demo6 save v1\n", text);
            var lines = text.Split('\n');
            StringAssert.StartsWith("saved=", lines[1]);
            StringAssert.StartsWith("playtime=", lines[2]);
            StringAssert.StartsWith("trip=", lines[3]);
            StringAssert.StartsWith("check=", lines[4]);
            Assert.AreEqual("carry v2", lines[5]);
            string check = lines[4].Substring("check=".Length);
            Assert.AreEqual(16, check.Length);
            Assert.IsTrue(check.All(ch => (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f')), "소문자 16자리");
            Assert.IsTrue(text.EndsWith("\n", StringComparison.Ordinal) && !text.EndsWith("\n\n", StringComparison.Ordinal), "끝 줄바꿈 하나");

            string plain = SaveFile.Compose(Sample(), null);
            StringAssert.Contains("\nplaytime=0\ntrip=0\n", plain, "머리가 null이면 기본 머리");
            Assert.AreEqual(SaveParseStatus.Ok, Parse(plain, out var h, out _));
            Assert.AreEqual(default(DateTime), h.SavedUtc, "시각 없는 머리는 다시 풀어도 시각 없음");
            Assert.Throws<ArgumentNullException>(() => SaveFile.Compose(null, Head()));
        }

        // 11
        [Test]
        public void BodyOfMatchesComposedBodyAndChecksum()
        {
            var d = Sample();
            d.Leg = new ExpeditionLeg { Kills = 3 };
            string body = SaveFile.BodyOf(d);
            string text = SaveFile.Compose(d, Head());
            int at = text.IndexOf("\ncarry v", StringComparison.Ordinal) + 1;
            Assert.AreEqual(body, text.Substring(at), "Compose 안의 몸과 글자까지 같다");
            StringAssert.StartsWith("carry v2\n", body);
            StringAssert.DoesNotContain("leg.", body);
            Assert.IsNotNull(d.Leg, "BodyOf도 원본을 바꾸지 않는다");
            StringAssert.Contains("\ncheck=" + SaveFile.Checksum(body) + "\n", text);

            // FNV-1a 64 알려진 값, '\r'·끝 줄바꿈은 셈에 들지 않음.
            Assert.AreEqual("cbf29ce484222325", SaveFile.Checksum(""));
            Assert.AreEqual("af63dc4c8601ec8c", SaveFile.Checksum("a"));
            Assert.AreEqual("85944171f73967e8", SaveFile.Checksum("foobar"));
            Assert.AreEqual(SaveFile.Checksum("foobar"), SaveFile.Checksum("foobar\n\n"));
            Assert.AreEqual(SaveFile.Checksum(body), SaveFile.Checksum(body.Replace("\n", "\r\n") + "\r\n"));
            Assert.AreNotEqual(SaveFile.Checksum(body), SaveFile.Checksum(body.Replace("gold=120", "gold=121")));
        }

        // 12
        [Test]
        public void ReloadedSaveDoesNotCountTheSameArrivalTwice()
        {
            // ① 새로 시작 → 오프닝이 끝나 쓴 저장 → 이어하기(바구니 도착으로 처리)
            var fresh = CarryData.NewProfile(42UL);
            var first = TownArrivalRules.Apply(fresh, TownArrivalKind.NewPlay);
            Assert.AreEqual(1, first.Visits);
            Assert.IsFalse(SaveRules.SaveOnArrival(TownArrivalKind.NewPlay, first.Repeated), "새 플레이 도착은 저장하지 않음");
            TalkDirector.Skip(TownScript.Opening, 0, fresh);
            Assert.IsTrue(TownSave.SeenScene(fresh, TownScript.OpeningId));
            string openingSave = SaveFile.Compose(fresh, Head());
            Assert.AreEqual(SaveParseStatus.Ok, Parse(openingSave, out _, out var loaded));
            var again = TownArrivalRules.Apply(loaded, TownArrivalKind.Basket);
            Assert.IsTrue(again.Repeated, "저장 때 이미 처리한 도착");
            Assert.AreEqual(1, again.Visits, "방문 수 그대로");
            Assert.IsFalse(again.OpeningPending, "오프닝을 본 저장");
            Assert.AreEqual(0, TownSave.Departures(loaded));
            Assert.IsFalse(SaveRules.SaveOnArrival(TownArrivalKind.Basket, again.Repeated), "같은 도착은 다시 저장하지 않음");

            // ② 바구니 도착 → 출발 직전 저장(trip=1, 출발 수 +1 앞) → 원정 도중 끔 → 이어하기
            var c = CarryData.NewProfile(42UL);
            TownArrivalRules.Apply(c, TownArrivalKind.NewPlay);
            TalkDirector.Skip(TownScript.Opening, 0, c);
            TownNight.Depart(c);
            TownNight.AdvanceForAscend(c);
            var arrived = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.IsFalse(arrived.Repeated);
            Assert.AreEqual(2, arrived.Visits);
            Assert.IsTrue(SaveRules.SaveOnArrival(TownArrivalKind.Basket, arrived.Repeated), "바구니 새 도착은 저장");
            int departures = TownSave.Departures(c);
            Assert.AreEqual(1, departures);
            string tripSave = SaveFile.Compose(c, Head(true));
            TownNight.Depart(c);
            Assert.AreEqual(2, TownSave.Departures(c), "출발 수 +1은 저장 뒤");

            Assert.AreEqual(SaveParseStatus.Ok, Parse(tripSave, out var h, out var back));
            Assert.IsTrue(h.Trip);
            var resumed = TownArrivalRules.Apply(back, TownArrivalKind.Basket);
            Assert.IsTrue(resumed.Repeated, "출발 직전 저장으로 이어해도 같은 도착");
            Assert.AreEqual(2, resumed.Visits, "방문 수 그대로");
            Assert.AreEqual(departures, TownSave.Departures(back), "town.departures는 출발 전 값");
            Assert.AreEqual(back.Expedition, TownSave.LastArrival(back), "마지막 도착 = 원정 번호");
            Assert.AreEqual(2, back.Expedition);
        }
    }
}
