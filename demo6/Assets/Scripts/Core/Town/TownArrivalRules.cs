using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Town
{
    /// <summary>마을에 어떻게 왔나(도착 쪽지 TownTravel의 도착 종류를 Core로 옮긴 것).</summary>
    public enum TownArrivalKind
    {
        /// <summary>새 플레이(꾸러미·도착 쪽지 없음, 남쪽 길 끝에서 시작).</summary>
        NewPlay,
        /// <summary>바구니로 올라옴(귀환 지점).</summary>
        Basket,
        /// <summary>쓰러진 채 실려 올라옴(자리만 둠, 1-6 — 첫 판은 쓰지 않는다).</summary>
        Downed,
    }

    /// <summary>마을 도착 처리 결과(도착 카드·바크가 본다).</summary>
    public sealed class TownArrivalResult
    {
        public TownArrivalKind Kind;
        /// <summary>같은 도착을 이미 처리함(방문 수를 다시 세지 않았다).</summary>
        public bool Repeated;
        /// <summary>처리 뒤 마을 도착 수(첫 도착 포함).</summary>
        public int Visits;
        /// <summary>첫 도착(새 플레이 또는 던전에서 바로 올라온 첫 방문).</summary>
        public bool FirstVisit;
        /// <summary>바구니로 처음 돌아옴(원정 1이 끝남 — 옥금 바크 "살아 왔네. 칼 꼴 좀 봐.").</summary>
        public bool FirstReturn;
        /// <summary>오프닝을 아직 못 봄(DungeonTest를 바로 Play해서 올라온 시험 경로면 도착 카드를 닫은 뒤 오프닝을 튼다, 1-6).</summary>
        public bool OpeningPending;
        /// <summary>이번에 새로 맡긴 명패, 맡긴 명패 합, 켜진 등불 수.</summary>
        public int NewTags;
        public int Tags;
        public int LitLamps;
        /// <summary>이번 처리에서 처음으로 명패를 맡겼다(rx:tag.f1을 적음).</summary>
        public bool FirstTag;
        /// <summary>이번 처리에서 새로 '받을 수 있음'이 된 의뢰.</summary>
        public readonly List<string> Opened = new List<string>();
    }

    /// <summary>
    /// 마을 도착 처리(기획/마을-의뢰-첫판.md 1-4 단계 4). 한 번, 순서 고정, 두 번 불러도 결과가 같다.
    /// ① 정산: 바구니 귀환이라 손실 없음(쓰러져 실려 오는 경우는 자리만). ② 재화: 이미 꾸러미에 있다.
    /// ③ 명패: 꾸러미에 받은 명패 수(OnceDone의 명패 id) − town.tags만큼 새로 맡긴다(등불이 꺼짐). 처음 맡기면 rx:tag.f1.
    /// ④ 의뢰: QuestBook.Refresh. 달성은 던전에서 이미 적혔고, <b>지급은 하지 않는다</b>(돌아와 보고).
    /// ⑤ 주민 단계·머리 위 표시는 의뢰 상태에서 계산한다(저장하지 않음). ⑥ town.visits +1(같은 도착이면 다시 세지 않음).
    /// 원정 번호와 밤 사건은 바꾸지 않는다(올라갈 때 던전 한 곳에서만 정함, TownNight.AdvanceForAscend).
    /// 같은 도착인지는 원정 번호로 안다(도착마다 원정 번호가 다르다: 새 플레이 1, 바구니로 올라올 때마다 +1).
    /// </summary>
    public static class TownArrivalRules
    {
        public static TownArrivalResult Apply(CarryData carry, TownArrivalKind kind, QuestContext ctx = default)
        {
            var result = new TownArrivalResult { Kind = kind };
            if (carry == null) return result;

            // ① 정산: 첫 판은 손실 없음. Downed 손실은 쓰러지면 마을로 실려 오는 규칙이 들어올 때 여기 넣는다.
            // ② 재화: 이미 꾸러미에 있다.

            // ③ 명패
            int before = TownSave.Tags(carry);
            int have = NameplatesHeld(carry);
            int add = Math.Max(0, have - before);
            if (add > 0)
            {
                TownSave.SetTags(carry, before + add);
                if (before == 0)
                {
                    TownSave.SetReaction(carry, TownSave.RxTagF1);
                    result.FirstTag = true;
                }
                // 둘째·셋째 명패에도 춘삼이 한 줄 반응한다(묶음 3 나-8). 한 번에 여럿이면 가장 큰 차례 하나.
                int after = before + add;
                if (before < 3 && after >= 3) TownSave.SetReaction(carry, TownSave.RxTag3);
                else if (before < 2 && after >= 2) TownSave.SetReaction(carry, TownSave.RxTag2);
            }
            result.NewTags = add;
            result.Tags = TownSave.Tags(carry);
            result.LitLamps = TownLayout.LitLamps(result.Tags);

            // ④ 의뢰(지급 없음)
            result.Opened.AddRange(new QuestBook(carry).Refresh(ctx));

            // ⑥ 방문 수
            int stamp = Math.Max(1, carry.Expedition);
            if (TownSave.LastArrival(carry) == stamp)
                result.Repeated = true;
            else
            {
                TownSave.SetLastArrival(carry, stamp);
                TownSave.BumpVisits(carry);
            }
            result.Visits = TownSave.Visits(carry);
            result.FirstVisit = result.Visits == 1;
            result.FirstReturn = kind != TownArrivalKind.NewPlay && carry.Expedition == 2;
            result.OpeningPending = !TownSave.SeenScene(carry, TownScript.OpeningId);
            return result;
        }

        /// <summary>꾸러미가 받은 명패 수(OnceDone 가운데 FloorRecipe의 명패 물건).</summary>
        public static int NameplatesHeld(CarryData carry)
        {
            if (carry == null) return 0;
            int n = 0;
            foreach (var id in carry.OnceDone)
            {
                var item = FloorRecipe.FindOnceItem(id);
                if (item != null && item.Kind == FeatureKind.Nameplate) n++;
            }
            return n;
        }

        /// <summary>
        /// 도착 카드(6-4)의 Core 몫 줄: 지갑, 명패(새로 맡겼을 때만), 알릴 일, 진행 중. 제목(TownScript.ArrivalTitle)과
        /// 원정 요약 두 줄(ExpeditionSummary.Line, 이번 원정 강화석·골드·가장 깊이)은 Game이 이 앞에 놓는다.
        /// </summary>
        public static List<string> CardLines(CarryData carry, TownArrivalResult result)
        {
            var lines = new List<string>();
            if (carry == null) return lines;
            lines.Add(TownScript.ArrivalWalletLine(carry.Stones, carry.Gold));
            if (result != null)
            {
                string tag = TownScript.NameplateLine(result.NewTags, result.LitLamps);
                if (tag != null)
                {
                    lines.Add(tag);
                    lines.Add(TownScript.MissingLine(result.Tags));
                }
            }
            var book = new QuestBook(carry);
            string report = book.ArrivalReportLine();
            if (report != null) lines.Add(report);
            string active = book.ArrivalActiveLine();
            if (active != null) lines.Add(active);
            // 이번 도착에서 새로 열린 의뢰(묶음 3 가-1): 주는 사람만, 이름 공개 규칙 그대로.
            string fresh = result != null ? book.NewQuestLine(result.Opened) : null;
            if (fresh != null) lines.Add(fresh);
            return lines;
        }
    }
}
