using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 피격 번쩍임과 무적 깜빡임. 게임 시간으로 흐르므로 히트스톱 동안 그대로 멈춘다.
    /// 도형은 색을 바꾸고, 그림은 Demo6/SpriteFlash 셰이더로 그림 색을 번쩍임 색 쪽으로 바꾼다(곱하기 색으로는 흰색이 안 보임).
    /// 대상은 몸(target) 하나와, 몸과 같이 번쩍이고 깜빡일 덧그림(정수리 투구 등, AddTarget)이다(장비 문서 9-1).
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
        /// <summary>몸과 같이 칠할 덧그림(보통 0~1개). 지워진 렌더러는 칠할 때 뺀다.</summary>
        readonly List<SpriteRenderer> _more = new List<SpriteRenderer>(2);

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

        /// <summary>
        /// 몸과 같이 번쩍이고 깜빡일 덧그림을 더한다(이미 있으면 그대로). 그 렌더러에만 지금 상태를 바로 입힌다
        /// (그림 재질은 PropertyBlock이 있어야 렌더러 색을 받으므로 첫 프레임부터 넣어 둔다). 몸 색은 건드리지 않는다.
        /// </summary>
        public void AddTarget(SpriteRenderer renderer)
        {
            if (!renderer || renderer == target || _more.Contains(renderer)) return;
            _more.Add(renderer);
            State(out bool flashing, out bool dim);
            Paint(renderer, flashing, dim);
        }

        /// <summary>덧그림을 뺀다(정수리 시점을 끌 때). 없으면 아무것도 하지 않는다.</summary>
        public void RemoveTarget(SpriteRenderer renderer) => _more.Remove(renderer);

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

        void State(out bool flashing, out bool dim)
        {
            flashing = _flashLeft > 0f;
            dim = !flashing && _blinkLeft > 0f && Mathf.FloorToInt(_blinkClock / 0.08f) % 2 == 0;
        }

        /// <summary>
        /// Update에서만 부른다. 처치 연출 코루틴이 그 뒤에 투명도를 덮어쓸 수 있어야 하므로 LateUpdate에서는 부르지 않는다.
        /// </summary>
        void Apply()
        {
            State(out bool flashing, out bool dim);
            if (target) Paint(target, flashing, dim);
            for (int i = _more.Count - 1; i >= 0; i--)
            {
                var sr = _more[i];
                if (!sr)
                {
                    _more.RemoveAt(i);
                    continue;
                }
                Paint(sr, flashing, dim);
            }
        }

        void Paint(SpriteRenderer sr, bool flashing, bool dim)
        {
            if (useShader)
            {
                _block ??= new MaterialPropertyBlock();
                sr.GetPropertyBlock(_block);
                _block.SetFloat(FlashAmountId, flashing ? 1f : 0f);
                _block.SetColor(FlashColorId, flashing ? _flashColor : Color.white);
                sr.SetPropertyBlock(_block);
                sr.color = new Color(1f, 1f, 1f, dim ? 0.35f : 1f);
                return;
            }
            var c = flashing ? _flashColor : baseColor;
            if (dim) c.a *= 0.35f;
            sr.color = c;
        }
    }
}
