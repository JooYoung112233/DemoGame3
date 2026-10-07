using System;
using System.Collections.Generic;
using Demo6.Core.Loot;

namespace Demo6.Core.Town
{
    /// <summary>대화 장면 종류.</summary>
    public enum TalkSceneKind
    {
        /// <summary>강제 장면(오프닝 하나뿐).</summary>
        Opening,
        /// <summary>받기 장면.</summary>
        Offer,
        /// <summary>받기 장면 — 이미 한 경우(받기와 보고를 한 번에).</summary>
        OfferDone,
        /// <summary>받기 장면 — 그 보스에게 진 적이 있는 경우(보스 의뢰만, 받기만).</summary>
        OfferLost,
        /// <summary>보고 장면.</summary>
        Report,
        /// <summary>반응 한 줄(다음 대화 첫 줄, 한 번만).</summary>
        Reaction,
        /// <summary>매듭 장면(한 번만, 본 기록 sc:로 — 오우거 뒤 무진 마무리·춘삼 예고, 묶음 3 나-6).</summary>
        Aftermath,
        /// <summary>반복 대사 한 줄.</summary>
        Repeat,
    }

    /// <summary>
    /// 받기 장면 세 경우(시스템-컨텐츠-다듬기-검토-1차.md Q2): 처음(보통 받기) · 진 적 있음(OfferLost) · 이미 함(OfferDone).
    /// 고르기는 TalkDirector.OfferCaseFor, 장면 찾기는 TownScript.OfferScene(의뢰, 경우). 그 경우 장면이 없는 의뢰는 보통 받기 장면을 쓴다.
    /// </summary>
    public enum OfferCase
    {
        First,
        Lost,
        Done,
    }

    /// <summary>바크 때(5-7).</summary>
    public enum BarkWhen
    {
        /// <summary>평소.</summary>
        Idle,
        /// <summary>오프닝 전.</summary>
        BeforeOpening,
        /// <summary>보고할 것이 있을 때.</summary>
        HasReport,
        /// <summary>첫 귀환.</summary>
        FirstReturn,
    }

    /// <summary>선택지 하나: 단추 글과 같은 화자의 대답 한 줄(이야기는 갈라지지 않는다).</summary>
    public sealed class TalkChoice
    {
        public string Label = "";
        public string Reply = "";
    }

    /// <summary>
    /// 대사 한 마디(2차 11-3 JSON 모양: speakerId, text, revealsSpeakerIds, choices). SpeakerId가 비면 나레이션(이름표 없는 다른 모양 상자).
    /// Text의 '\n'은 줄바꿈(한 줄 32자 이하, 한 발화 2줄 64자 이하). Reveals = 이 줄을 넘기는 순간 이름을 알게 되는 주민.
    /// </summary>
    public sealed class TalkLine
    {
        public string SpeakerId;
        public string Text = "";
        public string[] Reveals = Array.Empty<string>();
        public TalkChoice[] Choices = Array.Empty<TalkChoice>();
        /// <summary>지난밤이 있어야 하는 줄("어젯밤에도 울리더라." 1-7). 첫 밤을 지나기 전(TownNight.LastNight가 None)에는 반복 대사에서 뺀다.</summary>
        public bool NeedsPastNight;

        public bool IsNarration => string.IsNullOrEmpty(SpeakerId);

        public override string ToString() => (SpeakerId ?? "나레이션") + ": " + Text;
    }

    /// <summary>장면 끝 효과(2차 11-3 onEnd → onEnd.accept / onEnd.report). TalkDirector.End가 넣는다.</summary>
    public sealed class TalkEnd
    {
        public string[] Accept = Array.Empty<string>();
        public string[] Report = Array.Empty<string>();
        /// <summary>본 장면으로 적을 id(sc:), 없으면 null.</summary>
        public string Seen;
        /// <summary>지울 반응(rx:), 없으면 null.</summary>
        public string ClearReaction;
        /// <summary>반복 대사 차례(rp:)를 올릴 주민, 없으면 null.</summary>
        public string BumpRepeat;
    }

    /// <summary>
    /// 대화 장면 하나. OwnerNpcId = 이 장면을 여는 주민(오프닝은 춘삼). SkipSummary = 건너뛰었을 때 요약 한 줄
    /// (그 시점에 모르는 이름은 쓰지 않는다. 그 장면이 공개하는 이름은 쓴다).
    /// </summary>
    public sealed class TalkScene
    {
        public string Id = "";
        public TalkSceneKind Kind;
        public string OwnerNpcId = "";
        public string QuestId;
        public bool Forced;
        public TalkLine[] Lines = Array.Empty<TalkLine>();
        public TalkEnd OnEnd = new TalkEnd();
        public string SkipSummary = "";

        /// <summary>지난밤이 있어야 하는 줄이 있는가(TalkLine.NeedsPastNight).</summary>
        public bool NeedsPastNight
        {
            get
            {
                foreach (var l in Lines) if (l.NeedsPastNight) return true;
                return false;
            }
        }

        /// <summary>마디 수(선택지 대답 한 줄을 한 마디로 더함).</summary>
        public int Utterances
        {
            get
            {
                int n = 0;
                foreach (var l in Lines) n += l.Choices.Length > 0 ? 2 : 1;
                return n;
            }
        }

        /// <summary>말하는 주민(나온 차례, 겹치지 않게). 건너뛸 때 ▼를 차례로 띄운다.</summary>
        public List<string> Speakers
        {
            get
            {
                var list = new List<string>();
                foreach (var l in Lines)
                    if (!l.IsNarration && !list.Contains(l.SpeakerId)) list.Add(l.SpeakerId);
                return list;
            }
        }

        public override string ToString() => Id;
    }

    /// <summary>물체·시설 F(5-8): 안내 글과 나레이션 모양 글. 시설 창 제목·물체 글에 주민 이름을 쓰지 않는다.</summary>
    public sealed class TownObjectText
    {
        public string Id = "";
        public string Hint = "";
        public string Text = "";
    }

    /// <summary>
    /// 첫 판 대본 전부(기획/마을-의뢰-첫판.md 5장)와 마을 고정 글(5-8·5-9·6장). 12장 결정 13 A: Core C# 표로 두되 모양은 2차 JSON과 같게.
    /// 길이 규칙(TownScriptTests): 한 줄 32자 이하, 한 발화 2줄(64자) 이하, 반복 대사·바크 24자 이하, 선택지 3개 이하·각 14자 이하,
    /// 강제 장면 8마디 이하, 그 밖의 장면 14마디 이하. 장면 첫 마디에 화자 자신의 공개를 두지 않는다.
    /// 공개 계기는 줄의 Reveals뿐이다. 글은 짧고 음울하게, 없는 시스템을 기정사실처럼 쓰지 않는다.
    /// </summary>
    public static class TownScript
    {
        public const string OpeningId = "scene.opening";

