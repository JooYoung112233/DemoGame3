using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점 적 한 마리(TopDownView가 몬다). 몸 SpriteRenderer에 종류별 '위에서 본 한 장'(머리·엄니·꼬리·활까지)을 넣고 바라보는 쪽으로 돌린다
    /// (둥지·허수아비는 돌리지 않음). 걷기는 몸이 살짝 흔들리고 다리 점(자식 부품)이 엇갈린다. 준비 동작은 몸을 낮추고(0.95) 뒤로 당기고,
    /// 공격은 앞으로 짧게 내밀고, 무너짐은 비틀거리고(VisualJitter 유지), 잠·먹는 중은 웅크린다. 궁수는 조준 중 시위를 당긴다(활 부품).
    /// 처치되면 몸은 손대지 않는다(처치 날림 코루틴이 돌리고 줄임) → GoreSystem이 이 몸 스프라이트를 복사해 시체를 남긴다(처치 그림 칸이 있으면 그 그림으로 바꿔 둔다).
    /// 그림 칸(CombatArtSet.topDown)에 몸 그림이 있으면 유닛 그림(PPU 256)으로 놓고, 없으면 코드로 그린 몸 단위 임시 그림을 쓴다. 발 그림은 따로 고른다.
    /// 판정 반지름·충돌·수치는 그대로다. 끄면(Restore) 도형 스프라이트·색·재질·크기·회전으로 되돌린다.
    /// </summary>
    public sealed class TopDownEnemyRig
    {
        const float TurnRate = 20f;
        static readonly Color ForearmColor = new Color(0.19f, 0.27f, 0.25f);
        static readonly Color DrawHandColor = new Color(0.24f, 0.17f, 0.12f);
        static readonly Color ArrowShaftColor = new Color(0.45f, 0.33f, 0.2f);
        static readonly Color ArrowHeadColor = new Color(0.58f, 0.59f, 0.6f);
        static readonly Vector3 DrawHandSize = new Vector3(0.11f, 0.11f, 1f);

        readonly Enemy _enemy;
        readonly SpriteRenderer _body;
        readonly SpriteFlash _flash;
        readonly Sprite _sprite;
        /// <summary>정예로 키우기 전 충돌 지름(유닛). 유닛 그림을 정예 배율(ShapeDiameter ÷ 이 값)로 키울 때 쓴다.</summary>
        readonly float _baseDiameter;
        /// <summary>바라보는 쪽으로 돌리는가(둥지·허수아비는 아님).</summary>
        readonly bool _turns;
        /// <summary>도형일 때 몸을 돌리는 종류(Enemy.Spawn rotateWithFacing: 멧돼지·궁수, 허수아비 제외).</summary>
        readonly bool _shapeTurns;
        readonly Vector2[] _legAt;
        readonly Vector2 _legSize;
        readonly Color _legColor;
        readonly float _stride;
        readonly float _cycle;
        readonly float _sway;
        readonly Vector2 _shadowSize;
        readonly bool _archer;

        Sprite _shapeSprite;
        Material _shapeMaterial;
        Color _savedBaseColor;

        Transform _rig;
        SpriteRenderer _shadow;
        SpriteRenderer[] _legs;
        SpriteRenderer _stringA;
        SpriteRenderer _stringB;
        SpriteRenderer _forearm;
        SpriteRenderer _drawHand;
        SpriteRenderer _arrowShaft;
        SpriteRenderer _arrowHead;

        float _angle;
        float _phase;
        float _stepAmp;
        Vector2 _lastPos;
        bool _partsHidden;
        /// <summary>살아 있던 마지막 프레임에 몸 그림 칸을 썼는가(처치 그림은 같은 크기 단위일 때만 바꿔 끼운다).</summary>
        bool _artBody;

        public bool Applied { get; private set; }
        public Enemy Enemy => _enemy;
        public bool Alive => _enemy;

        TopDownEnemyRig(Enemy enemy, SpriteRenderer body, SpriteFlash flash, Sprite sprite, bool turns)
        {
            _enemy = enemy;
            _body = body;
            _flash = flash;
            _sprite = sprite;
            _turns = turns;
            _shapeTurns = (enemy.Kind == MonsterKind.Boar || enemy.Kind == MonsterKind.Archer) && !enemy.IsDummy;
            // 규칙 묶음(M0a·3차)마다 지름이 같다(굴쥐 0.5, 멧돼지·나무 허수아비 1.1, 궁수 0.6, 둥지 1.6).
            _baseDiameter = Mathf.Max(0.05f, MonsterRule.Of(enemy.Kind).Diameter);
            _legAt = System.Array.Empty<Vector2>();
            _legColor = Color.black;
            _shadowSize = new Vector2(1.1f, 0.9f);
            switch (enemy.Kind)
            {
                case MonsterKind.Rat when !enemy.IsDummy:
                    _legAt = new[] { new Vector2(0.17f, 0.25f), new Vector2(0.17f, -0.25f), new Vector2(-0.25f, 0.24f), new Vector2(-0.25f, -0.24f) };
                    _legSize = new Vector2(0.11f, 0.08f);
                    _legColor = new Color(0.4f, 0.31f, 0.29f);
                    _stride = 0.09f;
                    _sway = 5f;
                    _shadowSize = new Vector2(1.25f, 0.75f);
                    break;
                case MonsterKind.Boar when !enemy.IsDummy:
                    _legAt = new[] { new Vector2(0.2f, 0.37f), new Vector2(0.2f, -0.37f), new Vector2(-0.33f, 0.35f), new Vector2(-0.33f, -0.35f) };
                    _legSize = new Vector2(0.13f, 0.1f);
                    _legColor = new Color(0.11f, 0.09f, 0.075f);
                    _stride = 0.1f;
                    _sway = 3f;
                    _shadowSize = new Vector2(1.3f, 1f);
                    break;
                case MonsterKind.Archer:
                    _archer = true;
                    _legAt = new[] { new Vector2(0.02f, 0.18f), new Vector2(0.02f, -0.18f) };
                    _legSize = new Vector2(0.26f, 0.16f);
                    _legColor = new Color(0.17f, 0.13f, 0.1f);
                    _stride = 0.2f;
                    _sway = 5f;
                    _shadowSize = new Vector2(1f, 1.05f);
                    break;
                case MonsterKind.Nest:
                    _shadowSize = Vector2.zero;
                    break;
                default:
                    // 허수아비(나무·쥐): 움직이지 않는다.
                    _shadowSize = enemy.Kind == MonsterKind.Rat ? new Vector2(1.1f, 0.7f) : new Vector2(0.75f, 0.75f);
                    break;
            }
            _cycle = 1.6f;
        }

        /// <summary>이 적을 정수리 시점으로 그릴 수 있으면 만든다(몸 렌더러가 없으면 null).</summary>
        public static TopDownEnemyRig Create(Enemy enemy)
        {
            var flash = enemy.GetComponent<SpriteFlash>();
            var body = flash ? flash.target : null;
            if (!body) return null;
            bool dummy = enemy.IsDummy;
            Sprite sprite;
            bool turns = !dummy;
            switch (enemy.Kind)
            {
                case MonsterKind.Rat:
                    sprite = dummy ? TopDownSprites.RatDummy : TopDownSprites.Rat;
                    break;
                case MonsterKind.Boar:
                    sprite = dummy ? TopDownSprites.WoodDummy : TopDownSprites.Boar;
                    break;
                case MonsterKind.Archer:
                    sprite = TopDownSprites.Archer;
                    break;
                case MonsterKind.Nest:
                    sprite = TopDownSprites.Nest;
                    turns = false;
                    break;
                default:
                    return null;
            }
            return new TopDownEnemyRig(enemy, body, flash, sprite, turns);
        }

        /// <summary>도형 상태(EnemyVisual이 이번 프레임에 먼저 도형으로 되돌려 둠)를 기억하고 정수리 그림으로 바꾼다. 쓰러지는 중이면 손대지 않는다.</summary>
        public void Apply()
        {
            if (Applied || !_enemy || !_body || _enemy.Dead) return;
            Applied = true;
            _shapeSprite = _body.sprite;
            _shapeMaterial = null;
            ArtRuntime.UseArtMaterial(_body, _flash, ref _shapeMaterial);
            if (_flash)
            {
                // 시체(GoreSystem)는 도형 색(baseColor)으로 그림을 물들이므로 흰색으로 둔다(어둡게 바래는 색은 그대로). 끄면 되돌린다.
                _savedBaseColor = _flash.baseColor;
                _flash.baseColor = Color.white;
                // UseArtMaterial이 이미 useShader를 켜므로 조건 없이 흰색으로 둔다(멈춘 채 다시 켜면 SpriteFlash가 갱신을 건너뛰어 도형 색이 남았다).
                _body.color = new Color(1f, 1f, 1f, _body.color.a);
            }
            _body.sprite = _sprite;
            _body.flipX = false;
            EnsureParts();
            ShowParts(true);
            Vector2 f = _enemy.FacingDirection;
            _angle = _turns ? Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg : 0f;
            _lastPos = _enemy.transform.position;
        }

        /// <summary>도형 상태로 되돌린다. 쓰러지는 중이면 크기·회전은 처치 연출에 맡긴다.</summary>
        public void Restore()
        {
            if (!Applied) return;
            Applied = false;
            if (_rig) ShowParts(false);
            if (!_enemy || !_body) return;
            _body.sprite = _shapeSprite;
            _body.flipX = false;
            ArtRuntime.UseShapeMaterial(_body, _flash, _shapeMaterial);
            if (_flash)
            {
                _flash.baseColor = _savedBaseColor;
                var c = _savedBaseColor;
                c.a = _body.color.a;
                _body.color = c;
            }
            if (_enemy.Dead) return;
            var t = _body.transform;
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one * _enemy.ShapeDiameter;
            Vector2 f = _enemy.FacingDirection;
            t.rotation = _shapeTurns ? Quaternion.Euler(0f, 0f, Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg) : Quaternion.identity;
        }

        void EnsureParts()
        {
            if (_rig) return;
            var go = new GameObject("TopDown");
            go.layer = _enemy.gameObject.layer;
            _rig = go.transform;
            _rig.SetParent(_enemy.transform, false);
            // 부품은 번쩍이지 않으므로 빛 무시 기본 재질(SpriteRenderer.color·투명도가 먹음)을 쓴다.
            // 번쩍임 재질은 PropertyBlock 없는 렌더러의 색을 무시해 다리 점·활 부품이 흰색으로 그려졌다.
            var art = ArtRuntime.FlashMaterial;
            var unlitPart = RenderMaterials.Unlit;
            Material partMat = unlitPart ? unlitPart : (art ? art : _shapeMaterial);
            if (_shadowSize.x > 0f)
            {
                _shadow = TopDownView.Part("Shadow", _rig, TopDownSprites.Shadow, _shapeMaterial, new Color(0f, 0f, 0f, 0.38f), _body);
                _shadow.transform.localScale = new Vector3(_shadowSize.x, _shadowSize.y, 1f);
            }
            _legs = new SpriteRenderer[_legAt.Length];
            for (int i = 0; i < _legAt.Length; i++)
            {
                _legs[i] = TopDownView.Part("Leg", _rig, ShapeSprites.Circle, partMat, _legColor, _body);
                _legs[i].transform.localScale = new Vector3(_legSize.x, _legSize.y, 1f);
                // 발 그림은 오른쪽 발로 그린다: 왼쪽(+y) 발은 위아래로 뒤집는다(점 도형은 대칭이라 차이 없음).
                _legs[i].flipY = _legAt[i].y > 0f;
            }
            if (_archer)
            {
                // 활 부품은 몸에 붙인다(몸이 낮아지거나 돌 때 활과 어긋나지 않게).
                var bodyT = _body.transform;
                var stringColor = new Color(0.78f, 0.75f, 0.66f);
                _stringA = TopDownView.Part("BowString", bodyT, ShapeSprites.Square, partMat, stringColor, _body);
                _stringB = TopDownView.Part("BowString", bodyT, ShapeSprites.Square, partMat, stringColor, _body);
                _forearm = TopDownView.Part("Forearm", bodyT, TopDownSprites.Limb, partMat, ForearmColor, _body);
                _drawHand = TopDownView.Part("DrawHand", bodyT, ShapeSprites.Circle, partMat, DrawHandColor, _body);
                _drawHand.transform.localScale = DrawHandSize;
                _arrowShaft = TopDownView.Part("Arrow", bodyT, ShapeSprites.Square, partMat, ArrowShaftColor, _body);
                _arrowHead = TopDownView.Part("ArrowHead", bodyT, ShapeSprites.Triangle, partMat, ArrowHeadColor, _body);
                _arrowHead.transform.localScale = new Vector3(0.085f, 0.075f, 1f);
            }
        }

        /// <summary>이 적의 그림 칸(없으면 null). 허수아비는 종류(굴쥐·멧돼지 자리)와 따로 고른다.</summary>
        TopDownBodyArt Slot(TopDownArt art)
        {
            if (art == null) return null;
            switch (_enemy.Kind)
            {
                case MonsterKind.Rat: return _enemy.IsDummy ? art.ratDummy : art.rat;
                case MonsterKind.Boar: return _enemy.IsDummy ? art.woodDummy : art.boar;
                case MonsterKind.Archer: return art.archer;
                case MonsterKind.Nest: return art.nest;
                default: return null;
            }
        }

        /// <summary>지금 쓸 몸 그림(칸이 비면 null → 임시 그림). 둥지는 껍질이 깨졌고 열린 그림이 있으면 그것.</summary>
        Sprite BodyArt(TopDownBodyArt slot)
        {
            if (slot == null || !slot.body) return null;
            if (slot is TopDownNestArt nest && nest.opened && _enemy is NestBrain brain && !brain.HasShell) return nest.opened;
            return slot.body;
        }

        /// <summary>매 LateUpdate(브레인 Think·Enemy.LateUpdate·YSort 뒤). art는 TopDownView.CurrentArt(없으면 null → 임시 그림). 할당 없음.</summary>
        public void Drive(float dt, TopDownArt art)
        {
            if (!Applied || !_enemy || !_body) return;
            var slot = Slot(art);
            if (_enemy.Dead)
            {
                // 처치 연출(날림·시체)은 몸 스프라이트만 쓴다: 부품을 숨기고 몸 크기·회전은 손대지 않는다.
                if (!_partsHidden) ShowParts(false);
                // 처치 그림 칸: 살아 있을 때 몸 그림 칸을 썼으면(같은 유닛 크기) 날아가는 동안과 시체에 그 그림을 쓴다.
                if (_artBody && slot != null && slot.dead && _body.sprite != slot.dead) _body.sprite = slot.dead;
                return;
            }
            if (_partsHidden) ShowParts(true);

            Sprite bodyArt = BodyArt(slot);
            _artBody = bodyArt != null;
            Sprite bodySprite = _artBody ? bodyArt : _sprite;
            if (_body.sprite != bodySprite) _body.sprite = bodySprite;

            float d = _enemy.ShapeDiameter;
            // 몸 크기: 임시 그림은 몸 단위(지름 1)라 지름을 곱하고, 그림 칸은 유닛 그림이라 정예 배율 × 칸 배율만 곱한다.
            float unit = _artBody ? d / _baseDiameter * Mathf.Max(0.01f, slot.scale) : d;
            // 보간된 그림 위치로 걸음을 센다(물리 위치는 고정 시간마다만 바뀐다).
            Vector2 pos = _enemy.transform.position;
            float moved = (pos - _lastPos).magnitude;
            _lastPos = pos;
            Vector2 facing = _enemy.FacingDirection;
            if (dt > 0f)
            {
                if (_turns) _angle = Mathf.LerpAngle(_angle, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg, 1f - Mathf.Exp(-TurnRate * dt));
                bool walking = _enemy.IsMoving && moved > 0.1f * dt;
                _stepAmp = Mathf.MoveTowards(_stepAmp, walking ? 1f : 0f, dt * 7f);
                if (walking) _phase += moved * (Mathf.PI * 2f / Mathf.Max(0.1f, d * _cycle));
            }

            var pose = _enemy.CurrentPose;
            float t = _enemy.PoseTime;
            float wobble = _sway * Mathf.Sin(_phase) * _stepAmp;
            Vector2 offset = Vector2.zero;
            Vector2 scale = Vector2.one;
            float draw = 0f;
            float stringBuzz = 0f;
            bool arrow = false;

            switch (pose)
            {
                case EnemyPose.Windup:
                {
                    float p = Mathf.Clamp01(t / Mathf.Max(0.05f, _enemy.PoseDuration));
                    float crouch = TopDownSwing.Smooth(p * 2.5f);
                    scale *= 1f - 0.05f * crouch;
                    offset.x -= (_archer ? 0.03f : 0.07f) * crouch;
                    if (_archer)
                    {
                        draw = TopDownSwing.Smooth(p / 0.55f);
                        arrow = true;
                    }
                    break;
                }
                case EnemyPose.Attack:
                {
                    float k = Mathf.Clamp01(1f - t / 0.2f);
                    if (_archer)
                    {
                        offset.x -= 0.04f * k;
                        stringBuzz = k;
                    }
                    else offset.x += 0.12f * k;
                    if (_enemy.Kind == MonsterKind.Boar && _stepAmp > 0.5f) scale = new Vector2(1.06f, 0.94f);
                    break;
                }
                case EnemyPose.Hit:
                    if (_enemy.Broken)
                    {
                        wobble += 9f * Mathf.Sin(Time.time * 11f);
                        scale *= 0.97f;
                    }
                    else wobble += 6f * Mathf.Sin(t * 30f) * Mathf.Clamp01(1f - t / 0.25f);
                    break;
                case EnemyPose.Idle:
                case EnemyPose.Locomotion:
                    if (!_enemy.Aware && _turns)
                    {
                        // 잠·먹는 중: 웅크리고 천천히 숨 쉰다. 먹는 중은 머리를 까딱인다.
                        float breath = 0.025f * Mathf.Sin(Time.time * 1.8f);
                        scale = new Vector2(0.9f + breath, 0.93f + breath);
                        if (_enemy.IsEating)
                        {
                            float nod = Mathf.Max(0f, Mathf.Sin(Time.time * 8f));
                            offset.x += 0.035f * nod;
                            wobble += 2f * Mathf.Sin(Time.time * 8f);
                        }
                    }
                    else if (_stepAmp < 0.01f && _turns) scale *= 1f + 0.012f * Mathf.Sin(Time.time * 2.4f);
                    break;
            }

            var bt = _body.transform;
            float angle = _turns ? _angle : 0f;
            bt.localRotation = Quaternion.Euler(0f, 0f, angle + wobble);
            Vector2 jitter = _enemy.VisualJitter;
            Vector2 local = TopDownCanvas.Rotate(offset * d, angle) + jitter;
            bt.localPosition = new Vector3(local.x, local.y, 0f);
            bt.localScale = new Vector3(unit * scale.x, unit * scale.y, 1f);

            _rig.localPosition = new Vector3(jitter.x, jitter.y, 0f);
            _rig.localRotation = Quaternion.Euler(0f, 0f, angle);
            _rig.localScale = new Vector3(d, d, 1f);
            int order = _body.sortingOrder;
            float alpha = _body.color.a;
            if (_shadow)
            {
                _shadow.sortingOrder = TopDownPlayerRig.ShadowOrder;
                _shadow.color = new Color(0f, 0f, 0f, 0.38f * alpha);
            }
            PlaceLegs(slot is TopDownCreatureArt creature && creature.foot ? creature.foot : null, order, alpha);
            if (_archer) PlaceBow(draw, stringBuzz, t, arrow, order, alpha, _artBody ? slot as TopDownArcherArt : null);
        }

        /// <summary>
        /// 다리 점(또는 발 그림): 대각선 짝(앞왼·뒤오른 / 앞오른·뒤왼)이 함께 앞뒤로 엇갈린다. 자리는 몸 단위(틀이 지름만큼 커져 있음)이고,
        /// 발 그림은 유닛 그림이라 틀 배율을 지워(1 ÷ 기본 지름) 제 크기로 놓는다(정예는 틀을 따라 함께 커진다).
        /// </summary>
        void PlaceLegs(Sprite footArt, int order, float alpha)
        {
            if (_legs.Length == 0) return;
            Sprite legSprite = footArt ? footArt : ShapeSprites.Circle;
            bool swap = _legs[0].sprite != legSprite;
            var legScale = footArt ? new Vector3(1f / _baseDiameter, 1f / _baseDiameter, 1f) : new Vector3(_legSize.x, _legSize.y, 1f);
            Color tint = footArt ? Color.white : _legColor;
            float swing = Mathf.Sin(_phase) * _stepAmp * _stride;
            for (int i = 0; i < _legs.Length; i++)
            {
                bool pairA = _legs.Length == 2 ? i == 0 : (i == 0 || i == 3);
                Vector2 at = _legAt[i] + new Vector2(pairA ? swing : -swing, 0f);
                var leg = _legs[i];
                if (swap)
                {
                    leg.sprite = legSprite;
                    leg.transform.localScale = legScale;
                }
                leg.transform.localPosition = new Vector3(at.x, at.y, 0f);
                leg.sortingOrder = order - 1;
                leg.color = new Color(tint.r, tint.g, tint.b, alpha);
            }
        }

        /// <summary>
        /// 시위 두 줄·아래팔·당김 손·화살. draw 0이면 시위가 곧게, 1이면 가슴까지 당김. 부품은 몸에 붙어 몸과 함께 돌고 낮아진다.
        /// art가 null이면 임시 몸 그림(몸 로컬 1 = 몸 지름)이라 몸 단위 좌표를 그대로 쓰고, 있으면 몸 그림 칸(몸 로컬 1 = 칸 배율 유닛)이라
        /// 몸 단위 길이에는 '기본 지름 ÷ 배율'을, 칸의 유닛 좌표(활 끝·팔꿈치)와 부품 그림에는 '1 ÷ 배율'을 곱한다.
        /// </summary>
        void PlaceBow(float draw, float buzz, float t, bool arrow, int order, float alpha, TopDownArcherArt art)
        {
            float u = 1f;
            float inv = 1f;
            Vector2 tipA = TopDownSprites.ArcherBowTip;
            Vector2 elbow = TopDownSprites.ArcherElbow;
            if (art != null)
            {
                inv = 1f / Mathf.Max(0.01f, art.scale);
                u = _baseDiameter * inv;
                tipA = art.bowTip * inv;
                elbow = art.drawElbow * inv;
            }
            Vector2 tipB = new Vector2(tipA.x, -tipA.y);
            float ax = Mathf.Lerp(tipA.x, 0.03f * u, draw) + 0.025f * u * buzz * Mathf.Sin(t * 70f);
            Vector2 apex = new Vector2(ax, 0f);
            Segment(_stringA, tipA, apex, 0.016f * u, order + 1, alpha);
            Segment(_stringB, tipB, apex, 0.016f * u, order + 1, alpha);

            Sprite forearmArt = art != null && art.forearm ? art.forearm : null;
            if (forearmArt) SegmentArt(_forearm, elbow, apex, forearmArt, inv, order + 1);
            else
            {
                UseSprite(_forearm, TopDownSprites.Limb);
                Segment(_forearm, elbow, apex, 0.1f * u, order + 1, alpha);
            }
            SetTint(_forearm, forearmArt ? Color.white : ForearmColor, alpha);

            Sprite handArt = art != null && art.drawHand ? art.drawHand : null;
            UseSprite(_drawHand, handArt ? handArt : ShapeSprites.Circle);
            _drawHand.transform.localScale = handArt ? new Vector3(inv, inv, 1f) : new Vector3(DrawHandSize.x * u, DrawHandSize.y * u, 1f);
            _drawHand.transform.localPosition = new Vector3(apex.x, apex.y, 0f);
            _drawHand.sortingOrder = order + 2;
            SetTint(_drawHand, handArt ? Color.white : DrawHandColor, alpha);

            Sprite arrowArt = art != null && art.arrow ? art.arrow : null;
            bool head = arrow && !arrowArt;
            if (_arrowShaft.enabled != arrow) _arrowShaft.enabled = arrow;
            if (_arrowHead.enabled != head) _arrowHead.enabled = head;
            if (!arrow) return;
            if (arrowArt)
            {
                // 화살 그림: 오늬 끝(피벗)을 당김 점에 두고 촉이 앞(+x)을 향한다.
                UseSprite(_arrowShaft, arrowArt);
                var at = _arrowShaft.transform;
                at.localPosition = new Vector3(apex.x, apex.y, 0f);
                at.localRotation = Quaternion.identity;
                at.localScale = new Vector3(inv, inv, 1f);
                _arrowShaft.sortingOrder = order + 1;
                SetTint(_arrowShaft, Color.white, alpha);
                return;
            }
            UseSprite(_arrowShaft, ShapeSprites.Square);
            Segment(_arrowShaft, apex, apex + new Vector2(0.6f * u, 0f), 0.022f * u, order + 1, alpha);
            SetTint(_arrowShaft, ArrowShaftColor, alpha);
            _arrowHead.transform.localPosition = new Vector3(apex.x + 0.63f * u, 0f, 0f);
            _arrowHead.transform.localRotation = Quaternion.identity;
            _arrowHead.transform.localScale = new Vector3(0.085f * u, 0.075f * u, 1f);
            _arrowHead.sortingOrder = order + 1;
            SetAlpha(_arrowHead, alpha);
        }

        /// <summary>부품 그림 하나를 두 점 사이에 놓는다: 그림의 가로 길이(유닛)를 두 점 사이 길이에 맞추고 굵기는 그림 그대로(1 ÷ 배율).</summary>
        static void SegmentArt(SpriteRenderer sr, Vector2 a, Vector2 b, Sprite art, float inv, int order)
        {
            UseSprite(sr, art);
            Vector2 d = b - a;
            var t = sr.transform;
            t.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(Mathf.Max(0.001f, d.magnitude) / Mathf.Max(0.001f, art.bounds.size.x), inv, 1f);
            sr.sortingOrder = order;
        }

        static void UseSprite(SpriteRenderer sr, Sprite sprite)
        {
            if (sr.sprite != sprite) sr.sprite = sprite;
        }

        /// <summary>색(그림 칸은 흰색, 임시 부품은 정해 둔 색)과 투명도를 넣는다. 같으면 건드리지 않는다.</summary>
        static void SetTint(SpriteRenderer sr, Color rgb, float alpha)
        {
            var c = new Color(rgb.r, rgb.g, rgb.b, alpha);
            if (sr.color != c) sr.color = c;
        }

        void SetArcherParts(bool on, bool arrow)
        {
            if (!_archer || !_stringA) return;
            _stringA.enabled = on;
            _stringB.enabled = on;
            _forearm.enabled = on;
            _drawHand.enabled = on;
            _arrowShaft.enabled = on && arrow;
            _arrowHead.enabled = on && arrow;
        }

        /// <summary>네모 스프라이트 하나를 두 점 사이 막대로 놓는다(몸 단위).</summary>
        static void Segment(SpriteRenderer sr, Vector2 a, Vector2 b, float thickness, int order, float alpha)
        {
            Vector2 d = b - a;
            var t = sr.transform;
            t.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(Mathf.Max(0.001f, d.magnitude), thickness, 1f);
            sr.sortingOrder = order;
            SetAlpha(sr, alpha);
        }

        static void SetAlpha(SpriteRenderer sr, float alpha)
        {
            var c = sr.color;
            if (Mathf.Abs(c.a - alpha) < 0.001f) return;
            c.a = alpha;
            sr.color = c;
        }

        /// <summary>부품(그림자·다리 틀, 몸에 붙은 활 부품)을 함께 보이거나 숨긴다.</summary>
        void ShowParts(bool on)
        {
            _partsHidden = !on;
            if (_rig && _rig.gameObject.activeSelf != on) _rig.gameObject.SetActive(on);
            SetArcherParts(on, false);
        }
    }
}
