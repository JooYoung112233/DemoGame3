using System;
using System.Collections.Generic;
using System.Globalization;

namespace Demo6.Core.Progression
{
    /// <summary>
    /// 랭크로 올리는 칸(3차 초안 4-5 1줄 4칸 + 끓는 피). 값은 SkillRanks[(int)id]에 저장된다 — 차례를 바꾸지 않는다(저장 글 호환).
    /// 배우기·갈림 칸은 SkillNodeId로 따로 센다(기획/스킬-자원-트리-1차.md).
    /// </summary>
    public enum SkillId
    {
        /// <summary>회오리 갈래: 넓은 회오리.</summary>
        WideWhirl,
        /// <summary>검풍 갈래: 날 선 바람.</summary>
        SharpWind,
        /// <summary>몸 갈래: 마무리 일격.</summary>
        Finisher,
        /// <summary>몸 갈래: 질긴 몸.</summary>
        ToughBody,
        /// <summary>몸 갈래: 끓는 피(투지 얻는 양).</summary>
        BoilingBlood,
    }

    /// <summary>
    /// 랭크 칸 하나. 효과 값 = 기본값 + 랭크당 값 × 랭크. 랭크 0이면 M0a 그대로(4-5 '스킬 0점 = M0a 그대로').
    /// </summary>
    public sealed class SkillDef
    {
        public const int MaxRank = 4;
        /// <summary>1줄은 Lv 2부터(4-5).</summary>
        public const int RequiredLevel = 2;

        public readonly SkillId Id;
        /// <summary>문자열 id(시험 패널·기록용).</summary>
        public readonly string Key;
        /// <summary>갈래 이름(회오리, 검풍, 몸).</summary>
        public readonly string Branch;
        public readonly string Name;
        /// <summary>랭크 0 값(M0a 값).</summary>
        public readonly double BaseValue;
        public readonly double PerRank;
        /// <summary>효과 문구 틀. {0}에 값이 들어간다.</summary>
        public readonly string Format;

        public SkillDef(SkillId id, string key, string branch, string name, double baseValue, double perRank, string format)
        {
            Id = id;
            Key = key;
            Branch = branch;
            Name = name;
            BaseValue = baseValue;
            PerRank = perRank;
            Format = format;
        }

        public static int ClampRank(int rank) => Math.Max(0, Math.Min(MaxRank, rank));

        /// <summary>랭크에서 더해지는 값(랭크 0이면 0).</summary>
        public double BonusAt(int rank) => PerRank * ClampRank(rank);

        /// <summary>랭크에서의 효과 값(기본값 + 더해지는 값).</summary>
        public double ValueAt(int rank) => BaseValue + BonusAt(rank);

        /// <summary>화면에 쓸 효과 문구(예: '회오리 반경 3.0').</summary>
        public string EffectText(int rank) => string.Format(CultureInfo.InvariantCulture, Format, ValueAt(rank));

        /// <summary>랭크당 효과 설명(예: '+0.2/랭크').</summary>
        public string PerRankText => Id switch
        {
            SkillId.WideWhirl => "랭크당 반경 +0.2",
            SkillId.SharpWind => "랭크당 +25%p",
            SkillId.Finisher => "랭크당 마무리 피해 +6%",
            SkillId.BoilingBlood => "랭크당 투지 얻는 양 +10%",
            _ => "랭크당 레벨당 체력 +15 (지난 레벨에도)",
        };
    }

    /// <summary>스킬 트리 칸(기획/스킬-자원-트리-1차.md 3장).</summary>
    public enum SkillNodeId
    {
        LearnWhirl,
        WideWhirl,
        PullWhirl,
        BloodWhirl,
        LearnWave,
        SharpWind,
        ThreeWave,
        WallBurst,
        ToughBody,
        Finisher,
        BoilingBlood,
    }

    public enum SkillNodeKind
    {
        /// <summary>새 기술을 배운다(1점, 무진에게서).</summary>
        Learn,
        /// <summary>랭크를 올린다(1점씩, 어디서나 K).</summary>
        Rank,
        /// <summary>둘 가운데 하나만 고르는 변형(1점, 무진에게서).</summary>
        Choice,
    }

