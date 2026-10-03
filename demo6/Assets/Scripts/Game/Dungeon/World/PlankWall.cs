using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 판자벽(3차 초안 2-3 ':', 2-5 숨은 방). 처음부터 부술 수 있고 일반 벽과 똑같이 보인다(Palette.Wall, 같은 그림자).
    /// 기본공격(단계 모양 + 0.5 여유)·회오리(반경 + 0.5)·검풍(벽에 막힌 자리 1유닛 안) 2타에 부서진다.
    /// 맞으면 '탁' 소리·짧은 히트스톱·흔들림·나뭇조각으로 답한다. 일반 벽은 '퉁' 소리만 나고 히트스톱이 없어(WorldReactions) 둘을 견주면 찾을 수 있다.
    /// 단서(빛을 받는 그림이라 등잔 빛 안에서만 보임): 판자 앞 바닥 먼지 줄, 틈으로 빨려 드는 먼지 알갱이. 켠 벽 등잔 불꽃이 이쪽으로 기운다(WallLamp).
    /// 부수면 숨은 방 발견(5U, 경험치는 PlayerProgress).
    /// </summary>
    public sealed class PlankWall : MonoBehaviour
    {
        public const int HitsToBreak = 2;
        /// <summary>기본공격·회오리 판정 여유(계약서: 사거리 + 0.5).</summary>
        public const float HitSlack = 0.5f;
        /// <summary>쌍검 연타(0.1초 간격 2타)는 한 번으로 센다. 회오리 타(0.2초 간격)는 따로 센다.</summary>
        const float HitGap = 0.15f;
        const float WaveRange = 1f;
        const float ShakeTime = 0.18f;
        const int DustSpecks = 11;
        const int Motes = 6;
        const float MoteCycle = 3.2f;

        static readonly Color PlankWood = new Color(0.5f, 0.38f, 0.25f);
        static readonly Color DustColor = new Color(0.78f, 0.74f, 0.66f);

        DungeonEdge _edge;
        Rect _box;
        string _id;
        int _hits;
        float _lastHit = -999f;
        float _shake;
        Transform _visual;
        Vector3 _visualHome;

        // 먼지 알갱이(빛을 받음): 바깥에서 틈으로 빨려 든다.
        Transform[] _motes;
        SpriteRenderer[] _moteSprites;
        float[] _motePhase;
        float[] _moteLane;
        Vector2[] _moteNormal;

        /// <summary>문틈을 채우는 Wall 레이어 물체를 만든다.</summary>
        public static GameObject Create(DungeonEdge edge)
        {
            var go = DungeonWorld.Block(DungeonRoot.Instance ? DungeonRoot.Instance.transform : null, "PlankWall", edge.DoorCenter, edge.DoorSize, Palette.Wall, false);
            var plank = go.AddComponent<PlankWall>();
            plank.Setup(edge);
            return go;
        }

        void Setup(DungeonEdge edge)
        {
            _edge = edge;
            _box = WorldGeometry.DoorRect(edge);
            _id = "f" + WorldProps.Floor + ".plank." + edge.A.Id + "-" + edge.B.Id;
            _visual = transform.Find("Visual");
            if (_visual) _visualHome = _visual.localPosition;

            var hidden = HiddenSide(edge);
            var state = WorldProps.State;
            // 조사율·큰 지도 '?'에 들어가도록 만들 때 등록한다(지도 아이콘은 숨은 방 칸 가운데).
            if (state != null) state.Register(_id, DiscoveryKind.HiddenRoom, hidden, hidden.Center, "숨은 방");

            int clueSides = 0;
            if (edge.A.Piece != PieceKind.Hidden) clueSides++;
            if (edge.B.Piece != PieceKind.Hidden) clueSides++;
            _motes = new Transform[Motes * clueSides];
            _moteSprites = new SpriteRenderer[_motes.Length];
            _motePhase = new float[_motes.Length];
            _moteLane = new float[_motes.Length];
            _moteNormal = new Vector2[_motes.Length];
            int next = 0;
            if (edge.A.Piece != PieceKind.Hidden) next = BuildClue(edge.A, next);
            if (edge.B.Piece != PieceKind.Hidden) BuildClue(edge.B, next);
        }

        /// <summary>숨은 방 쪽 칸(조각이 Hidden). 둘 다 아니면 B.</summary>
        static DungeonCell HiddenSide(DungeonEdge edge)
        {
            if (edge.A.Piece == PieceKind.Hidden) return edge.A;
            return edge.B;
        }

        /// <summary>판자 앞(그 칸 쪽) 바닥 먼지 줄과 먼지 알갱이. 둘 다 빛을 받아 어둠 속에서는 거의 안 보인다.</summary>
        int BuildClue(DungeonCell cell, int moteStart)
        {
            Vector2 axis = WorldProps.DoorAxis(_edge);
            Vector2 normal = WorldProps.DoorNormal(_edge);
            Vector2 toCell = cell.Center - _edge.DoorCenter;
            if (Vector2.Dot(toCell, normal) < 0f) normal = -normal;
            float half = DungeonWorld.DoorWidth * 0.5f - 0.2f;

            // 먼지 줄: 판자 틈 바로 앞에 끌려 모인 먼지(바닥 그림).
            for (int i = 0; i < DustSpecks; i++)
            {
                float u = Mathf.Lerp(-half, half, i / (float)(DustSpecks - 1)) + Random.Range(-0.12f, 0.12f);
                float v = 0.72f + Random.Range(-0.14f, 0.14f);
                var c = DustColor;
                c.a = Random.Range(0.25f, 0.45f);
                Vector2 local = axis * u + normal * v;
                float angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg + Random.Range(-8f, 8f);
                WorldProps.Shape(transform, "Dust", local, new Vector2(Random.Range(0.18f, 0.42f), Random.Range(0.04f, 0.08f)), ShapeSprites.Square, c, WorldProps.FloorDecalOrder, false, angle);
            }
            for (int i = 0; i < 5; i++)
            {
                float u = Random.Range(-half, half);
                var c = DustColor;
                c.a = Random.Range(0.15f, 0.28f);
                Vector2 local = axis * u + normal * (1.08f + Random.Range(-0.1f, 0.1f));
                WorldProps.Shape(transform, "Dust", local, new Vector2(Random.Range(0.1f, 0.25f), 0.05f), ShapeSprites.Square, c, WorldProps.FloorDecalOrder, false, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg);
            }

            // 먼지 알갱이: 바깥에서 틈 쪽으로 천천히 끌려 든다(바람이 숨은 방으로 빨려 듦).
            int order = WorldProps.SortY(_edge.DoorCenter.y, 500);
            for (int i = 0; i < Motes; i++)
            {
                int k = moteStart + i;
                var c = DustColor;
                c.a = 0f;
                float size = Random.Range(0.05f, 0.09f);
                var sr = WorldProps.Shape(transform, "Mote", Vector2.zero, new Vector2(size, size), WorldProps.SoftDot, c, order, false);
                _motes[k] = sr.transform;
                _moteSprites[k] = sr;
                _motePhase[k] = i / (float)Motes;
                _moteLane[k] = Random.Range(-half, half);
                _moteNormal[k] = normal;
            }
            return moteStart + Motes;
        }

        void OnEnable()
        {
            CombatEvents.PlayerSwing += OnSwing;
            CombatEvents.PlayerWhirl += OnWhirl;
            CombatEvents.WaveHitWall += OnWave;
        }

        void OnDisable()
        {
            CombatEvents.PlayerSwing -= OnSwing;
            CombatEvents.PlayerWhirl -= OnWhirl;
            CombatEvents.WaveHitWall -= OnWave;
        }

        /// <summary>아직 안 부서졌는가.</summary>
        public bool Intact => _edge != null && !_edge.Opened;
        /// <summary>마지막으로 맞은 프레임(부서진 프레임 포함). WorldReactions가 같은 휘두름에 '퉁'을 겹쳐 내지 않게 본다.</summary>
        public int LastHitFrame { get; private set; } = -1;

        /// <summary>이 휘두름(단계 모양 + 0.5 여유)이 판자에 닿는가.</summary>
        public bool TouchedBySwing(Vector2 origin, Vector2 dir, ComboStep step) =>
            Intact && step != null && WorldGeometry.StepTouchesRect(_box, origin, dir, step, HitSlack);

        /// <summary>이 원(회오리 반경 + 0.5 여유)이 판자에 닿는가.</summary>
        public bool TouchedByCircle(Vector2 origin, float radius) =>
            Intact && WorldGeometry.Distance(_box, origin) <= radius + HitSlack;

        void OnSwing(Vector2 origin, Vector2 dir, ComboStep step, bool hitEnemy)
        {
            if (!TouchedBySwing(origin, dir, step)) return;
            Hit(_box.center - origin);
        }

        void OnWhirl(Vector2 origin, float radius, bool hitEnemy)
        {
            if (!TouchedByCircle(origin, radius)) return;
            Hit(_box.center - origin);
        }

        void OnWave(Vector2 point, Vector2 dir)
        {
            if (!Intact) return;
            if (WorldGeometry.Distance(_box, point) > WaveRange) return;
            Hit(dir);
        }

        void Hit(Vector2 dir)
        {
            LastHitFrame = Time.frameCount;
            if (Time.time - _lastHit < HitGap) return;
            _lastHit = Time.time;
            if (dir.sqrMagnitude < 0.0001f) dir = WorldProps.DoorNormal(_edge);
            dir.Normalize();
            _hits++;
            if (_hits >= HitsToBreak)
            {
                Break(dir);
                return;
            }
            // 첫 타: 일반 벽 '퉁'과 다른 반응(타격음 + 짧은 히트스톱 + 흔들림 + 나뭇조각 몇 개).
            Sfx.Play(SfxKind.Hit);
            TimeScaleService.HitStop(0.05f);
            ScreenShake.Add(0.06f, 0.1f);
            _shake = ShakeTime;
            Vector2 face = WorldGeometry.Closest(_box, _box.center - dir * 3f);
            WorldDebris.Burst(face, new Vector2(0.6f, 0.6f), -dir, PlankWood, Palette.Wall, 5);
        }

        void Break(Vector2 dir)
        {
            Vector2 center = _edge.DoorCenter;
            Sfx.Play(SfxKind.WallBreak);
            TimeScaleService.HitStop(0.08f);
            ScreenShake.Add(0.18f, 0.2f);
            WorldDebris.Burst(center, _edge.DoorSize, dir, PlankWood, Palette.Wall, 22);
            _edge.Open();
            var state = WorldProps.State;
            if (state != null) state.Complete(_id, DiscoveryKind.HiddenRoom, center, "숨은 방");
            DungeonEvents.Say("썩은 판자 너머, 누군가 숨겨 둔 방이 있었다");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_shake > 0f && _visual)
            {
                _shake -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_shake / ShakeTime);
                _visual.localPosition = _shake > 0f ? _visualHome + (Vector3)(Random.insideUnitCircle * (0.07f * k)) : _visualHome;
            }
            if (dt <= 0f || _motes == null) return;
            Vector2 axis = WorldProps.DoorAxis(_edge);
            float time = Time.time;
            for (int i = 0; i < _motes.Length; i++)
            {
                if (!_motes[i]) continue;
                _motePhase[i] += dt / MoteCycle;
                if (_motePhase[i] >= 1f)
                {
                    _motePhase[i] -= 1f;
                    _moteLane[i] = Random.Range(-1.7f, 1.7f);
                }
                float p = _motePhase[i];
                float v = Mathf.Lerp(2.6f, 0.15f, p);
                float u = _moteLane[i] * (1f - 0.65f * p) + Mathf.Sin(time * 1.3f + i * 1.7f) * 0.15f;
                _motes[i].localPosition = axis * u + _moteNormal[i] * v;
                var c = DustColor;
                c.a = Mathf.Sin(p * Mathf.PI) * 0.55f;
                _moteSprites[i].color = c;
            }
        }
    }
}
