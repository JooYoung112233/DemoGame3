using System;
using System.Collections.Generic;
using System.Globalization;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;

namespace Demo6.Core.Town
{
    /// <summary>
    /// '재화 쓸 곳 1차' 화면 글 한 곳(2-1·2-6·2-7·3장·4-1~4-5·5-1~5-4·6-1·6-2). 글은 모두 초안이고 바꿀 때는 여기만 고친다.
    /// 모루 창(ForgeWindow)·가방(Inventory)·HUD·권양기 창·떠날 때 거두기(BagSweep.ArrivalLine)가 여기서 글을 가져간다.
    /// 주민 이름은 쓰지 않는다(창은 모루이지 사람이 아니다, 화자 이름 공개 규칙). 조사(을/를·이/가·은/는)는 받침에 맞춰 여기서 고른다.
    /// 시험: ForgeShopTests(AllTexts에 주민 이름 없음).
    /// </summary>
    public static class ForgeText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ── 2-6 창 틀 ──
        public const string WindowTitle = "모루 — 무엇을 두드릴까";
        public const string TabEnhance = "강화";
        public const string TabSalvage = "분해";
        public const string TabGoods = "대장간 물건";
        /// <summary>칸 이름(창 맨 위 차례: 강화 · 분해 · 대장간 물건).</summary>
        public static IReadOnlyList<string> Tabs { get; } = new[] { TabEnhance, TabSalvage, TabGoods };
        public const string TabsLine = "강화 · 분해 · 대장간 물건";
        /// <summary>창 아래 안내.</summary>
        public const string FooterHint = "[Tab] 칸 바꾸기 · [Enter] 두드리기 · [Esc] 닫기";
        /// <summary>분해 칸 아래 안내(Enter가 고른 장비를 녹인다).</summary>
        public const string FooterHintSalvage = "[Tab] 칸 바꾸기 · [Enter] 분해 · [Esc] 닫기";
        /// <summary>대장간 물건 칸 아래 안내(Enter가 고른 물건을 산다).</summary>
        public const string FooterHintGoods = "[Tab] 칸 바꾸기 · [Enter] 사기 · [Esc] 닫기";
        /// <summary>칸 차례(강화 0 · 분해 1 · 대장간 물건 2)에 맞는 창 아래 안내.</summary>
        public static string FooterHintFor(int tab) => tab == 1 ? FooterHintSalvage : tab == 2 ? FooterHintGoods : FooterHint;

        // ── 2-1 강화 칸 목록 ──
        /// <summary>낀 장비 줄에 회색으로 붙이는 글.</summary>
        public const string EquippedTag = "낀 것";
        /// <summary>가방 장비 줄에 회색으로 붙이는 글.</summary>
        public const string BagTag = "가방";

        /// <summary>목록 한 줄: "무기 · 희귀 장검 +3 (+3/7)". slotName이 없으면 부위 이름(반지 자리는 '반지 1'을 넘겨도 됨). 없으면 빈 글.</summary>
        public static string EnhanceRow(GearItem item, string slotName = null)
        {
            if (item == null) return "";
            string place = string.IsNullOrEmpty(slotName) ? GearSlots.Name(item.Part) : slotName;
            return $"{place} · {item.DisplayName} +{item.Enhance} (+{item.Enhance}/{EnhanceRules.MaxTarget(item.Grade)})";
        }

        // ── 2-6 견적 ──
        /// <summary>단계 줄: "+3 → +4".</summary>
        public static string StepLine(int from, int to) => $"+{from} → +{to}";

        /// <summary>
        /// 능력치 줄(지금 → 다음): 무기·반지 "공격력 143 → 150", 방어구 "방어 75 → 78 · 체력 250 → 260", 목걸이 "체력 300 → 312".
        /// 어느 한쪽이라도 0이 아닌 능력치만 적는다. 둘 중 하나가 없으면 빈 글.
        /// </summary>
        public static string StatLine(GearItem now, GearItem next)
        {
            if (now == null || next == null) return "";
            var parts = new List<string>(3);
            if (now.Attack > 0 || next.Attack > 0) parts.Add($"공격력 {now.Attack} → {next.Attack}");
            if (now.Defense > 0 || next.Defense > 0) parts.Add($"방어 {now.Defense} → {next.Defense}");
            if (now.Hp > 0 || next.Hp > 0) parts.Add($"체력 {now.Hp} → {next.Hp}");
            return string.Join(" · ", parts);
        }

