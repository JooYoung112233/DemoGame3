using System;
using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demo6.Game
{
    /// <summary>
    /// Local presentation only: soft ground contact meshes and small module motions.
    /// Call Build once, after TownModularArtV058 has assembled its module hierarchy.
    /// Owns no collider, light, actor, input, camera, or global rendering setting.
    /// </summary>
    public sealed class TownEnvironmentV059 : MonoBehaviour
    {
        const float PixelsPerUnit = TownLayout.WorldPixelsPerUnit;
        const int ContactOrder = -952; // Above terrain/floors/voids, below actor shadows (-940).
        const int CastOrder = -982;    // Below floor (-977), mine ground (-970), well hole (-960).
        static readonly Color ShadowColor = new Color(.035f, .027f, .019f, 1f);
        readonly List<Motion> motions = new List<Motion>();
        readonly HashSet<SpriteRenderer> animated = new HashSet<SpriteRenderer>();
        TownGeneratedResourcesV058 resources;
        Transform shadowRoot;
        Material material;
        bool built;
        public int ContactShadowCount { get; private set; }
        public int BuildingShadowCount { get; private set; }
        public int MotionCount => motions.Count;

        sealed class Motion
        {
            public SpriteRenderer sprite;
            public Transform transform;
            public Quaternion baseRotation, lastRotation;
            public Vector3 baseScale, lastScale;
            public float baseAlpha, lastAlpha, phase, period, amplitude;
            public bool fire, applied;
        }

        public static TownEnvironmentV059 Build(Transform parent, TownModularArtV058.Layout layout)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var helper = parent.GetComponent<TownEnvironmentV059>();
            if (helper && helper.built) return helper;
            if (!helper) helper = parent.gameObject.AddComponent<TownEnvironmentV059>();
            helper.Assemble(parent, layout);
            helper.built = true;
            return helper;
        }

        void Assemble(Transform parent, TownModularArtV058.Layout layout)
        {
            resources = parent.GetComponent<TownGeneratedResourcesV058>();
            if (!resources) resources = parent.gameObject.AddComponent<TownGeneratedResourcesV058>();
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (!shader) throw new InvalidOperationException("No unlit vertex-color shader for town ground contacts.");
            material = resources.Track(new Material(shader) { name = "Town v059 soft ground contacts", mainTexture = Texture2D.whiteTexture });
            shadowRoot = new GameObject("Grounding shadows v059 - independent ground meshes").transform;
            shadowRoot.SetParent(parent, false);
            // Escape a possible containing SortingGroup, without changing that group's state.
            var sorting = shadowRoot.gameObject.AddComponent<SortingGroup>();
            sorting.sortAtRoot = true;
            sorting.sortingOrder = ContactOrder;

            foreach (var box in TownLayout.Buildings)
            {
                var footprints = new List<Rect> { ToRect(box) };
                if (box.Name == TownLayout.House.Name) footprints.Add(ToRect(TownLayout.MayorAnnex));
                if (box.Name == TownLayout.Tavern.Name) footprints.Add(ToRect(TownLayout.TavernAnnex));
                if (box.Name == TownLayout.TarpHouse.Name) footprints.Add(ToRect(TownLayout.TarpAnnex));
                // A union perimeter prevents a dark seam where the annex meets its parent.
                BuildingRing(box.Name, footprints, 6f / PixelsPerUnit, .18f);
                foreach (var footprint in footprints)
                    RoundedShadow("Wide cast " + box.Name, footprint.center + new Vector2(.06f, -.09f) * TownLayout.MapScale,
                        footprint.size, 0f, .26f, .065f, CastOrder, false);
                BuildingShadowCount++;
            }

            if (layout.contacts != null)
            foreach (var contact in layout.contacts)
            {
                if (contact == null || contact.width <= 0f || contact.height <= 0f) continue;
                var center = TownModularArtV058.FromPixel(contact.x, contact.y);
                var size = new Vector2(contact.width, contact.height) / PixelsPerUnit;
                string id = contact.id ?? "Contact";
                if (id.IndexOf("Well stone ring", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // The center must stay empty: the well hole is an existing separate ground layer.
                    EllipseShadow(id, center, size, -contact.rotation, .12f, .18f, ContactOrder, true);
                }
                else if (id.IndexOf("hole", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // A void has no object contact filling its center.
                else if (id.IndexOf("rail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         id.IndexOf("post", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         id.IndexOf("feet", StringComparison.OrdinalIgnoreCase) >= 0)
                    RoundedShadow(id, center, size, -contact.rotation, .07f, .19f, ContactOrder, false);
                else
                    EllipseShadow(id, center, size, -contact.rotation, .10f, .20f, ContactOrder, false);
                ContactShadowCount++;
            }

            if (layout.groups == null) return;
            foreach (var group in layout.groups)
            {
                if (group == null || group.parts == null) continue;
                var assembly = ChildNamed(parent, group.assembly);
                var instance = ChildNamed(assembly, group.id);
                foreach (var part in group.parts)
                {
                    if (part == null || string.IsNullOrEmpty(part.sprite)) continue;
                    AddInferredContact(layout, group, part);
                    if (!instance) continue;
                    bool fire = part.sprite == "outdoor-fire";
                    bool leaf = part.sprite == "foliage-canopy-blue" || part.sprite == "foliage-canopy-green";
                    bool cloth = part.sprite == "smith-awning" || part.sprite == "home-tarp" || part.sprite == "detail-clothes";
                    if (!fire && !leaf && !cloth) continue;
                    // Names are matched only within this declared assembly/group. Actors are never searched.
                    for (int i = 0; i < instance.childCount; i++)
                    {
                        var child = instance.GetChild(i);
                        if (child.name != part.sprite) continue;
                        var sr = child.GetComponent<SpriteRenderer>();
                        if (!sr || !animated.Add(sr)) continue;
                        float seed = StableUnit(group.assembly + "/" + group.id + "/" + i);
                        motions.Add(new Motion {
                            sprite = sr, transform = child, fire = fire,
                            baseRotation = child.localRotation, lastRotation = child.localRotation,
                            baseScale = child.localScale, lastScale = child.localScale,
                            baseAlpha = sr.color.a, lastAlpha = sr.color.a,
                            phase = seed * Mathf.PI * 2f,
                            period = fire ? .43f + seed * .29f : leaf ? 8f + seed * 6f : 6f + seed * 4f,
                            amplitude = .2f + seed * .4f
                        });
                    }
                }
            }
        }

        void AddInferredContact(TownModularArtV058.Layout layout, TownModularArtV058.PartGroup group, TownModularArtV058.Part part)
        {
            if (group.role != "body") return;
            float x = part.x + part.width * .5f, y, width, height;
            if (part.sprite == "foliage-trunk")
            {
                y = group.anchorY - 2f;
                width = height = part.width * .53125f; // Existing canopy*.17 / trunk*.32 contact convention.
            }
            else if (part.sprite == "outdoor-rock")
            {
                y = group.anchorY - part.width * .24f;
                width = part.width * .65f; height = part.width * .40f;
            }
            else if (part.sprite == "foliage-shrub")
            {
                y = group.anchorY - part.width * .26f;
                width = part.width * .62f; height = part.width * .34f;
            }
            else return;
            if (width <= 0f || height <= 0f || HasContactNear(layout.contacts, x, y)) return;
            EllipseShadow("Inferred base " + group.id, TownModularArtV058.FromPixel(x, y),
                new Vector2(width, height) / PixelsPerUnit, 0f, .10f, .18f, ContactOrder, false);
            ContactShadowCount++;
        }

        static bool HasContactNear(TownModularArtV058.Contact[] contacts, float x, float y)
        {
            if (contacts == null) return false;
            foreach (var contact in contacts)
                if (contact != null && Mathf.Abs(contact.x - x) <= 4f && Mathf.Abs(contact.y - y) <= 4f) return true;
            return false;
        }

        static Transform ChildNamed(Transform parent, string name)
        {
            if (!parent || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        static Rect ToRect(TownBox box) => new Rect(box.XMin, box.YMin, box.Width, box.Height);

        // Distance to the union is exact outside its component rectangles. Interior cells are omitted.
        // This creates only the small contact fringe, without a full dark polygon over the floor.
        void BuildingRing(string name, List<Rect> footprints, float feather, float alpha)
        {
            var bounds = footprints[0];
            foreach (var rect in footprints)
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                    Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax));
            float step = feather * .5f;
            float left = bounds.xMin - feather, bottom = bounds.yMin - feather;
            int columns = Mathf.CeilToInt((bounds.width + feather * 2f) / step);
            int rows = Mathf.CeilToInt((bounds.height + feather * 2f) / step);
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            for (int iy = 0; iy < rows; iy++)
            for (int ix = 0; ix < columns; ix++)
            {
                var a = new Vector2(left + ix * step, bottom + iy * step);
                var b = a + new Vector2(step, 0f); var c = a + new Vector2(step, step); var d = a + new Vector2(0f, step);
                float da = UnionDistance(a, footprints), db = UnionDistance(b, footprints);
                float dc = UnionDistance(c, footprints), dd = UnionDistance(d, footprints);
                if (Mathf.Max(Mathf.Max(da, db), Mathf.Max(dc, dd)) <= 0f) continue;
                if (Mathf.Min(Mathf.Min(da, db), Mathf.Min(dc, dd)) >= feather) continue;
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                colors.Add(Tint(alpha * Feather(da / feather))); colors.Add(Tint(alpha * Feather(db / feather)));
                colors.Add(Tint(alpha * Feather(dc / feather))); colors.Add(Tint(alpha * Feather(dd / feather)));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            Publish("Building contact union " + name, Vector2.zero, 0f, vertices, colors, triangles, ContactOrder);
        }

        static float UnionDistance(Vector2 point, List<Rect> footprints)
        {
            float distance = float.PositiveInfinity;
            foreach (var rect in footprints)
            {
                Vector2 q = new Vector2(Mathf.Abs(point.x - rect.center.x), Mathf.Abs(point.y - rect.center.y)) - rect.size * .5f;
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                distance = Mathf.Min(distance, d);
            }
            return distance;
        }

        void EllipseShadow(string name, Vector2 center, Vector2 size, float rotation, float feather, float alpha, int order, bool hollow)
        {
            feather *= TownLayout.MapScale; // Size/center already use scaled world units; source feather is a baseline distance.
            const int segments = 32;
            var inner = new List<Vector2>(); var outer = new List<Vector2>();
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                inner.Add(Vector2.Scale(direction, size * .5f));
                outer.Add(Vector2.Scale(direction, size * .5f + Vector2.one * feather));
            }
            PublishFeather(name, center, rotation, inner, outer, alpha, order, hollow);
        }

        void RoundedShadow(string name, Vector2 center, Vector2 size, float rotation, float feather, float alpha, int order, bool hollow)
        {
            feather *= TownLayout.MapScale;
            float radius = Mathf.Min(size.x, size.y) * .24f;
            var inner = RoundedPerimeter(size * .5f, radius);
            var outer = RoundedPerimeter(size * .5f + Vector2.one * feather, radius + feather);
            PublishFeather(name, center, rotation, inner, outer, alpha, order, hollow);
        }

        static List<Vector2> RoundedPerimeter(Vector2 halfSize, float radius)
        {
            var points = new List<Vector2>(24);
            for (int corner = 0; corner < 4; corner++)
            {
                float startAngle = corner * Mathf.PI * .5f;
                var center = new Vector2((corner == 0 || corner == 3 ? 1f : -1f) * (halfSize.x - radius),
                    (corner < 2 ? 1f : -1f) * (halfSize.y - radius));
                for (int step = 0; step <= 5; step++)
                {
                    float angle = startAngle + step * Mathf.PI * .1f;
                    points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            return points;
        }

        void PublishFeather(string name, Vector2 center, float rotation, List<Vector2> inner, List<Vector2> outer, float alpha, int order, bool hollow)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            int count = inner.Count;
            if (!hollow) { vertices.Add(Vector3.zero); colors.Add(Tint(alpha)); }
            int ringStart = vertices.Count;
            for (int i = 0; i < count; i++) { vertices.Add(inner[i]); colors.Add(Tint(alpha)); }
            for (int i = 0; i < count; i++) { vertices.Add(outer[i]); colors.Add(Tint(0f)); }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int a = ringStart + i, b = ringStart + next, c = ringStart + count + next, d = ringStart + count + i;
                if (!hollow) { triangles.Add(0); triangles.Add(a); triangles.Add(b); }
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }
            Publish(name, center, rotation, vertices, colors, triangles, order);
        }

        void Publish(string name, Vector2 center, float rotation, List<Vector3> vertices, List<Color> colors, List<int> triangles, int order)
        {
            if (vertices.Count == 0) return;
            var mesh = resources.Track(new Mesh { name = "Town v059 " + name });
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(.5f, .5f);
            mesh.uv = uv; mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(shadowRoot, false);
            go.transform.localPosition = center; go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.sortingLayerID = SortingLayer.NameToID("Default"); renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            // Each ground order must be global; the parent has a different order for contact shadows.
            var sorting = go.AddComponent<SortingGroup>(); sorting.sortAtRoot = true; sorting.sortingOrder = order;
        }

        static Color Tint(float alpha) => new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, Mathf.Clamp01(alpha));
        static float Feather(float t) { t = Mathf.Clamp01(t); return 1f - t * t * (3f - 2f * t); }
        static float StableUnit(string text)
        {
            uint hash = 2166136261;
            unchecked { foreach (char character in text) { hash ^= character; hash *= 16777619; } }
            return (hash & 0xffffu) / 65535f;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            float time = Time.time;
            foreach (var motion in motions)
            {
                if (!motion.sprite || !motion.transform || !motion.sprite.enabled || motion.sprite.forceRenderingOff ||
                    !motion.sprite.gameObject.activeInHierarchy) continue;
                float wave = Mathf.Sin(time * Mathf.PI * 2f / motion.period + motion.phase);
                if (motion.fire)
                {
                    if ((motion.transform.localScale - motion.lastScale).sqrMagnitude > .0000001f)
                        motion.baseScale = motion.transform.localScale;
                    var color = motion.sprite.color;
                    if (Mathf.Abs(color.a - motion.lastAlpha) > .00001f) motion.baseAlpha = color.a;
                    motion.lastScale = motion.baseScale * (1f + wave * .03f);
                    motion.lastAlpha = motion.baseAlpha * (1f - .06f * (.5f + .5f * wave));
                    motion.transform.localScale = motion.lastScale;
                    color.a = motion.lastAlpha; motion.sprite.color = color;
                }
                else
                {
                    if (Quaternion.Angle(motion.transform.localRotation, motion.lastRotation) > .001f)
                        motion.baseRotation = motion.transform.localRotation;
                    motion.lastRotation = motion.baseRotation * Quaternion.Euler(0f, 0f, wave * motion.amplitude);
                    motion.transform.localRotation = motion.lastRotation;
                }
                motion.applied = true;
            }
        }

        void OnDisable() => RestoreMotions();
        void OnDestroy() => RestoreMotions(); // Meshes/material are owned by TownGeneratedResourcesV058.
        void RestoreMotions()
        {
            foreach (var motion in motions)
            {
                if (!motion.applied || !motion.transform || !motion.sprite) continue;
                if (motion.fire)
                {
                    if ((motion.transform.localScale - motion.lastScale).sqrMagnitude <= .0000001f)
                        motion.transform.localScale = motion.baseScale;
                    var color = motion.sprite.color;
                    if (Mathf.Abs(color.a - motion.lastAlpha) <= .00001f) { color.a = motion.baseAlpha; motion.sprite.color = color; }
                    motion.lastScale = motion.transform.localScale; motion.lastAlpha = motion.sprite.color.a;
                }
                else
                {
                    if (Quaternion.Angle(motion.transform.localRotation, motion.lastRotation) <= .001f)
                        motion.transform.localRotation = motion.baseRotation;
                    motion.lastRotation = motion.transform.localRotation;
                }
                motion.applied = false;
            }
        }
    }
}
