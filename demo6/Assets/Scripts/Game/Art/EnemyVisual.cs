using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    public enum EnemyPose
    {
        /// <summary>움직이면 이동, 서 있으면 대기로 고른다.</summary>
        Locomotion,
        Idle,
        Windup,
        Attack,
        Hit,
        Dead,
    }

    /// <summary>
    /// 몬스터 그림 재생. 좌우 2방향(오른쪽 그림을 뒤집음), 그림이 있으면 몸을 돌리지 않는다.
    /// 준비 동작은 준비 시간(굴쥐 0.25초, 멧돼지 0.7·0.4초, 궁수 0.8초)에 맞춰 늘이고 줄인다.
    /// 처치 때 날아가며 도는 움직임은 코드가 하고, 그림은 처치 프레임만 고른다.
    /// </summary>
    public sealed class EnemyVisual : MonoBehaviour
    {
        Enemy _enemy;
        SpriteRenderer _body;
        SpriteFlash _flash;
        Sprite _shapeSprite;
        Material _shapeMaterial;
        bool _artShown;
        float _facingSign = 1f;

        public bool ArtShown => _artShown;

        public void Bind(Enemy enemy, SpriteRenderer body, SpriteFlash flash)
        {
            _enemy = enemy;
            _body = body;
            _flash = flash;
            _shapeSprite = body.sprite;
        }

        void LateUpdate()
        {
            if (!_enemy || !_body) return;
            // 정수리 시점 시험판이 켜져 있으면 도형 상태로 물러나 손을 뗀다(몸은 TopDownView가 이 뒤 LateUpdate에서 그린다).
            if (TopDownView.Active)
            {
                ShowShape();
                return;
            }
            var set = ArtRuntime.Active;
            bool wood = (_enemy is DummyBrain dummy && dummy.IsWood) || _enemy.Kind == MonsterKind.Nest;
            var art = set && !wood ? set.Enemy(_enemy.Kind) : null;
            if (art == null || !art.HasAny)
            {
                ShowShape();
                return;
            }

            Vector2 facing = _enemy.FacingDirection;
            if (Mathf.Abs(facing.x) > 0.15f) _facingSign = Mathf.Sign(facing.x);
            var pose = _enemy.CurrentPose;
            float t = _enemy.PoseTime;
            SpriteClip clip;
            int frame;
            switch (pose)
            {
                case EnemyPose.Dead:
                    clip = Pick(art.death, art.hit, art.idle);
                    frame = ClipTiming.OnceFrame(t, clip.fps, clip.frames.Length);
                    break;
                case EnemyPose.Hit:
                    clip = Pick(art.hit, art.idle, art.move);
                    frame = ClipTiming.OnceFrame(t, clip.fps, clip.frames.Length);
                    break;
                case EnemyPose.Windup:
                    clip = Pick(art.windup, art.attack, art.idle);
                    frame = ClipTiming.TimedFrame(t, _enemy.PoseDuration, _enemy.PoseHitTime, clip.frames.Length, clip.KeyIndex);
                    break;
                case EnemyPose.Attack:
                    clip = Pick(art.attack, art.windup, art.idle);
                    frame = ClipTiming.OnceFrame(t, clip.fps, clip.frames.Length);
                    break;
                case EnemyPose.Idle:
                    clip = Pick(art.idle, art.move, art.hit);
                    frame = ClipTiming.LoopFrame(Time.time, clip.fps, clip.frames.Length);
                    break;
                default:
                    clip = _enemy.IsMoving ? Pick(art.move, art.idle, art.hit) : Pick(art.idle, art.move, art.hit);
                    frame = ClipTiming.LoopFrame(Time.time, clip.fps, clip.frames.Length);
                    break;
            }
            if (clip == null || !clip.Has)
            {
                ShowShape();
                return;
            }

            _body.sprite = ArtRuntime.Frame(clip, frame);
            _body.flipX = _facingSign < 0f;
            if (!_artShown)
            {
                ArtRuntime.UseArtMaterial(_body, _flash, ref _shapeMaterial);
                _artShown = true;
            }
            // 처치 중에는 날림 연출이 크기·회전을 움직이므로 건드리지 않는다.
            if (pose == EnemyPose.Dead) return;
            _body.transform.localScale = Vector3.one * (art.scale * _enemy.SizeScale);
            _body.transform.localPosition = art.offset + _enemy.VisualJitter;
            _body.transform.localRotation = Quaternion.identity;
        }

        static SpriteClip Pick(SpriteClip a, SpriteClip b, SpriteClip c) => a != null && a.Has ? a : b != null && b.Has ? b : c;

        void ShowShape()
        {
            if (!_artShown) return;
            _artShown = false;
            _body.sprite = _shapeSprite;
            _body.flipX = false;
            _body.transform.localPosition = Vector3.zero;
            if (!_enemy.Dead) _body.transform.localScale = Vector3.one * _enemy.ShapeDiameter;
            ArtRuntime.UseShapeMaterial(_body, _flash, _shapeMaterial);
        }
    }
}