        /// <summary>한 단계 올렸을 때의 능력치 줄(StatLine(장비, 장비.WithEnhance(n + 1))).</summary>
        public static string EnhanceStatLine(GearItem item) => item == null ? "" : StatLine(item, item.WithEnhance(item.Enhance + 1));

        /// <summary>비용 줄: "강화석 3 (가진 것 31)".</summary>
        public static string CostLine(int cost, int have) => $"강화석 {cost} (가진 것 {have})";

        /// <summary>확률 줄: "성공 85%" / 확정이면 "성공 100% — 확정".</summary>
        public static string ChanceLine(int chancePermille) =>
            chancePermille >= 1000 ? "성공 100% — 확정" : "성공 " + Percent(chancePermille) + "%";

        /// <summary>천장 줄(+2~+5): 한 번 실패하면 확정.</summary>
        public const string PityShortLine = "한 번 실패하면 다음은 반드시 성공한다.";

        /// <summary>천장 줄(+6·+7): "실패할 때마다 +10%p, 두 번 실패하면 다음은 반드시 성공한다. (지금 1/2)".</summary>
        public static string PityGaugeLine(int gaugeStepPermille, int pityFails, int fails) =>
            $"실패할 때마다 +{Percent(gaugeStepPermille)}%p, {Times(pityFails)} 실패하면 다음은 반드시 성공한다. (지금 {Math.Max(0, fails)}/{pityFails})";

        /// <summary>견적의 천장 줄. 천장이 0(+1, 늘 성공)이거나 막힌 상한 견적이면 null.</summary>
        public static string PityLine(EnhanceQuote q)
        {
            if (q.PityFails <= 0) return null;
            if (q.GaugeStepPermille > 0) return PityGaugeLine(q.GaugeStepPermille, q.PityFails, q.Fails);
            return q.PityFails == 1 ? PityShortLine : $"{Times(q.PityFails)} 실패하면 다음은 반드시 성공한다.";
        }

        /// <summary>실패 규칙 줄(+7까지).</summary>
        public const string FailRuleLine = "실패하면 강화석만 잃는다. 장비는 그대로다.";

        /// <summary>등급 상한 줄: "일반은 +5까지 오른다."</summary>
        public static string GradeCapLine(Grade grade)
        {
            string name = GradeRules.Name(grade);
            return $"{name}{TopicParticle(name)} +{GearMath.EnhanceCap(grade)}까지 오른다.";
        }

        /// <summary>1차 상한 줄(EnhanceRules.FirstPassMax = 7).</summary>
        public const string FirstPassCapLine = "지금은 +7까지 — +8부터는 다음에 연다.";

        /// <summary>강화석 모자람: "강화석이 모자란다 — 3 필요, 2 있음".</summary>
        public static string NotEnoughStonesLine(int need, int have) => $"강화석이 모자란다 — {need} 필요, {have} 있음";

        /// <summary>고른 장비가 없을 때.</summary>
        public const string NoItemLine = "두드릴 장비를 고르자.";

        /// <summary>견적 막힘 이유 한 줄(막힘 없으면 null). have = 가진 강화석.</summary>
        public static string BlockLine(EnhanceQuote q, GearItem item, int have)
        {
            switch (q.Block)
            {
                case EnhanceBlock.NoItem: return NoItemLine;
                case EnhanceBlock.GradeCap: return item != null ? GradeCapLine(item.Grade) : NoItemLine;
                case EnhanceBlock.FirstPassCap: return FirstPassCapLine;
                case EnhanceBlock.NotEnoughStones: return NotEnoughStonesLine(q.Cost, have);
                default: return null;
            }
        }

        /// <summary>두드리기 단추.</summary>
        public const string EnhanceButton = "두드리기";

        /// <summary>성공 줄: "쨍 — 희귀 장검 +4!"(새 장비를 넘긴다).</summary>
        public static string SuccessLine(GearItem upgraded) => upgraded == null ? "" : $"쨍 — {upgraded.DisplayName} +{upgraded.Enhance}!";

        /// <summary>
        /// 실패 줄: "치익 — 실패. 강화석 3을 잃었다. 다음은 확정." / "…다음은 성공 80%."
        /// nextChancePermille = 실패 뒤 같은 단계의 확률(EnhanceRules.ChancePermille(목표, 새 실패 수)).
        /// </summary>
        public static string FailLine(int spent, int nextChancePermille) =>
            $"치익 — 실패. 강화석 {spent}{TownScript.ObjectParticle(spent)} 잃었다. " +
            (nextChancePermille >= 1000 ? "다음은 확정." : "다음은 성공 " + Percent(nextChancePermille) + "%.");

