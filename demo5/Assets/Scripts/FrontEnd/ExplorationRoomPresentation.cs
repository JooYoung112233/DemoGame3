using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo5.FrontEnd
{
    /// <summary>Painted-room lighting and walk presentation only. Never advances turns or changes encounter state.</summary>
    [DisallowMultipleComponent]
    public sealed class ExplorationRoomPresentation : MonoBehaviour
    {
        [Serializable] public sealed class RoomLight
        {
            public int Room;
            public string Label;
            [Range(0, 2)] public float AmbientIntensity = .9f;
            public Color AmbientColor = Color.white;
            public Vector2 EntrancePosition;
            [Range(0, 2)] public float EntranceIntensity = .23f;
            public Color EntranceColor = new Color(.78f, .88f, 1);
            [Min(.1f)] public float EntranceRadius = 3;
        }

        [Serializable] public sealed class DoorRoute
        {
            public string Label;
            public int FromRoom, ToRoom;
            [Tooltip("Feet at the outgoing doorway, in the unchanged 19.2 × 10.8 room frame.")]
            public Vector2 DepartureDoor;
            [Tooltip("Open floor before the door. Movement stays in this aisle before approaching the doorway.")]
            public Vector2 FloorWaypoint;
            public Vector2 DepartureQueue = new Vector2(0, -.23f);
            public Vector2 ArrivalDoor;
            public Vector2 ArrivalFormation;
            public Vector2 FormationStep = new Vector2(1.25f, 0);
            public bool CenterFormation;
        }

        public Light2D Ambient, EntranceLight;
        public Transform PawnRoot;
        public RoomLight[] RoomLights = {
            new RoomLight { Room = 0, Label = "오락실 · 오른쪽 출입구", AmbientIntensity = .9f,
                AmbientColor = new Color(.94f,.98f,1), EntrancePosition = new Vector2(7.2f,1.8f) },
            new RoomLight { Room = 1, Label = "복도 · 왼쪽 문", AmbientIntensity = .88f,
                AmbientColor = new Color(.9f,.95f,1), EntrancePosition = new Vector2(-8.5f,1.4f),
                EntranceColor = new Color(.8f,.89f,1), EntranceIntensity = .2f, EntranceRadius = 3.2f },
            new RoomLight { Room = 2, Label = "보관실 · 왼쪽 문", AmbientIntensity = .86f,
                AmbientColor = new Color(.98f,.96f,.89f), EntrancePosition = new Vector2(-8.5f,1.3f),
                EntranceColor = new Color(.95f,.92f,.83f), EntranceIntensity = .22f, EntranceRadius = 3.4f }
        };
        public DoorRoute[] Routes = {
            new DoorRoute { Label = "외부 → 오락실", FromRoom = -1, ToRoom = 0,
                DepartureDoor = new Vector2(8.2f,-.4f), FloorWaypoint = new Vector2(8.05f,-1.95f),
                ArrivalDoor = new Vector2(8.2f,-.4f), ArrivalFormation = new Vector2(0,-1.82f), CenterFormation = true },
            new DoorRoute { Label = "오락실 → 복도", FromRoom = 0, ToRoom = 1,
                DepartureDoor = new Vector2(8.2f,-.4f), FloorWaypoint = new Vector2(8.05f,-1.95f),
                ArrivalDoor = new Vector2(-8.55f,-.7f), ArrivalFormation = new Vector2(-1.8f,-1.72f), CenterFormation = true },
            new DoorRoute { Label = "복도 → 오락실", FromRoom = 1, ToRoom = 0,
                DepartureDoor = new Vector2(-8.55f,-.7f), FloorWaypoint = new Vector2(-7.75f,-1.6f),
                ArrivalDoor = new Vector2(8.2f,-.4f), ArrivalFormation = new Vector2(0,-1.82f), CenterFormation = true },
            new DoorRoute { Label = "복도 → 보관실", FromRoom = 1, ToRoom = 2,
                DepartureDoor = new Vector2(2.2f,.12f), FloorWaypoint = new Vector2(2.2f,-1.45f),
                ArrivalDoor = new Vector2(-8.55f,-.7f), ArrivalFormation = new Vector2(-.5f,-1.75f), CenterFormation = true },
            new DoorRoute { Label = "보관실 → 복도 중앙 철문", FromRoom = 2, ToRoom = 1,
                DepartureDoor = new Vector2(-8.55f,-.7f), FloorWaypoint = new Vector2(-7.75f,-1.6f),
                ArrivalDoor = new Vector2(2.2f,.12f), ArrivalFormation = new Vector2(2.2f,-1.72f), CenterFormation = true }
        };

        readonly List<PawnGroundShadow> shadows = new List<PawnGroundShadow>();
        int currentRoom, pawnCount = -1;

        public DoorRoute Route(int fromRoom, int toRoom)
        {
            if (Routes != null) foreach (var route in Routes)
                if (route != null && route.FromRoom == fromRoom && route.ToRoom == toRoom) return route;
            return null;
        }

        public Vector3 ArrivalPosition(int fromRoom, int toRoom, int index, int count)
        {
            var route = Route(fromRoom, toRoom);
            if (route == null) return toRoom == 0 ? new Vector3(5.8f - index * 1.35f, -2.15f, 0) : new Vector3(-5.6f + index * 1.35f, -1.35f, 0);
            float slot = route.CenterFormation ? index - (Mathf.Max(1, count) - 1) * .5f : index;
            return route.ArrivalFormation + route.FormationStep * slot;
        }

        public void FaceArrival(GameObject pawn, int fromRoom, int toRoom)
        {
            if (!pawn) return;
            var facing = pawn.GetComponent<PawnFacing>();
            var route = Route(fromRoom, toRoom);
            if (!facing) return;
            float dx = route != null ? pawn.transform.localPosition.x - route.ArrivalDoor.x : 0;
            if (Mathf.Abs(dx) > .15f) facing.Face(dx > 0); else facing.ResetTracking();
        }

        // All points are walkable foot positions, not body centers. Legs are distance-weighted to avoid a speed jump at a corner.
        public static Vector3 TravelPoint(DoorRoute route, Vector3 start, int member, float progress)
        {
            if (route == null) return start;
            Vector3 end = route.DepartureDoor + route.DepartureQueue * member;
            float aisleY = Mathf.Min(start.y, route.FloorWaypoint.y);
            Vector3 first = new Vector3(start.x, aisleY, start.z);
            Vector3 second = new Vector3(route.FloorWaypoint.x, aisleY, start.z);
            Vector3 third = new Vector3(route.FloorWaypoint.x, route.FloorWaypoint.y, start.z);
            end.z = start.z;
            float a = Vector3.Distance(start, first), b = Vector3.Distance(first, second), c = Vector3.Distance(second, third), d = Vector3.Distance(third, end);
            float remaining = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress)) * (a + b + c + d);
            if (remaining <= a && a > .0001f) return Vector3.Lerp(start, first, remaining / a);
            remaining -= a;
            if (remaining <= b && b > .0001f) return Vector3.Lerp(first, second, remaining / b);
            remaining -= b;
            if (remaining <= c && c > .0001f) return Vector3.Lerp(second, third, remaining / c);
            remaining -= c;
            return d > .0001f ? Vector3.Lerp(third, end, Mathf.Clamp01(remaining / d)) : end;
        }

        public void ApplyRoom(int room)
        {
            currentRoom = room;
            RoomLight settings = null;
            if (RoomLights != null) foreach (var candidate in RoomLights) if (candidate != null && candidate.Room == room) { settings = candidate; break; }
            if (settings == null) return;
            if (Ambient) { Ambient.intensity = settings.AmbientIntensity; Ambient.color = settings.AmbientColor; }
            if (EntranceLight)
            {
                EntranceLight.transform.localPosition = new Vector3(settings.EntrancePosition.x, settings.EntrancePosition.y, EntranceLight.transform.localPosition.z);
                EntranceLight.intensity = settings.EntranceIntensity; EntranceLight.color = settings.EntranceColor;
                EntranceLight.pointLightOuterRadius = settings.EntranceRadius;
            }
            RefreshShadows();
        }

        void OnEnable() => ApplyRoom(currentRoom);
        void LateUpdate()
        {
            // Newly revealed residents also use the current doorway light. Never changes their placement or state.
            if (PawnRoot && PawnRoot.childCount != pawnCount) RefreshShadows();
        }
        void RefreshShadows()
        {
            shadows.Clear();
            if (!PawnRoot || !EntranceLight) return;
            PawnRoot.GetComponentsInChildren(true, shadows);
            Vector2 lightPosition = EntranceLight.transform.position;
            foreach (var shadow in shadows) if (shadow) shadow.LightPosition = lightPosition;
            pawnCount = PawnRoot.childCount;
        }
    }
}
