using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 3차 예고 규칙(기획/탐험중심-전환-3차-초안.md 3-3 표 아래 '예고 규칙'): 맞을 한 방이 그 층 기준 플레이어 최대 체력에서 차지하는 몫으로
    /// 최소 예고 시간을 정한다. 5% 이하 0.25초, 5~10% 0.4초, 10~16% 0.6초(예고 시작 소리 필수), 16~20% 0.8초, 20% 넘음 0.9초.
    /// 처음에는 보스(BossRules)에만 있던 계산을 여기로 옮겨 일반 적도 함께 쓴다(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q6):
    /// 멧돼지 돌진·다시 조준·머리치기·뒷발, 궁수 꿰뚫는 화살, 가시 덫. 쓰는 예고 = 바탕 예고와 최소 예고 가운데 긴 쪽(판정 거리·피해는 그대로).
    /// 기준 플레이어는 보스와 같은 BossRules.ReferencePlayerHp·ReferencePlayerDefense(그 층 권장 레벨 + 기준 장비, 2층 체력 2,640·방어 120).
    /// 한 방은 굴림 평균(1.0)으로 센다. 일반·정예는 문서 상한이 20%라 0.9초 칸은 보스 몫이다. 정예 공격 ×1.18(Enemy.EliteAttackScale)로 일반·정예는 20%를 넘지 않는다
    /// (가장 큰 값 2층 정예 돌충이 돌진 19.8%). 그래도 넘는 한 방이 생기면 같은 0.9초를 준다.
    /// </summary>
    public static class TelegraphRule
    {
        /// <summary>이 몫을 넘는 한 방은 예고 시작 소리가 있어야 한다(10~16% '소리 필수', 그 위도 포함).</summary>
        public const double SoundFraction = 0.10;

        /// <summary>한 방 몫(플레이어 최대 체력 대비) → 최소 예고 시간(초). 경계값은 아래 칸(5% 정확히는 0.25초).</summary>
        public static float MinSeconds(double hitFractionOfPlayerHp)
        {
            if (hitFractionOfPlayerHp > 0.20) return 0.9f;
            if (hitFractionOfPlayerHp > 0.16) return 0.8f;
            if (hitFractionOfPlayerHp > SoundFraction) return 0.6f;
            if (hitFractionOfPlayerHp > 0.05) return 0.4f;
            return 0.25f;
        }

        /// <summary>
        /// 공격력 attack(층·정예 배율을 이미 곱한 값)의 percent% 한 방이 그 층 기준 플레이어 최대 체력에서 차지하는 몫(방어 반영, 굴림 1.0).
        /// 2층 멧돼지 돌진(공격 496, 100%) 16.8%, 머리치기·뒷발(60%) 10.1%, 궁수 꿰뚫는 화살(186, 200%) 12.6%, 가시 덫(186, 100%) 6.3%.
        /// </summary>
        public static double HitFraction(double attack, double percent, int floor) =>
            attack * percent / 100.0 * DamageMath.DefenseFactor(BossRules.ReferencePlayerDefense(floor)) / BossRules.ReferencePlayerHp(floor);

        /// <summary>쓸 예고 시간 = 바탕 예고(정예 +0.1초는 부르는 쪽이 더해 넘김)와 그 층 최소 예고 가운데 긴 쪽.</summary>
        public static float Seconds(float baseSeconds, double attack, double percent, int floor) =>
            Math.Max(baseSeconds, MinSeconds(HitFraction(attack, percent, floor)));

        /// <summary>예고 시작 소리가 필요한가(한 방이 기준 체력의 10%를 넘음).</summary>
        public static bool NeedsSound(double hitFractionOfPlayerHp) => hitFractionOfPlayerHp > SoundFraction;

        /// <summary>공격력·배율·층으로 본 NeedsSound.</summary>
        public static bool NeedsSound(double attack, double percent, int floor) => NeedsSound(HitFraction(attack, percent, floor));
    }
}
