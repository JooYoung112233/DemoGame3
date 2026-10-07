using System;
using System.Globalization;
using System.Text;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;

namespace Demo6.Core.TestStart
{
    /// <summary>
    /// 바로 가기 시험 메뉴(TestLauncher)의 시작 위치(2026-10-04 사용자 "테스트환경을 제공해줘" → '바로 가기 시험 메뉴').
    /// 차례(정수)는 설정 글에 이름으로 담으므로 바꿔도 되지만, 새 값은 끝에 붙인다.
    /// </summary>
    public enum TestStartAt
    {
        /// <summary>마을 처음: 새 프로필, 남쪽 길 끝에서 오프닝부터(의뢰 단계는 늘 '처음').</summary>
        TownFresh,
        /// <summary>마을: 고른 의뢰 단계까지 지난 꾸러미로 귀환 지점에 도착(단계가 '처음'이면 마을 처음과 같음).</summary>
        Town,
        /// <summary>던전 1층 승강장.</summary>
        Floor1,
        /// <summary>던전 2층 승강장.</summary>
        Floor2,
        /// <summary>2층 계단 아래 오우거 굴, 굴 앞 말뚝(켠 채).</summary>
        DenFront,
        /// <summary>오우거 굴 보스방 문 안쪽에서 시작해 곧바로 오우거를 깨운다.</summary>
        DenFight,
        /// <summary>전투 시험장(CombatTest) 프리셋.</summary>
        CombatTest,
    }

    /// <summary>
    /// 의뢰 진행 단계(정식 흐름의 점검 지점). 각 단계는 앞 단계에 정식 흐름을 이어 붙인 것이고,
    /// '…뒤'는 마을에 올라와 도착 처리를 마친 뒤 아직 아무와도 말하지 않은 때다(TestStartBuilder.ApplyStage가 차례를 정한다).
    /// </summary>
    public enum TestQuestStage
    {
        /// <summary>처음: 새 프로필(키 없음, 원정 1, 이름 모두 '?').</summary>
        Fresh,
        /// <summary>첫 귀환 뒤: 오프닝(춘삼·옥금 이름 앎, 두 의뢰 받음) → 원정 1(1층 내려섬 → 바구니로 올라옴) → 마을 도착. 알릴 일 '갱도로 내려가기'.</summary>
        AfterFirstReturn,
        /// <summary>궁수까지 끝: 첫 귀환 보고·받기(버팀목 길·궁수) → 원정 2(굴쥐 8·2층·궁수 3·2층 계단 앞 말뚝) → 마을 도착. 알릴 일 셋, 무진은 아직 '?'.</summary>
        ArcherDone,
        /// <summary>오우거 받음: 알릴 일 셋을 보고(무진 이름 공개) → 같은 대화에서 '굴의 큰 놈'을 받음.</summary>
        OgreAccepted,
        /// <summary>오우거 처치 뒤: 원정 3(2층 → 굴 앞 말뚝 → 오우거 첫 처치) → 마을 도착. 알릴 일 '굴의 큰 놈'.</summary>
        OgreKilled,
    }

