using UnityEngine;

namespace Demo6.Game
{
    public enum PickupKind
    {
        Gold,
        /// <summary>강화석.</summary>
        Stone,
    }

    /// <summary>
    /// 골드 무더기·강화석(2차 7-6·7-7): 빛기둥 없이 포물선으로 튀어나와 내려앉고, 반경 2.5에 들어오면 빨려 와 DungeonState에 더한다.
    /// 골드는 밝은 노랑 동전 더미(전설 주황과 구별). 플레이어가 15 밖으로 멀어지면 저절로 거둔다(3차 초안 2-6 '반경 15 밖 자동 수거').
    /// 매판 새 탐험 1차 2-4: 바구니로 올라가기 직전(ExpeditionEnding)과 계단으로 내려가기 직전(StairsUsed)에는 날아가는 중이어도
    /// 소리·글 없이 바로 거둔다(재화가 State에 들어간 뒤 꾸러미에 담긴다).
    /// </summary>
    public sealed class LootPickup : MonoBehaviour
    {
        const float AttractRadius = 2.5f;
        const float PickupRadius = 0.5f;
        const float AttractSpeed = 9f;
        const float AutoCollectDistance = 15f;

        public PickupKind Kind { get; private set; }
        public int Amount { get; private set; }

        LootArc _arc;
        Transform _visual;
        SpriteRenderer[] _sprites;
        bool _attracting;
        bool _collected;
        float _age;

        public static LootPickup Spawn(PickupKind kind, int amount, Vector2 from, Vector2 to, float delay)
        {
            var go = new GameObject(kind == PickupKind.Gold ? "GoldPile" : "Stone");
            go.transform.position = from;
            var p = go.AddComponent<LootPickup>();
            p.Kind = kind;
            p.Amount = Mathf.Max(1, amount);
            p._arc = new LootArc(from, to, delay);
            p._visual = new GameObject("Visual").transform;
            p._visual.SetParent(go.transform, false);
            if (kind == PickupKind.Gold) LootVisuals.BuildGoldPile(p._visual);
            else LootVisuals.BuildStone(p._visual);
            p._sprites = p._visual.GetComponentsInChildren<SpriteRenderer>(true);
            p.SetVisible(false);
            return p;
        }

        void OnEnable()
        {
            DungeonEvents.ExpeditionEnding += CollectNow;
            DungeonEvents.StairsUsed += CollectNow;
        }

        void OnDisable()
        {
            DungeonEvents.ExpeditionEnding -= CollectNow;
            DungeonEvents.StairsUsed -= CollectNow;
        }

        /// <summary>장면을 떠나기 직전: 날아가는 중이어도 바로 거둔다(검은 화면 뒤라 소리·글 없음).</summary>
        void CollectNow() => Collect(PlayerController.Instance, true);

        /// <summary>
        /// 장면에 있는 재화를 모두 바로 거둔다(소리·글 없음). 올라가기·계단은 사건(ExpeditionEnding·StairsUsed)으로 거두고,
        /// 사건 없이 장면을 바꾸는 곳(시험 패널 씨앗 다시 짓기)이 직접 부른다.
        /// </summary>
        public static void CollectAllNow()
        {
            foreach (var p in FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude))
                if (p) p.CollectNow();
        }

        void SetVisible(bool visible)
        {
            if (_sprites == null) return;
            foreach (var sr in _sprites)
                if (sr) sr.enabled = visible;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_collected || dt <= 0f) return;
            if (_arc != null && !_arc.Done)
            {
                _arc.Step(dt, out var ground, out var height);
                transform.position = ground;
                SetVisible(!_arc.Waiting);
                _visual.localPosition = new Vector3(0f, height, 0f);
                if (_arc.Done) _visual.localPosition = Vector3.zero;
                return;
            }
            _age += dt;
            float bob = 1f + 0.08f * Mathf.Sin(_age * 6f);
            _visual.localScale = Vector3.one * bob;

            var player = PlayerController.Instance;
            if (!player || player.IsDown) return;
            Vector2 to = player.Position - (Vector2)transform.position;
            float d = to.magnitude;
            if (d > AutoCollectDistance)
            {
                Collect(player);
                return;
            }
            if (d <= AttractRadius) _attracting = true;
            if (_attracting) transform.position += (Vector3)(to.normalized * Mathf.Min(d, AttractSpeed * dt));
            if (d <= PickupRadius) Collect(player);
        }

        void Collect(PlayerController player, bool quiet = false)
        {
            if (_collected) return;
            _collected = true;
            var root = DungeonRoot.Instance;
            if (root != null && root.State != null)
            {
                if (Kind == PickupKind.Gold) root.State.Gold += Amount;
                else root.State.Stones += Amount;
            }
            if (quiet)
            {
                Destroy(gameObject);
                return;
            }
            string text = Kind == PickupKind.Gold ? "+" + Amount + " 골드" : "+" + Amount + " 강화석";
            Color color = Kind == PickupKind.Gold ? LootVisuals.Gold : LootVisuals.Stone;
            Vector2 at = player ? player.Position + Vector2.up * 0.9f : (Vector2)transform.position;
            WorldOverlay.Text(at, text, color);
            Sfx.Play(SfxKind.Pickup);
            Destroy(gameObject);
        }
    }
}
