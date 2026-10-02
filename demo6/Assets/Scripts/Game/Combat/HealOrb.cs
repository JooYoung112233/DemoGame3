using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 회복 구슬(2차 3-5, 3차 3-6): 먹으면 최대 체력 10% 회복. 반경 2.5에서 자동으로 빨려 온다. 20초 뒤 사라진다.
    /// </summary>
    public sealed class HealOrb : MonoBehaviour
    {
        const float HealFraction = 0.1f;
        const float AttractRadius = 2.5f;
        const float PickupRadius = 0.5f;
        const float Life = 20f;

        static readonly System.Collections.Generic.List<HealOrb> Live = new System.Collections.Generic.List<HealOrb>();

        SpriteRenderer _sprite;
        float _age;
        bool _attracting;

        public static void Spawn(Vector2 position)
        {
            var go = new GameObject("HealOrb");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.32f;
            var orb = go.AddComponent<HealOrb>();
            Live.Add(orb);
            orb._sprite = go.AddComponent<SpriteRenderer>();
            RenderMaterials.MakeUnlit(orb._sprite);
            orb._sprite.sprite = ShapeSprites.Circle;
            orb._sprite.color = Palette.HealOrb;
            orb._sprite.sortingOrder = 2800;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            float pulse = 1f + 0.15f * Mathf.Sin(_age * 8f);
            transform.localScale = Vector3.one * (0.32f * pulse);
            var c = _sprite.color;
            c.a = _age > Life - 2f ? Mathf.Clamp01((Life - _age) / 2f) : 1f;
            _sprite.color = c;
            if (_age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            var player = PlayerController.Instance;
            if (!player || player.IsDown) return;
            Vector2 to = player.Position - (Vector2)transform.position;
            float d = to.magnitude;
            if (d <= AttractRadius) _attracting = true;
            if (_attracting) transform.position += (Vector3)(to.normalized * Mathf.Min(d, 9f * dt));
            if (d <= PickupRadius)
            {
                int healed = player.Health.Heal(Mathf.RoundToInt(player.Health.Max * HealFraction));
                if (healed > 0) WorldOverlay.Number(player.Position + Vector2.up * PlayerController.Radius, healed, NumberKind.Heal);
                Destroy(gameObject);
            }
        }

        void OnDestroy() => Live.Remove(this);

        /// <summary>구성을 바꾸거나 다시 세울 때 남은 구슬을 지운다.</summary>
        public static void DestroyAll()
        {
            foreach (var o in Live.ToArray())
                if (o) Destroy(o.gameObject);
            Live.Clear();
        }
    }
}
