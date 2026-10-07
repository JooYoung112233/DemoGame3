using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Approved overhead art pilot. Replaces artwork in entrance E only; no collision, lighting, visibility, UI or gameplay changes.</summary>
    public sealed class TopDownArtPilotV9 : MonoBehaviour
    {
        struct Swap { public SpriteRenderer renderer; public Sprite before, after; public Color colorBefore, colorAfter; public Vector3 scaleBefore, scaleAfter; }
        readonly List<Swap> _swaps = new List<Swap>();
        readonly List<SpriteRenderer> _hiddenOriginals = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _added = new List<SpriteRenderer>();
        public int ReplacementCount => _swaps.Count;
        public int NewPropCount => _added.Count;
        public bool PreviewEnabled { get; private set; } = true;
        static Sprite Art(string id) => Resources.Load<Sprite>("TopDownPilotV9/" + id);

        public static void Attach(DungeonCell cell, Transform cellRoot, SpriteRenderer floor, int number)
        {
            if (number != 1 || cell.Id != "E" || cell.Bounds != new Rect(-14f, 8f, 28f, 16f) || !Art("entry_floor")) return;
            var pilot = floor.gameObject.AddComponent<TopDownArtPilotV9>();
            pilot.SwapArt(floor, "entry_floor", false, false);
            foreach (var sr in cellRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == floor || !sr.sprite || sr.sprite.name != "V7 study wall") continue;
                string name = sr.transform.parent.name;
                string id = name == "Wall Left" ? "wall_v17" : name == "Wall Right a" ? "wall_v6a" : name == "Wall Right b" ? "wall_v6b"
                    : name == "Pillar" ? (sr.transform.position.x < 0 ? "pillar_a" : "pillar_b") : "wall_h29";
                pilot.SwapArt(sr, id, false, true);
            }
        }

        void SwapArt(SpriteRenderer sr, string id, bool neutralTint, bool unitScale)
        {
            var sprite = Art(id);
            if (!sprite) return;
            var swap = new Swap { renderer = sr, before = sr.sprite, after = sprite, colorBefore = sr.color, colorAfter = neutralTint ? Color.white : sr.color,
                scaleBefore = sr.transform.localScale, scaleAfter = unitScale ? Vector3.one : sr.transform.localScale };
            _swaps.Add(swap);
            sr.sprite = swap.after; sr.color = swap.colorAfter; sr.transform.localScale = swap.scaleAfter;
        }

        void Start()
        {
            var root = DungeonRoot.Instance;
            if (!root) return;
            var stake = root.transform.Find("Stake f1.E.stake");
            if (stake)
            {
                var basis = stake.Find("Base").GetComponent<SpriteRenderer>();
                NewProp(stake, "V9 winch", "winch", new Vector2(0, .4f), basis, basis.sortingOrder + 2);
                Hide(stake, "Base", "Post", "Ring", "Rope");
            }
            var lamp = root.transform.Find("WallLamp f1.E.lamp/Anchor");
            if (lamp)
            {
                var bowl = lamp.Find("Bowl").GetComponent<SpriteRenderer>();
                NewProp(lamp, "V9 lantern", "lantern", new Vector2(0, .08f), bowl, bowl.sortingOrder);
                Hide(lamp, "Bowl", "Bowl recess", "Worn bowl lip", "Wick");
            }
            var arches = root.transform.Find("Landing Arches");
            if (arches) foreach (Transform arch in arches)
            {
                var originals = arch.GetComponentsInChildren<SpriteRenderer>();
                if (originals.Length == 0 || !Art("blocked_arch")) continue;
                float angle = arch.name == "Arch Down" ? 180f : arch.name == "Arch Right" ? -90f : arch.name == "Arch Left" ? 90f : 0f;
                var visual = new GameObject("V9 blocked passage").transform; visual.SetParent(arch, false); visual.localRotation = Quaternion.Euler(0, 0, angle);
                NewProp(visual, "V9 rubble arch", "blocked_arch", new Vector2(0, -.55f), originals[0], WorldProps.WallDetailOrder + 4);
                foreach (var old in originals) if (old.enabled) { _hiddenOriginals.Add(old); old.enabled = false; }
            }
            var decor = root.transform.Find("Dungeon Decor/Decor E");
            if (decor) foreach (var sr in decor.GetComponentsInChildren<SpriteRenderer>())
            {
                string id = sr.name == "Plank" ? "broken_timber" : sr.name == "Chain" ? "chain" : sr.name == "Rubble" ? "rubble" : null;
                if (id != null) SwapArt(sr, id, true, false);
            }
        }

        void NewProp(Transform parent, string name, string id, Vector2 position, SpriteRenderer basis, int order)
        {
            var art = Art(id); if (!art) return;
            var go = new GameObject(name); go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = art; sr.sharedMaterial = basis.sharedMaterial; sr.sortingLayerID = basis.sortingLayerID; sr.sortingOrder = order;
            _added.Add(sr);
        }

        void Hide(Transform root, params string[] names)
        {
            foreach (string name in names) { var child = root.Find(name); if (child && child.TryGetComponent(out SpriteRenderer sr) && sr.enabled) { _hiddenOriginals.Add(sr); sr.enabled = false; } }
        }

        /// <summary>Review-only reversible art switch. Never changes gameplay or light state.</summary>
        public void SetPreview(bool enabled)
        {
            PreviewEnabled = enabled;
            foreach (var s in _swaps) if (s.renderer) { s.renderer.sprite = enabled ? s.after : s.before; s.renderer.color = enabled ? s.colorAfter : s.colorBefore; s.renderer.transform.localScale = enabled ? s.scaleAfter : s.scaleBefore; }
            foreach (var sr in _hiddenOriginals) if (sr) sr.enabled = !enabled;
            foreach (var sr in _added) if (sr) sr.enabled = enabled;
        }
    }
}
