using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 판자벽 부수기·벽 '퉁' 판정(3차 초안 2-5)에 쓰는 도형 계산.
    /// 콤보 단계 모양(부채꼴·직선 띠·원)을 PlayerController의 적 판정과 같은 뜻으로 보되, 벽은 몸 반지름이 없으므로 여유(slack)를 따로 준다.
    /// </summary>
    public static class WorldGeometry
    {
        /// <summary>부채꼴 가장자리 여유(도). 판자벽 '대충 앞에 있으면 맞음'.</summary>
        const float ArcSlackDeg = 15f;

        public static Rect DoorRect(DungeonEdge edge) => new Rect(edge.DoorCenter - edge.DoorSize * 0.5f, edge.DoorSize);

        public static Vector2 Closest(Rect r, Vector2 p) => new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));

        public static float Distance(Rect r, Vector2 p) => (Closest(r, p) - p).magnitude;

        /// <summary>점 p가 콤보 단계 판정 안에 있는가(slack만큼 넉넉하게).</summary>
        public static bool InStep(Vector2 p, Vector2 origin, Vector2 dir, ComboStep step, float slack)
        {
            if (step == null) return false;
            dir = Dir(dir);
            Vector2 v = p - origin;
            switch (step.shape)
            {
                case ComboShape.Line:
                {
                    float along = Vector2.Dot(v, dir);
                    float across = Mathf.Abs(Vector2.Dot(v, new Vector2(-dir.y, dir.x)));
                    return along >= -slack && along <= step.size + slack && across <= step.width * 0.5f + slack;
                }
                case ComboShape.Circle:
                    return (p - (origin + dir * step.centerOffset)).magnitude <= step.size + slack;
                default:
                {
                    float d = v.magnitude;
                    if (d > step.size + slack) return false;
                    if (d <= PlayerController.Radius + slack) return true;
                    float arc = step.arcDeg > 0f ? step.arcDeg : 90f;
                    return Vector2.Angle(dir, v) <= arc * 0.5f + ArcSlackDeg;
                }
            }
        }

        /// <summary>사각형의 어느 부분이라도 콤보 단계 판정에 닿는가. 가장 가까운 점과 사각형 위 격자 점(긴 쪽 9 × 짧은 쪽 3)을 본다.</summary>
        public static bool StepTouchesRect(Rect r, Vector2 origin, Vector2 dir, ComboStep step, float slack)
        {
            if (step == null) return false;
            dir = Dir(dir);
            if (step.shape == ComboShape.Circle)
            {
                Vector2 c = origin + dir * step.centerOffset;
                return Distance(r, c) <= step.size + slack;
            }
            if (InStep(Closest(r, origin), origin, dir, step, slack)) return true;
            bool wide = r.width >= r.height;
            int nx = wide ? 8 : 2;
            int ny = wide ? 2 : 8;
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= ny; j++)
            {
                var p = new Vector2(Mathf.Lerp(r.xMin, r.xMax, i / (float)nx), Mathf.Lerp(r.yMin, r.yMax, j / (float)ny));
                if (InStep(p, origin, dir, step, slack)) return true;
            }
            return false;
        }

        /// <summary>
        /// 충돌체의 어느 부분이라도 콤보 단계 판정에 닿는가(벽 '퉁'). 판정 가장자리 몇 점에서 충돌체의 가장 가까운 점을 찾아 본다.
        /// 판정 끝점이 벽 안에 있으면 그 점 자체가 돌아와 닿은 것으로 본다.
        /// </summary>
        public static bool StepTouchesCollider(Collider2D col, Vector2 origin, Vector2 dir, ComboStep step, float slack)
        {
            if (!col || step == null) return false;
            dir = Dir(dir);
            if (step.shape == ComboShape.Circle)
            {
                Vector2 c = origin + dir * step.centerOffset;
                return (col.ClosestPoint(c) - c).magnitude <= step.size + slack;
            }
            if (InStep(col.ClosestPoint(origin), origin, dir, step, slack)) return true;
            if (step.shape == ComboShape.Line)
            {
                Vector2 side = new Vector2(-dir.y, dir.x) * (step.width * 0.5f);
                for (int i = 1; i <= 4; i++)
                {
                    Vector2 q = origin + dir * (step.size * i / 4f);
                    if (InStep(col.ClosestPoint(q), origin, dir, step, slack)) return true;
                    if (InStep(col.ClosestPoint(q + side), origin, dir, step, slack)) return true;
                    if (InStep(col.ClosestPoint(q - side), origin, dir, step, slack)) return true;
                }
                return false;
            }
            float arc = step.arcDeg > 0f ? Mathf.Min(step.arcDeg, 360f) : 90f;
            for (int k = -3; k <= 3; k++)
            {
                Vector2 ray = WorldProps.Rotate(dir, arc * 0.5f * k / 3f);
                for (int s = 1; s <= 2; s++)
                {
                    Vector2 q = origin + ray * (step.size * s / 2f);
                    if (InStep(col.ClosestPoint(q), origin, dir, step, slack)) return true;
                }
            }
            return false;
        }

        /// <summary>판정이 닿을 수 있는 가장 먼 거리를 감싸는 원(넓게 고르기용).</summary>
        public static void Bounds(Vector2 origin, Vector2 dir, ComboStep step, out Vector2 center, out float radius)
        {
            dir = Dir(dir);
            if (step.shape == ComboShape.Circle)
            {
                center = origin + dir * step.centerOffset;
                radius = step.size;
                return;
            }
            center = origin;
            radius = step.shape == ComboShape.Line ? step.size + step.width * 0.5f : step.size;
        }

        static Vector2 Dir(Vector2 dir) => dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized;
    }
}
