using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 원정 '남은 것' 장부(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 가-4). 원정의 첫 층에 들어설 때 시작점을 적고(꾸러미 레벨·측량·줄 끝·한 번 받는 것,
    /// 플레이어 공격·최대 체력), 원정 동안 주운 장비·바꿔 낀 수·쓰러진 횟수를 세고, 바구니로 올라가기 직전에 끝 공격·최대 체력을 적는다.
    /// 마을 도착 카드가 TakeLine으로 한 줄을 가져가며 비운다. 장면을 넘어 살아야 해서 정적 값이고, 도메인 다시 불러오기가 꺼져 있어 SubsystemRegistration에서 비운다.
    /// 원정 도중에 처음 화면으로 나가거나 쪽지 없이 마을이 열리면 Clear로 버린다(그 원정은 남지 않는다).
    /// </summary>
    public static class ExpeditionLedger
    {
        static ExpeditionKept s_kept;
        static readonly HashSet<string> s_onceAtStart = new HashSet<string>();
        static int s_surveyAtStart;
        static int s_ropeAtStart;

        /// <summary>원정을 세는 중인가.</summary>
        public static bool Active => s_kept != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();

        /// <summary>장부를 버린다.</summary>
        public static void Clear()
        {
            s_kept = null;
            s_onceAtStart.Clear();
            s_surveyAtStart = 0;
            s_ropeAtStart = 0;
        }

        /// <summary>층에 들어섬(DungeonRoot): 세는 중이 아니면 이번 원정의 시작점을 적는다.</summary>
        public static void OnFloorEntered(PlayerController player)
        {
            if (s_kept != null) return;
            var carry = ProfileCarry.Data;
            if (carry == null) return;
            s_kept = new ExpeditionKept
            {
                LevelFrom = carry.Level,
                AttackFrom = player ? player.Attack : 0,
                HpFrom = player && player.Sheet != null ? player.Sheet.MaxHp : 0,
            };
            s_onceAtStart.Clear();
            foreach (var id in carry.OnceDone) s_onceAtStart.Add(id);
            s_surveyAtStart = SurveyTotal(carry);
            s_ropeAtStart = carry.RopeDepth;
        }

        /// <summary>장비를 가방에 주웠다(Inventory.PickUp).</summary>
        public static void NotePicked(GearItem item)
        {
            if (s_kept != null && item != null) s_kept.NotePicked(item.Grade, item.DisplayName);
        }

        /// <summary>장비를 바꿔 꼈다(Inventory 끼기).</summary>
        public static void NoteSwapped()
        {
            if (s_kept != null) s_kept.Swapped++;
        }

        /// <summary>쓰러졌다가 다시 섰다(DungeonRoot 다시 서기).</summary>
        public static void NoteDeath()
        {
            if (s_kept != null) s_kept.Deaths++;
        }

        /// <summary>바구니로 올라가기 직전(DungeonRoot): 끝 공격·최대 체력.</summary>
        public static void OnEnding(PlayerController player)
        {
            if (s_kept == null || !player) return;
            s_kept.AttackTo = player.Attack;
            s_kept.HpTo = player.Sheet != null ? player.Sheet.MaxHp : s_kept.HpFrom;
        }

        /// <summary>
        /// 도착 카드 한 줄을 가져가고 장부를 비운다(ExpeditionKept.Line). carry = 도착한 마을 꾸러미(끝 레벨·줄 끝·측량·한 번 받는 것). 세지 않았거나 남은 것이 없으면 null.
        /// </summary>
        public static string TakeLine(CarryData carry)
        {
            var kept = s_kept;
            if (kept == null || carry == null)
            {
                Clear();
                return null;
            }
            kept.LevelTo = carry.Level;
            if (kept.AttackTo == 0) kept.AttackTo = kept.AttackFrom;
            if (kept.HpTo == 0) kept.HpTo = kept.HpFrom;
            kept.NewLandings = Mathf.Max(0, carry.RopeDepth - s_ropeAtStart);
            kept.Survey = Mathf.Max(0, SurveyTotal(carry) - s_surveyAtStart);
            foreach (var id in carry.OnceDone)
            {
                if (s_onceAtStart.Contains(id)) continue;
                var item = FloorRecipe.FindOnceItem(id);
                kept.OnceNames.Add(item != null && !string.IsNullOrEmpty(item.Label) ? item.Label : id);
            }
            Clear();
            return kept.Line();
        }

        static int SurveyTotal(CarryData carry)
        {
            int n = 0;
            foreach (var v in carry.SurveySheets.Values) n += v;
            return n;
        }
    }
}
