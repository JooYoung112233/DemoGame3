using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥에 떨어진 장비 하나(7부위 공통, 2차 7-6 드랍 연출, 3차 초안 7-2 '빛기둥·등급색'). 포물선으로 튀어나와 내려앉는 순간 등급 연출을 시작한다.
    /// 몸통은 부위별 바닥 도형(LootVisuals.BuildGear), 빛기둥 규칙은 부위와 무관하게 등급만 본다.
    /// 빛기둥은 고급1.1·희귀1.6·영웅2.2·전설2.8유닛으로 제한한다. 전투 장면과 예고를 가리지 않는 작은 광륜을 쓴다.
    /// 등급색·착지 소리·고등급 입자와 전설의 짧은 착지 연출은 유지한다.
    /// 싸움 중(깨어 있는 적이 가까이 있음)에는 착지가 끝난 빛기둥을 35%로 낮춘다. F = 가방에 넣기, G = 바로 끼기(Inventory가 처리).
    /// </summary>
    public sealed class LootDrop : Interactable
    {
        const float UncommonRingDelay = 1.5f;
        const float RiseTime = 0.2f;
        const float LandingFullBright = 0.6f;
        const float CombatDim = 0.35f;
        const float ShockDuration = 0.5f;
        const float ShockMaxDiameter = 1.8f;
        const float LegendarySlowSeconds = 0.25f;
        const float LegendarySlowScale = 0.3f;
        const int MoteCount = 3;

        /// <summary>바닥 장비(7부위 공통). Inventory 카드·G·F, 이름표가 이것을 읽는다.</summary>
        public GearItem Gear { get; private set; }
        public bool Landed { get; private set; }

        public override string Prompt => "가방에 넣기";
        public override bool Available => Landed && !_taken && Gear != null;

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

        /// <summary>옛 무기로 부르는 곳(AgentScripts 검사 스크립트 등)을 위한 다리. 새 코드는 GearItem판을 쓴다.</summary>
        public static LootDrop Spawn(WeaponItem item, Vector2 from, Vector2 to, float delay) =>
            Spawn(GearItem.FromWeapon(item), from, to, delay);

        /// <summary>from에서 to로 delay초 뒤 튀어나오는 장비를 만든다.</summary>
        public static LootDrop Spawn(GearItem item, Vector2 from, Vector2 to, float delay)
        {
            var go = new GameObject("LootDrop " + (item != null ? item.DisplayName : "?"));
            go.transform.position = from;
            var drop = go.AddComponent<LootDrop>();
            drop.Setup(item, from, to, delay);
            return drop;
        }

        void Setup(GearItem item, Vector2 from, Vector2 to, float delay)
        {
            Gear = item ?? GearItem.Starting(GearSlot.Weapon);
            _color = LootVisuals.AtmosphereColor(Gear.Grade);
            _arc = new LootArc(from, to, delay);
            // 부위별 바닥 모양(무기 3종 + 갑옷·투구·장갑·장화·반지·목걸이 6종).
            _body = LootVisuals.BuildGear(transform, Gear);
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
            if (Gear != null && !_taken && Gear.Grade < Grade.Rare)
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
            DungeonEvents.RaiseGearDropped(Gear.DisplayName);
            switch (Gear.Grade)
            {
                case Grade.Common:
                    _glint = LootVisuals.BuildSparkle(transform, "Glint", new Color(0.85f, 0.85f, 0.85f, 0.9f), LootVisuals.GlintOrder, new Vector2(0.12f, 0.12f), 0.32f);
                    _glintSprites = _glint.GetComponentsInChildren<SpriteRenderer>(true);
                    break;
                case Grade.Uncommon:
                    MakeBeam(0.16f, 1.1f, false, 0.42f);
                    break;
                case Grade.Rare:
                    MakeBeam(0.20f, 1.6f, false, 0.48f);
                    break;
                case Grade.Epic:
                    MakeBeam(0.24f, 2.2f, false, 0.56f);
                    MakeMotes();
                    break;
                default:
                    MakeBeam(0.29f, 2.8f, false, 0.65f);
                    MakeMotes();
                    _shock = LootVisuals.Unlit(transform, "Shockwave", LootVisuals.DustRing, _color, LootVisuals.RingOrder, Vector2.zero, Vector2.one * 0.4f);
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
            // v059: in the zenith camera the reward light is viewed down its axis.
            // Keep its grade colour, duration, combat dimming, and existing sorting orders.
            _beam = LootVisuals.Unlit(transform, "Reward light seen from above", ShapeSprites.Glow, c, LootVisuals.BeamOrder, Vector2.zero, Vector2.one);
            // 바닥 자리 표시(작은 원). 고급은 1.5초 뒤 이것이 고리로 남는다.
            var foot = _color;
            foot.a = 0.5f;
            _ring = LootVisuals.Unlit(transform, "Foot", LootVisuals.DustRing, foot, LootVisuals.RingOrder, Vector2.zero, Vector2.one * (width * 2.2f));
            ApplyBeamScale();
        }

        void MakeMotes()
        {
            _motes = new SpriteRenderer[MoteCount];
            _motePhase = new float[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                var c = Color.Lerp(_color, new Color(.86f,.8f,.66f), 0.18f);
                var art = ArtRuntime.Active;
                var chip = art && art.effects.sparks != null && art.effects.sparks.Length > 0 ? art.effects.sparks[0] : null;
                var sprite = chip ? chip : ShapeSprites.Square;
                float scale = .075f / Mathf.Max(.001f, sprite.bounds.size.x);
                _motes[i] = LootVisuals.Unlit(transform, "Mote", sprite, c, LootVisuals.BeamOrder + 1, Vector2.zero, Vector2.one * scale, 90f);
                _motePhase[i] = i / (float)MoteCount;
            }
        }

        /// <summary>정수리에서 본 보상 발광: 기존 등급별 세기·착지 0.2초 출현을 유지한다.</summary>
        void ApplyBeamScale()
        {
            if (!_beam) return;
            float rise = Mathf.Clamp01(_landedAge / RiseTime);
            var size = _beam.sprite ? (Vector2)_beam.sprite.bounds.size : Vector2.one;
            // Width/height remain the existing grade envelope, now controlling a circular footprint.
            // No world-Y displacement is used to impersonate physical height.
            float diameter = Mathf.Max(0.01f, (_beamWidth * 2.2f + _beamHeight * 0.30f) * rise);
            _beam.transform.localScale = new Vector3(diameter / Mathf.Max(0.001f, size.x), diameter / Mathf.Max(0.001f, size.y), 1f);
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
                if (Gear.Grade == Grade.Uncommon && _landedAge >= UncommonRingDelay)
                {
                    // 고급: 1.5초 뒤 기둥이 줄어들어 바닥 고리로 남는다.
                    float shrink = Mathf.Clamp01((_landedAge - UncommonRingDelay) / 0.4f);
                    _beamHeight = Mathf.Lerp(1.1f, 0f, shrink);
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
                bool ringOnly = !_beam && Gear.Grade == Grade.Uncommon;
                float baseSize = _beamWidth * 2.2f;
                float size = ringOnly ? 0.9f + 0.08f * Mathf.Sin(Time.time * 3f) : baseSize;
                _ring.transform.localScale = new Vector3(size, size, 1f);
                var c = _ring.color;
                c.a = (ringOnly ? 0.65f : 0.38f) * _dim;
                _ring.color = c;
            }

            if (_motes != null && _beam)
            {
                float radius = (_beamWidth * 2.2f + Mathf.Min(_beamHeight, 6f) * 0.30f) * 0.5f;
                for (int i = 0; i < _motes.Length; i++)
                {
                    if (!_motes[i]) continue;
                    _motePhase[i] = Mathf.Repeat(_motePhase[i] + dt * 0.45f, 1f);
                    float p = _motePhase[i];
                    float angle = i * Mathf.PI * 2f / _motes.Length + Mathf.Sin(p * Mathf.PI * 2f) * 0.12f;
                    float spread = Mathf.Lerp(_beamWidth * 0.3f, radius * 0.85f, p);
                    _motes[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * spread, Mathf.Sin(angle) * spread, 0f);
                    var c = _motes[i].color;
                    c.a = Mathf.Sin(p * Mathf.PI) * 0.55f * _dim;
                    _motes[i].color = c;
                }
            }

            if (_shock)
            {
                float k = Mathf.Clamp01(_landedAge / ShockDuration);
                float d = Mathf.Lerp(0.4f, ShockMaxDiameter, 1f - (1f - k) * (1f - k));
                _shock.transform.localScale = new Vector3(d, d, 1f);
                var c = _shock.color;
                c.a = 0.5f * (1f - k) * (1f - k);
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
