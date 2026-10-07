using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Independent PNG views only. Logical carriers preserve the existing prop sizes/open transforms.</summary>
    internal static class PropsV062Art
    {
        // Match the approved raised-town-module band; low chests/safes keep their existing orders.
        const int RaisedDoorOrder = 50;
        public static int SolidOrder(float groundY) => WorldProps.SortY(groundY, RaisedDoorOrder);

        internal sealed class View
        {
            public Transform Carrier;
            public SpriteRenderer Renderer;
        }

        // Validate the complete set before creating any object: a missing module leaves the original shape fallback intact.
        public static bool TryLoad(string[] names, out Sprite[] sprites)
        {
            sprites = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var sprite = Resources.Load<Sprite>("PropsV062/" + names[i]);
                if (!sprite || sprite.bounds.size.x <= .0001f || sprite.bounds.size.y <= .0001f)
                {
                    sprites = null;
                    return false;
                }
                sprites[i] = sprite;
            }
            return true;
        }

        public static View Part(Transform parent, string name, Sprite sprite, Vector2 at, Vector2 size, Color tint, int order)
        {
            var carrier = new GameObject(name).transform;
            carrier.SetParent(parent, false);
            carrier.localPosition = at;
            carrier.localScale = new Vector3(size.x, size.y, 1f);
            var art = new GameObject("PNG").transform;
            art.SetParent(carrier, false);
            var renderer = art.gameObject.AddComponent<SpriteRenderer>();
            renderer.color = tint;
            renderer.sortingOrder = order;
            SetSprite(renderer, sprite);
            return new View { Carrier = carrier, Renderer = renderer };
        }

        public static Vector2 FitSize(Sprite sprite, Vector2 maximumSize)
        {
            Vector2 native = sprite.bounds.size;
            float scale = Mathf.Min(maximumSize.x / native.x, maximumSize.y / native.y);
            return native * scale;
        }

        public static void SetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            if (!renderer || !sprite) return;
            var bounds = sprite.bounds;
            renderer.sprite = sprite;
            renderer.transform.localScale = new Vector3(1f / bounds.size.x, 1f / bounds.size.y, 1f);
            renderer.transform.localPosition = new Vector3(-bounds.center.x / bounds.size.x, -bounds.center.y / bounds.size.y, 0f);
            // Default lit material, color, enabled and forceRenderingOff states are intentionally retained.
        }

        public static View DoorPart(Transform parent, string name, Sprite sprite, DungeonEdge edge, Vector2 localDoor,
            Vector2 sizeAlongNormal, int order)
        {
            var view = Part(parent, name, sprite, WorldProps.DoorLocal(edge, localDoor.x, localDoor.y), sizeAlongNormal, Color.white, order);
            if (edge.DoorSize.y > edge.DoorSize.x)
            {
                // DoorLocal's vertical basis is (up,right). Rotation alone would produce (up,left).
                view.Carrier.localRotation = Quaternion.Euler(0f, 0f, 90f);
                view.Carrier.localScale = new Vector3(sizeAlongNormal.x, -sizeAlongNormal.y, 1f);
            }
            return view;
        }
    }
}
