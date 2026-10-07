using System.Collections;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Applies approved map pixels and materials to existing renderers. Never changes lights, colliders, map data or visibility.</summary>
    [DefaultExecutionOrder(9000)]
    public sealed class ApprovedWorldArtV043 : MonoBehaviour
    {
        public int ReplacedRenderers { get; private set; }
        public static void Ensure(GameObject owner)
        {
            if (owner && !owner.GetComponent<ApprovedWorldArtV043>()) owner.AddComponent<ApprovedWorldArtV043>();
        }
        IEnumerator Start()
        {
            // Existing terrain and pilot prop Start methods finish before binding their visuals.
            yield return null;
            yield return null;
            var catalog = Resources.Load<ApprovedWorldCatalogV043>("ApprovedVisualsV043/World");
            if (catalog && DungeonRoot.Instance && DungeonRoot.Instance.gameObject.scene == gameObject.scene)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    string role = Role(renderer);
                    var entry = catalog.Find(role);
                    if (entry == null || !entry.sprite || !renderer.sprite) continue;
                    Vector2 size = renderer.drawMode == SpriteDrawMode.Simple ? (Vector2)renderer.sprite.bounds.size : renderer.size;
                    var scale = renderer.transform.localScale;
                    renderer.sprite = entry.sprite;
                    // Preserve the old world rectangle, position, rotation, pivot and collision footprint.
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.transform.localScale = new Vector3(scale.x * size.x / entry.sprite.bounds.size.x,
                        scale.y * size.y / entry.sprite.bounds.size.y, scale.z);
                    if (entry.material) renderer.sharedMaterial = entry.material;
                    ReplacedRenderers++;
                }
            }
            var grounding = gameObject.AddComponent<GroundContactV043>();
            grounding.wholeScene = true;
            grounding.Build();
            var projection = gameObject.AddComponent<ProjectedShadowV043>();
            projection.wholeScene = true;
            projection.Build();
        }
        public static string Role(SpriteRenderer renderer)
        {
            if (!renderer || !renderer.sprite) return null;
            if (renderer.name == "Floor" && renderer.sortingOrder == -1000) return "floor";
            string name = renderer.sprite.name;
            foreach (string id in new[] { "pillar_a", "pillar_b", "wall_h29", "wall_v17", "wall_v6a", "wall_v6b", "winch", "broken_timber", "chain", "rubble" })
                if (name == id || name.StartsWith(id + "-v")) return id;
            if (name == "lantern" || name == "lantern-body-v036" || name == "lantern-mount-v039") return "lantern";
            return null;
        }
    }
}
