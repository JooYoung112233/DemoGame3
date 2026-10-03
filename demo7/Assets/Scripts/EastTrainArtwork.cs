using UnityEngine;

namespace EastTrain
{
    public sealed partial class EastTrainDemo
    {
        void BuildArtwork()
        {
            var art = Resources.Load<EastTrainArtSet>("EastTrainArtSet");
            if (art == null) return;
            if (PlaceArt("Painted furnace", art.Furnace, new Vector2(-5.2f, -.34f), new Vector2(2.45f, 2.7f), 18))
                HideGeometry("Furnace", "Firebox rim", "Firebox grate", "Furnace rib", "Furnace bolt");
            if (PlaceArt("Painted engine", art.Engine, new Vector2(-1.8f, -.35f), new Vector2(2.95f, 2.3f), 18))
            {
                HideGeometry("Engine flywheel", "Engine inner wheel", "Cylinder casing", "Moving piston");
                var paintedEngine = train.Find("Painted engine");
                engineRotor.localPosition = paintedEngine.localPosition + new Vector3(.15f, 0, 0);
                engineRotor.localScale = Vector3.one * .63f;
                cylinderOrigin = new Vector2(-2.65f, paintedEngine.localPosition.y);
                engineStatus.transform.localPosition = new Vector3(-1.7f, -1.42f, 0);
            }
            if (PlaceArt("Painted workbench", art.Workbench, new Vector2(RepairStationX, -.94f), new Vector2(2.65f, 1.4f), 20))
                HideGeometry("Repair bench", "Detached side plate");
        }
        bool PlaceArt(string name, EastTrainArtSet.Part part, Vector2 position, Vector2 maxSize, int order)
        {
            if (part.Texture == null || part.Pixels.width <= 0) return false;
            var t = Root(name, train); t.localPosition = position;
            var sprite = Sprite.Create(part.Texture, part.Pixels, Vector2.one * .5f, 100);
            var renderer = t.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
            t.localScale = Vector3.one * Mathf.Min(maxSize.x / sprite.bounds.size.x, maxSize.y / sprite.bounds.size.y);
            // Every painted prop stands on the lower deck regardless of its native aspect ratio.
            t.localPosition = new Vector3(position.x, -1.65f + sprite.bounds.size.y * t.localScale.y * .5f, 0);
            return true;
        }
        void HideGeometry(params string[] names)
        {
            foreach (Transform child in train)
                foreach (var name in names)
                    if (child.name == name && child.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.enabled = false;
        }
    }
}
