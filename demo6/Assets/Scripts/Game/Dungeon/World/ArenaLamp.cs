using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 보스방 벽 등잔(전투·보스 문서 3-5·3-6, 묶음 7). 일반 벽 등잔(WallLamp, 다른 작업 파일)과 달리 끌 수 있다:
    /// 처음에는 꺼짐(F 1.0초로 켬, 반경 5/8), 2단계 0.3초에 대각선 둘이 옆으로 눕다가 꺼지고(Extinguish) F 0.5초로 다시 켠다,
    /// 다시 도전하면 모두 꺼진 처음 상태(ResetUnlit), 처치하면 모두 다시 켜진다(LightUp). 발견 기록·경험치는 없다(한 번 받는 것이 아님).
    /// 시야(VisionSystem.CollectLitLamps)가 켠 ArenaLamp를 등잔 빛으로 센다(빛 안의 보스 몸이 보이게).
    /// 그림은 WallLamp의 받침·그릇·불꽃 도형을 흉내 낸 임시판이고, 빛은 DungeonLighting.WallLampLight(일렁임 포함)를 그대로 쓴다.
    /// 받침 막대는 가장 가까운 벽 면까지 뻗는다(등잔 자리는 벽에서 0.9 떨어져 있다).
    /// </summary>
    public sealed class ArenaLamp : Interactable
    {
        /// <summary>그림 배율(넓은 보스방 어둠에서 꺼진 받침을 찾을 수 있게 일반 벽 등잔보다 조금 크게). 빛 반경은 바꾸지 않는다.</summary>
        const float ArtScale = 1.4f;
        /// <summary>받침을 붙일 벽을 찾는 거리(네 방향).</summary>
        const float WallProbe = 1.6f;
        /// <summary>꺼질 때 불꽃이 옆으로 눕는 시간(실제로 빛이 사라지는 때).</summary>
        const float OutSeconds = 0.25f;

        static readonly Color SconceColor = new Color(0.3f, 0.27f, 0.24f);
        static readonly Color BowlColor = new Color(0.42f, 0.38f, 0.33f);
        static readonly Color BowlRecess = new Color(0.17f, 0.15f, 0.13f);
        static readonly Color BowlLip = new Color(0.56f, 0.49f, 0.36f);
        static readonly Color FlameOuter = new Color(1f, 0.74f, 0.38f, 0.95f);
        static readonly Color FlameInner = new Color(1f, 0.96f, 0.82f, 1f);
        static readonly Color HaloColor = new Color(1f, 0.86f, 0.58f, 0.22f);
        static readonly Color SootColor = new Color(0.1f, 0.09f, 0.08f, 0.85f);

        string _id = "";
        bool _relight;
        float _seed;
        /// <summary>꺼지는 중이면 0부터 OutSeconds까지(실제 시간), 아니면 음수.</summary>
        float _outTime = -1f;
        float _leanSign = 1f;
        /// <summary>빛 자리(배율 없음, Light2D 반경이 배율을 타지 않게).</summary>
        Transform _anchor;
        /// <summary>그림 뿌리(ArtScale).</summary>
        Transform _art;
        Transform _bracket;
        Transform _flame;
        SpriteRenderer _halo;
        SpriteRenderer _soot;
        Light2D[] _lights;
        float[] _baseIntensity;

        /// <summary>id(OgreDen.ArenaLampIdPrefix + 1~4).</summary>
        public string Id => _id;
        /// <summary>켜져 있는가(꺼지는 0.25초 동안은 아직 켜짐).</summary>
        public bool Lit { get; private set; }
        /// <summary>2단계에 꺼진 등잔인가(다시 켜기 F 0.5초).</summary>
        public bool Relight => _relight;
        public override string Prompt => "벽 등잔 켜기";
        public override float HoldSeconds => _relight ? OgreDen.LampRelightHold : OgreDen.LampHold;
        public override bool Available => !Lit;
        public override Vector2 Position => _anchor ? (Vector2)_anchor.position : (Vector2)transform.position;

        /// <summary>등잔 하나를 만든다(꺼진 채).</summary>
        public static ArenaLamp Create(Transform parent, string id, Vector2 pos)
        {
            var go = new GameObject("ArenaLamp " + id);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var lamp = go.AddComponent<ArenaLamp>();
            lamp._id = id ?? "";
            lamp._seed = Random.value * 10f;
            lamp._leanSign = Random.value < 0.5f ? -1f : 1f;
            lamp.Build(pos);
            return lamp;
        }

        void Build(Vector2 pos)
        {
            _anchor = new GameObject("Anchor").transform;
            _anchor.SetParent(transform, false);
            _art = new GameObject("Art").transform;
            _art.SetParent(_anchor, false);
            _art.localScale = new Vector3(ArtScale, ArtScale, 1f);
            int order = WorldProps.SortY(pos.y, 5);
            // 받침 막대: Start에서 벽 쪽으로 뻗는다(그 전에는 위로).
            _bracket = WorldProps.Shape(_anchor, "Bracket", new Vector2(0f, 0.3f), new Vector2(0.18f, 0.6f), ShapeSprites.Square, SconceColor, order, false).transform;
            WorldProps.Shape(_art, "Bowl", Vector2.zero, new Vector2(0.44f, 0.44f), ShapeSprites.Circle, BowlColor, order + 1, false);
            WorldProps.Shape(_art, "Bowl recess", Vector2.zero, new Vector2(0.32f, 0.32f), ShapeSprites.Circle, BowlRecess, order + 2, false);
            WorldProps.Shape(_art, "Worn bowl lip", Vector2.zero, new Vector2(0.40f, 0.40f), ShapeSprites.Ring, BowlLip, order + 2, false);
            WorldProps.Shape(_art, "Wick", Vector2.zero, new Vector2(0.05f, 0.05f), ShapeSprites.Circle, SconceColor, order + 2, false);
            // 꺼진 뒤 남는 그을음(2단계에 꺼진 등잔은 심지 둘레가 검다).
            _soot = WorldProps.Shape(_art, "Soot", Vector2.zero, new Vector2(0.20f, 0.20f), ShapeSprites.Circle, SootColor, order + 3, false);
            _soot.enabled = false;

            _halo = WorldProps.Shape(_art, "Halo", Vector2.zero, new Vector2(1.1f, 1.1f), WorldProps.SoftDot, HaloColor, order + 2, true);
            _halo.enabled = false;
            _flame = new GameObject("Flame").transform;
            _flame.SetParent(_art, false);
            _flame.localPosition = Vector3.zero;
            _flame.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Existing tilt/fade timings rotate a plan-view flame area, not a vertical flame silhouette.
            WorldProps.Shape(_flame, "Outer", Vector2.zero, new Vector2(0.28f, 0.28f), ShapeSprites.Circle, FlameOuter, order + 3, true);
            WorldProps.Shape(_flame, "Inner", new Vector2(0.025f, 0f), new Vector2(0.11f, 0.11f), ShapeSprites.Circle, FlameInner, order + 4, true);
            _flame.gameObject.SetActive(false);
        }

        void Start() => ReachForWall();

        /// <summary>네 방향으로 가장 가까운 벽 면을 찾아 받침 막대를 그 면까지 뻗는다. 못 찾으면 위로 짧게 둔다.</summary>
        void ReachForWall()
        {
            Vector2 from = Position;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Layers.WallMask);
            var hits = new List<RaycastHit2D>(4);
            Vector2 best = Vector2.zero;
            float bestD = WallProbe;
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            foreach (var d in dirs)
            {
                int n = Physics2D.Raycast(from, d, filter, hits, WallProbe);
                for (int i = 0; i < n && i < hits.Count; i++)
                {
                    if (!hits[i].collider || hits[i].distance >= bestD) continue;
                    bestD = hits[i].distance;
                    best = d;
                }
            }
            if (best == Vector2.zero) return;
            float len = bestD + 0.15f;
            _bracket.localPosition = best * (len * 0.5f);
            _bracket.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(best.y, best.x) * Mathf.Rad2Deg);
            _bracket.localScale = new Vector3(len, 0.18f, 1f);
        }

        public override void Interact()
        {
            if (Lit) return;
            Ignite(true);
        }

        /// <summary>켠다(불꽃·빛, 소리). 이미 켜져 있으면 그대로.</summary>
        public void LightUp() => Ignite(true);

        /// <summary>소리 없이 켠다(같은 원정에 이미 잡아 쓰러진 채 시작할 때).</summary>
        public void LightUpQuiet() => Ignite(false);

        void Ignite(bool sound)
        {
            if (Lit && _outTime < 0f) return;
            // 꺼지는 중이었으면 눕던 불꽃을 세우고 빛을 그대로 이어 쓴다.
            _outTime = -1f;
            Lit = true;
            if (_lights == null || _lights.Length == 0 || !_lights[0])
            {
                DungeonLighting.WallLampLight(_anchor);
                _lights = _anchor.GetComponentsInChildren<Light2D>();
                _baseIntensity = new float[_lights.Length];
                for (int i = 0; i < _lights.Length; i++) _baseIntensity[i] = _lights[i].intensity;
            }
            _flame.gameObject.SetActive(true);
            _flame.localScale = Vector3.one;
            _halo.color = HaloColor;
            _halo.enabled = true;
            _soot.enabled = false;
            if (sound) Sfx.Play(SfxKind.Lamp);
        }

        /// <summary>2단계에 꺼짐: 불꽃이 0.25초 동안 옆으로 눕다가 빛과 함께 사라진다. 다시 켜기는 F 0.5초. 꺼져 있으면 그대로.</summary>
        public void Extinguish()
        {
            if (!Lit || _outTime >= 0f) return;
            _relight = true;
            _outTime = 0f;
            Sfx.PlayScaled(SfxKind.Lamp, 0.6f, 0.55f);
        }

        /// <summary>다시 도전: 처음처럼 꺼진 채(F 1.0초). 빛·불꽃·그을음을 모두 지운다.</summary>
        public void ResetUnlit()
        {
            PutOut();
            _relight = false;
            _soot.enabled = false;
        }

        /// <summary>빛을 지우고 불꽃을 감춘다(DungeonLighting 일렁임 목록은 지워진 빛을 건너뛴다).</summary>
        void PutOut()
        {
            _outTime = -1f;
            Lit = false;
            if (_lights != null)
                foreach (var l in _lights)
                    if (l) Destroy(l.gameObject);
            _lights = null;
            _baseIntensity = null;
            if (_flame)
            {
                _flame.gameObject.SetActive(false);
                _flame.localScale = Vector3.one;
            }
            if (_halo) _halo.enabled = false;
        }

        void Update()
        {
            if (!Lit || !_flame) return;
            float t = Time.time + _seed;
            if (_outTime >= 0f)
            {
                // 옆으로 눕기: 위(90°)에서 거의 수평까지 기울며 짧아지고 빛이 잦아든다.
                _outTime += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_outTime / OutSeconds);
                float angle = 90f - _leanSign * 80f * k * (2f - k);
                _flame.localRotation = Quaternion.Euler(0f, 0f, angle);
                _flame.localScale = new Vector3(1f + 0.35f * k, 1f - 0.6f * k, 1f);
                if (_halo) _halo.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloColor.a * (1f - k));
                WriteIntensity(1f - k);
                if (k >= 1f)
                {
                    PutOut();
                    if (_halo) _halo.color = HaloColor;
                    _soot.enabled = true;
                }
                return;
            }
            float wobble = Mathf.Sin(t * 7.3f) * 0.08f + Mathf.Sin(t * 12.1f) * 0.05f;
            Vector2 up = (Vector2.up + new Vector2(wobble, 0f)).normalized;
            _flame.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg);
            float len = 1f + Mathf.Sin(t * 9.7f) * 0.08f + Mathf.Sin(t * 15.3f) * 0.04f;
            _flame.localScale = new Vector3(len, 1f, 1f);
            WriteIntensity(1f + Mathf.Sin(t * 8.9f) * 0.02f + Mathf.Sin(t * 13.7f) * 0.01f);
        }

        void WriteIntensity(float scale)
        {
            if (_lights == null || _baseIntensity == null) return;
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i]) _lights[i].intensity = _baseIntensity[i] * scale;
        }
    }
}
