using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥에 떨어진 무기 하나(2차 7-6 드랍 연출, 3차 초안 7-2 '빛기둥·등급색'). 포물선으로 튀어나와 내려앉는 순간 등급 연출을 시작한다.
    /// 일반: 빛기둥 없이 작은 회색 반짝임. 고급: 초록 높이 2·폭 0.25, 1.5초 뒤 바닥 고리로 줄어듦. 희귀: 파랑 높이 4·폭 0.4.
    /// 영웅: 보라 화면 위 끝까지·폭 0.6·위로 흐르는 입자. 전설: 주황 화면 위 끝까지·폭 1.0 + 착지 충격파 고리 + 0.25초 시간 0.3배 + 약한 흔들림.
    /// 싸움 중(깨어 있는 적이 가까이 있음)에는 착지가 끝난 빛기둥을 35%로 낮춘다. F = 가방에 넣기, G = 바로 끼기(Inventory가 처리).
    /// </summary>
    public sealed class LootDrop : Interactable
    {
        const float UncommonRingDelay = 1.5f;
        const float RiseTime = 0.2f;
        const float LandingFullBright = 0.6f;
        const float CombatDim = 0.35f;
        const float ShockDuration = 0.5f;
        const float ShockMaxDiameter = 5f;
        const float LegendarySlowSeconds = 0.25f;
        const float LegendarySlowScale = 0.3f;
        const int MoteCount = 5;

        public WeaponItem Item { get; private set; }
        public bool Landed { get; private set; }

        public override string Prompt => "가방에 넣기";
        public override bool Available => Landed && !_taken && Item != null;

        LootArc _arc;
        Transform _body;
        SpriteRenderer[] _bodySprites;
        float _bodyAngle;
        float _spin;
        Color _color;

        SpriteRenderer _beam;
        float _beamWidth;
        float _beamHeight;
        bool _beamToTop;
        float _beamAlpha;
        SpriteRenderer _ring;
        Transform _glint;
        SpriteRenderer[] _glintSprites;
        SpriteRenderer _shock;
        SpriteRenderer[] _motes;
        float[] _motePhase;

        float _landedAge;
        float _dim = 1f;
        bool _taken;

        /// <summary>from에서 to로 delay초 뒤 튀어나오는 무기를 만든다.</summary>
        public static LootDrop Spawn(WeaponItem item, Vector2 from, Vector2 to, float delay)
        {
            var go = new GameObject("LootDrop " + (item != null ? item.DisplayName : "?"));
            go.transform.position = from;
            var drop = go.AddComponent<LootDrop>();
            drop.Setup(item, from, to, delay);
            return drop;
        }

        void Setup(WeaponItem item, Vector2 from, Vector2 to, float delay)
        {
            Item = item ?? WeaponItem.Starting();
            _color = LootVisuals.GradeColor(Item.Grade);
            _arc = new LootArc(from, to, delay);
            _body = LootVisuals.BuildWeapon(transform, Item);
            _bodySprites = _body.GetComponentsInChildren<SpriteRenderer>(true);
            _bodyAngle = UnityEngine.Random.Range(-20f, 20f);
            _spin = UnityEngine.Random.value < 0.5f ? -540f : 540f;
            SetBodyVisible(false);
        }

        /// <summary>가방에 넣었거나 끼었다. 바로 지운다.</summary>
        public void Take()
        {
            if (_taken) return;
            _taken = true;
            Destroy(gameObject);
        }

        public override void Interact()
        {
            if (!Available) return;
            var inv = Inventory.Instance;
            if (inv) inv.PickUp(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!Landed)
            {
                if (_arc == null) return;
                bool landed = _arc.Step(dt, out var ground, out var height);
                transform.position = ground;
                SetBodyVisible(!_arc.Waiting);
                _body.localPosition = new Vector3(0f, height, 0f);
                _body.localRotation = Quaternion.Euler(0f, 0f, _bodyAngle + _spin * (1f - _arc.Progress) * LootArc.Duration);
                if (landed) Land();
                return;
            }
            if (dt <= 0f) return;
            _landedAge += dt;
            AnimateGrade(dt);
            // 3차 초안 2-6: 반경 15 밖으로 멀어지면 일반·고급은 가방으로 저절로 거둔다(희귀 이상은 직접 줍는다).
            if (Item != null && !_taken && Item.Grade < Grade.Rare)
            {
                var p = PlayerController.Instance;
                var inv = Inventory.Instance;
                if (p && !p.IsDown && inv && !inv.BagFull && (p.Position - Position).sqrMagnitude > 15f * 15f) inv.PickUp(this);
            }
        }

        void LateUpdate()
        {
            // 화면 위 끝까지 서는 기둥(영웅·전설)은 카메라가 움직인 뒤 길이를 맞춘다.
            if (!_beam || !_beamToTop) return;
            _beamHeight = TopOfScreenHeight();
            ApplyBeamScale();
        }

        void SetBodyVisible(bool visible)
        {
            if (_bodySprites == null) return;
            foreach (var sr in _bodySprites)
                if (sr) sr.enabled = visible;
        }

        /// <summary>내려앉음: 소리, 알림, 등급 연출 시작(2차 7-6 '땅에 닿는 순간 등급 연출').</summary>
        void Land()
        {
            Landed = true;
            _landedAge = 0f;
            _body.localPosition = Vector3.zero;
            _body.localRotation = Quaternion.Euler(0f, 0f, _bodyAngle);
            Sfx.Play(SfxKind.Loot);
            DungeonEvents.RaiseGearDropped(Item.DisplayName);
            switch (Item.Grade)
            {
                case Grade.Common:
                    _glint = LootVisuals.BuildSparkle(transform, "Glint", new Color(0.85f, 0.85f, 0.85f, 0.9f), LootVisuals.GlintOrder, new Vector2(0.12f, 0.12f), 0.32f);
                    _glintSprites = _glint.GetComponentsInChildren<SpriteRenderer>(true);
                    break;
                case Grade.Uncommon:
                    MakeBeam(0.25f, 2f, false, 0.55f);
                    break;
                case Grade.Rare:
                    MakeBeam(0.4f, 4f, false, 0.65f);
                    break;
                case Grade.Epic:
                    MakeBeam(0.6f, TopOfScreenHeight(), true, 0.7f);
                    MakeMotes();
                    break;
                default:
                    MakeBeam(1.0f, TopOfScreenHeight(), true, 0.8f);
                    MakeMotes();
                    _shock = LootVisuals.Unlit(transform, "Shockwave", ShapeSprites.Ring, _color, LootVisuals.RingOrder, Vector2.zero, Vector2.one * 0.4f);
                    TimeScaleService.SlowMotion(LegendarySlowSeconds, LegendarySlowScale);
                    ScreenShake.Add(0.06f, 0.2f);
                    break;
            }
        }

        void MakeBeam(float width, float height, bool toTop, float alpha)
        {
            _beamWidth = width;
            _beamHeight = height;
            _beamToTop = toTop;
            _beamAlpha = alpha;
            var c = _color;
            c.a = alpha;
            _beam = LootVisuals.Unlit(transform, "Beam", LootVisuals.Beam, c, LootVisuals.BeamOrder, Vector2.zero, Vector2.one);
            // 바닥 자리 표시(작은 원). 고급은 1.5초 뒤 이것이 고리로 남는다.
            var foot = _color;
            foot.a = 0.5f;
            _ring = LootVisuals.Unlit(transform, "Foot", ShapeSprites.Ring, foot, LootVisuals.RingOrder, Vector2.zero, new Vector2(width * 2.2f, width * 1.1f));
            ApplyBeamScale();
        }

        void MakeMotes()
        {
            _motes = new SpriteRenderer[MoteCount];
            _motePhase = new float[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                var c = Color.Lerp(_color, Color.white, 0.4f);
                _motes[i] = LootVisuals.Unlit(transform, "Mote", ShapeSprites.Square, c, LootVisuals.BeamOrder + 1, Vector2.zero, Vector2.one * 0.07f, 45f);
                _motePhase[i] = i / (float)MoteCount;
            }
        }

        /// <summary>기둥 크기: 착지 뒤 0.2초 동안 솟는다.</summary>
        void ApplyBeamScale()
        {
            if (!_beam) return;
            float rise = Mathf.Clamp01(_landedAge / RiseTime);
            var size = _beam.sprite ? (Vector2)_beam.sprite.bounds.size : Vector2.one;
            float sx = _beamWidth / Mathf.Max(0.001f, size.x);
            float sy = Mathf.Max(0.01f, _beamHeight * rise) / Mathf.Max(0.001f, size.y);
            _beam.transform.localScale = new Vector3(sx, sy, 1f);
        }

        void AnimateGrade(float dt)
        {
            bool combat = DungeonLighting.Instance && DungeonLighting.Instance.InCombat && _landedAge > LandingFullBright;
            _dim = Mathf.MoveTowards(_dim, combat ? CombatDim : 1f, dt * 2.5f);
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.time * 4f);

            if (_glint)
            {
                // 일반: 1.6초마다 잠깐 반짝인다.
                float cycle = Mathf.Repeat(_landedAge, 1.6f);
                float flash = cycle < 0.35f ? Mathf.Sin(cycle / 0.35f * Mathf.PI) : 0f;
                _glint.localScale = Vector3.one * (0.5f + 0.7f * flash);
                LootVisuals.SetAlpha(_glintSprites, (0.25f + 0.7f * flash) * _dim);
            }

            if (_beam)
            {
                if (Item.Grade == Grade.Uncommon && _landedAge >= UncommonRingDelay)
                {
                    // 고급: 1.5초 뒤 기둥이 줄어들어 바닥 고리로 남는다.
                    float shrink = Mathf.Clamp01((_landedAge - UncommonRingDelay) / 0.4f);
                    _beamHeight = Mathf.Lerp(2f, 0f, shrink);
                    if (shrink >= 1f)
                    {
                        Destroy(_beam.gameObject);
                        _beam = null;
                    }
                }
                if (_beam)
                {
                    ApplyBeamScale();
                    var c = _beam.color;
                    c.a = _beamAlpha * pulse * _dim;
                    _beam.color = c;
                }
            }

            if (_ring)
            {
                bool ringOnly = !_beam && Item.Grade == Grade.Uncommon;
                float baseSize = _beamWidth * 2.2f;
                float size = ringOnly ? 0.9f + 0.08f * Mathf.Sin(Time.time * 3f) : baseSize;
                _ring.transform.localScale = new Vector3(size, size * 0.5f, 1f);
                var c = _ring.color;
                c.a = (ringOnly ? 0.75f : 0.5f) * _dim;
                _ring.color = c;
            }

            if (_motes != null && _beam)
            {
                float height = Mathf.Min(_beamHeight, 6f);
                for (int i = 0; i < _motes.Length; i++)
                {
                    if (!_motes[i]) continue;
                    _motePhase[i] = Mathf.Repeat(_motePhase[i] + dt * 0.45f, 1f);
                    float p = _motePhase[i];
                    float x = Mathf.Sin((p * 3f + i) * 2.1f) * _beamWidth * 0.3f;
                    _motes[i].transform.localPosition = new Vector3(x, p * height, 0f);
                    var c = _motes[i].color;
                    c.a = Mathf.Sin(p * Mathf.PI) * 0.9f * _dim;
                    _motes[i].color = c;
                }
            }

            if (_shock)
            {
                float k = Mathf.Clamp01(_landedAge / ShockDuration);
                float d = Mathf.Lerp(0.4f, ShockMaxDiameter, 1f - (1f - k) * (1f - k));
                _shock.transform.localScale = new Vector3(d, d * 0.55f, 1f);
                var c = _shock.color;
                c.a = 1f - k;
                _shock.color = c;
                if (k >= 1f)
                {
                    Destroy(_shock.gameObject);
                    _shock = null;
                }
            }
        }

        /// <summary>바닥에서 화면 위 끝까지의 길이(직교 카메라). 카메라가 없거나 바닥이 화면 위쪽 밖이면 4.</summary>
        float TopOfScreenHeight()
        {
            var cam = Camera.main;
            if (!cam) return 12f;
            float top = cam.orthographic
                ? cam.transform.position.y + cam.orthographicSize
                : cam.transform.position.y + 10f;
            return Mathf.Max(4f, top - transform.position.y + 1.5f);
        }
    }
}
