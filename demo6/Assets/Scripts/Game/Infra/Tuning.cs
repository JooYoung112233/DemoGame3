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
        /// <summary>
        /// 걷기 속도 배율(2026-10-03 사용자 "이속이좀 빠른편이긴하네 둔직한맛이 조금 부족해"). 장비 걸음 5.3 → 약 4.56, 탐험 걸음 6.5 → 약 5.59.
        /// 휘두르기·회오리·검풍 중 걷기는 이 값에 비례해 함께 줄고, 구르기·내딛기·넉백 거리는 그대로다.
        /// </summary>
        public static float MoveSpeedScale = DefaultMoveSpeedScale;
        public const float DefaultMoveSpeedScale = 0.86f;
        /// <summary>걷기 가감속(묵직함): 멈춘 데서 다 빨라지기까지 0.16초, 서기까지 0.10초. 구르기·내딛기·넉백·경직은 지금처럼 바로.</summary>
        public static bool MoveInertia = true;

        // ── 전투 시험장 장비 손잡이(장비 문서 3-4 '전투 시험장 손잡이'). CombatTestRoot가 StatOverrides로 넣는다. 던전은 쓰지 않는다 ──

        /// <summary>무기 종류 고유 치명(장검 +20‰/+100‰, 대검 0/+500‰, 쌍검 +40‰/−200‰) 켜기. 끄면 맨몸 치명 5%/150%.</summary>
        public static bool TestWeaponIntrinsic = true;
        /// <summary>공격 속도(‰, 0~300). 0이면 지금 콤보 시간 그대로.</summary>
        public static int TestAttackSpeedPermille;
        /// <summary>치명 확률·치명 피해 직접 값(‰). 음수면 장비대로(무기 고유 켜기·끄기를 따름).</summary>
        public static int TestCritChancePermille = -1;
        public static int TestCritDamagePermille = -1;
        /// <summary>전설 3종(연쇄 번개·불꽃 발자국·연쇄 폭발, LegendaryEffect 차례) 켜기와 세기(‰, 0~1000).</summary>
        public static readonly bool[] TestLegendOn = new bool[3];
        public static readonly int[] TestLegendRoll = { DefaultLegendRoll, DefaultLegendRoll, DefaultLegendRoll };
        public const int DefaultLegendRoll = 500;
        public const int TestAttackSpeedMax = 300;

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
            MoveSpeedScale = DefaultMoveSpeedScale;
            MoveInertia = true;
            TestWeaponIntrinsic = true;
            TestAttackSpeedPermille = 0;
            TestCritChancePermille = -1;
            TestCritDamagePermille = -1;
            for (int i = 0; i < TestLegendOn.Length; i++)
            {
                TestLegendOn[i] = false;
                TestLegendRoll[i] = DefaultLegendRoll;
            }
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

        // ── 던전 다크 판타지 색(기획/다크판타지-분위기-1차.md '색·빛·땅·벽'). 던전에서만 쓴다. 위 전투 시험장 색은 그대로 둔다. ──

        /// <summary>바닥 흙 어두운 끝·밝은 끝. 평균이 바둑판(FloorA/B)보다 조금 밝아 색 보정(노출 −0.15, 대비 +17) 뒤에도 등잔 빛 안에서 결이 보이고, 벽·바위(평균 Wall)보다는 어둡다.</summary>
        public static readonly Color DungeonDirtDark = Hex(0x201A15);
        public static readonly Color DungeonDirtLight = Hex(0x4E4236);
        /// <summary>칸마다 다른 젖은 진흙 자리.</summary>
        public static readonly Color DungeonMud = Hex(0x1C1511);
        /// <summary>벽 돌 블록(평균이 Wall과 같아 벽 파편 색과 맞는다)·모르타르.</summary>
        public static readonly Color DungeonStone = Hex(0x4A433C);
        public static readonly Color DungeonMortar = Hex(0x1C1916);
        /// <summary>통로·방을 채운 거친 바위와 그 틈. 틈을 빼고 평균이 벽(Wall)과 비슷해 바닥보다 밝게 읽힌다(걸을 수 없는 곳).</summary>
        public static readonly Color DungeonRock = Hex(0x544C44);
        public static readonly Color DungeonRockCrack = Hex(0x12100E);
        /// <summary>나무 버팀목 기둥(밝은 결·어두운 나이테).</summary>
        public static readonly Color DungeonTimber = Hex(0x56402A);
        public static readonly Color DungeonTimberDark = Hex(0x2A1D12);
        /// <summary>바닥 장식: 뼈·해골, 녹슨 사슬, 썩은 나무, 돌무더기, 오래된 핏자국(피 범위 #4A0606~#8A1010의 어두운 끝), 거미줄.</summary>
        public static readonly Color DungeonBone = Hex(0xA0957F);
        public static readonly Color DungeonRust = Hex(0x6E3B1C);
        public static readonly Color DungeonRottenWood = Hex(0x4F3B27);
        public static readonly Color DungeonRubble = Hex(0x5C554C);
        public static readonly Color DungeonOldBlood = new Color(0x4A / 255f, 0x06 / 255f, 0x06 / 255f, 0.72f);
        public static readonly Color DungeonCobweb = new Color(0.84f, 0.82f, 0.78f, 0.55f);
        /// <summary>던전 플레이어 도형: 밝은 베이지(Player) 대신 어두운 망토색 몸 + 뼈색 방향 표시.</summary>
        public static readonly Color DungeonPlayer = Hex(0x6B5F51);
        public static readonly Color DungeonPlayerMark = Hex(0xBFB39A);
        /// <summary>주황 횃불빛. 채도 0.4로 전설 주황(#F2994A, 채도 0.69)보다 낮다.</summary>
        public static readonly Color TorchLight = new Color(1f, 0.82f, 0.6f);
        /// <summary>빛 밖 전역 빛 색: 차가운 푸른 회색(밝기 0.08은 DungeonLighting.AmbientDark 그대로). 상대 휘도 0.73으로 예전 색(0.75)과 거의 같아 벽 윤곽 밝기를 지킨다.</summary>
        public static readonly Color AmbientCold = new Color(0.62f, 0.74f, 0.98f);
        /// <summary>불티(빛 무시): 막 튄 뜨거운 색 → 식어 가는 주황.</summary>
        public static readonly Color EmberHot = new Color(1f, 0.86f, 0.55f, 1f);
        public static readonly Color EmberCool = new Color(1f, 0.42f, 0.12f, 1f);
        /// <summary>등잔 빛 속 먼지(빛을 받음)·천장에서 떨어지는 흙먼지.</summary>
        public static readonly Color DustMote = new Color(0.88f, 0.82f, 0.72f, 0.5f);
        public static readonly Color CeilingDust = new Color(0.55f, 0.5f, 0.43f, 1f);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }

}
