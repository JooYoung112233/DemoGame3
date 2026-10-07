using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 넉백 한 번에 붙는 정보(Enemy.ApplyKnockback 세 번째 인자). 기본값이면 예전 넉백과 같다.
    /// 벽 박기(WallSlamRule)는 이 정보를 실은 채 벽·기둥에 닿은 넉백만 본다(CombatEvents.EnemyWallSlam의 WallSlamHit.Knock).
    /// 계약(꾸러미 ⑧이 소유): 필드 이름·뜻은 바꾸지 않는다. 넣는 쪽 = PlayerController(⑤), 읽는 쪽 = WallSlam(③).
    /// </summary>
    public struct KnockInfo
    {
        /// <summary>쇠망치 땅 울리기(ComboStep.wallBreak): 벽 박기 한 칸 위 결과.</summary>
        public bool WallBreak;
        /// <summary>그 타가 실제로 넣은 피해(굴쥐 벽 박기 '그 타 피해 50% 추가'의 바탕). 0이면 모름.</summary>
        public int HitDamage;
        /// <summary>저항·배율을 곱한 뒤 거리 상한(큰 낫 끌어당김: 몸 사이 거리 − 0.15). 0 이하면 상한 없음.</summary>
        public float MaxDistance;
        /// <summary>넉백을 준 피해 출처(기본 Basic).</summary>
        public DamageSource Source;
        /// <summary>플레이어 몫 넉백인가(벽 박기는 플레이어 몫만 본다. 적끼리 부딪힘은 판정 뒤 후보).</summary>
        public bool FromPlayer;

        public static KnockInfo Player(DamageSource source, int hitDamage, bool wallBreak = false, float maxDistance = 0f) => new KnockInfo
        {
            Source = source,
            HitDamage = hitDamage,
            WallBreak = wallBreak,
            MaxDistance = maxDistance,
            FromPlayer = true,
        };
    }

    /// <summary>벽 박기 한 번(CombatEvents.EnemyWallSlam). Point·Normal은 닿은 벽 자리와 벽 바깥 방향.</summary>
    public readonly struct WallSlamHit
    {
        public readonly Vector2 Point;
        public readonly Vector2 Normal;
        public readonly KnockInfo Knock;
        /// <summary>이 넉백이 저항·배율을 곱해 밀려고 한 거리(WallSlamRule.MinPush와 비교).</summary>
        public readonly float PushDistance;

        public WallSlamHit(Vector2 point, Vector2 normal, KnockInfo knock, float pushDistance)
        {
            Point = point;
            Normal = normal;
            Knock = knock;
            PushDistance = pushDistance;
        }
    }
}
