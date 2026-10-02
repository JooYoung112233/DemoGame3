using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 3차 초안 3-4 궁수 가시 덫: 놓인 뒤 0.6초 동안 빨간 예고가 차오르고, 그 뒤 밟으면 궁수 공격력의 100%(약 5.6%).
    /// 구르기 무적이면 그냥 지나간다. 한 번 밟히면 사라지고, 8초 뒤에도 사라진다.
    /// </summary>
    public sealed class SpikeTrap : MonoBehaviour
    {
        const float ArmTime = 0.6f;
        const float Radius = 0.7f;
        const float Life = 8f;

        static readonly System.Collections.Generic.List<SpikeTrap> Live = new System.Collections.Generic.List<SpikeTrap>();

        int _attack;
        float _age;
        Telegraph _telegraph;
        SpriteRenderer _spikes;

        public static void Place(Vector2 position, int attack)
        {
            var go = new GameObject("SpikeTrap");
            go.transform.position = position;
            var trap = go.AddComponent<SpikeTrap>();
            Live.Add(trap);
            trap._attack = attack;
            trap._telegraph = Telegraph.Circle(position, Radius, ArmTime);
            var spikes = new GameObject("Spikes");
            spikes.transform.SetParent(go.transform, false);
            spikes.transform.localScale = Vector3.one * (Radius * 2f);
            trap._spikes = spikes.AddComponent<SpriteRenderer>();
            trap._spikes.sprite = ShapeSprites.Ring;
            trap._spikes.color = Palette.Trap;
            trap._spikes.sortingOrder = -55;
            trap._spikes.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age < ArmTime) return;
            if (_telegraph)
            {
                _telegraph.Resolve();
                _telegraph = null;
                _spikes.enabled = true;
            }
            if (_age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            var player = PlayerController.Instance;
            if (!player || player.IsDown) return;
            if ((player.Position - (Vector2)transform.position).magnitude > Radius + PlayerController.Radius * 0.5f) return;
            if (player.ReceiveHit(_attack, 100f, transform.position, 0.4f, false)) Destroy(gameObject);
        }

        void OnDestroy()
        {
            Live.Remove(this);
            if (_telegraph) _telegraph.Cancel();
        }

        /// <summary>구성을 바꾸거나 다시 세울 때 남은 덫을 지운다.</summary>
        public static void DestroyAll()
        {
            foreach (var t in Live.ToArray())
                if (t) Destroy(t.gameObject);
            Live.Clear();
        }
    }
}