        const string G = NpcTable.Gate;
        const string S = NpcTable.Smith;
        const string T = NpcTable.Trainer;

        // ── 길이 규칙 ──
        public const int MaxLineChars = 32;
        public const int MaxUtteranceChars = 64;
        public const int MaxShortChars = 24;
        public const int MaxChoices = 3;
        public const int MaxChoiceChars = 14;
        public const int MaxForcedUtterances = 8;
        public const int MaxSceneUtterances = 14;

        // ── 바크(5-7) ──
        public const float BarkRadius = 3f;
        public const float BarkSeconds = 1.5f;
        public const float BarkCooldown = 20f;

        static TalkLine L(string speaker, string text, params string[] reveals) =>
            new TalkLine { SpeakerId = speaker, Text = text, Reveals = reveals ?? Array.Empty<string>() };

        static TalkLine N(string text) => new TalkLine { Text = text };

        /// <summary>지난밤이 있어야 나오는 줄로 표시한다(1-7: 지난밤은 TownNight.LastNight로 따로 계산).</summary>
        static TalkLine AfterNight(TalkLine line)
        {
            line.NeedsPastNight = true;
            return line;
        }

        static TalkLine C(string speaker, string text, params (string label, string reply)[] choices)
        {
            var list = new TalkChoice[choices.Length];
            for (int i = 0; i < choices.Length; i++) list[i] = new TalkChoice { Label = choices[i].label, Reply = choices[i].reply };
            return new TalkLine { SpeakerId = speaker, Text = text, Choices = list };
        }

        // ── 5-1 오프닝(강제, 7마디) ──
        public static readonly TalkScene Opening = new TalkScene
        {
            Id = OpeningId,
            Kind = TalkSceneKind.Opening,
            OwnerNpcId = G,
            Forced = true,
            Lines = new[]
            {
                N("등불골. 갱도가 버려진 지 아홉 해째다."),
                L(G, "어이, 칼 찬 양반. 갱도에 들어가려거든 저 바구니를 타."),
                L(S, "춘삼 영감, 그 사람 이쪽부터 보내요!\n칼 꼴이 말이 아니네.", G),
                L(G, "옥금이 성질 급한 건 여전하구먼.", S),
                L(G, "내려가서 숨 붙은 채로 올라와. 그게 첫 일이야."),
                L(S, "밤마다 굴쥐가 숯 자루를 뜯어.\n여덟 마리만 잡아 와."),
                L(G, "줄이 두 번 흔들리면 끌어올리마. 가 봐."),
            },
            OnEnd = new TalkEnd { Accept = new[] { QuestTable.GateDescend, QuestTable.SmithRats }, Seen = OpeningId },
            SkipSummary = "(갱도지기 춘삼, 대장장이 옥금과 인사를 나눴다. 의뢰 둘을 받았다.)",
        };

        // ── 5-2 춘삼 ──
        public static readonly TalkScene GateReportDescend = new TalkScene
        {
            Id = QuestTable.GateDescend + ".report",
            Kind = TalkSceneKind.Report,
            OwnerNpcId = G,
            QuestId = QuestTable.GateDescend,
            Lines = new[]
            {
                L(G, "올라왔구먼. 줄이 두 번 흔들리길래 알았지."),
                C(G, "아래는 어떻던가.",
                    ("쥐가 들끓소", "쥐들이 밤새 굴을 판다. 지난번 길로 가려 하지 마라."),
                    ("생각보다 깊소", "너무 깊이 파면… 아니다. 늙은이 말은 흘려들어.")),
                L(G, "품삯이야. 많진 않아."),
            },
            OnEnd = new TalkEnd { Report = new[] { QuestTable.GateDescend } },
            SkipSummary = "(첫 일을 알리고 품삯을 받았다.)",
        };

        public static readonly TalkScene GateOfferFloor2 = new TalkScene
        {
            Id = QuestTable.GateFloor2 + ".offer",
            Kind = TalkSceneKind.Offer,
            OwnerNpcId = G,
            QuestId = QuestTable.GateFloor2,
            Lines = new[]
            {
                L(G, "버팀목 길 끝에 말뚝이 하나 더 박혀 있어."),
                L(G, "거기 불을 켜고 와. 줄이 어디까지 닿나 봐야겠어."),
            },
            OnEnd = new TalkEnd { Accept = new[] { QuestTable.GateFloor2 } },
            SkipSummary = "(버팀목 길 끝 말뚝에 불을 켜는 일을 받았다.)",
        };

        /// <summary>받기 — 이미 한 경우(ms:stairs.f2가 있음): 받기와 보고를 한 번에.</summary>
        public static readonly TalkScene GateOfferFloor2Done = new TalkScene
        {
            Id = QuestTable.GateFloor2 + ".offer_done",
            Kind = TalkSceneKind.OfferDone,
            OwnerNpcId = G,
            QuestId = QuestTable.GateFloor2,
            Lines = new[]
            {
                L(G, "버팀목 길 끝 말뚝? 벌써 불 켜고 왔다고."),
                L(G, "그럼 이것도 네 몫이야."),
            },
            OnEnd = new TalkEnd { Accept = new[] { QuestTable.GateFloor2 }, Report = new[] { QuestTable.GateFloor2 } },
            SkipSummary = "(이미 켠 말뚝 몫의 품삯을 받았다.)",
        };

        public static readonly TalkScene GateReportFloor2 = new TalkScene
        {
            Id = QuestTable.GateFloor2 + ".report",
            Kind = TalkSceneKind.Report,
            OwnerNpcId = G,
            QuestId = QuestTable.GateFloor2,
            Lines = new[]
            {
                L(G, "버팀목은 아직 서 있던가."),
                L(G, "나무는 못 버텨. 돌로 쌓은 데만 남지."),
                L(G, "품삯이야. 더 아래는 줄이 아직 짧아."),
            },
            OnEnd = new TalkEnd { Report = new[] { QuestTable.GateFloor2 } },
            SkipSummary = "(버팀목 길 일을 알리고 품삯을 받았다.)",
        };