    /// <summary>장비 묶음. 시작 장비 = 정식 시작 장비(무기 종류만 고른 것으로), 그 밖은 7부위 8자리 한 벌(고른 등급·장비 층).</summary>
    public enum TestGearSet
    {
        Starting,
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>
    /// 바로 가기 시험 메뉴의 설정 값 한 벌(Core, UnityEngine 없음). 화면(TestLauncher)이 고치고, 설정 기억(PlayerPrefs)에는 ToText 글로 담는다.
    /// TestStartBuilder.Build가 이 값으로 정식 저장 형식 꾸러미(CarryData)와 장면 쪽지 값(TestStartPlan)을 만든다.
    /// 게임 규칙 값이 아니라 '시작 상태 만들기' 손잡이다. 기본값 = 마을 처음 · 레벨 단계대로 · 장검 · 시작 장비 · 손잡이 모두 끔 · 룬 0 · 시작 무기 룬 끔.
    /// 룬 두 손잡이(runes·startrune)는 판본을 올리지 않고 키만 더했다(옛 설정 글은 0·끔으로 읽힘).
    /// 숫자키 무기 손잡이(weaponkeys, 검토 1차 4장 Q1)는 뺐다(기획/키-배치-1차.md 0장 2). 옛 설정 글의 weaponkeys 줄은 읽고 버린다(판본 그대로).
    /// </summary>
    public sealed class TestStartPreset
    {
        /// <summary>설정 글 판본(머리 "testpreset v1").</summary>
        public const int CurrentVersion = 1;
        /// <summary>설정 글 첫 줄 머리.</summary>
        public const string Header = "testpreset v";
        /// <summary>고를 수 있는 가장 높은 레벨(1~10).</summary>
        public const int MaxLevel = 10;
        /// <summary>강화석·골드 넣기 상한.</summary>
        public const int MaxAdd = 100000;
        /// <summary>전설 효과 세기 기본값(‰, Tuning.DefaultLegendRoll과 같음).</summary>
        public const int DefaultLegendRoll = 500;
        /// <summary>전투 시험장 프리셋 기본값(CombatTestRoot.Preset 이름).</summary>
        public const string DefaultCombatPreset = "FrontBack";

        // ── ① 시작 위치·⑤ 의뢰 단계 ──
        public TestStartAt StartAt = TestStartAt.TownFresh;
        /// <summary>의뢰 단계. 마을 처음(TownFresh)이면 무시하고 '처음'(EffectiveStage).</summary>
        public TestQuestStage Stage = TestQuestStage.Fresh;

        // ── ② 레벨·스킬 ──
        /// <summary>레벨 1~10. 0 = 단계대로(정식 흐름의 보고 보상으로 오른 레벨 그대로). 고른 레벨 구간 안이면 단계가 쌓은 경험치를 지킨다(TestStartBuilder.ApplyLevel).</summary>
        public int Level;
        /// <summary>스킬 점수 자동 배분(회오리 → 검풍 → 마무리 → 몸 차례로 한 점씩, 랭크 4까지). 끄면 점수를 그대로 남긴다.</summary>
        public bool AutoSkills = true;

        // ── ③ 무기·④ 장비 ──
        /// <summary>무기 종류 id(GearBaseTable 무기 9종). 모르는 id는 장검.</summary>
        public string WeaponId = GearBaseTable.Longsword;
        public TestGearSet GearSet = TestGearSet.Starting;
        /// <summary>장비 층 = 아이템 레벨(1~GearMath.MaxItemLevel). 시작 장비 묶음은 iLv1 그대로.</summary>
        public int GearLevel = 1;
        /// <summary>방어구 무게(가죽·사슬·판금). None이면 가죽.</summary>
        public ArmorWeight ArmorWeight = ArmorWeight.Light;
        /// <summary>전설 효과 켜기(LegendaryEffect 차례: 연쇄 번개·불꽃 발자국·연쇄 폭발). 전투 시험장에서는 Tuning.TestLegendOn으로 넣는다.</summary>
        public readonly bool[] LegendOn = new bool[LegendaryTable.Count];
        /// <summary>전설 효과 세기(‰, 0~1000).</summary>
        public int LegendRoll = DefaultLegendRoll;

        // ── ⑥ 시험 손잡이 ──
        public bool Invincible;
        /// <summary>적 없음: 던전 칸 무리·둥지를 놓지 않는다(보스방 오우거는 그대로).</summary>
        public bool NoEnemies;
        public bool DarknessOff;
        public bool VisionOff;
        /// <summary>씨앗 고정: 켜면 프로필 소금과 시작 층 지도 씨앗(굴 제외)을 Seed로, 장비 옵션 굴림도 Seed로 정한다.</summary>
        public bool FixSeed;
        public ulong Seed = 1UL;
        public int AddStones;
        public int AddGold;
        /// <summary>룬 +n: 주머니에 버팀 룬을 넣는다(0~RuneRules.PouchCap, 기획/세-무기-우클릭-소켓-1차.md 6장).</summary>
        public int AddRunes;
        /// <summary>시작 무기에 버팀 룬 끼우기(기본 꺼짐). 던전·마을은 꾸러미 무기에 끼우고, 전투 시험장은 Tuning.TestSuperArmorRune으로 대신한다.</summary>
        public bool StartWeaponRune;

