using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 따라가는 카메라(3차 초안 2-1): 크기 8.0 고정, 지도 끝에서 멈춤, 커서 쪽으로 1.5 앞을 봄.
    /// 흔들림은 ScreenShake가 이 위치에 더한다(이 컴포넌트가 먼저 돈다).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DungeonCamera : MonoBehaviour
    {
        public const float Size = 8f;
        const float LookAhead = 1.5f;
        const float Smooth = 12f;

        Camera _cam;
        ScreenShake _shake;
        Transform _target;
        Rect _bounds;
        Vector2 _look;
        Vector2 _pos;

        public void Bind(Transform target, Rect bounds)
        {
            _target = target;
            _bounds = bounds;
            _cam = GetComponent<Camera>();
            _shake = GetComponent<ScreenShake>();
            if (!_shake) _shake = gameObject.AddComponent<ScreenShake>();
            _cam.orthographic = true;
            _cam.orthographicSize = Size;
            _cam.rect = new Rect(0f, 0f, 1f, 1f);
            _pos = target ? (Vector2)target.position : Vector2.zero;
            Apply();
        }

        /// <summary>순간 이동(말뚝 이동·다시 서기) 뒤 부드럽게 따라가지 않고 바로 옮긴다.</summary>
        public void Snap()
        {
            if (!_target) return;
            _pos = Clamp(_target.position);
            _look = Vector2.zero;
            Apply();
        }

        void LateUpdate()
        {
            if (!_target || !_cam) return;
            _cam.orthographicSize = Size;
            Vector2 want = _target.position;
            if (!TimeScaleService.Paused)
            {
                Vector3 mouse = UnityEngine.InputSystem.Mouse.current != null ? (Vector3)UnityEngine.InputSystem.Mouse.current.position.ReadValue() : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f);
                mouse.z = -transform.position.z;
                Vector2 cursor = _cam.ScreenToWorldPoint(mouse);
                Vector2 dir = cursor - want;
                Vector2 lookWant = dir.sqrMagnitude > 0.01f ? dir.normalized * Mathf.Min(LookAhead, dir.magnitude * 0.25f) : Vector2.zero;
                float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
                _look = Vector2.Lerp(_look, lookWant, k);
            }
            float s = 1f - Mathf.Exp(-Smooth * Time.unscaledDeltaTime);
            _pos = Vector2.Lerp(_pos, Clamp(want + _look), s);
            Apply();
        }

        Vector2 Clamp(Vector2 p)
        {
            float halfH = Size;
            float halfW = Size * (_cam ? _cam.aspect : 16f / 9f);
            float minX = _bounds.xMin + halfW, maxX = _bounds.xMax - halfW;
            float minY = _bounds.yMin + halfH, maxY = _bounds.yMax - halfH;
            p.x = minX <= maxX ? Mathf.Clamp(p.x, minX, maxX) : _bounds.center.x;
            p.y = minY <= maxY ? Mathf.Clamp(p.y, minY, maxY) : _bounds.center.y;
            return p;
        }

        void Apply()
        {
            var p = new Vector3(_pos.x, _pos.y, -10f);
            if (_shake) _shake.SetBase(p);
            else transform.position = p;
        }
    }
}