        // ── 5-3 옥금(받기 장면은 오프닝이 대신한다) ──
        public static readonly TalkScene SmithReportRats = new TalkScene
        {
            Id = QuestTable.SmithRats + ".report",
            Kind = TalkSceneKind.Report,
            OwnerNpcId = S,
            QuestId = QuestTable.SmithRats,
            Lines = new[]
            {
                L(S, "칼에 쥐 털이 엉겼네. 여덟 다 쓸었어?"),
                L(S, "쥐는 떼로 와. 한 놈씩 베면 끝이 안 나."),
                L(S, "숯값 아낀 몫이야. 받아."),
                L(S, "빛기둥 보이면 주워 와. 쇠는 언젠가 쓸모가 있어."),
            },
            OnEnd = new TalkEnd { Report = new[] { QuestTable.SmithRats } },
            SkipSummary = "(굴쥐 일을 알리고 품삯을 받았다.)",
        };

        // ── 5-4 무진 ──
        /// <summary>첫 만남·선택 장면. 이름을 묻는 선택지에도 이름을 밝히지 않는다. 요약에 이름을 쓰지 않는다.</summary>
        public static readonly TalkScene TrainerOfferWindblade = new TalkScene
        {
            Id = QuestTable.TrainerWindblade + ".offer",
            Kind = TalkSceneKind.Offer,
            OwnerNpcId = T,
            QuestId = QuestTable.TrainerWindblade,
            Lines = new[]
            {
                L(T, "허수아비는 건드리지 마. 다시 세울 손이 없어."),
                C(T, "볼일 있으면 짧게 해.",
                    ("이름이 뭐요?", "이름? 알아서 뭐 하게."),
                    ("무슨 일이오?", "버팀목 길에 궁수가 붙었어. 화살이 길지.")),
                L(T, "궁수는 멀리서 쏴. 그러니 멀리서 끊어. 검풍으로."),
                // 처음엔 스킬이 없다(기획/스킬-자원-트리-1차.md 2장): 대화가 끝나면 점수가 있을 때 배우기 창이 열린다.
                L(T, "검풍을 모르면 배워 가. 몸에 익힌 만큼만 가르쳐 주지."),
                L(T, "셋. 검풍으로 떨궈 오면 품삯은 주지."),
            },
            OnEnd = new TalkEnd { Accept = new[] { QuestTable.TrainerWindblade } },
            SkipSummary = "(경비 초소의 사내가 궁수 일을 맡겼다.)",
        };

        /// <summary>이름 공개: 둘째 마디 자기소개(12장 결정 5 A, 첫 마디 금지 규칙).</summary>
        public static readonly TalkScene TrainerReportWindblade = new TalkScene
        {
            Id = QuestTable.TrainerWindblade + ".report",
            Kind = TalkSceneKind.Report,
            OwnerNpcId = T,
            QuestId = QuestTable.TrainerWindblade,
            Lines = new[]
            {
                L(T, "셋 다 검풍에 떨어졌다고. 그럼 됐어."),
                L(T, "…무진이다. 여기 경비대장이었지.", T),
                L(T, "였다고. 지금은 쓰러진 허수아비나 지키지."),
                L(T, "가져가. 경비대 몫으로 남은 거야."),
            },
            OnEnd = new TalkEnd { Report = new[] { QuestTable.TrainerWindblade } },
            SkipSummary = "(옛 경비대장 무진이 궁수 일의 품삯을 줬다.)",
        };

        /// <summary>
        /// 오우거 굴이 있을 때만 열린다(2층 계단 아래 굴, QuestTable.OgreDenInDungeon). 궁수 의뢰 지급 뒤 무진에게 '!'.
        /// 받기 장면은 세 경우 중 하나(검토 1차 Q2, TalkDirector.OfferCaseFor): 처음 = 이 장면, 진 적 있음 = TrainerOfferOgreLost,
        /// 이미 잡음 = TrainerOfferOgreDone. 셋 다 궁수 보고(이름 공개) 뒤라 이름표는 '무진'이지만 글에는 이름을 쓰지 않는다.
        /// </summary>
        public static readonly TalkScene TrainerOfferOgre = new TalkScene
        {
            Id = QuestTable.TrainerOgre + ".offer",
            Kind = TalkSceneKind.Offer,
            OwnerNpcId = T,
            QuestId = QuestTable.TrainerOgre,
            Lines = new[]
            {
                L(T, "버팀목 길 안쪽 굴에 큰 놈이 산다."),
                L(T, "들이받을 땐 앞을 안 봐. 기둥 앞에 서 있어."),
                L(T, "눕히면 와. 못 하겠으면… 그냥 살아서 와."),
            },
            OnEnd = new TalkEnd { Accept = new[] { QuestTable.TrainerOgre } },
            SkipSummary = "(굴의 큰 놈을 눕히는 일을 받았다.)",
        };

        /// <summary>
        /// 받기 — 진 적 있음(남은 패배 반응 rx:ogre_lost 또는 굴에서 쓰러진 기록): 받기만. 패배 반응(힌트 줄)은 지우지 않고,
        /// 의뢰를 받은 뒤라 같은 대화 끝에 TalkDirector.Build가 힌트에 맞는 반응 장면을 붙인다(그 장면이 rx:ogre_lost를 씀).
        /// </summary>
        public static readonly TalkScene TrainerOfferOgreLost = new TalkScene
        {
            Id = QuestTable.TrainerOgre + ".offer_lost",
            Kind = TalkSceneKind.OfferLost,
            OwnerNpcId = T,
            QuestId = QuestTable.TrainerOgre,
            Lines = new[]
            {
                L(T, "굴 안쪽 큰 놈을 벌써 만났다고."),
                L(T, "살아서 올라왔으면 반은 한 거야."),
                L(T, "이번엔 눕히고 와. 품삯은 그때 주지."),
            },
            OnEnd = new TalkEnd { Accept = new[] { QuestTable.TrainerOgre } },
            SkipSummary = "(굴의 큰 놈을 눕히는 일을 받았다.)",
        };

        /// <summary>받기 — 이미 잡음(꾸러미 오우거 처치 수 1 이상, QuestBook.DoneBeforeAccept): 받기와 보고를 한 번에. 진 적이 있어도 이 장면이 먼저다.</summary>
        public static readonly TalkScene TrainerOfferOgreDone = new TalkScene
        {
            Id = QuestTable.TrainerOgre + ".offer_done",
            Kind = TalkSceneKind.OfferDone,
            OwnerNpcId = T,
            QuestId = QuestTable.TrainerOgre,
            Lines = new[]
            {
                L(T, "굴 안쪽 큰 놈 얘기를 하려 했는데."),
                L(T, "벌써 눕혔다고. …말이 늦었군."),
                L(T, "받아. 경비대가 진 빚이야."),
            },
            OnEnd = new TalkEnd { Accept = new[] { QuestTable.TrainerOgre }, Report = new[] { QuestTable.TrainerOgre } },
            SkipSummary = "(이미 눕힌 굴의 큰 놈 몫의 품삯을 받았다.)",
        };

