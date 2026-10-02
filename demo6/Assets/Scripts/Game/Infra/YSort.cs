using UnityEngine;

namespace Demo6.Game
{
    /// <summary>탑다운 앞뒤 겹침. 아래(앞)에 있을수록 위에 그린다.</summary>
    public sealed class YSort : MonoBehaviour
    {
        public int baseOrder = 1000;
        SpriteRenderer[] _renderers;
        int[] _offsets;

        void Awake() => Refresh();

        public void Refresh()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _offsets = new int[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _offsets[i] = _renderers[i].sortingOrder;
        }

        void LateUpdate()
        {
            int order = baseOrder - Mathf.RoundToInt(transform.position.y * 20f);
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i]) _renderers[i].sortingOrder = order + _offsets[i];
        }
    }
}