        /// <summary>굴림 결과로 실패 줄(실패한 결과만, 성공이면 SuccessLine).</summary>
        public static string FailLine(EnhanceOutcome outcome)
        {
            var item = outcome.Item;
            if (item == null) return "";
            return FailLine(outcome.Spent, EnhanceRules.ChancePermille(item.Enhance + 1, item.EnhanceFails));
        }

        /// <summary>굴림 결과 줄(성공이면 SuccessLine, 실패면 FailLine, 막히면 null).</summary>
        public static string ResultLine(EnhanceOutcome outcome)
        {
            if (outcome.Block != EnhanceBlock.None) return null;
            return outcome.Success ? SuccessLine(outcome.Item) : FailLine(outcome);
        }

        /// <summary>모루 위에 띄우는 글(2-7): "+4".</summary>
        public static string OverlayLine(int enhance) => "+" + enhance.ToString(Inv);

        // ── 3장 강화 사라짐 ──
        /// <summary>카드 빨간 줄: "강화 +3 사라짐 — 옮겨지지 않는다".</summary>
        public static string EnhanceLostLine(int enhance) => $"강화 +{enhance} 사라짐 — 옮겨지지 않는다";

        // ── 4-2 가방 상세 단추 ──
        /// <summary>분해 단추: "분해 (+2석)".</summary>
        public static string SalvageButton(int stones) => $"분해 (+{stones}석)";
        public const string PutDownButton = "내려놓기";
        /// <summary>마을의 내려놓기(없앰).</summary>
        public const string DiscardButton = "버리기";
        /// <summary>낀 장비를 골랐을 때 회색 단추 말풍선.</summary>
        public const string EquippedLocked = "낀 장비는 벗은 뒤에";

        // ── 4-3 한 번 더 묻기 ──
        public static string ConfirmSalvage(int stones) => $"한 번 더 누르면 분해 (+{stones}석)";
        public const string ConfirmPutDown = "한 번 더 누르면 내려놓기";
        /// <summary>마을 버리기(늘 묻는다): "한 번 더 누르면 버린다 — 분해하면 강화석 +6".</summary>
        public static string ConfirmDiscard(int stones) => $"한 번 더 누르면 버린다 — 분해하면 강화석 +{stones}";

        // ── 2-7·4-1·4-4 알림 ──
        /// <summary>분해 알림: "희귀 대검 분해 — 강화석 +6".</summary>
        public static string SalvagedNotice(string name, int stones) => $"{name} 분해 — 강화석 +{stones}";
        /// <summary>분해한 무기의 룬이 주머니에 들어가지 못했을 때: "룬 주머니가 가득 차 룬 1개를 버렸다".</summary>
        public static string RunePouchFullNotice(int lost) => $"룬 주머니가 가득 차 룬 {lost}개를 버렸다";
        /// <summary>던전 내려놓기: "일반 장검을 발밑에 내려놓았다".</summary>
        public static string PutDownNotice(string name) => $"{name}{ObjectParticle(name)} 발밑에 내려놓았다";
        /// <summary>마을 버리기: "일반 장검을 버렸다".</summary>
        public static string DiscardNotice(string name) => $"{name}{ObjectParticle(name)} 버렸다";

        // ── 4-5 모루 창 '분해' 칸 ──
        public const string SalvageTabIntro = "가방 장비를 녹여 강화석으로 바꾼다. 낀 장비는 벗은 뒤에 녹인다.";
        /// <summary>가방에 장비가 없을 때.</summary>
        public const string SalvageTabEmpty = "가방에 녹일 장비가 없다.";
        /// <summary>분해 칸 줄: "희귀 대검 +2 — 강화석 7"(강화 +0이면 단계를 붙이지 않음). 없으면 빈 글.</summary>
        public static string SalvageRow(GearItem item)
        {
            if (item == null) return "";
            string step = item.Enhance > 0 ? " +" + item.Enhance.ToString(Inv) : "";
            return $"{item.DisplayName}{step} — 강화석 {SalvageRules.Stones(item)}";
        }
        public const string SalvageRowButton = "분해";
        /// <summary>맨 아래 단추: "일반 모두 분해 — 8개 · 강화석 +8".</summary>
        public static string BulkSalvageButton(int count, int stones) => $"일반 모두 분해 — {count}개 · 강화석 +{stones}";
        /// <summary>일괄 분해 한 번 더 묻기(늘 묻는다).</summary>
        public static string ConfirmBulkSalvage(int count, int stones) => $"한 번 더 누르면 일반 {count}개 분해 (+{stones}석)";
        /// <summary>일괄 대상이 없을 때(회색 단추).</summary>
        public const string BulkSalvageNone = "일반 모두 분해 — 녹일 일반 장비가 없다";
        /// <summary>일괄 분해 알림: "일반 8개 분해 — 강화석 +8".</summary>
        public static string BulkSalvagedNotice(int count, int stones) => $"일반 {count}개 분해 — 강화석 +{stones}";

