using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 3-3 자동 조준.
    /// 1차: 찾는 범위 = 무기 사거리 + 1.5, 벽 너머 제외. ① 커서 1.5 안 적 중 커서에 가장 가까운 적 ② 플레이어에게 가장 가까운 적 ③ 없으면 허공.
    /// 2차(시험 패널에서 켬): 직전 대상 유지(0.8초), 점수 = 거리 + (이동 방향과의 각도/180°) × 1.5.
    /// 보스·정예라고 먼저 노리지 않는다.
    /// </summary>
    public static class AutoTargeter
    {
        public const float CursorRadius = 1.5f;
        public const float ExtraSearch = 1.5f;
        public const float StickyTime = 0.8f;

        public static Enemy Pick(Vector2 player, Vector2 cursor, float weaponRange, Vector2 moveDir, Enemy sticky, float sinceLastAttack)
        {
            float search = weaponRange + ExtraSearch;

            Enemy byCursor = null;
            float bestCursor = float.MaxValue;
            foreach (var e in Enemy.All)
            {
                if (!Valid(e, player, search)) continue;
                float dc = (e.Position - cursor).magnitude - e.Radius;
                if (dc <= CursorRadius && dc < bestCursor)
                {
                    bestCursor = dc;
                    byCursor = e;
                }
            }
            if (byCursor) return byCursor;

            if (Tuning.SmartTargeting && sticky && sinceLastAttack <= StickyTime && Valid(sticky, player, search))
                return sticky;

            Enemy best = null;
            float bestScore = float.MaxValue;
            bool useScore = Tuning.SmartTargeting && moveDir.sqrMagnitude > 0.01f;
            foreach (var e in Enemy.All)
            {
                if (!Valid(e, player, search)) continue;
                Vector2 to = e.Position - player;
                float score = to.magnitude - e.Radius;
                if (useScore) score += Vector2.Angle(moveDir, to) / 180f * 1.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        static bool Valid(Enemy e, Vector2 player, float search)
        {
            if (!e || e.Dead) return false;
            // 던전 시야 밖(보이지도, 눈빛도 없는) 적으로는 조준이 틀어지지 않는다. 빛 밖 눈만 보이는 적은 노릴 수 있다.
            if (e.VisionHidden && !e.VisionInSight) return false;
            float d = (e.Position - player).magnitude - e.Radius;
            if (d > search) return false;
            return !Physics2D.Linecast(player, e.Position, Layers.WallMask);
        }
    }
}
