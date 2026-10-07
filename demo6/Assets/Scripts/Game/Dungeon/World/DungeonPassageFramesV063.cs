using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Visual-only timber passage supports. Hidden routes receive no frame until opened.</summary>
    public sealed class DungeonPassageFramesV063 : MonoBehaviour
    {
        const float BeamSpan = 5f;
        const float BeamDepth = .34f;
        const float PostSize = .6f;
        const float PostAlong = 2.3f;
        const float CollarAlong = 2.25f;
        const int OverheadOffset = 120;
        readonly HashSet<DungeonEdge> _edges = new HashSet<DungeonEdge>();
        DungeonWorld _world;
        Transform _frameOwner;

        public static DungeonPassageFramesV063 Attach(DungeonWorld world, Transform floorRoot)
        {
            if (world == null || !floorRoot) return null;
            var binder = floorRoot.GetComponent<DungeonPassageFramesV063>();
            if (!binder) binder = floorRoot.gameObject.AddComponent<DungeonPassageFramesV063>();
            binder._world = world;
            binder._frameOwner = floorRoot;
            binder._edges.Clear();
            foreach (var edge in world.Edges) binder._edges.Add(edge);
            // LockedDoor can construct its frame before DungeonWorld.Build has returned.
            // Adopt only markers belonging to this exact world; no names or global static cache.
            var searchRoot = floorRoot.parent ? floorRoot.parent : floorRoot;
            foreach (var marker in searchRoot.GetComponentsInChildren<DungeonPassageFrameV063>(true))
                if (marker && binder._edges.Contains(marker.Edge)) marker.transform.SetParent(floorRoot, true);
            binder.RefreshEligible();
            return binder;
        }

        void OnEnable()
        {
            DungeonEvents.EdgeOpened -= OnEdgeOpened;
            DungeonEvents.EdgeOpened += OnEdgeOpened;
            RefreshEligible();
        }

        void OnDisable() => DungeonEvents.EdgeOpened -= OnEdgeOpened;
        void OnDestroy() => DungeonEvents.EdgeOpened -= OnEdgeOpened;

        void RefreshEligible()
        {
            if (_world == null || !_frameOwner) return;
            foreach (var edge in _world.Edges) TryBuildFrame(edge, _frameOwner);
        }

        void OnEdgeOpened(DungeonEdge edge)
        {
            // BossArena can notify this event again; an existing edge marker makes this idempotent.
            if (_frameOwner && edge != null && _edges.Contains(edge)) TryBuildFrame(edge, _frameOwner);
        }

        public static bool TryBuildLockedFrame(DungeonEdge edge, Transform parent)
            => edge != null && edge.Map != null && edge.Kind == EdgeKind.Locked && TryBuildFrame(edge, parent);

        public static bool TryBuildFrame(DungeonEdge edge, Transform parent)
        {
            if (edge == null || edge.Map == null ||
                !(edge.Opened || edge.Kind == EdgeKind.Open || edge.Kind == EdgeKind.Locked)) return false;
            var existing = FindFrame(edge, parent);
            if (existing && !existing.IsLegacy) return true;
            if (!TryLoad(out var sprites)) return false;

            string a = edge.A != null && edge.A.Map != null ? edge.A.Id : "A";
            string b = edge.B != null && edge.B.Map != null ? edge.B.Id : "B";
            var root = new GameObject("Dungeon passage frame " + a + "--" + b).transform;
            root.SetParent(parent, false);
            root.position = edge.DoorCenter;
            var marker = root.gameObject.AddComponent<DungeonPassageFrameV063>();
            marker.Configure(edge, false);

            // These are painted contact shadows only. No collider, light, FOV registration or shadow caster.
            foreach (float side in new[] { -1f, 1f })
            {
                var at = new Vector2(side * PostAlong, 0f);
                var shadow = PropsV062Art.DoorPart(root, side < 0f ? "Post A contact shadow" : "Post B contact shadow",
                    WorldProps.SoftDot, edge, at, new Vector2(.84f, .78f), WorldProps.FloorDecalOrder - 1);
                shadow.Renderer.color = new Color(0f, 0f, 0f, .22f);
                float groundY = root.TransformPoint(WorldProps.DoorLocal(edge, at.x, at.y)).y;
                PropsV062Art.DoorPart(root, side < 0f ? "Post A cap" : "Post B cap", sprites[1], edge,
                    at, new Vector2(PostSize, PostSize), WorldProps.SortY(groundY, 50));
            }
            float width = Mathf.Max(edge.DoorSize.x, edge.DoorSize.y);
            float thickness = Mathf.Min(edge.DoorSize.x, edge.DoorSize.y);
            PropsV062Art.DoorPart(root, "Ground threshold", sprites[3], edge, Vector2.zero,
                new Vector2(width, thickness), WorldProps.FloorDecalOrder);

            var beam = PropsV062Art.DoorPart(root, "Overhead beam", sprites[0], edge, Vector2.zero,
                new Vector2(BeamSpan, BeamDepth), 0);
            // Ceiling timber stays above actors underneath. A vertical beam spans five world units,
            // so use its lower bound, not its center, before adding the overhead height allowance.
            int overheadOrder = WorldProps.SortY(beam.Renderer.bounds.min.y, OverheadOffset);
            beam.Renderer.sortingOrder = overheadOrder;
            foreach (float side in new[] { -1f, 1f })
                PropsV062Art.DoorPart(root, side < 0f ? "Beam collar A" : "Beam collar B", sprites[2], edge,
                    new Vector2(side * CollarAlong, 0f), new Vector2(.16f, BeamDepth), overheadOrder + 1);

            if (existing)
            {
                // A complete new frame replaces the legacy v062 frame atomically; missing resources keep it intact.
                existing.gameObject.SetActive(false);
                Object.Destroy(existing.gameObject);
            }
            return true;
        }

        public static void RegisterLegacyFrame(DungeonEdge edge, Transform frame)
        {
            if (edge == null || !frame) return;
            var marker = frame.GetComponent<DungeonPassageFrameV063>();
            if (!marker) marker = frame.gameObject.AddComponent<DungeonPassageFrameV063>();
            marker.Configure(edge, true);
        }

        static DungeonPassageFrameV063 FindFrame(DungeonEdge edge, Transform parent)
        {
            var markers = parent ? parent.GetComponentsInChildren<DungeonPassageFrameV063>(true) :
                Object.FindObjectsByType<DungeonPassageFrameV063>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            DungeonPassageFrameV063 legacy = null;
            foreach (var marker in markers)
            {
                if (!marker || marker.Edge != edge) continue;
                if (!marker.IsLegacy) return marker;
                legacy = marker;
            }
            return legacy;
        }

        static bool TryLoad(out Sprite[] sprites)
        {
            string[] paths = { "PassageFramesV063/beam", "PassageFramesV063/post-cap", "PassageFramesV063/collar", "PropsV062/door-threshold" };
            sprites = new Sprite[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                var sprite = Resources.Load<Sprite>(paths[i]);
                if (!sprite || sprite.bounds.size.x <= .0001f || sprite.bounds.size.y <= .0001f)
                {
                    sprites = null;
                    return false;
                }
                sprites[i] = sprite;
            }
            return true;
        }
    }

    /// <summary>Exact edge identity for ownership and duplicate prevention; carries no gameplay state.</summary>
    public sealed class DungeonPassageFrameV063 : MonoBehaviour
    {
        public DungeonEdge Edge { get; private set; }
        public bool IsLegacy { get; private set; }
        public void Configure(DungeonEdge edge, bool legacy) { Edge = edge; IsLegacy = legacy; }
    }
}
