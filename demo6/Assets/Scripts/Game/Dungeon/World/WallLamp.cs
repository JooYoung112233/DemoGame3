using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 벽 등잔(3차 초안 2-4·2-5). 꺼진 채로는 빛을 받는 어두운 받침만 보여 갱도지기 등잔을 비춰야 찾는다.
    /// F 1초(켜는 1초 노출)로 켜면 밝은 5·흐린 8 빛이 영구로 남는다(경험치 1U는 PlayerProgress). 만들 때 이미 켠 것이면 켠 채로 둔다.
    /// 숨은 방 단서: 켠 등잔 14유닛 안에 아직 안 부순 판자벽이 있으면 불꽃이 그쪽으로 기울고 조금 더 흔들린다(판자 틈으로 바람이 빨려 듦).
    /// 층의 등잔을 모두 켜면 알림을 띄운다(숨은 방 칸의 흐린 '?'는 큰 지도가 그린다).
    /// 빛을 개수로 쓰기(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5-6, 2026-10-07): 켤 때 기름 병 1병(원정 몫 ExpeditionLeg.Oil)을 쓴다. 없으면 켜지 않고 알린다.
    /// 단서 알림·'?'는 '켠 등잔 3개'(DownRules.CluesShown). 플레이어가 켠 등잔 곁은 작은 다시 서는 곳이 된다(DungeonState.RespawnLamp, 말뚝에 닿으면 지움).
    /// 빛 크기(밝은 5·흐린 8)와 내 등잔은 그대로.
    /// </summary>
    public sealed class WallLamp : Interactable
    {
        const float Hold = 1f;
        const float LeanRange = 14f;
        const float LeanAmount = 0.6f;
        /// <summary>등잔을 벽 면에서 이만큼 떨어뜨려 빛 중심이 벽(그림자 드리우는 물체) 경계에 걸리지 않게 한다.</summary>
        const float WallNudge = 0.3f;
        const float ProbeRadius = 0.6f;

        static readonly Color SconceColor = new Color(0.3f, 0.27f, 0.24f);
        static readonly Color BowlColor = new Color(0.42f, 0.38f, 0.33f);
        static readonly Color FlameOuter = new Color(1f, 0.74f, 0.38f, 0.95f);
        static readonly Color FlameInner = new Color(1f, 0.96f, 0.82f, 1f);
        static readonly Color HaloColor = new Color(1f, 0.86f, 0.58f, 0.22f);

        string _id;
        string _label;
        bool _lit;
        float _seed;
        Transform _anchor;
        Transform _bracket;
        Transform _flame;
        SpriteRenderer _halo;
        Light2D[] _lights;
        float[] _baseIntensity;
        readonly List<DungeonEdge> _planks = new List<DungeonEdge>();

        public override string Prompt => !OilCounted ? "벽 등잔 켜기" : Oil > 0 ? "벽 등잔 켜기 · 기름 " + Oil + "병" : "벽 등잔 — 기름 병이 없다";
        public override float HoldSeconds => OilCounted && Oil <= 0 ? 0f : Hold;

        /// <summary>기름을 세는 장면인가(원정 몫이 있는 던전). 없으면(시험 장면) 예전처럼 그냥 켠다.</summary>
        static bool OilCounted => DungeonRoot.Instance && DungeonRoot.Instance.Leg != null;
        static int Oil => OilCounted ? DungeonRoot.Instance.Leg.Oil : 0;

        /// <summary>등잔 곁 다시 서는 자리: 벽에서 떨어진 쪽으로 1.2(벽에 걸리면 등잔 자리).</summary>
        public Vector2 RespawnPoint
        {
            get
            {
                Vector2 away = _anchor && _anchor.localPosition.sqrMagnitude > 0.0001f ? ((Vector2)_anchor.localPosition).normalized : Vector2.down;
                Vector2 p = Position + away * 1.2f;
                return Physics2D.OverlapCircle(p, 0.4f, Layers.WallMask) ? Position : p;
            }
        }
        public override bool Available => !_lit;
        public override Vector2 Position => _anchor ? (Vector2)_anchor.position : (Vector2)transform.position;
        public bool Lit => _lit;

        public static WallLamp Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("WallLamp " + f.Id, pos);
            var lamp = go.AddComponent<WallLamp>();
            lamp._id = f.Id;
            lamp._label = string.IsNullOrEmpty(f.Label) ? "벽 등잔" : f.Label;
            lamp._seed = Random.value * 10f;
            lamp.Build(pos);
            var state = WorldProps.State;
            if (state != null && state.IsDone(f.Id)) lamp.Ignite(false);
            return lamp;
        }

        void Build(Vector2 pos)
        {
            _anchor = new GameObject("Anchor").transform;
            _anchor.SetParent(transform, false);
            int order = WorldProps.SortY(pos.y, 5);
            _bracket = WorldProps.Shape(_anchor, "Bracket", new Vector2(0f, 0.2f), new Vector2(0.14f, 0.4f), ShapeSprites.Square, SconceColor, order, false).transform;
            // Strict overhead: concentric top faces, with the flame read as a small plan-view area.
            WorldProps.Shape(_anchor, "Bowl", Vector2.zero, new Vector2(0.44f, 0.44f), ShapeSprites.Circle, BowlColor, order + 1, false);
            WorldProps.Shape(_anchor, "Bowl recess", Vector2.zero, new Vector2(0.32f, 0.32f), ShapeSprites.Circle, new Color(0.17f, 0.15f, 0.13f), order + 2, false);
            WorldProps.Shape(_anchor, "Worn bowl lip", Vector2.zero, new Vector2(0.40f, 0.40f), ShapeSprites.Ring, new Color(0.56f, 0.49f, 0.36f), order + 2, false);
            WorldProps.Shape(_anchor, "Wick", Vector2.zero, new Vector2(0.05f, 0.05f), ShapeSprites.Circle, SconceColor, order + 2, false);

            _halo = WorldProps.Shape(_anchor, "Halo", Vector2.zero, new Vector2(1.1f, 1.1f), WorldProps.SoftDot, HaloColor, order + 2, true);
            _halo.enabled = false;
            _flame = new GameObject("Flame").transform;
            _flame.SetParent(_anchor, false);
            _flame.localPosition = Vector3.zero;
            _flame.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Keep the existing draft direction animation; the offset inner spot moves within a top-view flame.
            WorldProps.Shape(_flame, "Outer", Vector2.zero, new Vector2(0.28f, 0.28f), ShapeSprites.Circle, FlameOuter, order + 3, true);
            WorldProps.Shape(_flame, "Inner", new Vector2(0.025f, 0f), new Vector2(0.11f, 0.11f), ShapeSprites.Circle, FlameInner, order + 4, true);
            _flame.gameObject.SetActive(false);
        }

        void Start()
        {
            NudgeFromWall();
            var root = DungeonRoot.Instance;
            if (root && root.World != null)
                foreach (var e in root.World.Edges)
                    if (e.Kind == EdgeKind.Plank) _planks.Add(e);
        }

        /// <summary>둘레 8방향을 찔러 벽 쪽을 찾고, 비어 있는 쪽 평균 방향으로 조금 옮긴다. 받침 막대는 벽 쪽으로 뻗는다.</summary>
        void NudgeFromWall()
        {
            Vector2 pos = transform.position;
            Vector2 open = Vector2.zero;
            bool anyBlocked = false;
            for (int i = 0; i < 8; i++)
            {
                Vector2 d = WorldProps.Rotate(Vector2.right, i * 45f);
                if (Physics2D.OverlapPoint(pos + d * ProbeRadius, Layers.WallMask)) anyBlocked = true;
                else open += d;
            }
            if (!anyBlocked || open.sqrMagnitude < 0.0001f) return;
            Vector2 away = open.normalized;
            _anchor.localPosition = away * WallNudge;
            // 받침 막대: 그릇에서 벽 쪽(away 반대)으로.
            Vector2 toWall = -away;
            float len = WallNudge + 0.35f;
            _bracket.localPosition = toWall * (len * 0.5f);
            _bracket.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toWall.y, toWall.x) * Mathf.Rad2Deg);
            _bracket.localScale = new Vector3(len, 0.14f, 1f);
        }

        public override void Interact()
        {
            if (_lit) return;
            if (OilCounted)
            {
                var leg = DungeonRoot.Instance.Leg;
                if (leg.Oil <= 0)
                {
                    DungeonEvents.Say(DownRules.NoOilLine);
                    WorldOverlay.Text(Position + Vector2.up * 0.9f, "기름 없음", new Color(0.85f, 0.8f, 0.72f, 0.9f), 0.85f);
                    return;
                }
                leg.Oil--;
            }
            Ignite(true);
            var state = WorldProps.State;
            if (state != null) state.RespawnLamp = RespawnPoint;
        }

        void Ignite(bool announce)
        {
            if (_lit) return;
            _lit = true;
            DungeonLighting.WallLampLight(_anchor);
            _lights = _anchor.GetComponentsInChildren<Light2D>();
            _baseIntensity = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _baseIntensity[i] = _lights[i].intensity;
            _flame.gameObject.SetActive(true);
            _halo.enabled = true;
            if (!announce) return;
            Sfx.Play(SfxKind.Lamp);
            var state = WorldProps.State;
            if (state == null) return;
            if (!state.Complete(_id, DiscoveryKind.WallLamp, Position, _label)) return;
            // 단서는 '켠 등잔 3개'에서 한 번(층 등잔이 셋보다 적으면 모두).
            CountLamps(state, out int lit, out int total);
            if (!DownRules.CluesShown(lit, total) || DownRules.CluesShown(lit - 1, total)) return;
            DungeonEvents.Say(HiddenRoomLeft(state) ? "등잔 불빛이 갱도를 비춘다 — 지도에 '?'가 떠올랐다" : "등잔 불빛이 갱도를 비춘다");
        }

        /// <summary>층의 벽 등잔 수와 켠 수.</summary>
        public static void CountLamps(DungeonState state, out int lit, out int total)
        {
            lit = 0;
            total = 0;
            foreach (var e in state.OneTime.Values)
            {
                if (e.Kind != DiscoveryKind.WallLamp) continue;
                total++;
                if (e.Done) lit++;
            }
        }

        static bool HiddenRoomLeft(DungeonState state)
        {
            foreach (var e in state.OneTime.Values)
                if (e.Kind == DiscoveryKind.HiddenRoom && !e.Done) return true;
            return false;
        }

        /// <summary>가장 가까운 안 부순 판자벽 문 가운데(없으면 null).</summary>
        DungeonEdge NearestPlank(Vector2 from, out float distance)
        {
            DungeonEdge best = null;
            distance = float.MaxValue;
            foreach (var e in _planks)
            {
                if (e == null || e.Opened) continue;
                float d = (e.DoorCenter - from).magnitude;
                if (d >= distance) continue;
                distance = d;
                best = e;
            }
            return best;
        }

        void Update()
        {
            if (!_lit || !_flame) return;
            Vector2 from = _anchor.position;
            float t = Time.time + _seed;
            Vector2 lean = Vector2.zero;
            bool draft = false;
            var plank = NearestPlank(from, out float dist);
            if (plank != null && dist <= LeanRange && dist > 0.01f)
            {
                lean = (plank.DoorCenter - from) / dist * LeanAmount;
                draft = true;
            }
            float wobble = Mathf.Sin(t * 7.3f) * 0.08f + Mathf.Sin(t * 12.1f) * 0.05f;
            if (draft) wobble *= 1.6f;
            Vector2 up = (Vector2.up + lean + new Vector2(wobble, 0f)).normalized;
            _flame.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg);
            float len = 1f + Mathf.Sin(t * 9.7f) * 0.08f + Mathf.Sin(t * 15.3f) * (draft ? 0.08f : 0.04f);
            _flame.localScale = new Vector3(len, 1f, 1f);
            if (_lights == null) return;
            float flicker = 1f + Mathf.Sin(t * 8.9f) * (draft ? 0.05f : 0.02f) + Mathf.Sin(t * 13.7f) * (draft ? 0.03f : 0.01f);
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i]) _lights[i].intensity = _baseIntensity[i] * flicker;
        }
    }
}