        // ── 전투 시험장 ──
        /// <summary>CombatTestRoot.Preset 이름(Game이 해석, 모르는 이름은 FrontBack).</summary>
        public string CombatPreset = DefaultCombatPreset;
        /// <summary>전투 시험장 층(1~FloorScaling.MaxFloor).</summary>
        public int CombatFloor = 1;

        /// <summary>실제로 쓰는 의뢰 단계(마을 처음이면 '처음').</summary>
        public TestQuestStage EffectiveStage => StartAt == TestStartAt.TownFresh ? TestQuestStage.Fresh : Stage;

        /// <summary>⑥ 시험 손잡이를 하나라도 켰나(무적·적 없음·어둠 끄기·시야 끄기·씨앗 고정·강화석·골드·룬 넣기·시작 무기 룬).</summary>
        public bool UsesKnobs =>
            Invincible || NoEnemies || DarknessOff || VisionOff || FixSeed || AddStones > 0 || AddGold > 0 || AddRunes > 0 || StartWeaponRune;

        /// <summary>
        /// 정식 새 판과 같은 시작인가(4장 Q7 '시험 판' 표시): 마을 처음 · 레벨 단계대로(또는 1) · 장검 · 시작 장비 · 시험 손잡이 모두 끔.
        /// 시작 장비는 장비 층·방어구 무게·전설 켜기를 쓰지 않고, 레벨 1 이하·스킬 자동 배분은 새 프로필과 같아서 보지 않는다. 전투 시험장 값(프리셋·층)도 보지 않는다.
        /// 아니면 Game이 이번 판을 '시험 판(시험 메뉴 시작)'으로 적는다.
        /// </summary>
        public bool IsPlainStart =>
            StartAt == TestStartAt.TownFresh && Level <= 1 && WeaponId == GearBaseTable.Longsword && GearSet == TestGearSet.Starting && !UsesKnobs;

        /// <summary>깊은 복사.</summary>
        public TestStartPreset Clone()
        {
            var c = new TestStartPreset
            {
                StartAt = StartAt,
                Stage = Stage,
                Level = Level,
                AutoSkills = AutoSkills,
                WeaponId = WeaponId,
                GearSet = GearSet,
                GearLevel = GearLevel,
                ArmorWeight = ArmorWeight,
                LegendRoll = LegendRoll,
                Invincible = Invincible,
                NoEnemies = NoEnemies,
                DarknessOff = DarknessOff,
                VisionOff = VisionOff,
                FixSeed = FixSeed,
                Seed = Seed,
                AddStones = AddStones,
                AddGold = AddGold,
                AddRunes = AddRunes,
                StartWeaponRune = StartWeaponRune,
                CombatPreset = CombatPreset,
                CombatFloor = CombatFloor,
            };
            Array.Copy(LegendOn, c.LegendOn, Math.Min(LegendOn.Length, c.LegendOn.Length));
            return c;
        }

