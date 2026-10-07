using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 갱도 오우거 몸 그림(기획/전투-보스-무기-다듬기-1차.md 3-9). TopDownEnemyRig를 고치지 않고 오우거 전용으로 그린다
    /// (TopDownEnemyRig.Create는 모르는 종류에 null을 돌려 손대지 않는다). 첫 판은 코드 도형 임시판: 등이 굽은 큰 원(지름 2.4, Enemy 도형 몸) 위에
    /// 밝은 머리·어깨와 어두운 등, 등에 박힌 곡괭이 자루 셋, 팔 두 토막, 버팀목 몽둥이(길이 2.6, 오른 어깨 피벗에서 OgreBrain.ClubAngle대로 휘두름),
    /// 눈 두 점(1단계 흐린 호박색, 2단계 흰색, 빛 무시), 2단계 빛 무시 회백 테두리(두께 0.06, 맥동 없음 = 정예 흰 맥동과 구별).
    /// 무너짐이면 92%로 줄고 어두워지며 몽둥이를 떨군다. 처치면 몸 묶음을 떼어 내 앞으로 쓰러지고(어둡게, 앞뒤로 길게) 흙먼지 고리를 남긴다(20초 뒤 흐려짐).
    /// 먹는 중이면 머리를 까딱이며 돌 씹는 소리를 낸다(OgreSounds.Chew). 등급색·예고 빨강·밝은 금빛은 쓰지 않는다.
    /// 정렬: 몸 묶음은 YSort 순서보다 300 낮게 두어 플레이어·굴쥐가 늘 위에 그려진다(7장 #13). 빨간 예고(−60)보다는 위다.
    /// 원화(몸 1장 1440×1360·몽둥이 1장 1360×220 원본, 실제 레이어 PSD)는 리소스 단계에서 CombatArtSet 칸으로 잇는다.
    /// </summary>
    public sealed class OgreLook : MonoBehaviour
    {
        /// <summary>오른 어깨(몽둥이 피벗) 자리: 몸 앞 0.15, 오른쪽 0.95. 왼 어깨는 대칭.</summary>
        public const float ShoulderForward = 0.15f;
        public const float ShoulderSide = 0.95f;
        const float ClubLength = 2.6f;
        const float ClubWidth = 0.35f;
        const float ArmLength = 0.85f;
        const float ArmWidth = 0.38f;
        const float FistSize = 0.44f;
        /// <summary>머리는 몸 원 안 앞쪽(앞끝 +0.9, 원화 규격 3-9)에 밝게 둔다.</summary>
        const float HeadForward = 0.5f;
        const float HeadSize = 0.8f;
        const float EyeSize = 0.13f;
        const float EyeForward = 0.28f;
        const float EyeSide = 0.15f;
        const float BrokenScale = 0.92f;
        const float BrokenDim = 0.62f;
        /// <summary>몸 묶음 정렬 바탕(YSort 순서 + 이 값 + 부품 차례).</summary>
        const int OrderBase = -320;
        /// <summary>쓰러진 몸: 벽(−900) 아래, 핏자국·시체(−990) 위.</summary>
        const int CorpseOrder = -985;
        const float FallSeconds = 0.7f;
        const float FallDistance = 0.7f;
        const float CorpseSeconds = 20f;
        const float CorpseFade = 1.5f;
        const float DustSeconds = 0.6f;

        static readonly Color Wood = new Color(0x56 / 255f, 0x40 / 255f, 0x2A / 255f, 1f);
        static readonly Color Iron = new Color(0x54 / 255f, 0x57 / 255f, 0x5E / 255f, 1f);
        static readonly Color Nail = new Color(0x8F / 255f, 0x94 / 255f, 0x9C / 255f, 1f);
        static readonly Color Handle = new Color(0x6B / 255f, 0x52 / 255f, 0x36 / 255f, 1f);
        static readonly Color EyeAmber = new Color(0.72f, 0.55f, 0.24f, 1f);
        static readonly Color EyeWhite = new Color(0.96f, 0.95f, 0.9f, 1f);
        static readonly Color RimColor = new Color(0.86f, 0.85f, 0.81f, 0.9f);
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.32f);
        static readonly Color Dust = new Color(0.55f, 0.5f, 0.43f, 0.5f);

        /// <summary>2단계 회백 테두리(바깥 반지름 1.26, 두께 0.06). ShapeSprites를 고치지 않으려고 여기서 만든다.</summary>
        static Sprite _rimSprite;
        /// <summary>쓰러져 남은 몸(새 오우거가 나오면 치운다).</summary>
        static readonly List<OgreLook> Corpses = new List<OgreLook>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _rimSprite = null;
            Corpses.Clear();
        }

        /// <summary>그리는 오우거(Bind 전이면 null).</summary>
        public OgreBrain Ogre { get; private set; }

        readonly List<SpriteRenderer> _parts = new List<SpriteRenderer>(24);
        readonly List<Color> _colors = new List<Color>(24);
        readonly List<int> _orders = new List<int>(24);
        OgreArtRig _approvedArt;
        SpriteRenderer _body;
        SpriteFlash _flash;
        Color _bodyColor;
        Vector3 _bodyScale;
        SpriteRenderer _shadow;
        SpriteRenderer _rim;
        SpriteRenderer _head;
        SpriteRenderer _eyeL;
        SpriteRenderer _eyeR;
        SpriteRenderer _armL;
        SpriteRenderer _fistL;
        SpriteRenderer _armR;
        SpriteRenderer _fistR;
        SpriteRenderer _club;
        SpriteRenderer _band;
        SpriteRenderer _nailA;
        SpriteRenderer _nailB;
        SpriteRenderer _fallBody;
        SpriteRenderer _dust;
        bool _brokenShown;
        int _phaseShown;
        float _tint = 1f;
        float _chewAt;
        bool _fallen;
        float _fallT;
        Vector3 _fallFrom;
        Vector2 _fallDir = Vector2.right;
        bool _dustDone;

        /// <summary>오우거에 붙인다(OgreBrain.OnSpawned가 부른다). 이 물체는 오우거의 자식이고, 쓰러지면 떼어 내 남는다.</summary>
        public void Bind(OgreBrain ogre)
        {
            Ogre = ogre;
            // 같은 방에 새 판: 앞서 쓰러져 남은 오우거 몸을 치운다.
            for (int i = Corpses.Count - 1; i >= 0; i--)
                if (Corpses[i] && Corpses[i] != this) Destroy(Corpses[i].gameObject);
            Corpses.Clear();
            if (!ogre) return;
            _flash = ogre.GetComponent<SpriteFlash>();
            _body = _flash ? _flash.target : null;
            _bodyColor = _flash ? _flash.baseColor : GoreColors.OgreSkin;
            if (_body)
            {
                _bodyScale = _body.transform.localScale;
                _body.sortingOrder = OrderBase + 10;
            }
            _approvedArt = OgreArtRig.TryCreate(this, ogre, _body, _flash);
            if (_approvedArt)
            {
                _chewAt = Time.time + Random.Range(0.2f, 0.8f);
                return;
            }
            Build();
            _phaseShown = 0;
            var ysort = ogre.GetComponent<YSort>();
            if (ysort) ysort.Refresh();
            _chewAt = Time.time + Random.Range(0.2f, 0.8f);
            LateUpdate();
        }

        void Build()
        {
            var circle = ShapeSprites.Circle;
            var square = ShapeSprites.Square;
            _shadow = Part("Shadow", circle, Shadow, 0);
            _shadow.transform.localScale = Vector3.one * 2.9f;
            _rim = Part("Rim", RimSprite(), RimColor, 11, true);
            _rim.transform.localScale = Vector3.one * 2.52f;
            _rim.enabled = false;
            // 등은 어둡고(굽은 등), 머리·어깨는 밝다: 빛 밖·등잔 빛 모두에서 덩어리로 읽힌다.
            var back = Part("Back", circle, WithAlpha(GoreColors.OgreSkinDark, 0.9f), 12);
            Place(back, new Vector2(-0.38f, 0f), 0f, 1.55f, 2.05f);
            var front = Part("Shoulders", circle, WithAlpha(GoreColors.OgreSkinLight, 0.55f), 13);
            Place(front, new Vector2(0.28f, 0f), 0f, 1.25f, 2.2f);
            // 등에 박힌 부러진 곡괭이 자루 셋(설명 없이 그림으로만).
            AddHandle(156f, 0.95f);
            AddHandle(181f, 0.85f);
            AddHandle(206f, 0.9f);
            _armL = Part("ArmL", square, GoreColors.OgreSkin, 15);
            _fistL = Part("FistL", circle, GoreColors.OgreSkinLight, 16);
            _head = Part("Head", circle, GoreColors.OgreSkinLight, 17);
            _eyeL = Part("EyeL", circle, EyeAmber, 18, true);
            _eyeR = Part("EyeR", circle, EyeAmber, 18, true);
            _armR = Part("ArmR", square, GoreColors.OgreSkin, 19);
            _fistR = Part("FistR", circle, GoreColors.OgreSkinLight, 20);
            _club = Part("Club", square, Wood, 21);
            _band = Part("ClubBand", square, Iron, 22);
            _nailA = Part("NailA", circle, Nail, 23);
            _nailB = Part("NailB", circle, Nail, 23);
        }

        void AddHandle(float angle, float length)
        {
            var sr = Part("PickHandle", ShapeSprites.Square, Handle, 14);
            float a = angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Segment(sr, dir * 0.55f, dir * (0.55f + length), 0.09f);
        }

        SpriteRenderer Part(string name, Sprite sprite, Color color, int order, bool unlit = false)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = OrderBase + order;
            // 눈·2단계 테두리는 빛을 받지 않는다(어둠 위에서도 몸이 읽힘, 3-5).
            if (unlit) RenderMaterials.MakeUnlit(sr);
            _parts.Add(sr);
            _colors.Add(color);
            _orders.Add(order);
            return sr;
        }

        void LateUpdate()
        {
            if (_approvedArt)
            {
                if (!_fallen && Ogre)
                {
                    if (Ogre.Dead) Fall();
                    else Chew(Ogre);
                }
                return;
            }
            if (_fallen)
            {
                TickFall();
                return;
            }
            var o = Ogre;
            if (!o) return;
            if (o.Dead)
            {
                Fall();
                return;
            }
            Vector2 f = o.FacingDirection;
            float ang = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            transform.localPosition = o.VisualJitter;
            // 그림자는 몸 방향과 관계없이 화면 아래쪽으로 조금.
            _shadow.transform.localPosition = Quaternion.Euler(0f, 0f, -ang) * new Vector3(0.05f, -0.14f, 0f);

            bool broken = o.Broken;
            if (broken != _brokenShown) SetBroken(broken);
            if (o.Phase != _phaseShown) SetPhase(o.Phase);
            Pose(o, broken);
            Chew(o);
        }

        /// <summary>무너짐: 몸 92%·어둡게(Enemy 도형 몸은 번쩍임 바탕색을 어둡게). 일어나면 되돌린다.</summary>
        void SetBroken(bool broken)
        {
            _brokenShown = broken;
            transform.localScale = Vector3.one * (broken ? BrokenScale : 1f);
            if (_body) _body.transform.localScale = _bodyScale * (broken ? BrokenScale : 1f);
            if (_flash) _flash.baseColor = broken ? Dim(_bodyColor, BrokenDim) : _bodyColor;
            _tint = broken ? BrokenDim : 1f;
            ApplyTint();
        }

        /// <summary>2단계: 눈이 흰색, 빛 무시 회백 테두리(맥동 없음).</summary>
        void SetPhase(int phase)
        {
            _phaseShown = phase;
            bool two = phase >= 2;
            SetColor(_eyeL, two ? EyeWhite : EyeAmber);
            SetColor(_eyeR, two ? EyeWhite : EyeAmber);
            _rim.enabled = two;
        }

        void Pose(OgreBrain o, bool broken)
        {
            float t = Time.time;
            bool eating = !o.Aware && o.IsEating;
            float lean = o.Lean;
            float headX = HeadForward + lean * 0.15f;
            if (eating) headX += Mathf.Sin(t * Mathf.PI * 2f * 2.6f) * 0.05f;
            if (broken) headX = HeadForward * 0.8f;
            Place(_head, new Vector2(headX, 0f), 0f, HeadSize, HeadSize);
            float eyeScaleY = eating ? EyeSize * 0.55f : EyeSize;
            Place(_eyeL, new Vector2(headX + EyeForward, EyeSide), 0f, EyeSize, eyeScaleY);
            Place(_eyeR, new Vector2(headX + EyeForward, -EyeSide), 0f, EyeSize, eyeScaleY);

            var shoulderR = new Vector2(ShoulderForward, -ShoulderSide);
            var shoulderL = new Vector2(ShoulderForward, ShoulderSide);
            float clubAngle = o.ClubAngle;
            float reach = o.ClubReach;
            float leftAngle = 35f + 60f * o.ArmLift - lean * 20f;
            if (eating)
            {
                // 왼손으로 돌을 입에 가져가고, 버팀목은 오른손에 늘어뜨린다.
                clubAngle = -55f;
                reach = 0.95f;
                leftAngle = 20f + 25f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 1.3f));
            }

            // 왼팔.
            var dirL = Dir(leftAngle);
            Vector2 handL = shoulderL + dirL * ArmLength;
            Segment(_armL, shoulderL, handL, ArmWidth);
            Place(_fistL, handL, 0f, FistSize, FistSize);

            if (broken)
            {
                // 무너짐: 오른팔은 늘어지고 몽둥이는 오른쪽 바닥에 떨어져 누워 있다.
                Vector2 handR = shoulderR + Dir(-80f) * (ArmLength * 0.8f);
                Segment(_armR, shoulderR, handR, ArmWidth);
                Place(_fistR, handR, 0f, FistSize, FistSize);
                Vector2 start = shoulderR + Dir(-70f) * 1.15f;
                PlaceClub(start, Dir(-15f), 1f);
                return;
            }

            // 오른팔은 몽둥이 쪽으로 뻗고(머리 위로 들면 짧게 보임), 몽둥이는 주먹에서 이어진다.
            var dirR = Dir(clubAngle);
            Vector2 hand = shoulderR + dirR * (ArmLength * Mathf.Lerp(0.6f, 1f, reach));
            Segment(_armR, shoulderR, hand, ArmWidth);
            Place(_fistR, hand, 0f, FistSize, FistSize);
            PlaceClub(hand, dirR, reach);
        }

        /// <summary>몽둥이: 쥔 점에서 dir 쪽으로 길이 2.6 × reach. 끝 쪽에 쇠 띠와 박힌 못 둘.</summary>
        void PlaceClub(Vector2 grip, Vector2 dir, float reach)
        {
            float len = ClubLength * Mathf.Clamp(reach, 0.2f, 1f);
            Vector2 tip = grip + dir * len;
            Segment(_club, grip, tip, ClubWidth);
            Segment(_band, grip + dir * (len * 0.78f), grip + dir * (len * 0.88f), ClubWidth * 1.22f);
            var side = new Vector2(-dir.y, dir.x);
            Vector2 nail = grip + dir * (len * 0.94f);
            Place(_nailA, nail + side * 0.12f, 0f, 0.09f, 0.09f);
            Place(_nailB, nail - side * 0.12f, 0f, 0.09f, 0.09f);
        }

        void Chew(OgreBrain o)
        {
            if (o.Aware || !o.IsEating) return;
            if (Time.time < _chewAt) return;
            _chewAt = Time.time + Random.Range(1.1f, 1.6f);
            OgreSounds.Play(OgreSound.Chew, 0.55f);
        }

        /// <summary>
        /// 처치(3-7): 몸 묶음을 오우거에서 떼어 내 앞으로 쓰러뜨린다(0.7초, 앞뒤로 길게·어둡게) → 흙먼지 고리. Enemy 도형 몸은 숨겨지므로 같은 몸을 하나 만든다.
        /// 바닥 층으로 내려 20초 남았다가 흐려져 사라진다(새 오우거가 나오면 바로 치운다).
        /// </summary>
        public void Fall()
        {
            if (_fallen) return;
            if (_approvedArt)
            {
                _fallen = true;
                _approvedArt.BeginFall();
                Corpses.Add(this);
                return;
            }
            _fallen = true;
            var o = Ogre;
            Vector2 f = o ? o.FacingDirection : Vector2.right;
            _fallDir = f.sqrMagnitude > 0.0001f ? f.normalized : Vector2.right;
            _fallBody = Part("FallenBody", ShapeSprites.Circle, _bodyColor, 10);
            _fallBody.transform.localScale = Vector3.one * (_bodyScale.x > 0.01f ? _bodyScale.x : BossRules.Diameter);
            var ysort = o ? o.GetComponent<YSort>() : null;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            if (ysort) ysort.Refresh();
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i]) _parts[i].sortingOrder = CorpseOrder + _orders[i];
            _eyeL.enabled = false;
            _eyeR.enabled = false;
            _rim.enabled = false;
            _shadow.enabled = false;
            // 팔을 앞으로 뻗고 몽둥이는 손에서 빠져 오른쪽 앞에 눕는다.
            var shoulderR = new Vector2(ShoulderForward, -ShoulderSide);
            var shoulderL = new Vector2(ShoulderForward, ShoulderSide);
            Vector2 handL = shoulderL + Dir(15f) * ArmLength;
            Vector2 handR = shoulderR + Dir(-15f) * ArmLength;
            Segment(_armL, shoulderL, handL, ArmWidth);
            Place(_fistL, handL, 0f, FistSize, FistSize);
            Segment(_armR, shoulderR, handR, ArmWidth);
            Place(_fistR, handR, 0f, FistSize, FistSize);
            Place(_head, new Vector2(HeadForward + 0.1f, 0f), 0f, HeadSize, HeadSize * 0.95f);
            PlaceClub(handR + Dir(-60f) * 0.5f, Dir(-35f), 1f);
            _fallFrom = transform.position;
            _fallT = 0f;
            _dustDone = false;
            _tint = 1f;
            Corpses.Add(this);
        }

        void TickFall()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _fallT += dt;
            float k = Mathf.Clamp01(_fallT / FallSeconds);
            float e = k * k;
            transform.position = _fallFrom + (Vector3)(_fallDir * (FallDistance * e));
            // 앞으로 엎어진 몸: 몸 방향(로컬 x)으로 길어지고 옆으로 조금 납작해진다.
            transform.localScale = new Vector3(1f + 0.25f * e, 1f - 0.06f * e, 1f);
            _tint = Mathf.Lerp(1f, 0.5f, e);
            if (k >= 1f && !_dustDone)
            {
                _dustDone = true;
                ScreenShake.Add(0.1f, 0.2f);
                OgreSounds.Play(OgreSound.Slam, 0.7f, 0.7f);
                var go = new GameObject("OgreFallDust");
                go.transform.position = transform.position;
                _dust = go.AddComponent<SpriteRenderer>();
                _dust.sprite = ShapeSprites.Ring;
                _dust.color = Dust;
                _dust.sortingOrder = CorpseOrder + 30;
            }
            if (_dust)
            {
                float d = Mathf.Clamp01((_fallT - FallSeconds) / DustSeconds);
                _dust.transform.localScale = Vector3.one * Mathf.Lerp(2.6f, 6.4f, d);
                var c = Dust;
                c.a *= 1f - d;
                _dust.color = c;
                if (d >= 1f)
                {
                    Destroy(_dust.gameObject);
                    _dust = null;
                }
            }
            float age = _fallT - FallSeconds;
            float alpha = age <= CorpseSeconds ? 1f : 1f - (age - CorpseSeconds) / CorpseFade;
            if (alpha <= 0f)
            {
                Corpses.Remove(this);
                Destroy(gameObject);
                return;
            }
            ApplyTint(alpha);
        }

        void OnDestroy()
        {
            // 흙먼지 고리는 몸 묶음과 따로 둔다(몸이 앞뒤로 늘어나도 둥글게). 몸이 먼저 지워지면 함께 지운다.
            if (_dust) Destroy(_dust.gameObject);
            Corpses.Remove(this);
        }

        void ApplyTint(float alpha = 1f)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                var sr = _parts[i];
                if (!sr) continue;
                var c = _colors[i];
                // 눈·테두리(빛 무시)는 무너짐에 어두워지지 않는다.
                bool keep = sr == _eyeL || sr == _eyeR || sr == _rim || sr == _shadow;
                float k = keep ? 1f : _tint;
                sr.color = new Color(c.r * k, c.g * k, c.b * k, c.a * alpha);
            }
        }

        void SetColor(SpriteRenderer sr, Color c)
        {
            int i = _parts.IndexOf(sr);
            if (i >= 0) _colors[i] = c;
            sr.color = c;
        }

        static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        static void Place(SpriteRenderer sr, Vector2 pos, float angle, float sx, float sy)
        {
            var tr = sr.transform;
            tr.localPosition = pos;
            tr.localRotation = Quaternion.Euler(0f, 0f, angle);
            tr.localScale = new Vector3(sx, sy, 1f);
        }

        /// <summary>네모(1×1) 하나로 a에서 b까지 두께 width인 토막을 그린다.</summary>
        static void Segment(SpriteRenderer sr, Vector2 a, Vector2 b, float width)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            Place(sr, (a + b) * 0.5f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, Mathf.Max(0.01f, len), width);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>회백 테두리: 256×256, 유닛당 256(지름 1유닛). 바깥 반지름 1, 안 반지름 1.2 ÷ 1.26(몸 둘레에 두께 0.06).</summary>
        static Sprite RimSprite()
        {
            if (_rimSprite) return _rimSprite;
            const int size = 256;
            const float inner = 1.2f / 1.26f;
            float half = size * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "ogre_rim" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half;
                float v = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Min(Mathf.Clamp01((1f - d) * half), Mathf.Clamp01((d - inner) * half));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _rimSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _rimSprite.name = "ogre_rim";
            return _rimSprite;
        }
    }
}
