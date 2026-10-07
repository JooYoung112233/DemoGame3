using System;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.TestStart;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// F1 마을 시험 패널(기획/마을-의뢰-첫판.md 9-2 'F1 마을 시험 패널', IMGUI 기능만, 던전 F1 패널과 같은 오른쪽 판).
    /// 의뢰마다 상태 줄 + [받기] [달성] [보상 받음] [처음으로], [진행 중 모두 달성], [오우거 굴 있음 가정] 옆에 지금 던전 상태
    /// (QuestContext.Live, 2층 계단 아래 굴이 있으면 '굴 있음'이고 가정 없이도 열리며 실제 처치로 센다. 가정은 굴이 없는 빌드에서 받기·문구만 볼 때).
    /// 이름: 주민마다 [공개] / [모르는 상태로(시험)]. 장면: [오프닝 다시] [의뢰·이름·장면 지우기]. [바로 던전으로](밤 카드 없이, 줄 끝, Basket) / [권양기처럼 출발].
    /// [줄 끝 +1](2층 승강장) / [명패 +1] / [골드 +100] / [강화석 +10] / [버팀 룬 +1](마을 가방 주머니). [새 프로필로] / [꾸러미 글 복사](클립보드).
    /// 모루(기획/재화-쓸-곳-1차.md 14장): [모루 창 열기] / [강화석 +50](꾸러미) / [대장간 물건 되돌리기](산 물건을 비우고 마을 가방 칸을 다시 맞춤).
    /// 전역 빛 밀대, 플레이어 좌표·주민 자리까지 거리, 이번 방문 시간·대화 합.
    /// 시험 패널이라 주민은 내부 ID로만 적는다(표시 이름은 SpeakerIdentity를 거치는 곳에만). 단추는 OnGUI에서 받아 다음 Update에서 한 번 처리한다.
    /// F1 시험 패널은 Unity 편집기에서만 연다(키 배치 1차 0장 5, DevPanelGate). 마을 안내 줄에는 F1을 적지 않는다.
    /// 단추·가정 토글을 쓰면 이번 판을 '시험 판'으로 적는다(TestRunFlag). 꾸러미 글 복사와 빛 밀대(보기만 바꿈)는 적지 않는다.
    /// </summary>
    public sealed class TownDebugPanel : MonoBehaviour
    {
        const float PanelWidth = 460f;
        const int GuiDepth = -5;

        /// <summary>패널이 보이는가(F1로 켜고 끔).</summary>
        public bool Visible { get; set; }
        /// <summary>마우스가 패널 위에 있는가.</summary>
        public bool PointerOverPanel { get; private set; }

        Rect _panelRect;
        Vector2 _scroll;
        Action _pending;
        GUIStyle _small;
        GUIStyle _button;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
            {
                // 닫기는 바로, 열기는 DevPanelGate(F1 시험 패널은 Unity 편집기에서만 연다, 키 배치 1차 0장 5).
                if (Visible) Visible = false;
                else if (DevPanelGate.RequestOpen(() => Visible = true)) Visible = true;
            }
            if (_pending != null)
            {
                var action = _pending;
                _pending = null;
                action();
            }
            _panelRect = new Rect(DungeonUi.Width - PanelWidth - 12f, 12f, PanelWidth, DungeonUi.Height - 24f);
            var mouse = Mouse.current;
            if (!Visible || mouse == null) PointerOverPanel = false;
            else
            {
                Vector2 m = mouse.position.ReadValue();
                PointerOverPanel = _panelRect.Contains(new Vector2(m.x, Screen.height - m.y) / DungeonUi.Scale);
            }
        }

        /// <summary>단추의 일을 다음 Update로 넘긴다. testUse면(판을 바꾸는 단추) 이번 판을 시험 판으로 적는다(4장 Q7).</summary>
        void Do(Action action, bool testUse = true)
        {
            _pending = action;
            if (testUse) TestRunFlag.Note(TestRunReason.DevPanel);
        }

        void OnGUI()
        {
            if (!Visible) return;
            if (PauseMenu.IsOpen) return; // 멈춤 창이 열려 있으면 F1 패널을 그리지 않는다(저장·처음 화면·멈춤 창 1차 5-1·8-1).
            var root = TownRoot.Instance;
            if (!root) return;
            var card = NightCard.Instance;
            if (card && card.Showing) return;
            DungeonUi.Begin();
            GUI.depth = GuiDepth;
            if (_small == null)
            {
                _small = new GUIStyle(DungeonUi.Small) { fontSize = 14 };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = false, padding = new RectOffset(6, 6, 3, 3) };
            }
            DungeonUi.Box(_panelRect, 0.88f);
            if (DungeonUi.CloseButton(_panelRect, "닫기 · F1"))
            {
                Visible = false;
                return;
            }
            GUI.Label(new Rect(_panelRect.x + 18f, _panelRect.y + 16f, _panelRect.width - 92f, 36f), "개발 · 마을 시험", DungeonUi.Title);
            GUILayout.BeginArea(new Rect(_panelRect.x + 16f, _panelRect.y + 60f, _panelRect.width - 32f, _panelRect.height - 76f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            var back = DevPanelExtras.DrawBackToLauncher(_button); if (back != null) Do(back, false);
            var carry = ProfileCarry.Ensure();
            var book = new QuestBook(carry);

            GUILayout.Label("[F1] 닫기 · 주민은 내부 ID로 적는다", _small);
            DrawVisit(root, carry);
            DrawQuests(root, book);
            DrawNames(carry);
            DrawFlow(root, carry);
            DrawCarry(root, carry);
            DrawForge(root, carry);
            DrawLight(root);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, DungeonUi.Bold);
        }

        void DrawVisit(TownRoot root, CarryData carry)
        {
            Section("방문");
            GUILayout.Label($"도착 {root.ArrivalType} · 원정 {carry.Expedition} · 밤 {carry.Night} · 방문 {TownSave.Visits(carry)} · 출발 {TownSave.Departures(carry)}", _small);
            GUILayout.Label($"머문 {root.VisitSeconds:0.0}초 · 대화 합 {root.TalkSeconds:0.0}초" + (root.LoadSeconds >= 0f ? $" · 불러오기 {root.LoadSeconds * 1000f:0}ms" : ""), _small);
            GUILayout.Label($"명패 {TownSave.Tags(carry)} · 등불 {TownLayout.LitLamps(TownSave.Tags(carry))}/{TownLayout.LampCount} · 줄 끝 제{carry.RopeEnd(FloorRecipe.MaxTestFloor)}층 · 레벨 {carry.Level} · 경험치 {carry.TotalXp} · 스킬 점수 {carry.SkillPoints}", _small);
            var player = root.Player;
            if (!player) return;
            var p = player.Position;
            GUILayout.Label($"플레이어 ({p.x:0.0}, {p.y:0.0}) · 갱도 입구 {Dist(p, TownLayout.Gate.Pos):0.0}", _small);
            string near = "";
            foreach (var npc in NpcTable.All)
                near += (near.Length > 0 ? " · " : "") + npc.Id + " " + Dist(p, TownLayout.NpcHome(npc.Id)).ToString("0.0");
            GUILayout.Label(near, _small);
            GUILayout.Label($"걸음 초당 {player.MoveSpeed:0.00} (귀환 → 갱도 입구 {TownLayout.WalkSeconds(TownLayout.ReturnPoint, TownLayout.Gate.Pos):0.0}초)", _small);
        }

        static float Dist(Vector2 p, TownVec v) => Vector2.Distance(p, new Vector2(v.X, v.Y));

        void DrawQuests(TownRoot root, QuestBook book)
        {
            Section("의뢰");
            foreach (var q in QuestTable.All)
            {
                string id = q.Id;
                GUILayout.Label(book.DebugLine(id), _small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("받기", _button)) Do(() => QuestAction(id, QuestState.Active));
                if (GUILayout.Button("달성", _button)) Do(() => QuestAction(id, QuestState.Achieved));
                if (GUILayout.Button("보상 받음", _button)) Do(() => QuestAction(id, QuestState.Rewarded));
                if (GUILayout.Button("처음으로", _button)) Do(() => QuestAction(id, QuestState.Locked));
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("진행 중 모두 달성", _button))
                Do(() =>
                {
                    foreach (var u in new QuestBook(ProfileCarry.Ensure()).AchieveAllActive())
                        if (u.Notice != null) DungeonEvents.Say(u.Notice);
                });
            bool assumed = GUILayout.Toggle(TownRoot.OgreDenAssumed, "오우거 굴 있음 가정");
            if (assumed != TownRoot.OgreDenAssumed)
                Do(() =>
                {
                    TownRoot.OgreDenAssumed = assumed;
                    new QuestBook(ProfileCarry.Ensure()).Refresh(TownRoot.QuestCtx);
                });
            // 지금 던전 상태(QuestContext.Live). 굴이 있으면 가정을 켜지 않아도 오우거 의뢰가 열린다.
            GUILayout.Label(QuestContext.Live.OgreDenReady ? "던전: 굴 있음" : "던전: 굴 없음", _small);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 의뢰 한 줄 시험: 받기(받을 수 있음이면 진짜 받기, 아니면 바로 진행 중), 달성, 보상 받음(달성이면 진짜 보고 — 보상이 들어감, 아니면 지급됨으로만),
        /// 처음으로. 바꾼 뒤 의뢰를 다시 계산해 뒤 의뢰가 열린다.
        /// </summary>
        static void QuestAction(string id, QuestState to)
        {
            var root = TownRoot.Instance;
            var carry = ProfileCarry.Ensure();
            var book = new QuestBook(carry);
            switch (to)
            {
                case QuestState.Active:
                    if (book.State(id) != QuestState.Offered || !book.Accept(id)) book.ForceState(id, QuestState.Active);
                    break;
                case QuestState.Rewarded:
                    if (book.State(id) == QuestState.Achieved)
                    {
                        var r = book.Report(id);
                        if (r.HasValue)
                        {
                            DungeonEvents.Say(TownScript.RewardNotice(r.Value));
                            string up = TownScript.LevelUpNotice(r.Value);
                            if (up != null) DungeonEvents.Say(up);
                            if (root) root.SyncProgress();
                        }
                    }
                    else book.ForceState(id, QuestState.Rewarded);
                    break;
                default:
                    book.ForceState(id, to);
                    break;
            }
            book.Refresh(TownRoot.QuestCtx);
        }

        void DrawNames(CarryData carry)
        {
            Section("이름");
            foreach (var npc in NpcTable.All)
            {
                string id = npc.Id;
                GUILayout.BeginHorizontal();
                GUILayout.Label(id + (SpeakerIdentity.Knows(carry, id) ? " · 앎" : " · 모름"), _small, GUILayout.Width(170f));
                if (GUILayout.Button("공개", _button)) Do(() => SpeakerIdentity.Reveal(ProfileCarry.Ensure(), id));
                if (GUILayout.Button("모르는 상태로(시험)", _button)) Do(() => TownSave.ForgetNameForTest(ProfileCarry.Ensure(), id));
                GUILayout.EndHorizontal();
            }
        }

        void DrawFlow(TownRoot root, CarryData carry)
        {
            Section("장면·출발");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("오프닝 다시", _button)) Do(() => { if (TownRoot.Instance) TownRoot.Instance.RestartOpening(); });
            if (GUILayout.Button("의뢰·이름·장면 지우기", _button))
                Do(() =>
                {
                    var c = ProfileCarry.Ensure();
                    TownSave.ClearStory(c);
                    new QuestBook(c).Refresh(TownRoot.QuestCtx);
                });
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("바로 던전으로", _button)) Do(() => { if (TownRoot.Instance) TownRoot.Instance.DepartDirect(); });
            if (GUILayout.Button("권양기처럼 출발", _button))
                Do(() =>
                {
                    if (TownRoot.Instance) TownRoot.Instance.Depart(ProfileCarry.Ensure().RopeEnd(FloorRecipe.MaxTestFloor));
                });
            GUILayout.EndHorizontal();
        }

        void DrawCarry(TownRoot root, CarryData carry)
        {
            Section("꾸러미");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("줄 끝 +1", _button))
                Do(() =>
                {
                    var c = ProfileCarry.Ensure();
                    c.RopeDepth = Mathf.Min(FloorRecipe.MaxTestFloor, c.RopeDepth + 1);
                });
            if (GUILayout.Button("명패 +1", _button)) Do(AddNameplate);
            if (GUILayout.Button("골드 +100", _button)) Do(() => ProfileCarry.Ensure().Gold += 100);
            if (GUILayout.Button("강화석 +10", _button)) Do(() => ProfileCarry.Ensure().Stones += 10);
            if (GUILayout.Button(RuneTable.SuperArmor.Name + " +1", _button)) Do(AddRune);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("새 프로필로", _button)) Do(NewProfile);
            if (GUILayout.Button("꾸러미 글 복사", _button))
                Do(() =>
                {
                    if (TownRoot.Instance) TownRoot.Instance.SyncProgress();
                    GUIUtility.systemCopyBuffer = ProfileCarry.Ensure().ToText();
                    DungeonEvents.Say("꾸러미 글을 클립보드에 복사했다");
                }, false);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 모루(기획/재화-쓸-곳-1차.md 14장 마을 F1): 강화석·골드·가방 칸·산 대장간 물건 한 줄, [모루 창 열기](보기만이라 시험 판으로 적지 않음),
        /// [강화석 +50](꾸러미), [대장간 물건 되돌리기](산 물건을 비우고 마을 가방 칸을 ForgeShop.BagCapacity로 다시 맞춤. 넘친 장비는 그대로 둔다).
        /// </summary>
        void DrawForge(TownRoot root, CarryData carry)
        {
            Section("모루");
            var inv = root.Inventory;
            string bag = inv ? $" · 가방 {inv.BagCount}/{inv.Capacity}" : "";
            string bought = carry.TownPurchases.Count > 0 ? string.Join(", ", carry.TownPurchases) : "없음";
            GUILayout.Label($"강화석 {carry.Stones} · 골드 {carry.Gold}{bag} · 산 물건 {bought}", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("모루 창 열기", _button)) Do(() => ForgeWindow.Open(), false);
            if (GUILayout.Button("강화석 +50", _button)) Do(() => ProfileCarry.Ensure().Stones += 50);
            if (GUILayout.Button("대장간 물건 되돌리기", _button)) Do(ResetForgeGoods);
            GUILayout.EndHorizontal();
        }

        /// <summary>F1 '대장간 물건 되돌리기'(6-2 아래 '시험용'): 산 물건을 비우고 마을 가방 칸을 기본으로 되돌린다. 쓴 골드는 돌려주지 않는다.</summary>
        static void ResetForgeGoods()
        {
            var carry = ProfileCarry.Ensure();
            carry.TownPurchases.Clear();
            var root = TownRoot.Instance;
            var inv = root ? root.Inventory : Inventory.Instance;
            if (inv) inv.SetCapacity(ForgeShop.BagCapacity(carry));
            DungeonEvents.Say("대장간 물건을 되돌렸다 (시험)");
        }

        /// <summary>
        /// 명패 +1: 아직 받지 않은 명패(FloorRecipe 한 번 받는 것)가 있으면 받은 것으로 적고 도착 처리를 다시 돌려 맡긴다(처음이면 rx:tag.f1).
        /// 남은 명패가 없으면 맡긴 수만 하나 올린다(등불 하나 더 꺼짐).
        /// </summary>
        static void AddNameplate()
        {
            var carry = ProfileCarry.Ensure();
            string next = null;
            foreach (var item in FloorRecipe.AllOnceItems())
            {
                if (item.Kind != FeatureKind.Nameplate || carry.OnceDone.Contains(item.Id)) continue;
                next = item.Id;
                break;
            }
            var root = TownRoot.Instance;
            if (next != null)
            {
                carry.OnceDone.Add(next);
                if (root) root.ReapplyArrival();
                return;
            }
            TownSave.SetTags(carry, TownSave.Tags(carry) + 1);
            if (root) root.RefreshLamps();
        }

        /// <summary>
        /// 버팀 룬 +1: 마을 가방(Inventory) 주머니에 더한다. 마을에서는 가방이 주머니를 들고 있다가 떠날 때 꾸러미에 담으므로(ExportTo)
        /// 꾸러미에 바로 더하면 떠날 때 덮어써진다. 가방이 없으면 꾸러미에 더한다.
        /// </summary>
        static void AddRune()
        {
            var inv = Inventory.Instance;
            int added = inv ? inv.AddRunes(RuneTable.SuperArmorId) : ProfileCarry.Ensure().AddRunes(RuneTable.SuperArmorId);
            int have = inv ? inv.RuneCount(RuneTable.SuperArmorId) : ProfileCarry.Ensure().RuneCount(RuneTable.SuperArmorId);
            DungeonEvents.Say(added > 0 ? "+ " + RuneTable.SuperArmor.Name + " (" + have + "개)" : RuneTable.SuperArmor.Name + " 주머니가 가득 찼다");
        }

        /// <summary>새 프로필로: 창을 닫고 꾸러미·쪽지를 비운 뒤 이 장면을 다시 불러온다(남쪽 길 끝에서 오프닝부터, 1-6).</summary>
        void NewProfile()
        {
            if (DungeonUi.Modal != null) DungeonUi.Close(DungeonUi.Modal);
            ProfileCarry.Clear();
            // 새 프로필을 만든 채 다시 불러온다 — 꾸러미가 비면 처음 화면 규칙(저장·처음 화면·멈춤 창 1차 4-1)에 걸려 오프닝 대신 처음 화면이 뜬다(8-5).
            ProfileCarry.Ensure();
            TownTravel.Clear();
            string path = gameObject.scene.path;
            if (!SceneTravel.Load(string.IsNullOrEmpty(path) ? SceneTravel.TownPath : path))
                Debug.LogWarning("[마을] 새 프로필: 마을 장면을 다시 불러오지 못했다");
        }

        void DrawLight(TownRoot root)
        {
            var lighting = root.Lighting;
            if (!lighting) return;
            Section("빛");
            GUILayout.Label($"전역 빛 {lighting.GlobalIntensity:0.00} (기본 {TownLayout.GlobalLight:0.00}) · 권양기 등불 {lighting.LitLamps}/{TownLayout.LampCount}", _small);
            float v = GUILayout.HorizontalSlider(lighting.GlobalIntensity, TownLayout.GlobalLightMin, TownLayout.GlobalLightMax);
            if (!Mathf.Approximately(v, lighting.GlobalIntensity)) lighting.GlobalIntensity = v;
            GUILayout.Space(10f);
        }
    }
}