        /// <summary>
        /// 값 고르기: 레벨 0~MaxLevel, 장비 층 1~GearMath.MaxItemLevel, 전설 세기 0~1000, 강화석·골드 0~MaxAdd, 룬 0~RuneRules.PouchCap, 전투 층 1~FloorScaling.MaxFloor,
        /// 무기가 아니거나 모르는 WeaponId는 장검, ArmorWeight None은 가죽, 빈 CombatPreset은 기본값. 자신을 돌려준다.
        /// 표에 없는 enum 값(정수로 넣은 것)은 기본값으로 돌린다.
        /// </summary>
        public TestStartPreset Normalize()
        {
            if (!Enum.IsDefined(typeof(TestStartAt), StartAt)) StartAt = TestStartAt.TownFresh;
            if (!Enum.IsDefined(typeof(TestQuestStage), Stage)) Stage = TestQuestStage.Fresh;
            if (!Enum.IsDefined(typeof(TestGearSet), GearSet)) GearSet = TestGearSet.Starting;
            Level = Clamp(Level, 0, MaxLevel);
            var weapon = GearBaseTable.Get(WeaponId);
            if (weapon == null || !weapon.IsWeapon) WeaponId = GearBaseTable.Longsword;
            GearLevel = Clamp(GearLevel, 1, GearMath.MaxItemLevel);
            if (ArmorWeight != ArmorWeight.Light && ArmorWeight != ArmorWeight.Medium && ArmorWeight != ArmorWeight.Heavy) ArmorWeight = ArmorWeight.Light;
            LegendRoll = Clamp(LegendRoll, 0, 1000);
            AddStones = Clamp(AddStones, 0, MaxAdd);
            AddGold = Clamp(AddGold, 0, MaxAdd);
            AddRunes = Clamp(AddRunes, 0, RuneRules.PouchCap);
            if (string.IsNullOrWhiteSpace(CombatPreset)) CombatPreset = DefaultCombatPreset;
            else CombatPreset = CombatPreset.Trim();
            CombatFloor = Clamp(CombatFloor, 1, FloorScaling.MaxFloor);
            return this;
        }

        /// <summary>
        /// 설정 글(설정 기억). 첫 줄 "testpreset v1", 그 뒤 한 줄에 key=value 하나(enum은 이름, bool은 1/0, 전설은 "legend=1,0,1").
        /// 같은 값이면 같은 글이다. 자유 글(무기 id·전투 프리셋 이름)은 CarryData.Escape로 나누는 글자를 %XX로 바꾼다.
        /// </summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append(CurrentVersion.ToString(Inv)).Append('\n');
            Line(sb, "start", StartAt.ToString());
            Line(sb, "stage", Stage.ToString());
            Line(sb, "level", Level.ToString(Inv));
            Line(sb, "autoskills", Bool(AutoSkills));
            Line(sb, "weapon", CarryData.Escape(WeaponId));
            Line(sb, "gearset", GearSet.ToString());
            Line(sb, "gearlevel", GearLevel.ToString(Inv));
            Line(sb, "armor", ArmorWeight.ToString());
            var legend = new string[LegendOn.Length];
            for (int i = 0; i < LegendOn.Length; i++) legend[i] = Bool(LegendOn[i]);
            Line(sb, "legend", string.Join(",", legend));
            Line(sb, "legendroll", LegendRoll.ToString(Inv));
            Line(sb, "invincible", Bool(Invincible));
            Line(sb, "noenemies", Bool(NoEnemies));
            Line(sb, "darknessoff", Bool(DarknessOff));
            Line(sb, "visionoff", Bool(VisionOff));
            Line(sb, "fixseed", Bool(FixSeed));
            Line(sb, "seed", Seed.ToString(Inv));
            Line(sb, "stones", AddStones.ToString(Inv));
            Line(sb, "gold", AddGold.ToString(Inv));
            Line(sb, "runes", AddRunes.ToString(Inv));
            Line(sb, "startrune", Bool(StartWeaponRune));
            Line(sb, "combatpreset", CarryData.Escape(CombatPreset));
            Line(sb, "combatfloor", CombatFloor.ToString(Inv));
            return sb.ToString();
        }

        /// <summary>
        /// 설정 글을 읽는다. 비었거나 첫 줄 머리가 다르면 false(preset = null). 모르는 키·읽지 못한 값은 건너뛰고(기본값 유지) Normalize한다.
        /// 예외를 던지지 않는다.
        /// </summary>
        public static bool TryParse(string text, out TestStartPreset preset)
        {
            preset = null;
            try
            {
                if (string.IsNullOrEmpty(text)) return false;
                var lines = text.Replace("\r", "").Split('\n');
                string head = lines[0].Trim();
                if (!head.StartsWith(Header, StringComparison.Ordinal) ||
                    !int.TryParse(head.Substring(Header.Length), NumberStyles.Integer, Inv, out _))
                    return false;
                var p = new TestStartPreset();
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    p.Read(line.Substring(0, eq).Trim(), line.Substring(eq + 1));
                }
                preset = p.Normalize();
                return true;
            }
            catch (Exception)
            {
                preset = null;
                return false;
            }
        }

