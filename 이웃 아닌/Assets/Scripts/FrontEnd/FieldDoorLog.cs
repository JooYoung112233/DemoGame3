using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // 문 기록 (기획/탐험-말놓기-조작-재설계.md · 추가 결정): what the member at a door heard, turn after turn, for this visit only
    // (FieldTurnPlanner.Reset clears it at the visit's start and end: the thing is back in its den every visit). Kept per (the party's
    // room, the room behind the door): door 1 is two physical doors (from the arcade and from the storage). Newest first, at most
    // Max per door. The fresh-only reads (door markers, the 2-turns-ahead chip, AutoStop) stay FieldTurnPlanner.TryReport /
    // HeardPresent; this log is what hover and the door-log popup read. Its texts are this Inspector (FieldTurnPlanner → DoorLog).
    [Serializable] public sealed class FieldDoorLog
    {
        [Tooltip("문마다 남기는 기록 수 (오래된 것부터 지움)")] [Min(1)] public int Max = 8;
        [Header("나이")]
        [Tooltip("이번 턴에 들은 기록")] public string AgeNow = "방금";
        [Tooltip("지난 기록 ({0}: 몇 턴 전)")] public string AgeFormat = "{0}턴 전";
        [Tooltip("한 줄 ({0}: 나이, {1}: 들은 것)")] public string LineFormat = "{0} · {1}";
        [Header("들은 것")]
        [Tooltip("안에 아무것도 없음")] public string Quiet = "안에 없음";
        [Tooltip("그 턴에 안에서 나감")] public string Left = "나감";
        [Tooltip("안에 무언가 ({0}: 다음 걸음)")] public string Present = "안에 무언가 · 다음 {0}";
        [Tooltip("다음 걸음: 제자리 (머묾 · 돌아다니다 멈춤)")] public string NextStay = "머묾", NextStop = "멈춤";
        [Tooltip("다음 걸음: 원정대가 선 방으로")] public string NextHere = "이쪽으로";
        [Tooltip("다음 걸음: 다른 방으로 ({0}: 방)")] public string NextTo = "{0}로";
        [Tooltip("원정대가 조용하면 그다음 걸음 ({0}: 그다음)")] public string Then = " · 조용하면 그다음 {0}";
        [Tooltip("그다음: 제자리 · 이쪽으로 · 다른 방 ({0}: 방)")] public string ThenStay = "그대로", ThenHere = "이쪽으로", ThenTo = "{0}로";
        [Tooltip("지난 기록인데 안에 무언가 있었을 때 앞에 붙임 (지금 일이 아님)")] public string StalePrefix = "그때 ";

        [NonSerialized] List<(FieldDoorReport R, int Member)> entries;
        List<(FieldDoorReport R, int Member)> Entries => entries ?? (entries = new List<(FieldDoorReport, int)>());
        public int Count => Entries.Count;
        // Bumps on every change (views re-read only then).
        public int Version { get; private set; }

        public void Add(FieldDoorReport r, int member)
        {
            var e = Entries; e.Insert(0, (r, member)); int n = 0;
            for (int i = 0; i < e.Count; i++) if (e[i].R.Room == r.Room && e[i].R.Door == r.Door && ++n > Mathf.Max(1, Max)) { e.RemoveAt(i); i--; }
            Version++;
        }
        public void Clear() { if (Entries.Count == 0) return; Entries.Clear(); Version++; }
        // What was heard at the door of `room` onto `door` (the room behind it), newest first.
        public IReadOnlyList<(FieldDoorReport R, int Member)> For(int room, int door)
        {
            var list = new List<(FieldDoorReport R, int Member)>();
            foreach (var e in Entries) if (e.R.Room == room && e.R.Door == door) list.Add(e);
            return list;
        }
        public bool TryLatest(int room, int door, out (FieldDoorReport R, int Member) latest)
        {
            foreach (var e in Entries) if (e.R.Room == room && e.R.Door == door) { latest = e; return true; }
            latest = default; return false;
        }
        // One line: '방금 · 안에 없음', '2턴 전 · 그때 안에 무언가 · 다음 복도로 · 조용하면 그다음 그대로'. now: the site clock
        // (FieldSiteState.TurnsUsed); age = now − the turn it was heard.
        public string Line((FieldDoorReport R, int Member) e, int now)
        {
            var r = e.R; int age = Math.Max(0, now - r.Turn);
            string when = age == 0 ? AgeNow : string.Format(AgeFormat, age), what;
            if (!r.Present) what = r.Left ? Left : Quiet;
            else
            {
                string next = r.NextIn ? NextHere : r.Next == r.Door ? (r.Resident == ResidentState.Out ? NextStop : NextStay) : string.Format(NextTo, RoomName(r.Next));
                what = string.Format(Present, next);
                if (!r.NextIn) what += string.Format(Then, r.ThenIn ? ThenHere : r.Then == r.Door ? ThenStay : string.Format(ThenTo, RoomName(r.Then)));
                if (age > 0) what = StalePrefix + what;
            }
            return string.Format(LineFormat, when, what);
        }
        static string RoomName(int room) => room >= 0 && room < FieldSiteState.RoomNames.Length ? FieldSiteState.RoomNames[room] : "";
    }
}