        public static readonly TalkScene TrainerReportOgre = new TalkScene
        {
            Id = QuestTable.TrainerOgre + ".report",
            Kind = TalkSceneKind.Report,
            OwnerNpcId = T,
            QuestId = QuestTable.TrainerOgre,
            Lines = new[]
            {
                L(T, "그놈을 눕혔다고."),
                L(T, "아홉 해 전에 그런 칼이 있었으면… 아니다."),
                L(T, "받아. 경비대가 진 빚이야."),
            },
            OnEnd = new TalkEnd { Report = new[] { QuestTable.TrainerOgre } },
            SkipSummary = "(굴의 큰 놈 일을 알리고 품삯을 받았다.)",
        };

        // ── 묶음 3 나-6·7: 오우거 뒤 매듭(시스템-컨텐츠-다듬기-검토-1차.md) ──

        public const string OgreAfterId = "scene.ogre_after";
        public const string GateAfterOgreId = "scene.gate_after_ogre";
        /// <summary>'첫 매듭' 카드를 띄웠다(TownSave 이정표, 한 번만).</summary>
        public const string KnotMilestone = "knot.first";

        /// <summary>
        /// 오우거 보고(또는 이미 잡은 사람의 받기·보고 한 번에) 바로 뒤 무진 마무리(한 번만, TalkDirector.Build가 붙임). 끝나면 마을이 '첫 매듭' 카드를 띄운다.
        /// 다른 주민 이름을 쓰지 않는다(권양기 쪽 영감).
        /// </summary>
        public static readonly TalkScene OgreAfter = new TalkScene
        {
            Id = OgreAfterId,
            Kind = TalkSceneKind.Aftermath,
            OwnerNpcId = T,
            Lines = new[]
            {
                L(T, "굴이 조용해지면 갱도가 더 크게 운다."),
                L(T, "권양기 쪽 영감한테도 알려 둬."),
                L(T, "갱도 끝은 아직 멀어."),
            },
            OnEnd = new TalkEnd { Seen = OgreAfterId },
            SkipSummary = "(굴의 큰 놈 뒤의 일을 들었다.)",
        };

        /// <summary>오우거 지급 뒤 춘삼과 처음 말할 때 한 번: 다음 목표(줄을 더 내리기 — 권양기 고치기)를 예고한다.</summary>
        public static readonly TalkScene GateAfterOgre = new TalkScene
        {
            Id = GateAfterOgreId,
            Kind = TalkSceneKind.Aftermath,
            OwnerNpcId = G,
            Lines = new[]
            {
                L(G, "굴의 큰 놈을 눕혔다고. 들었네."),
                L(G, "줄을 더 내리려면 권양기부터 고쳐야 해."),
                L(G, "고치고 나면… 그때 다시 부르지."),
            },
            OnEnd = new TalkEnd { Seen = GateAfterOgreId },
            SkipSummary = "(줄을 더 내리려면 권양기를 고쳐야 한다고 들었다.)",
        };

        /// <summary>'첫 매듭' 카드 제목·닫기 글.</summary>
        public const string KnotTitle = "첫 매듭";
        public const string KnotClose = "[Enter] 닫기";

        /// <summary>등급 이름(일반·고급·희귀·영웅·전설).</summary>
        public static string GradeLabel(Grade g)
        {
            switch (g)
            {
                case Grade.Common: return "일반";
                case Grade.Uncommon: return "고급";
                case Grade.Rare: return "희귀";
                case Grade.Epic: return "영웅";
                default: return "전설";
            }
        }

        /// <summary>
        /// '첫 매듭' 카드 줄(묶음 3 나-6): 원정 수, 놀이 시간, 오우거에게 진 횟수, 찾은 명패·측량 장, 가장 좋은 장비 등급, 그리고 앞으로의 말 한 줄.
        /// </summary>
        public static List<string> KnotCardLines(int expeditions, double playSeconds, int ogreLosses, int nameplates, int survey, int surveyMax, Grade? bestGrade)
        {
            int minutes = (int)Math.Max(0, Math.Round(playSeconds / 60.0));
            var lines = new List<string>
            {
                $"굴의 큰 놈을 눕혔다. 원정 {Math.Max(1, expeditions)}번 · 놀이 시간 {minutes / 60}시간 {minutes % 60}분",
                ogreLosses > 0 ? $"그놈에게 진 횟수 {ogreLosses}" : "한 번도 지지 않고 눕혔다",
                $"찾은 명패 {nameplates} · 측량 {survey}/{surveyMax}",
            };
            if (bestGrade.HasValue) lines.Add("가장 좋은 장비: " + GradeLabel(bestGrade.Value));
            lines.Add("갱도는 아직 끝나지 않았다. 바구니 줄은 더 내려갈 것이다.");
            return lines;
        }

        /// <summary>의뢰를 다 끝낸 뒤 마을 목표 줄 '남은 일'(묶음 3 나-7, 없으면 null): "· 남은 일 — 측량 3/6 · 명패 1".</summary>
        public static string HudLeftoverLine(int survey, int surveyMax, int nameplates) =>
            survey >= surveyMax ? $"· 남은 일 — 명패 {nameplates} · 굴의 큰 놈 다시 눕히기" : $"· 남은 일 — 측량 {survey}/{surveyMax} · 명패 {nameplates}";

        // ── 묶음 3 나-10: 새 의뢰 받기·보고(시스템-컨텐츠-다듬기-검토-1차.md) ──

        static TalkScene QuestScene(string questId, TalkSceneKind kind, string npc, string summary, params TalkLine[] lines) => new TalkScene
        {
            Id = questId + (kind == TalkSceneKind.Report ? ".report" : kind == TalkSceneKind.OfferDone ? ".offer_done" : ".offer"),
            Kind = kind,
            OwnerNpcId = npc,
            QuestId = questId,
            Lines = lines,
            OnEnd = kind == TalkSceneKind.Report ? new TalkEnd { Report = new[] { questId } }
                : kind == TalkSceneKind.OfferDone ? new TalkEnd { Accept = new[] { questId }, Report = new[] { questId } }
                : new TalkEnd { Accept = new[] { questId } },
            SkipSummary = summary,
        };

