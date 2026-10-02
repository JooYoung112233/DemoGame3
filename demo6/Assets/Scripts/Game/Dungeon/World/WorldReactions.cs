using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 세계 반응: 벽 '퉁'(3차 초안 2-5 숨은 방 단서). 적을 못 맞힌 기본공격·회오리가 판자벽이 아닌 벽(Wall 레이어)에 닿으면
    /// '퉁' 소리만 낸다(히트스톱·흔들림 없음). 한 번 휘두를 때 한 번(쌍검 연타는 0.15초, 회오리 3타는 0.55초 안에 한 번).
    /// 판자벽에 닿은 휘두름은 판자벽(PlankWall)이 따로 '탁'·히트스톱으로 답하므로 여기서는 소리를 내지 않는다.
    /// </summary>
    public sealed class WorldReactions : MonoBehaviour
    {
        const float SwingGap = 0.15f;
        const float WhirlGap = 0.55f;
        /// <summary>벽 '퉁' 판정 여유(실제 사거리에 가깝게, 판자벽 0.5보다 작게).</summary>
        const float ReachSlack = 0.15f;

        public static WorldReactions Instance { get; private set; }

        readonly List<Collider2D> _overlap = new List<Collider2D>(16);
        readonly List<PlankWall> _planks = new List<PlankWall>();
        ContactFilter2D _wallFilter;
        float _lastSwingThud = -999f;
        float _lastWhirlThud = -999f;

        void Awake()
        {
            Instance = this;
            _wallFilter = new ContactFilter2D();
            _wallFilter.SetLayerMask(Layers.WallMask);
            _wallFilter.useTriggers = false;
            // CombatEvents.ResetStatics가 이 두 사건을 비우지 않으므로 OnDestroy에서 꼭 뗀다.
            // 판자벽은 지도를 만들 때(이보다 먼저) 구독하므로 같은 휘두름에서 판자벽이 먼저 답한다.
            CombatEvents.PlayerSwing += OnSwing;
            CombatEvents.PlayerWhirl += OnWhirl;
        }

        void Start()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.World == null) return;
            foreach (var e in root.World.Edges)
            {
                if (e.Kind != EdgeKind.Plank || !e.Blocker) continue;
                var plank = e.Blocker.GetComponent<PlankWall>();
                if (plank) _planks.Add(plank);
            }
        }

        void OnDestroy()
        {
            CombatEvents.PlayerSwing -= OnSwing;
            CombatEvents.PlayerWhirl -= OnWhirl;
            if (Instance == this) Instance = null;
        }

        void OnSwing(Vector2 origin, Vector2 dir, ComboStep step, bool hitEnemy)
        {
            if (hitEnemy || step == null) return;
            if (Time.time - _lastSwingThud < SwingGap) return;
            if (PlankAnswered(origin, dir, step, 0f, false)) return;
            WorldGeometry.Bounds(origin, dir, step, out Vector2 center, out float radius);
            _overlap.Clear();
            Physics2D.OverlapCircle(center, radius + ReachSlack + 0.1f, _wallFilter, _overlap);
            bool touched = false;
            foreach (var col in _overlap)
            {
                if (!col || col.isTrigger || col.GetComponentInParent<PlankWall>()) continue;
                if (!WorldGeometry.StepTouchesCollider(col, origin, dir, step, ReachSlack)) continue;
                touched = true;
                break;
            }
            if (!touched) return;
            _lastSwingThud = Time.time;
            Sfx.Play(SfxKind.Thud);
        }

        void OnWhirl(Vector2 origin, float radius, bool hitEnemy)
        {
            if (hitEnemy) return;
            if (Time.time - _lastWhirlThud < WhirlGap) return;
            if (PlankAnswered(origin, Vector2.right, null, radius, true)) return;
            _overlap.Clear();
            Physics2D.OverlapCircle(origin, radius, _wallFilter, _overlap);
            bool touched = false;
            foreach (var col in _overlap)
            {
                if (!col || col.isTrigger || col.GetComponentInParent<PlankWall>()) continue;
                touched = true;
                break;
            }
            if (!touched) return;
            _lastWhirlThud = Time.time;
            Sfx.Play(SfxKind.Thud);
        }

        /// <summary>
        /// 판자벽이 이 휘두름에 답했는가: 이번 프레임에 맞았거나(부서진 프레임 포함) 아직 서 있고 판정에 닿는다.
        /// circle이면 (origin, radius) 원으로, 아니면 콤보 단계 모양으로 본다.
        /// </summary>
        bool PlankAnswered(Vector2 origin, Vector2 dir, ComboStep step, float radius, bool circle)
        {
            int frame = Time.frameCount;
            foreach (var plank in _planks)
            {
                if (!plank) continue;
                if (plank.LastHitFrame == frame) return true;
                if (circle ? plank.TouchedByCircle(origin, radius) : plank.TouchedBySwing(origin, dir, step)) return true;
            }
            return false;
        }
    }
}
