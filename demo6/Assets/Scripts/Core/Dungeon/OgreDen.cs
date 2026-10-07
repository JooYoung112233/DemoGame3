using System;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 2층 계단 아래 '오우거 굴'(기획/전투-보스-무기-다듬기-1차.md 3-6·3-8, 6-1 묶음 7). 손 지도 2칸 "P-X":
    /// P = 보스방 앞 쉼터(돌방, 굴 앞 말뚝, 벽 등잔 2, 바닥 긁은 글), X = 보스방(안쪽 26×14, 왼쪽 벽 가운데 문 폭 4, 돌 기둥 4개).
    /// 돌로 쌓은 고정 칸이라 원정마다 같은 모습이다(매판 새 탐험 1차 2-4 '보스방, 앞 칸, 쉼터 말뚝 그대로'). 생성 2층 지도는 바꾸지 않고,
    /// 2층 계단(아래층이 시험판에 없을 때)이 이 굴로 내려간다(장면 쪽지 TripPlan.Den). 층 번호는 2층 그대로다(보스 수치·아이템 레벨 3·권장 레벨).
    /// 좌표는 칸 가운데 기준 유닛(칸 28×16, 걸을 수 있는 안쪽 x ±13.5, y ±7.5). 보스방 안 자리(기둥·등잔·쥐 구멍·시작 자리·줄 끝 말뚝)는
    /// 자리 표시가 아니라 이 표를 DungeonWorld(기둥)·BossArena(나머지)가 읽는다. 게임 안 글에 인물 이름을 쓰지 않는다.
    /// </summary>
    public static class OgreDen
    {
        /// <summary>굴이 딸린 층(제2층 바닥).</summary>
        public const int Floor = 2;
        /// <summary>지도 이름(큰 지도·콘솔).</summary>
        public const string Name = "오우거 굴";
        /// <summary>층 이름 카드(3-8).</summary>
        public const string CardTitle = "제2층 바닥 — 오우거 굴";
        public const string CardLine = "안쪽에서 돌 씹는 소리가 난다";
        /// <summary>꾸러미 보스 기록 열쇠(BossLedger).</summary>
        public const string BossId = "ogre";

        // ── 글자 지도 ──
        public const char FrontGlyph = 'P';
        public const char RoomGlyph = 'X';
        public const string FrontId = "P";
        public const string RoomId = "X";
        public const string FrontName = "돌방 쉼터";
        public const string RoomName = "오우거 굴";
        /// <summary>P(쉼터)가 왼쪽, X(보스방)가 오른쪽. X의 문은 왼쪽 벽 가운데다.</summary>
        public const string Glyphs = "P-X\n";

        // ── 자리 id(생성 2층의 "f2.*"와 겹치지 않게 머리말 'ogre.') ──
        public const string FrontStakeId = "ogre.P.stake";
        public const string FrontLampId1 = "ogre.P.lamp1";
        public const string FrontLampId2 = "ogre.P.lamp2";
        public const string ScrawlId = "ogre.P.scrawl";
        public const string BossFeatureId = "ogre.X.boss";
        /// <summary>처치 뒤 X 오른쪽 벽에 켜지는 줄 끝 말뚝(BossArena가 만든다, 자리 표시 아님).</summary>
        public const string EndStakeId = "ogre.X.stake";
        /// <summary>보스방 벽 등잔 id 머리(BossArena의 ArenaLamp, 'ogre.X.lamp1'~'4').</summary>
        public const string ArenaLampIdPrefix = "ogre.X.lamp";

        // ── 승강장 고르기 '보스방 앞'(3-8: 굴 앞 말뚝을 켜면 생기고 첫 처치 전까지 유지) ──
        /// <summary>승강장 고르기 콜백(onPick(int))이 '보스방 앞'을 고를 때 넘기는 값. 층 번호가 아니다(음수).</summary>
        public const int PickCode = -2;
        public const string LandingName = "보스방 앞";

        // ── 보스방 X(3-6, 방 가운데 (0, 0)) ──
        public const float InnerWidth = 26f;
        public const float InnerHeight = 14f;
        public const float InnerHalfWidth = InnerWidth * 0.5f;
        public const float InnerHalfHeight = InnerHeight * 0.5f;
        /// <summary>
        /// 실제로 걸을 수 있는 안쪽 반폭·반높이(칸 28×16, 벽 두께 1을 칸 경계 가운데에 놓음 → 27×15, x ±13.5, y ±7.5).
        /// 문서 '안쪽 26×14'(Inner*)는 벽을 칸 안에 넣어 센 값이라 자리 표(기둥·등잔·쥐 구멍)에만 쓰고, 화면에 담을 넓이는 이 값으로 잰다.
        /// </summary>
        public const float WalkHalfWidth = 13.5f;
        public const float WalkHalfHeight = 7.5f;
        /// <summary>X의 문 쪽(왼쪽 벽 가운데 (−13, 0), 폭 4).</summary>
        public const Side DoorSide = Side.Left;
        public static readonly Offset Door = new Offset(-13f, 0f);
        public const float DoorWidth = 4f;
        /// <summary>문이 막힐 때 문틈에 걸친 플레이어를 밀어 넣는 자리(방 안쪽).</summary>
        public static readonly Offset PlayerInside = new Offset(-11.5f, 0f);
        /// <summary>
        /// 돌 기둥 4개(1.6 × 1.6, 부서지지 않음). 기둥 사이 가로 8.5·세로 7, 벽과 틈 3.2라 지름 2.4인 몸이 끼지 않는다.
        /// PieceSlots.PillarsIn(BossRoom)도 이 표를 돌려준다(OgreDenTests가 틈을 손 계산과 맞춰 본다).
        /// </summary>
        public static readonly Offset[] Pillars =
        {
            new Offset(-4f, 3.5f), new Offset(-4f, -3.5f), new Offset(4.5f, 3.5f), new Offset(4.5f, -3.5f),
        };
        public const float PillarSize = 1.6f;
        /// <summary>오우거 시작 자리(BossRules.StartX와 같다), 오른쪽 벽을 보고 돌을 씹는 중.</summary>
        public static readonly Offset BossStart = new Offset(7f, 0f);
        public const float BossFacingDeg = 0f;
        /// <summary>벽 등잔 4개(반경 5/8, 처음에는 꺼짐). 2단계 0.3초에 대각선 둘(Phase2LampsOut)이 꺼지고 F로 다시 켠다.</summary>
        public static readonly Offset[] Lamps =
        {
            new Offset(-7f, 6.6f), new Offset(-7f, -6.6f), new Offset(3f, 6.6f), new Offset(3f, -6.6f),
        };
        /// <summary>2단계에 꺼지는 등잔(Lamps 차례): 왼쪽 위 (−7, 6.6)과 오른쪽 아래 (3, −6.6).</summary>
        public static readonly int[] Phase2LampsOut = { 0, 3 };
        /// <summary>2단계 전환 몇 초 뒤 등잔이 꺼지나(3-5 '0.3초').</summary>
        public const float LampsOutDelay = 0.3f;
        /// <summary>등잔 켜기 F: 처음 1.0초(일반 벽 등잔과 같음), 2단계에 꺼진 뒤 다시 켜기 0.5초(3-5).</summary>
        public const float LampHold = 1.0f;
        public const float LampRelightHold = 0.5f;
        /// <summary>쥐 구멍(벽 틈) 4곳: C에서 굴쥐가 나온다(OgreBrain이 BossRules.RatHole*로 같은 자리를 셈).</summary>
        public static readonly Offset[] RatHoles =
        {
            new Offset(-2f, 7f), new Offset(-2f, -7f), new Offset(10f, 7f), new Offset(10f, -7f),
        };
        /// <summary>줄 끝 말뚝(처치 뒤, X 오른쪽 벽 가까이).</summary>
        public static readonly Offset EndStake = new Offset(12f, 0f);
        /// <summary>장식(도형 임시판, 충돌 없음): 부서진 권양기 바구니, 사슬 더미, 뼈 더미, 갉아 먹힌 벽 자국.</summary>
        public static readonly Offset BasketDecor = new Offset(-9.5f, 5f);
        public static readonly Offset ChainDecor = new Offset(9f, 5.5f);
        public static readonly Offset BoneDecor = new Offset(10f, -5f);
        public static readonly Offset GnawDecor = new Offset(-10f, -6.8f);

        // ── 카메라(3-7: 문턱을 넘으면 고정 모드) ──
        /// <summary>고정 모드로 옮기는 시간(초).</summary>
        public const float CameraBlend = 0.6f;

        /// <summary>
        /// 고정 카메라 크기 = max((15 + 1) ÷ 2, 27.5 ÷ (2 × 화면비)): 걸을 수 있는 안쪽 27×15(Walk*)에 위아래 0.5·좌우 0.25 여유.
        /// 16:9·21:9는 8.0, 16:10은 8.59, 4:3은 10.31. 문서 3-7의 '안쪽 26×14' 식(16:10 8.28)이면 16:10·4:3에서 좌우 벽 쪽 0.25씩 잘렸다.
        /// </summary>
        public static float CameraSize(float aspect)
        {
            float a = Math.Max(0.1f, aspect);
            return Math.Max(WalkHalfHeight + 0.5f, (WalkHalfWidth + 0.25f) / a);
        }

        // ── 쉼터 P(3-8: 돌방, 벽 등잔 2, 바닥 긁은 글) ──
        /// <summary>쉼터 돌방 반폭·반높이(막다른 방과 같은 크기, 오른쪽 문 쪽은 폭 6 길로 트임).</summary>
        public const float FrontHalfWidth = 7f;
        public const float FrontHalfHeight = 4.5f;
        public static readonly Offset FrontStake = new Offset(3f, -2.5f);
        public static readonly Offset FrontLamp1 = new Offset(-5.5f, 4f);
        public static readonly Offset FrontLamp2 = new Offset(5.5f, 4f);
        public static readonly Offset Scrawl = new Offset(-2f, -1.5f);

        // ── 글(쉬운 한국어, 인물 이름 없음) ──
        public const string FrontStakeLabel = "굴 앞 말뚝";
        public const string ScrawlText = "여기서부터 돌방이다. 문 너머에서 씹는 소리가 난다.";
        public const string SealLine = "등 뒤에서 입구가 무너졌다.";
        public const string OpenLine = "막혔던 입구의 흙더미가 무너져 내린다.";
        public const string EndStakeLabel = "줄 끝 말뚝";
        public const string EndStakeText = "바구니로 올라가기 — 시험판은 여기까지.";
        public const string StairsPrompt = "굴로 내려가기";
        public const string RemainsPrompt = "살펴보기";
        /// <summary>처치 뒤 살펴보기(F) 글(3-1), 누를 때마다 차례로.</summary>
        public static readonly string[] RemainsLines =
        {
            "등에 박힌 곡괭이 자루가 셋이다.",
            "손잡이에 새긴 글자는 이빨 자국에 깎여 나갔다.",
        };

        /// <summary>던전에 오우거 굴이 있는가(마을 의뢰 '굴의 큰 놈' 잠금 풀기, QuestTable.OgreDenInDungeon).</summary>
        public static bool InDungeon => FloorRecipe.HasDenBelow(Floor);

        /// <summary>이 층 계단 아래가 이 굴인가.</summary>
        public static bool IsBelow(int floor) => floor == Floor && FloorRecipe.HasDenBelow(floor);

        /// <summary>이 층의 계단·계단 앞 말뚝 '내려가기'가 굴로 가는가(아래층이 시험판에 없고 굴이 있음).</summary>
        public static bool DescendsToDen(int floor) => IsBelow(floor) && !FloorRecipe.Exists(floor + 1);

        /// <summary>승강장 고르기 값이 '보스방 앞'인가.</summary>
        public static bool IsPick(int code) => code == PickCode;

        static CellFeature F(FeatureKind kind, string id, Offset at, string label = "", string param = "") =>
            new CellFeature { Kind = kind, Id = id, Local = at, Label = label, Param = param };

        /// <summary>범례(부를 때마다 새 객체). P: 굴 앞 말뚝·벽 등잔 2·바닥 긁은 글, X: 보스 자리 하나.</summary>
        public static CellDef[] Legend() => new[]
        {
            new CellDef
            {
                Glyph = FrontGlyph, Id = FrontId, Name = FrontName, Piece = PieceKind.BossFront,
                Features = new[]
                {
                    F(FeatureKind.Stake, FrontStakeId, FrontStake, FrontStakeLabel),
                    F(FeatureKind.WallLamp, FrontLampId1, FrontLamp1, "벽 등잔"),
                    F(FeatureKind.WallLamp, FrontLampId2, FrontLamp2, "벽 등잔"),
                    F(FeatureKind.Scrawl, ScrawlId, Scrawl, ScrawlText),
                },
            },
            new CellDef
            {
                Glyph = RoomGlyph, Id = RoomId, Name = RoomName, Piece = PieceKind.BossRoom,
                Features = new[]
                {
                    new CellFeature
                    {
                        Kind = FeatureKind.Boss, Id = BossFeatureId, Local = BossStart, Label = "갱도 오우거", Param = BossId,
                        FacingDeg = BossFacingDeg,
                    },
                },
            },
        };

        /// <summary>굴 지도(부를 때마다 새 객체).</summary>
        public static FloorMap Build() => FloorMap.Parse(Floor, Name, Glyphs, Legend());
    }
}
