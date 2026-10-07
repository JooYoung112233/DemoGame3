using System;
using System.Collections.Generic;
using System.Text;
using Demo6.Core.Loot;

namespace Demo6.Core.Town
{
    /// <summary>
    /// 한 원정에서 '남은 것'(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 가-4). 도착 카드에 한 줄로 보인다.
    /// 주운 장비 수와 등급별 수(희귀 이상은 이름), 바꿔 낀 수, 시작 → 끝 레벨·공격·최대 체력, 새로 켠 승강장(줄 끝), 받은 측량 장, 쓰러진 횟수,
    /// 이번에 받은 '한 번 받는 것'. 값은 Game(ExpeditionLedger)이 채운다.
    /// </summary>
    public sealed class ExpeditionKept
    {
        /// <summary>등급별 주운 장비 수(Grade 차례: 일반·고급·희귀·영웅·전설).</summary>
        public readonly int[] PickedByGrade = new int[5];
        /// <summary>주운 희귀 이상 장비 이름(주운 차례).</summary>
        public readonly List<string> RareNames = new List<string>();
        public int Swapped;
        public int LevelFrom, LevelTo;
        public int AttackFrom, AttackTo;
        public int HpFrom, HpTo;
        /// <summary>새로 닿은 바구니 줄(줄 끝 층 늘어난 수).</summary>
        public int NewLandings;
        public int Survey;
        public int Deaths;
        /// <summary>이번에 받은 '한 번 받는 것' 이름(명패·쪽지·곡괭이 등).</summary>
        public readonly List<string> OnceNames = new List<string>();

        public int PickedTotal
        {
            get
            {
                int n = 0;
                foreach (var v in PickedByGrade) n += v;
                return n;
            }
        }

        /// <summary>장비 하나를 주웠다.</summary>
        public void NotePicked(Grade grade, string name)
        {
            int g = Math.Max(0, Math.Min(PickedByGrade.Length - 1, (int)grade));
            PickedByGrade[g]++;
            if (grade >= Grade.Rare && !string.IsNullOrEmpty(name)) RareNames.Add(name);
        }

        /// <summary>
        /// 도착 카드 한 줄: "남은 것 — 장비 5(일반 3 · 고급 1 · 희귀 1: 희귀 대검) · 바꿔 낌 2 · 레벨 3 → 4 · 공격 210 → 240 · 최대 체력 900 → 950 ·
        /// 새 승강장 1 · 측량 +2 · 쓰러짐 1 · 손에 넣은 것: 곡괭이". 바뀐 것만 적고, 아무것도 없으면 null.
        /// </summary>
        public string Line()
        {
            var parts = new List<string>();
            int picked = PickedTotal;
            if (picked > 0)
            {
                var grades = new List<string>();
                for (int i = 0; i < PickedByGrade.Length; i++)
                    if (PickedByGrade[i] > 0) grades.Add(GradeName(i) + " " + PickedByGrade[i]);
                string text = "장비 " + picked + "(" + string.Join(" · ", grades);
                if (RareNames.Count > 0) text += ": " + string.Join(", ", RareNames);
                parts.Add(text + ")");
            }
            if (Swapped > 0) parts.Add("바꿔 낌 " + Swapped);
            if (LevelTo > LevelFrom) parts.Add("레벨 " + LevelFrom + " → " + LevelTo);
            if (AttackTo != AttackFrom && AttackFrom > 0) parts.Add("공격 " + AttackFrom + " → " + AttackTo);
            if (HpTo != HpFrom && HpFrom > 0) parts.Add("최대 체력 " + HpFrom + " → " + HpTo);
            if (NewLandings > 0) parts.Add("새 승강장 " + NewLandings);
            if (Survey > 0) parts.Add("측량 +" + Survey);
            if (Deaths > 0) parts.Add("쓰러짐 " + Deaths);
            if (OnceNames.Count > 0) parts.Add("손에 넣은 것: " + string.Join(", ", OnceNames));
            if (parts.Count == 0) return null;
            var sb = new StringBuilder(Prefix);
            sb.Append(string.Join(" · ", parts));
            return sb.ToString();
        }

        public const string Prefix = "남은 것 — ";

        static string GradeName(int g)
        {
            switch (g)
            {
                case 0: return "일반";
                case 1: return "고급";
                case 2: return "희귀";
                case 3: return "영웅";
                default: return "전설";
            }
        }
    }
}
