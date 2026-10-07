using System;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 원정의 긴장(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5, 사용자 결정 D1 '다', 2026-10-05).
    /// - 다시 설 때: 일반 층은 체력 60%, 물약은 쓰러질 때 남은 수 그대로(0병이면 1병). 보스 재도전은 보스방이 가득 채운다(그대로).
    /// - 말뚝 쉬기: 원정마다 한 번, 켠 말뚝에서 체력·물약을 가득.
    /// - 피 묻은 주머니: 쓰러진 자리에 이번 원정에서 번 강화석·골드의 20%를 떨군다. 주우면 되찾는다. 줍기 전에 또 쓰러지거나 층을 떠나면 사라진다.
    /// - 도시락통: 열면 반경 12의 큰 소리, 나온 굴쥐는 보상 없음. 그냥 두면 식은 주먹밥(먹으면 체력 15%, 원정마다 1개까지).
    /// 값은 F1 손잡이(DownTuning)로 바꿀 수 있다.
    /// </summary>
    public static class DownRules
    {
        public const float RespawnHpFraction = 0.6f;
        public const float PouchShare = 0.2f;
        public const float RiceHealFraction = 0.15f;
        public const int RiceBallCap = 1;
        public const float LunchboxNoise = 12f;

        // ── 빛을 개수로 쓰기(묶음 5-6, 2026-10-07 사용자 '기름 병도 진행해') ──
        /// <summary>원정을 시작할 때 든 기름 병.</summary>
        public const int StartOil = 2;
        /// <summary>들 수 있는 기름 병 상한.</summary>
        public const int OilCap = 4;
        /// <summary>나무 궤짝에서 기름 병이 나올 확률.</summary>
        public const double ChestOilChance = 0.35;
        /// <summary>숨은 방 '?'(그리고 '층 등잔' 알림)에 필요한 켠 등잔 수(층 등잔이 이보다 적으면 모두).</summary>
        public const int ClueLamps = 3;

        /// <summary>켠 등잔이 단서 수에 닿았는가(등잔이 없으면 false).</summary>
        public static bool CluesShown(int lit, int total) => total > 0 && lit >= Math.Min(ClueLamps, total);

        public static int AddOil(int oil, int add) => Math.Max(0, Math.Min(OilCap, oil + add));

        public const string NoOilLine = "기름 병이 없다 — 나무 궤짝·도시락통·막다른 곳에서 찾자";
        public const string OilFullLine = "기름 병을 더 들 자리가 없다";

        /// <summary>다시 설 때 물약 수: 쓰러질 때 남은 수, 0병이면 1병(칸 수로 자름).</summary>
        public static int RespawnPotions(int atDown, int capacity) => Math.Max(0, Math.Min(capacity, Math.Max(1, atDown)));

        /// <summary>다시 설 때 체력(최대 체력의 60%, 적어도 1).</summary>
        public static int RespawnHp(int maxHp, float fraction = RespawnHpFraction) => Math.Max(1, (int)Math.Round(maxHp * Math.Max(0f, Math.Min(1f, fraction))));

        /// <summary>주머니에 들 몫: 이번 원정에 번 양 × 비율(반올림), 지금 가진 것보다 많지 않게.</summary>
        public static int PouchAmount(int gainedThisTrip, int wallet, float share = PouchShare)
        {
            if (gainedThisTrip <= 0 || wallet <= 0 || share <= 0f) return 0;
            int amount = (int)Math.Round(gainedThisTrip * (double)share, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(wallet, amount));
        }

        public static string PouchDropLine(int stones, int gold) => "쓰러진 자리에 주머니를 흘렸다 — " + Amount(stones, gold) + " · 줍기 전에 또 쓰러지면 잃는다";
        public static string PouchTakeLine(int stones, int gold) => "피 묻은 주머니를 되찾았다 — " + Amount(stones, gold);
        public const string PouchLostLine = "앞서 흘린 주머니는 갱도 어딘가로 사라졌다";
        public const string RespawnLine = "피 맛이 남은 채, 말뚝 곁에서 눈을 떴다 — 몸이 무겁다";
        public const string RestLine = "말뚝에 기대 숨을 골랐다 — 체력·물약을 채웠다 (이번 원정 쉬기 끝)";
        public const string RestUsed = "쉬기 — 이번 원정에는 이미 쉬었다";
        public const string RestLabel = "쉬기 — 체력·물약 가득 (원정에 한 번)";

        static string Amount(int stones, int gold)
        {
            if (stones > 0 && gold > 0) return $"강화석 {stones} · 골드 {gold}";
            if (stones > 0) return $"강화석 {stones}";
            return $"골드 {gold}";
        }

        /// <summary>
        /// 주머니 몫을 지갑에서 뺄 때 원정 몫을 어떻게 고칠지(DungeonRoot의 층 시작 값·원정 몫).
        /// 이 층에서 번 것(floorGain)보다 많이 떨구면 넘는 몫(excess)을 원정 몫과 층 시작 값에서 함께 빼야, 떨군 뒤와 되찾은 뒤의 '이번 원정 번 양'이 맞다.
        /// </summary>
        public static int Excess(int dropped, int floorGain) => Math.Max(0, dropped - Math.Max(0, floorGain));
    }
}
