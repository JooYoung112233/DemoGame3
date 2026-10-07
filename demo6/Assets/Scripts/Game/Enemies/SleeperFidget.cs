using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 잠든 무리 '뒤척임'(기획/전투-보스-무기-다듬기-1차.md 4-2 [3]): 2~4초마다 Tuning.SleeperTurnChance 확률로 쉬는 적이 몸을 돌린다(Enemy.TurnWhileResting, 깨우지 않음).
    /// 기본 0%('숨어 다니기만' 보이면 30%). 기습 처형만 하는 놀이로 쏠리는 것을 막는 손잡이(7장 #8). PlayerController.Create가 플레이어에 붙인다.
    /// 쉬는 적마다 다음 뒤척일 시각을 따로 센다. 손잡이가 0이면 아무것도 하지 않는다(난수도 굴리지 않음).
    /// 허수아비·둥지·보스와 알아채는 중('!' 0.5초·무리 반응 0.8초)인 적은 돌리지 않는다.
    /// </summary>
    public sealed class SleeperFidget : MonoBehaviour
    {
        readonly Dictionary<Enemy, float> _next = new Dictionary<Enemy, float>();
        readonly List<Enemy> _drop = new List<Enemy>();

        void Update()
        {
            float chance = Tuning.SleeperTurnChance;
            if (chance <= 0f)
            {
                if (_next.Count > 0) _next.Clear();
                return;
            }
            if (TimeScaleService.Paused || Time.deltaTime <= 0f) return;
            float now = Time.time;
            foreach (var e in Enemy.All)
            {
                if (!Resting(e)) continue;
                if (!_next.TryGetValue(e, out float at))
                {
                    _next[e] = now + NextInterval();
                    continue;
                }
                if (now < at) continue;
                _next[e] = now + NextInterval();
                if (Random.value < chance) e.TurnWhileResting(Turned(e.FacingDirection));
            }
            // 깨거나 쓰러지거나 사라진 적은 지운다(다시 쉬면 새로 센다).
            _drop.Clear();
            foreach (var kv in _next)
                if (!Resting(kv.Key)) _drop.Add(kv.Key);
            foreach (var e in _drop) _next.Remove(e);
        }

        void OnDisable() => _next.Clear();

        static bool Resting(Enemy e) =>
            e && !e.Dead && !e.Aware && !e.WakePending && !e.IsDummy && !e.IsBoss && e.Kind != MonsterKind.Nest;

        static float NextInterval() => Random.Range(ExecutionRule.SleeperTurnMinSeconds, ExecutionRule.SleeperTurnMaxSeconds);

        /// <summary>지금 보는 쪽에서 왼쪽·오른쪽 무작위로 90~180° 돌린 방향.</summary>
        static Vector2 Turned(Vector2 facing)
        {
            if (facing.sqrMagnitude < 0.0001f) facing = Vector2.down;
            float deg = Random.Range(ExecutionRule.SleeperTurnMinDegrees, ExecutionRule.SleeperTurnMaxDegrees) * (Random.value < 0.5f ? -1f : 1f);
            float rad = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(facing.x * c - facing.y * s, facing.x * s + facing.y * c);
        }
    }
}
