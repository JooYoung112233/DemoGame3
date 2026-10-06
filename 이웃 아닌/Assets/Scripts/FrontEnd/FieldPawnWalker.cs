using UnityEngine;

namespace Demo5.FrontEnd
{
    // 말 걷기 (기획/탐험-말놓기-조작-재설계.md): a party pawn walks in a straight line to the spot its plan puts it at (FieldPawnBoard
    // decides the target; the plan is the only truth, this only moves the pawn to match it). Unscaled time, about Speed units a
    // second but never longer than MaxSeconds, with a small lean of the body (restored on arrival, as SettlementPawnMotion does);
    // on arrival it turns to the object (PawnFacing.Face). It gives way to the room move (ExpeditionRoomNavigation.Travel owns the
    // pawns while arrival.InTransit), and stands still while Frozen (an encounter or a battle). Added at runtime by the board;
    // presentation only: moving never changes the plan, time or the site.
    [DisallowMultipleComponent]
    public sealed class FieldPawnWalker : MonoBehaviour
    {
        [Tooltip("걷는 속도 (방 좌표 / 초 · 실제 시간)")] [Min(.1f)] public float Speed = 3.5f;
        [Tooltip("한 번 걷는 가장 긴 시간 (초) · 멀면 빨라짐")] [Min(.05f)] public float MaxSeconds = .9f;
        [Tooltip("걷는 동안 몸 기울기 (도)")] [Range(0, 8)] public float LeanDegrees = 2.5f;
        [Tooltip("도착해서 사물 쪽으로 돌아서는 가장 작은 좌우 거리")] [Min(0)] public float TurnThreshold = .15f;
        public ExpeditionArrivalPanel Arrival;
        public Transform Body;
        public PawnFacing Facing;

        public bool Walking { get; private set; }
        // Where the pawn is going (or stands), in its parent's space (the room frame).
        public Vector3 Target { get; private set; }
        public bool HasTarget { get; private set; }
        // An encounter or a battle is open: stand still where the pawn is.
        public bool Frozen { get; set; }
        public float LookAtX => lookAt;

        Vector3 from; float elapsed, duration, lookAt = float.NaN; Quaternion rest = Quaternion.identity; bool leaning;

        void Awake()
        {
            if (!Body) Body = transform.Find("Body");
            if (!Facing) Facing = GetComponent<PawnFacing>();
        }
        // Walk to `feet` (parent space) and face `lookAtX` on arrival (NaN: keep the facing the walk left).
        public void WalkTo(Vector3 feet, float lookAtX)
        {
            feet.z = transform.localPosition.z;
            if (HasTarget && (feet - Target).sqrMagnitude < 1e-6f) { lookAt = lookAtX; if (!Walking) FaceTarget(); return; }
            Target = feet; HasTarget = true; lookAt = lookAtX; from = transform.localPosition;
            float distance = Vector3.Distance(from, feet);
            if (distance < .005f) { Arrive(); return; }
            duration = Mathf.Min(distance / Mathf.Max(.1f, Speed), Mathf.Max(.05f, MaxSeconds)); elapsed = 0;
            if (!Walking) { rest = Body ? Body.localRotation : Quaternion.identity; leaning = Body; }
            Walking = true;
        }
        // Straight to the target ('턴 진행' never waits for a walk).
        public void Snap() { if (!Walking) return; transform.localPosition = Target; Arrive(); }
        // Drop the target where the pawn stands (the room move takes the pawns, a new visit).
        public void Stop()
        {
            if (Walking) Unlean();
            Walking = false; HasTarget = false; Target = transform.localPosition; lookAt = float.NaN;
        }

        void Update()
        {
            if (!Walking) return;
            if (Arrival && Arrival.InTransit) { Stop(); return; }
            if (Frozen) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.0001f, duration)), ease = t * t * (3 - 2 * t);
            transform.localPosition = Vector3.Lerp(from, Target, ease);
            if (Body && leaning)
            {
                float direction = Mathf.Abs(Target.x - from.x) > .01f ? Mathf.Sign(Target.x - from.x) : 1;
                Body.localRotation = rest * Quaternion.Euler(0, 0, -direction * LeanDegrees * Mathf.Sin(t * Mathf.PI));
            }
            if (t >= 1) Arrive();
        }
        void Arrive()
        {
            transform.localPosition = Target; Unlean(); Walking = false; FaceTarget();
        }
        void FaceTarget()
        {
            if (!Facing || float.IsNaN(lookAt)) return;
            float dx = lookAt - transform.localPosition.x;
            if (Mathf.Abs(dx) > TurnThreshold) Facing.Face(dx > 0);
        }
        void Unlean() { if (Body && leaning) Body.localRotation = rest; leaning = false; }
        void OnDisable() { if (Walking) { transform.localPosition = Target; Unlean(); Walking = false; } }
    }
}
