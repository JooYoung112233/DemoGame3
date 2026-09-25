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

        // 말 놓기 (기획/탐험-말놓기-조작-재설계.md): where a placed member pawn stands. Room + Key name one place ("search:N" an object,
        // "door:R" the door onto room R, "observe:ID" a trace; the office door holds both "door:3" and "search:8"). Feet in the
        // 19.2 × 10.8 room frame (the pawn root's local space).
        [Serializable] public sealed class WorkSpot
        {
            [Tooltip("이름 (Inspector에서 알아보기 위한 것)")] public string Label;
            [Tooltip("방 (0 오락실 · 1 복도 · 2 보관실)")] public int Room;
            [Tooltip("자리 이름: search:사물 번호 · door:문 너머 방 · observe:흔적 ID")] public string Key;
            [Tooltip("발 위치 (방 좌표) · [0] 수색 담당 · 귀 대기 · 먼저 모인 대원, [1] 협동 · 다음에 모인 대원 (문은 여섯까지, 모자라면 마지막 간격으로 이어 붙임)")]
            public Vector2[] Slots = new Vector2[0];
            [Tooltip("도착하면 바라보는 x (사물 · 문의 가운데)")] public float LookAtX;
        }
        // One room's floor: the band feet may stand in (x, y = its lower left, in the room frame) and where free members wait.
        [Serializable] public sealed class RoomFloor
        {
            [Tooltip("이름 (Inspector에서 알아보기 위한 것)")] public string Label;
            [Tooltip("방 (0 오락실 · 1 복도 · 2 보관실)")] public int Room;
            [Tooltip("발이 설 수 있는 바닥 (방 좌표 · x, y = 왼쪽 아래 · 아래 끝은 대원 카드 줄 위 통로)")] public Rect Band = new Rect(-8.8f, -1.95f, 17.6f, 1.65f);
            [Tooltip("할 일이 없는 대원이 서는 자리 (방 좌표)")] public Vector2[] Idle = new Vector2[0];
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
        [Header("대원 말 놓기 · 일하는 자리와 바닥 (BuildPawnBoard는 없는 것만 채움 · 여기서 조정)")]
        [Tooltip("사물 · 문 · 흔적 앞에 서는 자리")] public WorkSpot[] Spots = new WorkSpot[0];
        [Tooltip("방마다 발이 설 수 있는 바닥과 할 일 없는 대원의 자리")] public RoomFloor[] Floors = new RoomFloor[0];
        [Tooltip("두 말이 겹치지 않는 간격 (좌우로 x 이상, 또는 앞뒤로 y 이상 떨어짐 · 받침대 폭 약 1.02)")] public Vector2 MinGap = new Vector2(1.05f, .4f);
        // A room with no Floors entry: the travel aisle up to below the back wall.
        public static readonly Rect DefaultBand = new Rect(-8.8f, -1.95f, 17.6f, 1.65f);

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

        // ---- 말 놓기: spots, floor, free members (presentation only; FieldPawnBoard reads these) ----
        public WorkSpot SpotOf(int room, string key)
        {
            if (Spots != null && !string.IsNullOrEmpty(key)) foreach (var s in Spots) if (s != null && s.Room == room && s.Key == key) return s;
            return null;
        }
        public RoomFloor FloorOf(int room)
        {
            if (Floors != null) foreach (var f in Floors) if (f != null && f.Room == room) return f;
            return null;
        }
        public Rect BandOf(int room) { var f = FloorOf(room); return f != null && f.Band.width > 0 && f.Band.height > 0 ? f.Band : DefaultBand; }
        // Where slot `slot` of that place is (clamped into the room's floor band) and the x the pawn turns to on arrival. A door queue
        // longer than its authored slots continues with its last step. False when the place has no authored spot (the board then
        // falls back to the object's painted bottom edge).
        public bool TrySpot(int room, string key, int slot, out Vector3 feet, out float lookAtX)
        {
            feet = default; lookAtX = float.NaN;
            var s = SpotOf(room, key); if (s == null || s.Slots == null || s.Slots.Length == 0 || slot < 0) return false;
            int n = s.Slots.Length; Vector2 p;
            if (slot < n) p = s.Slots[slot];
            else { var step = n > 1 ? s.Slots[n - 1] - s.Slots[n - 2] : new Vector2(-MinGap.x, 0); p = s.Slots[n - 1] + step * (slot - n + 1); }
            feet = ClampFloor(room, p); lookAtX = s.LookAtX; return true;
        }
        public bool TrySpot(int room, string key, int slot, out Vector3 feet, out bool faceRight)
        {
            bool ok = TrySpot(room, key, slot, out feet, out float look); faceRight = ok && look > feet.x; return ok;
        }
        // Edges included (Rect.Contains leaves out the top and right edge, where ClampFloor puts a point and float sums like −1.95 + 1.75 land).
        public bool OnFloor(int room, Vector2 p) { var b = BandOf(room); const float e = 1e-4f; return p.x >= b.xMin - e && p.x <= b.xMax + e && p.y >= b.yMin - e && p.y <= b.yMax + e; }
        public Vector3 ClampFloor(int room, Vector2 p) { var b = BandOf(room); return new Vector3(Mathf.Clamp(p.x, b.xMin, b.xMax), Mathf.Clamp(p.y, b.yMin, b.yMax), 0); }
        // Two feet far enough apart that the bases do not overlap (side by side, or a row apart).
        public bool Apart(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) >= MinGap.x - .001f || Mathf.Abs(a.y - b.y) >= MinGap.y - .001f;
        bool Clear(Vector2 p, IReadOnlyList<Vector3> taken)
        {
            if (taken != null) for (int i = 0; i < taken.Count; i++) if (!Apart(p, taken[i])) return false;
            return true;
        }
        List<Vector2> IdleOf(int room) { var list = new List<Vector2>(); var f = FloorOf(room); if (f != null && f.Idle != null) list.AddRange(f.Idle); return list; }
        // The free-standing spot for a member (its own idle spot in member order, or the next one clear of `taken`).
        public Vector3 IdleFor(int room, int member, IReadOnlyList<Vector3> taken)
        {
            var idle = IdleOf(room); if (idle.Count == 0) return NearestClear(room, BandOf(room).center, taken);
            int start = Mathf.Max(0, member);
            for (int i = 0; i < idle.Count; i++) { var p = idle[(start + i) % idle.Count]; if (Clear(p, taken)) return ClampFloor(room, p); }
            return NearestClear(room, idle[start % idle.Count], taken);
        }
        // The idle spot nearest `from` that is clear of `taken` (a member let go from a work spot walks there).
        public Vector3 NearestIdle(int room, Vector3 from, IReadOnlyList<Vector3> taken)
        {
            var idle = IdleOf(room); float best = float.MaxValue; Vector3 pick = default; bool found = false;
            foreach (var p in idle)
            {
                if (!Clear(p, taken)) continue;
                float d = ((Vector2)from - p).sqrMagnitude; if (d < best) { best = d; pick = ClampFloor(room, p); found = true; }
            }
            return found ? pick : NearestClear(room, from, taken);
        }
        // `from` if it is clear of `taken`, else the nearest clear point along the same row, else the nearest clear idle spot.
        public Vector3 NearestClear(int room, Vector3 from, IReadOnlyList<Vector3> taken)
        {
            var start = ClampFloor(room, from); if (Clear(start, taken)) return start;
            float step = Mathf.Max(.2f, MinGap.x * .5f);
            for (int k = 1; k <= 36; k++)
                for (int side = 1; side >= -1; side -= 2)
                {
                    var p = ClampFloor(room, new Vector2(start.x + side * k * step, start.y));
                    if (Clear(p, taken)) return p;
                }
            float best = float.MaxValue; Vector3 pick = start;
            foreach (var p in IdleOf(room)) { if (!Clear(p, taken)) continue; float d = ((Vector2)start - p).sqrMagnitude; if (d < best) { best = d; pick = ClampFloor(room, p); } }
            return pick;
        }
        // How clear a floor point is of the pawns standing or walking in this room (their feet and walk targets): the horizontal
        // distance, where standing a row apart (MinGap.y) counts as MinGap.x. The resident's spot picks the clearer one of its two.
        // ignore: that pawn itself. `room` is the party's room (every pawn under PawnRoot stands in it).
        public float ClearanceAt(int room, Vector3 feet, Transform ignore = null)
        {
            float d = float.MaxValue; if (!PawnRoot) return d;
            foreach (Transform t in PawnRoot)
            {
                if (!t || t == ignore || !t.gameObject.activeSelf) continue;
                d = Mathf.Min(d, Gap(feet, t.localPosition));
                var w = t.GetComponent<FieldPawnWalker>(); if (w && w.Walking) d = Mathf.Min(d, Gap(feet, w.Target));
            }
            return d;
        }
        float Gap(Vector2 a, Vector2 b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y) * MinGap.x / Mathf.Max(.01f, MinGap.y));

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

        // Scene view: the floor bands (grey), idle spots (white) and every place's slots (gold = the first) with the x it looks at.
        void OnDrawGizmosSelected()
        {
            var frame = PawnRoot ? PawnRoot : transform;
            Vector3 W(Vector2 p) => frame.TransformPoint(new Vector3(p.x, p.y, 0));
            if (Floors != null)
                foreach (var f in Floors)
                {
                    if (f == null) continue;
                    Gizmos.color = new Color(.7f, .7f, .7f, .8f); var b = f.Band;
                    Gizmos.DrawLine(W(new Vector2(b.xMin, b.yMin)), W(new Vector2(b.xMax, b.yMin))); Gizmos.DrawLine(W(new Vector2(b.xMax, b.yMin)), W(new Vector2(b.xMax, b.yMax)));
                    Gizmos.DrawLine(W(new Vector2(b.xMax, b.yMax)), W(new Vector2(b.xMin, b.yMax))); Gizmos.DrawLine(W(new Vector2(b.xMin, b.yMax)), W(new Vector2(b.xMin, b.yMin)));
                    Gizmos.color = Color.white; if (f.Idle != null) foreach (var p in f.Idle) Gizmos.DrawWireSphere(W(p), .12f);
                }
            if (Spots != null)
                foreach (var s in Spots)
                {
                    if (s == null || s.Slots == null) continue;
                    for (int i = 0; i < s.Slots.Length; i++)
                    {
                        Gizmos.color = i == 0 ? new Color(1, .8f, .3f, 1) : new Color(1, .9f, .6f, .7f);
                        Gizmos.DrawWireSphere(W(s.Slots[i]), .18f);
                        if (i == 0) Gizmos.DrawLine(W(s.Slots[i]), W(new Vector2(s.LookAtX, s.Slots[i].y + .4f)));
                    }
#if UNITY_EDITOR
                    if (s.Slots.Length > 0) UnityEditor.Handles.Label(W(s.Slots[0] + new Vector2(0, .35f)), s.Room + " " + s.Key + (string.IsNullOrEmpty(s.Label) ? "" : " · " + s.Label));
#endif
                }
        }
    }
}
