using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 보상 묶음(Core가 먼저 확정한 결과)을 바닥에 흩뿌린다(2차 7-6 공통 흐름).
    /// 여러 개면 0.08초 간격으로 부채꼴(장비가 가운데), 거리 0.8~2.0. 벽 너머로 넘어가지 않게 원 던지기로 거리를 줄인다.
    /// 궤짝(A), 처치·둥지 정리(A)가 쓰고, 금고·사건(D)도 같은 함수를 쓸 수 있다.
    /// </summary>
    public static class LootSpawner
    {
        /// <summary>몬스터가 쓰러지고 장비가 튀어나오기까지(2차 7-6).</summary>
        public const float KillDelay = 0.15f;
        /// <summary>여러 개일 때 사이 간격.</summary>
        public const float Interval = 0.08f;
        public const float MinDistance = 0.8f;
        public const float MaxDistance = 2.0f;
        const float FanStepDeg = 30f;
        const float MaxFanDeg = 160f;
        const float LandingRadius = 0.25f;
        /// <summary>강화석은 이 개수까지 따로 튀고, 넘으면 나눠 담는다.</summary>
        const int MaxStonePickups = 4;

        /// <summary>
        /// 묶음 하나를 origin에서 direction 쪽으로 흩뿌린다(장비 → 강화석 → 골드 순서로 튀어나옴).
        /// direction이 0이면 무작위 방향.
        /// </summary>
        public static void Spawn(LootBundle bundle, Vector2 origin, Vector2 direction, float delay)
        {
            if (bundle == null || bundle.Empty) return;
            int stonePickups = Mathf.Min(MaxStonePickups, Mathf.Max(0, bundle.Stones));
            int total = bundle.Gear.Count + stonePickups + bundle.GoldPiles.Count;
            if (direction.sqrMagnitude < 0.0001f) direction = UnityEngine.Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;
            direction.Normalize();

            int index = 0;
            foreach (var item in bundle.Gear)
            {
                LootDrop.Spawn(item, origin, Landing(origin, direction, index, total), delay + Interval * index);
                index++;
            }
            for (int i = 0; i < stonePickups; i++)
            {
                int amount = bundle.Stones / stonePickups + (i < bundle.Stones % stonePickups ? 1 : 0);
                LootPickup.Spawn(PickupKind.Stone, amount, origin, Landing(origin, direction, index, total), delay + Interval * index);
                index++;
            }
            foreach (int gold in bundle.GoldPiles)
            {
                LootPickup.Spawn(PickupKind.Gold, gold, origin, Landing(origin, direction, index, total), delay + Interval * index);
                index++;
            }
        }

        /// <summary>장비 하나만 떨어뜨린다(7부위 공통, 시험 패널·다른 모듈용).</summary>
        public static LootDrop SpawnGear(GearItem item, Vector2 origin, Vector2 direction, float delay)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;
            return LootDrop.Spawn(item, origin, Landing(origin, direction.normalized, 0, 1), delay);
        }

        /// <summary>
        /// 부채꼴 자리: 차례 0이 가운데, 그다음 왼쪽·오른쪽으로 번갈아 벌어진다. 거리 0.8~2.0(무작위, 연출용).
        /// </summary>
        public static Vector2 Landing(Vector2 origin, Vector2 direction, int index, int count)
        {
            float spread = Mathf.Min(MaxFanDeg, FanStepDeg * Mathf.Max(0, count - 1));
            float step = count > 1 ? spread / (count - 1) : 0f;
            int ring = (index + 1) / 2;
            float sign = index % 2 == 1 ? 1f : -1f;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + sign * ring * step + UnityEngine.Random.Range(-6f, 6f);
            if (count <= 1) angle += UnityEngine.Random.Range(-15f, 15f);
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float dist = UnityEngine.Random.Range(MinDistance, MaxDistance);
            return SafeLanding(origin, dir, dist);
        }

        /// <summary>벽(Wall 레이어)에 막히면 그 앞에 내려앉는다.</summary>
        public static Vector2 SafeLanding(Vector2 origin, Vector2 dir, float distance)
        {
            var hit = Physics2D.CircleCast(origin, LandingRadius, dir, distance, Layers.WallMask);
            if (hit.collider != null) distance = Mathf.Max(0f, hit.distance - 0.1f);
            return origin + dir * distance;
        }
    }
}
