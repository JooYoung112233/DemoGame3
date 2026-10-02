using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 레벨업 순간의 따뜻한 흰 고리(3차 초안 4-3): 플레이어 둘레에서 0.5초 동안 퍼지며 사라진다.
    /// 빛을 무시하는 재질이라 어둠 속에서도 보이고, 실제 시간으로 돌아 역경직·느린 화면과 관계없이 0.5초다. 시간은 멈추지 않는다.
    /// </summary>
    public sealed class LevelUpRing : MonoBehaviour
    {
        const float Duration = 0.5f;
        const float StartDiameter = 0.9f;
        const float EndDiameter = 4.4f;
        const int SortOrder = 3200;
        static readonly Color Warm = new Color(1f, 0.95f, 0.82f, 1f);

        Transform _follow;
        SpriteRenderer _ring;
        SpriteRenderer _glow;
        float _age;

        public static LevelUpRing Spawn(Transform follow)
        {
            var go = new GameObject("LevelUpRing");
            go.transform.position = follow ? follow.position : Vector3.zero;
            go.transform.localScale = Vector3.one * StartDiameter;
            var fx = go.AddComponent<LevelUpRing>();
            fx._follow = follow;

            fx._ring = go.AddComponent<SpriteRenderer>();
            fx._ring.sprite = ShapeSprites.Ring;
            fx._ring.color = Warm;
            fx._ring.sortingOrder = SortOrder;
            RenderMaterials.MakeUnlit(fx._ring);

            // 고리 안쪽의 옅은 빛(고리와 함께 커지고 더 빨리 사라진다).
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            fx._glow = glowGo.AddComponent<SpriteRenderer>();
            fx._glow.sprite = ShapeSprites.Circle;
            fx._glow.color = new Color(Warm.r, Warm.g, Warm.b, 0.22f);
            fx._glow.sortingOrder = SortOrder - 1;
            RenderMaterials.MakeUnlit(fx._glow);
            return fx;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / Duration);
            if (_follow) transform.position = _follow.position;
            float ease = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(StartDiameter, EndDiameter, ease);

            var c = Warm;
            c.a = 1f - t * t;
            _ring.color = c;
            var g = Warm;
            g.a = 0.22f * (1f - t);
            _glow.color = g;

            if (_age >= Duration) Destroy(gameObject);
        }
    }
}