        // ── 5-1 넘침 ──
        /// <summary>넘침 끝에서 G로 바꿔 낌: "가방이 넘쳤다 — 벗은 일반 장검을 발밑에 내려놓았다".</summary>
        public static string OverflowSwapNotice(string name) => $"가방이 넘쳤다 — 벗은 {name}{ObjectParticle(name)} 발밑에 내려놓았다";
        /// <summary>넘침 끝에서 반지·목걸이 벗기.</summary>
        public const string OverflowUnequipNotice = "가방이 넘쳤다 — 분해하거나 내려놓은 뒤 벗을 수 있다";

        // ── 5-2 저절로 분해 ──
        /// <summary>하나: "일반 가죽 장화 — 저절로 분해 · 강화석 +1".</summary>
        public static string AutoSalvageNotice(string name, int stones) => $"{name} — 저절로 분해 · 강화석 +{stones}";
        /// <summary>2초 안에 여럿을 한 줄로: "저절로 분해 3개 · 강화석 +3".</summary>
        public static string AutoSalvageBatchNotice(int count, int stones) => $"저절로 분해 {count}개 · 강화석 +{stones}";
        /// <summary>F1 손잡이 ㉡: "저절로 분해(일반) 켬".</summary>
        public static string AutoSalvageToggle(bool on) => "저절로 분해(일반) " + (on ? "켬" : "끔");
        /// <summary>F1 손잡이 ㉠: "저절로 줍기 여유 칸 0/3".</summary>
        public static string AutoReserveToggle(int reserve) => $"저절로 줍기 여유 칸 {reserve}/{BagRules.AutoReserveSlots}";

        // ── 5-3 줍기 판정 알림 ──
        /// <summary>F 줍기 막힘: "가방이 가득 찼다 (20/20) — [I] 가방에서 분해하거나 내려놓자".</summary>
        public static string BagFullNotice(int count, int capacity) => $"가방이 가득 찼다 ({count}/{capacity}) — [I] 가방에서 분해하거나 내려놓자";
        /// <summary>떠날 때 가득이라 분해: "가방 가득 — 일반·고급 3개 분해 · 강화석 +4".</summary>
        public static string SweepSalvageLine(int count, int stones) => $"가방 가득 — 일반·고급 {count}개 분해 · 강화석 +{stones}";

        // ── 5-4 화면 표시 ──
        /// <summary>던전 HUD 가방 단추 아래: "17/20"(넘치면 "22/20").</summary>
        public static string BagCountLine(int count, int capacity) => $"{count}/{capacity}";
        /// <summary>마을 HUD 지갑 줄 아래: "가방 17/20".</summary>
        public static string TownBagLine(int count, int capacity) => "가방 " + BagCountLine(count, capacity);
        /// <summary>가방 창 칸 줄: "17 / 20칸".</summary>
        public static string BagWindowLine(int count, int capacity) => $"{count} / {capacity}칸";
        /// <summary>가방이 가득일 때 가장 가까운 바닥 이름표 끝: "[F] 일반 장검 · 가방 가득".</summary>
        public const string FloorLabelFullSuffix = " · 가방 가득";
        /// <summary>권양기 창 덧줄: "가방이 거의 찼다 (17/20) — 모루에서 분해하고 가도 된다."</summary>
        public static string GateNearlyFullLine(int count, int capacity) => $"가방이 거의 찼다 ({count}/{capacity}) — 모루에서 분해하고 가도 된다.";

