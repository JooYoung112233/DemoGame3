using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Save
{
    /// <summary>읽은 저장이 어디서 왔나(2-4): 없음 / 본 파일 / 백업(바로 앞 저장).</summary>
    public enum SaveLoadSource
    {
        None,
        Main,
        Backup,
    }

    /// <summary>
    /// 저장 칸 읽기 결과(저장·처음 화면·멈춤 창 1차 2-4·4-7). 본 파일이 되면 백업은 읽지 않는다(BackupStatus는 Empty 그대로).
    /// 파일이 없으면 그 상태는 Empty, 읽다가 예외가 나면 Damaged.
    /// </summary>
    public sealed class SaveLoadResult
    {
        public SaveLoadSource Source;
        public SaveParseStatus MainStatus = SaveParseStatus.Empty;
        public SaveParseStatus BackupStatus = SaveParseStatus.Empty;
        /// <summary>본 파일이나 백업 가운데 하나라도 있었다(읽지 못한 것 포함 — '새로 시작'이 한 번 더 묻는다, 4-3).</summary>
        public bool AnyFile;
        /// <summary>읽은 저장의 머리·꾸러미(Ok일 때만).</summary>
        public SaveHeader Header;
        public CarryData Carry;

        public bool Ok => Source != SaveLoadSource.None;

        /// <summary>읽지 못했고, 그 까닭에 '더 새 판'이 있다(4-7 '더 새 판에서 만든 저장').</summary>
        public bool Newer => !Ok && (MainStatus == SaveParseStatus.NewerVersion || BackupStatus == SaveParseStatus.NewerVersion);
    }

    /// <summary>
    /// 저장 칸 쓰기 결과(2-3의 5). 실패하면 Error에 짧은 한국어 까닭만(화면 글 — 멈춤 창 아래 줄·저장 실패 확인 본문에 그대로 나온다),
    /// Detail에 예외 형식 이름과 메시지(콘솔 경고에만 쓴다).
    /// </summary>
    public sealed class SaveWriteResult
    {
        public bool Ok;
        public string Error;
        public string Detail;
    }

    /// <summary>
    /// 저장 칸 하나(저장·처음 화면·멈춤 창 1차 2-1·2-3·2-4·2-6). 폴더 안의 {이름}.txt(본) · {이름}.bak(직전 판) · {이름}.tmp(쓰는 중).
    /// 쓰기: .tmp에 다 쓰고 디스크까지 내린 뒤 File.Replace로 바꿔치기(직전 본이 .bak). 바꿔치기가 IOException·PlatformNotSupportedException이면
    /// 복사만 쓰는 대신 길(본 → .bak 복사, .tmp → 본 복사, .tmp 지움). 지우기·옮기기를 쓰지 않아 어느 순간에 꺼져도 본이나 .bak 가운데 하나는 직전 판이다.
    /// 읽기: 남은 .tmp는 읽지 않고 지운다 → 본 → 안 되면 .bak.
    /// 엔진을 쓰지 않는다(System.IO만). 폴더를 정하는 일(Application.persistentDataPath/save, 에디터 이름)은 Game 몫이다.
    /// </summary>
    public sealed class SaveSlot
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public string Folder { get; }
        /// <summary>칸 이름(예: slot1, editor-slot1).</summary>
        public string Name { get; }
        public string MainPath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }

        /// <summary>
        /// 참(기본)이면 본이 있을 때 File.Replace로 바꿔치기한다. 거짓이면 File.Replace를 부르지 않고 처음부터 대신 길(복사)로 쓴다 —
        /// 시험이 대신 길을 거치게(2-3, 12-2의 9).
        /// </summary>
        public bool UseReplace = true;

        public SaveSlot(string folder, string name)
        {
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("저장 폴더가 비었다", nameof(folder));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("저장 칸 이름이 비었다", nameof(name));
            Folder = folder;
            Name = name;
            MainPath = Path.Combine(folder, name + ".txt");
            BackupPath = Path.Combine(folder, name + ".bak");
            TempPath = Path.Combine(folder, name + ".tmp");
        }

        /// <summary>본 파일이나 백업 가운데 하나라도 있다.</summary>
        public bool AnyFile => FileExists(MainPath) || FileExists(BackupPath);

        // ── 쓰기(2-3) ─────────────────────────────────────

        /// <summary>
        /// 저장 글을 쓴다(2-3). 어디서든 예외가 나면 잡아 Ok 거짓 + 까닭을 돌려주고 남은 .tmp 지우기를 해 본다(실패 무시).
        /// 실패해도 본·.bak 가운데 하나는 직전 판 그대로다: .tmp를 다 쓰기 전·File.Replace 실패면 둘 다 그대로,
        /// 대신 길의 첫 복사 도중이면 본이 그대로(.bak만 반쯤), 둘째 복사 도중이면 본이 반쯤일 수 있으나 .bak이 직전 판.
        /// 빈 글은 쓰지 않는다(본을 빈 파일로 덮지 않게).
        /// </summary>
        public SaveWriteResult Write(string text)
        {
            var result = new SaveWriteResult();
            if (string.IsNullOrEmpty(text))
            {
                result.Error = "저장할 글이 비었다";
                return result;
            }
            try
            {
                Directory.CreateDirectory(Folder);
                WriteTemp(text);
                if (File.Exists(MainPath))
                {
                    bool replaced = false;
                    if (UseReplace)
                    {
                        try
                        {
                            File.Replace(TempPath, MainPath, BackupPath, true);
                            replaced = true;
                        }
                        catch (IOException)
                        {
                            // 파일 체계·다른 프로그램이 잡음 → 대신 길(바꿔치기가 반쯤 됐을 수 있어 본을 다시 본다).
                        }
                        catch (PlatformNotSupportedException)
                        {
                            // 바꿔치기를 못 하는 환경 → 대신 길.
                        }
                    }
                    if (!replaced) CopyInstead();
                }
                else
                {
                    File.Move(TempPath, MainPath);
                }
                result.Ok = true;
            }
            catch (Exception e)
            {
                result.Ok = false;
                result.Error = Reason(e);
                result.Detail = e.GetType().Name + ": " + e.Message;
                TryDeleteFile(TempPath);
            }
            return result;
        }

        /// <summary>.tmp에 BOM 없는 UTF-8로 다 쓰고 디스크까지 내린다(2-3의 2).</summary>
        void WriteTemp(string text)
        {
            byte[] bytes = Utf8NoBom.GetBytes(text);
            using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
        }

        /// <summary>
        /// 대신 길(2-3): 복사만 쓴다. 본이 있는지 다시 보고(바꿔치기가 반쯤 되어 본이 이미 .bak으로 옮겨졌을 수 있음),
        /// 있으면 본 → .bak 복사(덮어씀) → .tmp → 본 복사(덮어씀), 없으면 .tmp → 본 복사. 끝에 .tmp 지우기를 해 본다
        /// (본은 이미 새 글이라 지우기 실패는 쓰기 실패로 치지 않는다 — 다음 읽기가 지운다).
        /// 옛 대신 길(.bak 지움 → 본을 .bak으로 옮김)은 쓰지 않는다. 다른 프로그램이 본을 잡고 있으면 .bak만 잃기 때문이다.
        /// </summary>
        void CopyInstead()
        {
            if (File.Exists(MainPath))
            {
                File.Copy(MainPath, BackupPath, true);
                File.Copy(TempPath, MainPath, true);
            }
            else
            {
                File.Copy(TempPath, MainPath, true);
            }
            TryDeleteFile(TempPath);
        }

        /// <summary>쓰기 실패 까닭(짧은 한국어만, 예: '디스크에 쓰지 못했다'). 예외 형식 이름은 SaveWriteResult.Detail에 따로 둔다.</summary>
        static string Reason(Exception e)
        {
            string why;
            if (e is UnauthorizedAccessException) why = "권한이 없다";
            else if (e is DirectoryNotFoundException) why = "저장 폴더를 찾지 못했다";
            else if (e is PathTooLongException) why = "저장 경로가 너무 길다";
            else if (e is IOException) why = IsDiskFull(e) ? "디스크가 가득 찼다" : "디스크에 쓰지 못했다";
            else if (e is ArgumentException || e is NotSupportedException) why = "저장 경로가 잘못됐다";
            else why = "저장하지 못했다";
            return why;
        }

        /// <summary>디스크 가득(Win32 ERROR_DISK_FULL 112, ERROR_HANDLE_DISK_FULL 39).</summary>
        static bool IsDiskFull(Exception e)
        {
            int code = e.HResult & 0xFFFF;
            return code == 112 || code == 39;
        }

        // ── 읽기(2-4) ─────────────────────────────────────

        /// <summary>
        /// 저장을 읽는다(2-4): 남은 .tmp는 읽지 않고 지우기를 해 본다(실패 무시) → 본을 풀어 되면 Main →
        /// 아니면 .bak을 풀어 되면 Backup → 둘 다 안 되면 None. 각 상태는 MainStatus·BackupStatus(없으면 Empty, 읽기 예외는 Damaged).
        /// </summary>
        public SaveLoadResult Load()
        {
            var r = new SaveLoadResult();
            TryDeleteFile(TempPath);
            bool main = FileExists(MainPath);
            bool backup = FileExists(BackupPath);
            r.AnyFile = main || backup;

            if (main)
            {
                r.MainStatus = ReadAndParse(MainPath, out var h, out var c);
                if (r.MainStatus == SaveParseStatus.Ok)
                {
                    r.Source = SaveLoadSource.Main;
                    r.Header = h;
                    r.Carry = c;
                    return r;
                }
            }
            if (backup)
            {
                r.BackupStatus = ReadAndParse(BackupPath, out var h, out var c);
                if (r.BackupStatus == SaveParseStatus.Ok)
                {
                    r.Source = SaveLoadSource.Backup;
                    r.Header = h;
                    r.Carry = c;
                }
            }
            return r;
        }

        /// <summary>파일 하나를 읽어 푼다. 사이에 사라졌으면 Empty, 읽다가 예외가 나면 Damaged.</summary>
        static SaveParseStatus ReadAndParse(string path, out SaveHeader header, out CarryData carry)
        {
            header = null;
            carry = null;
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                return SaveFile.TryParse(text, out header, out carry);
            }
            catch (FileNotFoundException)
            {
                return SaveParseStatus.Empty;
            }
            catch (DirectoryNotFoundException)
            {
                return SaveParseStatus.Empty;
            }
            catch (Exception)
            {
                header = null;
                carry = null;
                return SaveParseStatus.Damaged;
            }
        }

        /// <summary>
        /// 저장 파일 글 그대로(시험 메뉴 '저장 파일 글 불러오기', 8-3): 본 글, 본이 없거나 읽지 못하면 백업 글, 둘 다 없으면 null(예외도 null).
        /// </summary>
        public string ReadRaw()
        {
            string text = ReadOrNull(MainPath);
            return text ?? ReadOrNull(BackupPath);
        }

        static string ReadOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ── 복사해 남기기(2-1·2-4·4-5) ─────────────────────────────────────

        /// <summary>
        /// 원래 파일을 복사해 남긴다(지우지 않는다). tag는 "broken"(읽지 못함·더 새 판·백업에서 이어함) 또는 "old"(새로 시작 전 예전 저장, 4-5),
        /// 비었으면 "copy". 본은 {이름}-{tag}-{yyyyMMdd-HHmmss}.txt, 백업은 {이름}-{tag}-{같은 시각}-bak.txt(같은 폴더).
        /// 같은 이름이 이미 있으면 확장자 앞에 -2, -3…을 붙인다(덮어쓰지 않음). 만든 경로 목록을 돌려준다. 예외는 잡고 만든 것만 돌려준다.
        /// </summary>
        public List<string> KeepCopies(DateTime localNow, string tag)
        {
            var made = new List<string>();
            try
            {
                string t = CleanTag(tag);
                string stem = Name + "-" + t + "-" + localNow.ToString("yyyyMMdd-HHmmss", Inv);
                CopyOne(MainPath, stem, made);
                CopyOne(BackupPath, stem + "-bak", made);
            }
            catch (Exception)
            {
                // 만든 것만 돌려준다.
            }
            return made;
        }

        void CopyOne(string source, string stem, List<string> made)
        {
            try
            {
                if (!File.Exists(source)) return;
                string target = FreePath(stem);
                File.Copy(source, target, false);
                made.Add(target);
            }
            catch (Exception)
            {
                // 이 파일은 남기지 못했다. 원래 파일은 그대로다.
            }
        }

        /// <summary>폴더 안에서 아직 없는 이름: {stem}.txt, 있으면 {stem}-2.txt, {stem}-3.txt …</summary>
        string FreePath(string stem)
        {
            string path = Path.Combine(Folder, stem + ".txt");
            for (int n = 2; File.Exists(path) || Directory.Exists(path); n++)
                path = Path.Combine(Folder, stem + "-" + n.ToString(Inv) + ".txt");
            return path;
        }

        /// <summary>꼬리표를 파일 이름에 쓸 수 있게(비었으면 "copy", 파일 이름에 못 쓰는 글자는 '_').</summary>
        static string CleanTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return "copy";
            var bad = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(tag.Length);
            foreach (char c in tag.Trim()) sb.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        // ── 바탕 ─────────────────────────────────────

        static bool FileExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>파일 지우기를 해 본다(실패·폴더는 무시).</summary>
        static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // 무시: 다음 읽기·쓰기가 다시 해 본다.
            }
        }
    }
}