    /// <summary>트리 칸 하나. Col·Row는 창의 자리(왼쪽 → 오른쪽으로 레벨이 오른다, 토탈워 개혁 나무처럼 가로로 뻗는다).</summary>
    public sealed class SkillNodeDef
    {
        public SkillNodeId Id;
        public string Key;
        public SkillNodeKind Kind;
        public string Branch;
        public string Name;
        /// <summary>한 줄 설명(무엇이 바뀌나).</summary>
        public string Desc;
        public int RequiredLevel = SkillDef.RequiredLevel;
        /// <summary>먼저 있어야 하는 칸(없으면 null).</summary>
        public SkillNodeId? Parent;
        /// <summary>먼저 칸에 필요한 랭크(배우기 칸이면 1).</summary>
        public int ParentRank = 1;
        /// <summary>함께 고를 수 없는 칸(갈림).</summary>
        public SkillNodeId? Exclusive;
        /// <summary>마을 무진에게서만 배울 수 있다(배우기·갈림 칸).</summary>
        public bool Trainer;
        /// <summary>랭크 칸이면 그 랭크 자리.</summary>
        public SkillId? RankSkill;
        public float Col;
        public float Row;
        /// <summary>SkillGlyph 모양(0 회오리, 1 검풍, 2 마무리, 3 몸, 5 투지).</summary>
        public int Glyph;

        public int MaxRank => Kind == SkillNodeKind.Rank ? SkillDef.MaxRank : 1;
    }

    /// <summary>배우기 판정 결과.</summary>
    public readonly struct SkillCheck
    {
        public readonly bool Ok;
        /// <summary>안 되는 까닭(되면 '사용할 점수 1점').</summary>
        public readonly string Reason;
        /// <summary>마을 무진에게 가면 되는가(다른 조건은 다 맞음).</summary>
        public readonly bool NeedsTrainer;

        public SkillCheck(bool ok, string reason, bool needsTrainer = false)
        {
            Ok = ok;
            Reason = reason;
            NeedsTrainer = needsTrainer;
        }
    }

    /// <summary>
    /// 레벨·점수·랭크·배운 칸 한 묶음(PlayerProgress·시험 시작·마을이 같이 쓴다). 꾸러미에는 SkillPoints·SkillRanks·SkillNodes로 담긴다.
    /// </summary>
    public sealed class SkillState
    {
        public int Level = 1;
        public int Points;
        public readonly int[] Ranks = new int[SkillTree.Count];
        public readonly HashSet<string> Nodes = new HashSet<string>();

        public int Rank(SkillId id)
        {
            int i = (int)id;
            return i >= 0 && i < Ranks.Length ? Ranks[i] : 0;
        }

        /// <summary>칸의 지금 랭크(배우기·갈림 칸은 배웠으면 1).</summary>
        public int RankOf(SkillNodeDef node)
        {
            if (node == null) return 0;
            if (node.RankSkill.HasValue) return Rank(node.RankSkill.Value);
            return Nodes.Contains(node.Key) ? 1 : 0;
        }

        public int RankOf(SkillNodeId id) => RankOf(SkillTree.Node(id));

        public bool Has(SkillNodeId id) => RankOf(id) > 0;

        /// <summary>꾸러미 값으로 채운다(옛 저장은 SkillTree.Migrate로 랭크가 있는 기술을 배운 것으로 본다).</summary>
        public void Load(int level, int points, int[] ranks, IEnumerable<string> nodes)
        {
            Level = Math.Max(1, level);
            Points = Math.Max(0, points);
            for (int i = 0; i < Ranks.Length; i++) Ranks[i] = ranks != null && i < ranks.Length ? SkillDef.ClampRank(ranks[i]) : 0;
            Nodes.Clear();
            if (nodes != null)
                foreach (var n in nodes)
                    if (SkillTree.FindNode(n) != null) Nodes.Add(n);
            SkillTree.Migrate(this);
        }
    }

