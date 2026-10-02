using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 11-6 레이어. 물리 충돌은 Enemy↔Enemy, Player↔Wall, Enemy↔Wall, EnemyCharging↔Wall만 켠다.
    /// Player↔Enemy를 꺼서 적 사이에 끼여 못 움직이는 일을 막는다.
    /// </summary>
    public static class Layers
    {
        public const int Player = 8;
        public const int Enemy = 9;
        public const int EnemyCharging = 10;
        public const int Wall = 11;

        public const int WallMask = 1 << Wall;
        public const int EnemyMask = (1 << Enemy) | (1 << EnemyCharging);

        public static readonly (int index, string name)[] Named =
        {
            (Player, "Player"),
            (Enemy, "Enemy"),
            (EnemyCharging, "EnemyCharging"),
            (Wall, "Wall"),
        };

        static bool IsOurs(int layer) => layer >= Player && layer <= Wall;

        public static bool ShouldCollide(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            return (a == Enemy && b == Enemy)
                || (a == Player && b == Wall)
                || (a == Enemy && b == Wall)
                || (a == EnemyCharging && b == Wall);
        }

        public static void ApplyCollisionMatrix()
        {
            for (int a = 0; a < 32; a++)
            for (int b = a; b < 32; b++)
            {
                if (!IsOurs(a) && !IsOurs(b)) continue;
                Physics2D.IgnoreLayerCollision(a, b, !ShouldCollide(a, b));
            }
        }

        public static ContactFilter2D EnemyFilter()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(EnemyMask);
            filter.useTriggers = true;
            return filter;
        }
    }
}
