using System;
using Demo6.Core.Combat;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 시야와 문 1차(기획/시야와-문-1차.md 3·4·5장)의 수와 판정, 화면 글. 시야(VisionSystem)·등 뒤 기척(BehindSounds)·살핌 경험치가 이 값을 읽는다.
    /// 3장 보는 칸 가두기: 시야 광선은 보는 칸 경계 0.5 밖(벽 바깥 면)까지만 가고, 몸이 경계를 0.3 넘게 지나야 보는 칸이 바뀌며,
    /// 바뀐 순간부터 0.25초 동안 부채꼴이 펼쳐진다.
    /// 4장 적은 부채꼴 안에서만: 반각 + 5° + 몸 각도 안이면 들어오고, 한 번 보인 적은 6° 더 벗어나야 숨는다. 보스방만 예외(방 안 360°).
    /// 5장 칸 살핌: 걸을 수 있는 바닥의 75%를 눈으로 훑으면 '살폈다'와 경험치(보스방은 들어설 때 그대로).
    /// 글은 모두 이 한 곳에 모은다(6장, 주민 이름 없음).
    /// </summary>
    public static class SightRules
    {
        // ── 3장 보는 칸 ──
        /// <summary>시야 광선이 보는 칸 경계 밖으로 더 가는 거리(벽 두께 1의 바깥 면, 3-1).</summary>
        public const float ClipPad = 0.5f;
        /// <summary>몸 가운데가 보는 칸 경계를 이만큼 넘게 지나야 보는 칸이 바뀐다(문턱, 3-2).</summary>
        public const float SwitchPad = 0.3f;
        /// <summary>보는 칸이 바뀐 뒤 부채꼴이 몸 둘레 반경에서 전체 반경까지 펼쳐지는 시간(실제 초, 0이면 바로, 3-3).</summary>
        public const float RevealSeconds = 0.25f;

        // ── 4장 적 보이기 ──
        /// <summary>적이 들어올 때 부채꼴 반각에 더하는 옆면 절반(도, 4-1).</summary>
        public const float ConeEnterPad = 5f;
        /// <summary>한 번 보인 적이 숨으려면 더 벗어나야 하는 각도(도, 4-2).</summary>
        public const float ConeExitExtra = 6f;
        /// <summary>몸 둘레에서 적을 늘 보이게 하는 반경. 0 = 없음(몸 둘레 2.5 원은 바닥·벽만 보여 준다, 4-1).</summary>
        public const float NearEnemyRadius = 0f;
        /// <summary>안 보이는 적이 나를 때리면 보이는 시간(실제 초, 4-3).</summary>
        public const float RevealOnHurtSeconds = 0.8f;
        /// <summary>내가 직접 친 적이 보이는 시간(실제 초, 4-3).</summary>
        public const float RevealOnHitSeconds = 0.5f;
        /// <summary>
        /// 나를 때린 적을 찾는 거리: 맞은 쪽에서 몸 반경 + 0.3 안 가장 가까운 적(4-3). 근접 공격(굴쥐, 돌충이 박치기·발차기·돌진, 오우거 휩쓸기)은
        /// 자기 가운데를 맞은 쪽으로 넘기므로 이만큼이면 찾는다. 예전 1.6은 화살·덫 자리(플레이어 몸 바로 앞)에서 1.6 안에 있던 다른 적(등 뒤 굴쥐)을
        /// 드러내 정보가 샜다(물결 2 고침).
        /// </summary>
        public const float HurtRevealReach = 0.3f;

        // ── 5장 살핌 ──
        /// <summary>칸을 살핀 것으로 치는 걸을 수 있는 바닥 몫(5-3).</summary>
        public const float SweepFraction = 0.75f;

        // ── 4-6 소리 단서 ──
        /// <summary>등 뒤 기척: 깬 적이 안 보이고 이 거리 안일 때.</summary>
        public const float BehindRange = 9f;
        /// <summary>깨는 기척: 안 보이는 적이 깨어날 때 이 거리 안.</summary>
        public const float WakeCueRange = 12f;
        /// <summary>예고 기척: 안 보이는 적이 예고를 시작할 때 이 거리 안.</summary>
        public const float TelegraphCueRange = 14f;
        /// <summary>등 뒤 기척 후보를 훑는 간격(초).</summary>
        public const float BehindScanSeconds = 0.2f;
        /// <summary>새로 든 적의 첫 소리까지(초).</summary>
        public const float BehindFirstMin = 0.15f;
        public const float BehindFirstMax = 0.35f;
        /// <summary>무리가 한꺼번에 깨면 깨는 기척 사이 간격(초).</summary>
        public const float WakeCueSpacing = 0.3f;
        /// <summary>한 번에 내는 등 뒤 기척 수(가까운 순).</summary>
        public const int BehindVoices = 2;
        /// <summary>돌충이 기척은 긁기를 낮게(음높이 × 0.65, 4-6).</summary>
        public const float BeetlePitch = 0.65f;
        /// <summary>깨는 기척 크기(× 1.3), 예고 기척 크기(× 1.2).</summary>
        public const float WakeCueLevel = 1.3f;
        public const float TelegraphCueLevel = 1.2f;

        // ── 6장 화면 글 ──
        /// <summary>칸을 살폈을 때 플레이어 머리 위 작은 글(옆에 '+n').</summary>
        public const string SweptWord = "살폈다";
        /// <summary>첫 급습 안내(알림 줄, 한 판 한 번, 4-9).</summary>
        public const string AmbushHint = "등 뒤는 보이지 않는다. 긁는 소리가 나면 돌아봐라.";

        /// <summary>
        /// 원점 (ox, oy)에서 방향 (dx, dy)로 사각형을 나가는 거리(3-1 광선 칸 가두기). 방향은 길이와 관계없이 단위로 맞춰 잰다.
        /// 한 성분이 0이면 그 축은 나가지 않는다. 원점이 경계 위·밖이거나 방향이 0이면 0.
        /// </summary>
        public static float ExitDistance(float ox, float oy, float dx, float dy, float xMin, float yMin, float xMax, float yMax)
        {
            if (!(ox > xMin && ox < xMax && oy > yMin && oy < yMax)) return 0f;
            double len = Math.Sqrt((double)dx * dx + (double)dy * dy);
            if (len <= 1e-9) return 0f;
            double ux = dx / len, uy = dy / len;
            double t = double.PositiveInfinity;
            if (ux > 0) t = Math.Min(t, (xMax - ox) / ux);
            else if (ux < 0) t = Math.Min(t, (xMin - ox) / ux);
            if (uy > 0) t = Math.Min(t, (yMax - oy) / uy);
            else if (uy < 0) t = Math.Min(t, (yMin - oy) / uy);
            if (double.IsInfinity(t) || t < 0) return 0f;
            return (float)t;
        }

        /// <summary>보던 칸을 그대로 볼까(3-2 문턱): 몸 가운데가 사각형을 SwitchPad(0.3)만큼 넓힌 안(경계 포함)이면 참.</summary>
        public static bool KeepViewCell(float px, float py, float xMin, float yMin, float xMax, float yMax) =>
            px >= xMin - SwitchPad && px <= xMax + SwitchPad && py >= yMin - SwitchPad && py <= yMax + SwitchPad;

        /// <summary>
        /// 새 칸 드러남(3-3): 바뀐 뒤 elapsed초 동안 startRadius에서 fullRadius까지 부드럽게(smoothstep) 늘어난다.
        /// RevealSeconds(0.25초)가 지나면 fullRadius, 0초 이하면 startRadius. RevealSeconds가 0이면 바로 fullRadius.
        /// </summary>
        public static float RevealRadius(float elapsed, float startRadius, float fullRadius)
        {
            if (RevealSeconds <= 0f || elapsed >= RevealSeconds) return fullRadius;
            if (!(elapsed > 0f)) return startRadius;
            float t = elapsed / RevealSeconds;
            float s = t * t * (3f - 2f * t);
            return startRadius + (fullRadius - startRadius) * s;
        }

        /// <summary>적 몸이 차지하는 각도(도, 4-1): asin(반경 ÷ 거리). 거리 ≤ 반경이면 180, 반경이 0 이하면 0.</summary>
        public static float AngularRadius(float radius, float distance)
        {
            if (radius <= 0f) return 0f;
            if (distance <= radius) return 180f;
            return (float)(Math.Asin(radius / (double)distance) * (180.0 / Math.PI));
        }

        /// <summary>
        /// 부채꼴이 적을 보나(4-1·4-2). offDeg = 바라보는 쪽과 적 사이 각도(부호 무시). 들어올 때 반각 + 5° + 몸 각도 안,
        /// 이미 보이던 적(wasSeen)은 6° 더(ConeExitExtra) 벗어나야 거짓이다.
        /// </summary>
        public static bool ConeSees(bool wasSeen, float offDeg, float halfAngle, float angularRadius)
        {
            float limit = halfAngle + ConeEnterPad + angularRadius;
            if (wasSeen) limit += ConeExitExtra;
            return Math.Abs(offDeg) <= limit;
        }

        /// <summary>적 부채꼴 규칙을 쓰는 보는 칸인가(4-7): 보스방만 아니다(방 안 360°).</summary>
        public static bool ConeApplies(PieceKind viewPiece) => viewPiece != PieceKind.BossRoom;

        /// <summary>살핌으로 경험치를 미루는 칸인가(5-3): 보스방만 아니다(들어설 때 그대로 준다).</summary>
        public static bool SweepApplies(PieceKind piece) => piece != PieceKind.BossRoom;

        /// <summary>살핌에 필요한 '봤다' 칸 수 = ceil(걸을 수 있는 칸 × 0.75). 걸을 수 있는 칸이 0 이하면 0.</summary>
        public static int SweepNeeded(int walkable)
        {
            if (walkable <= 0) return 0;
            return (int)Math.Ceiling(walkable * (double)SweepFraction - 1e-6);
        }

        /// <summary>살핌이 끝났나: 본 칸 수 ≥ SweepNeeded. 걸을 수 있는 칸이 0 이하면 늘 참.</summary>
        public static bool SweepDone(int seen, int walkable) => walkable <= 0 || seen >= SweepNeeded(walkable);

        /// <summary>
        /// 등 뒤 기척 간격(초, 4-6): 굴쥐 1.2~1.8, 돌충이(코드 이름 Boar) 1.6~2.4, 궁수 2.0~3.0, 정예는 종류와 관계없이 1.4~2.0, 그 밖 2.0~3.0.
        /// 새로 든 적의 첫 소리는 BehindFirstMin~BehindFirstMax(0.15~0.35초).
        /// </summary>
        public static void BehindGap(MonsterKind kind, bool elite, out float min, out float max)
        {
            if (elite)
            {
                min = 1.4f;
                max = 2.0f;
                return;
            }
            switch (kind)
            {
                case MonsterKind.Rat:
                    min = 1.2f;
                    max = 1.8f;
                    return;
                case MonsterKind.Boar:
                    min = 1.6f;
                    max = 2.4f;
                    return;
                case MonsterKind.Archer:
                    min = 2.0f;
                    max = 3.0f;
                    return;
                default:
                    min = 2.0f;
                    max = 3.0f;
                    return;
            }
        }
    }
}
