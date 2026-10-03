using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 공기(기획/다크판타지-분위기-1차.md '공기', 계약서 A).
    /// ① 등잔 빛 속 떠다니는 먼지: 플레이어 둘레(등잔 흐린 반경 안)만 56개를 돌려 쓴다. 빛을 받는 점이라 빛 밖에서는 보이지 않는다.
    /// ② 횃불 불티: 켠 벽 등잔·켠 말뚝 위로 오르는 주황 불티(빛 무시, 블룸으로 번짐). 48개 고리 풀, 플레이어 18유닛 안 불씨만.
    /// ③ 천장 흙먼지: 7~16초마다 플레이어 가까이, 그리고 소리(DungeonEvents.Noise: 금 간 벽·광맥·금고)·문이 열릴 때(EdgeOpened) 그 자리에 떨어진다.
    ///    빛을 받는 알갱이 + 옅은 먼지 구름.
    /// 게임 시간으로 움직여 멈춤·히트스톱 동안 함께 멈춘다. 매 프레임 할당이 없고 전역 Random 순서를 건드리지 않게 자기 난수를 쓴다.
    /// </summary>
    public sealed class DungeonAir : MonoBehaviour
    {
        const int DustCount = 56;
        const int EmberCount = 48;
        const int FallCount = 40;
        /// <summary>먼지·흙먼지는 액터(YSort 200~1200) 위, 효과(2900~) 아래. 시야 덮개(5000)가 시야 밖을 어둡게 한다.</summary>
        const int DustOrder = 2850;
        const int FallOrder = 2840;
        const int EmberOrder = 2960;
        const float EmberRange = 18f;
        /// <summary>불씨 하나가 초당 내는 불티 수.</summary>
        const float WallLampEmberRate = 3f;
        const float StakeEmberRate = 1.4f;
        const float SourceRefresh = 0.5f;
        const float FallMinGap = 7f;
        const float FallMaxGap = 16f;
        const float EventFallCooldown = 1.2f;
        const float EventFallRange = 14f;
        const float FallGravity = 5.5f;

        struct Mote
        {
            public Transform T;
            public SpriteRenderer Sprite;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;
            public float Life;
            public float Size;
            public float Alpha;
            public float Phase;
        }

        struct Ember
        {
            public Transform T;
            public SpriteRenderer Sprite;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;
            public float Life;
            public float Size;
            public float Phase;
            public bool Live;
        }

        struct Speck
        {
            public Transform T;
            public SpriteRenderer Sprite;
            public Vector2 Pos;
            public Vector2 Vel;
            public float LandY;
            public float Age;
            public float Life;
            public float Size;
            public float Alpha;
            public float LandedAt;
            public bool Cloud;
            public bool Landed;
            public bool Live;
        }

        Mote[] _dust;
        Ember[] _embers;
        Speck[] _falls;
        /// <summary>불씨 자리(x, y)와 초당 불티 수(z).</summary>
        readonly List<Vector3> _sources = new List<Vector3>(16);
        readonly System.Random _rng = new System.Random(6151);
        float _refresh;
        float _nextFall;
        float _lastEventFall = -99f;
        int _emberNext;
        int _fallNext;
        bool _on = true;

        /// <summary>시험·비교용: 끄면 먼지·불티·흙먼지를 모두 숨기고 멈춘다.</summary>
        public bool On
        {
            get => _on;
            set
            {
                if (_on == value) return;
                _on = value;
                if (!value) HideAll();
            }
        }

        void Awake()
        {
            var glow = ShapeSprites.Glow;
            _dust = new Mote[DustCount];
            for (int i = 0; i < DustCount; i++)
            {
                var sr = MakeSprite("Dust", glow, DustOrder, false);
                _dust[i] = new Mote { T = sr.transform, Sprite = sr, Age = 0f, Life = 0f };
            }
            _embers = new Ember[EmberCount];
            for (int i = 0; i < EmberCount; i++)
            {
                var sr = MakeSprite("Ember", glow, EmberOrder, true);
                sr.enabled = false;
                _embers[i] = new Ember { T = sr.transform, Sprite = sr };
            }
            _falls = new Speck[FallCount];
            for (int i = 0; i < FallCount; i++)
            {
                var sr = MakeSprite("Ceiling dust", ShapeSprites.Square, FallOrder, false);
                sr.enabled = false;
                _falls[i] = new Speck { T = sr.transform, Sprite = sr };
            }
            _nextFall = Time.time + Range(FallMinGap * 0.5f, FallMaxGap);
            DungeonEvents.Noise += OnNoise;
            DungeonEvents.EdgeOpened += OnEdgeOpened;
        }

        void OnDestroy()
        {
            DungeonEvents.Noise -= OnNoise;
            DungeonEvents.EdgeOpened -= OnEdgeOpened;
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, int order, bool unlit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = new Color(1f, 1f, 1f, 0f);
            if (unlit) RenderMaterials.MakeUnlit(sr);
            return sr;
        }

        float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        void Update()
        {
            if (!_on) return;
            var player = PlayerController.Instance;
            if (!player) return;
            float dt = Time.deltaTime;
            Vector2 center = player.transform.position;
            var lighting = DungeonLighting.Instance;
            float radius = lighting ? lighting.DimRadius : DungeonLighting.LampDim;

            UpdateDust(center, radius, dt);

            _refresh -= dt;
            if (_refresh <= 0f)
            {
                _refresh = SourceRefresh;
                RefreshSources(center);
            }
            if (dt > 0f)
            {
                for (int i = 0; i < _sources.Count; i++)
                {
                    var s = _sources[i];
                    if (_rng.NextDouble() < s.z * dt) SpawnEmber(new Vector2(s.x, s.y));
                }
            }
            UpdateEmbers(dt);

            if (Time.time >= _nextFall)
            {
                _nextFall = Time.time + Range(FallMinGap, FallMaxGap);
                if (!player.IsDown) RandomFall(center, radius);
            }
            UpdateFalls(dt);
        }

        // ── ① 먼지 ──

        void UpdateDust(Vector2 center, float radius, float dt)
        {
            Color baseColor = Palette.DustMote;
            float reach = (radius + 1f) * (radius + 1f);
            for (int i = 0; i < _dust.Length; i++)
            {
                ref var m = ref _dust[i];
                m.Age += dt;
                if (m.Life <= 0f || m.Age >= m.Life || (m.Pos - center).sqrMagnitude > reach) RespawnDust(ref m, center, radius, m.Life <= 0f);
                float t = m.Age + m.Phase;
                Vector2 sway = new Vector2(Mathf.Sin(t * 0.7f), Mathf.Cos(t * 0.53f)) * 0.06f;
                m.Pos += (m.Vel + sway) * dt;
                float env = Mathf.Sin(Mathf.Clamp01(m.Age / m.Life) * Mathf.PI);
                m.T.position = new Vector3(m.Pos.x, m.Pos.y, 0f);
                m.Sprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, m.Alpha * env);
            }
        }

        void RespawnDust(ref Mote m, Vector2 center, float radius, bool first)
        {
            float r = radius * Mathf.Sqrt((float)_rng.NextDouble());
            float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
            m.Pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            float va = (float)_rng.NextDouble() * Mathf.PI * 2f;
            m.Vel = new Vector2(Mathf.Cos(va), Mathf.Sin(va)) * Range(0.04f, 0.14f) + new Vector2(0f, -0.02f);
            m.Life = Range(5f, 11f);
            // 처음에는 나이를 흩어 한꺼번에 나타나지 않게 한다.
            m.Age = first ? Range(0f, m.Life) : 0f;
            m.Size = Range(0.04f, 0.09f);
            m.Alpha = Range(0.3f, 0.65f);
            m.Phase = Range(0f, 10f);
            m.T.localScale = new Vector3(m.Size, m.Size, 1f);
        }

        // ── ② 불티 ──

        void RefreshSources(Vector2 center)
        {
            _sources.Clear();
            var all = Interactable.All;
            float range2 = EmberRange * EmberRange;
            for (int i = 0; i < all.Count; i++)
            {
                var it = all[i];
                if (!it) continue;
                if (it is WallLamp lamp)
                {
                    if (!lamp.Lit) continue;
                    Vector2 p = lamp.Position + new Vector2(0f, 0.32f);
                    if ((p - center).sqrMagnitude <= range2) _sources.Add(new Vector3(p.x, p.y, WallLampEmberRate));
                }
                else if (it is Stake stake)
                {
                    if (!stake.Active) continue;
                    Vector2 p = (Vector2)stake.transform.position + new Vector2(0f, 0.95f);
                    if ((p - center).sqrMagnitude <= range2) _sources.Add(new Vector3(p.x, p.y, StakeEmberRate));
                }
            }
        }

        void SpawnEmber(Vector2 at)
        {
            ref var e = ref _embers[_emberNext];
            _emberNext = (_emberNext + 1) % _embers.Length;
            e.Pos = at + new Vector2(Range(-0.1f, 0.1f), Range(-0.03f, 0.06f));
            e.Vel = new Vector2(Range(-0.18f, 0.18f), Range(0.55f, 1.1f));
            e.Age = 0f;
            e.Life = Range(0.7f, 1.5f);
            e.Size = Range(0.09f, 0.16f);
            e.Phase = Range(0f, 6.28f);
            e.Live = true;
            e.Sprite.enabled = true;
        }

        void UpdateEmbers(float dt)
        {
            Color hot = Palette.EmberHot;
            Color cool = Palette.EmberCool;
            for (int i = 0; i < _embers.Length; i++)
            {
                ref var e = ref _embers[i];
                if (!e.Live) continue;
                e.Age += dt;
                if (e.Age >= e.Life)
                {
                    e.Live = false;
                    e.Sprite.enabled = false;
                    continue;
                }
                // 위로 오르며 좌우로 팔랑이고 점점 느려진다.
                e.Vel.x += Mathf.Sin(e.Age * 7f + e.Phase) * 0.6f * dt;
                e.Vel.y *= 1f - 0.35f * dt;
                e.Pos += e.Vel * dt;
                float t = e.Age / e.Life;
                var c = Color.Lerp(hot, cool, Mathf.Clamp01(t * 1.4f));
                float flicker = 0.75f + 0.25f * Mathf.Sin(e.Age * 31f + e.Phase);
                c.a = Mathf.Pow(1f - t, 1.2f) * flicker;
                float s = e.Size * (1f - 0.5f * t);
                e.T.position = new Vector3(e.Pos.x, e.Pos.y, 0f);
                e.T.localScale = new Vector3(s, s, 1f);
                e.Sprite.color = c;
            }
        }

        // ── ③ 천장 흙먼지 ──

        void RandomFall(Vector2 center, float radius)
        {
            for (int tries = 0; tries < 4; tries++)
            {
                float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
                Vector2 at = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Range(1.5f, Mathf.Min(5f, radius));
                if (Physics2D.OverlapPoint(at, Layers.WallMask)) continue;
                Fall(at, 10 + _rng.Next(5));
                return;
            }
        }

        void OnNoise(Vector2 pos, float radius)
        {
            if (!_on || Time.time - _lastEventFall < EventFallCooldown) return;
            var player = PlayerController.Instance;
            if (!player || (player.Position - pos).sqrMagnitude > EventFallRange * EventFallRange) return;
            _lastEventFall = Time.time;
            Fall(pos + new Vector2(Range(-0.8f, 0.8f), Range(-0.4f, 0.6f)), 14);
        }

        void OnEdgeOpened(DungeonEdge edge)
        {
            if (!_on || edge == null || Time.time - _lastEventFall < EventFallCooldown) return;
            var player = PlayerController.Instance;
            if (!player || (player.Position - edge.DoorCenter).sqrMagnitude > EventFallRange * EventFallRange) return;
            _lastEventFall = Time.time;
            Fall(edge.DoorCenter, 18);
        }

        /// <summary>at 자리에 흙먼지: 옅은 구름 하나 + 위(천장 쪽)에서 떨어져 내려앉는 알갱이 count개.</summary>
        void Fall(Vector2 at, int count)
        {
            ref var cloud = ref NextFall();
            cloud.Cloud = true;
            cloud.Pos = at + new Vector2(0f, 0.25f);
            cloud.Vel = new Vector2(Range(-0.06f, 0.06f), -0.16f);
            cloud.Age = 0f;
            cloud.Life = Range(1.8f, 2.6f);
            cloud.Size = Range(0.5f, 0.8f);
            cloud.Alpha = Range(0.22f, 0.32f);
            cloud.Landed = false;
            cloud.Live = true;
            cloud.Sprite.sprite = ShapeSprites.Glow;
            cloud.Sprite.enabled = true;
            for (int k = 0; k < count; k++)
            {
                ref var s = ref NextFall();
                s.Cloud = false;
                s.Pos = at + new Vector2(Range(-0.6f, 0.6f), Range(0.6f, 1.6f));
                s.LandY = at.y + Range(-0.5f, 0.4f);
                s.Vel = new Vector2(Range(-0.15f, 0.15f), -Range(0.3f, 0.8f));
                s.Age = 0f;
                s.Life = 3f;
                s.Size = Range(0.03f, 0.07f);
                s.Alpha = Range(0.6f, 0.95f);
                s.Landed = false;
                s.LandedAt = 0f;
                s.Live = true;
                s.Sprite.sprite = ShapeSprites.Square;
                s.Sprite.enabled = true;
            }
        }

        ref Speck NextFall()
        {
            ref var s = ref _falls[_fallNext];
            _fallNext = (_fallNext + 1) % _falls.Length;
            return ref s;
        }

        void UpdateFalls(float dt)
        {
            Color dustColor = Palette.CeilingDust;
            for (int i = 0; i < _falls.Length; i++)
            {
                ref var s = ref _falls[i];
                if (!s.Live) continue;
                s.Age += dt;
                float alpha;
                float size;
                if (s.Cloud)
                {
                    float t = s.Age / s.Life;
                    if (t >= 1f)
                    {
                        Hide(ref s);
                        continue;
                    }
                    s.Pos += s.Vel * dt;
                    size = s.Size * (1f + 1.6f * t);
                    alpha = s.Alpha * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * (1f - t * 0.4f);
                }
                else
                {
                    if (!s.Landed)
                    {
                        s.Vel.y -= FallGravity * dt;
                        s.Pos += s.Vel * dt;
                        if (s.Pos.y <= s.LandY)
                        {
                            s.Pos.y = s.LandY;
                            s.Landed = true;
                            s.LandedAt = s.Age;
                        }
                        if (s.Age >= s.Life)
                        {
                            Hide(ref s);
                            continue;
                        }
                        alpha = s.Alpha * Mathf.Clamp01(s.Age / 0.15f);
                    }
                    else
                    {
                        float since = s.Age - s.LandedAt;
                        if (since >= 0.6f)
                        {
                            Hide(ref s);
                            continue;
                        }
                        alpha = s.Alpha * (1f - since / 0.6f);
                    }
                    size = s.Size;
                }
                s.T.position = new Vector3(s.Pos.x, s.Pos.y, 0f);
                s.T.localScale = new Vector3(size, size, 1f);
                s.Sprite.color = new Color(dustColor.r, dustColor.g, dustColor.b, alpha);
            }
        }

        static void Hide(ref Speck s)
        {
            s.Live = false;
            s.Sprite.enabled = false;
        }

        void HideAll()
        {
            for (int i = 0; i < _dust.Length; i++)
            {
                _dust[i].Sprite.color = new Color(1f, 1f, 1f, 0f);
                _dust[i].Life = 0f;
            }
            for (int i = 0; i < _embers.Length; i++)
            {
                _embers[i].Live = false;
                _embers[i].Sprite.enabled = false;
            }
            for (int i = 0; i < _falls.Length; i++) Hide(ref _falls[i]);
        }
    }
}
