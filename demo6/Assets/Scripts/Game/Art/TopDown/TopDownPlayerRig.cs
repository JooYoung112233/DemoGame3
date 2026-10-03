using Demo6.Core.Combat;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점 검사(TopDownView가 몬다). 몸 SpriteRenderer에 '위에서 본 어깨·망토(맨머리)' 한 장을 넣어 SpriteFlash 번쩍임이 그대로 먹고,
    /// 몸 전체를 바라보는 쪽으로 돌린다(걷기·대기는 묵직하게 천천히, 조준해서 휘두를 때는 빠르게). 발 둘은 걸을 때 앞뒤로 엇갈리고 어깨가 좌우로 6° 흔들린다.
    /// 방향 삼각형은 숨긴다. 무기만 콤보 단계(Weapon.combo[ComboIndex])의 모양·시간대로 코드로 휘두른다(TopDownSwing). 판정·수치는 읽기만 한다.
    /// 팔은 어깨에서 주먹까지 늘려 놓는 막대 하나씩이라(몸 밑에 깔림) 따로 동작 그림이 필요 없다.
    /// 겉모습(장비 문서 9-1): 플레이어 겉모습 id 4개(PlayerController.Look)로 갑옷 몸·소매, 투구 덧그림, 장갑 주먹(쥔·빈), 장화를 고른다.
    /// LookChanged가 왔을 때(와 그림 묶음이 바뀌었을 때)만 다시 고르고, 매 프레임은 고른 그림을 놓기만 한다(임시 그림은 TopDownSprites가 무게마다 한 번 만듦).
    /// 투구는 몸 Transform의 자식(몸 회전·비틀기·숨쉬기·구르기·쓰러짐을 그대로 따름)이고 SpriteFlash 대상이라 몸과 같이 번쩍이고 깜빡이며 어둡게 된다.
    /// 무기 그림에는 주먹이 없고, 쥔 주먹은 손잡이 위에 따로 놓는다(9-2 ②).
    /// 그리는 순서(위 → 아래): 검풍 빛 +4, 주먹 +3, 무기 +2, 투구 +1, 몸 0, 발·소매 −1.
    /// 그림 칸(CombatArtSet.topDown.player·weapons)에 그림이 있으면 그 그림을, 없으면 코드로 그린 임시 그림을 부품마다 따로 쓴다.
    /// 끄면(Restore) 몸 스프라이트·색·재질·크기·회전·방향 삼각형을 도형 상태로 되돌리고, 그 뒤 PlayerVisual이 그림 모드를 다시 맡는다.
    /// </summary>
    public sealed class TopDownPlayerRig
    {
        const float Diameter = PlayerController.Radius * 2f;
        /// <summary>임시 몸 그림 배율: 어깨 폭이 판정 지름(0.8)과 같아지게 조금 키운다(머리·어깨받이는 몸 지름 1.0~1.1배). 그림 칸은 유닛 그림이라 쓰지 않는다.</summary>
        const float BodyArt = 1.08f;
        /// <summary>
        /// 걷기·대기 때 몸 회전 보간 빠르기(초당). 2026-10-03 '둔직한 맛이 조금 부족해' 피드백으로 35 → 16:
        /// 커서를 휙 돌려도 몸이 약 0.06초 늦게 따라와 무게가 실린다(90° 돌기 약 0.2초).
        /// </summary>
        const float TurnRate = 16f;
        /// <summary>
        /// 검풍 시전의 회전 빠르기(예전 값 그대로). 기본공격은 SwingTiming.AimTurnRate(공격 속도) = 35 × (1 + 공격 속도 ÷ 2)로 돌고(장비 문서 3-3 ⑥),
        /// 공격 속도 0이면 이 값과 비트까지 같다. 판정 순간 남은 오차가 1° 미만이 되게 한다(칼날이 조준 한가운데를 지남).
        /// </summary>
        const float AimTurnRate = SwingTiming.BaseAimTurnRate;
        const float FollowRate = 16f;
        /// <summary>
        /// 발걸음 한 주기에 나아가는 거리(유닛). 걸음은 이동 거리로 세므로 이동 속도가 바뀌어도 보폭은 그대로다.
        /// 2026-10-03 '둔직한 맛' 피드백으로 0.95 → 1.1: 같은 속도에서 걸음이 약 14% 느려지고 보폭이 커 보인다.
        /// </summary>
        const float StrideLength = 1.1f;
        /// <summary>발이 앞뒤로 나가는 폭(유닛). 보폭에 맞춰 0.14 → 0.16.</summary>
        const float FootSwing = 0.16f;
        /// <summary>그림자·피·시체 정렬(GoreSystem 시체 −990 위, 벽 −900·빨간 예고 −60 아래).</summary>
        public const int ShadowOrder = -980;
        /// <summary>어깨 자리(월드, 몸 틀). 어깨받이 밑에서 팔이 나온다.</summary>
        static readonly Vector2 Shoulder = new Vector2(0f, 0.27f);
        const float ArmThickness = 0.085f;
        static readonly Color GlowColor = new Color(0.95f, 0.93f, 0.85f);

        // 그리는 순서(몸 순서 기준 더하기, 9-1).
        const int GlowOrder = 4;
        const int FistOrder = 3;
        const int WeaponOrder = 2;
        const int HelmOrder = 1;
        const int UnderOrder = -1;

        readonly PlayerController _player;
        readonly SpriteRenderer _body;
        readonly SpriteFlash _flash;
        readonly Transform _mark;

        Sprite _shapeSprite;
        Material _shapeMaterial;
        Color _savedBaseColor;
        bool _baseColorChanged;
        bool _markWasActive;

        Transform _rig;
        SpriteRenderer _shadow;
        SpriteRenderer _footL;
        SpriteRenderer _footR;
        SpriteRenderer _bladeR;
        SpriteRenderer _bladeL;
        SpriteRenderer _glow;
        SpriteRenderer _armR;
        SpriteRenderer _armL;
        SpriteRenderer _fistR;
        SpriteRenderer _fistL;
        SpriteRenderer _helm;
        TopDownWeaponLook _look;

        // 고른 겉모습(LookChanged·그림 묶음이 바뀔 때만 SelectLook이 다시 채운다).
        bool _lookDirty = true;
        bool _subscribed;
        TopDownPlayerArt _lookArt;
        Sprite _bodyStand;
        Sprite _bodyHurt;
        Sprite _bodyDown;
        float _bodyUnit = Diameter * BodyArt;
        Vector2 _downHead;
        Sprite _helmSprite;
        float _helmUnit = Diameter * BodyArt;
        Sprite _sleeveArt;
        Color _sleeveTint = Color.white;
        Sprite _fistClosed;
        Sprite _fistOpen;
        Sprite _bootSprite;

        float _angle;
        float _twist;
        Vector2 _offset;
        Vector2 _scale = Vector2.one;
        float _light = 1f;
        TopDownHand _right;
        TopDownHand _left;
        float _phase;
        float _stepAmp;
        Vector2 _lastPos;
        float _glowNow;
        float _glowFadeFrom;
        float _glowFadeStart = -999f;
        bool _wasWave;
        // 행동(휘두르기·회오리·검풍·구르기)이 시작될 때 직전에 보이던 자세에서 짧게 이어 붙인다(구르기 끊기·회오리 뒤 베기 등에서 무기가 한 프레임에 튀지 않게).
        PlayerPose _lastPose;
        float _lastPoseTime;
        float _blendT0;
        float _blendDur;
        float _fromTwist;
        TopDownHand _fromRight;
        TopDownHand _fromLeft;
        Vector2 _fromOffset;
        Vector2 _fromScale = Vector2.one;

        public bool Applied { get; private set; }
        public PlayerController Player => _player;

        /// <summary>시험·확인용: 겉모습을 다시 고른 횟수(LookChanged·그림 묶음 바뀜·켜기 때만 는다).</summary>
        public int LookSelections { get; private set; }
        /// <summary>시험·확인용: 마지막으로 고른 겉모습 id 4개.</summary>
        public GearLook SelectedLook { get; private set; } = GearLook.Starting;
        /// <summary>시험·확인용: 지금 고른 몸(서 있는)·투구·쥔 주먹·장화 그림.</summary>
        public Sprite BodySprite => _bodyStand;
        public Sprite HelmSprite => _helmSprite;
        public Sprite FistSprite => _fistClosed;
        public Sprite BootSprite => _bootSprite;
        /// <summary>시험·확인용: 투구 렌더러(몸 Transform의 자식). 켜기 전이면 null.</summary>
        public SpriteRenderer HelmRenderer => _helm;

        public TopDownPlayerRig(PlayerController player)
        {
            _player = player;
            _flash = player.GetComponent<SpriteFlash>();
            _body = _flash ? _flash.target : null;
            var mark = player.transform.Find("Facing");
            _mark = mark;
        }

        public bool Valid => _player && _body;

        /// <summary>도형 상태(PlayerVisual이 이번 프레임에 먼저 도형으로 되돌려 둠)를 기억하고 정수리 그림으로 바꾼다.</summary>
        public void Apply()
        {
            if (Applied || !Valid) return;
            Applied = true;
            _shapeSprite = _body.sprite;
            _shapeMaterial = null;
            ArtRuntime.UseArtMaterial(_body, _flash, ref _shapeMaterial);
            _baseColorChanged = false;
            if (_flash && !_flash.useShader)
            {
                // 번쩍임 재질이 없을 때만: 도형 색이 그림을 물들이지 않게 흰색으로 둔다(끄면 되돌림).
                _savedBaseColor = _flash.baseColor;
                _flash.baseColor = Color.white;
                _baseColorChanged = true;
            }
            if (_mark)
            {
                _markWasActive = _mark.gameObject.activeSelf;
                _mark.gameObject.SetActive(false);
            }
            EnsureParts();
            _rig.gameObject.SetActive(true);
            _helm.gameObject.SetActive(true);
            _helm.sharedMaterial = _body.sharedMaterial;
            if (_flash) _flash.AddTarget(_helm);
            // 꺼져 있던 동안 온 바뀜은 듣지 못했으므로 켤 때 한 번 다시 고른다.
            if (!_subscribed)
            {
                _player.LookChanged += OnLookChanged;
                _subscribed = true;
            }
            var playerArt = TopDownView.CurrentArt()?.player;
            SelectLook(playerArt);
            _body.sprite = _bodyStand;
            _body.flipX = false;
            Vector2 f = _player.FacingDirection;
            _angle = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
            _twist = 0f;
            _offset = Vector2.zero;
            _scale = Vector2.one;
            _lastPos = _player.transform.position;
            SetWeapon(_player.Weapon);
            _right = _look.RestRight;
            _left = _look.RestLeft;
            _lastPose = _player.Pose;
            _lastPoseTime = _player.PoseTime;
            _blendDur = 0f;
        }

        /// <summary>도형 상태로 되돌린다(PlayerVisual이 다음 LateUpdate부터 도형·그림 모드를 다시 맡는다).</summary>
        public void Restore()
        {
            if (!Applied) return;
            Applied = false;
            if (_subscribed)
            {
                // 플레이어가 지워졌어도 C# 물체는 남아 있으므로 구독은 늘 푼다.
                if (!ReferenceEquals(_player, null)) _player.LookChanged -= OnLookChanged;
                _subscribed = false;
            }
            if (_rig) _rig.gameObject.SetActive(false);
            if (_helm)
            {
                if (_flash) _flash.RemoveTarget(_helm);
                _helm.gameObject.SetActive(false);
            }
            if (!Valid) return;
            _body.sprite = _shapeSprite;
            _body.flipX = false;
            ArtRuntime.UseShapeMaterial(_body, _flash, _shapeMaterial);
            if (_flash)
            {
                if (_baseColorChanged) _flash.baseColor = _savedBaseColor;
                _body.color = _flash.baseColor;
            }
            var t = _body.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            // PlayerController.OnDied·StandUp과 같은 크기(쓰러져 있으면 납작).
            t.localScale = _player.IsDown
                ? new Vector3(PlayerController.Radius * 2f, PlayerController.Radius * 0.9f, 1f)
                : Vector3.one * (PlayerController.Radius * 2f);
            if (_mark && _markWasActive) _mark.gameObject.SetActive(true);
        }

        void OnLookChanged() => _lookDirty = true;

        void EnsureParts()
        {
            if (_rig) return;
            var go = new GameObject("TopDown");
            go.layer = _player.gameObject.layer;
            _rig = go.transform;
            _rig.SetParent(_player.transform, false);
            // 부품은 번쩍이지 않으므로 빛 무시 기본 재질(SpriteRenderer.color·투명도가 먹음)을 쓴다.
            // 번쩍임 재질은 PropertyBlock 없는 렌더러의 색을 무시해 부품이 흰색으로 그려졌다(투구는 SpriteFlash 대상이라 PropertyBlock이 있어 몸 재질을 쓴다).
            var art = ArtRuntime.FlashMaterial;
            var unlitPart = RenderMaterials.Unlit;
            Material partMat = unlitPart ? unlitPart : (art ? art : _shapeMaterial);
            _shadow = TopDownView.Part("Shadow", _rig, TopDownSprites.Shadow, _shapeMaterial, new Color(0f, 0f, 0f, 0.4f), _body);
            _shadow.transform.localScale = new Vector3(0.98f, 0.92f, 1f);
            // 장화 그림은 오른발로 그린다: 왼발(+y 쪽)은 위아래로 뒤집는다. 임시 장화·그림 칸 모두 유닛 그림이라 크기 1.
            _footL = TopDownView.Part("FootL", _rig, null, partMat, Color.white, _body);
            _footR = TopDownView.Part("FootR", _rig, null, partMat, Color.white, _body);
            _footL.flipY = true;
            _bladeR = TopDownView.Part("WeaponR", _rig, null, partMat, Color.white, _body);
            _bladeL = TopDownView.Part("WeaponL", _rig, null, partMat, Color.white, _body);
            _bladeL.flipY = true;
            var unlit = RenderMaterials.Unlit;
            _glow = TopDownView.Part("WaveGlow", _bladeR.transform, ShapeSprites.Square, unlit ? unlit : partMat, new Color(1f, 1f, 1f, 0f), _body);
            _armR = TopDownView.Part("ArmR", _rig, TopDownSprites.Limb, partMat, Color.white, _body);
            _armL = TopDownView.Part("ArmL", _rig, TopDownSprites.Limb, partMat, Color.white, _body);
            // 주먹(장갑 부품): 오른손은 늘 쥔 주먹, 왼손은 쌍검·대검이면 쥔 주먹, 장검이면 빈 주먹.
            _fistR = TopDownView.Part("FistR", _rig, null, partMat, Color.white, _body);
            _fistL = TopDownView.Part("FistL", _rig, null, partMat, Color.white, _body);
            // 투구: 몸의 자식(피벗 = 몸 피벗). 몸 재질(번쩍임 셰이더)을 같이 쓰고 SpriteFlash 대상으로 더한다(Apply).
            _helm = TopDownView.Part("Helm", _body.transform, null, _body.sharedMaterial, Color.white, _body);
        }

        /// <summary>
        /// 겉모습 고르기(장비 문서 9-1). 그림 칸 묶음(id → 그림) → 예전 기본 칸 → 무게 색 임시 그림 순서로 고른다.
        /// LookChanged·그림 묶음 바뀜·켜기 때만 부른다(매 프레임 부르지 않음). 임시 그림은 무게마다 한 번만 만든다.
        /// </summary>
        void SelectLook(TopDownPlayerArt art)
        {
            _lookDirty = false;
            _lookArt = art;
            var look = _player.Look;
            SelectedLook = look;
            LookSelections++;

            var armorWeight = GearLook.WeightOf(look.ArmorId);
            if (art != null && art.TryBody(look.ArmorId, out var stand, out var hurt, out var down, out var head))
            {
                _bodyStand = stand;
                _bodyHurt = hurt;
                _bodyDown = down;
                _downHead = head;
                _bodyUnit = Mathf.Max(0.01f, art.scale);
            }
            else
            {
                // 임시 몸은 맞은·쓰러진 그림이 따로 없다(코드가 젖히고 돌리고 어둡게 함). 머리는 서 있을 때와 같은 자리.
                _bodyStand = _bodyHurt = _bodyDown = TopDownSprites.PlayerBodyOf(armorWeight);
                _downHead = Vector2.zero;
                _bodyUnit = Diameter * BodyArt;
            }

            _sleeveArt = art != null ? art.SleeveFor(look.ArmorId) : null;
            _sleeveTint = _sleeveArt ? Color.white : TopDownSprites.SleeveTint(armorWeight);

            // 투구 그림 칸은 유닛 그림(몸 그림 배율을 같이 받음), 임시 투구는 임시 몸과 같은 몸 단위.
            Sprite helmArt = art != null ? art.HelmFor(look.HelmId) : null;
            _helmSprite = helmArt ? helmArt : TopDownSprites.HelmOf(GearLook.WeightOf(look.HelmId));
            _helmUnit = helmArt ? Mathf.Max(0.01f, art.scale) : Diameter * BodyArt;

            var glovesWeight = GearLook.WeightOf(look.GlovesId);
            Sprite closed = art != null ? art.FistFor(look.GlovesId, true) : null;
            Sprite open = art != null ? art.FistFor(look.GlovesId, false) : null;
            _fistClosed = closed ? closed : TopDownSprites.FistOf(glovesWeight, true);
            _fistOpen = open ? open : TopDownSprites.FistOf(glovesWeight, false);

            Sprite boot = art != null ? art.BootFor(look.BootsId) : null;
            _bootSprite = boot ? boot : TopDownSprites.BootOf(GearLook.WeightOf(look.BootsId));

            if (_helm && _helm.sprite != _helmSprite) _helm.sprite = _helmSprite;
        }

        void SetWeapon(WeaponAttackRule weapon)
        {
            string id = weapon != null ? weapon.id : "";
            if (_look != null && _look.Id == id) return;
            _look = TopDownWeaponLook.Of(id);
            _bladeR.sprite = _look.Sprite;
            _bladeL.sprite = _look.Sprite;
            _bladeL.gameObject.SetActive(_look.Twin);
            _right = _look.RestRight;
            _left = _look.RestLeft;
            // 왼손 주먹: 쌍검·장검 왼손은 위아래를 뒤집고(엄지가 바깥), 대검 둘째 손은 같은 손잡이를 같은 쪽에서 쥐므로 그대로.
            _fistL.flipY = !_look.TwoHanded;
            // 검풍 빛 줄: 칼날 구간에 얇게.
            _glow.transform.localPosition = new Vector3((_look.BladeFrom + _look.BladeTo) * 0.5f, 0f, 0f);
            _glow.transform.localScale = new Vector3(_look.BladeTo - _look.BladeFrom, 0.026f, 1f);
        }

        /// <summary>무기 그림: 칸에 그림이 있으면 그 그림, 없으면 임시 그림(같은 손잡이 피벗·유닛 크기라 휘두르기 계산은 그대로). 둘 다 주먹은 없다.</summary>
        void UseWeaponArt(TopDownArt art)
        {
            Sprite s = art != null && art.weapons != null ? art.weapons.For(_look.Id) : null;
            if (!s) s = _look.Sprite;
            if (_bladeR.sprite == s) return;
            _bladeR.sprite = s;
            _bladeL.sprite = s;
        }

        /// <summary>매 LateUpdate(PlayerController·SpriteFlash가 이번 프레임을 마친 뒤). art는 TopDownView.CurrentArt(없으면 null → 임시 그림). 할당 없음.</summary>
        public void Drive(float dt, TopDownArt art)
        {
            if (!Applied || !Valid) return;
            var playerArt = art != null ? art.player : null;
            if (_lookDirty || !ReferenceEquals(playerArt, _lookArt)) SelectLook(playerArt);
            SetWeapon(_player.Weapon);
            UseWeaponArt(art);

            var pose = _player.Pose;
            Vector2 facing = _player.FacingDirection;
            float target = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            // 보간된 그림 위치로 걸음을 센다(물리 위치는 고정 시간마다만 바뀌어 프레임마다 걸음이 끊긴다).
            Vector2 pos = _player.transform.position;
            float moved = (pos - _lastPos).magnitude;
            _lastPos = pos;
            if (dt > 0f)
            {
                // 걷기·대기는 묵직하게 돌고, 조준해서 휘두르는 동작은 판정 순간까지 조준 방향에 다 닿게 빠르게 돈다.
                // 기본공격만 공격 속도를 따라 빨라진다(3-3 ⑥, 공격 속도 0이면 35 그대로). 검풍 시전은 35 그대로.
                float rate = pose == PlayerPose.Attack ? SwingTiming.AimTurnRate(_player.AttackSpeedPermille)
                    : pose == PlayerPose.WaveCast ? AimTurnRate
                    : TurnRate;
                _angle = Mathf.LerpAngle(_angle, target, 1f - Mathf.Exp(-rate * dt));
                bool walking = _player.IsMoving;
                _stepAmp = Mathf.MoveTowards(_stepAmp, walking ? 1f : 0f, dt * 6f);
                if (walking) _phase += moved * (Mathf.PI * 2f / StrideLength);
            }

            float t = _player.PoseTime;
            float duration = _player.PoseDuration;
            float hit = _player.PoseHitTime;
            // 히트스톱(dt=0) 동안은 칼날을 판정 순간 자세(대상 한가운데)에 멈춰 둔다. 판정이 한 프레임 늦게 잡혀 생기는 10~25° 지나침을 숨긴다(그림만).
            if (pose == PlayerPose.Attack && dt <= 0f && hit >= 0f && t > hit && t - hit < 0.04f) t = hit;

            float twist = 6f * Mathf.Sin(_phase) * _stepAmp;
            Vector2 offset = Vector2.zero;
            Vector2 scale = Vector2.one;
            float light = 1f;
            bool feet = true;
            bool direct = false;
            bool bladesBelow = false;
            float glowTarget = 0f;
            var right = _look.RestRight;
            var left = _look.RestLeft;
            float bob = 4f * Mathf.Sin(_phase) * _stepAmp;
            right.Angle += bob;
            left.Angle -= bob;

            switch (pose)
            {
                case PlayerPose.Attack:
                    direct = true;
                    AttackPose(t, duration, hit, ref right, ref left, ref twist, ref offset, ref scale);
                    break;

                case PlayerPose.Whirl:
                {
                    direct = true;
                    float spin = TopDownSwing.Whirl(t, hit > 0f ? hit : 0.1f, 0.2f);
                    twist = spin;
                    // 첫 0.06초에 팔을 벌린다(첫 판정 0.1초 전에 다 벌어짐).
                    float open = TopDownSwing.Smooth(t / 0.06f);
                    right = TopDownHand.Lerp(TopDownSwing.Tucked(_look.RestRight, spin), TopDownSwing.Extended(spin, -1f), open);
                    left = TopDownHand.Lerp(TopDownSwing.Tucked(_look.RestLeft, spin), TopDownSwing.Extended(spin, 1f), open);
                    break;
                }

                case PlayerPose.WaveCast:
                {
                    direct = true;
                    right = TopDownSwing.WaveThrust(_look.RestRight, t, duration > 0f ? duration : 0.25f, out twist);
                    glowTarget = TopDownSwing.Smooth((t - 0.06f) / 0.12f);
                    break;
                }

                case PlayerPose.Dodge:
                {
                    direct = true;
                    float d = duration > 0f ? duration : 0.22f;
                    float u = Mathf.Clamp01(t / d);
                    float spin = 360f * TopDownSwing.Smooth(u);
                    float bump = Mathf.Sin(Mathf.PI * u);
                    twist = spin;
                    scale = new Vector2(1f - 0.2f * bump, 1f - 0.32f * bump);
                    feet = false;
                    right = TopDownSwing.Tucked(_look.RestRight, spin, 0.85f);
                    left = TopDownSwing.Tucked(_look.RestLeft, spin, 0.85f);
                    break;
                }

                case PlayerPose.Hurt:
                {
                    float k = Mathf.Clamp01(1f - t / 0.2f);
                    Vector2 away = pos - _player.LastHitFrom;
                    if (away.sqrMagnitude < 0.0001f) away = -facing;
                    // 맞은 반대쪽으로 살짝 젖힌다(몸 틀 기준으로 바꿔 넣음).
                    offset = TopDownCanvas.Rotate(away.normalized, -_angle) * (0.06f * k);
                    scale = Vector2.one * (1f - 0.04f * k);
                    right.Angle -= 10f * k;
                    left.Angle += 10f * k;
                    break;
                }

                case PlayerPose.Down:
                {
                    float f = TopDownSwing.Smooth(t / 0.3f);
                    twist = 90f * f;
                    light = Mathf.Lerp(1f, 0.55f, f);
                    offset = new Vector2(-0.04f, -0.1f) * f;
                    feet = false;
                    bladesBelow = true;
                    // 무기는 옆 바닥에 떨어뜨린다.
                    right = TopDownHand.At(new Vector2(0.06f, -0.6f), -110f);
                    left = TopDownHand.At(new Vector2(0.0f, 0.6f), 110f);
                    break;
                }

                default:
                    // 대기는 숨 쉬듯 아주 조금 부푼다.
                    if (_stepAmp < 0.01f) scale = Vector2.one * (1f + 0.012f * Mathf.Sin(Time.time * 2.2f));
                    break;
            }

            BlendActionStart(pose, t, hit, direct, ref right, ref left, ref twist, ref offset, ref scale);

            if (dt > 0f || direct)
            {
                float k = 1f - Mathf.Exp(-FollowRate * dt);
                if (direct)
                {
                    _twist = Mathf.DeltaAngle(0f, twist);
                    _right = right;
                    _left = left;
                    _offset = offset;
                    _scale = scale;
                }
                else
                {
                    _twist = Mathf.LerpAngle(_twist, twist, k);
                    _right = TopDownHand.Follow(_right, right, k);
                    _left = TopDownHand.Follow(_left, left, k);
                    _offset = Vector2.Lerp(_offset, offset, k);
                    _scale = Vector2.Lerp(_scale, scale, k);
                }
                _light = pose == PlayerPose.Down ? light : Mathf.MoveTowards(_light, light, dt * 4f);
            }

            UpdateGlow(pose == PlayerPose.WaveCast, glowTarget);
            Place(feet, bladesBelow, pose != PlayerPose.Down, pose);
        }

        /// <summary>
        /// 행동이 새로 시작되면(자세가 바뀌거나 다음 콤보 단계로 시간이 처음부터 다시 흐름) 직전에 보이던 자세를 기억해 두고,
        /// 처음 0.06초 안에 계산된 자세로 부드럽게 넘어간다. 판정이 있는 행동은 판정 순간까지 남은 시간의 절반 안에 끝나므로
        /// 판정 순간의 칼날 자세(TopDownSwing)는 그대로다. 행동이 아닌 때(대기·이동·피격·쓰러짐)는 원래 따라가기 보간을 쓴다.
        /// </summary>
        void BlendActionStart(PlayerPose pose, float t, float hit, bool direct, ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale)
        {
            bool started = pose != _lastPose || t + 1e-4f < _lastPoseTime;
            _lastPose = pose;
            _lastPoseTime = t;
            if (started)
            {
                _fromTwist = _twist;
                _fromRight = _right;
                _fromLeft = _left;
                _fromOffset = _offset;
                _fromScale = _scale;
                _blendT0 = t;
                if (!direct) _blendDur = 0f;
                else if (hit >= 0f) _blendDur = hit > t ? Mathf.Min(0.06f, (hit - t) * 0.5f) : 0f;
                else _blendDur = 0.06f;
            }
            if (!direct || _blendDur <= 0f) return;
            float b = TopDownSwing.Smooth((t - _blendT0) / _blendDur);
            if (b >= 1f) return;
            twist = Mathf.LerpAngle(_fromTwist, twist, b);
            right = TopDownHand.Follow(_fromRight, right, b);
            left = TopDownHand.Follow(_fromLeft, left, b);
            offset = Vector2.Lerp(_fromOffset, offset, b);
            scale = Vector2.Lerp(_fromScale, scale, b);
        }

        /// <summary>지금 콤보 단계(Weapon.combo[ComboIndex])의 모양·시간대로 휘두르기(TopDownSwing.Attack).</summary>
        void AttackPose(float t, float duration, float hit, ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale)
        {
            var weapon = _player.Weapon;
            if (weapon == null || weapon.combo == null || weapon.combo.Length == 0) return;
            TopDownSwing.Attack(weapon, _player.ComboIndex, t, duration, hit, _look.RestRight, _look.RestLeft, _look.Twin,
                ref right, ref left, ref twist, ref offset, ref scale);
        }

        /// <summary>검풍 시전 중 칼날이 빛나고, 시전이 끝나면 0.15초에 걸쳐 꺼진다.</summary>
        void UpdateGlow(bool casting, float target)
        {
            if (casting)
            {
                _glowNow = target;
                _wasWave = true;
                return;
            }
            if (_wasWave)
            {
                _wasWave = false;
                _glowFadeFrom = _glowNow;
                _glowFadeStart = Time.time;
            }
            _glowNow = _glowFadeFrom * Mathf.Clamp01(1f - (Time.time - _glowFadeStart) / 0.15f);
        }

        /// <summary>
        /// 몸 그림 고르기: 고른 갑옷의 몸(맞은 동안·쓰러진 동안 그림이 있으면 그것)과 크기 단위(그림 칸 = PPU 256 × 배율, 임시 = 지름 × 1.08).
        /// 피격·쓰러짐 그림은 몸 그림과 같은 묶음에서만 나온다(SelectLook).
        /// </summary>
        Sprite PoseBody(PlayerPose pose)
        {
            if (pose == PlayerPose.Hurt) return _bodyHurt;
            if (pose == PlayerPose.Down) return _bodyDown;
            return _bodyStand;
        }

        /// <summary>부품 그림을 바꿀 때만 크기도 함께 바꾼다(그림 칸·임시 부품 모두 유닛 그림이라 보통 크기 1).</summary>
        static void UsePartSprite(SpriteRenderer sr, Sprite sprite, Vector3 scale)
        {
            if (sr.sprite == sprite) return;
            sr.sprite = sprite;
            sr.transform.localScale = scale;
        }

        static Color Tint(Color c, float light, float alpha) => new Color(c.r * light, c.g * light, c.b * light, alpha);

        void Place(bool feet, bool bladesBelow, bool arms, PlayerPose pose)
        {
            float alpha = _body.color.a;
            // 번쩍임 셰이더가 있으면 색은 밝기만 맡는다(번쩍임은 셰이더 값). 셰이더가 없을 때는 번쩍임 색을 지우지 않게 쓰러졌을 때만 쓴다.
            if (_flash.useShader || _light < 0.999f) _body.color = new Color(_light, _light, _light, alpha);
            var bodySprite = PoseBody(pose);
            if (_body.sprite != bodySprite) _body.sprite = bodySprite;
            float unit = _bodyUnit;
            var bt = _body.transform;
            bt.localRotation = Quaternion.Euler(0f, 0f, _angle + _twist);
            bt.localPosition = TopDownCanvas.Rotate(_offset, _angle);
            bt.localScale = new Vector3(unit * _scale.x, unit * _scale.y, 1f);

            _rig.localPosition = Vector3.zero;
            _rig.localRotation = Quaternion.Euler(0f, 0f, _angle);
            // 정수리 시점: 서 있는 플레이어와 허리 높이 칼이 낮은 적(2유닛 = YSort 40 안) 위에 오게 몸 순서를 올린다.
            // YSort(순서 0)가 매 프레임 절대값을 다시 넣은 뒤 이 LateUpdate(순서 50)가 돌므로 값이 쌓이지 않는다.
            int order = _body.sortingOrder + 40;
            _body.sortingOrder = order;

            PlaceHelm(pose, unit, order);

            _shadow.sortingOrder = ShadowOrder;
            _shadow.color = new Color(0f, 0f, 0f, 0.4f * alpha);

            if (_footL.enabled != feet)
            {
                _footL.enabled = feet;
                _footR.enabled = feet;
            }
            if (feet)
            {
                UsePartSprite(_footL, _bootSprite, Vector3.one);
                UsePartSprite(_footR, _bootSprite, Vector3.one);
                float s = Mathf.Sin(_phase) * _stepAmp * FootSwing;
                _footL.transform.localPosition = new Vector3(0.04f + s, 0.13f, 0f);
                _footR.transform.localPosition = new Vector3(0.04f - s, -0.13f, 0f);
                var boot = Tint(Color.white, _light, alpha);
                _footL.color = boot;
                _footR.color = boot;
                _footL.sortingOrder = order + UnderOrder;
                _footR.sortingOrder = order + UnderOrder;
            }

            var bladeColor = new Color(_light, _light, _light, alpha);
            PlaceHand(_bladeR, _right, bladeColor, bladesBelow ? order + UnderOrder : order + WeaponOrder);
            if (_look.Twin) PlaceHand(_bladeL, _left, bladeColor, bladesBelow ? order + UnderOrder : order + WeaponOrder);
            PlaceArms(arms, order, alpha);

            bool glowOn = _glowNow > 0.01f;
            if (_glow.enabled != glowOn) _glow.enabled = glowOn;
            if (glowOn)
            {
                _glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, _glowNow * alpha);
                _glow.sortingOrder = order + GlowOrder;
            }
        }

        /// <summary>
        /// 투구: 몸의 자식이라 몸 회전·비틀기·숨쉬기·구르기 찌그러짐·쓰러짐 90°를 그대로 따른다. 크기는 투구 단위 ÷ 몸 단위
        /// (임시 몸 + 임시 투구면 1). 쓰러진 동안은 갑옷마다 정한 머리 자리(downHeadOffset, 유닛)로 옮긴다.
        /// 색은 몸 색을 그대로 받는다(쓰러짐 어둡게·무적 깜빡임 투명도, 셰이더가 없을 때 번쩍임 색). 번쩍임 셰이더 값은 SpriteFlash가 몸과 같이 넣는다.
        /// </summary>
        void PlaceHelm(PlayerPose pose, float unit, int order)
        {
            if (_helm.sprite != _helmSprite) _helm.sprite = _helmSprite;
            if (_helm.sharedMaterial != _body.sharedMaterial) _helm.sharedMaterial = _body.sharedMaterial;
            var ht = _helm.transform;
            float k = _helmUnit / unit;
            ht.localScale = new Vector3(k, k, 1f);
            Vector2 head = pose == PlayerPose.Down ? _downHead / unit : Vector2.zero;
            ht.localPosition = new Vector3(head.x, head.y, 0f);
            ht.localRotation = Quaternion.identity;
            _helm.color = _body.color;
            _helm.sortingLayerID = _body.sortingLayerID;
            _helm.sortingOrder = order + HelmOrder;
            if (_helm.forceRenderingOff != _body.forceRenderingOff) _helm.forceRenderingOff = _body.forceRenderingOff;
        }

        /// <summary>
        /// 팔 막대: 어깨(몸 비틀기·위치를 따름)에서 손까지. 몸 밑에 깔려 몸 밖으로 뻗은 부분만 보인다.
        /// 왼손은 쌍검이면 왼쪽 칼 손, 대검이면 손잡이 두 번째 손(오른 주먹에서 칼날 방향 −0.105), 장검이면 빈 손이다.
        /// 소매 그림이 있으면 길이만 늘이고 굵기는 그림 제 굵기를 쓴다. 주먹(장갑 부품)은 무기 위(+3)에 놓는다.
        /// </summary>
        void PlaceArms(bool on, int order, float alpha)
        {
            if (_armR.enabled != on)
            {
                _armR.enabled = on;
                _armL.enabled = on;
                _fistR.enabled = on;
                _fistL.enabled = on;
            }
            if (!on) return;
            Vector2 shoulderR = TopDownCanvas.Rotate(new Vector2(Shoulder.x * _scale.x, -Shoulder.y * _scale.y), _twist) + _offset;
            Vector2 shoulderL = TopDownCanvas.Rotate(new Vector2(Shoulder.x * _scale.x, Shoulder.y * _scale.y), _twist) + _offset;
            var handL = _left;
            if (_look.TwoHanded)
            {
                handL = _right;
                handL.Pos = _right.Pos + TopDownCanvas.Rotate(new Vector2(_look.SecondGrip * _right.Length * _right.Size, 0f), _right.Angle);
            }
            var sleeve = Tint(_sleeveTint, _light, alpha);
            Segment(_armR, shoulderR, _right.Pos, ArmThickness, order + UnderOrder, sleeve, _sleeveArt);
            Segment(_armL, shoulderL, handL.Pos, ArmThickness, order + UnderOrder, sleeve, _sleeveArt);

            var fist = Tint(Color.white, _light, alpha);
            bool leftHolds = _look.Twin || _look.TwoHanded;
            PlaceFist(_fistR, _right, _fistClosed, fist, order + FistOrder);
            PlaceFist(_fistL, handL, leftHolds ? _fistClosed : _fistOpen, fist, order + FistOrder);
        }

        /// <summary>
        /// 주먹 하나를 손 자리에 놓는다. 손 각도로 돌리고, 카메라 쪽으로 들린 배율(Size)만큼 키운다. 칼날이 머리 위로 넘어가
        /// 길이 배율이 음수면 앞뒤를 뒤집는다(엄지가 칼날 쪽을 따라감). 칼날 길이 배율로는 늘이지 않는다.
        /// </summary>
        static void PlaceFist(SpriteRenderer sr, TopDownHand hand, Sprite sprite, Color color, int order)
        {
            if (sr.sprite != sprite) sr.sprite = sprite;
            var t = sr.transform;
            t.localPosition = new Vector3(hand.Pos.x, hand.Pos.y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, hand.Angle);
            float size = hand.Size;
            t.localScale = new Vector3(hand.Length < 0f ? -size : size, size, 1f);
            sr.color = color;
            sr.sortingOrder = order;
        }

        /// <summary>
        /// 막대 하나를 두 점 사이에 놓는다. art가 없으면 임시 팔(1×1 막대)을 (길이, 굵기)로 늘이고,
        /// 있으면 그림의 가로 길이(유닛)를 두 점 사이 길이에 맞추고 굵기는 그림 그대로 둔다.
        /// </summary>
        static void Segment(SpriteRenderer sr, Vector2 a, Vector2 b, float thickness, int order, Color color, Sprite art)
        {
            Sprite want = art ? art : TopDownSprites.Limb;
            if (sr.sprite != want) sr.sprite = want;
            Vector2 d = b - a;
            var t = sr.transform;
            t.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            float length = Mathf.Max(0.02f, d.magnitude + thickness * 0.5f);
            t.localScale = art
                ? new Vector3(length / Mathf.Max(0.001f, art.bounds.size.x), 1f, 1f)
                : new Vector3(length, thickness, 1f);
            sr.color = color;
            sr.sortingOrder = order;
        }

        static void PlaceHand(SpriteRenderer blade, TopDownHand hand, Color color, int order)
        {
            var t = blade.transform;
            t.localPosition = new Vector3(hand.Pos.x, hand.Pos.y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, hand.Angle);
            t.localScale = new Vector3(hand.Length * hand.Size, hand.Size, 1f);
            blade.color = color;
            blade.sortingOrder = order;
        }
    }
}
