using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 카메라(기획/마을-의뢰-첫판.md 2-2): 크기 8.0, 배경 #15120F. 플레이어를 부드럽게 따라가되 칠한 땅 밖을 보지 않게 중심을 묶는다
    /// (TownLayout.ClampCamera, 16:9면 중심 x 36.2~47.8, y 32~46, 화면이 더 넓으면 그 축은 가운데). 도착·시작 때는 바로 맞춘다(Snap).
    /// 던전 카메라(DungeonCamera)는 다른 작업이 고치는 중이라 쓰지 않는다. 멈춘 동안에도 실제 시간으로 따라가 끊기지 않는다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class TownCamera : MonoBehaviour
    {
        /// <summary>따라가는 날카로움(클수록 바로 붙음). 0.5초면 약 95% 따라붙는다.</summary>
        public const float FollowSharpness = 6f;

        Camera _cam;
        Transform _target;
        Vector2 _center;
        float _z = -10f;

        public Camera Camera => _cam;
        /// <summary>지금 카메라 중심(묶은 뒤).</summary>
        public Vector2 Center => _center;

        public void Bind(Transform target)
        {
            _cam = GetComponent<Camera>();
            if (_cam)
            {
                _cam.orthographic = true;
                _cam.orthographicSize = TownLayout.CameraSize;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Palette.Background;
            }
            _z = transform.position.z < -0.01f ? transform.position.z : -10f;
            _target = target;
            Snap();
        }

        /// <summary>바로 맞춘다(도착·시작·순간 이동).</summary>
        public void Snap()
        {
            if (!_target) return;
            _center = Clamp(_target.position);
            Apply();
        }

        void LateUpdate()
        {
            if (!_target) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _center = Vector2.Lerp(_center, Clamp(_target.position), 1f - Mathf.Exp(-FollowSharpness * dt));
            // 화면 비율이 바뀌어도(창 크기) 묶음을 다시 지킨다.
            _center = Clamp(_center);
            Apply();
        }

        Vector2 Clamp(Vector2 p)
        {
            float aspect = _cam ? _cam.aspect : 16f / 9f;
            var c = TownLayout.ClampCamera(new TownVec(p.x, p.y), aspect);
            return new Vector2(c.X, c.Y);
        }

        void Apply() => transform.position = new Vector3(_center.x, _center.y, _z);
    }
}
