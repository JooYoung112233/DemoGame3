using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 손맛 조절 값. 시험 패널에서 바꾼다. 플레이를 다시 시작해도 에디터를 다시 컴파일하기 전까지 유지된다.
    /// </summary>
    public static class Tuning
    {
        public static bool HitStopEnabled = true;
        public static float HitStopScale = 1f;
        public static float KnockbackScale = 1f;
        public static float ShakeScale = 1f;
        public static bool DamageNumbers = true;
        /// <summary>자동 조준 2차 규칙(직전 대상 유지, 점수, 다가가기). 기획상 M6에 넣는다.</summary>
        public static bool SmartTargeting;
        public static bool Invincible;
        public static bool Sound = true;
        public static float SoundVolume = 0.6f;
        /// <summary>처치하면 맞은 방향으로 날아간다(M0a '시원함' 보강).</summary>
        public static bool KillFling = true;
        public static float KillFlingScale = 1f;
        /// <summary>맞은 자리 파편과 베인 자국.</summary>
        public static bool HitSparks = true;
        /// <summary>한 번에 3마리 이상 처치하면 히트스톱·흔들림, 5마리 이상이면 잠깐 느려짐.</summary>
        public static bool MultiKillJuice = true;
        /// <summary>화면 위 연속 처치 숫자.</summary>
        public static bool KillStreakText = true;
        /// <summary>M0a 값 / 3차 값(적게·강하게: 버팀·무너짐, 강한 멧돼지·궁수, 공격 기회 3/2). 바꾸면 적을 다시 세운다.</summary>
        public static CombatRuleset Ruleset = CombatRuleset.V3;
        /// <summary>3차 값이 너무 질길 때 먼저 내리는 값: 멧돼지 체력 1,600, 버팀 70.</summary>
        public static bool SoftBoar;
        /// <summary>회피 반격: 예고 공격을 구르기로 피하고 1.0초 안 첫 타의 버팀 피해 ×2(3차 초안 선택 규칙).</summary>
        public static bool DodgeCounter;

        public static void ResetToDefaults()
        {
            HitStopEnabled = true;
            HitStopScale = 1f;
            KnockbackScale = 1f;
            ShakeScale = 1f;
            DamageNumbers = true;
            SmartTargeting = false;
            Invincible = false;
            Sound = true;
            SoundVolume = 0.6f;
            KillFling = true;
            KillFlingScale = 1f;
            HitSparks = true;
            MultiKillJuice = true;
            KillStreakText = true;
        }
    }

    public static class Palette
    {
        public static readonly Color Background = Hex(0x15120F);
        public static readonly Color FloorA = Hex(0x2A2520);
        public static readonly Color FloorB = Hex(0x2F2924);
        public static readonly Color Wall = Hex(0x4A433C);
        public static readonly Color Pillar = Hex(0x3E3832);
        public static readonly Color Player = Hex(0xE8E2D6);
        public static readonly Color Rat = Hex(0x7A5636);
        public static readonly Color Boar = Hex(0x9C8530);
        public static readonly Color Archer = Hex(0x3E7C78);
        public static readonly Color WoodDummy = Hex(0x8A6A44);
        public static readonly Color RatDummy = Hex(0x6E5A48);
        /// <summary>바닥 빨강은 공격 예고에만 쓴다.</summary>
        public static readonly Color TelegraphOutline = new Color(0.88f, 0.16f, 0.16f, 0.2f);
        public static readonly Color TelegraphFill = new Color(0.95f, 0.2f, 0.2f, 0.36f);
        public static readonly Color TelegraphLocked = new Color(1f, 0.25f, 0.25f, 0.7f);
        public static readonly Color Swing = new Color(1f, 1f, 1f, 0.42f);
        public static readonly Color Wave = new Color(0.94f, 0.92f, 0.85f, 0.85f);
        public static readonly Color Arrow = Hex(0xD8D0BC);
        public static readonly Color SpawnMarker = new Color(0.81f, 0.78f, 0.7f, 0.55f);
        public static readonly Color Stun = Hex(0xF2E6A0);
        public static readonly Color HealthBar = Hex(0xD9CFB8);
        /// <summary>버팀 막대: 등급색·예고 빨강과 겹치지 않는 옅은 하늘빛 흰색.</summary>
        public static readonly Color Poise = new Color(0.85f, 0.9f, 0.95f, 0.95f);
        public static readonly Color HealOrb = new Color(1f, 0.93f, 0.85f, 1f);
        public static readonly Color Nest = Hex(0x5A4636);
        public static readonly Color Trap = Hex(0xBDB49E);
        public static readonly Color NumberNormal = Color.white;
        public static readonly Color NumberCrit = Hex(0xFFE45C);
        public static readonly Color NumberTaken = Hex(0xFF5A5A);
        public static readonly Color NumberHeal = Hex(0x6EE07A);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }

}
