using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥에 남는 잔혹 한 겹(피 얼룩·웅덩이, 시체, 살 조각). 기획/다크판타지-분위기-1차.md '잔혹', 계약서 C.
    /// 고정 크기 고리 버퍼: 상한(cap)을 넘으면 가장 오래된 것부터 흐려 사라지고, 여유 칸(spare)이 흐려지는 동안 새것을 받는다.
    /// 렌더러는 처음 그 칸을 쓸 때 한 번 만들고 계속 돌려 쓴다(매 프레임 할당 없음). 기본 스프라이트 재질이라 빛을 받는다.
    /// 시간은 게임 시간이라 히트스톱·멈춤 동안 번짐·내려앉음·흐려짐도 멈춘다.
    /// </summary>
    public sealed class GoreLayer
    {
        public struct Piece
        {
            public SpriteRenderer Sr;
            public Transform T;
            public bool Active;
            public float Born;
            /// <summary>지금 바탕 색(흐려짐 곱하기 전).</summary>
            public Color Color;

            // 번짐(웅덩이)·내려앉음(시체) 애니메이션.
            public bool Animating;
            public float AnimStart;
            public float AnimTime;
            public bool EaseCubic;
            public Vector3 FromScale;
            public Vector3 ToScale;
            public Color FromColor;
            public Color ToColor;
            public float FromAngle;
            public float ToAngle;

            // 흐려짐(-1 = 아님).
            public float FadeStart;
            public float FadeTime;

            // 날기(살 조각): 바닥 위 자리 Pos + 높이 H(화면에서는 y로 올려 그림).
            public bool Flying;
            public Vector2 Pos;
            public Vector2 Vel;
            public float H;
            public float VH;
            public float Angle;
            public float Spin;
            public int Bounces;
            public int GroundOrder;
            public float Size;
            public GoreMatter Matter;
        }

        /// <summary>날아가는 조각·방울의 중력(유닛/초²). GoreDrops와 같은 값.</summary>
        public const float Gravity = 22f;

        readonly Piece[] _items;
        readonly int _spare;
        readonly Transform _parent;
        readonly string _name;
        readonly GoreSystem _owner;
        int _next;

        /// <summary>살아 있는 시간. 던전은 끝없음(원정 다시 시작 때 지움), 전투 시험장은 25초.</summary>
        public float Lifetime = float.PositiveInfinity;
        /// <summary>수명이 다했을 때 흐려지는 시간.</summary>
        public float LifeFade = 2f;
        /// <summary>상한을 넘어 밀려날 때 흐려지는 시간.</summary>
        public float CapFade = 1.5f;

        public int Cap => _items.Length - _spare;

        public GoreLayer(GoreSystem owner, Transform parent, string name, int cap, int spare)
        {
            _owner = owner;
            _parent = parent;
            _name = name;
            _spare = Mathf.Max(1, spare);
            _items = new Piece[Mathf.Max(1, cap) + _spare];
        }

        /// <summary>
        /// 새 조각을 놓는다. 고리의 다음 칸을 쓰고(살아 있으면 바로 바꿈), 상한만큼 앞선 가장 오래된 것을 흐리기 시작한다.
        /// 돌려받은 ref로 번짐·날기 같은 설정을 더한다.
        /// </summary>
        public ref Piece Spawn(Sprite sprite, Vector2 pos, float angle, Vector3 scale, Color color, int order, bool flipX = false)
        {
            int i = _next;
            _next = (_next + 1) % _items.Length;
            ref var p = ref _items[i];
            if (!p.Sr) Create(ref p);
            float now = Time.time;
            p.Active = true;
            p.Born = now;
            p.Color = color;
            p.Animating = false;
            p.AnimTime = 0f;
            p.FadeStart = -1f;
            p.FadeTime = 1f;
            p.Flying = false;
            p.Pos = pos;
            p.Vel = Vector2.zero;
            p.H = 0f;
            p.VH = 0f;
            p.Angle = angle;
            p.Spin = 0f;
            p.Bounces = 0;
            p.GroundOrder = order;
            p.Size = Mathf.Max(scale.x, scale.y);
            p.Matter = GoreMatter.None;
            p.Sr.sprite = sprite;
            p.Sr.flipX = flipX;
            p.Sr.color = color;
            p.Sr.sortingOrder = order;
            p.T.SetPositionAndRotation(new Vector3(pos.x, pos.y, 0f), Quaternion.Euler(0f, 0f, angle));
            p.T.localScale = scale;
            p.Sr.enabled = true;

            ref var old = ref _items[(i + _spare) % _items.Length];
            if (old.Active && old.FadeStart < 0f) BeginFade(ref old, CapFade, now);
            return ref p;
        }

        /// <summary>번짐·내려앉음: 지금 모습에서 목표 크기·색·각도로 seconds 동안 옮겨 간다.</summary>
        public static void Animate(ref Piece p, Vector3 toScale, Color toColor, float toAngle, float seconds, bool easeCubic)
        {
            p.Animating = seconds > 0f;
            p.AnimStart = Time.time;
            p.AnimTime = Mathf.Max(0.0001f, seconds);
            p.EaseCubic = easeCubic;
            p.FromScale = p.T.localScale;
            p.ToScale = toScale;
            p.FromColor = p.Color;
            p.ToColor = toColor;
            p.FromAngle = p.Angle;
            p.ToAngle = toAngle;
            if (!p.Animating) Finish(ref p);
        }

        /// <summary>날려 보낸다(살 조각). 땅에 닿으면 바닥 정렬로 내려가고 GoreSystem에 알린다(작은 얼룩).</summary>
        public static void Launch(ref Piece p, Vector2 velocity, float height, float upSpeed, float spin, int flyingOrder, GoreMatter matter)
        {
            p.Flying = true;
            p.Vel = velocity;
            p.H = Mathf.Max(0.01f, height);
            p.VH = upSpeed;
            p.Spin = spin;
            p.Bounces = 0;
            p.Matter = matter;
            p.Sr.sortingOrder = flyingOrder;
            p.T.position = new Vector3(p.Pos.x, p.Pos.y + p.H, 0f);
        }

        public void Tick(float dt, float now)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                ref var p = ref _items[i];
                if (!p.Active) continue;
                bool colorDirty = false;
                if (p.Flying && dt > 0f) Fly(ref p, dt);
                if (p.Animating)
                {
                    float k = Mathf.Clamp01((now - p.AnimStart) / p.AnimTime);
                    float e = p.EaseCubic ? 1f - (1f - k) * (1f - k) * (1f - k) : 1f - (1f - k) * (1f - k);
                    p.Color = Color.Lerp(p.FromColor, p.ToColor, e);
                    p.Angle = Mathf.LerpAngle(p.FromAngle, p.ToAngle, e);
                    p.T.localScale = Vector3.LerpUnclamped(p.FromScale, p.ToScale, e);
                    p.T.rotation = Quaternion.Euler(0f, 0f, p.Angle);
                    colorDirty = true;
                    if (k >= 1f) p.Animating = false;
                }
                if (p.FadeStart < 0f && now - p.Born >= Lifetime) BeginFade(ref p, LifeFade, now);
                if (p.FadeStart >= 0f)
                {
                    float f = (now - p.FadeStart) / p.FadeTime;
                    if (f >= 1f)
                    {
                        Deactivate(ref p);
                        continue;
                    }
                    var c = p.Color;
                    c.a *= 1f - f;
                    p.Sr.color = c;
                }
                else if (colorDirty) p.Sr.color = p.Color;
            }
        }

        /// <summary>모두 지운다(원정 다시 시작: 암전 중이라 바로 지운다).</summary>
        public void Clear()
        {
            for (int i = 0; i < _items.Length; i++)
                if (_items[i].Active) Deactivate(ref _items[i]);
            _next = 0;
        }

        void Fly(ref Piece p, float dt)
        {
            p.VH -= Gravity * dt;
            p.H += p.VH * dt;
            p.Pos += p.Vel * dt;
            p.Vel *= Mathf.Max(0f, 1f - 1.2f * dt);
            p.Angle += p.Spin * dt;
            if (p.H <= 0f)
            {
                p.H = 0f;
                _owner.OnPieceLanded(p.Pos, p.Size, p.Matter);
                if (p.Bounces == 0 && p.VH < -3f)
                {
                    // 한 번 튀고 눕는다(철퍽 자국이 둘).
                    p.Bounces = 1;
                    p.VH = -p.VH * 0.28f;
                    p.Vel *= 0.45f;
                    p.Spin *= 0.5f;
                    p.H = 0.001f;
                }
                else
                {
                    p.Flying = false;
                    p.VH = 0f;
                    p.Sr.sortingOrder = p.GroundOrder;
                }
            }
            p.T.SetPositionAndRotation(new Vector3(p.Pos.x, p.Pos.y + p.H, 0f), Quaternion.Euler(0f, 0f, p.Angle));
        }

        static void Finish(ref Piece p)
        {
            p.Color = p.ToColor;
            p.Angle = p.ToAngle;
            p.T.localScale = p.ToScale;
            p.T.rotation = Quaternion.Euler(0f, 0f, p.Angle);
            p.Sr.color = p.Color;
            p.Animating = false;
        }

        static void BeginFade(ref Piece p, float seconds, float now)
        {
            p.FadeStart = now;
            p.FadeTime = Mathf.Max(0.05f, seconds);
        }

        static void Deactivate(ref Piece p)
        {
            p.Active = false;
            p.Animating = false;
            p.Flying = false;
            if (p.Sr) p.Sr.enabled = false;
        }

        void Create(ref Piece p)
        {
            var go = new GameObject(_name);
            go.transform.SetParent(_parent, false);
            p.T = go.transform;
            p.Sr = go.AddComponent<SpriteRenderer>();
            p.Sr.enabled = false;
        }
    }
}
