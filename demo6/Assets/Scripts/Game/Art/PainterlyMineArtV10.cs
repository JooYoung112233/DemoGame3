using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Shared painted first-floor terrain. World-aligned rock pixels also cover unopened secret walls.</summary>
    public static class PainterlyMineArtV10
    {
        const int Ppu = 80;
        const float RockPeriod = 12f;
        static Texture2D _rocks;
        static Color32[] _rockPixels;

        public static void ApplyFloor(DungeonCell cell, SpriteRenderer renderer, int floor)
        {
            if (CuteArtProfileV15.ApplyFloor(cell, renderer)) return;
            if (floor != 1 || cell.Id == "E") return;
            int variant = (int)((uint)ShapeSprites.Seed(cell.Id, 7919) % 3);
            var art = Resources.Load<Sprite>(variant == 0 ? "TopDownPilotV9/entry_floor" : "TopDownV10/floor_variant_" + variant);
            if (!art) return;
            renderer.sprite = art;
            renderer.transform.localScale = new Vector3(cell.Bounds.width / art.bounds.size.x,
                cell.Bounds.height / art.bounds.size.y, 1f);
        }

        public static Sprite Rock(Rect bounds)
        {
            var root = DungeonRoot.Instance;
            if (!root || root.Floor != 1) return null;
            if (!_rocks)
            {
                _rocks = Resources.Load<Texture2D>("TopDownV10/rock_field");
                if (!_rocks || !_rocks.isReadable) return null;
                _rockPixels = _rocks.GetPixels32();
            }
            int w = Mathf.Max(1, Mathf.RoundToInt(bounds.width * Ppu));
            int h = Mathf.Max(1, Mathf.RoundToInt(bounds.height * Ppu));
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int sy = Mathf.FloorToInt(Mathf.Repeat(bounds.yMin + (y + .5f) / Ppu, RockPeriod) / RockPeriod * _rocks.height);
                for (int x = 0; x < w; x++)
                {
                    int sx = Mathf.FloorToInt(Mathf.Repeat(bounds.xMin + (x + .5f) / Ppu, RockPeriod) / RockPeriod * _rocks.width);
                    var color = _rockPixels[sy * _rocks.width + sx];
                    color.a = 255; // Exact opaque blocker footprint; shadow/collider geometry is unchanged.
                    pixels[y * w + x] = color;
                }
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true)
            { name = "V10 painted mine rock", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), Ppu, 0, SpriteMeshType.FullRect);
            sprite.name = "Dungeon wall";
            return sprite;
        }
    }
}
