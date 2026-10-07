using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 예고 시작 소리 종류(3차 예고 규칙 '한 방 10% 넘으면 소리 필수', TelegraphRule.NeedsSound, 검토 1차 Q6).
    /// 새 소리 파일 없이 있는 합성음(Sfx)을 음높이·음량만 바꿔 다시 쓴다. 묶음 6 '소리 2개'(궁수 시위 당김, 멧돼지 콧김)가 들어오면 Telegraph.PlayStartCue만 바꾼다.
    /// </summary>
    public enum TelegraphCue
    {
        /// <summary>멧돼지 돌진·다시 조준: 짧고 높은 콧김(돌진 그르렁을 높여 짧게).</summary>
        BoarCharge,
        /// <summary>멧돼지 머리치기·뒷발: 더 높고 짧은 끙(돌진 콧김과 구별).</summary>
        BoarMelee,
        /// <summary>궁수 꿰뚫는 화살 조준 시작: 낮게 우는 시위(쏘는 소리를 낮춰 길게).</summary>
        ArcherDraw,
        /// <summary>가시 덫이 놓임: 단단한 '탁'(껍질 소리를 낮춤). 지금 수치(1~10층 4.5~7.5%)로는 나지 않는다.</summary>
        Trap,
    }

    /// <summary>
    /// 기획 3-6 공격 예고. 바닥의 빨간 반투명 도형이 안쪽부터 차오르고, 다 차는 순간 판정한다.
    /// 게임 시간으로 흐르므로 히트스톱 동안 멈춘다. 모양은 원·띠·앞 반원(보스 D 휩쓸기, 기획/전투-보스-무기-다듬기-1차.md 3-3).
    /// 던전 보스방(AboveDark)에서는 테두리와 차오름 끝 선을 시야 덮개·기억 안개 위에 그려 어둠 속에서도 100% 밝기로 보인다(3-3, 7장 #13).
    /// 예고마다 주인 적을 기억한다(적이 Think 하는 동안 만든 예고, CreatingOwner). 주인 적이 안 보이면 그리지 않는다(시야와 문 1차 4-5):
    /// 채움·테두리·안쪽 테두리·끝 선을 모두 끄고, 주인이 보이게 되면(돌아봄·맞음) 그 순간부터 남은 예고를 그린다. 판정 번쩍임도 같은 규칙.
    /// 주인 없는 예고(Think 밖에서 만든 것, 낙석 등)와 보스 예고는 늘 그린다. 그래서 3차 공정 규칙 '빨간 예고는 어둠 위에 100% 밝기'는
    /// '보이는 적의 예고는'으로 좁힌다. 안 보이는 적의 예고는 예고 시작 소리(Q6, 그대로)와 등 뒤 소리의 예고 기척(BehindSounds, Started)으로 알린다.
    /// </summary>
    public sealed class Telegraph : MonoBehaviour
    {
        const int OutlineOrder = -60;
        const int FillOrder = -59;
        /// <summary>어둠 위 테두리·끝 선: 시야 덮개(5000)·기억 안개(14000) 위, 눈 두 점(20000) 아래.</summary>
        // Floor warnings must remain below actor feet, bodies and heads.
        const int AboveDarkOrder = -60;
        /// <summary>띠 예고의 끝 선 두께(유닛).</summary>
        const float EdgeThick = 0.055f;
        /// <summary>원·반원 끝 고리의 두께(반지름 비율). 반경 3이면 약 0.2.</summary>
        const float EdgeBand = 0.07f;

        // 어둠 위 끝 선·테두리 색(Palette의 예고 빨강 계열, 이 파일에서만 씀).
        static readonly Color EdgeColor = new Color(1f, 0.3f, 0.3f, 0.85f);
        static readonly Color EdgeLocked = new Color(1f, 0.16f, 0.16f, 1f);
        static readonly Color OutlineLocked = new Color(.70f, .04f, .05f, 1f);
        static readonly Color EdgeFlash = new Color(1f, 0.4f, 0.4f, 1f);
        static readonly Color OutlineFlash = new Color(1f, .40f, .34f, 1f);
        static readonly Color RimColor = new Color(.48f, .018f, .026f, 1f);
        static readonly Color FillColor = new Color(.62f, .028f, .035f, .90f);
        static Sprite Mask(string shape, string part) => Resources.Load<Sprite>("TelegraphV31/" + shape + "-" + part);

        /// <summary>
        /// 켜진 동안 새로 만든 예고는 테두리(Outline)와 차오름 끝 선(Edge: 원·반원은 고리, 띠는 가로 막대)을 어둠 위(AboveDarkOrder)에 그린다.
        /// 채움은 바닥 차례 그대로라 플레이어 몸을 가리지 않고, 판정 시점(끝 선이 테두리에 닿는 순간)·마지막 돌진의 진한 빨강(Lock)·판정 번쩍임은
        /// 끝 선과 테두리가 어둠 위에서 함께 보여 준다(기둥 그림자·시야 밖에서도).
        /// 보스방 문이 막힌 동안 BossArena가 켜고, 처치·다시 도전·장면 바꿈(SceneStatics)에서 끈다. 전투 시험장은 늘 꺼짐.
        /// </summary>
        public static bool AboveDark;

        /// <summary>
        /// 지금 생각 중인 적(시야와 문 1차 4-5). Enemy.Update가 Think 바로 앞에서 자기로 정하고 뒤에서(finally) 비운다.
        /// 이 값이 있는 동안 만든 예고는 그 적이 주인이다(궁수가 놓는 덫의 예고도 궁수가 주인). 생각 밖에서 만든 예고는 주인이 없다.
        /// </summary>
        public static Enemy CreatingOwner;

        /// <summary>예고가 시작됐다(모양·자리를 정한 직후 한 번). 등 뒤 소리(BehindSounds)의 예고 기척이 듣는다(시야와 문 1차 4-5·4-6).</summary>
        public static event System.Action<Telegraph> Started;

        enum Shape
        {
            Circle,
            Rect,
            /// <summary>중심에서 방향 쪽 앞 반원(180°). 판정은 SectorMath.InHalfDisc.</summary>
            HalfDisc,
        }

        /// <summary>앞 반원 그림(지름 1유닛, 중심 기준, +x 쪽 반쪽). ShapeSprites를 고치지 않으려고 여기서 한 번 만들어 둔다.</summary>
        static Sprite _halfDisc;
        /// <summary>끝 고리 그림(지름 1유닛, 얇은 띠): 원 전체 / +x 쪽 반쪽.</summary>
        static Sprite _edgeRing;
        static Sprite _edgeHalfRing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTelegraphStatics()
        {
            _halfDisc = null;
            _edgeRing = null;
            _edgeHalfRing = null;
            AboveDark = false;
            // 시야와 문 1차 4-5: 장면을 넘어 남지 않게(SceneStatics는 AboveDark만 비운다). 구독은 BehindSounds가 OnDestroy에서 푼다.
            CreatingOwner = null;
            Started = null;
        }

        Shape _shape;
        Vector2 _origin;
        Vector2 _dir = Vector2.right;
        float _length;
        float _width;
        float _radius;
        float _duration;
        float _elapsed;
        bool _locked;
        bool _driven;
        float _resolveFlash = -1f;
        SpriteRenderer _outline;
        SpriteRenderer _fill;
        SpriteRenderer _rim; // v031 thin inner rim, independent of the moving progress edge
        /// <summary>어둠 위 차오름 끝 선(AboveDark일 때만, 아니면 null).</summary>
        SpriteRenderer _edge;

        /// <summary>예고가 시작될 때 플레이어가 범위 안에 있었고 구르기를 쓸 수 있었는가(기획 12장 합격 기준 계측).</summary>
        public bool Avoidable { get; set; }

        public float Progress => _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        public bool Done => _elapsed >= _duration;
        public Vector2 Origin => _origin;
        public Vector2 Direction => _dir;
        /// <summary>이 예고를 만든 적(Think 안에서 만들었을 때, 시야와 문 1차 4-5). 생각 밖에서 만들었으면 null.</summary>
        public Enemy Owner { get; private set; }
        /// <summary>주인 적이 안 보여 지금 그리지 않는가(시야와 문 1차 4-5). 보스·주인 없음·시야 꺼짐이면 늘 거짓.</summary>
        public bool HiddenByVision { get; private set; }

        /// <summary>
        /// 예고 시작 소리(TelegraphCue). 부르는 쪽이 3차 규칙으로 '소리 필수'인 공격(TelegraphRule.NeedsSound, 한 방이 그 층 기준 체력 10% 넘음)일 때만 부른다.
        /// 있는 합성음 재사용: 음량·음높이 배율만 준다(Sfx.PlayScaled, 같은 프레임 같은 종류 한 번 규칙 그대로).
        /// </summary>
        public static void PlayStartCue(TelegraphCue cue)
        {
            switch (cue)
            {
                case TelegraphCue.BoarCharge: Sfx.PlayScaled(SfxKind.BoarCharge, 0.7f, 1.4f); break;
                case TelegraphCue.BoarMelee: Sfx.PlayScaled(SfxKind.BoarCharge, 0.6f, 1.75f); break;
                case TelegraphCue.ArcherDraw: Sfx.PlayScaled(SfxKind.ArcherShot, 0.8f, 0.7f); break;
                case TelegraphCue.Trap: Sfx.PlayScaled(SfxKind.Shell, 0.8f, 0.8f); break;
            }
        }

        public static Telegraph Circle(Vector2 center, float radius, float duration)
        {
            var t = Create("Telegraph(Circle)", Mask("circle", "outline") ?? ShapeSprites.Circle, Mask("circle", "fill") ?? ShapeSprites.Circle, Mask("circle", "edge") ?? EdgeRingSprite(false), Mask("circle", "rim"));
            t._shape = Shape.Circle;
            t._origin = center;
            t._radius = radius;
            t._duration = duration;
            t.Refresh();
            return Begin(t);
        }

        /// <summary>
        /// center에서 dir 쪽 앞 반원(반경 radius, 반각 90°). 보스 D 휩쓸기. 채움은 원처럼 가운데부터 차오른다.
        /// 위치·방향은 SetRect(중심, 방향)로 옮긴다. 빛을 받지 않는 재질, 빨강 100%(다른 예고와 같은 색).
        /// </summary>
        public static Telegraph HalfDisc(Vector2 center, Vector2 dir, float radius, float duration)
        {
            var sprite = HalfDiscSprite();
            var t = Create("Telegraph(HalfDisc)", Mask("half", "outline") ?? sprite, Mask("half", "fill") ?? sprite, Mask("half", "edge") ?? EdgeRingSprite(true), Mask("half", "rim"));
            t._shape = Shape.HalfDisc;
            t._origin = center;
            t._dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            t._radius = radius;
            t._duration = duration;
            t.Refresh();
            return Begin(t);
        }

        /// <summary>origin에서 dir 방향으로 length만큼 뻗는 띠.</summary>
        public static Telegraph Rect(Vector2 origin, Vector2 dir, float length, float width, float duration)
        {
            var t = Create("Telegraph(Rect)", Mask("rect", "outline") ?? ShapeSprites.Square, Mask("rect", "fill") ?? ShapeSprites.Square, ShapeSprites.Square, Mask("rect", "rim"));
            t._shape = Shape.Rect;
            if (t._outline.sprite.border.x > 0) t._outline.drawMode = SpriteDrawMode.Sliced;
            if (t._fill.sprite.border.x > 0) t._fill.drawMode = SpriteDrawMode.Sliced;
            if (t._rim && t._rim.sprite.border.x > 0) t._rim.drawMode = SpriteDrawMode.Sliced;
            t._origin = origin;
            t._dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            t._length = length;
            t._width = width;
            t._duration = duration;
            t.Refresh();
            return Begin(t);
        }

        /// <param name="edge">어둠 위 끝 선 그림(AboveDark일 때만 넘김, null이면 끝 선 없음).</param>
        static Telegraph Create(string name, Sprite outline, Sprite fill, Sprite edge, Sprite rim)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Telegraph>();
            // 시야와 문 1차 4-5: 적이 생각하는 동안 만든 예고는 그 적이 주인.
            t.Owner = CreatingOwner;
            t._outline = NewChild(go.transform, "Outline", outline, RimColor, AboveDark ? AboveDarkOrder : OutlineOrder);
            t._fill = NewChild(go.transform, "Fill", fill, FillColor, FillOrder);
            if (rim) t._rim = NewChild(go.transform, "Inner rim", rim, Color.white, AboveDark ? AboveDarkOrder + 1 : FillOrder + 1);
            if (edge) t._edge = NewChild(go.transform, "Edge", edge, EdgeColor, AboveDark ? AboveDarkOrder + 1 : FillOrder + 1);
            return t;
        }

        /// <summary>
        /// 모양·자리를 정한 뒤: 주인이 안 보이면 처음부터 끄고(지난 프레임 시야 값, 같은 프레임 LateUpdate가 다시 맞춤) 시작 알림을 낸다.
        /// 듣는 쪽 오류가 적의 생각(Think)을 끊어 예고가 주인 없이 남지 않게 여기서 받는다.
        /// </summary>
        static Telegraph Begin(Telegraph t)
        {
            t.ApplyVision();
            var started = Started;
            if (started != null)
            {
                try { started(t); }
                catch (System.Exception ex) { Debug.LogException(ex); }
            }
            return t;
        }

        static SpriteRenderer NewChild(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            // 3차 초안 2-4: 빨간 예고는 어둠 위에서도 100% 밝기. 시야와 문 1차 4-5부터는 '보이는 적의 예고는'(주인이 안 보이면 ApplyVision이 끈다).
            RenderMaterials.MakeUnlit(sr);
            return sr;
        }

        public void SetRect(Vector2 origin, Vector2 dir)
        {
            if (_locked) return;
            _origin = origin;
            if (dir.sqrMagnitude > 0.0001f) _dir = dir.normalized;
            Refresh();
        }

        public void SetCenter(Vector2 center)
        {
            if (_locked) return;
            _origin = center;
            Refresh();
        }

        /// <summary>
        /// 브레인이 진행 시간을 직접 넣는다(스스로 차오르지 않음). 경직 중 준비가 멈추는 적(3차 궁수)은 예고도 같이 멈춰야
        /// '다 차는 순간 = 판정'이 지켜진다.
        /// </summary>
        public void Drive(float elapsed)
        {
            _driven = true;
            _elapsed = Mathf.Clamp(elapsed, 0f, _duration);
            Refresh();
        }

        /// <summary>궁수 조준선처럼 마지막 구간에 고정하고 진하게 보인다. 어둠 위 예고면 테두리·끝 선도 진한 빨강으로(B 마지막 돌진 알림이 어둠에 묻히지 않게).</summary>
        public void Lock()
        {
            _locked = true;
            if (_fill) _fill.color = new Color(1f, .12f, .16f, 1f);
            if (_edge) _edge.color = EdgeLocked;
            if (_outline) _outline.color = OutlineLocked;
        }

        public bool Contains(Vector2 point, float pad)
        {
            if (_shape == Shape.Circle) return (point - _origin).sqrMagnitude <= (_radius + pad) * (_radius + pad);
            if (_shape == Shape.HalfDisc) return SectorMath.InHalfDisc(_origin.x, _origin.y, _dir.x, _dir.y, _radius, point.x, point.y, pad);
            Vector2 local = point - _origin;
            float along = Vector2.Dot(local, _dir);
            float side = Mathf.Abs(Vector2.Dot(local, new Vector2(-_dir.y, _dir.x)));
            return along >= -pad && along <= _length + pad && side <= _width * 0.5f + pad;
        }

        /// <summary>판정 순간: 잠깐 진하게 보였다가 사라진다.</summary>
        public void Resolve()
        {
            if (_resolveFlash >= 0f) return;
            _elapsed = _duration;
            _resolveFlash = 0.08f;
            if (_fill) _fill.color = new Color(1f, .40f, .34f, 1f);
            if (_edge) _edge.color = EdgeFlash;
            if (_outline) _outline.color = OutlineFlash;
            Refresh();
        }

        public void Cancel()
        {
            if (this) Destroy(gameObject);
        }

        void Update()
        {
            if (_resolveFlash >= 0f)
            {
                _resolveFlash -= Time.unscaledDeltaTime;
                if (_resolveFlash < 0f) Destroy(gameObject);
                return;
            }
            if (_driven) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _elapsed = Mathf.Min(_duration, _elapsed + dt);
            Refresh();
        }

        /// <summary>
        /// 시야와 문 1차 4-5: 주인 적이 안 보이면 그리지 않는다. VisionSystem이 먼저(차례 −40) LateUpdate에서 적의 VisionInSight를 정하므로
        /// 같은 프레임에 맞춰 한 프레임 번쩍임이 없다. 판정 번쩍임(Resolve) 동안에도 같은 규칙.
        /// </summary>
        void LateUpdate() => ApplyVision();

        /// <summary>주인(보스 아님, 살아 있음)이 시야를 켠 던전에서 안 보이면 네 렌더러를 끈다. 바뀌었을 때만 맞춘다.</summary>
        void ApplyVision()
        {
            var owner = Owner;
            var vision = VisionSystem.Instance;
            bool hide = owner && !owner.IsBoss && !owner.Dead && vision && vision.VisionOn && !owner.VisionInSight;
            if (hide == HiddenByVision) return;
            HiddenByVision = hide;
            SetRenderingOff(_outline, hide);
            SetRenderingOff(_fill, hide);
            SetRenderingOff(_rim, hide);
            SetRenderingOff(_edge, hide);
        }

        static void SetRenderingOff(SpriteRenderer sr, bool off)
        {
            if (sr && sr.forceRenderingOff != off) sr.forceRenderingOff = off;
        }

        void Refresh()
        {
            float p = Progress;
            RefreshColors(p);
            if (_shape == Shape.Circle)
            {
                transform.position = _origin;
                transform.rotation = Quaternion.identity;
                _outline.transform.localScale = Vector3.one * (_radius * 2f);
                if (_rim) _rim.transform.localScale = _outline.transform.localScale;
                _fill.transform.localScale = Vector3.one * (_radius * 2f * Mathf.Max(0.02f, p));
                // 끝 고리는 채움 가장자리를 따라간다(다 차면 테두리와 겹침 = 판정).
                if (_edge) _edge.transform.localScale = _fill.transform.localScale;
                return;
            }
            if (_shape == Shape.HalfDisc)
            {
                transform.position = _origin;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
                _outline.transform.localScale = Vector3.one * (_radius * 2f);
                if (_rim) _rim.transform.localScale = _outline.transform.localScale;
                _fill.transform.localScale = Vector3.one * (_radius * 2f * Mathf.Max(0.02f, p));
                if (_edge) _edge.transform.localScale = _fill.transform.localScale;
                return;
            }
            float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            transform.position = _origin;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _outline.transform.localPosition = new Vector3(_length * 0.5f, 0f, 0f);
            SizeRect(_outline, _length, _width);
            if (_rim) { _rim.transform.localPosition = _outline.transform.localPosition; SizeRect(_rim, _length, _width); }
            float filled = _length * Mathf.Max(0.02f, p);
            _fill.transform.localPosition = new Vector3(filled * 0.5f, 0f, 0f);
            SizeRect(_fill, filled, _width);
            // 띠의 끝 선: 채움 앞끝에 가로로 선 막대(띠 밖으로 나가지 않게 안쪽에 붙임).
            if (_edge)
            {
                _edge.transform.localPosition = new Vector3(filled - Mathf.Min(EdgeThick, filled) * 0.5f, 0f, 0f);
                _edge.transform.localScale = new Vector3(Mathf.Min(EdgeThick, filled), _width, 1f);
            }
        }

        /// <summary>
        /// 앞 반원 그림: 128×128, 유닛당 128(지름 1유닛), 가운데 피벗. +x 쪽 반쪽만 칠하고 둥근 가장자리와 곧은 가장자리 모두 1픽셀 안에서 부드럽게 끊는다.
        /// </summary>
        void RefreshColors(float progress)
        {
            bool flash = _resolveFlash >= 0f;
            float imminent = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.84f, 1f, progress));
            if (_outline) _outline.color = flash ? OutlineFlash : _locked ? OutlineLocked : Color.Lerp(RimColor, OutlineLocked, imminent * .45f);
            if (_rim) _rim.color = flash ? new Color(1f, .58f, .46f, .92f) : new Color(.92f, .16f, .12f, .48f + .24f * imminent);
            if (_edge) _edge.color = flash ? EdgeFlash : _locked ? EdgeLocked : new Color(.82f, .12f, .10f, .62f + .18f * imminent);
        }

        static void SizeRect(SpriteRenderer sr, float length, float width)
        {
            if (sr.drawMode == SpriteDrawMode.Sliced)
            {
                sr.transform.localScale = Vector3.one;
                sr.size = new Vector2(length, width);
            }
            else sr.transform.localScale = new Vector3(length, width, 1f);
        }

        static Sprite HalfDiscSprite()
        {
            if (_halfDisc) return _halfDisc;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "telegraph_halfdisc" };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half;
                float v = (y + 0.5f - half) / half;
                float round = Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * half);
                float straight = Mathf.Clamp01(u * half + 0.5f);
                byte a = (byte)Mathf.RoundToInt(255f * Mathf.Min(round, straight));
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _halfDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _halfDisc.name = "telegraph_halfdisc";
            return _halfDisc;
        }

        /// <summary>
        /// 끝 고리 그림: 256×256, 유닛당 256(지름 1유닛), 가운데 피벗. 바깥 반지름의 EdgeBand만큼 안쪽까지의 얇은 띠를 1픽셀 안에서 부드럽게 끊는다.
        /// half면 +x 쪽 반쪽만(앞 반원 예고의 둥근 끝).
        /// </summary>
        static Sprite EdgeRingSprite(bool half)
        {
            if (half ? _edgeHalfRing : _edgeRing) return half ? _edgeHalfRing : _edgeRing;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = half ? "telegraph_edge_half" : "telegraph_edge" };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x + 0.5f - r;
                float v = y + 0.5f - r;
                float d = Mathf.Sqrt(u * u + v * v);
                float outer = Mathf.Clamp01(r - d);
                float inner = Mathf.Clamp01(d - r * (1f - EdgeBand));
                float a = Mathf.Min(outer, inner);
                if (half) a = Mathf.Min(a, Mathf.Clamp01(u + 0.5f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = tex.name;
            if (half) _edgeHalfRing = sprite;
            else _edgeRing = sprite;
            return sprite;
        }
    }
}
