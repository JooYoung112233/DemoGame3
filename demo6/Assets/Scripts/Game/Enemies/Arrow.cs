using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 궁수 화살. 콜라이더 없이 Update에서 이전 위치부터 지금 위치까지 CircleCast로 옮긴다(기획 11-6).
    /// 플레이어가 무적(구르기)이면 그대로 지나간다. 방패로 막거나 튕기면(PlayerController.LastHitResult) 꿰뚫는 화살도 멈춘다
    /// (기획/세-무기-우클릭-소켓-1차.md 2-7).
    /// </summary>
    public sealed class Arrow : MonoBehaviour
    {
        public const float Speed = 10f;
        public const float Range = 12f;
        const float HitRadius = 0.12f;

        Vector2 _dir;
        float _traveled;
        int _attack;
        float _percent = 100f;
        bool _pierce;
        bool _hitPlayer;
        ArrowVolley _volley;
        bool _done;

        static readonly System.Collections.Generic.List<Arrow> Live = new System.Collections.Generic.List<Arrow>();

        /// <param name="percent">패턴 배율(%). 3차 꿰뚫는 화살 200.</param>
        /// <param name="pierce">꿰뚫는 화살: 플레이어를 맞혀도 멈추지 않고 벽까지 간다.</param>
        public static void Spawn(Vector2 origin, Vector2 dir, int attack, ArrowVolley volley, float percent = 100f, bool pierce = false)
        {
            var go = new GameObject("Arrow");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var visual = new GameObject("Shaft");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = pierce ? new Vector3(0.85f, 0.14f, 1f) : new Vector3(0.55f, 0.08f, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            RenderMaterials.MakeUnlit(sr);
            sr.sprite = ShapeSprites.Square;
            sr.color = Palette.Arrow;
            sr.sortingOrder = 3000;
            var arrow = go.AddComponent<Arrow>();
            Live.Add(arrow);
            arrow._dir = dir.normalized;
            arrow._attack = attack;
            arrow._volley = volley;
            arrow._percent = percent;
            arrow._pierce = pierce;
        }

        void Update()
        {
            if (_done) return;
            float step = Speed * Time.deltaTime;
            if (step <= 0f) return;
            Vector2 from = transform.position;
            float travel = Mathf.Min(step, Range - _traveled);

            var wall = Physics2D.CircleCast(from, HitRadius, _dir, travel, Layers.WallMask);
            if (wall.collider) travel = wall.distance;

            var player = PlayerController.Instance;
            if (!_hitPlayer && player && !player.IsDown && SegmentDistance(from, from + _dir * travel, player.Position) <= PlayerController.Radius + HitRadius)
            {
                // 방패 표 2-7: 보통 화살 Arrow, 꿰뚫는 화살 PierceArrow, 진행 방향 = 날아가는 쪽. 쏜 궁수는 넘기지 않는다(화살 패링은 적을 휘청하게 하지 않음).
                bool landed = player.ReceiveHit(_attack, _percent, from, _pierce ? 0.8f : 0.4f, true, _pierce ? HitKind.PierceArrow : HitKind.Arrow, _dir);
                var result = player.LastHitResult;
                // 반환 약속(막음 true, 튕김 false)과 맞을 때만 본다: 이번 호출이 결과를 남기지 못한 옛 값에 화살이 멈추지 않게.
                bool stopped = (result == HitResult.Blocked && landed) || (result == HitResult.Parried && !landed);
                if (stopped)
                {
                    // 방패에 꽂힘(막음)·튕김(패링): 꿰뚫는 화살도 여기서 멈춘다(되쏘기 없음).
                    // 예고 계측은 막음 = 맞음, 튕김 = 피함(2-7): ReceiveHit 반환값(막음 true, 튕김 false)을 그대로 보고한다.
                    transform.position = from + _dir * travel;
                    Finish(landed);
                    return;
                }
                if (landed)
                {
                    _hitPlayer = true;
                    if (!_pierce)
                    {
                        Finish(true);
                        return;
                    }
                }
            }

            transform.position = from + _dir * travel;
            _traveled += travel;
            if (wall.collider || _traveled >= Range - 0.0001f) Finish(_hitPlayer);
        }

        void Finish(bool hit)
        {
            if (_done) return;
            _done = true;
            _volley?.Report(hit);
            Destroy(gameObject);
        }

        /// <summary>구성을 바꾸거나 다시 세울 때 날아가는 화살을 기록 없이 지운다.</summary>
        public static void DiscardAll()
        {
            foreach (var a in Live.ToArray())
            {
                if (!a) continue;
                a._done = true;
                Destroy(a.gameObject);
            }
            Live.Clear();
        }

        void OnDestroy()
        {
            Live.Remove(this);
            if (!_done)
            {
                _done = true;
                _volley?.Report(false);
            }
        }

        static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.000001f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
