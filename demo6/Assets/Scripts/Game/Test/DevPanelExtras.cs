using System;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.TestStart;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// F1 시험 패널에 더하는 단추(기획/저장-처음화면-멈춤창-1차.md 8-1·8-2, 11-7 'E'). 패널은 Unity 편집기에서만 열린다(DevPanelGate, 그대로).
    /// ① DrawBackToLauncher: 세 패널(마을 TownDebugPanel·던전 ExplorationLog·전투 시험장 CombatHud) 맨 위 '시험 메뉴로 (저장하지 않음)' → GameFlow.GoToTestLauncher
    ///    (저장하는 판 끝·시험 메뉴 판 끝·꾸러미와 도착 쪽지 비움·창 닫기·시험 메뉴 장면 불러오기, 저장하지 않음). F1 단추지만 판을 바꾸지 않으므로 시험 판으로 적지 않는다(8-1).
    /// ② DrawDungeonTools(던전 F1, 8-2): 채우기(체력·물약), 레벨 +1, 골드 +100, 장비 떨구기(부위·등급을 고르고 '발밑에 떨구기'), 계단으로 내려가기(시험).
    ///    이미 있는 강화석 +50·가방 채우기·곡괭이·올라가기·굴로·무기 종류 단추는 ExplorationLog에 그대로 있고 여기서 다시 만들지 않는다.
    ///    판을 바꾸는 일은 모두 TestRunFlag.Note(DevPanel) 뒤에 하고 'DungeonEvents.Say("시험: …")' 한 줄을 남긴다 — 그 판은 더 저장하지 않는다(3-1).
    ///    부위·등급 고르기는 단추 글만 바꾸므로 적지 않는다.
    /// 단추는 그리는 도중에 화면을 바꾸지 않게 할 일(Action)만 돌려주고, 부르는 패널이 다음 Update에서 실행한다(각 패널의 지연 실행 방식).
    /// 할 일은 실행할 때 DungeonRoot.Instance를 다시 찾는다(그 사이 장면이 바뀌었으면 아무것도 하지 않는다).
    /// 고른 부위·등급은 정적 값이라 플레이를 새로 시작할 때(SubsystemRegistration) 무기·일반으로 되돌린다(도메인 다시 불러오기 꺼짐).
    /// </summary>
    public static class DevPanelExtras
    {
        /// <summary>'골드 +100'(8-2).</summary>
        public const int TestGold = 100;
        /// <summary>'발밑에 떨구기' 장비 굴림 흐름 번호(피해 7·처치 보상 23·치명 31·궤짝 41·가방 채우기 67과 겹치지 않음).</summary>
        const ulong DropStream = 71;

        /// <summary>'장비 떨구기'에서 고른 부위(누를 때마다 무기 → 갑옷 → 투구 → 장갑 → 장화 → 반지 → 목걸이).</summary>
        static GearPart _part = GearPart.Weapon;
        /// <summary>'장비 떨구기'에서 고른 등급(누를 때마다 일반 → 고급 → 희귀 → 영웅 → 전설).</summary>
        static Grade _grade = Grade.Common;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _part = GearPart.Weapon;
            _grade = Grade.Common;
        }

        /// <summary>
        /// F1 패널 맨 위 '시험 메뉴로 (저장하지 않음)'(8-1). 누르면 시험 메뉴로 가는 일을, 아니면 null을 돌려준다.
        /// 시험 판 표시는 하지 않는다(가는 길이 저장하는 판을 끝내므로 저장할 일이 없다).
        /// </summary>
        public static Action DrawBackToLauncher(GUIStyle button)
        {
            if (GUILayout.Button("시험 메뉴로 (저장하지 않음)", button ?? GUI.skin.button)) return () => GameFlow.GoToTestLauncher();
            return null;
        }

        /// <summary>
        /// 던전 F1 '시험 도구' 절(8-2). root·root.State가 없으면 아무것도 그리지 않고 null. 누른 단추의 일을 돌려주고(없으면 null) 부르는 패널이 다음 Update에서 실행한다.
        /// 줄 1: 채우기(체력·물약) · 레벨 +1 · 골드 +100. 줄 2: 부위 · 등급 · 발밑에 떨구기. 줄 3: 계단으로 내려가기(시험) — 아래층이나 굴이 없거나 떠날 수 없으면 막는다.
        /// width = 패널 안쪽 너비(한 줄 단추 셋을 나눈다).
        /// </summary>
        public static Action DrawDungeonTools(DungeonRoot root, GUIStyle button, float width)
        {
            if (!root || root.State == null) return null;
            var style = button ?? GUI.skin.button;
            float third = Mathf.Max(60f, (width - 12f) / 3f);
            Action pick = null;

            var player = root.Player;
            var progress = root.Progress;
            string vitals = player && player.Health ? $"체력 {player.Health.Current}/{player.Health.Max} · 물약 {player.Potions}/{player.PotionCapacity}" : "플레이어 없음";
            string level = progress ? $" · 레벨 {progress.Level} ({progress.Xp}/{progress.XpToNext})" : "";
            GUILayout.Label($"{vitals}{level} · 골드 {root.State.Gold}", DungeonUi.Small, GUILayout.MaxWidth(width));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("채우기(체력·물약)", style, GUILayout.Width(third))) pick = FillVitals;
            if (GUILayout.Button("레벨 +1", style, GUILayout.Width(third))) pick = LevelUp;
            if (GUILayout.Button("골드 +" + TestGold, style, GUILayout.Width(third))) pick = AddGold;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("부위: " + GearSlots.Name(_part), style, GUILayout.Width(third))) pick = NextPart;
            if (GUILayout.Button("등급: " + GradeRules.Name(_grade), style, GUILayout.Width(third))) pick = NextGrade;
            if (GUILayout.Button("발밑에 떨구기", style, GUILayout.Width(third))) pick = DropGear;
            GUILayout.EndHorizontal();

            bool was = GUI.enabled;
            bool canDescend = root.CanDescend;
            GUI.enabled = was && canDescend && root.CanLeave;
            string descend = !canDescend ? "계단으로 내려가기(시험) — 아래가 없다" : root.DescendsToDen ? "계단으로 내려가기(시험) — 오우거 굴로" : "계단으로 내려가기(시험)";
            if (GUILayout.Button(descend, style)) pick = DescendForTest;
            GUI.enabled = was;
            return pick;
        }

        /// <summary>판을 바꾸는 시험 단추를 썼다: 이번 판을 '시험 판'(F1 시험 패널)으로 적는다. 그 판은 더 저장하지 않는다(3-1·8-2).</summary>
        static void NoteTestUse() => TestRunFlag.Note(TestRunReason.DevPanel);

        /// <summary>'채우기(체력·물약)': 체력 가득, 물약을 지금 칸 수만큼(PlayerController.RestoreVitals). 쓰러진 동안에는 하지 않는다.</summary>
        static void FillVitals()
        {
            var root = DungeonRoot.Instance;
            var player = root ? root.Player : null;
            if (!player || !player.Health) return;
            if (player.IsDown)
            {
                DungeonEvents.Say("시험: 쓰러진 동안에는 채우지 않는다");
                return;
            }
            NoteTestUse();
            player.RestoreVitals(player.Health.Max, player.PotionCapacity);
            DungeonEvents.Say($"시험: 체력·물약을 채웠다 (체력 {player.Health.Current}/{player.Health.Max} · 물약 {player.Potions}/{player.PotionCapacity})");
        }

        /// <summary>'레벨 +1': 다음 레벨까지 남은 경험치를 준다(PlayerProgress.GrantXp, 보통 레벨업 길 — 연출·스킬 점수 그대로).</summary>
        static void LevelUp()
        {
            var root = DungeonRoot.Instance;
            var progress = root ? root.Progress : null;
            if (!progress) return;
            NoteTestUse();
            int before = progress.Level;
            progress.GrantXp(Mathf.Max(1, progress.XpToNext - progress.Xp));
            DungeonEvents.Say(progress.Level > before ? $"시험: 레벨 {progress.Level}" : $"시험: 경험치를 더했다 (레벨 {progress.Level} 그대로)");
        }

        /// <summary>'골드 +100': 이번 장면 골드(DungeonState.Gold)에 더한다(떠날 때 꾸러미에 담김).</summary>
        static void AddGold()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            NoteTestUse();
            root.State.Gold += TestGold;
            DungeonEvents.Say("시험: 골드 +" + TestGold);
        }

        static void NextPart()
        {
            int i = Array.IndexOf(GearSlots.Parts, _part);
            _part = GearSlots.Parts[(i + 1) % GearSlots.Parts.Length];
        }

        static void NextGrade() => _grade = (Grade)(((int)_grade + 1) % GradeRules.Count);

        /// <summary>
        /// '발밑에 떨구기': 고른 부위·등급의 장비 하나를 플레이어가 바라보는 쪽 발치에 떨군다(LootSpawner.SpawnGear).
        /// 재료는 ExplorationLog.CommonGear·LootRules.RollGear와 같다: 아이템 레벨 = 지금 층(FloorScaling.Clamp), 종류 = 그 층에 풀린 종류 가운데 하나(없으면 그 부위 전체),
        /// 굴림 900~1100‰, 옵션 OptionTable.RollAll(부위, 등급, 층), 강화 +0. 전설이면 그 부위에 나올 수 있는 효과(LegendaryTable.EffectsOn) 가운데 하나와 세기
        /// LegendaryTable.RollStrength(처음 가진 효과로 굴림). 효과가 없는 부위면 알림만 남기고 떨구지 않는다.
        /// 난수 = Pcg32Random(지금 시각, DropStream) — 누를 때마다 다른 장비.
        /// </summary>
        static void DropGear()
        {
            var root = DungeonRoot.Instance;
            var player = root ? root.Player : null;
            if (!player) return;
            var part = _part;
            var grade = _grade;
            var rng = new Demo6.Core.Random.Pcg32Random((ulong)DateTime.UtcNow.Ticks, DropStream);

            string legendId = null;
            int legendRoll = 0;
            if (grade == Grade.Legendary)
            {
                var effects = LegendaryTable.EffectsOn(part);
                if (effects.Count == 0)
                {
                    DungeonEvents.Say("시험: 이 부위에는 전설 효과가 없다");
                    return;
                }
                var effect = effects[rng.NextInt(0, effects.Count)];
                legendId = LegendaryTable.Get(effect).Id;
                legendRoll = LegendaryTable.RollStrength(false, rng);
            }

            int floor = FloorScaling.Clamp(root.Floor);
            var kinds = GearBaseTable.ForPart(part, floor);
            if (kinds.Count == 0) kinds = GearBaseTable.ForPart(part);
            if (kinds.Count == 0)
            {
                DungeonEvents.Say("시험: 이 부위의 장비 종류가 없다");
                return;
            }
            NoteTestUse();
            var kind = kinds[rng.NextInt(0, kinds.Count)];
            int roll = rng.NextInt(GearMath.RollMinPermille, GearMath.RollMaxPermille + 1);
            var options = OptionTable.RollAll(part, grade, floor, rng);
            var item = new GearItem(kind.Id, grade, floor, roll, 0, options, legendId, legendRoll);
            var facing = player.FacingDirection;
            if (facing.sqrMagnitude < 0.0001f) facing = Vector2.down;
            LootSpawner.SpawnGear(item, player.Position, facing, 0f);
            string legend = legendId != null ? $" · {LegendaryTable.Get(legendId).Name} {legendRoll}‰" : "";
            DungeonEvents.Say($"시험: {GradeRules.Name(grade)} {GearSlots.Name(part)} {kind.Name}을(를) 떨궜다 (iLv {floor}{legend})");
        }

        /// <summary>'계단으로 내려가기(시험)': 계단을 쓴 것처럼 아래층(이나 굴)으로 간다(DungeonRoot.Descend). 아래가 없거나 떠날 수 없으면 알림만.</summary>
        static void DescendForTest()
        {
            var root = DungeonRoot.Instance;
            if (!root) return;
            if (!root.CanDescend || !root.CanLeave)
            {
                DungeonEvents.Say("시험: 지금은 계단으로 내려갈 수 없다");
                return;
            }
            NoteTestUse();
            DungeonEvents.Say(root.DescendsToDen ? "시험: 계단으로 오우거 굴에 내려간다" : "시험: 계단으로 한 층 내려간다");
            root.Descend();
        }
    }
}
