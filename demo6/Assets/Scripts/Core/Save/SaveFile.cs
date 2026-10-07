using System;
using System.Globalization;
using System.Text;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Save
{
    /// <summary>
    /// 저장 파일 머리(저장·처음 화면·멈춤 창 1차 2-2): 저장 판본, 저장 시각(UTC, 초까지), 놀이 시간(초), 출발 직전 저장 표시(trip).
    /// 머리 없는 옛 꾸러미 글을 읽으면 기본값(시각 없음 = default, 놀이 0, trip 거짓)이다.
    /// </summary>
    public sealed class SaveHeader
    {
        /// <summary>저장 판본(SaveFile.CurrentVersion부터).</summary>
        public int Version = SaveFile.CurrentVersion;
        /// <summary>저장 시각(UTC). default면 '시각 없음'(머리 없는 글).</summary>
        public DateTime SavedUtc;
        /// <summary>놀이 시간(초). 처음 화면에서 시작한 판에서 멈춤 창이 닫혀 있는 동안 센다(2-2).</summary>
        public long PlaySeconds;
        /// <summary>권양기 출발 직전에 쓴 저장(3-2). 이어하기 줄·알림에 '원정 도중 끝냄'을 붙인다(3-5).</summary>
        public bool Trip;

        public SaveHeader Clone() => new SaveHeader { Version = Version, SavedUtc = SavedUtc, PlaySeconds = PlaySeconds, Trip = Trip };
    }

    /// <summary>저장 글 풀기 결과(2-4의 5): 됨 / 비었음 / 저장 글이 아님 / 상함 / 더 새 판.</summary>
    public enum SaveParseStatus
    {
        Ok,
        /// <summary>빈 글·공백뿐.</summary>
        Empty,
        /// <summary>첫 줄이 저장 머리도 꾸러미 머리도 아님(저장 판본 숫자를 못 읽음 포함).</summary>
        NotSave,
        /// <summary>검사 값 틀림·꾸러미 줄 없음·꾸러미를 풀지 못함.</summary>
        Damaged,
        /// <summary>저장 판본이나 꾸러미 판본이 지금보다 큼(2-5 — 읽지 않는다).</summary>
        NewerVersion,
    }

    /// <summary>
    /// 저장 글 만들기·풀기(저장·처음 화면·멈춤 창 1차 2-2 형식, 2-4의 5 풀기, 2-5 판본). 디스크는 만지지 않는다(SaveSlot이 쓴다).
    /// 글 = 머리 줄('demo6 save v1' / saved= / playtime= / trip= / check=) + 꾸러미 글(CarryData.ToText, 'carry v2'부터 끝까지).
    /// UTF-8, 줄바꿈 LF만. 원정 몫(leg.*) 줄은 늘 뺀다(2-7 — 마을 저장에는 원정 몫이 없어야 한다).
    /// 검사 값 = 꾸러미 글의 FNV-1a 64비트('\r'을 빼고 끝 줄바꿈을 뗀 UTF-8 바이트, 소문자 16자리). check 줄이 없으면 검사를 건너뛴다.
    /// 머리 없이 'carry v'로 시작하는 옛 글(F1 '꾸러미 글 복사')도 읽는다.
    /// </summary>
    public static class SaveFile
    {
        public const string Magic = "demo6 save v";
        public const int CurrentVersion = 1;
        public const string CheckKey = "check";
        public const string SavedKey = "saved";
        public const string PlaytimeKey = "playtime";
        public const string TripKey = "trip";
        /// <summary>저장 시각 형식(UTC, 초까지 — T·Z는 따옴표로 묶은 글자).</summary>
        public const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// 저장 몸(2-2 'carry v2 줄부터 끝까지'): 꾸러미를 깊이 복사해 원정 몫을 지운 꾸러미 글. 원본 꾸러미는 바꾸지 않는다.
        /// 글 끝은 줄바꿈 하나다(CarryData.ToText 그대로).
        /// </summary>
        public static string BodyOf(CarryData carry)
        {
            if (carry == null) throw new ArgumentNullException(nameof(carry));
            var copy = carry.Clone();
            copy.Leg = null;
            return copy.ToText();
        }

        /// <summary>
        /// 저장 글을 만든다(2-2): 'demo6 save v1' / 'saved=' UTC 시각(초 아래 버림) / 'playtime=' 초 / 'trip=0|1' / 'check=' 검사 값 / 꾸러미 몸.
        /// header가 null이면 기본 머리(시각 없음·놀이 0·trip 거짓). 머리의 Version과 상관없이 지금 판본으로 쓴다. 줄바꿈은 LF만.
        /// carry가 null이면 ArgumentNullException(빈 꾸러미로 실제 저장을 덮지 않게).
        /// </summary>
        public static string Compose(CarryData carry, SaveHeader header)
        {
            if (carry == null) throw new ArgumentNullException(nameof(carry));
            var h = header ?? new SaveHeader();
            string body = BodyOf(carry);
            var sb = new StringBuilder(body.Length + 128);
            sb.Append(Magic).Append(CurrentVersion.ToString(Inv)).Append('\n');
            sb.Append(SavedKey).Append('=').Append(FormatTime(h.SavedUtc)).Append('\n');
            sb.Append(PlaytimeKey).Append('=').Append(Math.Max(0L, h.PlaySeconds).ToString(Inv)).Append('\n');
            sb.Append(TripKey).Append('=').Append(h.Trip ? '1' : '0').Append('\n');
            sb.Append(CheckKey).Append('=').Append(Checksum(body)).Append('\n');
            sb.Append(body);
            return sb.ToString();
        }

        /// <summary>
        /// 꾸러미 글의 검사 값(2-2): '\r'을 모두 빼고 끝의 '\n'들을 뗀 뒤 UTF-8 바이트로 FNV-1a 64비트, 소문자 16자리.
        /// 그래서 줄바꿈을 CRLF로 바꾸거나 끝에 빈 줄을 더한 글도 같은 값이다.
        /// </summary>
        public static string Checksum(string body)
        {
            string s = (body ?? "").Replace("\r", "").TrimEnd('\n');
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            ulong hash = 14695981039346656037UL;
            unchecked
            {
                foreach (byte b in bytes)
                {
                    hash ^= b;
                    hash *= 1099511628211UL;
                }
            }
            return hash.ToString("x16", Inv);
        }

        /// <summary>
        /// 저장 글을 푼다(2-4의 5, 2-5). Ok일 때만 header·carry가 null이 아니다.
        /// - null·공백뿐 → Empty.
        /// - 첫 줄이 'carry v' → 머리 없는 옛 글: 머리는 기본값. 꾸러미 판본이 지금보다 크면 NewerVersion, 꾸러미를 풀지 못하면 NotSave.
        ///   (원정 몫 줄이 있으면 그대로 풀린다 — 마을에 깔 때 지우는 것은 부르는 쪽 몫, 8-3.)
        /// - 첫 줄이 'demo6 save v' → 판본 숫자를 못 읽으면 NotSave, 지금보다 크면 NewerVersion. 'carry v' 줄 앞까지 key=value 머리
        ///   (모르는 키 무시, 못 읽은 값은 기본). 'carry v' 줄이 없거나, check가 있는데 맞지 않거나, 꾸러미를 풀지 못하면 Damaged.
        ///   꾸러미 판본이 지금보다 크면 NewerVersion.
        /// - 그 밖 → NotSave.
        /// '\r'은 모두 빼고 읽는다. 글 앞의 BOM·빈 줄은 건너뛴다(붙여 넣은 글).
        /// </summary>
        public static SaveParseStatus TryParse(string text, out SaveHeader header, out CarryData carry)
        {
            header = null;
            carry = null;
            if (string.IsNullOrWhiteSpace(text)) return SaveParseStatus.Empty;
            string norm = text.Replace("\r", "").TrimStart('﻿').TrimStart();
            if (norm.Length == 0) return SaveParseStatus.Empty;
            var lines = norm.Split('\n');
            string first = lines[0].Trim();

            // 머리 없는 옛 꾸러미 글(2-2 끝, 9장 '옛 꾸러미 글')
            if (first.StartsWith(CarryData.Header, StringComparison.Ordinal))
            {
                if (!TryVersion(first, CarryData.Header, out int oldCarryVersion)) return SaveParseStatus.NotSave;
                if (oldCarryVersion > CarryData.CurrentVersion) return SaveParseStatus.NewerVersion;
                try
                {
                    carry = CarryData.FromText(norm);
                }
                catch (FormatException)
                {
                    carry = null;
                    return SaveParseStatus.NotSave;
                }
                catch (Exception)
                {
                    carry = null;
                    return SaveParseStatus.Damaged;
                }
                header = new SaveHeader();
                return SaveParseStatus.Ok;
            }

            if (!first.StartsWith(Magic, StringComparison.Ordinal)) return SaveParseStatus.NotSave;
            // 저장 판본은 1부터(2-5). 못 읽거나 1보다 작으면 저장 글이 아니다.
            if (!TryVersion(first, Magic, out int saveVersion) || saveVersion < 1) return SaveParseStatus.NotSave;
            if (saveVersion > CurrentVersion) return SaveParseStatus.NewerVersion;

            var h = new SaveHeader { Version = saveVersion };
            string check = null;
            int bodyLine = -1;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.TrimStart().StartsWith(CarryData.Header, StringComparison.Ordinal))
                {
                    bodyLine = i;
                    break;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case SavedKey: h.SavedUtc = ParseTime(value); break;
                    case PlaytimeKey:
                        h.PlaySeconds = long.TryParse(value, NumberStyles.Integer, Inv, out long seconds) ? Math.Max(0L, seconds) : 0L;
                        break;
                    case TripKey: h.Trip = value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase); break;
                    case CheckKey: check = value; break;
                }
            }
            if (bodyLine < 0) return SaveParseStatus.Damaged;

            string body = string.Join("\n", lines, bodyLine, lines.Length - bodyLine);
            if (check != null && !string.Equals(check, Checksum(body), StringComparison.OrdinalIgnoreCase)) return SaveParseStatus.Damaged;
            if (!TryVersion(lines[bodyLine].Trim(), CarryData.Header, out int carryVersion)) return SaveParseStatus.Damaged;
            if (carryVersion > CarryData.CurrentVersion) return SaveParseStatus.NewerVersion;

            CarryData parsed;
            try
            {
                parsed = CarryData.FromText(body);
            }
            catch (Exception)
            {
                return SaveParseStatus.Damaged;
            }
            header = h;
            carry = parsed;
            return SaveParseStatus.Ok;
        }

        /// <summary>'{머리}{숫자}' 줄의 판본 숫자.</summary>
        static bool TryVersion(string line, string prefix, out int version)
        {
            version = 0;
            return line != null && line.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(line.Substring(prefix.Length), NumberStyles.Integer, Inv, out version);
        }

        /// <summary>저장 시각 글(UTC, TimeFormat). 지역 시각이 오면 UTC로 바꾸고, Kind가 정해지지 않았으면 UTC로 본다. 초 아래는 버린다.</summary>
        static string FormatTime(DateTime t)
        {
            var utc = t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : t;
            return utc.ToString(TimeFormat, Inv);
        }

        /// <summary>저장 시각 읽기(쓰기와 같은 형식). 못 읽으면 default(시각 없음). Kind는 Utc.</summary>
        static DateTime ParseTime(string value)
        {
            if (DateTime.TryParseExact(value, TimeFormat, Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime t))
                return DateTime.SpecifyKind(t, DateTimeKind.Utc);
            return default;
        }
    }
}
