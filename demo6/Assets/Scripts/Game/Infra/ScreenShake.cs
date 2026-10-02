using UnityEngine;

namespace Demo6.Game
{
    /// <summary>카메라 흔들림. 실제 시간으로 흐른다.</summary>
    public sealed class ScreenShake : MonoBehaviour
    {
        static ScreenShake _instance;
        Vector3 _base;
        float _strength;
        float _duration;
        float _remaining;

        public static void Add(float strength, float duration)
        {
            if (!_instance) return;
            strength *= Tuning.ShakeScale;
            if (strength <= 0f || duration <= 0f) return;
            if (strength >= _instance.Current)
            {
                _instance._strength = strength;
                _instance._duration = duration;
                _instance._remaining = duration;
            }
        }

        public void SetBase(Vector3 position)
        {
            _base = position;
            transform.position = position;
        }

        float Current => _remaining > 0f ? _strength * (_remaining / _duration) : 0f;

        void Awake()
        {
            _instance = this;
            _base = transform.position;
        }

        void LateUpdate()
        {
            _remaining -= Time.unscaledDeltaTime;
            var offset = _remaining > 0f ? (Vector3)(Random.insideUnitCircle * Current) : Vector3.zero;
            transform.position = _base + offset;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
