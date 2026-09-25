using UnityEngine;

namespace Demo5.FrontEnd
{
    // A soft gold ring on the floor around a party pawn's base (not above the head): this member still has a free action this
    // turn (기획/탐험-화면정리와-행동칸-1차.md §3). World-space sprite drawn just under the base parts inside the pawn's sorting
    // group; its ring texture is generated in code once (no raster asset). FieldMemberActionSlots adds it at runtime and says
    // when it shows; presentation only.
    [DisallowMultipleComponent]
    public sealed class FieldPawnGlow : MonoBehaviour
    {
        const float RingAt = .8f; // ring radius in the generated sprite (share of its half size)
        [Tooltip("받침대 (비우면 이름이 'Base'인 자식)")] public SpriteRenderer Base;
        public bool Shown { get; private set; }
        public bool Strong { get; private set; }
        public SpriteRenderer Ring => ring;

        Color color = new Color(1f, .8f, .3f, .9f); float size = 1.4f, speed = 3f, depth = .35f, shownAt;
        SpriteRenderer ring; static Sprite sprite;

        // on: show; size: ring width × the base width; strong: the member chosen on the cards (faster pulse).
        public void Set(bool on, Color c, float sizeTimes, float pulseSpeed, float pulseDepth, bool strong)
        {
            color = c; size = sizeTimes; speed = pulseSpeed; depth = pulseDepth; Strong = strong;
            if (on == Shown) return;
            Shown = on; if (on) { shownAt = Time.unscaledTime; Build(); }
            if (ring && ring.enabled != on) ring.enabled = on;
        }

        void Build()
        {
            if (ring) return;
            if (!Base) { var shadow = GetComponent<PawnGroundShadow>(); if (shadow) Base = shadow.Base; }
            if (!Base) { var t = transform.Find("Base"); Base = t ? t.GetComponent<SpriteRenderer>() : null; }
            if (!Base) return;
            var go = new GameObject("FreeGlow"); go.layer = gameObject.layer; go.transform.SetParent(transform, false);
            ring = go.AddComponent<SpriteRenderer>(); ring.sprite = RingSprite(); ring.sharedMaterial = Base.sharedMaterial; ring.sortingLayerID = Base.sortingLayerID;
            // Under every base part (shadow, foot, bevel): the ring lies on the floor around the stand.
            int order = Base.sortingOrder; foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) if (r != ring && r.name.StartsWith("Base")) order = Mathf.Min(order, r.sortingOrder);
            ring.sortingOrder = order - 1; ring.enabled = Shown;
        }

        void LateUpdate()
        {
            if (!ring || !ring.enabled || !Base || !ring.sprite) return;
            var b = Base.bounds; var t = ring.transform; var parent = transform.lossyScale; var s = ring.sprite.bounds.size;
            t.position = new Vector3(b.center.x, b.center.y, Base.transform.position.z);
            float wave = .5f - .5f * Mathf.Cos((Time.unscaledTime - shownAt) * speed * (Strong ? 1.7f : 1));
            float k = size * (1 + .05f * wave) / RingAt;
            t.localScale = new Vector3(b.size.x * k / Mathf.Max(1e-4f, s.x * Mathf.Abs(parent.x)), b.size.y * k / Mathf.Max(1e-4f, s.y * Mathf.Abs(parent.y)), 1);
            var c = color; c.a *= 1 - depth * wave; ring.color = c;
        }
        void OnDestroy() { if (ring) Destroy(ring.gameObject); }

        // A white ellipse ring with a soft glow, drawn once in code.
        static Sprite RingSprite()
        {
            if (sprite) return sprite;
            const int w = 256, h = 112; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Field pawn glow ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + .5f) / w * 2 - 1, ny = (y + .5f) / h * 2 - 1, d = Mathf.Sqrt(nx * nx + ny * ny), off = Mathf.Abs(d - RingAt);
                    float core = Mathf.Clamp01(1 - off / .075f), glow = Mathf.Clamp01(1 - off / .2f);
                    float a = Mathf.Max(core * core * (3 - 2 * core), .42f * glow * glow);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(a)));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100); sprite.name = "Field pawn glow ring"; sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
