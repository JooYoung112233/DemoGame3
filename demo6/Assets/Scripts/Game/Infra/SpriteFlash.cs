using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 피격 번쩍임과 무적 깜빡임. 게임 시간으로 흐르므로 히트스톱 동안 그대로 멈춘다.
    /// 도형은 색을 바꾸고, 그림은 Demo6/SpriteFlash 셰이더로 그림 색을 번쩍임 색 쪽으로 바꾼다(곱하기 색으로는 흰색이 안 보임).
    /// </summary>
    public sealed class SpriteFlash : MonoBehaviour
    {
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static MaterialPropertyBlock _block;

        public SpriteRenderer target;
        public Color baseColor = Color.white;
        /// <summary>그림 모드: 색 대신 셰이더 값으로 번쩍인다.</summary>
        public bool useShader;
        Color _flashColor;
        float _flashLeft;
        float _blinkLeft;
        float _blinkClock;

        public void Flash(Color color, float seconds)
        {
            _flashColor = color;
            _flashLeft = Mathf.Max(_flashLeft, seconds);
            Apply();
        }

        public void Blink(float seconds)
        {
            _blinkLeft = Mathf.Max(_blinkLeft, seconds);
            _blinkClock = 0f;
        }

        public void StopAll()
        {
            _flashLeft = 0f;
            _blinkLeft = 0f;
            Apply();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_flashLeft > 0f) _flashLeft -= dt;
            if (_blinkLeft > 0f)
            {
                _blinkLeft -= dt;
                _blinkClock += dt;
            }
            Apply();
        }

        /// <summary>
        /// Update에서만 부른다. 처치 연출 코루틴이 그 뒤에 투명도를 덮어쓸 수 있어야 하므로 LateUpdate에서는 부르지 않는다.
        /// </summary>
        void Apply()
        {
            if (!target) return;
            bool flashing = _flashLeft > 0f;
            bool dim = !flashing && _blinkLeft > 0f && Mathf.FloorToInt(_blinkClock / 0.08f) % 2 == 0;
            if (useShader)
            {
                _block ??= new MaterialPropertyBlock();
                target.GetPropertyBlock(_block);
                _block.SetFloat(FlashAmountId, flashing ? 1f : 0f);
                _block.SetColor(FlashColorId, flashing ? _flashColor : Color.white);
                target.SetPropertyBlock(_block);
                target.color = new Color(1f, 1f, 1f, dim ? 0.35f : 1f);
                return;
            }
            var c = flashing ? _flashColor : baseColor;
            if (dim) c.a *= 0.35f;
            target.color = c;
        }
    }
}
