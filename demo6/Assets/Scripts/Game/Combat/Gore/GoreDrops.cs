using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 날아가는 피 방울과 맞은 자리의 피 안개(기획/다크판타지-분위기-1차.md '잔혹': 맞은 방향으로 크게 튄다).
    /// 손맛 효과라 빛을 무시하고(3차 초안 2-4, HitEffects와 같음) 효과 정렬(3050)에 그린다. 시야 덮개(5000) 아래라 시야 밖에서는 어둡다.
    /// 바닥 위 자리 + 높이(화면에서는 y로 올려 그림)로 포물선을 그리며, 땅에 닿은 방울 일부는 GoreSystem에 알려 얼룩을 남긴다.
    /// 렌더러는 처음에 모두 만들어 돌려 쓰고, 게임 시간으로 움직여 히트스톱 동안 멈춘다.
    /// </summary>
    public sealed class GoreDrops
    {
        public const int DropOrder = 3050;
        public const int PuffOrder = 3060;
        const float Gravity = GoreLayer.Gravity;
        const float MaxLife = 1.4f;

        struct Drop
        {
            public SpriteRenderer Sr;
            public Transform T;
            public bool Active;
            public bool Puff;
            public bool Stain;
            public Vector2 Origin;
            public Vector2 Pos;
            public Vector2 Vel;
            public float H;
            public float VH;
            public float Age;
            public float Life;
            public float Size;
            public Color Color;
            public GoreMatter Matter;
        }

        readonly Drop[] _drops;
        readonly GoreSystem _owner;
        int _next;

        public GoreDrops(GoreSystem owner, Transform parent, int count)
        {
            _owner = owner;
            _drops = new Drop[Mathf.Max(8, count)];
            var sprite = GoreSprites.Drop;
            for (int i = 0; i < _drops.Length; i++)
            {
                var go = new GameObject("Blood drop");
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = DropOrder;
                sr.enabled = false;
                RenderMaterials.MakeUnlit(sr);
                _drops[i] = new Drop { Sr = sr, T = go.transform };
            }
        }

        /// <summary>
        /// 방울 count개를 dir 쪽 ±spreadDeg 부채꼴로 뿌린다. 색은 a~b 사이, stainChance 확률로 땅에 얼룩을 남긴다.
        /// </summary>
        public void Emit(Vector2 at, Vector2 dir, float spreadDeg, float speedMin, float speedMax, float upMin, float upMax,
            float sizeMin, float sizeMax, Color a, Color b, float stainChance, GoreMatter matter, int count)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            float spread = spreadDeg * Mathf.Deg2Rad;
            var dropSprite = GoreSprites.Drop;
            for (int i = 0; i < count; i++)
            {
                // 가운데로 몰리게(두 난수 평균): 줄기처럼 뿜어지고 가장자리는 드문드문.
                float ang = baseAngle + (Random.Range(-spread, spread) + Random.Range(-spread, spread)) * 0.5f;
                float speed = Random.Range(speedMin, speedMax);
                ref var d = ref Take();
                d.Active = true;
                d.Puff = false;
                d.Origin = at;
                d.Pos = at + Random.insideUnitCircle * 0.08f;
                d.Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                d.H = Random.Range(0.12f, 0.4f);
                d.VH = Random.Range(upMin, upMax);
                d.Age = 0f;
                d.Life = MaxLife;
                d.Size = Random.Range(sizeMin, sizeMax);
                d.Color = Color.Lerp(a, b, Random.value);
                d.Stain = Random.value < stainChance;
                d.Matter = matter;
                // 피 안개로 쓰였던 칸이면 방울 그림으로 되돌린다.
                if (d.Sr.sprite != dropSprite) d.Sr.sprite = dropSprite;
                d.Sr.sortingOrder = DropOrder;
                d.Sr.color = d.Color;
                d.T.rotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
                d.T.position = new Vector3(d.Pos.x, d.Pos.y + d.H, 0f);
                d.T.localScale = new Vector3(d.Size, d.Size, 1f);
                d.Sr.enabled = true;
            }
        }

        /// <summary>맞은 자리에 잠깐 부풀었다 사라지는 피 안개(크게 튀는 느낌).</summary>
        public void Puff(Vector2 at, float size, Color color, float life, Sprite sprite)
        {
            ref var d = ref Take();
            d.Active = true;
            d.Puff = true;
            d.Stain = false;
            d.Pos = at;
            d.Age = 0f;
            d.Life = Mathf.Max(0.05f, life);
            d.Size = size;
            d.Color = color;
            d.Sr.sprite = sprite ? sprite : GoreSprites.Drop;
            d.Sr.sortingOrder = PuffOrder;
            d.Sr.color = color;
            d.T.SetPositionAndRotation(new Vector3(at.x, at.y + 0.15f, 0f), Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            d.T.localScale = new Vector3(size, size, 1f);
            d.Sr.enabled = true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _drops.Length; i++)
            {
                ref var d = ref _drops[i];
                if (!d.Active) continue;
                d.Age += dt;
                if (d.Puff)
                {
                    float k = d.Age / d.Life;
                    if (k >= 1f)
                    {
                        Off(ref d);
                        continue;
                    }
                    float s = d.Size * (1f + 0.8f * k);
                    d.T.localScale = new Vector3(s, s, 1f);
                    var c = d.Color;
                    c.a *= (1f - k) * (1f - k);
                    d.Sr.color = c;
                    continue;
                }
                d.VH -= Gravity * dt;
                d.H += d.VH * dt;
                d.Vel *= Mathf.Max(0f, 1f - 2.2f * dt);
                d.Pos += d.Vel * dt;
                if (d.H <= 0f || d.Age >= d.Life)
                {
                    if (d.Stain && d.H <= 0f) _owner.OnDropLanded(d.Origin, d.Pos, d.Vel, d.Size, d.Matter);
                    Off(ref d);
                    continue;
                }
                // 화면에서 보이는 속도(바닥 속도 + 높이 속도) 쪽으로 늘여 그린다.
                float sx = d.Vel.x;
                float sy = d.Vel.y + d.VH;
                float sp = Mathf.Sqrt(sx * sx + sy * sy);
                float stretch = 1f + Mathf.Min(2.2f, sp * 0.18f);
                float angle = sp > 0.01f ? Mathf.Atan2(sy, sx) * Mathf.Rad2Deg : 0f;
                d.T.SetPositionAndRotation(new Vector3(d.Pos.x, d.Pos.y + d.H, 0f), Quaternion.Euler(0f, 0f, angle));
                d.T.localScale = new Vector3(d.Size * stretch, d.Size, 1f);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _drops.Length; i++)
                if (_drops[i].Active) Off(ref _drops[i]);
        }

        /// <summary>고리의 다음 칸(가장 오래된 것). 아직 날고 있으면 그대로 빼앗는다.</summary>
        ref Drop Take()
        {
            ref var d = ref _drops[_next];
            _next = (_next + 1) % _drops.Length;
            return ref d;
        }

        static void Off(ref Drop d)
        {
            d.Active = false;
            if (d.Sr) d.Sr.enabled = false;
        }
    }
}
