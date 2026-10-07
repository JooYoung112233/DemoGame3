using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 룬 주머니·룬 홈 끼우기·빼기(기획/세-무기-우클릭-소켓-1차.md 6-4·6-5, 5-3). 게임 안 말은 '소켓' 대신 '룬 홈'.
    /// 주머니 = 룬 id → 개수(종류마다 RuneRules.PouchCap, 가방 칸 안 씀). 꾸러미(ExportTo/ImportFrom)에 같이 담는다.
    /// 끼우기·빼기는 새 GearItem을 만들어 같은 자리(장착 칸·가방 칸·고른 것)를 갈아 끼우고(ReplaceItem) 능력치·룬을 다시 넣는다. 빼면 공짜로 주머니로 돌아온다.
    /// 무기만, 빈 홈에만, 같은 룬은 한 무기에 하나만. 효과는 낀 무기의 룬만 센다(RecomputeStats → PlayerController.SetWeaponRunes).
    /// 무기 행동 중(PlayerController.InWeaponAct)에는 끼우기·빼기·장착 단추가 회색('무기 행동 중')이고 G를 무시한다.
    /// 처치·둥지 정리 룬은 장비 난수와 따로 흐르는 난수(시각 씨앗, RuneRules.KillStream)로 굴린다. UI는 IMGUI 기능만(배치는 Unity 개발 단계).
    /// </summary>
    public sealed partial class Inventory
    {
        /// <summary>무기 행동 중 회색 단추 글.</summary>
        public const string WeaponActLockedText = "무기 행동 중";
        /// <summary>무기를 바꿔 낄 때 옛 무기에 룬이 남아 있으면 띄우는 한 줄(6-5).</summary>
        public const string OldWeaponRunesNotice = "옛 무기에 룬이 남아 있다 — 가방에서 옮길 수 있다";
        /// <summary>처음 룬을 주웠을 때 한 번만 띄우는 알림의 꾸러미 세기 키(5-9).</summary>
        public const string RuneHintKey = "rune_hint";

        readonly Dictionary<string, int> _runePouch = new Dictionary<string, int>();
        Pcg32Random _runeRng;
        GUIStyle _runeMark;

        /// <summary>룬 주머니(읽기 전용). 룬 id → 개수.</summary>
        public IReadOnlyDictionary<string, int> RunePouch => _runePouch;

        /// <summary>주머니의 그 룬 개수.</summary>
        public int RuneCount(string runeId) => runeId != null && _runePouch.TryGetValue(runeId, out int n) ? Mathf.Max(0, n) : 0;

        /// <summary>주머니 룬 합계.</summary>
        public int RuneTotal
        {
            get
            {
                int sum = 0;
                foreach (var kv in _runePouch) sum += Mathf.Max(0, kv.Value);
                return sum;
            }
        }

        /// <summary>무기 행동 중이라 장착·룬 끼우기·빼기·G를 막는가(5-3).</summary>
        public bool WeaponActLocked => _player && _player.InWeaponAct;

        /// <summary>주머니에 룬을 더한다(바닥 줍기·시험 패널). 종류마다 RuneRules.PouchCap까지, 모르는 룬은 거른다. 실제로 더한 개수.</summary>
        public int AddRunes(string runeId, int count = 1)
        {
            int added = RuneRules.PouchAdd(_runePouch, runeId, count);
            if (added > 0) _card = null;
            return added;
        }

        void ClearRunePouch() => _runePouch.Clear();

        /// <summary>꾸러미에 주머니를 담는다(0개 줄은 담지 않음).</summary>
        void ExportRunes(CarryData data)
        {
            data.RunePouch.Clear();
            foreach (var kv in _runePouch)
                if (kv.Value > 0) data.RunePouch[kv.Key] = kv.Value;
        }

        /// <summary>꾸러미에서 주머니를 푼다(모르는 룬·상한은 RuneRules.PouchAdd가 거름).</summary>
        void ImportRunes(CarryData data)
        {
            _runePouch.Clear();
            foreach (var kv in data.RunePouch) RuneRules.PouchAdd(_runePouch, kv.Key, kv.Value);
        }

        /// <summary>처치·둥지 정리 룬 난수(시각 씨앗, 흐름 RuneRules.KillStream). 장비 처치 난수(흐름 23)와 따로 흘러 기존 결과가 바뀌지 않는다.</summary>
        Pcg32Random RuneRng => _runeRng ?? (_runeRng = new Pcg32Random((ulong)System.DateTime.UtcNow.Ticks, RuneRules.KillStream));

        /// <summary>처치 보상 묶음에 룬을 굴려 붙인다(6-3 표: 굴쥐 3‰·궁수 15‰·멧돼지 25‰·정예 150‰·둥지 정리 80‰, 층 배율 없음, 난수 늘 2번).</summary>
        void RollKillRune(LootBundle bundle, KillSource source) => RuneRules.AddTo(bundle, RuneRules.SourceOf(source), RuneRng);

        /// <summary>이 장비가 장착 칸이나 가방에 있는가(참조로 찾음).</summary>
        bool Owns(GearItem item) => item != null && (Equipment.IsEquipped(item) || _bag.Contains(item));

        /// <summary>주머니에서 이 장비에 끼울 수 있는 첫 룬(RuneTable 차례). 없으면 null. 룬 종류가 늘면 고르기 창을 둔다(지금은 버팀 룬 하나).</summary>
        public string InsertableRune(GearItem item)
        {
            if (item == null) return null;
            foreach (var r in RuneTable.All)
                if (RuneCount(r.Id) > 0 && RuneRules.CanInsert(item.Part, item.Grade, item.Runes, r.Id)) return r.Id;
            return null;
        }

        /// <summary>지금 이 장비에 그 룬을 끼울 수 있는가(무기 행동 중 아님·가진 장비·주머니에 있음·RuneRules.CanInsert).</summary>
        public bool CanInsertRune(GearItem item, string runeId) =>
            item != null && !WeaponActLocked && Owns(item) && RuneCount(runeId) > 0 && RuneRules.CanInsert(item.Part, item.Grade, item.Runes, runeId);

        /// <summary>
        /// 주머니의 룬 하나를 이 장비의 빈 홈에 끼운다(새 GearItem으로 같은 자리를 갈아 끼움). 알림 '희귀 대검에 버팀 룬을 끼웠다'.
        /// 바꾼 새 장비를 돌려준다(못 끼우면 null).
        /// </summary>
        public GearItem InsertRune(GearItem item, string runeId)
        {
            if (!CanInsertRune(item, runeId)) return null;
            var runes = new List<string>(item.Runes) { runeId };
            var next = item.WithRunes(runes);
            if (!next.HasRune(runeId) || ReplaceItem(item, next) == null) return null;
            RuneRules.PouchTake(_runePouch, runeId);
            _card = null;
            string name = RuneName(runeId);
            Sfx.Play(SfxKind.Pickup);
            DungeonEvents.Say(item.DisplayName + "에 " + name + ObjectParticle(name) + " 끼웠다");
            return next;
        }

        /// <summary>
        /// index번째 홈의 룬을 빼서 주머니로 돌려보낸다(공짜, 6-5). 주머니가 가득(RuneRules.PouchCap)이면 빼지 않는다.
        /// 바꾼 새 장비를 돌려준다(못 빼면 null).
        /// </summary>
        public GearItem RemoveRune(GearItem item, int index)
        {
            if (item == null || WeaponActLocked || !Owns(item) || index < 0 || index >= item.Runes.Count) return null;
            string runeId = item.Runes[index];
            if (RuneCount(runeId) >= RuneRules.PouchCap)
            {
                DungeonEvents.Say(RuneName(runeId) + " 주머니가 가득 찼다 (" + RuneRules.PouchCap + "개)");
                return null;
            }
            var runes = new List<string>(item.Runes);
            runes.RemoveAt(index);
            var next = item.WithRunes(runes);
            if (ReplaceItem(item, next) == null) return null;
            RuneRules.PouchAdd(_runePouch, runeId, 1);
            _card = null;
            string name = RuneName(runeId);
            Sfx.Play(SfxKind.Pickup);
            DungeonEvents.Say(item.DisplayName + "에서 " + name + ObjectParticle(name) + " 뺐다 — 주머니로");
            return next;
        }

        /// <summary>
        /// 장비를 새 장비로 같은 자리에 갈아 끼운다(장착 칸이면 그 칸, 가방이면 같은 칸 번호, 고른 것도 따라감). 장착 칸이면 능력치·룬을 다시 넣는다
        /// (RecomputeAfterGearChange → SetWeaponRunes). 장착에도 가방에도 없거나 부위가 맞지 않으면 null.
        /// </summary>
        public GearItem ReplaceItem(GearItem oldItem, GearItem newItem)
        {
            if (oldItem == null || newItem == null) return null;
            if (ReferenceEquals(oldItem, newItem)) return newItem;
            var slot = Equipment.SlotOf(oldItem);
            if (slot.HasValue)
            {
                if (!Loadout.CanEquip(slot.Value, newItem) || !Equipment.TryEquip(slot.Value, newItem, out _)) return null;
            }
            else
            {
                int index = _bag.IndexOf(oldItem);
                if (index < 0) return null;
                _bag[index] = newItem;
            }
            if (ReferenceEquals(_selected, oldItem)) _selected = newItem;
            _card = null;
            if (slot.HasValue) RecomputeAfterGearChange();
            return newItem;
        }

        static string RuneName(string runeId) => RuneTable.Get(runeId)?.Name ?? runeId;

        /// <summary>목적격 조사: 받침이 있으면 '을', 없으면 '를'(한글이 아니면 '을').</summary>
        static string ObjectParticle(string word)
        {
            if (string.IsNullOrEmpty(word)) return "을";
            char c = word[word.Length - 1];
            if (c < '가' || c > '힣') return "을";
            return (c - '가') % 28 != 0 ? "을" : "를";
        }

        // ── 카드·가방 그리기(IMGUI 기능만) ──

        /// <summary>
        /// 카드 룬 홈 줄(옵션 줄 뒤, 6-5): '룬 홈 ◆◇ · 버팀 룬'(룬 색, 빈 홈뿐이면 회색). 이 무기 행동에 효과 없는 룬은 회색 '버팀 룬: 이 무기에서는 효과 없음'.
        /// 무기가 아니면(홈 0) 줄을 넣지 않는다.
        /// </summary>
        void AddRuneCardLines(List<CardLine> lines, GearItem item)
        {
            string socket = GearNaming.SocketLine(item);
            if (socket == null) return;
            lines.Add(new CardLine(socket, item.Runes.Count > 0 ? RuneLook.ColorOf(item.Runes[0]) : SameColor));
            foreach (var id in item.Runes)
                if (!RuneRules.Works(id, item.BaseId, Tuning.SuperArmorAllActs))
                    lines.Add(new CardLine(RuneName(id) + ": 이 무기에서는 효과 없음", SameColor));
        }

        /// <summary>가방 제목 줄의 주머니 글('룬: 버팀 ×2', 없으면 회색 '룬: 없음').</summary>
        void DrawRunePouchLine(Rect area)
        {
            var parts = new List<string>();
            Color color = DungeonUi.BoneDim;
            foreach (var r in RuneTable.All)
            {
                int n = RuneCount(r.Id);
                if (n <= 0) continue;
                if (parts.Count == 0) color = RuneLook.ColorOf(r);
                parts.Add(r.ShortName + " ×" + n);
            }
            var prev = GUI.color;
            GUI.color = color;
            DungeonUi.CompactLabel(area, "룬: " + (parts.Count > 0 ? string.Join(" · ", parts) : "없음"), DungeonUi.Small);
            GUI.color = prev;
        }

        /// <summary>칸 그림 오른쪽 아래 룬 홈 표시: ◆(끼움, 룬 색)·◇(빈 홈, 회색). 홈이 없는 장비는 그리지 않는다.</summary>
        void DrawRuneMarks(Rect rect, GearItem item)
        {
            if (item == null) return;
            int sockets = item.SocketCount;
            if (sockets <= 0) return;
            if (_runeMark == null) _runeMark = new GUIStyle(DungeonUi.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontSize = 13 };
            const float size = 13f;
            var prev = GUI.color;
            for (int i = 0; i < sockets; i++)
            {
                bool filled = i < item.Runes.Count;
                // 왼쪽부터 홈 차례(◆◇). 아래 등급 점 줄(yMax − 13)과 겹치지 않게 그 위에 둔다.
                var at = new Rect(rect.xMax - 9f - size * (sockets - i), rect.yMax - 28f, size, 14f);
                GUI.color = filled ? RuneLook.ColorOf(item.Runes[i]) : SameColor;
                GUI.Label(at, filled ? GearNaming.FilledSocket.ToString() : GearNaming.EmptySocket.ToString(), _runeMark);
            }
            GUI.color = prev;
        }

        /// <summary>
        /// 상세 단추 아래 룬 홈 단추 줄(6-5): 홈마다 하나. 빈 홈 '◇ 끼우기'(주머니에 이 무기에 없는 룬이 있을 때만 켜짐), 찬 홈 '◆ 버팀 룬 빼기'.
        /// 무기 행동 중에는 모두 회색. 홈이 없는 장비는 그리지 않는다.
        /// </summary>
        void DrawRuneButtons(Rect row, GearItem item)
        {
            if (item == null || item.SocketCount <= 0) return;
            int sockets = item.SocketCount;
            const float gap = 10f;
            float w = (row.width - gap * (sockets - 1)) / sockets;
            bool locked = WeaponActLocked;
            string insertable = locked ? null : InsertableRune(item);
            bool was = GUI.enabled;
            for (int i = 0; i < sockets; i++)
            {
                var at = new Rect(row.x + i * (w + gap), row.y, w, row.height);
                if (i < item.Runes.Count)
                {
                    string id = item.Runes[i];
                    var def = RuneTable.Get(id);
                    GUI.enabled = was && !locked;
                    var label = new GUIContent(GearNaming.FilledSocket + " " + RuneName(id) + " 빼기", def != null ? def.Name + ": " + def.EffectLine : "");
                    if (GUI.Button(at, label) && !locked)
                    {
                        RemoveRune(item, i);
                        GUI.enabled = was;
                        return;
                    }
                }
                else
                {
                    GUI.enabled = was && insertable != null;
                    var def = insertable != null ? RuneTable.Get(insertable) : null;
                    string tip = locked ? WeaponActLockedText : def != null ? def.Name + ": " + def.EffectLine : "주머니에 끼울 룬이 없다";
                    if (GUI.Button(at, new GUIContent(GearNaming.EmptySocket + " 끼우기", tip)) && insertable != null)
                    {
                        InsertRune(item, insertable);
                        GUI.enabled = was;
                        return;
                    }
                }
            }
            GUI.enabled = was;
        }
    }
}
