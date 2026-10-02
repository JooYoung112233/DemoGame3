using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 타격감 보강(M0a 판정 '시원함 부족' 반영): 맞은 자리의 파편과 베인 자국.
    /// 파편은 게임 시간으로 움직여 히트스톱 동안 멈춘다(맞는 순간이 한 장면으로 보이게).
    /// 스프라이트 묶음을 미리 만들어 돌려 쓴다.
    /// </summary>
    public sealed class HitEffects : MonoBehaviour
    {
        const int PoolSize = 320;
        const int SortOrder = 3200;

        struct Shard
        {
            public SpriteRenderer Sprite;
            public Vector2 Velocity;
            public float Life;
            public float Age;
            public float Size;
            public Color Color;
            public bool Mark;
            public bool ArtSprite;
        }

        static HitEffects _instance;
        Shard[] _shards;
        int _next;

        /// <summary>껍질에 막힌 타격: 흰 불꽃만 튄다.</summary>
        public static void OnBlocked(Vector2 at, Vector2 dir)
        {
            if (!_instance || !Tuning.HitSparks) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            _instance.Burst(at, -dir.normalized, 4, Color.white, 0.8f);
        }

        public static void OnHit(Enemy enemy, Vector2 dir, bool crit, bool heavy, int extraSparks = 0)
        {
            if (!_instance || !enemy) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            Vector2 at = enemy.Position;
            if (Tuning.HitSparks)
            {
                Color body = Color.Lerp(BodyColor(enemy), Color.white, 0.45f);
                int count = (crit ? 10 : heavy ? 8 : 5) + extraSparks;
                _instance.Burst(at, dir, count, crit ? Palette.NumberCrit : body, crit ? 1.25f : 1f);
                if (enemy.Dead) _instance.Burst(at, dir, 6, BodyColor(enemy), 0.85f);
                _instance.SlashMark(at, dir, enemy.Radius, crit || heavy);
            }
        }

        static Color BodyColor(Enemy enemy)
        {
            switch (enemy.Kind)
            {
                case Demo6.Core.Combat.MonsterKind.Rat: return enemy.IsDummy ? Palette.RatDummy : Palette.Rat;
                case Demo6.Core.Combat.MonsterKind.Boar: return enemy.IsDummy ? Palette.WoodDummy : Palette.Boar;
                case Demo6.Core.Combat.MonsterKind.Nest: return Palette.Nest;
                default: return Palette.Archer;
            }
        }

        void Awake()
        {
            _instance = this;
            _shards = new Shard[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Shard");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ShapeSprites.Square;
                sr.sortingOrder = SortOrder;
                sr.enabled = false;
                RenderMaterials.MakeUnlit(sr);
                _shards[i] = new Shard { Sprite = sr, Life = 0f };
            }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Burst(Vector2 at, Vector2 dir, int count, Color color, float speedScale)
        {
            var art = ArtRuntime.Active;
            var sparks = art ? art.effects.sparks : null;
            bool useArt = sparks != null && sparks.Length > 0;
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            for (int i = 0; i < count; i++)
            {
                float a = baseAngle + Random.Range(-0.9f, 0.9f);
                float speed = Random.Range(4f, 9.5f) * speedScale;
                ref var s = ref Take();
                s.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                s.Life = Random.Range(0.16f, 0.3f);
                s.Age = 0f;
                s.Size = Random.Range(0.06f, 0.13f);
                s.Color = color;
                s.Mark = false;
                s.ArtSprite = useArt;
                s.Sprite.sprite = useArt ? sparks[Random.Range(0, sparks.Length)] : ShapeSprites.Square;
                s.Sprite.transform.position = at + Random.insideUnitCircle * 0.12f;
                s.Sprite.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
                s.Sprite.enabled = true;
            }
        }

        /// <summary>대상 위에 베는 방향과 엇갈린 흰 줄을 잠깐 긋는다.</summary>
        void SlashMark(Vector2 at, Vector2 dir, float radius, bool strong)
        {
            ref var s = ref Take();
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f + Random.Range(-28f, 28f);
            s.Velocity = Vector2.zero;
            s.Life = strong ? 0.12f : 0.09f;
            s.Age = 0f;
            s.Size = Mathf.Max(0.9f, radius * 2.6f);
            s.Color = new Color(1f, 1f, 1f, strong ? 1f : 0.85f);
            s.Mark = true;
            var art = ArtRuntime.Active;
            var mark = art ? art.effects.slashMark : null;
            s.ArtSprite = mark;
            s.Sprite.sprite = mark ? mark : ShapeSprites.Square;
            s.Sprite.transform.position = at;
            s.Sprite.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            // 그림 자국은 가로 1유닛 기준으로 길이만 맞춘다.
            s.Sprite.transform.localScale = mark
                ? new Vector3(s.Size / Mathf.Max(0.01f, mark.bounds.size.x), strong ? 1.3f : 1f, 1f)
                : new Vector3(s.Size, strong ? 0.09f : 0.06f, 1f);
            s.Sprite.enabled = true;
        }

        ref Shard Take()
        {
            ref var s = ref _shards[_next];
            _next = (_next + 1) % PoolSize;
            return ref s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < _shards.Length; i++)
            {
                ref var s = ref _shards[i];
                if (!s.Sprite.enabled) continue;
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    s.Sprite.enabled = false;
                    continue;
                }
                float k = 1f - s.Age / s.Life;
                var c = s.Color;
                c.a *= k;
                s.Sprite.color = c;
                if (s.Mark) continue;
                s.Velocity *= Mathf.Max(0f, 1f - 6f * dt);
                s.Sprite.transform.position += (Vector3)(s.Velocity * dt);
                float size = s.Size * (0.5f + 0.5f * k);
                // 그림 조각은 제 크기(약 0.1유닛)를 기준으로 늘이고 줄인다.
                float scale = s.ArtSprite ? size / 0.1f : size;
                s.Sprite.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
