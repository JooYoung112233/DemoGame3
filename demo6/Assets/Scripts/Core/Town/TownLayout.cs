using System;
using System.Collections.Generic;

namespace Demo6.Core.Town
{
    /// <summary>마을 좌표 한 점(유닛, 2차 마을 좌표계). UnityEngine 없이 쓰려고 따로 둔다.</summary>
    public readonly struct TownVec
    {
        public readonly float X;
        public readonly float Y;

        public TownVec(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static float Distance(TownVec a, TownVec b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>축에 나란한 상자(XMin~XMax, YMin~YMax).</summary>
    public readonly struct TownBox
    {
        public readonly string Name;
        public readonly float XMin;
        public readonly float YMin;
        public readonly float XMax;
        public readonly float YMax;

        public TownBox(string name, float xMin, float yMin, float xMax, float yMax)
        {
            Name = name;
            XMin = Math.Min(xMin, xMax);
            YMin = Math.Min(yMin, yMax);
            XMax = Math.Max(xMin, xMax);
            YMax = Math.Max(yMin, yMax);
        }

        public float Width => XMax - XMin;
        public float Height => YMax - YMin;
        public TownVec Center => new TownVec((XMin + XMax) * 0.5f, (YMin + YMax) * 0.5f);

        /// <summary>점이 상자 안(경계 포함)인가.</summary>
        public bool Contains(TownVec p) => p.X >= XMin && p.X <= XMax && p.Y >= YMin && p.Y <= YMax;

        /// <summary>점이 상자 속(경계 제외)인가. 충돌 상자 안에 F 자리가 묻혔는지 볼 때 쓴다.</summary>
        public bool ContainsStrict(TownVec p) => p.X > XMin && p.X < XMax && p.Y > YMin && p.Y < YMax;

        /// <summary>다른 상자가 이 상자 안에 들어가는가(경계 포함).</summary>
        public bool Encloses(TownBox o) => o.XMin >= XMin && o.XMax <= XMax && o.YMin >= YMin && o.YMax <= YMax;

        /// <summary>선분 a–b가 상자 속을 지나가는가(경계를 스치기만 하면 아님).</summary>
        public bool SegmentHits(TownVec a, TownVec b)
        {
            // 리앙-바스키 자르기를 줄인 꼴: 선분 위 t ∈ [0, 1]에서 상자 속 구간이 남으면 지나감.
            double t0 = 0, t1 = 1;
            double dx = b.X - a.X, dy = b.Y - a.Y;
            if (!Clip(-dx, a.X - XMin, ref t0, ref t1)) return false;
            if (!Clip(dx, XMax - a.X, ref t0, ref t1)) return false;
            if (!Clip(-dy, a.Y - YMin, ref t0, ref t1)) return false;
            if (!Clip(dy, YMax - a.Y, ref t0, ref t1)) return false;
            if (t1 - t0 <= 1e-6) return false;
            double mx = a.X + dx * (t0 + t1) * 0.5, my = a.Y + dy * (t0 + t1) * 0.5;
            return mx > XMin + 1e-6 && mx < XMax - 1e-6 && my > YMin + 1e-6 && my < YMax - 1e-6;
        }

        static bool Clip(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-12) return q >= 0;
            double r = q / p;
            if (p < 0)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        public override string ToString() => $"{Name} x {XMin}~{XMax}, y {YMin}~{YMax}";
    }

    /// <summary>F 자리(상호작용 반경, 안내 글은 TownScript 물체 글·TalkDirector.Hint).</summary>
    public readonly struct TownSpot
    {
        public readonly string Id;
        public readonly TownVec Pos;
        public readonly float Radius;

        public TownSpot(string id, float x, float y, float radius)
        {
            Id = id;
            Pos = new TownVec(x, y);
            Radius = radius;
        }

        public override string ToString() => $"{Id} {Pos} r{Radius}";
    }

    /// <summary>마을 점광 하나(2-2). 색은 Game(기존 등잔빛 색)이 정한다. Flicker = 일렁임.</summary>
    public readonly struct TownLight
    {
        public readonly string Name;
        public readonly TownVec Pos;
        public readonly float Radius;
        public readonly float Intensity;
        public readonly bool Flicker;

        public TownLight(string name, float x, float y, float radius, float intensity, bool flicker)
        {
            Name = name;
            Pos = new TownVec(x, y);
            Radius = radius;
            Intensity = intensity;
            Flicker = flicker;
        }
    }

    /// <summary>v058 승인 마을 원본의 배치와 기존 시설·주민 연결 좌표. 시스템 규칙과 속도·광량은 유지한다.</summary>
    public static class TownLayout
    {
        // v061: scale environment geometry about the existing town center; actor art and camera stay unchanged.
        public const float MapScale = 1.20f;
        public const float OriginX = 42f, OriginY = 39f;
        public const float ReferencePixelsPerUnit = 40f;
        public const float WorldPixelsPerUnit = ReferencePixelsPerUnit / MapScale;
        public static float WorldX(float baselineX) => OriginX + (baselineX-OriginX)*MapScale;
        public static float WorldY(float baselineY) => OriginY + (baselineY-OriginY)*MapScale;
        public static TownVec Pixel(float x,float y) => new TownVec(OriginX+(x-768f)/WorldPixelsPerUnit,OriginY-(y-512f)/WorldPixelsPerUnit);
        static TownBox WorldBox(string name,float xMin,float yMin,float xMax,float yMax) => new TownBox(name,WorldX(xMin),WorldY(yMin),WorldX(xMax),WorldY(yMax));
        static TownSpot Spot(string id,float x,float y,float radius) { var p=Pixel(x,y);return new TownSpot(id,p.X,p.Y,radius); }
        static TownBox PixelBox(string name,float x,float y,float w,float h) { var a=Pixel(x,y+h);var b=Pixel(x+w,y);return new TownBox(name,a.X,a.Y,b.X,b.Y); }
        public static readonly TownBox MineHole=PixelBox("갱구 낙하 방지",838,76,93,90);
        public static readonly TownBox MayorAnnex=PixelBox("촌장집 부속채",450,145,108,154);
        public static readonly TownBox TavernAnnex=PixelBox("주막 부속채",1324,541,122,168);
        public static readonly TownBox PlankHouse=PixelBox("판자 주택",94,485,198,144);
        public static readonly TownBox TarpHouse=PixelBox("천막 주택",432,486,142,172);
        public static readonly TownBox TarpAnnex=PixelBox("천막 주택 부속채",574,524,42,91);
        public static readonly TownBox RuinBase=PixelBox("무너진 집 잔존 기초",167,743,121,111);
        public static TownBox[] Buildings => new[]{House,Smithy,Tavern,PlankHouse,TarpHouse,RuinBase,GuardPost};

        // ── 땅 ──
        public static readonly TownBox Walk = WorldBox("걷는 땅", 23.8f, 27f, 60.2f, 51f);
        public static readonly TownBox Paint = WorldBox("칠하는 땅", 22f, 24f, 62f, 54f);
        public const float WallThickness = 1f * MapScale;

        // ── 자리 ──
        public static readonly TownVec ReturnPoint = Pixel(891f, 357f);
        public static readonly TownVec NewPlayStart = Pixel(924f, 958f);
        /// <summary>새 플레이: 기존 한 걸음 1초 뒤 오프닝. 지리 확대와 별개로 시작점에서의 보행 시간을 보존한다.</summary>
        public static readonly float OpeningLineY = NewPlayStart.Y + WalkSpeed;
        /// <summary>새 플레이: 시작 뒤 이만큼 밝아짐(초), 춘삼 바크 "어이, 이쪽!"까지(초).</summary>
        public const float NewPlayFadeIn = 0.8f;
        public const float NewPlayBarkDelay = 0.5f;
        /// <summary>올라온 뒤 밝아짐(초).</summary>
        public const float ArrivalFadeIn = 0.5f;
        /// <summary>분필 화살표 3개(마당 쪽을 가리킴).</summary>
        public static readonly TownVec[] ChalkArrows = { Pixel(924,925), Pixel(925,870), Pixel(916,812) };

        // ── 상호작용 반경(6-3) ──
        public const float NpcRadius = 1.6f;
        public const float FacilityRadius = 1.4f;
        public const float ObjectRadius = 1.2f;
        /// <summary>주민이 플레이어 쪽으로 도는 거리.</summary>
        public const float NpcTurnRadius = 3f;

        // ── F 자리(5-8 물체 id) ──
        public static readonly TownSpot Gate = Spot(TownScript.ObjGate, 884f, 244f, FacilityRadius);
        public static readonly TownSpot Anvil = Spot(TownScript.ObjAnvil, 1406f, 322f, FacilityRadius);
        public static readonly TownSpot Board = Spot(TownScript.ObjBoard, 777f, 663f, FacilityRadius);
        public static readonly TownSpot Shutter = Spot(TownScript.ObjShutter, 1207f, 732f, ObjectRadius);
        public static readonly TownSpot Cart = Spot(TownScript.ObjCart, 797f, 916f, ObjectRadius);

        public static readonly TownSpot[] Facilities = { Gate, Anvil, Board, Shutter, Cart };

        // ── 주민 평소 자리(3-1) ──
        public static readonly TownVec GateNpcHome = Pixel(871f, 411f);
        public static readonly TownVec SmithNpcHome = Pixel(1380f, 449f);
        public static readonly TownVec TrainerNpcHome = Pixel(946f, 783f);

        /// <summary>주민 평소 자리(표에 없는 주민이면 귀환 지점).</summary>
        public static TownVec NpcHome(string npcId)
        {
            switch (npcId)
            {
                case NpcTable.Gate: return GateNpcHome;
                case NpcTable.Smith: return SmithNpcHome;
                case NpcTable.Trainer: return TrainerNpcHome;
                default: return ReturnPoint;
            }
        }

        // ── 충돌 상자(WorldProps.SolidBox, Wall 레이어, 그림자 끔) ──
        public static readonly TownBox Winch = PixelBox("권양틀", 822f, 44f, 130f, 136f);
        public static readonly TownBox Smithy = PixelBox("대장간", 1119f, 185f, 207f, 197f);
        public static readonly TownBox Tavern = PixelBox("주막", 1085f, 493f, 239f, 216f);
        public static readonly TownBox GuardPost = PixelBox("경비 초소", 628f, 797f, 124f, 90f);
        public static readonly TownBox House = PixelBox("촌장집", 216f, 109f, 234f, 255f);
        /// <summary>수레(42, 28.8)가 남쪽 벽 틈 x 40.5~43.5를 막는다.</summary>
        public static readonly TownVec CartPos = Pixel(796.5f, 883.5f);
        public static readonly TownBox CartBox = PixelBox("수레 잔해", 779f, 870f, 35f, 27f);
        /// <summary>남쪽 수레길 벽 틈.</summary>
        public const float SouthGapXMin = OriginX + (43.725f - OriginX) * MapScale;
        public const float SouthGapXMax = OriginX + (47.95f - OriginX) * MapScale;

        /// <summary>훈련 울타리 x 51~57, y 35~43, 서쪽 문 y 38~40. 울타리 두께.</summary>
        public static readonly TownBox Fence = PixelBox("미사용 훈련장 호환 범위", 700f, 320f, 120f, 100f);
        public const float FenceThickness = 0.3f * MapScale;
        public const float FenceGateYMin = OriginY + (41.5f - OriginY) * MapScale;
        public const float FenceGateYMax = OriginY + (43f - OriginY) * MapScale;
        /// <summary>쓰러진 허수아비(장식).</summary>
        public static readonly TownVec Dummy = Pixel(760f, 370f);

        /// <summary>바깥 테두리 벽(걷는 땅 바로 바깥, 남쪽은 수레길 틈을 비움).</summary>
        public static TownBox[] OuterWalls()
        {
            float t = WallThickness;
            return new[]
            {
                new TownBox("남쪽 벽 서", Walk.XMin - t, Walk.YMin - t, SouthGapXMin, Walk.YMin),
                new TownBox("남쪽 벽 동", SouthGapXMax, Walk.YMin - t, Walk.XMax + t, Walk.YMin),
                new TownBox("북쪽 벽", Walk.XMin - t, Walk.YMax, Walk.XMax + t, Walk.YMax + t),
                new TownBox("서쪽 벽", Walk.XMin - t, Walk.YMin - t, Walk.XMin, Walk.YMax + t),
                new TownBox("동쪽 벽", Walk.XMax, Walk.YMin - t, Walk.XMax + t, Walk.YMax + t),
            };
        }

        /// <summary>울타리 조각(안쪽에 붙인 얇은 상자, 서쪽은 문 자리를 비움).</summary>
        public static TownBox[] FenceSegments()
        {
            float t = FenceThickness;
            return new[]
            {
                new TownBox("울타리 북", Fence.XMin, Fence.YMax - t, Fence.XMax, Fence.YMax),
                new TownBox("울타리 남", Fence.XMin, Fence.YMin, Fence.XMax, Fence.YMin + t),
                new TownBox("울타리 동", Fence.XMax - t, Fence.YMin, Fence.XMax, Fence.YMax),
                new TownBox("울타리 서 아래", Fence.XMin, Fence.YMin, Fence.XMin + t, FenceGateYMin),
                new TownBox("울타리 서 위", Fence.XMin, FenceGateYMax, Fence.XMin + t, Fence.YMax),
            };
        }

        /// <summary>건물·수레·울타리·바깥 벽 등 모든 충돌 상자.</summary>
        public static List<TownBox> Solids()
        {
            var list = new List<TownBox> { MineHole, Smithy, Tavern, GuardPost, House, MayorAnnex, TavernAnnex, PlankHouse, TarpHouse, TarpAnnex, RuinBase, CartBox };
            list.AddRange(OuterWalls());
            list.Add(new TownBox("남쪽 길 끝 경계", SouthGapXMin, Walk.YMin - WallThickness, SouthGapXMax, Walk.YMin));
            return list;
        }

        // ── 장식·불빛 자리 ──
        /// <summary>권양기 가로보의 원본 x 43.725, 간격 0.22, y 47.525를 MapScale로 옮긴 등불 12개. 켜진 수 규칙 유지.</summary>
        public const int LampCount = 12;
        public const float LampX0 = OriginX + (43.725f - OriginX) * MapScale;
        public const float LampStep = 0.22f * MapScale;
        public const float LampY = OriginY + (47.525f - OriginY) * MapScale;
        public const string LampOffHex = "#2A2520";

        public static TownVec LampPos(int i) => new TownVec(LampX0 + LampStep * i, LampY);

        /// <summary>켜진 등불 수(12 − 맡긴 명패, 0 아래로 내려가지 않음).</summary>
        public static int LitLamps(int tags) => Math.Max(0, Math.Min(LampCount, LampCount - Math.Max(0, tags)));

        public static readonly TownVec ForgeGlow = Pixel(1464f, 285f);
        public static readonly TownVec TavernDoor = Pixel(1207f, 711f);
        public static readonly TownVec[] TavernWindows = { Pixel(1156,700), Pixel(1281,702) };
        public static readonly TownVec Brazier = Pixel(586f, 861f);
        public static readonly TownVec[] HouseWindows = { Pixel(280,356), Pixel(409,354) };
        public static readonly TownVec ReturnLamp = Pixel(705f, 620f);

        // ── 빛(2-2, TownLighting) ──
        /// <summary>전역 빛 세기(던전 0.06), F1 밀대 범위.</summary>
        public const float GlobalLight = 0.32f;
        public const float GlobalLightMin = 0.10f;
        public const float GlobalLightMax = 0.70f;

        /// <summary>권양기 등불 줄 빛 세기 = 0.30 + 0.45 × 켜진 등불/12.</summary>
        public static float WinchLightIntensity(int litLamps) => 0.30f + 0.45f * Math.Max(0, Math.Min(LampCount, litLamps)) / LampCount;

        /// <summary>따뜻한 점광 5개(권양기 등불 줄은 켜진 등불 수에 따라 세기가 바뀐다). 플레이어를 따라가는 약한 빛은 PlayerLight.</summary>
        public static TownLight[] Lights(int litLamps) => new[]
        {
            new TownLight("화덕", ForgeGlow.X, ForgeGlow.Y, 5.5f * MapScale, 0.9f, true),
            new TownLight("주막 창", TavernDoor.X, TavernDoor.Y, 4f * MapScale, 0.6f, false),
            new TownLight("초소 화로", Brazier.X, Brazier.Y, 4.5f * MapScale, 0.7f, true),
            new TownLight("권양기 등불 줄", Winch.Center.X, LampY, 7f * MapScale, WinchLightIntensity(litLamps), false),
            new TownLight("귀환 지점 등", ReturnLamp.X, ReturnLamp.Y, 3.5f * MapScale, 0.45f, false),
        };

        public const float PlayerLightRadius = 3f;
        public const float PlayerLightIntensity = 0.2f;

        // ── 카메라(2-2, TownCamera) ──
        public const float CameraSize = 8f;
        public const string CameraBackgroundHex = "#15120F";

        /// <summary>
        /// 카메라 중심이 머물 범위: 확대한 칠하는 땅 안에서 기존 카메라 반폭·반높이를 뺀 범위다. 카메라 크기 8은 바꾸지 않는다.
        /// 화면이 칠한 땅보다 넓으면 그 축은 가운데에 묶는다.
        /// </summary>
        public static TownBox CameraCenterBounds(float aspect)
        {
            float halfH = CameraSize;
            float halfW = CameraSize * Math.Max(0.1f, aspect);
            float xMin = Paint.XMin + halfW, xMax = Paint.XMax - halfW;
            float yMin = Paint.YMin + halfH, yMax = Paint.YMax - halfH;
            if (xMin > xMax) xMin = xMax = Paint.Center.X;
            if (yMin > yMax) yMin = yMax = Paint.Center.Y;
            return new TownBox("카메라 중심", xMin, yMin, xMax, yMax);
        }

        /// <summary>카메라 중심을 범위 안으로.</summary>
        public static TownVec ClampCamera(TownVec target, float aspect)
        {
            var b = CameraCenterBounds(aspect);
            return new TownVec(Math.Max(b.XMin, Math.Min(b.XMax, target.X)), Math.Max(b.YMin, Math.Min(b.YMax, target.Y)));
        }

        // ── 걷기(2-1) ──
        /// <summary>SpeedOverride 6.5 × 걷기 배율 0.80 = 실제 초당 5.2(던전 시작 걸음 4.24보다 약 23% 빠름).</summary>
        public const float SpeedOverride = 6.5f;
        public const float WalkScale = 0.80f;
        public const float WalkSpeed = SpeedOverride * WalkScale;

        /// <summary>곧은 거리를 걷는 시간(초).</summary>
        public static float WalkSeconds(TownVec a, TownVec b) => TownVec.Distance(a, b) / WalkSpeed;

        /// <summary>보고 한 바퀴(귀환 → 춘삼 → 옥금 → 무진 → 갱도 입구).</summary>
        public static TownVec[] ReportLoop() => new[] { ReturnPoint, GateNpcHome, Pixel(1050,430), SmithNpcHome, Pixel(1030,435), Pixel(918,520), Pixel(899,703), TrainerNpcHome, Pixel(899,703), Pixel(918,520), Pixel(884,370), Gate.Pos };

        /// <summary>점들을 차례로 잇는 곧은 거리 합.</summary>
        public static float PathLength(IReadOnlyList<TownVec> points)
        {
            float sum = 0f;
            for (int i = 1; i < points.Count; i++) sum += TownVec.Distance(points[i - 1], points[i]);
            return sum;
        }
    }
}
