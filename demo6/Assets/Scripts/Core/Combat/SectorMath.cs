using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 부채꼴·반원 판정(엔진을 모르는 2D 식). 보스 D 휩쓸기 앞 반원(180°, 반경 3.2), 예고 Telegraph 반원 모양, 등 각도 판정이 함께 쓴다.
    /// 방향 벡터는 길이와 상관없다(0이면 오른쪽). pad는 대상 반지름(플레이어 0.4 등)만큼 넉넉하게 보는 여유다:
    /// 반지름 pad인 원이 부채꼴과 조금이라도 겹치면 안이다(가장자리 반지름 + pad, 옆 경계선에서 pad 안).
    /// BossRuleTests가 경계(반각, 반경 + pad, 반원 모서리)를 지킨다.
    /// </summary>
    public static class SectorMath
    {
        const double RadToDeg = 180.0 / Math.PI;
        const double DegToRad = Math.PI / 180.0;

        /// <summary>두 방향 사이 각(도, 0~180). 한쪽 길이가 0이면 0.</summary>
        public static float AngleBetween(float ax, float ay, float bx, float by)
        {
            double la = Math.Sqrt(ax * (double)ax + ay * (double)ay);
            double lb = Math.Sqrt(bx * (double)bx + by * (double)by);
            if (la < 1e-9 || lb < 1e-9) return 0f;
            double c = (ax * (double)bx + ay * (double)by) / (la * lb);
            if (c > 1) c = 1;
            else if (c < -1) c = -1;
            return (float)(Math.Acos(c) * RadToDeg);
        }

        /// <summary>
        /// 부채꼴 안인가: 중심에서 반경 + pad 안이고, 바라보는 방향과의 각이 반각 안이면 안.
        /// 각이 반각 밖이면 가까운 옆 경계선(중심에서 반경까지의 선분)까지 거리가 pad 이하일 때만 안이다(모서리에서 넉넉하게 보지 않음).
        /// 중심에 pad보다 가까우면 늘 안. 반각 180° 이상이면 원 전체.
        /// </summary>
        public static bool InSector(float cx, float cy, float dirX, float dirY, float radius, float halfAngleDeg, float px, float py, float pad = 0f)
        {
            double dx = px - (double)cx, dy = py - (double)cy;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d > radius + pad) return false;
            if (d <= pad || halfAngleDeg >= 180f) return true;
            if (dirX * dirX + dirY * dirY < 1e-12f)
            {
                dirX = 1f;
                dirY = 0f;
            }
            float angle = AngleBetween(dirX, dirY, (float)dx, (float)dy);
            if (angle <= halfAngleDeg) return true;
            if (pad <= 0f) return false;
            // 가까운 옆 경계선: 바라보는 방향을 점 쪽으로 반각만큼 돌린 선분(길이 반경).
            double baseAngle = Math.Atan2(dirY, dirX);
            double side = dirX * dy - dirY * dx >= 0 ? 1.0 : -1.0;
            double edge = baseAngle + side * halfAngleDeg * DegToRad;
            double ux = Math.Cos(edge), uy = Math.Sin(edge);
            double t = dx * ux + dy * uy;
            if (t < 0) t = 0;
            else if (t > radius) t = radius;
            double ex = dx - ux * t, ey = dy - uy * t;
            return ex * ex + ey * ey <= pad * (double)pad;
        }

        /// <summary>앞 반원(반각 90°) 안인가. 보스 D 휩쓸기와 Telegraph 반원이 쓴다.</summary>
        public static bool InHalfDisc(float cx, float cy, float dirX, float dirY, float radius, float px, float py, float pad = 0f) =>
            InSector(cx, cy, dirX, dirY, radius, 90f, px, py, pad);

        /// <summary>
        /// 등 뒤인가: 바라보는 방향과 (점 − 중심) 사이 각이 180° − 반각 이상. 반각 60°면 뒤쪽 120°(±60°).
        /// 기습 처형과 단검 등 찌르기가 BackstabRule.IsBehind를 거쳐 이 식을 쓴다.
        /// </summary>
        public static bool IsBehind(float facingX, float facingY, float toPointX, float toPointY, float halfAngleDeg)
        {
            if (toPointX * toPointX + toPointY * toPointY < 1e-12f) return false;
            if (facingX * facingX + facingY * facingY < 1e-12f) return false;
            return AngleBetween(facingX, facingY, toPointX, toPointY) >= 180f - halfAngleDeg;
        }
    }
}
