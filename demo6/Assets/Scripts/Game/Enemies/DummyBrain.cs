using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 10장 훈련장 허수아비. 움직이거나 공격하지 않는다.
    /// 나무 허수아비: 체력 무한, 방어 0, 넉백 무시, 지름 1.1. 쥐 허수아비: 고른 층의 굴쥐 체력, 쓰러지면 3초 뒤 다시 선다(소환기가 처리).
    /// 처치 수·의뢰·계측 처치 수에 들어가지 않는다.
    /// </summary>
    public sealed class DummyBrain : Enemy
    {
        public bool IsWood { get; private set; }

        public void MakeDummy(bool wood)
        {
            IsDummy = true;
            IsWood = wood;
            UseMeasurementRules();
            if (wood)
            {
                Health.Infinite = true;
                BaseKnockbackResist = 1f;
                Body.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        protected override void Think(float dt)
        {
            SetPose(EnemyPose.Idle);
            DesiredVelocity = Vector2.zero;
        }
    }
}
