using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 탐험 걸음(3차 초안 2-8): 12유닛 안에 깨어 있는 적이 없고 3초가 지나면 이동 6.5.
    /// 공격·스킬을 쓰거나 맞으면(PlayerController.LastCombatActionTime) 바로 장비 속도로 돌아간다. 싸울 때는 M0a 그대로.
    /// 근거: 2차 이동 상한·마을 걷기와 같은 값으로, 아는 길을 되돌아가는 걸음을 줄인다.
    /// 2026-10-03: 6.5는 기획 숫자로 두고, 실제 속도는 PlayerController.MoveSpeed가 Tuning.MoveSpeedScale(기본 0.86)을 곱해 약 5.59가 된다.
    /// </summary>
    public sealed class ExploreWalk : MonoBehaviour
    {
        /// <summary>탐험 걸음 이동 속도(2-8, 배율 곱하기 전 기획 숫자).</summary>
        public const float WalkSpeed = 6.5f;
        /// <summary>배율을 곱한 실제 탐험 걸음 속도(시험 패널 표시용).</summary>
        public static float ScaledSpeed => WalkSpeed * Tuning.MoveSpeedScale;
        /// <summary>깨어 있는 적을 찾는 반경(2-8, 전투 중 판정 DungeonLighting.InCombat과 같은 12).</summary>
        public const float EnemyRange = 12f;
        /// <summary>조용한 상태가 이만큼 이어져야 켜진다(2-8).</summary>
        public const float QuietSeconds = 3f;

        public static ExploreWalk Instance { get; private set; }

        /// <summary>지금 탐험 걸음인가(화면 '탐험 걸음' 표시).</summary>
        public bool Active { get; private set; }

        /// <summary>근처에 깨어 있는 적이 없어진 시각(게임 시간). 적이 있으면 -1.</summary>
        float _quietSince = -1f;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var player = PlayerController.Instance;
            if (player && Active) player.SpeedOverride = 0f;
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
                && Time.time - _quietSince >= QuietSeconds
                && Time.time - player.LastCombatActionTime >= QuietSeconds;
            Active = active;
            float want = active ? WalkSpeed : 0f;
            if (!Mathf.Approximately(player.SpeedOverride, want)) player.SpeedOverride = want;
        }

        static bool AwakeEnemyNear(Vector2 at)
        {
            float r2 = EnemyRange * EnemyRange;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead || !e.Aware || e.IsReturning) continue;
                if ((e.Position - at).sqrMagnitude <= r2) return true;
            }
            return false;
        }
    }
}
