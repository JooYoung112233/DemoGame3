using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 크기 8.0, 실제 화면 중앙을 플레이어가 차지한다. 이동할 때만 짧게 따라온다.
    /// 흔들림은 ScreenShake가 이 위치에 더한다(이 컴포넌트가 먼저 돈다).
    /// 고정 모드(전투·보스 문서 3-7, 묶음 7 오우거 굴): 보스방 문이 막히면 방 가운데·정한 크기로 blend초에 걸쳐 옮겨 멈춘다(실제 시간).
    /// 풀면 같은 방식으로 플레이어를 따라가는 자리·크기 8로 돌아간다. 고정 중에도 흔들림은 그대로 더한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DungeonCamera : MonoBehaviour
    {
        public const float Size = 8f;
        const float Smooth = 12f;

        Camera _cam;
        ScreenShake _shake;
        Transform _target;
        Vector2 _pos;
        float _size = Size;

        // ── 고정 모드 ──
        bool _fixed;
        Vector2 _fixedCenter;
        float _fixedSize = Size;
        /// <summary>고정·풀기 옮김(시작 자리·크기에서 목표까지, 실제 시간).</summary>
        bool _blending;
        float _blendTime;
        float _blendDuration;
        Vector2 _blendFrom;
        float _blendFromSize = Size;

        /// <summary>고정 모드인가(보스방 문이 막힌 동안).</summary>
        public bool IsFixed => _fixed;

        public void Bind(Transform target, Rect bounds)
        {
            _target = target;
            // bounds는 기존 호출 호환용. 맵 가장자리에서도 플레이어 중심을 유지한다.
            _cam = GetComponent<Camera>();
            _shake = GetComponent<ScreenShake>();
            if (!_shake) _shake = gameObject.AddComponent<ScreenShake>();
            _cam.orthographic = true;
            _cam.orthographicSize = Size;
            _cam.rect = new Rect(0f, 0f, 1f, 1f);
            _pos = target ? (Vector2)target.position : Vector2.zero;
            _size = Size;
            _fixed = false;
            _blending = false;
            Apply();
        }

        /// <summary>
        /// 고정 모드: center를 보고 크기 size로 blendSeconds(실제 시간)에 걸쳐 옮긴 뒤 멈춘다. 0이면 바로.
        /// 플레이어를 따라가지 않는다(앞보기 없음). 크기가 0 이하면 기본 크기 8.
        /// </summary>
        public void SetFixed(Vector2 center, float size, float blendSeconds)
        {
            _fixed = true;
            _fixedCenter = center;
            _fixedSize = size > 0.01f ? size : Size;
            StartBlend(blendSeconds);
        }

        /// <summary>고정 모드를 푼다: blendSeconds(실제 시간)에 걸쳐 플레이어 자리·크기 8로 돌아간다. 0이면 다음 프레임부터 바로 따라간다(Snap과 함께 쓴다).</summary>
        public void ClearFixed(float blendSeconds)
        {
            if (!_fixed) return;
            _fixed = false;
            StartBlend(blendSeconds);
        }

        void StartBlend(float seconds)
        {
            _blendFrom = _pos;
            _blendFromSize = _cam ? _cam.orthographicSize : _size;
            _blendDuration = Mathf.Max(0f, seconds);
            _blendTime = 0f;
            _blending = _blendDuration > 0f;
        }

        /// <summary>순간 이동(말뚝 이동·다시 서기) 뒤 부드럽게 따라가지 않고 바로 옮긴다. 고정 모드면 고정 자리·크기로 바로 맞춘다.</summary>
        public void Snap()
        {
            _blending = false;
            if (_fixed)
            {
                _pos = _fixedCenter;
                _size = _fixedSize;
            }
            else
            {
                if (!_target) return;
                _pos = _target.position;
                _size = Size;
            }
            if (_cam) _cam.orthographicSize = _size;
            Apply();
        }

        void LateUpdate()
        {
            if (!_cam) return;
            if (!_fixed && !_target) return;
            float dt = Time.unscaledDeltaTime;
            Vector2 want = _fixed ? _fixedCenter : (Vector2)_target.position;
            float wantSize = _fixed ? _fixedSize : Size;
            if (_blending)
            {
                _blendTime += dt;
                float k = Mathf.Clamp01(_blendTime / _blendDuration);
                k = k * k * (3f - 2f * k);
                _pos = Vector2.Lerp(_blendFrom, want, k);
                _size = Mathf.Lerp(_blendFromSize, wantSize, k);
                if (_blendTime >= _blendDuration) _blending = false;
            }
            else if (_fixed)
            {
                _pos = want;
                _size = wantSize;
            }
            else
            {
                float s = 1f - Mathf.Exp(-Smooth * dt);
                _pos = Vector2.Lerp(_pos, want, s);
                _size = Size;
            }
            _cam.orthographicSize = _size;
            Apply();
        }

        void Apply()
        {
            var p = new Vector3(_pos.x, _pos.y, -10f);
            if (_shake) _shake.SetBase(p);
            else transform.position = p;
        }
    }
}
