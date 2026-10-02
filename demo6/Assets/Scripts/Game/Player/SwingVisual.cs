using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 콤보 단계 판정 범위를 잠깐 보여 준다(실제 시간). 판정과 같은 모양·크기다.
    /// 부채꼴은 단계마다 방향을 엇갈려 베는 흐름이 보이게 하고, 마무리는 더 진하고 오래 남긴다.
    /// </summary>
    public sealed class SwingVisual : MonoBehaviour
    {
        SpriteRenderer _sprite;
        float _age;
        float _life;
        Color _color;
        float _growFrom = 1f;
        Vector3 _baseScale;
        bool _line;
        Vector2 _lineOrigin;
        Vector2 _lineDir;

        public static void ShowStep(WeaponAttackRule weapon, int stepIndex, ComboStep step, Vector2 origin, Vector2 dir, int hitIndex)
        {
            int stepNumber = stepIndex + 1;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // 그림 이펙트가 있으면 조준 방향으로 돌려 재생한다(원형은 원 중심, 부채꼴·찌르기는 검사 중심 기준).
            var set = ArtRuntime.Active;
            var stepArt = set ? set.Weapon(weapon.id)?.Step(stepIndex) : null;
            if (stepArt != null && stepArt.slash.Has)
            {
                Vector2 at = step.shape == ComboShape.Circle ? origin + dir * step.centerOffset : origin;
                bool mirror = stepArt.mirrorAlternate && (stepNumber + hitIndex) % 2 == 0;
                ClipVfx.Play(stepArt.slash, at, step.shape == ComboShape.Circle ? 0f : angle, stepArt.slashScale, mirror);
                return;
            }
            bool strong = step.finisher;
            var color = new Color(1f, 1f, 1f, strong ? 0.75f : 0.6f);
            float life = strong ? 0.18f : 0.13f;
            switch (step.shape)
            {
                case ComboShape.Line:
                {
                    // 찌르기: 앞으로 뻗는 긴 띠.
                    var v = Create(ShapeSprites.Square, origin + dir * (step.size * 0.5f), angle, new Vector3(step.size, step.width * 0.55f, 1f), color, life);
                    v._growFrom = 0.4f;
                    v._line = true;
                    v._lineOrigin = origin;
                    v._lineDir = dir;
                    break;
                }
                case ComboShape.Circle:
                {
                    Vector2 center = origin + dir * step.centerOffset;
                    var ring = Create(ShapeSprites.Ring, center, 0f, Vector3.one * (step.size * 2f), color, life);
                    ring._growFrom = 0.55f;
                    var fill = Create(ShapeSprites.Circle, center, 0f, Vector3.one * (step.size * 2f), new Color(1f, 1f, 1f, strong ? 0.28f : 0.18f), life * 0.8f);
                    fill._growFrom = 0.3f;
                    break;
                }
                default:
                {
                    // 홀수·짝수 단계와 타를 엇갈려 좌우로 베는 느낌을 준다.
                    bool flip = (stepNumber + hitIndex) % 2 == 0;
                    var v = Create(ShapeSprites.Sector(step.arcDeg), origin, angle + (flip ? 8f : -8f), Vector3.one * step.size, color, life);
                    v._sprite.flipY = flip;
                    break;
                }
            }
        }

        public static void ShowRing(Vector2 origin, float radius)
        {
            var v = Create(ShapeSprites.Ring, origin, 0f, Vector3.one * (radius * 2f), new Color(1f, 1f, 1f, 0.55f), 0.12f);
            v._growFrom = 0.7f;
        }

        static SwingVisual Create(Sprite sprite, Vector2 position, float angle, Vector3 scale, Color color, float life)
        {
            var go = new GameObject("SwingVisual");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = scale;
            var v = go.AddComponent<SwingVisual>();
            v._sprite = go.AddComponent<SpriteRenderer>();
            v._sprite.sprite = sprite;
            v._sprite.sortingOrder = 2900;
            RenderMaterials.MakeUnlit(v._sprite);
            v._color = color;
            v._sprite.color = color;
            v._life = life;
            v._baseScale = scale;
            return v;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(_age / _life);
            var c = _color;
            c.a *= 1f - p * p;
            _sprite.color = c;
            if (_growFrom < 1f)
            {
                float g = Mathf.Lerp(_growFrom, 1f, 1f - (1f - p) * (1f - p));
                if (_line)
                {
                    // 찌르기는 플레이어 쪽 끝을 고정하고 앞으로 뻗는다.
                    float length = _baseScale.x * g;
                    transform.localScale = new Vector3(length, _baseScale.y, 1f);
                    transform.position = _lineOrigin + _lineDir * (length * 0.5f);
                }
                else transform.localScale = _baseScale * g;
            }
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