        public static readonly TalkScene TrainerOfferSlam = QuestScene(QuestTable.TrainerSlam, TalkSceneKind.Offer, T, "(돌충이를 벽에 박는 일을 받았다.)",
            L(T, "돌충이는 들이받을 때 멈추질 못해."),
            L(T, "벽 앞에서 받아쳐. 머리가 벽에 박힌다."),
            L(T, "세 번 박아 봐. 그럼 덜 무섭지."));
        public static readonly TalkScene TrainerReportSlam = QuestScene(QuestTable.TrainerSlam, TalkSceneKind.Report, T, "(돌충이를 벽에 박은 일을 알리고 품삯을 받았다.)",
            L(T, "벽에 박았다고. 머리가 울렸겠군."),
            L(T, "받아. 경비대 훈련 값이야."));

        public static readonly TalkScene TrainerOfferPillar = QuestScene(QuestTable.TrainerPillar, TalkSceneKind.Offer, T, "(굴의 큰 놈을 기둥에 박는 일을 받았다.)",
            L(T, "그놈한테 졌다고. 살아 왔으면 됐어."),
            L(T, "마지막 돌진은 앞을 안 봐. 기둥 앞에 서."),
            L(T, "기둥에 박히면 한동안 못 일어나."));
        public static readonly TalkScene TrainerReportPillar = QuestScene(QuestTable.TrainerPillar, TalkSceneKind.Report, T, "(기둥에 박은 일을 알리고 품삯을 받았다.)",
            L(T, "기둥에 박았다고. 그놈도 별거 아니지."),
            L(T, "받아. 다음엔 눕히고 와."));

        public static readonly TalkScene GateOfferSurvey = QuestScene(QuestTable.GateSurvey, TalkSceneKind.Offer, G, "(도면을 다시 그리는 일을 받았다.)",
            L(G, "도면이 다 낡았어. 밤마다 길이 바뀌니."),
            L(G, "갱도를 걸으며 측량해 와. 석 장이면 돼."));
        public static readonly TalkScene GateReportSurvey = QuestScene(QuestTable.GateSurvey, TalkSceneKind.Report, G, "(측량 일을 알리고 품삯을 받았다.)",
            L(G, "석 장이구먼. 줄 내릴 궁리를 해 보지."),
            L(G, "품삯이야. 도면은 내가 맡아 두마."));

        public static readonly TalkScene SmithOfferOre = QuestScene(QuestTable.SmithOre, TalkSceneKind.Offer, S, "(광맥을 캐는 일을 받았다.)",
            L(S, "쇠 냄새 나는 데가 있어. 광맥이야."),
            L(S, "곡괭이로 두 군데만 캐 와. 모루가 좋아해."));
        public static readonly TalkScene SmithReportOre = QuestScene(QuestTable.SmithOre, TalkSceneKind.Report, S, "(광맥 일을 알리고 품삯을 받았다.)",
            L(S, "광맥 냄새가 뱄네. 좋아."),
            L(S, "돌 값은 쳐 줄게. 받아."));

        public static readonly TalkScene SmithOfferSafe = QuestScene(QuestTable.SmithSafe, TalkSceneKind.Offer, S, "(광업소 금고를 여는 일을 받았다.)",
            L(S, "광업소 사무실 열쇠를 주웠다며."),
            L(S, "금고에 옛 품삯이 남았을 거야. 열어 봐."));
        public static readonly TalkScene SmithOfferSafeDone = QuestScene(QuestTable.SmithSafe, TalkSceneKind.OfferDone, S, "(이미 연 금고 몫의 수고비를 받았다.)",
            L(S, "금고를 벌써 열었다고?"),
            L(S, "빠르네. 수고비는 따로 줄게."));
        public static readonly TalkScene SmithReportSafe = QuestScene(QuestTable.SmithSafe, TalkSceneKind.Report, S, "(금고 일을 알리고 수고비를 받았다.)",
            L(S, "금고가 열렸어? 별게 다 남았네."),
            L(S, "안의 건 네 몫. 수고비는 따로야."));

        static readonly TalkScene[] QuestScenes =
        {
            Opening, GateReportDescend, GateOfferFloor2, GateOfferFloor2Done, GateReportFloor2, SmithReportRats,
            TrainerOfferWindblade, TrainerReportWindblade, TrainerOfferOgre, TrainerOfferOgreLost, TrainerOfferOgreDone, TrainerReportOgre,
            TrainerOfferSlam, TrainerReportSlam, TrainerOfferPillar, TrainerReportPillar, GateOfferSurvey, GateReportSurvey,
            SmithOfferOre, SmithReportOre, SmithOfferSafe, SmithOfferSafeDone, SmithReportSafe,
        };

        // ── 5-5 반응 한 줄(다음 대화 첫 줄, 한 번만) ──
        static TalkScene Reaction(string reaction, string npc, params string[] texts) => ReactionAs("rx." + reaction, reaction, npc, texts);

        /// <summary>같은 반응을 지우는 장면이 여럿일 때(힌트마다 다른 줄) 장면 id를 따로 준다.</summary>
        static TalkScene ReactionAs(string id, string reaction, string npc, params string[] texts)
        {
            var lines = new TalkLine[texts.Length];
            for (int i = 0; i < texts.Length; i++) lines[i] = L(npc, texts[i]);
            return new TalkScene
            {
                Id = id,
                Kind = TalkSceneKind.Reaction,
                OwnerNpcId = npc,
                Lines = lines,
                OnEnd = new TalkEnd { ClearReaction = reaction },
            };
        }

        const string ChargeHintLine = "들이받을 땐 앞을 안 봐. 마지막 걸 기둥에 박아.";
        const string SlamHintLine = "내려찍을 땐 걸어선 못 빠져. 굴러.";

        /// <summary>
        /// 오우거 패배 반응(rx:ogre_lost, 전투 문서 3-7 '그 판에 가장 많이 맞은 패턴을 가리킴'): 힌트(TownSave.OgreLossHint)마다 장면 하나.
        /// 돌진·내려찍기는 그 한 줄, 힌트 없음은 두 줄 그대로. 셋 다 rx:ogre_lost를 지운다(지울 때 힌트도 함께, TownSave.ClearReaction).
        /// '굴의 큰 놈'을 받기 전(잠김·받을 수 있음)에는 꺼내지 않고 아껴 둔다(TalkDirector.OgreLossReady, 검토 1차 Q2): 무진 첫 만남은 첫 만남 장면부터,
        /// 의뢰를 받는 대화에서는 받기 장면 뒤 끝에 붙는다.
        /// </summary>
        public static readonly TalkScene OgreLost = Reaction(TownSave.RxOgreLost, T, ChargeHintLine, SlamHintLine);
        public static readonly TalkScene OgreLostCharge = ReactionAs("rx." + TownSave.RxOgreLost + ".charge", TownSave.RxOgreLost, T, ChargeHintLine);
        public static readonly TalkScene OgreLostSlam = ReactionAs("rx." + TownSave.RxOgreLost + ".slam", TownSave.RxOgreLost, T, SlamHintLine);