        // ── 6-1·6-2 대장간 물건 ──
        /// <summary>물건 칸 맨 아래.</summary>
        public const string NoStonesForGold = "강화석은 골드로 살 수 없다 — 갱도와 분해에서만 나온다.";
        public const string BuyButton = "사기";
        /// <summary>갖춤(회색).</summary>
        public const string OwnedLabel = "갖춤";
        /// <summary>값: "150골드".</summary>
        public static string PriceLine(int price) => $"{price}골드";
        /// <summary>물건 칸 골드 줄: "골드 250".</summary>
        public static string GoldLine(int gold) => $"골드 {gold}";
        /// <summary>잠김: "가방 쇠틀이 먼저 있어야 한다".</summary>
        public static string LockedLine(string requiresName) => $"{requiresName}{SubjectParticle(requiresName)} 먼저 있어야 한다";
        /// <summary>모자람: "골드가 모자란다 — 150 필요, 100 있음".</summary>
        public static string NotEnoughGoldLine(int price, int have) => $"골드가 모자란다 — {price} 필요, {have} 있음";

        /// <summary>칸 줄 상태 글(살 수 있음 '사기' / 갖춤 '갖춤' / 잠김 / 모자람). 모르는 물건이면 빈 글.</summary>
        public static string GoodStateLine(CarryData carry, string id)
        {
            var good = ForgeShop.Get(id);
            if (good == null) return "";
            switch (ForgeShop.State(carry, id))
            {
                case ForgeBuyState.CanBuy: return BuyButton;
                case ForgeBuyState.Owned: return OwnedLabel;
                case ForgeBuyState.NotEnoughGold: return NotEnoughGoldLine(good.Price, carry != null ? carry.Gold : 0);
                default:
                    var need = ForgeShop.Get(good.Requires);
                    return need != null ? LockedLine(need.Name) : "";
            }
        }

        /// <summary>
        /// 산 뒤 알림: "가방 쇠틀을 달았다 — 가방 25칸", "큰 가방 쇠틀을 달았다 — 가방 30칸", "허리 병걸이를 찼다 — 원정마다 물약 4병".
        /// 모르는 물건이면 null.
        /// </summary>
        public static string BoughtNotice(string id)
        {
            var good = ForgeShop.Get(id);
            if (good == null) return null;
            string name = good.Name + ObjectParticle(good.Name);
            switch (good.Id)
            {
                case ForgeShop.Bag25: return $"{name} 달았다 — 가방 {BagRules.Capacity(1)}칸";
                case ForgeShop.Bag30: return $"{name} 달았다 — 가방 {BagRules.Capacity(2)}칸";
                case ForgeShop.Flask: return $"{name} 찼다 — 원정마다 물약 {ForgeShop.BasePotions + ForgeShop.FlaskPotions}병";
                default: return null;
            }
        }

        // ── 조사·숫자 ──

        /// <summary>목적격 조사(받침 있으면 '을', 없으면 '를'). 숫자로 끝나면 읽은 끝소리로(TownScript.ObjectParticle과 같음).</summary>
        public static string ObjectParticle(string word) => HasFinalConsonant(word) ? "을" : "를";
        /// <summary>주격 조사(받침 있으면 '이', 없으면 '가').</summary>
        public static string SubjectParticle(string word) => HasFinalConsonant(word) ? "이" : "가";
        /// <summary>보조사(받침 있으면 '은', 없으면 '는').</summary>
        public static string TopicParticle(string word) => HasFinalConsonant(word) ? "은" : "는";

