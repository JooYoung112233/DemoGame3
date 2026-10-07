using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    public enum PickupKind
    {
        Gold,
        /// <summary>강화석.</summary>
        Stone,
        /// <summary>룬(기획/세-무기-우클릭-소켓-1차.md 6-4): 주우면 Inventory 룬 주머니로(가방 칸 안 씀).</summary>
        Rune,
    }

    /// <summary>
    /// 골드 무더기·강화석(2차 7-6·7-7): 빛기둥 없이 포물선으로 튀어나와 내려앉고, 반경 2.5에 들어오면 빨려 와 DungeonState에 더한다.
    /// 골드는 밝은 노랑 동전 더미(전설 주황과 구별). 플레이어가 15 밖으로 멀어지면 저절로 거둔다(3차 초안 2-6 '반경 15 밖 자동 수거').
    /// 매판 새 탐험 1차 2-4: 바구니로 올라가기 직전(ExpeditionEnding)과 계단으로 내려가기 직전(StairsUsed)에는 날아가는 중이어도
    /// 소리·글 없이 바로 거둔다(재화가 State에 들어간 뒤 꾸러미에 담긴다).
    /// 룬(6-4)도 같은 길: 강화석처럼 2.5 안에 오면 빨려 와 자동으로 줍고 Inventory 룬 주머니(종류마다 99)에 넣는다. 줍기 글 '+ 버팀 룬',
    /// 처음 한 번은 '버팀 룬 — 가방(I)에서 무기에 끼울 수 있다'(꾸러미 세기 Inventory.RuneHintKey). 바닥 그림은 RuneLook 임시 도형.
    /// </summary>
    public sealed class LootPickup : MonoBehaviour
    {
        const float AttractRadius = 2.5f;
        const float PickupRadius = 0.5f;
        const float AttractSpeed = 9f;
        const float AutoCollectDistance = 15f;

        public PickupKind Kind { get; private set; }
        public int Amount { get; private set; }
        /// <summary>룬 id(Kind가 Rune일 때만, 그 밖은 null).</summary>
        public string RuneId { get; private set; }

        LootArc _arc;
        Transform _visual;
        SpriteRenderer[] _sprites;
        bool _attracting;
        bool _collected;
        float _age;

        public static LootPickup Spawn(PickupKind kind, int amount, Vector2 from, Vector2 to, float delay)
        {
            // 룬은 id가 필요해 SpawnRune으로(종류만 넘기면 첫 룬 '버팀 룬').
            if (kind == PickupKind.Rune) return SpawnRune(RuneTable.SuperArmorId, from, to, delay);
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

        /// <summary>룬 하나를 튀어나오게 한다(LootSpawner가 장비 다음·강화석 앞에 부름). 모르는 룬 id면 만들지 않고 null.</summary>
        public static LootPickup SpawnRune(string runeId, Vector2 from, Vector2 to, float delay)
        {
            var def = RuneTable.Get(runeId);
            if (def == null) return null;
            var go = new GameObject("Rune " + def.Id);
            go.transform.position = from;
            var p = go.AddComponent<LootPickup>();
            p.Kind = PickupKind.Rune;
            p.Amount = 1;
            p.RuneId = def.Id;
            p._arc = new LootArc(from, to, delay);
            p._visual = new GameObject("Visual").transform;
            p._visual.SetParent(go.transform, false);
            RuneLook.Build(p._visual, def.Id);
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
            if (Kind == PickupKind.Rune)
            {
                CollectRune(player, quiet);
                return;
            }
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

        /// <summary>
        /// 룬 줍기: Inventory 룬 주머니에 더한다(가방 칸 안 씀). 조용히 거둘 때(올라가기·계단 직전)는 글·소리 없이.
        /// 글 '+ 버팀 룬'(룬 색), 주머니가 가득(99)이면 '버팀 룬 — 주머니가 가득 찼다'. 처음 한 번은 가방 안내 한 줄.
        /// </summary>
        void CollectRune(PlayerController player, bool quiet)
        {
            var inv = Inventory.Instance;
            int added = inv ? inv.AddRunes(RuneId, Amount) : 0;
            if (quiet)
            {
                Destroy(gameObject);
                return;
            }
            var def = RuneTable.Get(RuneId);
            string name = def != null ? def.Name : RuneId;
            Vector2 at = player ? player.Position + Vector2.up * 0.9f : (Vector2)transform.position;
            WorldOverlay.Text(at, added > 0 ? "+ " + name : name + " — 주머니가 가득 찼다", RuneLook.ColorOf(def));
            Sfx.Play(SfxKind.Pickup);
            if (added > 0)
            {
                var data = ProfileCarry.Ensure();
                if (data.Count(Inventory.RuneHintKey) == 0)
                {
                    data.Bump(Inventory.RuneHintKey);
                    DungeonEvents.Say(name + " — 가방(I)에서 무기에 끼울 수 있다");
                }
            }
            Destroy(gameObject);
        }
    }
}
