using System;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 도착 쪽지(기획/마을-의뢰-첫판.md 1-4 단계 2): 던전에서 바구니로 올라갈 때 두고, 마을 루트 Awake가 꺼내 쓴다.
    /// 원정 요약(도착 카드 둘째·셋째 줄 = 옛 결과 창 숫자), 도착 종류, 이어 보여 줄 올라가는 카드 글·끝 시각.
    /// </summary>
    public sealed class TownArrivalNote
    {
        public TownArrivalKind Kind = TownArrivalKind.Basket;
        /// <summary>이번 원정 요약(없으면 도착 카드에서 두 줄을 뺀다).</summary>
        public ExpeditionSummary Summary;
        /// <summary>올라가는 카드 글(TownNight.AscendLines)과 끝 시각(Time.realtimeSinceStartup 기준, 0이면 없음).</summary>
        public string[] CardLines = Array.Empty<string>();
        public float CardUntil;
    }

    /// <summary>
    /// 마을 도착 쪽지 보관소. 장면을 바꿔도 살아남고(SceneStatics.Reset이 비우지 않음), 플레이를 새로 시작하면 비워진다.
    /// 쪽지가 없고 town.visits가 0이면 새 플레이(남쪽 길 끝에서 시작, 1-2).
    /// </summary>
    public static class TownTravel
    {
        /// <summary>마을이 아직 꺼내지 않은 쪽지(없으면 null).</summary>
        public static TownArrivalNote Note { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Note = null;

        public static void Set(TownArrivalNote note) => Note = note;

        /// <summary>쪽지를 꺼낸다(한 번만, 꺼내면 비움).</summary>
        public static TownArrivalNote Take()
        {
            var note = Note;
            Note = null;
            return note;
        }

        public static void Clear() => Note = null;

        /// <summary>
        /// 던전 올라가기 갈래(1-4 단계 2, 꾸러미 4가 DungeonRoot에서 부름). 꾸러미를 담은 뒤에 부른다:
        /// 원정 번호 +1과 오늘 밤 사건을 정하고(TownNight.AdvanceForAscend — 올라가기 한 번에 여기 한 곳에서만), 도착 쪽지를 두고,
        /// 올라가는 카드 "바구니가 덜컹이며 올라간다." 1.4초를 띄운 뒤 화면이 덮이면 마을 장면을 불러온다.
        /// 마을 장면을 불러올 수 없으면 아무것도 바꾸지 않고 경고를 남긴 뒤 false(던전은 옛 흐름: 결과 창 → 밤 → 다시 불러오기, 1-4 단계 3).
        /// </summary>
        public static bool TryAscend(NightCard card, ExpeditionSummary summary)
        {
            if (!SceneTravel.CanLoad(SceneTravel.TownPath))
            {
                Debug.LogWarning("[마을] 마을 장면이 없어 옛 흐름(결과 창 → 밤 → 같은 던전)으로 간다 — 'Demo6/마을 씬 만들기'를 돌리면 마을로 올라간다");
                return false;
            }
            var carry = ProfileCarry.Ensure();
            TownNight.AdvanceForAscend(carry);
            var lines = TownNight.AscendLines();
            float until = Time.realtimeSinceStartup + TownNight.AscendSeconds;
            Set(new TownArrivalNote { Kind = TownArrivalKind.Basket, Summary = summary, CardLines = lines, CardUntil = until });
            ProfileCarry.SetTrip(null);
            Debug.Log($"[마을] 바구니로 올라감 → 등불골 (다음 원정 {carry.Expedition} · 오늘 밤 {carry.Night})");
            if (card) card.ShowNight(lines, TownNight.AscendSeconds, Covered);
            else Covered();
            return true;

            void Covered()
            {
                if (!SceneTravel.Load(SceneTravel.TownPath))
                    Debug.LogError("[마을] 마을 장면을 불러오지 못했다(카드 뒤) — 빌드 목록을 확인");
            }
        }
    }
}
