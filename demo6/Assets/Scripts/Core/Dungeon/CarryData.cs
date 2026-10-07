using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Demo6.Core.Loot;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 이번 원정 몫(매판 새 탐험 1차 3-3 '원정 몫'). 계단으로 내려가 장면을 다시 불러올 때만 들고 가고, 바구니로 올라가면 결과 창에 쓰고 버린다.
    /// </summary>
    public sealed class ExpeditionLeg
    {
        /// <summary>체력·물약(-1 = 가득). 계단으로 내려가도 그대로 이어진다.</summary>
        public int Hp = -1;
        public int Potions = -1;
        /// <summary>원정 시작 실제 시각(Time.realtimeSinceStartup).</summary>
        public float StartRealtime;
        /// <summary>결과 창 숫자: 쓰러뜨린 것, 연 궤짝, 밝힌 칸 / 처음 갈 수 있는 칸(층마다 더함), 이번 원정 강화석·골드.</summary>
        public int Kills;
        public int ChestsOpened;
        public int CellsVisited;
        public int CellsTotal;
        public int StonesGained;
        public int GoldGained;
        /// <summary>이번 원정에 말뚝에서 쉬었는가(원정마다 한 번, 시스템-컨텐츠-다듬기-검토-1차.md 묶음 5-2).</summary>
        public bool Rested;
        /// <summary>챙긴 식은 주먹밥 수(원정마다 1개까지, 묶음 5-7).</summary>
        public int RiceBalls;
        /// <summary>이번 원정에 쓰러진 수.</summary>
        public int Downs;
        /// <summary>든 기름 병(원정 시작 2, 벽 등잔 하나에 1병, 묶음 5-6).</summary>
        public int Oil = DownRules.StartOil;
        /// <summary>이번 원정에 밟은 층(차례대로).</summary>
        public readonly List<int> Floors = new List<int>();

        /// <summary>깊은 복사(층 목록까지).</summary>
        public ExpeditionLeg Clone()
        {
            var c = new ExpeditionLeg
            {
                Hp = Hp,
                Potions = Potions,
                StartRealtime = StartRealtime,
                Kills = Kills,
                ChestsOpened = ChestsOpened,
                CellsVisited = CellsVisited,
                CellsTotal = CellsTotal,
                StonesGained = StonesGained,
                GoldGained = GoldGained,
                Rested = Rested,
                RiceBalls = RiceBalls,
                Downs = Downs,
                Oil = Oil,
            };
            c.Floors.AddRange(Floors);
            return c;
        }

        /// <summary>이번 원정에 밟은 가장 깊은 층(없으면 0).</summary>
        public int DeepestFloor
        {
            get
            {
                int deepest = 0;
                foreach (int f in Floors)
                    if (f > deepest) deepest = f;
                return deepest;
            }
        }
    }

    /// <summary>
    /// 꾸러미(매판 새 탐험 1차 '한눈에' 6번째 줄, 3-3 '꾸러미에 담을 것'): 장면을 다시 불러와도 들고 가는 프로필 몫.
    /// 레벨·경험치·스킬, 낀 장비·가방·재화, 능력, 켠 승강장·줄 닿은 깊이, 원정 번호·프로필 소금, 층별 첫 방문, 한 번 받는 것 id,
    /// 층별 지난 원정 지도 글자(흔적 비교), 발견 주머니·측량 장 수, 보스 기록(전투·보스 문서 3-8, 묶음 7: 처치 수·마지막 처치 원정·쓰러진 횟수·
    /// 켠 보스방 앞 말뚝, 읽고 쓰기는 BossLedger 한 곳). 칸 객체는 담지 않고 id만 담는다(위험 5).
    /// 같은 꾸러미가 나중에 마을 장면 오가기와 디스크 저장(M2)의 바탕이 된다. 버전 번호로 옛 글을 알아본다.
    /// 판본 2(장비 문서 12장 단계 2): 낀 장비가 무기 한 줄에서 장착 8자리(eq.weapon … eq.amulet)로, 장비 글에 강화·옵션·전설이 붙었다.
    /// 판본 1 글(낀 무기 'equipped', 4칸짜리 가방 항목)도 읽는다. 보스 기록 줄(bosskills·bosskillexp·bosslosses·bossstakes)은
    /// 판본을 올리지 않고 키만 더했다(오우거 굴 전에 담은 판본 2 글에는 키가 없어 빈 기록으로 읽힌다).
    /// 재화 쓸 곳 1차 8장도 판본 2 그대로: 장비 글 9번째 칸(강화 실패 수, 0이면 안 씀)과 산 대장간 물건 줄(bought, 비면 안 씀).
    /// </summary>
    public sealed class CarryData
    {
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        /// <summary>프로필 소금(씨앗·궤짝 결과를 프로필마다 다르게).</summary>
        public ulong ProfileSalt;
        /// <summary>지금 원정 번호(1부터). 바구니로 올라가 밤을 지나면 +1.</summary>
        public int Expedition = 1;
        /// <summary>이번 원정 앞의 밤 사건(생성기 입력, 밤 카드 글).</summary>
        public NightEvent Night = NightEvent.None;

        // ── 레벨·스킬 ──
        public int Level = 1;
        public int TotalXp;
        public int SkillPoints;
        /// <summary>스킬 랭크(SkillTree.Count 길이, 빈 배열이면 모두 0).</summary>
        public int[] SkillRanks = Array.Empty<int>();
        /// <summary>무진에게 배운 기술 칸(배우기·갈림 칸 key, 기획/스킬-자원-트리-1차.md 3장). 랭크 칸은 SkillRanks에 있다.</summary>
        public readonly HashSet<string> SkillNodes = new HashSet<string>();

        // ── 장비·재화 ──
        /// <summary>
        /// 장착 8자리(GearSlot 차례, 장비 문서 4-1). null 칸은 무기·갑옷·투구·장갑·장화면 시작 장비(4-4), 반지 1·2·목걸이면 빈 자리다.
        /// </summary>
        public readonly GearItem[] Equipment = new GearItem[GearSlots.SlotCount];
        /// <summary>가방 장비(7부위 공통, 넣은 차례).</summary>
        public readonly List<GearItem> Bag = new List<GearItem>();
        public int Stones;
        public int Gold;
        /// <summary>
        /// 룬 주머니(기획/세-무기-우클릭-소켓-1차.md 6-4·6-6): 룬 id → 개수(종류마다 RuneRules.PouchCap까지, 가방 칸 안 씀).
        /// 글에는 비어 있지 않을 때만 "runes=rune_superarmor:2"(id Ordinal 차례) 한 줄로 쓴다. 더할 때는 AddRunes(상한·모르는 룬 거름)를 쓴다.
        /// </summary>
        public readonly Dictionary<string, int> RunePouch = new Dictionary<string, int>();

        // ── 능력 ──
        public bool HasPickaxe;
        public bool HasKey;

        // ── 깊이 ──
        /// <summary>바구니 줄이 닿는 가장 깊은 승강장 층('줄 끝'). 계단 앞 말뚝을 켜면 아래층으로 늘어남(영구).</summary>
        public int RopeDepth = 1;
        /// <summary>불을 켠 승강장 층(영구).</summary>
        public readonly SortedSet<int> LitLandings = new SortedSet<int>();
        /// <summary>한 번이라도 밟은 층. 없으면 그 층은 '처음 밟는 원정'이라 고른 씨앗을 쓴다.</summary>
        public readonly HashSet<int> VisitedFloors = new HashSet<int>();
        /// <summary>지금까지 도달한 가장 깊은 층.</summary>
        public int DeepestFloor = 1;

        // ── 한 번 받는 것·지도 ──
        /// <summary>받은 '한 번 받는 것' id(FloorRecipe.OnceItems, 영구).</summary>
        public readonly HashSet<string> OnceDone = new HashSet<string>();
        /// <summary>층 → 그 층을 마지막으로 밟은 원정의 글자 지도(MapDiff 흔적 비교용).</summary>
        public readonly Dictionary<int, string> LastGlyphs = new Dictionary<int, string>();

        // ── 경험치 상한(2-6) ──
        /// <summary>층 → 발견 주머니(층 첫 방문 지도에서 받을 수 있는 발견 경험치 합).</summary>
        public readonly Dictionary<int, int> DiscoveryPouch = new Dictionary<int, int>();
        /// <summary>층 → 지금까지 받은 발견 경험치.</summary>
        public readonly Dictionary<int, int> DiscoveryXpGiven = new Dictionary<int, int>();
        /// <summary>층 → 측량 장 수(층마다 3장까지, 장당 4U).</summary>
        public readonly Dictionary<int, int> SurveySheets = new Dictionary<int, int>();
        /// <summary>사건 경험치(10U)를 이미 받은 층.</summary>
        public readonly HashSet<int> EventXpFloors = new HashSet<int>();

        /// <summary>글을 보인 횟수 같은 작은 세기(예: "landing_line" 승강장 도착 글 처음 3번).</summary>
        public readonly Dictionary<string, int> Counters = new Dictionary<string, int>();

        // ── 보스(전투·보스 문서 3-8, 묶음 7 '오우거 굴'). 읽고 쓰는 규칙은 BossLedger 한 곳 ──
        /// <summary>보스 id(OgreDen.BossId) → 처치 수(영구). 0에서 1이 되는 처치가 첫 처치(3-8 첫 처치 보상).</summary>
        public readonly Dictionary<string, int> BossKills = new Dictionary<string, int>();
        /// <summary>보스 id → 마지막으로 잡은 원정 번호. 같은 원정 안에서는 다시 나오지 않는다(BossLedger.Present).</summary>
        public readonly Dictionary<string, int> BossKillExpedition = new Dictionary<string, int>();
        /// <summary>보스 id → 그 보스방에서 쓰러진 횟수.</summary>
        public readonly Dictionary<string, int> BossLosses = new Dictionary<string, int>();
        /// <summary>켠 보스방 앞 쉼터 말뚝 id(영구, 승강장 고르기 '보스방 앞').</summary>
        public readonly HashSet<string> BossStakes = new HashSet<string>();

        // ── 마을에서 산 것(재화 쓸 곳 1차 6-2·8장). 읽고 쓰는 규칙은 ForgeShop 한 곳 ──
        /// <summary>산 대장간 물건 id(ForgeShop.Bag25·Bag30·Flask, 영구). 글에는 비어 있지 않을 때만 "bought=forge.bag25,forge.flask"(id Ordinal 차례).</summary>
        public readonly HashSet<string> TownPurchases = new HashSet<string>();

        /// <summary>이번 원정 몫. 계단으로 내려가는 중에만 있고 바구니로 올라가면 null.</summary>
        public ExpeditionLeg Leg;

        /// <summary>새 프로필(첫 원정, 1층, 레벨 1).</summary>
        public static CarryData NewProfile(ulong salt) => new CarryData { ProfileSalt = salt, Expedition = 1 };

        public int Count(string key) => key != null && Counters.TryGetValue(key, out int n) ? n : 0;

        /// <summary>주머니의 그 룬 개수.</summary>
        public int RuneCount(string runeId) => runeId != null && RunePouch.TryGetValue(runeId, out int n) ? Math.Max(0, n) : 0;

        /// <summary>주머니에 룬을 더한다(종류마다 RuneRules.PouchCap까지, 모르는 룬은 거름). 실제로 더한 개수.</summary>
        public int AddRunes(string runeId, int count = 1) => RuneRules.PouchAdd(RunePouch, runeId, count);

        /// <summary>세기를 1 올리고 올린 값을 돌려준다.</summary>
        public int Bump(string key)
        {
            int n = Count(key) + 1;
            if (key != null) Counters[key] = n;
            return n;
        }

        /// <summary>승강장 고르기 목록: 1층부터 줄 끝(RopeDepth)까지, 시험판 상한(maxFloor)을 넘지 않게.</summary>
        public List<int> LandingFloors(int maxFloor)
        {
            var list = new List<int>();
            int last = Math.Max(1, Math.Min(RopeDepth, maxFloor));
            for (int f = 1; f <= last; f++) list.Add(f);
            return list;
        }

        /// <summary>기본 출발 층('줄 끝').</summary>
        public int RopeEnd(int maxFloor) => Math.Max(1, Math.Min(RopeDepth, maxFloor));

        // ── 글로 담기·풀기 ─────────────────────────────────────

        /// <summary>글 첫 줄 머리("carry v1").</summary>
        public const string Header = "carry v";

        /// <summary>
        /// 글로 담기(디스크 저장 M2의 바탕). 첫 줄 "carry v{버전}", 그 뒤 한 줄에 key=value 하나.
        /// 목록은 쉼표, 사전은 k:v를 쉼표로 잇는다. 같은 값이면 같은 글이 나오게 집합·사전은 정렬해 쓴다.
        /// 장착은 eq.weapon … eq.amulet 8줄(빈 칸은 빈 값), 가방은 장비 글을 쉼표로 잇는다. 장비 글은 GearText.
        /// 자유 글(id·지도 글자·세기 이름·보스 id·말뚝 id)의 '%' 줄바꿈 ',' ':' '|' '='는 %XX로 바꿔 나누기와 겹치지 않게 한다.
        /// 보스 기록은 "bosskills=ogre:1", "bosskillexp=ogre:3", "bosslosses=ogre:2", "bossstakes=ogre.P.stake"(키·id는 Ordinal 차례).
        /// 룬 주머니는 비어 있지 않을 때만 "runes=rune_superarmor:2"(판본은 2 그대로, 키만 더함). 원정 몫(Leg)은 있을 때만 leg.* 줄로 쓴다.
        /// 산 대장간 물건은 비어 있지 않을 때만 "bought=forge.bag25,forge.flask"(재화 쓸 곳 1차 8장, 판본 2 그대로).
        /// </summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append(CurrentVersion.ToString(Inv)).Append('\n');
            Line(sb, "salt", ProfileSalt.ToString(Inv));
            Line(sb, "expedition", Expedition.ToString(Inv));
            Line(sb, "night", Night.ToString());
            Line(sb, "level", Level.ToString(Inv));
            Line(sb, "totalxp", TotalXp.ToString(Inv));
            Line(sb, "skillpoints", SkillPoints.ToString(Inv));
            Line(sb, "skillranks", JoinInts(SkillRanks ?? Array.Empty<int>()));
            foreach (var slot in GearSlots.All)
            {
                var item = Equipment[(int)slot];
                Line(sb, EquipKeyPrefix + GearSlots.Key(slot), item != null ? GearText(item) : "");
            }
            var bag = new List<string>();
            foreach (var g in Bag)
                if (g != null) bag.Add(GearText(g));
            Line(sb, "bag", string.Join(",", bag));
            Line(sb, "stones", Stones.ToString(Inv));
            Line(sb, "gold", Gold.ToString(Inv));
            // 룬 주머니는 비어 있지 않을 때만 쓴다(룬이 없는 꾸러미 글은 예전과 글자까지 같다).
            string runes = JoinPouch(RunePouch);
            if (runes.Length > 0) Line(sb, RunesKey, runes);
            Line(sb, "pickaxe", HasPickaxe ? "1" : "0");
            Line(sb, "key", HasKey ? "1" : "0");
            Line(sb, "ropedepth", RopeDepth.ToString(Inv));
            Line(sb, "litlandings", JoinInts(LitLandings));
            Line(sb, "visitedfloors", JoinInts(Sorted(VisitedFloors)));
            Line(sb, "deepestfloor", DeepestFloor.ToString(Inv));
            Line(sb, "oncedone", JoinIds(OnceDone));
            Line(sb, "lastglyphs", JoinMap(LastGlyphs, Escape));
            Line(sb, "pouch", JoinMap(DiscoveryPouch, v => v.ToString(Inv)));
            Line(sb, "pouchgiven", JoinMap(DiscoveryXpGiven, v => v.ToString(Inv)));
            Line(sb, "surveysheets", JoinMap(SurveySheets, v => v.ToString(Inv)));
            Line(sb, "eventxpfloors", JoinInts(Sorted(EventXpFloors)));
            Line(sb, "counters", JoinMap(Counters, v => v.ToString(Inv)));
            Line(sb, "bosskills", JoinMap(BossKills, v => v.ToString(Inv)));
            Line(sb, "bosskillexp", JoinMap(BossKillExpedition, v => v.ToString(Inv)));
            Line(sb, "bosslosses", JoinMap(BossLosses, v => v.ToString(Inv)));
            Line(sb, "bossstakes", JoinIds(BossStakes));
            // 산 대장간 물건은 비어 있지 않을 때만 쓴다(재화 쓸 곳 1차 8장, 판본 2 그대로 — 산 것이 없는 글은 예전과 글자까지 같다).
            string bought = JoinIds(TownPurchases);
            if (bought.Length > 0) Line(sb, BoughtKey, bought);
            // 배운 기술 칸도 비어 있지 않을 때만 쓴다(배운 것이 없는 글은 예전과 같다).
            string learned = JoinIds(SkillNodes);
            if (learned.Length > 0) Line(sb, SkillNodesKey, learned);
            if (Leg != null)
            {
                Line(sb, "leg.hp", Leg.Hp.ToString(Inv));
                Line(sb, "leg.potions", Leg.Potions.ToString(Inv));
                Line(sb, "leg.start", Leg.StartRealtime.ToString("R", Inv));
                Line(sb, "leg.kills", Leg.Kills.ToString(Inv));
                Line(sb, "leg.chests", Leg.ChestsOpened.ToString(Inv));
                Line(sb, "leg.cellsvisited", Leg.CellsVisited.ToString(Inv));
                Line(sb, "leg.cellstotal", Leg.CellsTotal.ToString(Inv));
                Line(sb, "leg.stones", Leg.StonesGained.ToString(Inv));
                Line(sb, "leg.gold", Leg.GoldGained.ToString(Inv));
                if (Leg.Rested) Line(sb, "leg.rested", "1");
                if (Leg.RiceBalls > 0) Line(sb, "leg.rice", Leg.RiceBalls.ToString(Inv));
                if (Leg.Downs > 0) Line(sb, "leg.downs", Leg.Downs.ToString(Inv));
                Line(sb, "leg.oil", Leg.Oil.ToString(Inv));
                Line(sb, "leg.floors", JoinInts(Leg.Floors));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 글에서 풀기. 첫 줄이 "carry v{n}"가 아니면 FormatException. 버전이 달라도 아는 키만 읽고(모르는 키·= 없는 줄·읽지 못한 값은 건너뜀)
        /// 나머지는 새 프로필 기본값이다. 풀린 꾸러미는 지금 버전(CurrentVersion)이다.
        /// </summary>
        public static CarryData FromText(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new FormatException("꾸러미 글이 비어 있다");
            var lines = text.Replace("\r", "").Split('\n');
            string head = lines[0].Trim();
            if (!head.StartsWith(Header, StringComparison.Ordinal) || !int.TryParse(head.Substring(Header.Length), NumberStyles.Integer, Inv, out _))
                throw new FormatException("꾸러미 글 첫 줄이 '" + Header + "{버전}'이 아니다: " + head);

            var d = new CarryData();
            d._overflowRunes = new List<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                d.Read(line.Substring(0, eq).Trim(), line.Substring(eq + 1));
            }
            // 장비 홈에 들어가지 못한 룬(칸 넘침·같은 룬 두 번·장비를 버림)은 주머니로(6-6). 줄 차례와 상관없이 마지막에 더한다.
            foreach (var id in d._overflowRunes) d.AddRunes(id);
            d._overflowRunes = null;
            d.Version = CurrentVersion;
            return d;
        }

        /// <summary>FromText 동안만 쓰는 넘친 룬 모음(글로 담지 않음).</summary>
        List<string> _overflowRunes;

        /// <summary>깊은 복사(ToText → FromText와 같은 결과).</summary>
        public CarryData Clone() => FromText(ToText());

        /// <summary>키 하나를 읽는다. 모르는 키나 읽지 못한 값은 건너뛴다(기본값 유지).</summary>
        void Read(string key, string value)
        {
            switch (key)
            {
                case "salt":
                    if (ulong.TryParse(value, NumberStyles.Integer, Inv, out ulong salt)) ProfileSalt = salt;
                    break;
                case "expedition": ReadInt(value, ref Expedition); break;
                case "night":
                    if (Enum.TryParse(value, out NightEvent night) && Enum.IsDefined(typeof(NightEvent), night)) Night = night;
                    break;
                case "level": ReadInt(value, ref Level); break;
                case "totalxp": ReadInt(value, ref TotalXp); break;
                case "skillpoints": ReadInt(value, ref SkillPoints); break;
                case "skillranks": SkillRanks = SplitInts(value).ToArray(); break;
                case "equipped":
                    // 판본 1: 낀 무기 한 줄(id|등급|아이템 레벨|굴림)을 eq.weapon으로 읽는다.
                    ReadEquip(GearSlot.Weapon, value);
                    break;
                case "bag":
                    Bag.Clear();
                    foreach (var part in SplitList(value))
                    {
                        var g = ParseGear(part, _overflowRunes);
                        if (g != null) Bag.Add(g);
                    }
                    break;
                case "stones": ReadInt(value, ref Stones); break;
                case "gold": ReadInt(value, ref Gold); break;
                case RunesKey: ReadPouch(value); break;
                case "pickaxe": HasPickaxe = ParseBool(value); break;
                case "key": HasKey = ParseBool(value); break;
                case "ropedepth": ReadInt(value, ref RopeDepth); break;
                case "litlandings":
                    LitLandings.Clear();
                    foreach (int f in SplitInts(value)) LitLandings.Add(f);
                    break;
                case "visitedfloors":
                    VisitedFloors.Clear();
                    foreach (int f in SplitInts(value)) VisitedFloors.Add(f);
                    break;
                case "deepestfloor": ReadInt(value, ref DeepestFloor); break;
                case "oncedone": ReadIds(value, OnceDone); break;
                case "lastglyphs": ReadMap(value, LastGlyphs, Unescape); break;
                case "pouch": ReadIntMap(value, DiscoveryPouch); break;
                case "pouchgiven": ReadIntMap(value, DiscoveryXpGiven); break;
                case "surveysheets": ReadIntMap(value, SurveySheets); break;
                case "eventxpfloors":
                    EventXpFloors.Clear();
                    foreach (int f in SplitInts(value)) EventXpFloors.Add(f);
                    break;
                case "counters": ReadMap(value, Counters); break;
                case "bosskills": ReadMap(value, BossKills); break;
                case "bosskillexp": ReadMap(value, BossKillExpedition); break;
                case "bosslosses": ReadMap(value, BossLosses); break;
                case "bossstakes": ReadIds(value, BossStakes); break;
                case BoughtKey: ReadIds(value, TownPurchases); break;
                case SkillNodesKey: ReadIds(value, SkillNodes); break;
                default:
                    if (key.StartsWith("leg.", StringComparison.Ordinal)) ReadLeg(key.Substring(4), value);
                    else if (key.StartsWith(EquipKeyPrefix, StringComparison.Ordinal) &&
                             GearSlots.TryParseKey(key.Substring(EquipKeyPrefix.Length), out var slot))
                        ReadEquip(slot, value);
                    break;
            }
        }

        /// <summary>장착 줄 키 머리(eq.weapon … eq.amulet).</summary>
        const string EquipKeyPrefix = "eq.";

        /// <summary>
        /// 장착 한 칸을 읽는다. 비었거나 읽지 못했거나 부위가 맞지 않으면 null(앞 5자리는 시작 장비, 반지·목걸이는 빈 자리).
        /// 부위가 맞지 않아 버리는 장비에 룬이 끼워져 있었으면 그 룬은 주머니로 보낸다(룬만 잃지 않게).
        /// </summary>
        void ReadEquip(GearSlot slot, string value)
        {
            var g = ParseGear(value, _overflowRunes);
            bool fits = Loadout.CanEquip(slot, g);
            if (!fits && g != null && _overflowRunes != null) _overflowRunes.AddRange(g.Runes);
            Equipment[(int)slot] = fits ? g : null;
        }

        static readonly string[] LegKeys = { "hp", "potions", "start", "kills", "chests", "cellsvisited", "cellstotal", "stones", "gold", "rested", "rice", "downs", "oil", "floors" };

        /// <summary>원정 몫 키(leg.*) 하나. 아는 키가 처음 나올 때 원정 몫을 만든다.</summary>
        void ReadLeg(string key, string value)
        {
            if (Array.IndexOf(LegKeys, key) < 0) return;
            if (Leg == null) Leg = new ExpeditionLeg();
            switch (key)
            {
                case "hp": ReadInt(value, ref Leg.Hp); break;
                case "potions": ReadInt(value, ref Leg.Potions); break;
                case "start":
                    if (float.TryParse(value, NumberStyles.Float, Inv, out float start)) Leg.StartRealtime = start;
                    break;
                case "kills": ReadInt(value, ref Leg.Kills); break;
                case "chests": ReadInt(value, ref Leg.ChestsOpened); break;
                case "cellsvisited": ReadInt(value, ref Leg.CellsVisited); break;
                case "cellstotal": ReadInt(value, ref Leg.CellsTotal); break;
                case "stones": ReadInt(value, ref Leg.StonesGained); break;
                case "gold": ReadInt(value, ref Leg.GoldGained); break;
                case "rested": Leg.Rested = value == "1"; break;
                case "rice": ReadInt(value, ref Leg.RiceBalls); break;
                case "downs": ReadInt(value, ref Leg.Downs); break;
                case "oil": ReadInt(value, ref Leg.Oil); break;
                case "floors":
                    Leg.Floors.Clear();
                    Leg.Floors.AddRange(SplitInts(value));
                    break;
            }
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const string EscapedChars = "%\n\r,:|=";

        static void Line(StringBuilder sb, string key, string value) => sb.Append(key).Append('=').Append(value).Append('\n');

        static void ReadInt(string value, ref int field)
        {
            if (int.TryParse(value, NumberStyles.Integer, Inv, out int n)) field = n;
        }

        static bool ParseBool(string value)
        {
            string v = value.Trim();
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        static List<int> Sorted(IEnumerable<int> values)
        {
            var list = new List<int>(values);
            list.Sort();
            return list;
        }

        static string JoinInts(IEnumerable<int> values)
        {
            var parts = new List<string>();
            foreach (int v in values) parts.Add(v.ToString(Inv));
            return string.Join(",", parts);
        }

        static IEnumerable<string> SplitList(string value)
        {
            if (string.IsNullOrEmpty(value)) yield break;
            foreach (var part in value.Split(','))
                if (part.Length > 0) yield return part;
        }

        static List<int> SplitInts(string value)
        {
            var list = new List<int>();
            foreach (var part in SplitList(value))
                if (int.TryParse(part, NumberStyles.Integer, Inv, out int n)) list.Add(n);
            return list;
        }

        static string JoinMap<T>(Dictionary<int, T> map, Func<T, string> write)
        {
            var keys = new List<int>(map.Keys);
            keys.Sort();
            var parts = new List<string>();
            foreach (int k in keys) parts.Add(k.ToString(Inv) + ":" + write(map[k]));
            return string.Join(",", parts);
        }

        static void ReadMap(string value, Dictionary<int, string> map, Func<string, string> read)
        {
            map.Clear();
            foreach (var part in SplitList(value))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(part.Substring(0, colon), NumberStyles.Integer, Inv, out int k)) map[k] = read(part.Substring(colon + 1));
            }
        }

        static void ReadIntMap(string value, Dictionary<int, int> map)
        {
            map.Clear();
            foreach (var part in SplitList(value))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(part.Substring(0, colon), NumberStyles.Integer, Inv, out int k) &&
                    int.TryParse(part.Substring(colon + 1), NumberStyles.Integer, Inv, out int v))
                    map[k] = v;
            }
        }

        /// <summary>글 열쇠 사전(세기·보스 기록): Escape(키):값을 쉼표로 잇는다. 같은 값이면 같은 글이 나오게 키를 Ordinal 차례로 쓴다.</summary>
        static string JoinMap<T>(Dictionary<string, T> map, Func<T, string> write)
        {
            var keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            var parts = new List<string>();
            foreach (var k in keys) parts.Add(Escape(k) + ":" + write(map[k]));
            return string.Join(",", parts);
        }

        /// <summary>JoinMap(글 열쇠)을 되돌린다. 키가 비었거나 값이 숫자가 아닌 조각은 건너뛴다.</summary>
        static void ReadMap(string value, Dictionary<string, int> map)
        {
            map.Clear();
            foreach (var part in SplitList(value))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(part.Substring(colon + 1), NumberStyles.Integer, Inv, out int v)) map[Unescape(part.Substring(0, colon))] = v;
            }
        }

        /// <summary>id 모음(한 번 받는 것·보스방 앞 말뚝): Escape(id)를 Ordinal 차례로 쉼표로 잇는다.</summary>
        static string JoinIds(IEnumerable<string> ids)
        {
            var sorted = new List<string>(ids);
            sorted.Sort(StringComparer.Ordinal);
            var parts = new List<string>();
            foreach (var id in sorted) parts.Add(Escape(id));
            return string.Join(",", parts);
        }

        /// <summary>JoinIds를 되돌린다.</summary>
        static void ReadIds(string value, HashSet<string> set)
        {
            set.Clear();
            foreach (var part in SplitList(value)) set.Add(Unescape(part));
        }

        /// <summary>룬 주머니 줄 키("runes=rune_superarmor:2").</summary>
        const string RunesKey = "runes";

        /// <summary>산 대장간 물건 줄 키("bought=forge.bag25,forge.flask", 재화 쓸 곳 1차 8장).</summary>
        const string BoughtKey = "bought";
        const string SkillNodesKey = "skillnodes";

        /// <summary>룬 주머니 글: Escape(id):개수를 id Ordinal 차례로 쉼표로 잇는다. 0개 이하 줄은 뺀다(같은 값이면 같은 글).</summary>
        static string JoinPouch(Dictionary<string, int> pouch)
        {
            var keys = new List<string>();
            foreach (var kv in pouch)
                if (kv.Value > 0 && !string.IsNullOrEmpty(kv.Key)) keys.Add(kv.Key);
            if (keys.Count == 0) return "";
            keys.Sort(StringComparer.Ordinal);
            var parts = new List<string>(keys.Count);
            foreach (var k in keys) parts.Add(Escape(k) + ":" + pouch[k].ToString(Inv));
            return string.Join(",", parts);
        }

        /// <summary>룬 주머니 줄을 읽는다(주머니를 비우고 다시 채움). 모르는 룬·읽지 못한 조각은 버리고, 개수는 RuneRules.PouchCap까지.</summary>
        void ReadPouch(string value)
        {
            RunePouch.Clear();
            foreach (var part in SplitList(value))
            {
                int colon = part.LastIndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(part.Substring(colon + 1), NumberStyles.Integer, Inv, out int n)) AddRunes(Unescape(part.Substring(0, colon)), n);
            }
        }

        /// <summary>
        /// 장비 글(판본 2, 장비 문서 12장 단계 2): Escape(종류 id)|등급(enum 이름)|아이템 레벨|굴림‰|강화|옵션|전설[|룬].
        /// 옵션 = 종류(OptionKind 이름)~값~단계를 ';'로 잇는다(굴린 차례). 전설 = Escape(id)~굴림‰, 없으면 빈 칸. 같은 장비면 같은 글이다.
        /// 룬(기획/세-무기-우클릭-소켓-1차.md 6-6) = 끼운 룬이 있을 때만 8번째 칸에 Escape(id)를 ';'로 잇는다(끼운 차례, 예 'wpn_greatsword|Rare|2|1050|0|||rune_superarmor').
        /// 룬이 없으면 7칸 그대로라 예전 글과 글자까지 같다. 옛 빌드는 8번째 칸을 읽지 않는다(룬만 잃고 장비는 산다).
        /// 강화 실패 수(재화 쓸 곳 1차 8장) = 0보다 클 때만 9번째 칸. 룬이 없으면 8번째 칸은 빈 칸이다(예 'wpn_longsword|Common|1|1000|5||||1').
        /// 실패 수가 0이면 예전 글과 글자까지 같다(판본 2 그대로).
        /// </summary>
        public static string GearText(GearItem g)
        {
            if (g == null) return "";
            var sb = new StringBuilder();
            sb.Append(Escape(g.BaseId)).Append('|').Append(g.Grade).Append('|')
                .Append(g.ItemLevel.ToString(Inv)).Append('|').Append(g.RollPermille.ToString(Inv)).Append('|')
                .Append(g.Enhance.ToString(Inv)).Append('|');
            for (int i = 0; i < g.Options.Count; i++)
            {
                var o = g.Options[i];
                if (i > 0) sb.Append(OptionSeparator);
                sb.Append(o.Kind).Append(PieceSeparator).Append(o.Value.ToString(Inv)).Append(PieceSeparator).Append(o.Tier.ToString(Inv));
            }
            sb.Append('|');
            if (g.IsLegendary) sb.Append(Escape(g.LegendaryId)).Append(PieceSeparator).Append(g.LegendaryRollPermille.ToString(Inv));
            if (g.Runes.Count > 0 || g.EnhanceFails > 0)
            {
                sb.Append('|');
                for (int i = 0; i < g.Runes.Count; i++)
                {
                    if (i > 0) sb.Append(OptionSeparator);
                    sb.Append(Escape(g.Runes[i]));
                }
            }
            if (g.EnhanceFails > 0) sb.Append('|').Append(g.EnhanceFails.ToString(Inv));
            return sb.ToString();
        }

        const char OptionSeparator = ';';
        const char PieceSeparator = '~';

        /// <summary>
        /// 장비 글을 읽는다. 비었거나, 칸이 4개보다 적거나, 종류 id·등급·아이템 레벨·굴림을 읽지 못하면 null.
        /// 4칸(판본 1 무기: id|등급|아이템 레벨|굴림)은 강화 0·옵션·전설 없는 장비로 읽는다.
        /// 모르는 옵션 종류나 읽지 못한 옵션 조각, 모르는 전설은 그 조각만 버리고 장비는 살린다. 강화를 읽지 못하면 0.
        /// 7칸(룬 칸 없음)은 빈 홈으로 읽는다. 룬 칸의 모르는 id는 버리고, 홈에 들어가지 못한 룬도 버린다(꾸러미 글 읽기는 넘친 룬을 주머니로 보내는 아래 갈래를 씀).
        /// 9번째 칸(강화 실패 수, 재화 쓸 곳 1차 8장)이 없거나 읽지 못하면 0이다.
        /// </summary>
        public static GearItem ParseGear(string value) => ParseGear(value, null);

        /// <summary>
        /// 장비 글을 읽고, 아는 룬인데 홈에 들어가지 못한 것(칸 넘침·같은 룬 두 번·무기가 아님)은 overflow에 담는다(6-6 '넘친 룬은 주머니로').
        /// 장비를 읽지 못하면(null) 룬 칸도 읽지 않는다. overflow가 null이면 넘친 룬은 버린다.
        /// </summary>
        public static GearItem ParseGear(string value, ICollection<string> overflow)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var p = value.Split('|');
            if (p.Length < 4) return null;
            string id = Unescape(p[0]);
            if (GearBaseTable.Get(id) == null) return null;
            if (!Enum.TryParse(p[1], out Grade grade) || !Enum.IsDefined(typeof(Grade), grade)) return null;
            if (!int.TryParse(p[2], NumberStyles.Integer, Inv, out int itemLevel)) return null;
            if (!int.TryParse(p[3], NumberStyles.Integer, Inv, out int roll)) return null;
            int enhance = 0;
            if (p.Length > 4 && !int.TryParse(p[4], NumberStyles.Integer, Inv, out enhance)) enhance = 0;
            var options = new List<GearOption>();
            if (p.Length > 5)
                foreach (var piece in p[5].Split(OptionSeparator))
                    if (TryParseOption(piece, out var option)) options.Add(option);
            string legendaryId = null;
            int legendaryRoll = 0;
            if (p.Length > 6 && p[6].Length > 0)
            {
                int cut = p[6].LastIndexOf(PieceSeparator);
                if (cut > 0)
                {
                    string legId = Unescape(p[6].Substring(0, cut));
                    if (LegendaryTable.Get(legId) != null && int.TryParse(p[6].Substring(cut + 1), NumberStyles.Integer, Inv, out legendaryRoll))
                        legendaryId = legId;
                }
            }
            List<string> runes = null;
            if (p.Length > 7 && p[7].Length > 0)
            {
                var listed = new List<string>();
                foreach (var piece in p[7].Split(OptionSeparator))
                    if (piece.Length > 0) listed.Add(Unescape(piece));
                runes = new List<string>(RuneRules.MaxSockets);
                RuneRules.Fit(GearBaseTable.Get(id).Part, grade, listed, runes, overflow);
            }
            int fails = 0;
            if (p.Length > 8 && !int.TryParse(p[8], NumberStyles.Integer, Inv, out fails)) fails = 0;
            return new GearItem(id, grade, itemLevel, roll, enhance, options, legendaryId, legendaryId != null ? legendaryRoll : 0, runes, fails);
        }

        /// <summary>옵션 조각 하나(종류~값~단계). 모르는 종류나 숫자가 아니면 false.</summary>
        static bool TryParseOption(string piece, out GearOption option)
        {
            option = default;
            if (string.IsNullOrEmpty(piece)) return false;
            var q = piece.Split(PieceSeparator);
            if (q.Length != 3) return false;
            if (!Enum.TryParse(q[0], out OptionKind kind) || !Enum.IsDefined(typeof(OptionKind), kind)) return false;
            if (!int.TryParse(q[1], NumberStyles.Integer, Inv, out int v)) return false;
            if (!int.TryParse(q[2], NumberStyles.Integer, Inv, out int tier)) return false;
            option = new GearOption(kind, v, tier);
            return true;
        }

        /// <summary>나누는 글자('%' 줄바꿈 ',' ':' '|' '=')를 %XX로 바꾼다.</summary>
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (EscapedChars.IndexOf(c) >= 0) sb.Append('%').Append(((int)c).ToString("X2", Inv));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Escape를 되돌린다(잘못된 %는 그대로 둔다).</summary>
        public static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('%') < 0) return s ?? "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '%' && i + 2 < s.Length && int.TryParse(s.Substring(i + 1, 2), NumberStyles.HexNumber, Inv, out int code))
                {
                    sb.Append((char)code);
                    i += 2;
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
