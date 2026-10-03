using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 3-4 검풍: 직선 검기가 꿰뚫고 지나간다. 길이 10, 폭 1.4, 초당 20, 벽에 막힘, 350%, 넉백 2.0.
    /// 콜라이더 없이 Update에서 지나간 구간을 상자로 훑는다. 같은 적은 한 번만 맞는다.
    /// </summary>
    public sealed class SwordWave : MonoBehaviour
    {
        public const float Percent = 350f;
        public const float Knockback = 2.0f;
        const float Speed = 20f;
        const float MaxTravel = 10f;
        const float Width = 1.4f;
        const float Thickness = 0.5f;

        readonly HashSet<Enemy> _hit = new HashSet<Enemy>();
        readonly List<Collider2D> _overlap = new List<Collider2D>(32);
        PlayerController _owner;
        Vector2 _dir;
        float _traveled;
        bool _stopped;
        bool _anyHit;
        /// <summary>이 검풍 한 번이 낸 치명 연출 단계(첫 치명에서 PlayerController가 정함).</summary>
        CritTier _critShown;
        float _fade = -1f;
        SpriteRenderer _sprite;
        ContactFilter2D _filter;

        public static void Spawn(Vector2 origin, Vector2 dir, PlayerController owner)
        {
            var go = new GameObject("SwordWave");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var visual = new GameObject("Blade");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(Thickness, Width, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = ShapeSprites.Circle;
            sr.color = Palette.Wave;
            sr.sortingOrder = 3100;
            RenderMaterials.MakeUnlit(sr);
            var wave = go.AddComponent<SwordWave>();
            wave._owner = owner;
            wave._dir = dir.normalized;
            wave._sprite = sr;
            wave._filter = Layers.EnemyFilter();
            wave.Sweep(origin, 0f);
        }

        void Update()
        {
            if (_fade >= 0f)
            {
                _fade -= Time.unscaledDeltaTime;
                if (_sprite)
                {
                    var c = _sprite.color;
                    c.a = Mathf.Clamp01(_fade / 0.12f) * Palette.Wave.a;
                    _sprite.color = c;
                }
                if (_fade < 0f) Destroy(gameObject);
                return;
            }
            float step = Speed * Time.deltaTime;
            if (step <= 0f) return;
            Vector2 from = transform.position;
            float travel = Mathf.Min(step, MaxTravel - _traveled);
            var wall = Physics2D.CircleCast(from, 0.2f, _dir, travel, Layers.WallMask);
            if (wall.collider)
            {
                travel = wall.distance;
                _stopped = true;
                CombatEvents.RaiseWaveHitWall(wall.point, _dir);
            }
            Sweep(from, travel);
            transform.position = from + _dir * travel;
            _traveled += travel;
            if (_stopped || _traveled >= MaxTravel - 0.0001f) _fade = 0.12f;
        }

        void Sweep(Vector2 from, float travel)
        {
            if (!_owner) return;
            Vector2 center = from + _dir * (travel * 0.5f);
            float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            _overlap.Clear();
            Physics2D.OverlapBox(center, new Vector2(travel + Thickness, Width), angle, _filter, _overlap);
            bool anyCrit = false;
            bool anyKill = false;
            bool newHit = false;
            foreach (var col in _overlap)
            {
                var enemy = col ? col.GetComponent<Enemy>() : null;
                if (!enemy || enemy.Dead || !_hit.Add(enemy)) continue;
                if (_owner.HitWithWave(enemy, _dir, ref _critShown, out bool crit, out bool killed))
                {
                    newHit = true;
                    anyCrit |= crit;
                    anyKill |= killed;
                }
            }
            if (!newHit) return;
            // 치명 연출 단계(장비 문서 3-4)는 검풍 한 번에 하나: 첫 치명에서 정한 _critShown(350% × 치명 피해라 보통 무거움, 0.5초 안 두 번째면 보통).
            var tier = anyCrit ? _critShown : CritTier.None;
            Sfx.Play(anyKill ? SfxKind.Kill : anyCrit ? (tier == CritTier.Light ? SfxKind.CritLight : SfxKind.Crit) : SfxKind.Hit);
            if (!_anyHit)
            {
                // 기획 3-6: 검풍 0.07. 그 뒤 새로 맞힌 적이 무거운 치명·마지막 일격이면 0.06(더 길 때만 덮어씀).
                _anyHit = true;
                TimeScaleService.HitStop(0.07f);
                ScreenShake.Add(0.1f, 0.1f);
            }
            else if (tier == CritTier.Heavy || anyKill)
            {
                TimeScaleService.HitStop(0.06f);
                if (tier == CritTier.Heavy) ScreenShake.Add(0.06f, 0.08f);
            }
            else if (tier == CritTier.Normal) ScreenShake.Add(0.04f, 0.06f);
        }
    }
}
