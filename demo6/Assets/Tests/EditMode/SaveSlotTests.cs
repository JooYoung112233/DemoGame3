using System;
using System.IO;
using Demo6.Core.Dungeon;
using Demo6.Core.Save;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 저장·처음 화면·멈춤 창 1차 12-2 SaveSlotTests(임시 폴더 — 끝나면 지움): 쓰기(2-3 .tmp → 바꿔치기, 직전 판 .bak, 복사만 쓰는 대신 길)·
    /// 읽기(2-4 남은 .tmp 지움, 본 → 백업)·실패(2-6 본·.bak 그대로)·원래 파일 복사해 남기기(2-1 -broken-·-old-, 같은 이름이면 -2).
    /// 실제 저장 폴더(Application.persistentDataPath)는 건드리지 않는다.
    /// </summary>
    public sealed class SaveSlotTests
    {
        string _root;
        string _folder;
        SaveSlot _slot;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "demo6-save-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            // 저장 폴더는 아직 없다(쓰기가 만든다, 2-3의 1).
            _folder = Path.Combine(_root, "save");
            _slot = new SaveSlot(_folder, "slot1");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch (Exception)
            {
                // 임시 폴더라 남아도 괜찮다.
            }
        }

        static readonly DateTime At = new DateTime(2026, 10, 7, 21, 30, 12);

        /// <summary>골드만 다른 저장 글.</summary>
        static string Text(int gold)
        {
            var d = CarryData.NewProfile(42UL);
            d.Level = 3;
            d.Gold = gold;
            return SaveFile.Compose(d, new SaveHeader { SavedUtc = new DateTime(2026, 10, 7, 12, 30, 12, DateTimeKind.Utc), PlaySeconds = 60 + gold });
        }

        static string Read(string path) => File.ReadAllText(path);

        static string Damage(string text) => text.Replace("\ngold=", "\ngold=9");

        // 1
        [Test]
        public void WriteThenLoadFromMain()
        {
            Assert.IsFalse(Directory.Exists(_folder));
            var w = _slot.Write(Text(10));
            Assert.IsTrue(w.Ok, w.Error ?? "");
            Assert.IsNull(w.Error);
            Assert.IsTrue(File.Exists(_slot.MainPath));
            Assert.IsFalse(File.Exists(_slot.TempPath), ".tmp가 남지 않음");
            Assert.AreEqual(Path.Combine(_folder, "slot1.txt"), _slot.MainPath);
            Assert.AreEqual(Path.Combine(_folder, "slot1.bak"), _slot.BackupPath);
            Assert.AreEqual(Path.Combine(_folder, "slot1.tmp"), _slot.TempPath);

            byte[] bytes = File.ReadAllBytes(_slot.MainPath);
            Assert.AreEqual((byte)'d', bytes[0], "BOM 없는 UTF-8");
            Assert.AreEqual(-1, Array.IndexOf(bytes, (byte)'\r'), "LF만");
            Assert.AreEqual(Text(10), Read(_slot.MainPath));

            var r = _slot.Load();
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.AreEqual(SaveParseStatus.Ok, r.MainStatus);
            Assert.IsTrue(r.AnyFile);
            Assert.IsFalse(r.Newer);
            Assert.AreEqual(10, r.Carry.Gold);
            Assert.AreEqual(70, r.Header.PlaySeconds);
            Assert.IsTrue(_slot.AnyFile);
        }

        // 2
        [Test]
        public void SecondWriteKeepsFirstAsBackup()
        {
            Assert.IsTrue(_slot.Write(Text(10)).Ok);
            Assert.IsTrue(_slot.Write(Text(20)).Ok);
            Assert.AreEqual(Text(10), Read(_slot.BackupPath), ".bak = 직전 판");
            Assert.AreEqual(Text(20), Read(_slot.MainPath));
            Assert.IsFalse(File.Exists(_slot.TempPath));
            var r = _slot.Load();
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.AreEqual(20, r.Carry.Gold, "읽기는 둘째 글");
            Assert.AreEqual(SaveParseStatus.Empty, r.BackupStatus, "본이 되면 백업은 읽지 않음");
        }

        // 3
        [Test]
        public void DamagedMainFallsBackToBackup()
        {
            Assert.IsTrue(_slot.Write(Text(10)).Ok);
            Assert.IsTrue(_slot.Write(Text(20)).Ok);
            File.WriteAllText(_slot.MainPath, Damage(Text(20)));
            var r = _slot.Load();
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(SaveLoadSource.Backup, r.Source);
            Assert.AreEqual(SaveParseStatus.Damaged, r.MainStatus);
            Assert.AreEqual(SaveParseStatus.Ok, r.BackupStatus);
            Assert.AreEqual(10, r.Carry.Gold, "바로 앞 저장");

            File.WriteAllText(_slot.MainPath, "?? 망가진 글 ??");
            var r2 = _slot.Load();
            Assert.AreEqual(SaveLoadSource.Backup, r2.Source);
            Assert.AreEqual(SaveParseStatus.NotSave, r2.MainStatus);
        }

        // 4
        [Test]
        public void BothDamagedLoadsNothingAndKeepCopiesSavesBoth()
        {
            Assert.IsTrue(_slot.Write(Text(10)).Ok);
            Assert.IsTrue(_slot.Write(Text(20)).Ok);
            string badMain = Damage(Text(20));
            string badBackup = Damage(Text(10));
            File.WriteAllText(_slot.MainPath, badMain);
            File.WriteAllText(_slot.BackupPath, badBackup);

            var r = _slot.Load();
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(SaveLoadSource.None, r.Source);
            Assert.IsTrue(r.AnyFile, "파일은 있다");
            Assert.IsFalse(r.Newer);
            Assert.AreEqual(SaveParseStatus.Damaged, r.MainStatus);
            Assert.AreEqual(SaveParseStatus.Damaged, r.BackupStatus);
            Assert.IsNull(r.Carry);
            Assert.IsNull(r.Header);

            var kept = _slot.KeepCopies(At, "broken");
            Assert.AreEqual(2, kept.Count);
            Assert.AreEqual(Path.Combine(_folder, "slot1-broken-20261007-213012.txt"), kept[0]);
            Assert.AreEqual(Path.Combine(_folder, "slot1-broken-20261007-213012-bak.txt"), kept[1]);
            Assert.AreEqual(badMain, Read(kept[0]));
            Assert.AreEqual(badBackup, Read(kept[1]));
            Assert.AreEqual(badMain, Read(_slot.MainPath), "원래 파일은 그대로");
            Assert.AreEqual(badBackup, Read(_slot.BackupPath));
        }

        // 5
        [Test]
        public void LeftoverTempIsDeletedWithoutReading()
        {
            Assert.IsTrue(_slot.Write(Text(10)).Ok);
            File.WriteAllText(_slot.TempPath, Text(99).Substring(0, 40));
            var r = _slot.Load();
            Assert.AreEqual(SaveLoadSource.Main, r.Source);
            Assert.AreEqual(10, r.Carry.Gold, "남은 .tmp는 읽지 않음");
            Assert.IsFalse(File.Exists(_slot.TempPath), "남은 .tmp를 지움");
            Assert.AreEqual(Text(10), Read(_slot.MainPath), "본 파일 그대로");
        }

        // 6
        [Test]
        public void BlockedTempFailsAndKeepsMainAndBackup()
        {
            Assert.IsTrue(_slot.Write(Text(10)).Ok);
            Assert.IsTrue(_slot.Write(Text(20)).Ok);
            Directory.CreateDirectory(_slot.TempPath);

            var w = _slot.Write(Text(30));
            Assert.IsFalse(w.Ok, "쓰기 실패");
            Assert.IsFalse(string.IsNullOrEmpty(w.Error), "짧은 까닭");
            StringAssert.DoesNotContain("Exception", w.Error, "화면 까닭은 한국어만");
            StringAssert.Contains("Exception", w.Detail, "예외 형식 이름은 Detail(콘솔 경고)에");
            Assert.AreEqual(Text(20), Read(_slot.MainPath), "본 그대로");
            Assert.AreEqual(Text(10), Read(_slot.BackupPath), ".bak 그대로");
            Assert.AreEqual(20, _slot.Load().Carry.Gold);

            Directory.Delete(_slot.TempPath);
            Assert.IsTrue(_slot.Write(Text(30)).Ok, "막힌 것을 치우면 다음 저장이 된다");
            Assert.AreEqual(Text(20), Read(_slot.BackupPath));
            Assert.AreEqual(30, _slot.Load().Carry.Gold);

            Assert.IsFalse(_slot.Write("").Ok, "빈 글은 쓰지 않음");
            Assert.IsFalse(_slot.Write(null).Ok);
            Assert.AreEqual(Text(30), Read(_slot.MainPath));
        }

        // 7
        [Test]
        public void NoFileLoadsNothing()
        {
            var r = _slot.Load();
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(SaveLoadSource.None, r.Source);
            Assert.IsFalse(r.AnyFile);
            Assert.IsFalse(r.Newer);
            Assert.AreEqual(SaveParseStatus.Empty, r.MainStatus);
            Assert.AreEqual(SaveParseStatus.Empty, r.BackupStatus);
            Assert.IsFalse(_slot.AnyFile);
            Assert.IsNull(_slot.ReadRaw());
            Assert.IsFalse(Directory.Exists(_folder), "읽기는 폴더를 만들지 않음");
        }

        // 8
        [Test]
        public void ReadRawPrefersMainThenBackup()
        {
            Assert.IsNull(_slot.ReadRaw());
            Assert.IsTrue(_slot.Write(Text(10)).Ok);
            Assert.AreEqual(Text(10), _slot.ReadRaw());
            Assert.IsTrue(_slot.Write(Text(20)).Ok);
            Assert.AreEqual(Text(20), _slot.ReadRaw(), "본 글");
            File.Delete(_slot.MainPath);
            Assert.AreEqual(Text(10), _slot.ReadRaw(), "본이 없으면 백업 글");
            Assert.IsTrue(_slot.AnyFile);
            File.Delete(_slot.BackupPath);
            Assert.IsNull(_slot.ReadRaw(), "둘 다 없으면 null");
            Assert.IsFalse(_slot.AnyFile);
        }

        // 9
        [Test]
        public void CopyOnlyPathWithoutReplace()
        {
            _slot.UseReplace = false;
            Assert.IsTrue(_slot.Write(Text(10)).Ok, "처음 쓰기");
            Assert.AreEqual(Text(10), Read(_slot.MainPath));
            Assert.IsFalse(File.Exists(_slot.BackupPath));

            Assert.IsTrue(_slot.Write(Text(20)).Ok, "두 번째 쓰기(복사만)");
            Assert.AreEqual(Text(10), Read(_slot.BackupPath), ".bak에 첫 글");
            Assert.AreEqual(Text(20), Read(_slot.MainPath), "본에 둘째 글");
            Assert.IsFalse(File.Exists(_slot.TempPath), ".tmp 없음");

            File.Delete(_slot.MainPath);
            Assert.IsTrue(_slot.Write(Text(30)).Ok, "본 없이 .bak만 있을 때");
            Assert.AreEqual(Text(30), Read(_slot.MainPath), "본이 생김");
            Assert.AreEqual(Text(10), Read(_slot.BackupPath), ".bak은 그대로");
            Assert.IsFalse(File.Exists(_slot.TempPath));
            Assert.AreEqual(SaveLoadSource.Main, _slot.Load().Source);
        }

        // 10
        [Test]
        public void KeepCopiesOldNamesAndNeverOverwrites()
        {
            Assert.AreEqual(0, _slot.KeepCopies(At, "old").Count, "파일이 없으면 빈 목록");

            Assert.IsTrue(_slot.Write(Text(10)).Ok);
            Assert.IsTrue(_slot.Write(Text(20)).Ok);
            var first = _slot.KeepCopies(At, "old");
            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(Path.Combine(_folder, "slot1-old-20261007-213012.txt"), first[0]);
            Assert.AreEqual(Path.Combine(_folder, "slot1-old-20261007-213012-bak.txt"), first[1]);

            Assert.IsTrue(_slot.Write(Text(30)).Ok);
            var second = _slot.KeepCopies(At, "old");
            Assert.AreEqual(2, second.Count);
            Assert.AreEqual(Path.Combine(_folder, "slot1-old-20261007-213012-2.txt"), second[0], "같은 시각이면 -2");
            Assert.AreEqual(Path.Combine(_folder, "slot1-old-20261007-213012-bak-2.txt"), second[1]);
            Assert.AreEqual(Text(20), Read(first[0]), "첫 복사본을 덮지 않음");
            Assert.AreEqual(Text(10), Read(first[1]));
            Assert.AreEqual(Text(30), Read(second[0]));
            Assert.AreEqual(Text(20), Read(second[1]));
            Assert.AreEqual(Text(30), Read(_slot.MainPath), "원래 파일은 그대로");

            var plain = _slot.KeepCopies(At, "");
            Assert.AreEqual(Path.Combine(_folder, "slot1-copy-20261007-213012.txt"), plain[0], "꼬리표가 비면 copy");
            Assert.AreEqual(SaveLoadSource.Main, _slot.Load().Source, "복사본이 읽기를 흐리지 않음");
        }
    }
}