        /// <summary>
        /// 반응 장면(차례 = 고르는 차례: 쓰러짐, 오우거 패배, 명패). 한 대화에 하나만 쓴다.
        /// 오우거 패배는 힌트에 맞는 하나를 TalkDirector.PendingReaction이 고른다(OgreLostScene). 반응 이름으로 찾으면(ReactionScene) 두 줄 장면이 먼저다.
        /// </summary>
        public static readonly TalkScene[] Reactions =
        {
            Reaction(TownSave.RxDowned, G, "쓰러졌다 일어난 얼굴이구먼. 살았으면 됐어."),
            OgreLost,
            OgreLostCharge,
            OgreLostSlam,
            Reaction(TownSave.RxTagF1, G, "명패구먼. …등불 하나 내리마."),
            Reaction(TownSave.RxTag2, G, "또 하나구먼. 그 집 창이 오늘 어둡겠어."),
            Reaction(TownSave.RxTag3, G, "셋째야. 기다리는 등불이 줄어드는구먼."),
        };

        /// <summary>오우거 패배 반응 장면(힌트 없음·모르는 값이면 두 줄 장면).</summary>
        public static TalkScene OgreLostScene(OgreLossHint hint)
        {
            switch (hint)
            {
                case OgreLossHint.Charge: return OgreLostCharge;
                case OgreLossHint.Slam: return OgreLostSlam;
                default: return OgreLost;
            }
        }