    /// <summary>
    /// 스킬 트리(기획/스킬-자원-트리-1차.md, 사용자 결정 2026-10-07):
    /// 처음에는 회오리·검풍이 없다. Lv 2부터 점수 1점으로 마을 무진에게 배운다(배우기 칸). 배운 기술은 K 창에서 어디서나 랭크를 올린다.
    /// Lv 5에 갈래마다 갈림 두 칸 가운데 하나를 무진에게 배운다. 몸 갈래(질긴 몸·마무리 일격)는 처음부터 K로 올리고, 끓는 피는 마무리 일격 1랭크 뒤 Lv 4부터.
    /// 랭크 칸 값(넓은 회오리 반경 2.6 → 3.4, 날 선 바람 350 → 450%, 마무리 +6%/랭크, 질긴 몸 +15/랭크)은 3차 초안 4-5 그대로.
    /// </summary>
    public static class SkillTree
    {
        public static readonly SkillDef WideWhirl =
            new SkillDef(SkillId.WideWhirl, "skill.wide_whirl", "회오리", "넓은 회오리", 2.6, 0.2, "회오리 반경 {0:0.0}");
        public static readonly SkillDef SharpWind =
            new SkillDef(SkillId.SharpWind, "skill.sharp_wind", "검풍", "날 선 바람", 350.0, 25.0, "검풍 피해 {0:0}%");
        public static readonly SkillDef Finisher =
            new SkillDef(SkillId.Finisher, "skill.finisher", "몸", "마무리 일격", 0.0, 6.0, "마무리 피해 +{0:0}%");
        public static readonly SkillDef ToughBody =
            new SkillDef(SkillId.ToughBody, "skill.tough_body", "몸", "질긴 몸", LevelHp.PerLevel, LevelHp.ToughPerRank, "레벨당 체력 +{0:0}");
        public static readonly SkillDef BoilingBlood =
            new SkillDef(SkillId.BoilingBlood, "skill.boiling_blood", "몸", "끓는 피", 0.0, 10.0, "투지 얻는 양 +{0:0}%");

        /// <summary>랭크 칸(SkillId 차례).</summary>
        public static readonly IReadOnlyList<SkillDef> All = new[] { WideWhirl, SharpWind, Finisher, ToughBody, BoilingBlood };

        public const int Count = 5;

        /// <summary>트리 칸 표(창 그리는 차례 = 표 차례).</summary>
        public static readonly IReadOnlyList<SkillNodeDef> Nodes = new[]
        {
            new SkillNodeDef { Id = SkillNodeId.LearnWhirl, Key = "node.learn_whirl", Kind = SkillNodeKind.Learn, Branch = "회오리", Name = "회오리 베기",
                Desc = "E — 둘레를 세 번 벤다. 투지 40", Trainer = true, Col = 0, Row = 0.5f, Glyph = 0 },
            new SkillNodeDef { Id = SkillNodeId.WideWhirl, Key = WideWhirl.Key, Kind = SkillNodeKind.Rank, Branch = "회오리", Name = WideWhirl.Name,
                Desc = "회오리 반경이 넓어진다", Parent = SkillNodeId.LearnWhirl, RankSkill = SkillId.WideWhirl, Col = 1, Row = 0.5f, Glyph = 0 },
            new SkillNodeDef { Id = SkillNodeId.PullWhirl, Key = "node.pull_whirl", Kind = SkillNodeKind.Choice, Branch = "회오리", Name = "끌어당기는 회오리",
                Desc = "밀어내는 대신 둘레의 적을 안쪽으로 끌어당긴다", RequiredLevel = 5, Parent = SkillNodeId.WideWhirl, Exclusive = SkillNodeId.BloodWhirl,
                Trainer = true, Col = 2, Row = 0f, Glyph = 0 },
            new SkillNodeDef { Id = SkillNodeId.BloodWhirl, Key = "node.blood_whirl", Kind = SkillNodeKind.Choice, Branch = "회오리", Name = "피의 회오리",
                Desc = "회오리가 맞힐 때마다 투지 +6(한 번에 +24까지)", RequiredLevel = 5, Parent = SkillNodeId.WideWhirl, Exclusive = SkillNodeId.PullWhirl,
                Trainer = true, Col = 2, Row = 1f, Glyph = 0 },
            new SkillNodeDef { Id = SkillNodeId.LearnWave, Key = "node.learn_wave", Kind = SkillNodeKind.Learn, Branch = "검풍", Name = "검풍",
                Desc = "Q — 꿰뚫는 검기를 날린다. 투지 50", Trainer = true, Col = 0, Row = 2.5f, Glyph = 1 },
            new SkillNodeDef { Id = SkillNodeId.SharpWind, Key = SharpWind.Key, Kind = SkillNodeKind.Rank, Branch = "검풍", Name = SharpWind.Name,
                Desc = "검풍 피해가 오른다", Parent = SkillNodeId.LearnWave, RankSkill = SkillId.SharpWind, Col = 1, Row = 2.5f, Glyph = 1 },
            new SkillNodeDef { Id = SkillNodeId.ThreeWave, Key = "node.three_wave", Kind = SkillNodeKind.Choice, Branch = "검풍", Name = "세 갈래 검풍",
                Desc = "검기 셋을 부채꼴로 날린다(하나에 60%)", RequiredLevel = 5, Parent = SkillNodeId.SharpWind, Exclusive = SkillNodeId.WallBurst,
                Trainer = true, Col = 2, Row = 2f, Glyph = 1 },
            new SkillNodeDef { Id = SkillNodeId.WallBurst, Key = "node.wall_burst", Kind = SkillNodeKind.Choice, Branch = "검풍", Name = "벽 울림",
                Desc = "검풍이 벽에 닿으면 터진다(반경 1.5, 100%)", RequiredLevel = 5, Parent = SkillNodeId.SharpWind, Exclusive = SkillNodeId.ThreeWave,
                Trainer = true, Col = 2, Row = 3f, Glyph = 1 },
            new SkillNodeDef { Id = SkillNodeId.ToughBody, Key = ToughBody.Key, Kind = SkillNodeKind.Rank, Branch = "몸", Name = ToughBody.Name,
                Desc = "레벨마다 오르는 체력이 늘어난다(지난 레벨에도)", RankSkill = SkillId.ToughBody, Col = 0, Row = 4f, Glyph = 3 },
            new SkillNodeDef { Id = SkillNodeId.Finisher, Key = Finisher.Key, Kind = SkillNodeKind.Rank, Branch = "몸", Name = Finisher.Name,
                Desc = "콤보 마무리 피해가 오르고, 마무리가 맞으면 투지 +8", RankSkill = SkillId.Finisher, Col = 0, Row = 5f, Glyph = 2 },
            new SkillNodeDef { Id = SkillNodeId.BoilingBlood, Key = BoilingBlood.Key, Kind = SkillNodeKind.Rank, Branch = "몸", Name = BoilingBlood.Name,
                Desc = "투지가 더 빨리 찬다", RequiredLevel = 4, Parent = SkillNodeId.Finisher, RankSkill = SkillId.BoilingBlood, Col = 1, Row = 5f, Glyph = 5 },
        };

