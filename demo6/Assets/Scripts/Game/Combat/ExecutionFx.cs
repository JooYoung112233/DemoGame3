using Demo6.Core.Combat;
using Demo6.Core.Time;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 처형 연출(기획/전투-보스-무기-다듬기-1차.md 4-2 [3], ExecutionRule 값): 0.06초에 0.6유닛 당겨 붙음, 대검 내려찍기 자세를 칼 1.25배로(PlayerController.ExecutionPose*),
    /// 히트스톱 0.10, 흔들림 0.12/0.14, 고어(GoreSystem.ExecutionGore: 조각 4~6·피 웅덩이 1.4배), 느린 화면 0.3초 × 0.35는 마주침의 마지막 적·정예만(SlowPriority.Execution).
    /// '처형' 글자는 띄우지 않는다(소리·피·멈춤으로 말한다). PlayerController.Create가 플레이어에 붙인다(정적 Play만 써서 몸 컴포넌트는 비어 있다).
    /// 부르는 곳: PlayerController 무너짐 처형(마무리·검풍)·기습 처형. 당겨 붙기와 자세는 PlayerController가 맡고, 여기서는 멈춤·흔들림·피·소리만 낸다.
    /// 히트스톱 0.10은 무너진 적 마무리 0.1을 대신하고 무거운 적 처치 0.12는 생략된다(PlayerController가 처형이 난 판정에서 둘을 내지 않음).
    /// </summary>
    public sealed class ExecutionFx : MonoBehaviour
    {
        /// <summary>처형 연출을 낸 수와 마지막 시각(게임 시간, 시험 확인용). 플레이 시작 때 비운다.</summary>
        public static int PlayCount { get; private set; }
        public static int SlowCount { get; private set; }
        public static float LastPlayTime { get; private set; } = -999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PlayCount = 0;
            SlowCount = 0;
            LastPlayTime = -999f;
        }

        /// <summary>처형 연출 한 번(dir = 플레이어 → 적, slowMotion = 마지막 적·정예, ambush = 기습 처형).</summary>
        public static void Play(Enemy enemy, Vector2 dir, bool slowMotion, bool ambush)
        {
            if (!enemy) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            PlayCount++;
            LastPlayTime = Time.time;
            TimeScaleService.HitStop(ExecutionRule.HitStop);
            ScreenShake.Add(ExecutionRule.ShakeAmplitude, ExecutionRule.ShakeSeconds);
            GoreSystem.ExecutionGore(enemy, dir.normalized);
            if (slowMotion)
            {
                SlowCount++;
                TimeScaleService.SlowMotion(ExecutionRule.SlowSeconds, ExecutionRule.SlowScale, SlowPriority.Execution);
            }
            // 무거운 몸이 꺾여 쓰러지는 소리와 피. 기습 처형은 조금 낮고 작게(조용히 끝낸다).
            Sfx.PlayScaled(SfxKind.BodyFall, ambush ? 0.75f : 1f, ambush ? 0.8f : 0.9f);
            Sfx.Play(SfxKind.Blood);
        }

        /// <summary>
        /// 그 마주침의 마지막 적인가(처형 느린 화면 조건, ExecutionRule.UsesSlowMotion): 같은 무리 번호(0 이상)의 살아 있는 다른 적이 없음.
        /// 무리 번호가 없으면 ExecutionRule.LastEnemyRange(12) 안에 깨어 있는 다른 적이 없음. 허수아비는 세지 않는다.
        /// </summary>
        public static bool IsLastOfEncounter(Enemy enemy)
        {
            if (!enemy) return false;
            float range2 = ExecutionRule.LastEnemyRange * ExecutionRule.LastEnemyRange;
            foreach (var other in Enemy.All)
            {
                if (!other || other == enemy || other.Dead || other.IsDummy) continue;
                if (enemy.GroupId >= 0)
                {
                    if (other.GroupId == enemy.GroupId) return false;
                }
                else if (other.Aware && (other.Position - enemy.Position).sqrMagnitude <= range2) return false;
            }
            return true;
        }
    }
}
