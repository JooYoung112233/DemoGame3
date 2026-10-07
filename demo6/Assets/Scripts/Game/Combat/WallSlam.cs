using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 벽·기둥 박기 결과 적용과 연출(기획/전투-보스-무기-다듬기-1차.md 4-2 [1], WallSlamRule). CombatEvents.EnemyWallSlam(Enemy 넉백 자리 한 곳이 냄)을 듣고
    /// WallSlamRule.Resolve(enemy.Class, hit.Knock.WallBreak)대로: 굴쥐 그 타 피해 50% 추가(DamageSource.Environment), 궁수·쇠망치에 박힌 멧돼지 BreakNow,
    /// 멧돼지 버팀 최대치 35% + 0.15초 움찔(정예 × 0.5). 연출: 히트스톱 0.05, 흙먼지 6알(HitEffects), 둔탁한 '쿵'(타격음 음높이 0.7), 벽 피 자국(GoreSystem.WallSplat).
    /// Tuning.WallSlamOn이 꺼져 있으면 아무것도 하지 않는다. PlayerController.Create가 플레이어에 붙인다(두 시험장 공통).
    /// 판정(실제 0.5 이상 밀림, 같은 적 0.5초에 한 번, 플레이어 몫 넉백만)은 사건을 내는 쪽(Enemy, 꾸러미 ⑧)이 지키고 여기서는 다시 보지 않는다.
    /// 멧돼지 자기 돌진 벽 박기(BoarBrain, 바로 무너짐)와 오우거 마지막 돌진(OgreBrain)은 각자 처리하고 이 사건을 내지 않는다.
    /// </summary>
    public sealed class WallSlam : MonoBehaviour
    {
        /// <summary>마지막 결과(시험 패널·eval 확인용). 사건이 없었으면 None.</summary>
        public static WallSlamKind LastKind { get; private set; }
        /// <summary>지금까지 적용한 벽 박기 수(플레이 시작 때 0).</summary>
        public static int Count { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastKind = WallSlamKind.None;
            Count = 0;
        }

        void OnEnable() => CombatEvents.EnemyWallSlam += OnWallSlam;

        void OnDisable() => CombatEvents.EnemyWallSlam -= OnWallSlam;

        void OnWallSlam(Enemy enemy, WallSlamHit hit)
        {
            if (!Tuning.WallSlamOn || !enemy || enemy.Dead) return;
            var r = WallSlamRule.Resolve(enemy.Class, hit.Knock.WallBreak);
            if (r.Kind == WallSlamKind.None) return;
            LastKind = r.Kind;
            Count++;

            // 연출을 먼저 낸다: 추가 피해로 쓰러져도 벽에 박힌 순간은 보이게.
            TimeScaleService.HitStop(WallSlamRule.HitStop);
            HitEffects.WallDust(hit.Point, hit.Normal, WallSlamRule.DustCount);
            // 둔탁한 '쿵': 타격음을 낮게(0.7) + 벽 '퉁'을 겹친다(같은 프레임에 타격음이 이미 났어도 벽 소리는 남는다).
            Sfx.PlayScaled(SfxKind.Hit, 1f, WallSlamRule.ThudPitch);
            Sfx.PlayScaled(SfxKind.Thud, 0.9f, 0.85f);
            if (r.BloodMark) GoreSystem.WallSplat(enemy, hit.Point, hit.Normal);

            switch (r.Kind)
            {
                case WallSlamKind.ExtraDamage:
                {
                    int extra = Mathf.RoundToInt(hit.Knock.HitDamage * r.ExtraDamageFraction);
                    if (extra > 0) enemy.TakeHit(extra, false, DamageSource.Environment, 0f, false, 1f, out _, out _);
                    break;
                }
                case WallSlamKind.Break:
                    enemy.BreakNow();
                    break;
                case WallSlamKind.PoiseHit:
                    if (enemy.Poise != null) enemy.ApplyPoiseHit(enemy.Poise.Max * r.PoiseFractionOfMax, r.FlinchSeconds);
                    break;
            }
        }
    }
}
