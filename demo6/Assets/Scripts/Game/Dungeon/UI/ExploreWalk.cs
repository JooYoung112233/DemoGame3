using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 아는 길(전투·보스·무기 다듬기 1차 1-2, 예전 3차 초안 2-8 '탐험 걸음'): 이미 지나온 칸으로 다시 들어가
    /// 12유닛 안에 깨어 있는 적이 없고 조용한 지 3초가 지나면 이동 6.5(× 걷기 배율 0.80 = 5.20).
    /// 처음 들어서는 칸(DungeonEvents.CellEntered first = true)에서는 바로 끈다 — 새 칸에 들어서는 순간 느려진다.
    /// 공격·스킬을 쓰거나 맞으면(PlayerController.LastCombatActionTime) 바로 장비 속도로 돌아가고, 웅크리면 켜지지 않는다.
    /// 매판 새 탐험에서는 칸이 원정마다 새로 생기므로 같은 원정 안에서 되돌아갈 때(말뚝까지 가기, 곡괭이 들고 되돌아가기)만 빨라진다.
    /// </summary>
    public sealed class ExploreWalk : MonoBehaviour
    {
        /// <summary>아는 길 이동 속도(1-2, 배율 곱하기 전 기획 숫자 6.5).</summary>
        public const float WalkSpeed = ExplorePace.KnownPathSpeed;
        /// <summary>배율을 곱한 실제 아는 길 걸음 속도(시험 패널 표시용, B 5.20).</summary>
        public static float ScaledSpeed => WalkSpeed * Tuning.MoveSpeedScale;
        /// <summary>깨어 있는 적을 찾는 반경(아는 길 12, 싸움 판정 DungeonLighting.InCombat이 켜지는 거리도 12).</summary>
        public const float EnemyRange = ExplorePace.EnemyRange;
        /// <summary>조용한 상태가 이만큼 이어져야 켜진다.</summary>
        public const float QuietSeconds = ExplorePace.QuietSeconds;

        /// <summary>
        /// 시험 패널 걸음 단추 '지금'(1-2 비교안): 처음 가는 칸에서도 빠른 걸음을 켠다(예전 탐험 걸음). 기본 끔(아는 길에서만).
        /// </summary>
        public static bool FastEverywhere;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FastEverywhere = false;
            Instance = null;
        }

        public static ExploreWalk Instance { get; private set; }

        /// <summary>지금 아는 길 걸음인가(화면 '아는 길' 표시).</summary>
        public bool Active { get; private set; }
        /// <summary>지금 칸이 이번 원정에서 다시 들어온 칸인가(마지막 CellEntered의 first = false).</summary>
        public bool KnownCell { get; private set; }

        /// <summary>근처에 깨어 있는 적이 없어진 시각(게임 시간). 적이 있으면 -1.</summary>
        float _quietSince = -1f;

        void Awake()
        {
            Instance = this;
            // DungeonRoot.Awake가 사건 구독을 비운 뒤 이 컴포넌트를 붙이므로 여기서 구독한다.
            DungeonEvents.CellEntered += OnCellEntered;
        }

        void OnDestroy()
        {
            DungeonEvents.CellEntered -= OnCellEntered;
            if (Instance == this) Instance = null;
            var player = PlayerController.Instance;
            if (player && Active) player.SpeedOverride = 0f;
        }

        /// <summary>처음 들어선 칸이면 바로 끄고(이번 프레임부터 느려짐), 다시 들어선 칸이면 아는 칸으로 둔다(조건이 맞으면 Update가 켬).</summary>
        void OnCellEntered(DungeonCell cell, bool first)
        {
            KnownCell = !first;
            if (!first) return;
            Active = false;
            var player = PlayerController.Instance;
            if (player && player.SpeedOverride > 0f) player.SpeedOverride = 0f;
        }

        void Update()
        {
            var player = PlayerController.Instance;
            if (!player)
            {
                Active = false;
                _quietSince = -1f;
                return;
            }
            if (TimeScaleService.Paused) return;

            bool quiet = !player.IsDown && !AwakeEnemyNear(player.Position);
            if (!quiet) _quietSince = -1f;
            else if (_quietSince < 0f) _quietSince = Time.time;

            bool active = quiet
                && (KnownCell || FastEverywhere)
                && !player.Crouching
                && Time.time - _quietSince >= QuietSeconds
                && Time.time - player.LastCombatActionTime >= QuietSeconds;
            Active = active;
            float want = active ? WalkSpeed : 0f;
            if (!Mathf.Approximately(player.SpeedOverride, want)) player.SpeedOverride = want;
        }

        /// <summary>반경 12 안에 깨어 있는(돌아가는 중이 아닌) 적이 있는가. 아는 길은 벽 너머 적도 센다(조용할 때만 빨라지게).</summary>
        public static bool AwakeEnemyNear(Vector2 at)
        {
            float r2 = EnemyRange * EnemyRange;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead || !e.Aware || e.IsReturning) continue;
                if ((e.Position - at).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>
        /// 싸움 판정(DungeonLighting.InCombat)용: range 안에 벽에 가리지 않은 깬 적(돌아가는 중·허수아비 제외)이 있는가.
        /// 벽 너머에서 깨고 돌아가기를 되풀이하는 무리 때문에 판정이 오가지 않게 한다. 말뚝·광맥 같은 작은 물체는 가리지 않는다(VisionSystem.WallBetween).
        /// </summary>
        public static bool AwakeEnemyInSight(Vector2 at, float range)
        {
            float r2 = range * range;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead || e.IsDummy || !e.Aware || e.IsReturning) continue;
                if ((e.Position - at).sqrMagnitude > r2) continue;
                if (!VisionSystem.WallBetween(at, e.Position)) return true;
            }
            return false;
        }
    }
}
