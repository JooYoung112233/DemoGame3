using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Save
{
    /// <summary>
    /// 저장 화면 글 한 곳(저장·처음 화면·멈춤 창 1차 4-3 이어하기 줄, 4-8 이어서 알림, 2-6 실패 알림, 3-1 F1 알림). 글은 모두 초안이다.
    /// 시각은 부르는 쪽이 이 기기 시각으로 바꿔 넘긴다(savedLocal — 시험이 시간대에 매이지 않게).
    /// </summary>
    public static class SaveText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>쓰기 실패 알림(2-6, 실패가 이어지는 동안 한 번만).</summary>
        public const string SaveFailed = "저장하지 못했다 — 다음 저장 때 다시 해 본다.";
        /// <summary>F1을 써서 이제 저장하지 않는 판(3-1, 판마다 한 번).</summary>
        public const string NotSavingDevPanel = "F1 시험 손잡이를 쓴 판이라 이제 저장하지 않는다.";

        /// <summary>이어서 알림 첫 줄(시각이 없을 때).</summary>
        public const string ContinuePlain = "이어서 한다.";
        /// <summary>출발 직전 저장에서 이어함(3-5, 4-8).</summary>
        public const string TripNotice = "지난번엔 갱도 안에서 끝냈다. 권양기를 떠나기 직전으로 돌아왔다 — 그 원정에서 얻은 것은 남지 않았다.";
        /// <summary>백업에서 이어함(2-4의 3, 4-8).</summary>
        public const string BackupNotice = "저장 파일이 상해 바로 앞 저장을 불러왔다.";
        /// <summary>이어하기 줄 꼬리(출발 직전 저장).</summary>
        public const string TripTail = " · 원정 도중 끝냄";
        /// <summary>이어하기 줄 꼬리(백업에서 읽음).</summary>
        public const string BackupTail = " (바로 앞 저장)";

        /// <summary>'10월 7일 21:30'(월·일 앞 0 없음, 시:분 두 자리).</summary>
        public static string When(DateTime local) =>
            local.Month.ToString(Inv) + "월 " + local.Day.ToString(Inv) + "일 " + local.Hour.ToString("00", Inv) + ":" + local.Minute.ToString("00", Inv);

        /// <summary>놀이 시간(4-8): 1분 미만 '1분 미만', 1시간 미만 '{m}분', 그 밖 '{h}시간 {m}분'.</summary>
        public static string PlayTime(long seconds)
        {
            long s = Math.Max(0L, seconds);
            if (s < 60) return "1분 미만";
            if (s < 3600) return (s / 60).ToString(Inv) + "분";
            return (s / 3600).ToString(Inv) + "시간 " + (s % 3600 / 60).ToString(Inv) + "분";
        }

        /// <summary>
        /// 처음 화면 이어하기 아래 줄(4-3): '원정 3번째 준비 · 레벨 4 · 놀이 1시간 2분 · 10월 7일 21:30 저장'.
        /// 출발 직전 저장이면 ' · 원정 도중 끝냄', 백업에서 읽었으면 ' (바로 앞 저장)'. 저장 시각이 없으면(머리 없는 글) 시각 마디를 뺀다.
        /// </summary>
        public static string ContinueLine(SaveHeader h, CarryData c, DateTime savedLocal, bool fromBackup)
        {
            var head = h ?? new SaveHeader();
            var parts = new List<string>(4);
            if (c != null)
            {
                parts.Add("원정 " + c.Expedition.ToString(Inv) + "번째 준비");
                parts.Add("레벨 " + c.Level.ToString(Inv));
            }
            parts.Add("놀이 " + PlayTime(head.PlaySeconds));
            if (HasTime(head)) parts.Add(When(savedLocal) + " 저장");
            string line = string.Join(" · ", parts);
            if (head.Trip) line += TripTail;
            if (fromBackup) line += BackupTail;
            return line;
        }

        /// <summary>
        /// 이어한 뒤 마을 알림(4-8): '이어서 한다 — 10월 7일 21:30 마을 저장.'(시각이 없으면 '이어서 한다.'),
        /// 출발 직전 저장이면 원정이 남지 않았다는 줄, 백업이면 상한 저장 줄을 차례로 더한다.
        /// </summary>
        public static string[] ContinueNotice(SaveHeader h, bool fromBackup, DateTime savedLocal)
        {
            var head = h ?? new SaveHeader();
            var lines = new List<string>(3)
            {
                HasTime(head) ? "이어서 한다 — " + When(savedLocal) + " 마을 저장." : ContinuePlain,
            };
            if (head.Trip) lines.Add(TripNotice);
            if (fromBackup) lines.Add(BackupNotice);
            return lines.ToArray();
        }

        /// <summary>
        /// 읽지 못한 저장을 남기고 새로 시작한 알림(4-8): '저장을 읽지 못해 새로 시작했다. 원래 파일은 slot1-broken-….txt로 남겼다.'
        /// keptName은 남긴 파일 이름(경로가 오면 이름만 쓴다). 조사는 끝소리에 맞춰 '로'·'으로'.
        /// </summary>
        public static string BrokenNewStart(string keptName)
        {
            string name = FileNameOf(keptName);
            if (string.IsNullOrEmpty(name)) return "저장을 읽지 못해 새로 시작했다. 원래 파일은 저장 폴더에 따로 남겼다.";
            return "저장을 읽지 못해 새로 시작했다. 원래 파일은 " + name + RoParticle(name) + " 남겼다.";
        }

        /// <summary>경로면 파일 이름만(경로 글자가 잘못돼 못 자르면 받은 글 그대로).</summary>
        static string FileNameOf(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                return Path.GetFileName(path.Trim());
            }
            catch (ArgumentException)
            {
                return path.Trim();
            }
        }

        /// <summary>저장 시각이 있나(default면 머리 없는 글).</summary>
        static bool HasTime(SaveHeader h) => h != null && h.SavedUtc != default(DateTime);

        /// <summary>
        /// 방향 조사 '로'·'으로'(받침이 없거나 ㄹ이면 '로'). 한글이 아니면 읽는 소리로: 숫자 0·3·6(영·삼·육)과 m·n(엠·엔)은 '으로', 그 밖은 '로'.
        /// </summary>
        static string RoParticle(string word)
        {
            char c = word[word.Length - 1];
            if (c >= '가' && c <= '힣')
            {
                int final = (c - '가') % 28;
                return final == 0 || final == 8 ? "로" : "으로";
            }
            switch (c)
            {
                case '0':
                case '3':
                case '6':
                case 'm':
                case 'n':
                case 'M':
                case 'N':
                    return "으로";
                default:
                    return "로";
            }
        }
    }
}