        /// <summary>
        /// 낱말 끝소리에 받침이 있는가. 끝의 띄어쓰기·문장 부호는 건너뛴다. 한글 음절은 받침 유무, 숫자는 읽은 소리
        /// (0 영·1 일·3 삼·6 육·7 칠·8 팔 = 받침), 그 밖의 글자는 받침 없음으로 본다.
        /// </summary>
        static bool HasFinalConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            for (int i = word.Length - 1; i >= 0; i--)
            {
                char c = word[i];
                if (c >= '가' && c <= '힣') return (c - '가') % 28 != 0;
                if (c >= '0' && c <= '9') return "013678".IndexOf(c) >= 0;
                if (char.IsLetter(c)) return false;
            }
            return false;
        }

        /// <summary>‰를 %로: 850 → "85", 805 → "80.5".</summary>
        public static string Percent(int permille) =>
            permille % 10 == 0 ? (permille / 10).ToString(Inv) : (permille / 10.0).ToString("0.#", Inv);

        /// <summary>횟수 말: 1 → "한 번", 2 → "두 번", 3 → "세 번", 그 밖 "n번".</summary>
        static string Times(int n)
        {
            switch (n)
            {
                case 1: return "한 번";
                case 2: return "두 번";
                case 3: return "세 번";
                default: return n.ToString(Inv) + "번";
            }
        }

        // ── 시험 ──

        /// <summary>이 파일의 글 전부(상수와 보기 값을 넣은 함수 글). 시험이 주민 이름·빈 글을 찾는다.</summary>
        public static IEnumerable<string> AllTexts
        {
            get
            {
                yield return WindowTitle;
                foreach (var t in Tabs) yield return t;
                yield return TabsLine;
                yield return FooterHint;
                yield return FooterHintSalvage;
                yield return FooterHintGoods;
                yield return EquippedTag;
                yield return BagTag;
                var sword = new GearItem(GearBaseTable.Longsword, Grade.Rare, 2, 1000, 3);
                var armor = new GearItem(GearBaseTable.LeatherArmor, Grade.Common, 1, 1000, 4);
                var amulet = new GearItem(GearBaseTable.AmberAmulet, Grade.Uncommon, 1, 1000);
                yield return EnhanceRow(sword);
                yield return EnhanceRow(sword, GearSlots.SlotName(GearSlot.Ring1));
                yield return StepLine(3, 4);
                yield return EnhanceStatLine(sword);
                yield return EnhanceStatLine(armor);
                yield return EnhanceStatLine(amulet);
                yield return CostLine(3, 31);
                yield return ChanceLine(850);
                yield return ChanceLine(1000);
                yield return PityShortLine;
                yield return PityGaugeLine(100, 2, 1);
                yield return FailRuleLine;
                yield return GradeCapLine(Grade.Common);
                yield return GradeCapLine(Grade.Uncommon);
                yield return FirstPassCapLine;
                yield return NotEnoughStonesLine(3, 2);
                yield return NoItemLine;
                yield return EnhanceButton;
                yield return SuccessLine(sword.WithEnhance(4));
                yield return FailLine(3, 1000);
                yield return FailLine(3, 800);
                yield return OverlayLine(4);
                yield return EnhanceLostLine(3);
                yield return SalvageButton(2);
                yield return PutDownButton;
                yield return DiscardButton;
                yield return EquippedLocked;
                yield return ConfirmSalvage(6);
                yield return ConfirmPutDown;
                yield return ConfirmDiscard(6);
                yield return SalvagedNotice(sword.DisplayName, 6);
                yield return RunePouchFullNotice(1);
                yield return PutDownNotice(armor.DisplayName);
                yield return DiscardNotice(armor.DisplayName);
                yield return SalvageTabIntro;
                yield return SalvageTabEmpty;
                yield return SalvageRow(sword);
                yield return SalvageRow(amulet);
                yield return SalvageRowButton;
                yield return BulkSalvageButton(8, 8);
                yield return ConfirmBulkSalvage(8, 8);
                yield return BulkSalvageNone;
                yield return BulkSalvagedNotice(8, 8);
                yield return OverflowSwapNotice(armor.DisplayName);
                yield return OverflowUnequipNotice;
                yield return AutoSalvageNotice(armor.DisplayName, 1);
                yield return AutoSalvageBatchNotice(3, 3);
                yield return AutoSalvageToggle(true);
                yield return AutoSalvageToggle(false);
                yield return AutoReserveToggle(0);
                yield return AutoReserveToggle(BagRules.AutoReserveSlots);
                yield return BagFullNotice(20, 20);
                yield return SweepSalvageLine(3, 4);
                yield return BagCountLine(17, 20);
                yield return TownBagLine(17, 20);
                yield return BagWindowLine(22, 20);
                yield return FloorLabelFullSuffix;
                yield return GateNearlyFullLine(17, 20);
                yield return NoStonesForGold;
                yield return BuyButton;
                yield return OwnedLabel;
                yield return PriceLine(150);
                yield return GoldLine(250);
                yield return NotEnoughGoldLine(150, 100);
                foreach (var g in ForgeShop.All)
                {
                    yield return g.Name;
                    yield return g.Line;
                    yield return PriceLine(g.Price);
                    yield return BoughtNotice(g.Id);
                    var need = ForgeShop.Get(g.Requires);
                    if (need != null) yield return LockedLine(need.Name);
                }
            }
        }
    }
}
