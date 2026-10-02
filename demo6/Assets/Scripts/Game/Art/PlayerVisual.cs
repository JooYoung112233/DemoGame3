using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 검사 그림 재생. 몸은 좌우 2방향(오른쪽 그림을 왼쪽은 뒤집음). 공격은 콤보 단계 그림을 동작 길이에 맞추고,
    /// 타격 프레임이 판정 순간에 시작한다. 그림 칸이 비어 있으면 도형(원 + 방향 삼각형)을 그대로 쓴다.
    /// </summary>
    public sealed class PlayerVisual : MonoBehaviour
    {
        PlayerController _player;
        SpriteRenderer _body;
        SpriteFlash _flash;
        Transform _mark;
        Sprite _shapeSprite;
        Material _shapeMaterial;
        bool _artShown;
        float _facingSign = 1f;

        /// <summary>시험 패널 표시용: 지금 몸 프레임, 전체 프레임, 타격 프레임, 클립 이름.</summary>
        public int DebugFrame { get; private set; } = -1;
        public int DebugFrameCount { get; private set; }
        public int DebugKeyFrame { get; private set; } = -1;
        public string DebugClip { get; private set; } = "";

        public void Bind(PlayerController player, SpriteRenderer body, SpriteFlash flash, Transform facingMark)
        {
            _player = player;
            _body = body;
            _flash = flash;
            _mark = facingMark;
            _shapeSprite = body.sprite;
        }

        void LateUpdate()
        {
            if (!_player || !_body) return;
            var set = ArtRuntime.Active;
            var art = set ? set.player : null;
            Vector2 facing = _player.FacingDirection;
            if (Mathf.Abs(facing.x) > 0.15f) _facingSign = Mathf.Sign(facing.x);

            SpriteClip clip = null;
            string name = "";
            float t = _player.PoseTime;
            float duration = _player.PoseDuration;
            float hit = _player.PoseHitTime;
            bool timed = false;
            bool once = false;
            if (art != null)
            {
                switch (_player.Pose)
                {
                    case PlayerPose.Attack:
                        var step = set.Weapon(_player.Weapon.id)?.Step(_player.ComboIndex);
                        clip = step?.body;
                        name = $"{_player.Weapon.displayName} {_player.ComboStepName}";
                        timed = true;
                        break;
                    case PlayerPose.Dodge:
                        clip = art.dodge;
                        name = "구르기";
                        timed = true;
                        break;
                    case PlayerPose.Whirl:
                        clip = art.whirl;
                        name = "회오리 베기";
                        timed = true;
                        break;
                    case PlayerPose.WaveCast:
                        clip = art.waveCast;
                        name = "검풍 시전";
                        timed = true;
                        break;
                    case PlayerPose.Hurt:
                        clip = art.hurt.Has ? art.hurt : art.idle;
                        name = "피격";
                        once = true;
                        break;
                    case PlayerPose.Down:
                        clip = art.down;
                        name = "쓰러짐";
                        once = true;
                        break;
                    case PlayerPose.Move:
                        clip = art.move.Has ? art.move : art.idle;
                        name = "이동";
                        break;
                    default:
                        clip = art.idle.Has ? art.idle : art.move;
                        name = "대기";
                        break;
                }
            }

            // A single-pose art review stays visible until each actual motion is supplied.
            // Keep empty production clips empty instead of disguising a still as an animation.
            if ((clip == null || !clip.Has) && art != null && art.previewIdleForMissingClips && art.idle.Has)
            {
                clip = art.idle;
                name += " (정지 원화 시험)";
                timed = false;
                once = false;
            }

            if (clip == null || !clip.Has)
            {
                ShowShape();
                return;
            }

            int count = clip.frames.Length;
            int frame = timed
                ? ClipTiming.TimedFrame(t, duration, hit, count, clip.KeyIndex)
                : once ? ClipTiming.OnceFrame(t, clip.fps, count) : ClipTiming.LoopFrame(Time.time, clip.fps, count);
            _body.sprite = ArtRuntime.Frame(clip, frame);
            _body.flipX = _facingSign < 0f;
            _body.transform.localScale = Vector3.one * art.scale;
            _body.transform.localPosition = art.offset;
            _body.transform.localRotation = Quaternion.identity;
            if (!_artShown)
            {
                ArtRuntime.UseArtMaterial(_body, _flash, ref _shapeMaterial);
                if (_mark) _mark.gameObject.SetActive(false);
                _artShown = true;
            }
            DebugFrame = frame;
            DebugFrameCount = count;
            DebugKeyFrame = timed ? clip.KeyIndex : -1;
            DebugClip = name;
        }

        void ShowShape()
        {
            DebugFrame = -1;
            DebugFrameCount = 0;
            DebugKeyFrame = -1;
            DebugClip = "";
            if (!_artShown) return;
            _artShown = false;
            _body.sprite = _shapeSprite;
            _body.flipX = false;
            _body.transform.localScale = Vector3.one * (PlayerController.Radius * 2f);
            _body.transform.localPosition = Vector3.zero;
            ArtRuntime.UseShapeMaterial(_body, _flash, _shapeMaterial);
            if (_mark) _mark.gameObject.SetActive(true);
        }
    }
}