        // ── 글 읽기·쓰기 바탕(CarryData 글 규칙을 본뜸) ──

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        static string Bool(bool on) => on ? "1" : "0";

        static void Line(StringBuilder sb, string key, string value) => sb.Append(key).Append('=').Append(value).Append('\n');

        /// <summary>키 하나를 읽는다. 모르는 키나 읽지 못한 값은 건너뛴다(기본값 유지).</summary>
        void Read(string key, string value)
        {
            switch (key)
            {
                case "start": ReadEnum(value, ref StartAt); break;
                case "stage": ReadEnum(value, ref Stage); break;
                case "level": ReadInt(value, ref Level); break;
                case "autoskills": ReadBool(value, ref AutoSkills); break;
                case "weapon":
                    if (value.Length > 0) WeaponId = CarryData.Unescape(value.Trim());
                    break;
                case "gearset": ReadEnum(value, ref GearSet); break;
                case "gearlevel": ReadInt(value, ref GearLevel); break;
                case "armor": ReadEnum(value, ref ArmorWeight); break;
                case "legend":
                    var parts = value.Split(',');
                    for (int i = 0; i < parts.Length && i < LegendOn.Length; i++)
                        if (TryBool(parts[i], out bool on)) LegendOn[i] = on;
                    break;
                case "legendroll": ReadInt(value, ref LegendRoll); break;
                case "invincible": ReadBool(value, ref Invincible); break;
                case "noenemies": ReadBool(value, ref NoEnemies); break;
                case "darknessoff": ReadBool(value, ref DarknessOff); break;
                case "visionoff": ReadBool(value, ref VisionOff); break;
                case "fixseed": ReadBool(value, ref FixSeed); break;
                case "seed":
                    if (ulong.TryParse(value.Trim(), NumberStyles.Integer, Inv, out ulong seed)) Seed = seed;
                    break;
                case "stones": ReadInt(value, ref AddStones); break;
                case "gold": ReadInt(value, ref AddGold); break;
                case "runes": ReadInt(value, ref AddRunes); break;
                case "startrune": ReadBool(value, ref StartWeaponRune); break;
                // 옛 숫자키 무기 손잡이(키 배치 1차 0장 2에서 뺌): 옛 설정 글이 오류 없이 읽히게 읽고 버린다.
                case "weaponkeys": break;
                case "combatpreset":
                    if (value.Length > 0) CombatPreset = CarryData.Unescape(value.Trim());
                    break;
                case "combatfloor": ReadInt(value, ref CombatFloor); break;
            }
        }

        static void ReadInt(string value, ref int field)
        {
            if (int.TryParse(value.Trim(), NumberStyles.Integer, Inv, out int n)) field = n;
        }

        static void ReadBool(string value, ref bool field)
        {
            if (TryBool(value, out bool on)) field = on;
        }

        /// <summary>1/0(또는 true/false). 그 밖은 읽지 못함.</summary>
        static bool TryBool(string value, out bool on)
        {
            string v = value.Trim();
            on = v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            return on || v == "0" || string.Equals(v, "false", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>enum 이름 하나만 읽는다(숫자 글·쉼표로 이은 이름·표에 없는 값은 건너뜀).</summary>
        static void ReadEnum<T>(string value, ref T field) where T : struct
        {
            string v = value.Trim();
            if (v.Length == 0 || char.IsDigit(v[0]) || v[0] == '-' || v[0] == '+' || v.IndexOf(',') >= 0) return;
            if (Enum.TryParse(v, out T parsed) && Enum.IsDefined(typeof(T), parsed)) field = parsed;
        }
    }
}