        // ── 5-6 반복 대사(24자 이하, 단계는 의뢰 상태로 계산, rp:{npc} 차례로 돌림) ──
        static TalkScene[] Repeats(string npc, int stage, params TalkLine[] lines)
        {
            var scenes = new TalkScene[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                scenes[i] = new TalkScene
                {
                    Id = "rp." + npc + "." + stage + "." + i,
                    Kind = TalkSceneKind.Repeat,
                    OwnerNpcId = npc,
                    Lines = new[] { lines[i] },
                    OnEnd = new TalkEnd { BumpRepeat = npc },
                };
            return scenes;
        }

        /// <summary>
        /// 반복 대사 단계: 춘삼 1 = 오프닝 뒤, 2 = q.gate_floor2 지급 뒤 / 옥금 1 = 오프닝 뒤, 2 = q.smith_rats 지급 뒤 / 무진 1 = 이름 공개 전, 2 = 공개 뒤.
        /// 옥금 2단계 "춘삼 영감 말은 반만 들어."에 npc.gate 공개를 달아 둔다(다른 길로 이 줄을 먼저 들어도 규칙을 지킴).
        /// 춘삼 1단계 "어젯밤에도 울리더라."는 지난밤이 있을 때만 돈다(첫 울림은 둘째 방문 뒤 밤 카드라 원정 2까지는 빠짐, TalkDirector.RepeatScene).
        /// </summary>
        static readonly Dictionary<string, TalkScene[][]> RepeatTable = new Dictionary<string, TalkScene[][]>
        {
            [G] = new[]
            {
                Repeats(G, 1, L(G, "바구니는 승강장까지만 닿아."), L(G, "등불 하나에 사람 하나야."), AfterNight(L(G, "어젯밤에도 울리더라."))),
                Repeats(G, 2, L(G, "갈래 말뚝은 밤이면 묻혀."), L(G, "나무 버팀목은 못 버텨."), L(G, "늙은이 말은 흘려들어.")),
                Repeats(G, 3, L(G, "굴이 조용하니 잠이 오더라."), L(G, "줄은 2층에서 끝나. 아직은."), L(G, "권양기 톱니가 닳았어.")),
            },
            [S] = new[]
            {
                Repeats(S, 1, L(S, "모루는 식어도 쇠는 기억해."), L(S, "칼날 이 빠진 거 다 보여."), L(S, "쥐는 떼로 와. 돌려 베.")),
                Repeats(S, 2, L(S, "모루 데웠어. +7까진 돌만 잃어."), L(S, "춘삼 영감 말은 반만 들어.", G), L(S, "초소 그 사람은 상대 마.")),
                Repeats(S, 3, L(S, "그 큰 놈 이빨, 쇠 같더라."), L(S, "칼은 더 무거워져도 돼."), L(S, "더 깊이 가면 쇠가 달라.")),
            },
            [T] = new[]
            {
                Repeats(T, 1, L(T, "볼일 없으면 지나가."), L(T, "초소엔 볼 것 없어."), L(T, "멀리 있는 놈은 멀리서 끊어.")),
                Repeats(T, 2, L(T, "경비대는 없어. 나만 남았지."), L(T, "궁수는 검풍, 쥐는 회오리."), L(T, "주막 술은 맹물이야.")),
                Repeats(T, 3, L(T, "그놈 뒤로 굴이 조용해."), L(T, "기둥 앞에 서는 법, 잊지 마."), L(T, "아래엔 더 큰 게 있어.")),
            },
        };

        // ── 5-7 바크(반경 3, 머리 위 1.5초, 같은 주민은 20초에 1번, 이름표 없음, 24자 이하) ──
        static readonly (string npc, BarkWhen when, string text)[] BarkTable =
        {
            (G, BarkWhen.BeforeOpening, "어이, 이쪽!"),
            (G, BarkWhen.HasReport, "올라왔으면 말을 해."),
            (G, BarkWhen.Idle, "바구니는 내려가 있어."),
            (S, BarkWhen.FirstReturn, "살아 왔네. 칼 꼴 좀 봐."),
            (S, BarkWhen.Idle, "쇠 식는다, 쇠 식어."),
            (T, BarkWhen.Idle, "볼일 없으면 지나가."),
        };

        // ── 5-8 물체·시설 글 ──
        public const string ObjBoard = "obj.board";
        public const string ObjShutter = "obj.shutter";
        public const string ObjAnvil = "obj.anvil";
        public const string ObjCart = "obj.cart";
        public const string ObjGate = "obj.gate";

        static readonly TownObjectText[] ObjectTable =
        {
            new TownObjectText { Id = ObjBoard, Hint = "F 의뢰 목록", Text = "'갱도에 들어갈 검사를 구함. 권양기로.' 누가 대신 써 준 글씨다." },
            new TownObjectText { Id = ObjShutter, Hint = "F 살펴보기", Text = "덧문 틈으로 불빛만 샌다." },
            new TownObjectText { Id = ObjAnvil, Hint = "F 모루", Text = ForgeText.WindowTitle },
            new TownObjectText { Id = ObjCart, Hint = "F 살펴보기", Text = "수레길. 지금은 갱도 말고 갈 곳이 없다." },
            new TownObjectText { Id = ObjGate, Hint = "F 갱도 입구", Text = GateWindowTitle },
        };

        /// <summary>던전 말뚝 메뉴 '바구니로 올라가기' 글(선택).</summary>
        public const string StakeAscendLabel = "바구니로 올라가기 — 등불골로 돌아간다.";

        // ── 6장 HUD·창 고정 글 ──
        public const string HudActivePrefix = "· ";
        public const string HudReportPrefix = "… ";
        public const string HudBeforeOpening = "· 갱도 마당으로";
        public const string HudNoQuest = "· 권양기 바구니로 내려가기";
        public const string TownTitle = "등불골 · 갱도 마당";
        /// <summary>마을 안내 줄. F1 시험 패널은 적지 않는다(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q7: 누구나 눌러 판정 기록이 흐려지지 않게).</summary>
        public const string ControlsLine = "I 가방 · K 스킬 · J 의뢰";
        public const string GateWindowTitle = "갱도 입구 — 어디서 내려갈까";
        public const string SkipPrompt = "이 장면 건너뛰기? [Enter] 예 / [Esc] 아니오";
        public const string ArrivalTitle = "등불골로 돌아왔다";
        public const string ArrivalClose = "[마을로 (Enter)]";

        // ── 읽기 ──

        /// <summary>의뢰 장면·반응 장면 전부(반복 대사 제외).</summary>
        public static IEnumerable<TalkScene> StoryScenes
        {
            get
            {
                foreach (var s in QuestScenes) yield return s;
                foreach (var s in Reactions) yield return s;
                yield return OgreAfter;
                yield return GateAfterOgre;
            }
        }

        /// <summary>반복 대사 장면 전부.</summary>
        public static IEnumerable<TalkScene> RepeatScenes
        {
            get
            {
                foreach (var kv in RepeatTable)
                    foreach (var stage in kv.Value)
                        foreach (var s in stage)
                            yield return s;
            }
        }

        /// <summary>모든 장면(의뢰·반응·반복).</summary>
        public static IEnumerable<TalkScene> AllScenes
        {
            get
            {
                foreach (var s in StoryScenes) yield return s;
                foreach (var s in RepeatScenes) yield return s;
            }
        }

        /// <summary>이 id의 장면(없으면 null).</summary>
        public static TalkScene Scene(string id)
        {
            foreach (var s in AllScenes)
                if (string.Equals(s.Id, id, StringComparison.Ordinal)) return s;
            return null;
        }

        /// <summary>
        /// 이 의뢰의 받기 장면. alreadyDone = 받기 전 기록이 있음('이미 한 경우' 장면, 없으면 보통 받기 장면).
        /// 오프닝 의뢰는 오프닝이 받기 장면이다.
        /// </summary>
        public static TalkScene OfferScene(string questId, bool alreadyDone = false) =>
            OfferScene(questId, alreadyDone ? OfferCase.Done : OfferCase.First);

        /// <summary>
        /// 이 의뢰의 그 경우 받기 장면(검토 1차 Q2): Done = OfferDone, Lost = OfferLost, First = 보통 받기(Offer).
        /// 그 경우 장면이 없는 의뢰는 보통 받기 장면, 오프닝 의뢰는 오프닝이다.
        /// </summary>
        public static TalkScene OfferScene(string questId, OfferCase which)
        {
            if (Array.IndexOf(QuestTable.OpeningQuests, questId) >= 0) return Opening;
            TalkScene plain = null;
            foreach (var s in QuestScenes)
            {
                if (!string.Equals(s.QuestId, questId, StringComparison.Ordinal)) continue;
                if (s.Kind == TalkSceneKind.OfferDone && which == OfferCase.Done) return s;
                if (s.Kind == TalkSceneKind.OfferLost && which == OfferCase.Lost) return s;
                if (s.Kind == TalkSceneKind.Offer) plain = s;
            }
            return plain;
        }

        /// <summary>이 의뢰의 보고 장면(없으면 null).</summary>
        public static TalkScene ReportScene(string questId)
        {
            foreach (var s in QuestScenes)
                if (s.Kind == TalkSceneKind.Report && string.Equals(s.QuestId, questId, StringComparison.Ordinal)) return s;
            return null;
        }

        /// <summary>반응 장면(없으면 null).</summary>
        public static TalkScene ReactionScene(string reaction)
        {
            foreach (var s in Reactions)
                if (s.OnEnd.ClearReaction == reaction) return s;
            return null;
        }

        /// <summary>반복 대사 장면들(단계 1·2, 없으면 빈 배열).</summary>
        public static TalkScene[] RepeatSet(string npcId, int stage)
        {
            if (npcId == null || !RepeatTable.TryGetValue(npcId, out var stages)) return Array.Empty<TalkScene>();
            int i = Math.Max(1, Math.Min(stages.Length, stage)) - 1;
            return stages[i];
        }

        /// <summary>바크 글(그 주민에게 그 때의 바크가 없으면 평소 바크, 그것도 없으면 null).</summary>
        public static string Bark(string npcId, BarkWhen when)
        {
            string idle = null;
            foreach (var b in BarkTable)
            {
                if (b.npc != npcId) continue;
                if (b.when == when) return b.text;
                if (b.when == BarkWhen.Idle) idle = b.text;
            }
            return idle;
        }

        /// <summary>바크 표 전부(시험).</summary>
        public static IEnumerable<string> AllBarks
        {
            get
            {
                foreach (var b in BarkTable) yield return b.text;
            }
        }

        /// <summary>물체 글(없으면 null).</summary>
        public static TownObjectText Object(string id)
        {
            foreach (var o in ObjectTable)
                if (o.Id == id) return o;
            return null;
        }

        public static IReadOnlyList<TownObjectText> Objects => ObjectTable;

        // ── 5-9 카드·알림 글 ──

        /// <summary>목적격 조사(숫자를 읽은 끝소리: 2·4·5·9로 끝나면 '를', 그 밖은 '을').</summary>
        public static string ObjectParticle(int n)
        {
            int last = Math.Abs(n) % 10;
            return last == 2 || last == 4 || last == 5 || last == 9 ? "를" : "을";
        }

        /// <summary>보상 줄(대화창): "강화석 3 · 골드 30 · 경험치 40을 받았다."</summary>
        public static string RewardLine(QuestReward r) => r.AmountText + ObjectParticle(r.Xp) + " 받았다.";

        /// <summary>마을 보상 알림: "품삯 — 강화석 3 · 골드 30 · 경험치 40"</summary>
        public static string RewardNotice(QuestReward r) => "품삯 — " + r.AmountText;

        /// <summary>마을 레벨업 알림: "몸에 힘이 차오른다 — 레벨 2 · 스킬 점수 +1 [K]"(던전 레벨업 알림과 같은 표기, 마을에서도 K가 열림). 레벨이 오르지 않았으면 null.</summary>
        public static string LevelUpNotice(QuestReward r) =>
            r.LeveledUp ? $"몸에 힘이 차오른다 — 레벨 {r.LevelAfter} · 스킬 점수 +{r.SkillPointsGained} [K]" : null;

        /// <summary>던전 완료 알림: "의뢰 끝 — {제목}. 올라가면 {알릴 곳} 알리자." 알릴 곳은 SpeakerIdentity.ReportTarget.</summary>
        public static string QuestDoneNotice(string title, string reportTarget) => $"의뢰 끝 — {title}. 올라가면 {reportTarget} 알리자.";

        /// <summary>
        /// 도착 카드 명패 줄(주민 이름 없음): "광부 명패를 권양기 틀에 걸었다. 등불 하나가 꺼졌다. (남은 등불 11)". 새로 맡긴 것이 없으면 null.
        /// </summary>
        public static string NameplateLine(int newTags, int litLeft)
        {
            if (newTags <= 0) return null;
            if (newTags == 1) return $"광부 명패를 권양기 틀에 걸었다. 등불 하나가 꺼졌다. (남은 등불 {litLeft})";
            return $"광부 명패 {newTags}개를 권양기 틀에 걸었다. 등불 {newTags}개가 꺼졌다. (남은 등불 {litLeft})";
        }

        /// <summary>돌아오지 못한 사람(마을 등불 수).</summary>
        public const int MissingMiners = 12;

        /// <summary>도착 카드 명패 덧줄(묶음 3 나-8): "돌아오지 못한 사람 12 — 소식 n".</summary>
        public static string MissingLine(int tags) => $"돌아오지 못한 사람 {MissingMiners} — 소식 {Math.Max(0, tags)}";

        /// <summary>명패를 주울 때 나레이션(이야기 문서 3장 글귀): "명패에 '갑돌'이라 새겨져 있다." 이름을 모르면 null.</summary>
        public static string NameplateInscription(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            char last = name[name.Length - 1];
            bool batchim = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
            return $"명패에 '{name}'{(batchim ? "이라" : "라")} 새겨져 있다.";
        }

        /// <summary>권양기 창 덧줄: "아직 알리지 않은 일 n — 보상은 다음에 와서 받아도 된다."(없으면 null).</summary>
        public static string GateUnreportedLine(int unreported) =>
            unreported > 0 ? $"아직 알리지 않은 일 {unreported} — 보상은 다음에 와서 받아도 된다." : null;

        /// <summary>
        /// 새로 열린 의뢰 한 줄(검토 1차 묶음 3 가-1): "새 일이 생겼다 — ? (경비 초소) · 옥금". 주는 사람만 적고 이름 공개 규칙(SpeakerIdentity.GiverLabel)을 거친다.
        /// 도착 카드·보고 장면 끝 결과 줄·마을 알림이 함께 쓴다. 없으면 null.
        /// </summary>
        public static string NewQuestLine(IReadOnlyList<string> giverLabels) =>
            giverLabels == null || giverLabels.Count == 0 ? null : "새 일이 생겼다 — " + string.Join(" · ", giverLabels);

        /// <summary>마을 목표 줄 끝: "! 받을 일 n"(받을 수 있는데 아직 받지 않은 의뢰, 없으면 null).</summary>
        public static string HudOfferLine(int offered) => offered > 0 ? $"! 받을 일 {offered}" : null;

        /// <summary>권양기 창 덧줄: "받지 않은 일 n — 받지 않고 떠나도 된다."(없으면 null).</summary>
        public static string GateOfferLine(int offered) =>
            offered > 0 ? $"받지 않은 일 {offered} — 받지 않고 떠나도 된다." : null;

        /// <summary>마을 지갑 줄(TownHud): "강화석 n · 골드 n".</summary>
        public static string WalletLine(int stones, int gold) => $"강화석 {stones} · 골드 {gold}";

        /// <summary>도착 카드 지갑 줄: "지갑 강화석 n · 골드 n".</summary>
        public static string ArrivalWalletLine(int stones, int gold) => "지갑 " + WalletLine(stones, gold);

        /// <summary>시험이 이름을 찾는 고정 글 전부(제목·HUD 틀·바크·물체 글·카드·알림 틀).</summary>
        public static IEnumerable<string> FixedTexts
        {
            get
            {
                foreach (var q in QuestTable.All)
                {
                    yield return q.Title;
                    foreach (var g in q.Steps)
                    {
                        yield return g.Hud;
                        if (g.Notice != null) yield return g.Notice;
                    }
                }
                foreach (var b in AllBarks) yield return b;
                foreach (var o in ObjectTable)
                {
                    yield return o.Hint;
                    yield return o.Text;
                }
                yield return StakeAscendLabel;
                yield return HudActivePrefix;
                yield return HudReportPrefix;
                yield return HudBeforeOpening;
                yield return HudNoQuest;
                yield return TownTitle;
                yield return ControlsLine;
                yield return GateWindowTitle;
                yield return SkipPrompt;
                yield return ArrivalTitle;
                yield return ArrivalClose;
                yield return NameplateLine(1, 11);
                yield return NameplateLine(2, 10);
                yield return GateUnreportedLine(2);
                yield return MissingLine(2);
                yield return NewQuestLine(new[] { "? (경비 초소)", "? (대장간)" });
                yield return HudOfferLine(2);
                yield return GateOfferLine(2);
                var r = new QuestReward(3, 30, 40, 0, 1, 2, 1);
                yield return RewardLine(r);
                yield return RewardNotice(r);
                yield return LevelUpNotice(r);
                yield return ArrivalWalletLine(3, 30);
                yield return TownNight.AscendLine;
                yield return TownNight.DescendLine;
                yield return TownNight.FirstNightLine;
                yield return TownNight.FirstNightDawnLine;
                yield return TownNight.NightLine;
                foreach (var e in TownNight.EventLines) yield return e;
            }
        }
    }
}
