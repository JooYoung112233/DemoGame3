using UnityEngine;

namespace Demo6.Game
{
    /// <summary>한 번 재생하고 사라지는 그림 이펙트(베기 궤적 등). 게임 시간으로 재생해 히트스톱 동안 첫 장면에 멈춘다.</summary>
    public sealed class ClipVfx : MonoBehaviour
    {
        SpriteClip _clip;
        SpriteRenderer _sprite;
        float _t;

        public static void Play(SpriteClip clip, Vector2 position, float angleDeg, float scale, bool flipY, int sortingOrder = 2900)
        {
            if (clip == null || !clip.Has) return;
            var go = new GameObject("ClipVfx");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            go.transform.localScale = Vector3.one * scale;
            var v = go.AddComponent<ClipVfx>();
            v._clip = clip;
            v._sprite = go.AddComponent<SpriteRenderer>();
            RenderMaterials.MakeUnlit(v._sprite);
            v._sprite.sprite = clip.frames[0];
            v._sprite.flipY = flipY;
            v._sprite.sortingOrder = sortingOrder;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float fps = _clip.fps > 0f ? _clip.fps : 12f;
            int frame = Mathf.FloorToInt(_t * fps);
            if (frame >= _clip.frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            _sprite.sprite = _clip.frames[frame];
        }
    }
}