        public static SkillDef Get(SkillId id)
        {
            int i = (int)id;
            if (i < 0 || i >= All.Count) throw new ArgumentOutOfRangeException(nameof(id));
            return All[i];
        }

        /// <summary>문자열 id로 랭크 칸을 찾는다. 없으면 null.</summary>
        public static SkillDef Find(string key)
        {
            foreach (var d in All)
                if (d.Key == key) return d;
            return null;
        }

        public static SkillNodeDef Node(SkillNodeId id)
        {
            foreach (var n in Nodes)
                if (n.Id == id) return n;
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        /// <summary>key로 트리 칸을 찾는다. 없으면 null.</summary>
        public static SkillNodeDef FindNode(string key)
        {
            foreach (var n in Nodes)
                if (n.Key == key) return n;
            return null;
        }

        /// <summary>랭크 칸의 트리 칸.</summary>
        public static SkillNodeDef NodeOf(SkillId id)
        {
            foreach (var n in Nodes)
                if (n.RankSkill == id) return n;
            return null;
        }

        /// <summary>
        /// 옛 저장 맞추기: 랭크를 올린 기술은 배운 것으로 본다(처음부터 회오리·검풍이 있던 판). 그 밖에는 바꾸지 않는다.
        /// </summary>
        public static void Migrate(SkillState s)
        {
            if (s.Rank(SkillId.WideWhirl) > 0) s.Nodes.Add(Node(SkillNodeId.LearnWhirl).Key);
            if (s.Rank(SkillId.SharpWind) > 0) s.Nodes.Add(Node(SkillNodeId.LearnWave).Key);
        }

        /// <summary>
        /// 이 칸에 점수 1점을 쓸 수 있는가. 까닭 차례: 다 배움 → 레벨 → 먼저 칸 → 갈림 → 점수 → 무진.
        /// atTrainer = 마을에서 무진과 이야기하는 중(배우기·갈림 칸은 여기서만).
        /// </summary>
        public static SkillCheck Check(SkillState s, SkillNodeId id, bool atTrainer)
        {
            var n = Node(id);
            int rank = s.RankOf(n);
            if (rank >= n.MaxRank) return new SkillCheck(false, n.Kind == SkillNodeKind.Rank ? "최대 단계" : "배움");
            if (s.Level < n.RequiredLevel) return new SkillCheck(false, "레벨 " + n.RequiredLevel + "부터");
            if (n.Parent.HasValue)
            {
                var parent = Node(n.Parent.Value);
                if (s.RankOf(parent) < n.ParentRank) return new SkillCheck(false, "먼저: " + parent.Name);
            }
            if (n.Exclusive.HasValue && s.Has(n.Exclusive.Value)) return new SkillCheck(false, Node(n.Exclusive.Value).Name + "을(를) 골랐다");
            if (s.Points < 1) return new SkillCheck(false, "점수 1점 필요");
            if (n.Trainer && !atTrainer) return new SkillCheck(false, "마을에서 배운다", true);
            return new SkillCheck(true, "사용할 점수 1점");
        }

        /// <summary>점수 1점을 쓴다(Check가 되는 때만). 랭크 칸은 랭크 +1, 배우기·갈림 칸은 배운 칸에 넣는다.</summary>
        public static bool Take(SkillState s, SkillNodeId id, bool atTrainer)
        {
            if (!Check(s, id, atTrainer).Ok) return false;
            var n = Node(id);
            if (n.RankSkill.HasValue) s.Ranks[(int)n.RankSkill.Value]++;
            else s.Nodes.Add(n.Key);
            s.Points--;
            return true;
        }

        /// <summary>무진에게 지금 배울 수 있는 칸이 있나(점수·레벨·먼저 칸이 맞는 배우기·갈림 칸).</summary>
        public static bool AnyTrainerNode(SkillState s)
        {
            foreach (var n in Nodes)
                if (n.Trainer && Check(s, n.Id, true).Ok) return true;
            return false;
        }

        /// <summary>
        /// 시험 시작용 점수 쓰기(TestStartBuilder): 회오리·검풍을 먼저 배우고, 그다음 랭크 칸을 차례로 돌아가며 한 랭크씩 올린다. 갈림 칸은 고르지 않는다.
        /// </summary>
        public static void SpendForTest(SkillState s)
        {
            Take(s, SkillNodeId.LearnWhirl, true);
            Take(s, SkillNodeId.LearnWave, true);
            bool placed = true;
            while (s.Points > 0 && placed)
            {
                placed = false;
                foreach (var n in Nodes)
                {
                    if (n.Kind != SkillNodeKind.Rank || s.Points <= 0) continue;
                    if (Take(s, n.Id, true)) placed = true;
                }
            }
        }

        /// <summary>
        /// 랭크를 올릴 수 있는가(옛 1줄 규칙: 점수 1점 이상, 레벨 2 이상, 4랭크 미만). 트리 조건은 Check를 쓴다.
        /// </summary>
        public static bool CanRankUp(int rank, int level, int skillPoints) =>
            skillPoints > 0 && level >= SkillDef.RequiredLevel && rank < SkillDef.MaxRank;

        /// <summary>회오리 반경에 더할 값(랭크당 0.2유닛).</summary>
        public static double WhirlRadiusBonus(int rank) => WideWhirl.BonusAt(rank);

        /// <summary>검풍 배율에 더할 %p(랭크당 25).</summary>
        public static double WavePercentBonus(int rank) => SharpWind.BonusAt(rank);

        /// <summary>마무리 피해에 곱해 더할 비율(랭크당 0.06 = +6%).</summary>
        public static double FinisherDamageBonus(int rank) => Finisher.BonusAt(rank) / 100.0;

        /// <summary>레벨당 체력(랭크 0 = 120, 4 = 180).</summary>
        public static int HpPerLevel(int rank) => LevelHp.PerLevelWith(rank);

        /// <summary>레벨업 알림 한 줄. 무진에게 배울 칸이 열렸으면 그 안내를 붙인다(teachAt = SpeakerIdentity.TeachAt, '무진에게'·'경비 초소에서').</summary>
        public static string LevelUpLine(int level, int gained, bool trainerNode, string teachAt) =>
            $"몸에 힘이 차오른다 — 레벨 {level} · 스킬 점수 +{gained} [K]" + (trainerNode ? $" · 마을 {teachAt} 기술을 배울 수 있다" : "");

        /// <summary>꾸러미 값으로 무진에게 배울 칸이 있는지 본다(마을 보상 알림·안내용).</summary>
        public static bool AnyTrainerNode(int level, int points, int[] ranks, IEnumerable<string> nodes)
        {
            var s = new SkillState();
            s.Load(level, points, ranks, nodes);
            return AnyTrainerNode(s);
        }

        /// <summary>세 갈래 검풍: 갈래 사이 각도(도)와 한 갈래 피해 배율.</summary>
        public const float ThreeWaveSpread = 15f;
        public const float ThreeWaveScale = 0.6f;
        /// <summary>벽 울림: 반경과 피해 %.</summary>
        public const float WallBurstRadius = 1.5f;
        public const float WallBurstPercent = 100f;
        /// <summary>끌어당기는 회오리: 안쪽으로 당기는 거리.</summary>
        public const float PullDistance = 0.6f;
    }
}
