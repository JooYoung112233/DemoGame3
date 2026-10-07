using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>재질 시범: 1층 E 승강장의 바닥·소유 벽만 교체한다. 배치·재질·그림자·충돌은 그대로 둔다.</summary>
    public sealed class TextureStudyV7 : MonoBehaviour
    {
        struct Pair { public SpriteRenderer Renderer; public Sprite Before, After; }
        readonly List<Pair> _pairs = new List<Pair>();
        public int RendererCount => _pairs.Count;
        public bool PreviewEnabled { get; private set; } = true;

        public static void Attach(DungeonCell cell, Transform cellRoot, SpriteRenderer floor, int floorNumber)
        {
            // 미리 그린 바닥의 월드 좌표가 맞는 이 한 칸만. 다른 층·절차 배치로 확장하지 않는다.
            if (floorNumber != 1 || cell.Id != "E" || cell.Bounds != new Rect(-14f, 8f, 28f, 16f)) return;
            var floorArt = Resources.Load<Sprite>("TextureStudyV7/entry_floor");
            var wallArt = Resources.Load<Texture2D>("TextureStudyV7/wall_tile");
            if (!floorArt || !wallArt) return;
            var study = floor.gameObject.AddComponent<TextureStudyV7>();
            study.Add(floor, floorArt);
            var source = wallArt.GetPixels32();
            foreach (var sr in cellRoot.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.sprite || sr.sprite.name != "Dungeon wall") continue;
                var bounds = sr.bounds;
                const int ppu = ShapeSprites.GroundPixelsPerUnit;
                int w = Mathf.Max(1, Mathf.RoundToInt(bounds.size.x * ppu));
                int h = Mathf.Max(1, Mathf.RoundToInt(bounds.size.y * ppu));
                int x0 = Mathf.RoundToInt(bounds.min.x * ppu), y0 = Mathf.RoundToInt(bounds.min.y * ppu);
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    pixels[y * w + x] = source[Wrap(y0 + y, wallArt.height) * wallArt.width + Wrap(x0 + x, wallArt.width)];
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "V7 study wall", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels32(pixels); texture.Apply(false, true);
                var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.one * .5f, ppu, 0, SpriteMeshType.FullRect);
                sprite.name = "V7 study wall";
                RuntimeAssetOwner.Own(sr.gameObject, sprite);
                study.Add(sr, sprite);
            }
            TopDownArtPilotV9.Attach(cell, cellRoot, floor, floorNumber);
        }

        void Add(SpriteRenderer renderer, Sprite after)
        {
            _pairs.Add(new Pair { Renderer = renderer, Before = renderer.sprite, After = after });
            renderer.sprite = after;
        }

        // 동일 Play 상태에서 전후 비교할 때만 사용한다. 프로젝트 자산을 변경하지 않는다.
        public void SetPreview(bool enabled)
        {
            PreviewEnabled = enabled;
            foreach (var pair in _pairs) if (pair.Renderer) pair.Renderer.sprite = enabled ? pair.After : pair.Before;
        }

        static int Wrap(int value, int period) => (value % period + period) % period;
    }
}
