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
        /// <summary>
        /// 회피 반격: 예고 공격을 구르기로 피하고 1.0초 안 첫 타의 버팀 피해 ×2(3차 초안 선택 규칙).
        /// 기본값은 CounterRule.DefaultOn(전투·보스·무기 다듬기 1차 결정 8-1 #5 '켜기'를 꾸러미 ⑤가 넣는다).
        /// </summary>
        public static bool DodgeCounter = CounterRule.DefaultOn;
        /// <summary>
        /// 걷기 속도 배율(2026-10-03 사용자 "이속이좀 빠른편이긴하네 둔직한맛이 조금 부족해" → 0.86, 2026-10-04 결정 ① B → 0.80).
        /// B에서 장비 걸음 5.3 → 4.24, 아는 길 6.5 → 5.20. 시험 패널 걸음 단추(ExplorePace.Options)가 이 값과 EnemyMoveScale을 함께 바꾼다.
        /// 휘두르기·회오리·검풍 중 걷기는 이 값에 비례해 함께 줄고, 구르기·내딛기·넉백 거리는 그대로다.
        /// </summary>
        public static float MoveSpeedScale = DefaultMoveSpeedScale;
        /// <summary>걷기 배율 기본값(전투·보스·무기 다듬기 1차 1-2, 결정 ① = B 0.80). 값은 ExplorePace가 정한다.</summary>
        public const float DefaultMoveSpeedScale = Demo6.Core.Dungeon.ExplorePace.DefaultPlayerScale;
        /// <summary>
        /// 적 걷기 배율(1-2 B = 0.93 = 0.80 ÷ 0.86, 나와 굴쥐의 속도 차 1.085를 지킴). Enemy.MoveSpeed가 곱한다. 돌진·뒤로 뛰기·화살·검풍은 받지 않는다.
        /// </summary>
        public static float EnemyMoveScale = DefaultEnemyMoveScale;
        public const float DefaultEnemyMoveScale = Demo6.Core.Dungeon.ExplorePace.DefaultEnemyScale;
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

        // ── 전투·보스·무기 다듬기 1차 손잡이(기획/전투-보스-무기-다듬기-1차.md). 숫자는 가안이고 기본값은 각 규칙(Core)이 정한다 ──
        // 계약: 이 파일은 꾸러미 ⑧이 소유한다. 다른 꾸러미는 값을 읽고 시험 패널에서 바꾸기만 하고, 새 손잡이가 필요하면 자기 파일의 정적 값으로 둔다.

        /// <summary>웅크리기(결정 ③): 걸음 × 0.55, 발소리·소음 반경 × 0.3, 시야 반각 40°·반경 10·몸 둘레 2.0.</summary>
        public static float CrouchMoveScale = Demo6.Core.Dungeon.CrouchRules.MoveScale;
        public static float CrouchNoiseScale = Demo6.Core.Dungeon.CrouchRules.NoiseScale;
        public static float CrouchConeHalfAngle = Demo6.Core.Dungeon.CrouchRules.ConeHalfAngle;
        public static float CrouchViewRadius = Demo6.Core.Dungeon.CrouchRules.ViewRadius;
        public static float CrouchNearRadius = Demo6.Core.Dungeon.CrouchRules.NearRadius;

        /// <summary>무너짐 공급원은 하나씩 켠다(4-3): 벽 박기, 무리 공포, 무너짐 처형, 기습 처형. 처형 회복(3%)은 기본 끔.</summary>
        public static bool WallSlamOn = WallSlamRule.DefaultOn;
        public static bool FearOn = FearRule.DefaultOn;
        public static bool ExecutionOn = ExecutionRule.DefaultOn;
        public static bool AmbushExecutionOn = ExecutionRule.AmbushDefaultOn;
        public static bool ExecutionHealOn = ExecutionRule.HealDefaultOn;
        /// <summary>무너짐 처형 문턱(보통 30%·무거움 20%·정예 12%).</summary>
        public static float ExecuteThresholdMedium = ExecutionRule.ThresholdMedium;
        public static float ExecuteThresholdHeavy = ExecutionRule.ThresholdHeavy;
        public static float ExecuteThresholdElite = ExecutionRule.ThresholdElite;
        /// <summary>잠든 무리 '뒤척임' 확률(2~4초마다, 기본 0).</summary>
        public static float SleeperTurnChance = ExecutionRule.SleeperTurnDefault;

        /// <summary>보스 시험(3-10 시험 패널): 2단계 켜기, 패턴 강제(-1 = 없음, BossPattern 차례), 재도전 때 물약 가득.</summary>
        public static bool BossPhase2On = true;
        public static int BossForcePattern = -1;
        public static bool BossRetryFullPotions = true;

        /// <summary>
        /// 백어택·헤드어택(PositionalHitRule, 2026-10-04 사용자 요청): 등 뒤 ±60°에서 맞히면 피해 +‰·치명 +‰(상한 밖, 단검 등 찌르기와는 큰 쪽 하나),
        /// 정면 ±45° 치명이면 버팀 × 배율. 전투 시험장 F1에서 켜고 끄고 바꾼다. 둥지·허수아비는 듣지 않는다.
        /// </summary>
        public static bool BackAttackOn = PositionalHitRule.BackDefaultOn;
        public static bool HeadAttackOn = PositionalHitRule.HeadDefaultOn;
        public static int BackAttackDamagePermille = PositionalHitRule.BackDamageBonusPermille;
        public static int BackAttackCritPermille = PositionalHitRule.BackCritBonusPermille;
        public static float HeadAttackPoiseScale = PositionalHitRule.HeadPoiseScale;
        /// <summary>'백어택'·'헤드어택' 글자(피해 숫자 위). 끄면 규칙만 돈다.</summary>
        public static bool PositionalHitText = true;

        // ── 세 무기·오른쪽 클릭·소켓 1차 손잡이(기획/세-무기-우클릭-소켓-1차.md 5-5·5-9·6-7). 기본값은 Core 규칙이 정한다 ──
        // 계약: 읽는 곳 PlayerController(꾸러미 ③), 바꾸는 곳 전투 시험 패널 CombatHud(꾸러미 ⑤). 모두 ResetToDefaults에 있다.

        /// <summary>방패 막기 켜기(끄면 한손검과 방패 오른쪽 클릭이 아무 일도 하지 않음).</summary>
        public static bool GuardEnabled = true;
        /// <summary>패링 창(초, 0.10~0.30, 기본 ShieldRule.ParryWindow 0.18).</summary>
        public static float ParryWindow = ShieldRule.ParryWindow;
        /// <summary>막은 일반 타(근접·멧돼지 돌진·꿰뚫는 화살) 피해 배율(0.0~0.5, 기본 0.25).</summary>
        public static float GuardDamageScale = DefaultGuardDamageScale;
        public const float DefaultGuardDamageScale = 0.25f;
        /// <summary>시험: 낀 무기에 버팀 룬이 있는 것처럼(대검 기 모으기·놓아 베기가 안 끊김).</summary>
        public static bool TestSuperArmorRune;
        /// <summary>시험: 버팀 룬을 세 행동(막기·기 모으기·난사) 모두에 적용(기본 꺼짐).</summary>
        public static bool SuperArmorAllActs;
        /// <summary>끊김 최소 피해(최대 체력 대비 ‰, 기본 0 = 문턱 없음). 답답하면 30(3%)을 켠다(8-1 #7).</summary>
        public static int WeaponActInterruptMinPermille;
        /// <summary>방어 게이지 최대치(50~200, 기본 ShieldRule.GuardMax 100). 깎는 양은 그대로라 200이면 굴쥐 물기 8번에 깨진다(0-3의 28).</summary>
        public static float GuardMeterMax = ShieldRule.GuardMax;
        /// <summary>방어 게이지 회복 빠르기 배율(0.25~3, 기본 1 = 쉴 때 초당 50·막는 중 초당 10). 쉬는 시간 0.5·1.0초는 그대로.</summary>
        public static float GuardRegenScale = 1f;
        /// <summary>패링 성공 때 돌려받는 방어 게이지(0~50, 기본 ShieldRule.ParryRefund 20).</summary>
        public static float GuardParryRefund = ShieldRule.ParryRefund;
        /// <summary>패링 피해(공격력 %, 0~100, 기본 ShieldRule.ParryDamagePercent 30). 0이면 피해 없음.</summary>
        public static float ParryDamagePercent = ShieldRule.ParryDamagePercent;
        /// <summary>패링 그로기(버팀) 깎기: 일반 적 몫(0~1, 기본 ShieldRule.ParryPoiseFraction 0.5)과 정예 몫(기본 ShieldRule.ParryElitePoiseFraction 0.35). 보스 휩쓸기 6%는 그대로.</summary>
        public static float ParryPoiseFraction = ShieldRule.ParryPoiseFraction;
        public static float ParryElitePoiseFraction = ShieldRule.ParryElitePoiseFraction;

        // ── 재화 쓸 곳 1차 손잡이(기획/재화-쓸-곳-1차.md 5-2·14장). 기본값은 Core BagRules가 정한다. 읽는 곳 Inventory, 바꾸는 곳 던전 F1 ──

        /// <summary>㉡ 저절로 분해(일반): 끼어도 나아지지 않는 일반 +0 장비는 저절로 줍기·떠날 때 거두기에서 그 자리에서 강화석으로 분해한다(기본 켬).</summary>
        public static bool AutoSalvageCommon = Demo6.Core.Loot.BagRules.AutoSalvageCommonDefault;
        /// <summary>㉠ 저절로 줍기 여유 칸(0 또는 BagRules.AutoReserveSlots = 3): 가방이 칸 − 이 수에 닿으면 저절로 줍기를 멈춘다. F 줍기는 그대로(기본 0).</summary>
        public static int AutoPickupReserve = 0;

        public static void ResetToDefaults()
        {
            AutoSalvageCommon = Demo6.Core.Loot.BagRules.AutoSalvageCommonDefault;
            AutoPickupReserve = 0;
            GuardEnabled = true;
            ParryWindow = ShieldRule.ParryWindow;
            GuardDamageScale = DefaultGuardDamageScale;
            GuardMeterMax = ShieldRule.GuardMax;
            GuardRegenScale = 1f;
            GuardParryRefund = ShieldRule.ParryRefund;
            ParryDamagePercent = ShieldRule.ParryDamagePercent;
            ParryPoiseFraction = ShieldRule.ParryPoiseFraction;
            ParryElitePoiseFraction = ShieldRule.ParryElitePoiseFraction;
            TestSuperArmorRune = false;
            SuperArmorAllActs = false;
            WeaponActInterruptMinPermille = 0;
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
            EnemyMoveScale = DefaultEnemyMoveScale;
            // 걸음 비교안 '지금'이 켜는 '빠른 걸음 모든 칸'도 함께 되돌린다(던전 F1 '손맛 기본값으로'와 같음).
            ExploreWalk.FastEverywhere = false;
            MoveInertia = true;
            DodgeCounter = CounterRule.DefaultOn;
            CrouchMoveScale = Demo6.Core.Dungeon.CrouchRules.MoveScale;
            CrouchNoiseScale = Demo6.Core.Dungeon.CrouchRules.NoiseScale;
            CrouchConeHalfAngle = Demo6.Core.Dungeon.CrouchRules.ConeHalfAngle;
            CrouchViewRadius = Demo6.Core.Dungeon.CrouchRules.ViewRadius;
            CrouchNearRadius = Demo6.Core.Dungeon.CrouchRules.NearRadius;
            WallSlamOn = WallSlamRule.DefaultOn;
            FearOn = FearRule.DefaultOn;
            ExecutionOn = ExecutionRule.DefaultOn;
            AmbushExecutionOn = ExecutionRule.AmbushDefaultOn;
            ExecutionHealOn = ExecutionRule.HealDefaultOn;
            ExecuteThresholdMedium = ExecutionRule.ThresholdMedium;
            ExecuteThresholdHeavy = ExecutionRule.ThresholdHeavy;
            ExecuteThresholdElite = ExecutionRule.ThresholdElite;
            SleeperTurnChance = ExecutionRule.SleeperTurnDefault;
            BossPhase2On = true;
            BossForcePattern = -1;
            BossRetryFullPotions = true;
            BackAttackOn = PositionalHitRule.BackDefaultOn;
            HeadAttackOn = PositionalHitRule.HeadDefaultOn;
            BackAttackDamagePermille = PositionalHitRule.BackDamageBonusPermille;
            BackAttackCritPermille = PositionalHitRule.BackCritBonusPermille;
            HeadAttackPoiseScale = PositionalHitRule.HeadPoiseScale;
            PositionalHitText = true;
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
