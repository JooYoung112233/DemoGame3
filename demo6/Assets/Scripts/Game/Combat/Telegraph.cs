using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 3-6 공격 예고. 바닥의 빨간 반투명 도형이 안쪽부터 차오르고, 다 차는 순간 판정한다.
    /// 게임 시간으로 흐르므로 히트스톱 동안 멈춘다.
    /// </summary>
    public sealed class Telegraph : MonoBehaviour
    {
        const int OutlineOrder = -60;
        const int FillOrder = -59;

        enum Shape
        {
            Circle,
            Rect,
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

        /// <summary>예고가 시작될 때 플레이어가 범위 안에 있었고 구르기를 쓸 수 있었는가(기획 12장 합격 기준 계측).</summary>
        public bool Avoidable { get; set; }

        public float Progress => _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        public bool Done => _elapsed >= _duration;
        public Vector2 Origin => _origin;
        public Vector2 Direction => _dir;

        public static Telegraph Circle(Vector2 center, float radius, float duration)
        {
            var t = Create("Telegraph(Circle)", ShapeSprites.Circle, ShapeSprites.Circle);
            t._shape = Shape.Circle;
            t._origin = center;
            t._radius = radius;
            t._duration = duration;
            t.Refresh();
            return t;
        }

        /// <summary>origin에서 dir 방향으로 length만큼 뻗는 띠.</summary>
        public static Telegraph Rect(Vector2 origin, Vector2 dir, float length, float width, float duration)
        {
            var t = Create("Telegraph(Rect)", ShapeSprites.Square, ShapeSprites.Square);
            t._shape = Shape.Rect;
            t._origin = origin;
            t._dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            t._length = length;
            t._width = width;
            t._duration = duration;
            t.Refresh();
            return t;
        }

        static Telegraph Create(string name, Sprite outline, Sprite fill)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<Telegraph>();
            t._outline = NewChild(go.transform, "Outline", outline, Palette.TelegraphOutline, OutlineOrder);
            t._fill = NewChild(go.transform, "Fill", fill, Palette.TelegraphFill, FillOrder);
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
            // 3차 초안 2-4: 빨간 예고는 어둠 위에서도 100% 밝기.
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

        /// <summary>궁수 조준선처럼 마지막 구간에 고정하고 진하게 보인다.</summary>
        public void Lock()
        {
            _locked = true;
            if (_fill) _fill.color = Palette.TelegraphLocked;
        }

        public bool Contains(Vector2 point, float pad)
        {
            if (_shape == Shape.Circle) return (point - _origin).sqrMagnitude <= (_radius + pad) * (_radius + pad);
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
            if (_fill) _fill.color = new Color(1f, 0.3f, 0.3f, 0.6f);
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

        void Refresh()
        {
            float p = Progress;
            if (_shape == Shape.Circle)
            {
                transform.position = _origin;
                transform.rotation = Quaternion.identity;
                _outline.transform.localScale = Vector3.one * (_radius * 2f);
                _fill.transform.localScale = Vector3.one * (_radius * 2f * Mathf.Max(0.02f, p));
                return;
            }
            float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            transform.position = _origin;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _outline.transform.localPosition = new Vector3(_length * 0.5f, 0f, 0f);
            _outline.transform.localScale = new Vector3(_length, _width, 1f);
            float filled = _length * Mathf.Max(0.02f, p);
            _fill.transform.localPosition = new Vector3(filled * 0.5f, 0f, 0f);
            _fill.transform.localScale = new Vector3(filled, _width, 1f);
        }
    }
}
