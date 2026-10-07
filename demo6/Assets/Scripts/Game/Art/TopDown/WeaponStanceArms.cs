using Demo6.Core.Combat;
using Demo6.Core.Combat.Stance;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 세 무기 팔·칼 그림 고쳐 그리기(spec.json 3차 '팔_그리기'·'무기_모양', 2026-10-05). 리그(TopDownPlayerRig, 다른 세션 파일)는 어깨 → 손 곧은 막대 하나를
    /// 몸 밑에 그리고 칼 그림을 길이·크기·세움·처형 칼 배율(1.25)로 늘이고 줄여 놓는다. 이 부품은 TopDownView(순서 50)가 리그를 다 놓은 뒤(순서 60) 같은 프레임에
    /// 리그가 놓은 부품 자리를 읽어 고친다(리그 파일은 고치지 않음, 리그가 매 프레임 다시 놓으므로 쌓이지 않음).
    /// ① 두 마디 팔: 어깨 → 팔꿈치(어깨·손 선 위 0.40 ÷ 0.84 자리에서 몸 가운데와 먼 쪽으로 0.10 × clamp((0.80 − 거리) ÷ 0.25, 0.3, 1)) → 손.
    ///    위팔(굵기 0.12)은 리그 팔 막대를 고쳐 몸 밑(−1)에, 아래팔(굵기 0.10)은 새 막대로 몸 위·투구 밑에 그린다(투구와 같은 순서, 조금 뒤 깊이).
    /// ② 칼 그림 배율 1(3차, 사용자 원문 "지금보니 검들이 다늘어나고 압축되는데 이부분이 제일어색한것같다"): 세 무기 칼 그림의 늘이기·줄이기(길이·크기·세움·처형 1.25배)를
    ///    모두 지워 늘 제 크기로 둔다.
    /// ③ 대검: 기울기(WeaponStanceLook.Tilt, 대기·걷기는 리그 따라가기처럼 부드럽게)에 맞는 기울기별 그림(GreatswordTiltArt, 넓은 날 임시 그림, 원화가 오면 같은 칸)을
    ///    갈아 끼우고, 둘째 주먹을 오른손 + 회전((−0.18 × max(cos 기울기, 0.65), 0), 날)에, 칼빛 띠를 보이는 날 구간(0.11 ~ 1.33 × cos 기울기)에 놓는다.
    ///    손 높이(WeaponStanceLook.Height)가 있으면 칼 그림자(부드러운 어두운 띠, 바닥 자리 + 0.4 × 높이 × 화면 아래)를 몸 그림자 위에 깐다.
    ///    세로 내려찍기(③·처형)가 바닥에 닿으면(WeaponStanceLook.ImpactCount) 돌 조각 고리(Resources/OgreVfxV30/slam, 크기 1.6, 0.28초)·먼지(dust, 1.0)·
    ///    바닥 균열(임시 그림, 0.6초)을 내고 화면을 0.06 / 0.08초 흔든다(맞히면 PlayerController가 ③ 흔들림 0.14와 히트스톱 0.09를 따로 줌, 큰 값이 남음).
    /// 귀여운 판(리그가 팔·주먹을 끔)·세 무기 밖은 손대지 않는다. 쓰러짐(팔 끔)은 팔을 그리지 않고 대검 그림(넓은 날, 제 크기)만 맞춘다. 판정은 바꾸지 않는다(그림만). 매 프레임 할당 없음(효과 그림은 처음 한 번 만듦).
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class WeaponStanceArms : MonoBehaviour
    {
        /// <summary>위팔·아래팔 길이(팔꿈치 비율), 굵기, 팔꿈치 벌림 최대(spec '몸_모형.팔').</summary>
        public const float UpperArm = 0.40f;
        public const float Forearm = 0.44f;
        public const float UpperThickness = 0.12f;
        public const float ForearmThickness = 0.10f;
        public const float ElbowBendMax = 0.10f;
        /// <summary>소매 그림 칸의 제 굵기(CombatArtSet 소매 설명 '굵기 약 0.085유닛'). 그림이면 굵기 ÷ 이 값으로 세로를 키운다.</summary>
        const float SleeveArtThickness = 0.085f;
        /// <summary>아래팔을 투구와 같은 정렬 순서에 두고 투구 뒤(카메라에서 조금 먼 깊이)로 보내 투구 위로 넘지 않게 한다.</summary>
        const float ForearmDepth = 0.01f;

        static PlayerController s_checked;

        /// <summary>플레이어에 한 번 붙인다(WeaponStanceLook.Override가 부름, 이미 있으면 아무것도 안 함).</summary>
        public static void Ensure(PlayerController p)
        {
            if (ReferenceEquals(p, s_checked) || !p) return;
            s_checked = p;
            if (!p.GetComponent<WeaponStanceArms>()) p.gameObject.AddComponent<WeaponStanceArms>();
        }

        PlayerController _p;
        Transform _rig;
        SpriteRenderer _armR, _armL, _fistR, _fistL, _bladeR, _bladeL;
        SpriteRenderer _foreR, _foreL;
        /// <summary>번쩍임 값 옮기기용(MonoBehaviour 생성자에서는 만들 수 없어 처음 쓸 때 만든다).</summary>
        MaterialPropertyBlock _block;

        /// <summary>대기·걷기 기울기 따라가기 빠르기(리그 FollowRate와 같음).</summary>
        const float TiltFollowRate = 16f;
        /// <summary>칼 그림자: 바닥 자리 + Drop × 높이 × 화면 아래, 폭, 투명도(낮을 때 → 칼끝이 높을 때).</summary>
        const float ShadowDrop = 0.4f, ShadowWidth = 0.5f, ShadowAlphaLow = 0.30f, ShadowAlphaHigh = 0.15f;
        /// <summary>바닥 효과 크기·길이(오우거 효과 그림 재사용, 고리 0.28초 크기 1.6, 먼지 0.30초 크기 1.0, 균열 0.6초).</summary>
        const float RingSize = 1.6f, RingLife = 0.28f, DustSize = 1.0f, DustLife = 0.30f, CrackSize = 1.0f, CrackLife = 0.6f;
        /// <summary>헛쳐도 바닥에 닿은 무게를 보이는 화면 흔들림(맞히면 기존 ③ 흔들림이 더 커서 그것이 남음).</summary>
        const float ImpactShake = 0.06f, ImpactShakeTime = 0.08f;

        SpriteRenderer _bladeShadow, _floorShadow;
        Transform _glowT, _counterT;
        float _tilt;
        bool _tiltReady;
        int _impactSeen = -1;
        string _authoredClip; int _authoredAction=-1; float _authoredClock=-1;

        sealed class Puff
        {
            public SpriteRenderer Sr;
            public Sprite[] Frames;
            public float Age, Life, Alpha;
        }
        static Sprite[] s_ring, s_dust;
        static bool s_fxLoaded;
        GameObject _fxRoot;
        Puff _ring, _dust, _crack;

        /// <summary>시험·확인용: 이번 프레임에 고쳐 그렸나, 대검 둘째 주먹 자리(리그 틀), 팔꿈치(리그 틀), 그린 기울기, 칼 그림자가 보이나, 바닥 효과를 낸 횟수.</summary>
        public bool Drawn { get; private set; }
        public Vector2 SecondFist { get; private set; }
        public float ShownTilt => _tilt;
        public bool ShadowOn => _bladeShadow && _bladeShadow.enabled;
        public int ImpactsShown { get; private set; }
        public Vector2 ElbowRight { get; private set; }
        public Vector2 ElbowLeft { get; private set; }

        void Awake() => _p = GetComponent<PlayerController>();

        bool FindParts()
        {
            if (_rig && _armR && _fistR && _bladeR) return true;
            _rig = transform.Find("TopDown");
            if (!_rig) return false;
            _armR = Child("ArmR");
            _armL = Child("ArmL");
            _fistR = Child("FistR");
            _fistL = Child("FistL");
            _bladeR = Child("WeaponR");
            _bladeL = Child("WeaponL");
            return _armR && _armL && _fistR && _fistL && _bladeR;
        }

        SpriteRenderer Child(string name)
        {
            var t = _rig.Find(name);
            return t ? t.GetComponent<SpriteRenderer>() : null;
        }

        void EnsureForearms()
        {
            if (_foreR && _foreL) return;
            _foreR = TopDownView.Part("ForearmR", _rig, _armR.sprite, _armR.sharedMaterial, Color.white, _armR);
            _foreL = TopDownView.Part("ForearmL", _rig, _armL.sprite, _armL.sharedMaterial, Color.white, _armL);
        }

        void Hide()
        {
            Drawn = false;
            if (_foreR && _foreR.enabled) _foreR.enabled = false;
            if (_foreL && _foreL.enabled) _foreL.enabled = false;
            HideShadow();
        }

        void HideShadow()
        {
            if (_bladeShadow && _bladeShadow.enabled) _bladeShadow.enabled = false;
        }

        void LateUpdate()
        {
            if (!_p) _p = GetComponent<PlayerController>();
            float dt = Time.deltaTime;
            UpdateFx(dt);
            // The approved first-attack adapter owns its separate source-cel arms and weapons.
            var authored = TopDownView.PlayerRig?.FirstAttackArt;
            if (authored?.Active == true)
            {
                Hide();
                _tiltReady = false;
                var pose=authored.AuthoredRight;
                if(_p&&_p.Weapon?.id=="wpn_greatsword"&&authored.ExtendedMotion&&pose!=null&&FindParts()){
                    _tilt=pose[4];
                    PlaceShadow(new Vector2(pose[0],pose[1]),pose[3],Mathf.Max(.05f,pose[2]-1f));
                    if(_authoredClip!=authored.ClipId||_authoredAction!=_p.ActionId||authored.VisualTime<_authoredClock-.002f)_authoredClock=-1;
                    if(authored.AuthoredSlam&&_authoredClock<authored.AuthoredHitTime&&authored.VisualTime>=authored.AuthoredHitTime)
                        SpawnImpact(new Vector2(pose[0],pose[1]),pose[3]);
                    _authoredClip=authored.ClipId;_authoredAction=_p.ActionId;_authoredClock=authored.VisualTime;
                }else{_authoredClip=null;_authoredClock=-1;}
                return;
            }
            var weapon = _p ? _p.Weapon : null;
            var w = weapon != null ? WeaponStances.Of(weapon.id) : StanceWeapon.None;
            if (w == StanceWeapon.None || !FindParts() || !_rig.gameObject.activeInHierarchy)
            {
                Hide();
                _tiltReady = false;
                return;
            }
            if (!_armR.enabled || !_fistR.enabled)
            {
                // 쓰러짐(리그가 팔·주먹을 끔): 리그는 매 프레임 원화 칸의 좁은 칼로 되돌리므로 대검은 여기서도 같은 넓은 날 그림을 제 크기로 둔다
                // (쓰러질 때 칼이 다른 칼로 바뀌지 않게). 귀여운 판(한손검만, 팔을 끔)은 손대지 않는다.
                if (w == StanceWeapon.Greatsword) KeepGreatswordArt(WeaponStanceLook.Tilt);
                Hide();
                _tiltReady = false;
                return;
            }
            EnsureForearms();

            // ② 칼 그림은 늘 제 크기(리그가 넣은 길이·크기·세움·처형 1.25배를 지움). 자리·각은 리그 값 그대로.
            _bladeR.transform.localScale = Vector3.one;
            if (w == StanceWeapon.Twinblades && _bladeL) _bladeL.transform.localScale = Vector3.one;

            // 기울기: 행동 자세는 곧바로, 대기·걷기·맞음은 리그 따라가기처럼 부드럽게.
            float target = WeaponStanceLook.Tilt;
            if (!_tiltReady || WeaponStanceLook.TiltDirect) _tilt = target;
            else if (dt > 0f) _tilt = Mathf.Lerp(_tilt, target, 1f - Mathf.Exp(-TiltFollowRate * dt));
            _tiltReady = true;

            // 리그가 놓은 손·어깨(리그 틀). 팔 막대는 어깨와 손의 가운데에 놓였다.
            var tr = _fistR.transform;
            Vector2 handR = tr.localPosition;
            float angleR = tr.localEulerAngles.z;
            Vector2 handL = _fistL.transform.localPosition;
            Vector2 shoulderR = 2f * (Vector2)_armR.transform.localPosition - handR;
            Vector2 shoulderL = 2f * (Vector2)_armL.transform.localPosition - handL;

            if (w == StanceWeapon.Greatsword)
            {
                // ③ 기울기별 그림(원화 칸이 생기면 같은 자리), 둘째 주먹, 칼빛 띠, 그림자, 바닥 효과.
                var sprite = GreatswordTiltArt.For(_tilt);
                if (_bladeR.sprite != sprite) _bladeR.sprite = sprite;
                var second = GreatswordPoses.SecondHand(HandPose.Tilted(handR.x, handR.y, angleR, _tilt));
                handL = new Vector2(second.X, second.Y);
                _fistL.transform.localPosition = new Vector3(handL.x, handL.y, 0f);
                SecondFist = handL;
                float c = Mathf.Cos(_tilt * Mathf.Deg2Rad);
                PlaceGlow(c);
                PlaceShadow(handR, angleR);
                int impacts = WeaponStanceLook.ImpactCount;
                if (_impactSeen < 0) _impactSeen = impacts;
                else if (impacts != _impactSeen)
                {
                    _impactSeen = impacts;
                    SpawnImpact(handR, angleR);
                }
            }
            else
            {
                HideShadow();
                _impactSeen = WeaponStanceLook.ImpactCount;
            }

            // 몸 틀: 어깨 둘의 가운데 = 앞뒤·옆 밀림, 어깨 방향 = 비틀기, 어깨 사이 ÷ 0.54 = 몸 세로 크기.
            Vector2 center = (shoulderR + shoulderL) * 0.5f;
            Vector2 across = shoulderL - shoulderR;
            float bodyScale = across.magnitude / (2f * WeaponKeepOut.ShoulderY);
            Vector2 left = across.sqrMagnitude > 1e-8f ? across.normalized : Vector2.up;
            Vector2 forward = new Vector2(left.y, -left.x);
            Vector2 bodyMid = center + forward * (0.05f * bodyScale);

            var color = _armR.color;
            ElbowRight = DrawArm(_armR, _foreR, shoulderR, handR, bodyMid, -left, color);
            ElbowLeft = DrawArm(_armL, _foreL, shoulderL, handL, bodyMid, left, color);
            CopyFlash(_armR, _foreR);
            CopyFlash(_armL, _foreL);
            Drawn = true;
        }

        /// <summary>팔 없이 대검 그림만 맞춘다(쓰러짐): 기울기 그림, 제 크기, 칼빛 띠 자리.</summary>
        void KeepGreatswordArt(float tilt)
        {
            _tilt = tilt;
            var sprite = GreatswordTiltArt.For(tilt);
            if (_bladeR.sprite != sprite) _bladeR.sprite = sprite;
            _bladeR.transform.localScale = Vector3.one;
            PlaceGlow(Mathf.Cos(tilt * Mathf.Deg2Rad));
        }

        /// <summary>칼빛 띠(검풍·기 모으기 빛 줄 WaveGlow, 반격 칼빛 CounterGlowR)를 보이는 날 구간(0.11 ~ 1.33 × cos 기울기)에(리그는 무기를 바꿀 때만 놓음).</summary>
        void PlaceGlow(float c)
        {
            if (!_glowT) _glowT = _bladeR.transform.Find("WaveGlow");
            if (!_counterT) _counterT = _bladeR.transform.Find("CounterGlowR");
            float from = GreatswordPoses.BladeFrom * c, to = GreatswordPoses.TipAt * c;
            float len = Mathf.Max(0.04f, to - from);
            if (_glowT)
            {
                _glowT.localPosition = new Vector3((from + to) * 0.5f, 0f, _glowT.localPosition.z);
                _glowT.localScale = new Vector3(len, _glowT.localScale.y, 1f);
            }
            if (_counterT)
            {
                _counterT.localPosition = new Vector3((from + to) * 0.5f, 0f, _counterT.localPosition.z);
                _counterT.localScale = new Vector3(len + 0.04f, _counterT.localScale.y, 1f);
            }
        }

        /// <summary>
        /// 칼 그림자: 손 높이 h &gt; 0이면 손 바닥 자리 + 0.4h × 화면 아래에서 칼끝 바닥 자리 + 0.4 × 칼끝 높이 × 화면 아래까지 부드러운 어두운 띠(폭 0.5).
        /// 투명도 0.30(낮을 때) → 0.15(칼끝 높이 1.3), 손 높이 0.15까지는 서서히 나타남. 몸 그림자 바로 위 순서.
        /// </summary>
        void PlaceShadow(Vector2 handR, float angleR) => PlaceShadow(handR,angleR,WeaponStanceLook.Height);

        void PlaceShadow(Vector2 handR,float angleR,float h)
        {
            if (h <= 0.001f)
            {
                HideShadow();
                return;
            }
            if (!_bladeShadow)
            {
                var floor = _rig.Find("Shadow");
                _floorShadow = floor ? floor.GetComponent<SpriteRenderer>() : null;
                _bladeShadow = TopDownView.Part("BladeShadow", _rig, GreatswordTiltArt.ShadowBand, _floorShadow ? _floorShadow.sharedMaterial : null, new Color(0f, 0f, 0f, 0f), _floorShadow);
            }
            float rad = _tilt * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angleR * Mathf.Deg2Rad), Mathf.Sin(angleR * Mathf.Deg2Rad));
            Vector2 tip = handR + dir * (GreatswordPoses.TipAt * Mathf.Cos(rad));
            float tipH = Mathf.Max(0f, h + GreatswordPoses.TipAt * Mathf.Sin(rad));
            Vector3 down3 = _rig.InverseTransformVector(Vector3.down);
            Vector2 down = new Vector2(down3.x, down3.y);
            Vector2 g0 = handR + down * (ShadowDrop * h);
            Vector2 g1 = tip + down * (ShadowDrop * tipH);
            Vector2 d = g1 - g0;
            var t = _bladeShadow.transform;
            t.localPosition = new Vector3((g0.x + g1.x) * 0.5f, (g0.y + g1.y) * 0.5f, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, d.sqrMagnitude > 1e-8f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : angleR);
            t.localScale = new Vector3(d.magnitude + ShadowWidth, ShadowWidth, 1f);
            float a = Mathf.Lerp(ShadowAlphaLow, ShadowAlphaHigh, Mathf.Clamp01((tipH - 0.3f) / 1.0f)) * Mathf.Clamp01(h / 0.15f) * _armR.color.a;
            _bladeShadow.color = new Color(0f, 0f, 0f, a);
            if (_floorShadow)
            {
                _bladeShadow.sortingLayerID = _floorShadow.sortingLayerID;
                _bladeShadow.sortingOrder = _floorShadow.sortingOrder + 1;
            }
            if (!_bladeShadow.enabled) _bladeShadow.enabled = true;
        }

        // ── 세로 내려찍기 바닥 효과(그림만, 판정 무관) ──

        static Sprite[] Slice(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (!tex) return null;
            int n = Mathf.Max(1, tex.width / 512);
            var frames = new Sprite[n];
            for (int i = 0; i < n; i++) frames[i] = Sprite.Create(tex, new Rect(i * 512, 0, 512, Mathf.Min(512, tex.height)), Vector2.one * 0.5f, 512f, 0, SpriteMeshType.FullRect);
            return frames;
        }

        Puff MakePuff(string name, Sprite[] frames, int order)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(_fxRoot.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = ArtRuntime.FlashMaterial;
            sr.sortingOrder = order;
            sr.enabled = false;
            return new Puff { Sr = sr, Frames = frames };
        }

        void EnsureFx()
        {
            if (_fxRoot) return;
            if (!s_fxLoaded)
            {
                s_ring = Slice("OgreVfxV30/slam");
                s_dust = Slice("OgreVfxV30/dust");
                s_fxLoaded = true;
            }
            _fxRoot = new GameObject("대검 바닥 효과(임시)");
            _crack = MakePuff("균열", new[] { GreatswordTiltArt.Crack }, -56);
            _ring = MakePuff("돌 조각 고리", s_ring, -55);
            _dust = MakePuff("먼지", s_dust, -55);
        }

        static void Play(Puff p, Vector2 at, float size, float life, float angle, float alpha)
        {
            if (p == null || p.Frames == null || p.Frames.Length == 0) return;
            p.Age = 0f;
            p.Life = life;
            p.Alpha = alpha;
            var t = p.Sr.transform;
            t.position = new Vector3(at.x, at.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = Vector3.one * size;
            p.Sr.sprite = p.Frames[0];
            p.Sr.color = new Color(1f, 1f, 1f, alpha);
            p.Sr.enabled = true;
        }

        void SpawnImpact(Vector2 handR, float angleR)
        {
            EnsureFx();
            float c = Mathf.Cos(_tilt * Mathf.Deg2Rad);
            Vector2 dir = new Vector2(Mathf.Cos(angleR * Mathf.Deg2Rad), Mathf.Sin(angleR * Mathf.Deg2Rad));
            Vector2 mid = _rig.TransformPoint(handR + dir * (0.72f * c));
            Vector2 tip = _rig.TransformPoint(handR + dir * (GreatswordPoses.TipAt * c));
            float worldAngle = angleR + _rig.eulerAngles.z;
            Play(_crack, mid, CrackSize, CrackLife, worldAngle + 17f * (ImpactsShown % 3), 0.85f);
            Play(_ring, mid, RingSize, RingLife, worldAngle, 0.78f);
            Play(_dust, tip, DustSize, DustLife, worldAngle, 0.78f);
            ScreenShake.Add(ImpactShake, ImpactShakeTime);
            ImpactsShown++;
        }

        void UpdateFx(float dt)
        {
            if (!_fxRoot) return;
            Step(_crack, dt, true);
            Step(_ring, dt, false);
            Step(_dust, dt, false);
        }

        static void Step(Puff p, float dt, bool decal)
        {
            if (p == null || !p.Sr.enabled) return;
            p.Age += dt;
            float u = p.Life > 0f ? p.Age / p.Life : 1f;
            if (u >= 1f)
            {
                p.Sr.enabled = false;
                return;
            }
            if (!decal) p.Sr.sprite = p.Frames[Mathf.Min(p.Frames.Length - 1, Mathf.FloorToInt(u * p.Frames.Length))];
            float a = p.Alpha * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(decal ? 0.5f : 0.48f, 1f, u)));
            p.Sr.color = new Color(1f, 1f, 1f, a);
        }

        /// <summary>두 마디 팔 하나: 위팔은 리그 팔 막대를 고쳐 쓰고(몸 밑), 아래팔은 새 막대(몸 위·투구 밑). 팔꿈치 자리를 돌려준다.</summary>
        Vector2 DrawArm(SpriteRenderer upper, SpriteRenderer fore, Vector2 shoulder, Vector2 hand, Vector2 bodyMid, Vector2 outward, Color color)
        {
            Vector2 d = hand - shoulder;
            float dist = d.magnitude;
            Vector2 elbow = shoulder + d * (UpperArm / (UpperArm + Forearm));
            if (dist > 1e-5f)
            {
                Vector2 n = new Vector2(-d.y / dist, d.x / dist);
                float dot = Vector2.Dot(n, elbow - bodyMid);
                if (Mathf.Abs(dot) < 0.02f) dot = Vector2.Dot(n, outward);
                if (dot < 0f) n = -n;
                float bend = ElbowBendMax * Mathf.Clamp((0.80f - dist) / 0.25f, 0.3f, 1f);
                elbow += n * bend;
            }
            int bodyOrder = upper.sortingOrder + 1;
            Segment(upper, shoulder, elbow, UpperThickness, upper.sortingOrder, 0f);
            if (fore.sprite != upper.sprite) fore.sprite = upper.sprite;
            if (fore.sharedMaterial != upper.sharedMaterial) fore.sharedMaterial = upper.sharedMaterial;
            if (!fore.enabled) fore.enabled = true;
            fore.color = color;
            fore.sortingLayerID = upper.sortingLayerID;
            fore.forceRenderingOff = upper.forceRenderingOff;
            Segment(fore, elbow, hand, ForearmThickness, bodyOrder + 1, ForearmDepth);
            return elbow;
        }

        /// <summary>막대 하나를 두 점 사이에(리그 Segment와 같은 식: 길이 = 거리 + 굵기 ÷ 2). 임시 팔 막대면 (길이, 굵기), 소매 그림이면 그림 길이·굵기에 맞춰 늘림.</summary>
        static void Segment(SpriteRenderer sr, Vector2 a, Vector2 b, float thickness, int order, float depth)
        {
            Vector2 d = b - a;
            var t = sr.transform;
            t.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, depth);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            float length = Mathf.Max(0.02f, d.magnitude + thickness * 0.5f);
            var art = sr.sprite;
            bool limb = !art || art == TopDownSprites.Limb;
            t.localScale = limb
                ? new Vector3(length, thickness, 1f)
                : new Vector3(length / Mathf.Max(0.001f, art.bounds.size.x), thickness / SleeveArtThickness, 1f);
            sr.sortingOrder = order;
        }

        /// <summary>몸 번쩍임 값(리그가 팔에 넣은 것)을 아래팔에도.</summary>
        void CopyFlash(SpriteRenderer from, SpriteRenderer to)
        {
            if (!from || !to) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            from.GetPropertyBlock(_block);
            to.SetPropertyBlock(_block);
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (ReferenceEquals(s_checked, _p)) s_checked = null;
            if (_fxRoot) Destroy(_fxRoot);
        }
    }
}
