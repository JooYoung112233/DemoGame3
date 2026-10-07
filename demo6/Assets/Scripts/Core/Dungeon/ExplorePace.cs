using System;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 걸음 비교안 한 줄(전투·보스·무기 다듬기 1차 1-2 '비교안'): 이름, 플레이어 걷기 배율, 적 걷기 배율,
    /// 빠른 걸음을 처음 가는 칸에서도 쓰는가('지금' = 예전 탐험 걸음, 나머지는 아는 길에서만).
    /// </summary>
    public readonly struct PaceOption
    {
        public readonly string Name;
        public readonly float PlayerScale;
        public readonly float EnemyScale;
        public readonly bool FastEverywhere;

        public PaceOption(string name, float playerScale, float enemyScale, bool fastEverywhere = false)
        {
            Name = name;
            PlayerScale = playerScale;
            EnemyScale = enemyScale;
            FastEverywhere = fastEverywhere;
        }
    }

    /// <summary>
    /// 걸음과 어둠(기획/전투-보스-무기-다듬기-1차.md 1장, 2026-10-04 결정 ① = B). 엔진을 모르는 상수와 공정 점검 식만 둔다.
    /// Tuning 기본값(MoveSpeedScale·EnemyMoveScale), 시험 패널 걸음 단추(M0a·지금·A·B·C), DungeonLighting·VisionSystem·DungeonPostFx가 여기서 읽는다.
    /// 계약(꾸러미 ① 걸음·어둠·웅크리기가 채우고 ExplorePaceTests ①~⑧로 지킨다): 걸음 이름·단위는 바꾸지 않는다.
    /// 기본값(DefaultPlayerScale·DefaultEnemyScale)은 B(0.80 / 0.93)다(계약 단계의 '지금' 0.86 / 1.0에서 바꿈).
    /// 어둠은 2026-10-04 사용자 결정("주변 등 2번째처럼 유지")으로 탐험·싸움 빛 나누기를 없앴다: 등잔은 늘 같은 반경(지금 7.5 / 11.0, 1-3 '바뀜')이다.
    /// </summary>
    public static class ExplorePace
    {
        // ── 1-2 걸음 ──

        /// <summary>추천안 B: 나 0.80, 적 0.93(함께 7% 느리게, 결정 ①).</summary>
        public const float PlayerScaleB = 0.80f;
        public const float EnemyScaleB = 0.93f;

        /// <summary>Tuning.MoveSpeedScale 기본값 = B 0.80(결정 ①, 예전 0.86).</summary>
        public const float DefaultPlayerScale = PlayerScaleB;
        /// <summary>Tuning.EnemyMoveScale 기본값 = B 0.93(= 0.80 ÷ 0.86, 예전 1.0). 나와 굴쥐의 속도 차 1.085를 지킨다.</summary>
        public const float DefaultEnemyScale = EnemyScaleB;

        /// <summary>
        /// 시험 패널 단추 차례(1-2 비교안 표). M0a 값 1.0/1.0, 지금 0.86/1.0(빠른 걸음이 처음 가는 칸에서도 켜짐 = 예전 탐험 걸음),
        /// A 0.86/1.0(빠른 걸음은 아는 길에서만), B 0.80/0.93, C 0.74/0.86.
        /// </summary>
        public static readonly PaceOption[] Options =
        {
            new PaceOption("M0a 값", 1f, 1f),
            new PaceOption("지금", 0.86f, 1f, true),
            new PaceOption("A", 0.86f, 1f),
            new PaceOption("B", PlayerScaleB, EnemyScaleB),
            new PaceOption("C", 0.74f, 0.86f),
        };

        /// <summary>'아는 길'(다시 들어간 칸, 12유닛 안 깨어 있는 적 없음, 조용한 지 3초) 걸음(배율 곱하기 전, 예전 탐험 걸음 6.5).</summary>
        public const float KnownPathSpeed = 6.5f;
        /// <summary>
        /// 깨어 있는 적을 찾는 반경 12. 아는 길(ExploreWalk.AwakeEnemyNear)과 싸움 판정이 켜지는 거리(DungeonLighting.InCombat,
        /// 벽에 가리지 않은 적만, 끄는 거리는 CombatExitRange 14)가 함께 쓴다.
        /// </summary>
        public const float EnemyRange = 12f;
        /// <summary>조용한 상태가 이만큼 이어져야 아는 길이 켜진다.</summary>
        public const float QuietSeconds = 3f;

        /// <summary>
        /// 서 있을 때 시야(VisionSystem, 3차 초안 2-4 그대로): 바라보는 쪽 부채꼴 반각 65°(130°)·반경 14, 몸 둘레 늘 보는 반경 2.5.
        /// 웅크리면 CrouchRules(40° / 10 / 2.0)로 바뀐다. 시험(Core만 참조)이 공정 확인 ④에 쓰려고 여기 둔다.
        /// </summary>
        public const float ViewRadius = 14f;
        public const float NearRadius = 2.5f;
        public const float ConeHalfAngle = 65f;

        /// <summary>공정 점검용 기획 속도(배율 곱하기 전): 시작 장비 걸음 5.3, 굴쥐 4.2, 궁수 3.0, 멧돼지 2.2.</summary>
        public const float EquipWalk = 5.3f;
        public const float RatSpeed = 4.2f;
        public const float ArcherSpeed = 3.0f;
        public const float BoarSpeed = 2.2f;

        // ── 1-3 어둠(2026-10-04 사용자 결정으로 바뀜: 등잔은 늘 싸움 빛) ──
        // 1차 초안의 '탐험 4.0 / 6.5 ↔ 싸움 6.0 / 9.0' 섞기는 '12유닛 안에 깬 적이 있나'가 바뀔 때마다(회피·한 방 처치·벽 너머 무리)
        // 등잔이 0.25초 만에 커졌다 1.4초에 걸쳐 줄어 깜빡임으로 보였다. 사용자 원문 "주변 등 2번째처럼 유지가 안된다 깜빡거린다".
        // 그래서 등잔은 늘 같은 반경(지금 7.5 / 11.0)이고, 어두운 분위기는 빛 밖 밝기 0.06·덮개 0.70·비네트 0.42·잡티 0.20(어두운 쪽 값, 섞지 않음)이 맡는다.

        /// <summary>빛 밖 전역 밝기(예전 0.08 → 0.06). 색 띠가 보이면 0.07.</summary>
        public const float AmbientDark = 0.06f;
        /// <summary>
        /// 내 등잔 밝은 / 흐린 반경: 늘 7.5 / 11.0. 싸움 여부로 바꾸지 않는다.
        /// 2026-10-04 사용자 "등잔 크기 좀더키워줘 잘보이게"로 6.0 / 9.0에서 키웠다. 서 있는 시야 반경 14 안이다.
        /// </summary>
        public const float LampBright = 7.5f;
        public const float LampDim = 11.0f;
        /// <summary>가 본 곳 덮개(예전 0.62 → 0.70).</summary>
        public const float VeilAlpha = 0.70f;
        /// <summary>비네트·필름 잡티 세기: 늘 0.42 / 0.20(1차 초안의 탐험 값). 싸움 섞기로 바꾸지 않는다.</summary>
        public const float VignetteIntensity = 0.42f;
        public const float GrainIntensity = 0.20f;
        /// <summary>심지 단계(RadiusBonus)가 더하는 양: 1단계 밝은 +1.0 / 흐린 +1.5, 2단계 +2.0 / +3.0(그대로).</summary>
        public static readonly float[] WickBright = { 0f, 1.0f, 2.0f };
        public static readonly float[] WickDim = { 0f, 1.5f, 3.0f };

        /// <summary>등잔 밝은 반경(일렁임을 뺀 값). 심지 단계는 0~2로 자른다.</summary>
        public static float BrightRadius(int wick) => LampBright + WickBright[Wick(wick)];

        /// <summary>등잔 흐린 반경(시야 판정 '빛 안'의 기준).</summary>
        public static float DimRadius(int wick) => LampDim + WickDim[Wick(wick)];

        /// <summary>심지 단계 수(0·1·2).</summary>
        public static int WickLevels => WickBright.Length;

        static int Wick(int wick) => Math.Max(0, Math.Min(WickBright.Length - 1, wick));

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ── 싸움 판정(DungeonLighting.InCombat·CombatBlend) ──
        // 빛 반경은 바꾸지 않는다. 바닥 장비 이름표·빛기둥을 옅게 하고 탐험 기록의 전투 몫을 잰다.
        // 판정이 짧게 오가도 출렁이지 않게 켜는 거리 12 / 끄는 거리 14로 여유를 두고, 마지막으로 본 뒤 3초 켜 둔다(벽에 가린 적은 세지 않음).

        /// <summary>켜진 싸움 판정을 끄는 거리(켜는 거리는 EnemyRange 12). 경계에 선 적 때문에 오가지 않게.</summary>
        public const float CombatExitRange = 14f;
        /// <summary>보이는 깬 적이 없어진 뒤에도 이만큼(unscaled 초) 싸움으로 둔다(회피 귀환·한 방 처치·무리가 0.8초 늦게 깨는 사이).</summary>
        public const float CombatHoldSeconds = 3f;
        /// <summary>싸움 섞기 빠르기(초당): 켜질 때 4(0.25초), 꺼질 때 0.7(약 1.4초). 이름표 옅게 하기에만 쓴다.</summary>
        public const float BlendInPerSecond = 4f;
        public const float BlendOutPerSecond = 0.7f;

        /// <summary>깬 적을 찾는 반경: 싸움이 꺼져 있으면 12, 켜져 있으면 14.</summary>
        public static float CombatRange(bool inCombat) => inCombat ? CombatExitRange : EnemyRange;

        /// <summary>
        /// 싸움 판정 한 걸음(now는 unscaled 초): 보이는 깬 적이 있으면 holdUntil을 now + 3초로 미루고 켠다.
        /// 없으면 holdUntil이 지날 때까지 켜 둔다. holdUntil의 처음 값은 음수(또는 0)로 둔다.
        /// </summary>
        public static bool StepCombatHold(bool enemyNear, float now, ref float holdUntil)
        {
            if (enemyNear) holdUntil = now + CombatHoldSeconds;
            return enemyNear || now < holdUntil;
        }

        /// <summary>
        /// 싸움 섞기 한 걸음(unscaled 초 dt): 싸움이면 초당 4로 1까지(0.25초), 아니면 초당 0.7로 0까지(약 1.4초).
        /// </summary>
        public static float StepCombatBlend(float blend, bool inCombat, float dt)
        {
            if (dt <= 0f) return Clamp01(blend);
            return inCombat
                ? Math.Min(1f, blend + BlendInPerSecond * dt)
                : Math.Max(0f, blend - BlendOutPerSecond * dt);
        }

        // ── 바라보는 쪽(VisionSystem 부채꼴) ──
        // 카메라가 몸을 화면 가운데에 두므로 커서가 몸 가까이 있으면 손이 조금만 움직여도 커서 방향이 뒤집힌다.
        // 0.6 안으로 들어오면 방향을 붙잡고, 1.0 밖으로 나가야 다시 커서를 따른다.

        /// <summary>커서가 몸에서 이 거리 안으로 들어오면 부채꼴 방향을 붙잡는다.</summary>
        public const float CursorHoldEnter = 0.6f;
        /// <summary>붙잡은 방향은 커서가 이 거리 밖으로 나가야 놓는다.</summary>
        public const float CursorHoldExit = 1.0f;

        /// <summary>커서가 몸 가까이(방향을 붙잡음)인가. 붙잡지 않았으면 0.6 안에서 켜고, 붙잡았으면 1.0 밖에서 끈다. NaN이면 그대로.</summary>
        public static bool CursorNear(bool wasNear, float distance)
        {
            if (float.IsNaN(distance)) return wasNear;
            return wasNear ? distance <= CursorHoldExit : distance < CursorHoldEnter;
        }

        /// <summary>
        /// CursorNear에 넣을 거리: 몸에서 잰 거리와 화면 가운데(카메라 중심)에서 잰 거리 중 작은 쪽.
        /// 구르기(3.5 / 0.22초)·내딛기·넉백에 카메라(따라오기 12)가 늦으면 화면 가운데에 둔 커서가 몸에서 최대 약 1.2 떨어진다.
        /// 마우스를 움직이지 않으면 화면 가운데 거리는 그대로라 이 동안에도 붙잡은 방향이 풀리지 않는다. 하나가 NaN이면 다른 쪽을 쓴다.
        /// </summary>
        public static float CursorNearDistance(float fromBody, float fromCenter)
        {
            if (float.IsNaN(fromBody)) return fromCenter;
            if (float.IsNaN(fromCenter)) return fromBody;
            return Math.Min(fromBody, fromCenter);
        }

        // ── 공정 확인 기준값(ExplorePaceTests ①~⑧) ──

        /// <summary>② 잠든 적 앞 감지 6 + 여유 0.5 ≤ 등잔 흐린 반경.</summary>
        public const float SleepDetectFront = 6f;
        public const float DetectMargin = 0.5f;
        /// <summary>④ 시야 반경 ≥ 눈 두 점 12, 화살 사거리 12.</summary>
        public const float EyesRange = 12f;
        public const float ArrowRange = 12f;
        /// <summary>⑤ 등잔 밝은 반경 &gt; 근접 최대 사거리 3.5(장검 찌르기 3.4, 대검 내려찍기 1.3 + 2.2).</summary>
        public const float MeleeMaxReach = 3.5f;
        /// <summary>⑥ 적이 깨는 시간 0.5초, 궁수 조준 0.8초. 등잔이 처음부터 싸움 반경이라 빛이 커지기를 기다리지 않는다.</summary>
        public const float WakeDelay = 0.5f;
        public const float ArcherAim = 0.8f;
        /// <summary>① 모든 안에서 나 ÷ 굴쥐 ≥ 1.05.</summary>
        public const double MinPlayerOverRat = 1.05;

        /// <summary>나 ÷ 굴쥐 = (5.3 × 플레이어 배율) ÷ (4.2 × 적 배율). B는 1.085.</summary>
        public static double PlayerOverRat(float playerScale, float enemyScale) =>
            enemyScale <= 0f ? double.PositiveInfinity : EquipWalk * (double)playerScale / (RatSpeed * (double)enemyScale);

        /// <summary>싸움 섞기가 다 켜지는 시간(초) = 1 ÷ 켜질 때 빠르기(0.25).</summary>
        public static float CombatBlendInSeconds => 1f / BlendInPerSecond;

        /// <summary>싸움 섞기가 다 꺼지는 시간(초) = 1 ÷ 꺼질 때 빠르기(약 1.43). 붙잡아 두는 3초 뒤부터 센다.</summary>
        public static float CombatBlendOutSeconds => 1f / BlendOutPerSecond;

        /// <summary>아는 길 실제 걸음 = 6.5 × 플레이어 배율(B 5.20).</summary>
        public static float KnownPathScaled(float playerScale) => KnownPathSpeed * playerScale;
    }
}
