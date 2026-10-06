using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Demo5.FrontEnd
{
    public sealed partial class ExpeditionBattlePanel : MonoBehaviour
    {
        [Serializable] public sealed class PawnBounds { public Sprite Sprite; public Rect Pixels; }
        public PawnBounds[] VisibleBounds;
        public GameObject View, Result, RetreatReview;
        public CanvasGroup Workspace;
        public RectTransform Frame, TurnContent;
        public BattleTurnCard TurnPrefab;
        public BattleBoardCell[] Cells;
        public Button[] CellButtons;
        public Button Melee, Shoot, Guard, Items, Retreat, Execute, ResultContinue, RetreatCancel, RetreatConfirm;
        public Text Place, Clock, Round, ActorName, ActorInfo, TargetName, TargetInfo, Chance, Hint, Feedback, ExecuteLabel;
        public Text ResultTitle, ResultBody, ResultContinueLabel;
        public Image ActorPortrait, TargetPortrait, ActorHealth, TargetHealth;
        public Sprite EnemyPortrait, EnemyBody;
        public Sprite BattleBackground;
        public GameObject PawnPrefab;
        [Header("전투 규칙 · 임시 수치")]
        public FieldBattleRules Rules = new FieldBattleRules();
        [Header("크리쳐 · 종별 수치·그림·손맛 (Assets/Data/BattleCreatures)")]
        public BattleCreatureRoster Creatures;
        public Color DangerCell = new Color(.95f, .3f, .24f, .34f);
        [Min(0)] public float DangerPulse = 1.2f;
        // Tests and review captures pick the next lineup. An empty list fights the rules' 감염자 (no creature data).
        [NonSerialized] public IList<BattleCreature> NextLineup;
        [Header("차례 구분 · 우리 차례 / 적의 차례")]
        public BattlePhaseBanner PhaseBanner;
        public string AllyPhaseTitle = "우리 차례", EnemyPhaseTitle = "적의 차례";
        [Tooltip("{0}에 먼저 움직이는 대원 이름")]
        public string AllyPhaseDetail = "{0}부터 · 이동 1회와 행동 1회";
        [Tooltip("먼저 움직일 대원이 묶였을 때")]
        public string AllyPhaseBoundDetail = "{0}부터 · 발이 묶여 행동 1회만";
        [Tooltip("{0}에 움직일 적 이름")]
        public string EnemyPhaseDetail = "{0} 움직임";
        public Color AllyPhaseColor = new Color(.56f, .84f, .7f), EnemyPhaseColor = new Color(.93f, .47f, .38f);
        [Tooltip("적의 다음 공격 대상을 항상 선으로 잇는다. 끄면 적이나 대원 위에 포인터를 올렸을 때만 잇고, 조준 중에는 잇지 않는다.")]
        public bool ThreatLines;
        [Header("전투 손맛 · 연출과 머리 위 정보")]
        public BattlePresentation Presentation;
        public RectTransform HudLayer, FxLayer, NoiseGauge;
        public BattlePawnHud HudPrefab;
        public BattleThreatLine ThreatPrefab;
        public Text NoiseValue, RetreatBody;
        public Image NoiseFill;
        public Color AllyFill = new Color(.56f, .76f, .6f), EnemyFill = new Color(.86f, .42f, .33f), NoiseColor = new Color(1, .78f, .38f);
        [Min(0)] public float ResultDelay = .5f;
        [Header("문구 · {0}{1}에 규칙 수치, {2}에 물러날 방")]
        public string MeleeDescription = "전열 · 피해 {0}";
        public string ShootDescription = "피해 {0} · 소음 +{1}";
        [TextArea(2, 3)][Tooltip("{0} 물릴 확률 감소(%p), {1} 예고 공격 피해 감소 · 카드 설명은 두 줄까지")] public string GuardDescription = "물릴 확률 -{0}%p\n예고 피해 -{1}";
        public string RetreatCost = "{2} 1턴";
        [TextArea(3, 6)] public string RetreatThreatBody = "{2}로 이동 · 원정대 전체 1턴\n\n등을 보이면 적 {0}마리가 한 번씩 덤빕니다.\n명중 {1}% · 피해 {3} · 쓰러지지는 않습니다.\n가방과 수색 진행도는 잃지 않습니다.";
        [TextArea(3, 6)] public string RetreatQuietBody = "{2}로 이동 · 원정대 전체 1턴\n\n붙어 있는 적이 없어 조용히 물러납니다.\n가방과 수색 진행도는 잃지 않습니다.";
        [Header("문구 · 출구(오락실)에서 물러나 거점으로 귀환 · {0}{1}{3}은 위와 같음")]
        public string RetreatHomeCost = "출구 · 귀환";
        [Tooltip("결과 창 계속 버튼")]
        public string RetreatHomeLabel = "거점으로 귀환";
        [Tooltip("물러나기 확인창의 확정 버튼 · 방 이동 / 거점 귀환")]
        public string RetreatConfirmLabel = "철수 · 1턴", RetreatHomeConfirmLabel = "귀환";
        [TextArea(3, 6)] public string RetreatHomeThreatBody = "출구로 빠져나가 거점으로 귀환합니다\n\n등을 보이면 적 {0}마리가 한 번씩 덤빕니다.\n명중 {1}% · 피해 {3} · 쓰러지지는 않습니다.\n챙긴 물건은 그대로 가져갑니다.";
        [TextArea(3, 6)] public string RetreatHomeQuietBody = "출구로 빠져나가 거점으로 귀환합니다\n\n붙어 있는 적이 없어 조용히 빠져나갑니다.\n챙긴 물건은 그대로 가져갑니다.";
        public FieldBattleState State { get; private set; }
        public bool IsOpen => View.activeSelf;
        public bool Busy { get; private set; }
        public int SelectedTarget { get; private set; } = -1;
        public bool Ranged { get; private set; }
        public float ActionPause = .3f;

        ExpeditionArrivalPanel arrival;
        ExpeditionEncounterPanel encounter;
        readonly List<BattlePawnView> views = new List<BattlePawnView>();
        readonly List<BattlePawnHud> huds = new List<BattlePawnHud>();
        readonly List<BattleTurnCard> cards = new List<BattleTurnCard>();
        readonly List<BattleThreatLine> threatLines = new List<BattleThreatLine>();
        readonly List<int> shown = new List<int>();
        readonly List<bool> down = new List<bool>();
        readonly Dictionary<Adventurer, int> startingHealth = new Dictionary<Adventurer, int>();
        List<EnemyIntent> intents = new List<EnemyIntent>();
        GameObject world;
        Transform stage;
        Camera canvasCamera;
        Sprite previousBackground;
        Vector3 previousBackgroundScale, backgroundHome;
        readonly List<Mesh> gridMeshes = new List<Mesh>();
        Material gridMaterial;
        bool layoutDirty, tintDirty, settled;
        Vector2 lastScreen;
        Vector3 lastFrameScale;
        int hover = -1, armed = -1, armedActor = -1, focusTarget = -1, focusChance, focusDamage, displayActor, displayRound = 1;
        bool focusCommitted;
        readonly HashSet<int> dangerCells = new HashSet<int>();
        readonly Color[] baseTints = new Color[18], edgeTints = new Color[18];
        readonly bool[] edgeWide = new bool[18];
        readonly List<(int enemy, CreatureAttack attack, BattleHit hit)> liveThreats = new List<(int, CreatureAttack, BattleHit)>();
        int linkEnemy = -1, linkAlly = -1;
        float blinkUntil;
        // Target card as it stood when the action was chosen, so the card does not spoil the roll before the hit lands.
        int cardTarget = -1, cardChance, cardDamage; bool cardFrozen;
        string feedbackOverride;
        static readonly Color Gold = new Color(1, .78f, .38f), Red = new Color(.83f, .43f, .34f), AllyBase = new Color(.64f, .79f, .74f), GuardBase = new Color(.72f, .9f, .74f);

        public void Initialize(ExpeditionArrivalPanel a, ExpeditionEncounterPanel e)
        {
            arrival = a; encounter = e; View.SetActive(false); Result.SetActive(false); RetreatReview.SetActive(false);
            Melee.onClick.AddListener(() => SelectAction(false)); Shoot.onClick.AddListener(() => SelectAction(true));
            Execute.onClick.AddListener(Attack); Guard.onClick.AddListener(Defend);
            Retreat.onClick.AddListener(AskRetreat); RetreatCancel.onClick.AddListener(CancelRetreat);
            RetreatConfirm.onClick.AddListener(ConfirmRetreat); ResultContinue.onClick.AddListener(Continue);
            Items.onClick.AddListener(OpenItems);
            if (Drawer) { Drawer.Use.onClick.AddListener(UseSelectedItem); Drawer.Cancel.onClick.AddListener(CloseItems); }
            SetItemLayout(false); HideAim();
            for (int i = 0; i < CellButtons.Length; i++) { int index = i; CellButtons[i].onClick.AddListener(() => ClickCell(index)); }
            for (int i = 0; i < Cells.Length; i++) { var h = Cells[i].GetComponent<BattleCellHover>(); if (!h) h = Cells[i].gameObject.AddComponent<BattleCellHover>(); h.Bind(this, i); }
            if (!RetreatBody) RetreatBody = RetreatReview.transform.Find("Body")?.GetComponent<Text>();
            Describe(Melee, "Description", string.Format(MeleeDescription, Rules.MeleeDamage)); Describe(Shoot, "Description", string.Format(ShootDescription, Rules.ShotDamage, Rules.ShotNoise));
            Describe(Guard, "Description", string.Format(GuardDescription, Rules.GuardHitPenalty, Rules.GuardStrikeReduction)); Describe(Items, "Description", ItemsDescription);
        }
        static void Describe(Button b, string child, string text) { var d = b.transform.Find(child)?.GetComponent<Text>(); if (d) d.text = text; }
        string RetreatRoom => arrival && arrival.Rooms ? arrival.Rooms.RetreatRoomName : "복도";
        // In the first room (오락실) the way back is the exit: a retreat ends the expedition (FinishReturn), like the encounter's own retreat.
        public bool RetreatsHome => arrival && arrival.Rooms && arrival.Rooms.CurrentRoom == 0;
        // Travel minutes the home return spends (the site's one-way time), or -1 when the destination cannot be found.
        int HomeMinutes
        {
            get
            {
                var owner = arrival ? arrival.GetComponentInParent<SettlementController>(true) : null;
                if (!owner || !owner.ExpeditionPanel || owner.ExpeditionPanel.Destinations == null || owner.Campaign == null) return -1;
                var site = owner.ExpeditionPanel.Destinations.FirstOrDefault(d => d != null && d.Id == owner.Campaign.FieldDestination);
                return site != null ? site.OneWayMinutes : -1;
            }
        }
        // Encounters draw distinct creature types from the roster; reinforcements come from the same pool.
        public void Begin(int enemyCount)
        {
            var lineup = NextLineup; NextLineup = null;
            if (lineup == null && Creatures) lineup = Creatures.Lineup(Mathf.Clamp(enemyCount, 1, 3), () => UnityEngine.Random.Range(0, 100));
            Begin(lineup, enemyCount);
        }
        void Begin(IList<BattleCreature> lineup, int enemyCount)
        {
            if (IsOpen || !encounter.IsOpen || !arrival.Participants.Any(p => p.Health > 0)) return;
            bool infected = lineup == null || lineup.Count == 0;
            var battleRules = Rules;
            if (!infected && lineup.Count == 1 && lineup[0] != null && lineup[0].SuppressReinforcements)
            {
                // The management-room resident stays a single creature; never change the prefab's shared rules.
                battleRules = JsonUtility.FromJson<FieldBattleRules>(JsonUtility.ToJson(Rules));
                battleRules.MaxReinforcements = 0;
            }
            State = new FieldBattleState(arrival.Participants, enemyCount, p => arrival.Inventory.CountFor(p, "ammo"),
                p => arrival.Inventory.TransferField(p, "ammo", 1, false), () => UnityEngine.Random.Range(0, 100), battleRules,
                infected ? null : lineup, infected || !Creatures ? null : Creatures.Pool.ToList());
            startingHealth.Clear(); foreach (var p in arrival.Participants) startingHealth[p] = p.Health;
            Result.GetComponent<BattleResultSummary>()?.Capture(arrival);
            settled = Busy = Ranged = false; hover = armed = focusTarget = -1; feedbackOverride = null; displayActor = State.Actor; displayRound = State.Round; linkEnemy = linkAlly = -1;
            aiming = itemMode = false; aimTarget = itemTarget = -1; pan = panTarget = 0; SetItemLayout(false); HideAim(); dangerCells.Clear(); blinkUntil = 0; ResetLesson();
            SelectedTarget = State.Units.FindIndex(u => u.Enemy);
            arrival.Main.gameObject.SetActive(false); arrival.PawnRoot.gameObject.SetActive(false);
            encounter.Workspace.gameObject.SetActive(false);
            View.SetActive(true); Result.SetActive(false); RetreatReview.SetActive(false); Workspace.interactable = Workspace.blocksRaycasts = true;
            canvasCamera = Frame.GetComponentInParent<Canvas>().worldCamera;
            world = new GameObject("BattlePawns"); world.transform.SetParent(arrival.World.transform, false);
            // Screen shake moves this stage and the background together; layout math stays in world space.
            stage = new GameObject("Stage").transform; stage.SetParent(world.transform, false);
            var background = arrival.Rooms.Background.transform;
            previousBackground = arrival.Rooms.Background.sprite; previousBackgroundScale = background.localScale; backgroundHome = background.localPosition;
            if (BattleBackground)
            {
                arrival.Rooms.Background.sprite = BattleBackground;
                background.localScale = new Vector3(19.2f / BattleBackground.bounds.size.x, 10.8f / BattleBackground.bounds.size.y, 1);
            }
            gridMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            gridMaterial.mainTexture = Texture2D.whiteTexture;
            foreach (var cell in Cells)
            {
                var grid = new GameObject(cell.name + "Floor", typeof(MeshFilter), typeof(MeshRenderer)); grid.transform.SetParent(stage, false);
                var mesh = new Mesh { name = cell.name }; gridMeshes.Add(mesh); grid.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = grid.GetComponent<MeshRenderer>(); renderer.sharedMaterial = gridMaterial; renderer.sortingOrder = 15;
            }
            if (Presentation) Presentation.Bind(this);
            for (int i = 0; i < State.Units.Count; i++) SpawnPawn(i);
            Place.text = arrival.Place.text; Clock.text = arrival.Clock.text;
            Describe(Retreat, "Cost", RetreatsHome ? RetreatHomeCost : string.Format(RetreatCost, 0, 0, RetreatRoom));
            layoutDirty = true; Refresh();
            if (Presentation) Presentation.TurnStart(State.Actor);
            ShowPhase(false, false);
        }
        // Announces a phase change. Blocking while the enemies take over; the player's own phase never waits on it.
        Coroutine phaseRoutine;
        IEnumerator PhaseBannerRoutine(bool enemy)
        {
            if (!PhaseBanner) yield break;
            string names = enemy ? string.Join(" · ", State.Units.Where(u => u.Enemy && u.Alive).Select(u => u.Name)) : State.Current.Name;
            float speed = Presentation ? Presentation.Speed : 1;
            if (Presentation) Presentation.PhaseSound(enemy);
            string detail = enemy ? EnemyPhaseDetail : lessonActive && State.Current.Bound == 0 ? AllyPhaseLessonDetail : State.Current.Bound > 0 ? AllyPhaseBoundDetail : AllyPhaseDetail;
            yield return PhaseBanner.Play(enemy, enemy ? EnemyPhaseTitle : AllyPhaseTitle, string.Format(detail, names), speed);
        }
        void ShowPhase(bool enemy, bool wait)
        {
            if (!PhaseBanner || State == null) return;
            if (phaseRoutine != null) StopCoroutine(phaseRoutine);
            if (!wait) phaseRoutine = StartCoroutine(PhaseBannerRoutine(enemy));
        }
        // Review fixture: restart the open battle with another lineup in the same encounter (creature captures and tests).
        public void Restage(IList<BattleCreature> lineup) { if (!IsOpen) return; Cleanup(); Begin(lineup ?? new List<BattleCreature>(), Mathf.Max(1, lineup?.Count ?? 1)); }
        // Review fixture: after a test places units in State directly, bring standees and overlays back in step.
        public void ReviewSync() { Sync(); layoutDirty = true; foreach (var v in views) if (v) v.Offset = Vector3.zero; Refresh(); }
        public BattlePawnView SpawnPawn(int index)
        {
            var unit = State.Units[index];
            int member = unit.Enemy ? -1 : arrival.Participants.ToList().IndexOf(unit.Person);
            var partyPawn = member >= 0 && member < arrival.PartyPawns.Count ? arrival.PartyPawns[member] : null;
            var partyBody = partyPawn ? partyPawn.transform.Find("Body")?.GetComponent<SpriteRenderer>() : null;
            if (!unit.Enemy && (!partyBody || !partyBody.sprite))
                throw new InvalidOperationException("전투 대원의 현장 말을 찾을 수 없습니다: " + unit.Name);
            var pawn = Instantiate(PawnPrefab, stage); pawn.name = unit.Name;
            var body = pawn.transform.Find("Body").GetComponent<SpriteRenderer>();
            var creature = unit.Creature; bool drawn = creature != null && creature.Body;
            body.sprite = unit.Enemy ? (drawn ? creature.Body : EnemyBody) : partyBody.sprite;
            var pixels = drawn && creature.Visible.height > 0 ? creature.Visible
                : VisibleBounds?.FirstOrDefault(v => v.Sprite == body.sprite)?.Pixels ?? new Rect(0, 0, body.sprite.rect.width, body.sprite.rect.height);
            float bodyHeight = drawn ? creature.Height : 1.72f, hover = drawn ? creature.Hover : 0;
            var roster = !unit.Enemy && arrival ? arrival.GetComponentInParent<SettlementController>(true)?.Roster : null;
            if (roster) bodyHeight *= roster.BodyScaleFor(body.sprite);
            float scale = bodyHeight * body.sprite.pixelsPerUnit / pixels.height;
            body.transform.localScale = Vector3.one * scale;
            body.transform.localPosition = new Vector3((body.sprite.pivot.x - pixels.center.x) * scale / body.sprite.pixelsPerUnit,
                (body.sprite.pivot.y - pixels.y) * scale / body.sprite.pixelsPerUnit + .035f, 0);
            var view = pawn.AddComponent<BattlePawnView>(); view.Unit = index; view.Enemy = unit.Enemy; view.Depth = unit.Depth; view.Lane = unit.Lane; view.Hover = hover;
            view.Setup(Presentation ? Presentation.FlashMaterial : null, bodyHeight + .035f + hover); view.SetSorting(unit.Lane);
            view.Home = CellHome(unit.Enemy, unit.Depth, unit.Lane); view.BaseTint = unit.Enemy ? Red : AllyBase;
            while (views.Count <= index) { views.Add(null); huds.Add(null); cards.Add(null); shown.Add(0); down.Add(false); }
            views[index] = view; shown[index] = unit.Health; down[index] = false;
            if (HudPrefab && HudLayer)
            {
                var hud = Instantiate(HudPrefab, HudLayer); hud.name = unit.Name + " HUD"; hud.gameObject.SetActive(true);
                hud.Setup(unit.Name, unit.Maximum, unit.Health, unit.Enemy ? EnemyFill : AllyFill); hud.SetRole(unit.Enemy, unit.Creature); huds[index] = hud;
            }
            var card = Instantiate(TurnPrefab, TurnContent); card.Portrait.sprite = Portrait(unit);
            card.Label.text = unit.Enemy ? (unit.Creature != null ? unit.Creature.Name : "적") : unit.Name; card.SetRole(unit.Enemy, unit.Creature); cards[index] = card;
            return view;
        }
        Sprite Portrait(FieldBattleState.Unit u) => u.Enemy ? (u.Creature != null && u.Creature.Body ? u.Creature.Body : EnemyPortrait) : arrival.Cards[arrival.Participants.ToList().IndexOf(u.Person)].Portrait.sprite;

        // ---- queries used by BattlePresentation ----
        public BattlePawnView PawnView(int unit) => unit >= 0 && unit < views.Count ? views[unit] : null;
        public Vector3 CellHome(bool enemy, int depth, int lane) => LocalFromCanvas(Point(enemy, depth, lane));
        public Vector2 FxPoint(Vector3 worldPosition) => LayerPoint(FxLayer, worldPosition);
        public Vector2 FxPoint(RectTransform rect) => LayerPoint(FxLayer, rect.TransformPoint(rect.rect.center));
        public Vector3 CellWorld(bool enemy, int depth, int lane) => world.transform.TransformPoint(CellHome(enemy, depth, lane));
        public Vector3 LocalToWorld(Vector3 local) => world.transform.TransformPoint(local);
        // Freshly marked cells flash brighter for a moment so the player sees what the wind-up just claimed.
        public void BlinkCells(IEnumerable<int> cells) { foreach (int c in cells) dangerCells.Add(c); blinkUntil = Time.unscaledTime + .7f / (Presentation ? Mathf.Max(.1f, Presentation.Speed) : 1); tintDirty = true; }
        public void ShowHealth(int unit, int health, bool animate)
        {
            if (unit < 0 || unit >= shown.Count) return;
            shown[unit] = health; if (huds[unit]) huds[unit].SetHealth(health, animate); Refresh();
        }
        // Result text waits for the hit to land instead of spoiling it when the button is pressed.
        public void RevealOutcome() { threatFrozen = false; if (feedbackOverride == null) return; feedbackOverride = null; if (Feedback && State != null) Feedback.text = State.Message; }
        public void PawnDown(int unit)
        {
            if (unit < 0 || unit >= down.Count) return;
            down[unit] = true; if (huds[unit]) huds[unit].gameObject.SetActive(false); if (cards[unit]) cards[unit].gameObject.SetActive(false);
        }
        // Hovering an empty ally cell previews how the infected would answer that move.
        public void Hover(int cell, bool enter)
        {
            if (!enter && hover != cell) return;
            hover = enter ? cell : -1; if (CanInput) Refresh();
        }

        // ---- input ----
        // Picking 근접/사격 starts aiming; picking the same card again puts the arrow away.
        public void SelectAction(bool ranged)
        {
            if (!CanInput) return;
            if (aiming && Ranged == ranged) StopAim(); else { Ranged = ranged; aiming = true; aimTarget = -1; }
            Refresh();
        }
        bool CanInput => IsOpen && State != null && State.PlayerTurn && !Busy && !itemMode && !Result.activeSelf && !RetreatReview.activeSelf;
        public void ClickCell(int index)
        {
            if (!CanInput || index < 0 || index >= 18) return;
            int side = index / 9, lane = index % 9 / 3, depth = index % 3;
            if (side == 1)
            {
                int target = State.Units.FindIndex(u => u.Enemy && u.Alive && u.Depth == depth && u.Lane == lane);
                if (target < 0) return;
                if (aiming) { ConfirmAim(aimTarget >= 0 ? aimTarget : target); return; }
                // A second tap on the enemy the player already picked confirms the attack.
                if (target == armed && armedActor == State.Actor && target == SelectedTarget && State.CanAttack(target, Ranged)) { Attack(); return; }
                SelectedTarget = armed = target; armedActor = State.Actor; if (Presentation) Presentation.Select(target); Refresh(); return;
            }
            int actor = State.Actor; FreezeCard();
            if (State.Move(depth, lane)) { hover = -1; StartCoroutine(Run(actor)); } else Unfreeze();
        }
        // The marks and tags as they stood when the action was chosen: a kill must not wipe its victim's marks before the blow lands.
        readonly List<int> frozenDanger = new List<int>();
        readonly Dictionary<int, EnemyIntent> frozenTags = new Dictionary<int, EnemyIntent>();
        readonly List<(int enemy, CreatureAttack attack, BattleHit hit)> frozenThreats = new List<(int, CreatureAttack, BattleHit)>();
        bool threatFrozen;
        void FreezeCard()
        {
            cardTarget = SelectedTarget; cardChance = State.HitChance(SelectedTarget, Ranged); cardDamage = SelectedTarget >= 0 && SelectedTarget < State.Units.Count ? State.ExpectedDamage(SelectedTarget, Ranged) : State.DamageFor(Ranged); cardFrozen = true;
            frozenDanger.Clear(); frozenDanger.AddRange(State.DangerCells()); frozenTags.Clear(); frozenThreats.Clear();
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i]; if (!u.Enemy || !u.Alive || (u.Pending == null && u.Stagger == 0)) continue;
                frozenTags[i] = new EnemyIntent { Kind = u.Stagger > 0 ? EnemyIntentKind.Recover : EnemyIntentKind.Strike, Enemy = i, Attack = u.Pending?.Attack ?? CreatureAttack.Bite };
                foreach (var h in State.PendingHits(i)) frozenThreats.Add((i, u.Pending.Attack, h));
            }
            threatFrozen = true;
        }
        void Unfreeze() { cardFrozen = false; threatFrozen = false; }
        void Attack() { if (!CanInput) return; int actor = State.Actor; FreezeCard(); if (State.Attack(SelectedTarget, Ranged)) { armed = -1; StartCoroutine(Run(actor)); } else Unfreeze(); }
        void Defend() { if (!CanInput) return; StopAim(); int actor = State.Actor; FreezeCard(); if (State.Guard()) StartCoroutine(Run(actor)); else Unfreeze(); }
        IEnumerator Run(int actor)
        {
            bool turnEnds = State.Events.Any(e => e.Kind != BattleEventKind.Move);
            if (turnEnds) StopAim();
            // The party's action never wraps the round, so the round on display is still the one being played.
            Busy = true; hover = -1; displayActor = actor; displayRound = State.Round; feedbackOverride = Announce(State.Events.FirstOrDefault()); Refresh();
            yield return Replay();
            feedbackOverride = null; threatFrozen = false;
            bool enemyPhase = State.Outcome == FieldBattleOutcome.Playing && State.Current.Enemy;
            if (enemyPhase && PhaseBanner)
            {
                if (phaseRoutine != null) { StopCoroutine(phaseRoutine); phaseRoutine = null; }
                displayActor = State.Actor; Refresh(); yield return PhaseBannerRoutine(true);
            }
            while (State.Outcome == FieldBattleOutcome.Playing && State.Current.Enemy)
            {
                displayActor = State.Actor; int actingRound = State.Round;
                var plan = State.PredictIntents().FirstOrDefault(p => p.Enemy == State.Actor);
                focusTarget = (plan.Kind == EnemyIntentKind.Attack || plan.Kind == EnemyIntentKind.Strike) && plan.HitCount > 0 ? plan.Hits[0].Target : -1;
                focusChance = plan.Chance; focusDamage = plan.HitCount > 0 ? plan.Hits[0].Damage : plan.Damage; focusCommitted = plan.Kind == EnemyIntentKind.Strike;
                feedbackOverride = State.Current.Name + "의 차례"; Refresh();
                yield return Pause(ActionPause);
                State.EnemyStep(); displayRound = actingRound;
                yield return Replay();
                yield return LessonAfterEnemyStep();
                feedbackOverride = null; displayRound = State.Round; Refresh();
            }
            focusTarget = -1; feedbackOverride = null; Sync();
            if (State.Outcome != FieldBattleOutcome.Playing) { yield return Pause(ResultDelay); ShowResult(); Busy = false; cardFrozen = false; yield break; }
            Busy = false; cardFrozen = false; displayActor = State.Actor;
            if (SelectedTarget < 0 || !State.Units[SelectedTarget].Alive) { SelectedTarget = State.Units.FindIndex(u => u.Enemy && u.Alive); armed = -1; }
            Refresh();
            if (turnEnds && Presentation) Presentation.TurnStart(State.Actor);
            if (enemyPhase) ShowPhase(false, false);
        }
        IEnumerator Replay()
        {
            var events = State.Events.ToList();
            if (Presentation) yield return Presentation.Play(events);
            else foreach (var e in events.Where(e => e.Kind == BattleEventKind.Reinforce)) SpawnPawn(e.Actor);
            Sync();
        }
        IEnumerator Pause(float seconds)
        {
            float end = Time.unscaledTime + seconds / (Presentation ? Mathf.Max(.1f, Presentation.Speed) : 1);
            while (Time.unscaledTime < end) yield return null;
        }
        // Display values follow the presentation; after every replay they must equal the rules state.
        void Sync()
        {
            for (int i = 0; i < State.Units.Count && i < views.Count; i++)
            {
                var u = State.Units[i]; if (!views[i]) continue;
                if (shown[i] != u.Health) { shown[i] = u.Health; if (huds[i]) huds[i].SetHealth(u.Health, false); }
                if (views[i].Depth != u.Depth || views[i].Lane != u.Lane) { views[i].Depth = u.Depth; views[i].Lane = u.Lane; views[i].SetSorting(u.Lane); layoutDirty = true; }
                if (!u.Alive && !down[i]) PawnDown(i);
                if (!u.Alive && (!Presentation || views[i].Alpha <= .01f)) views[i].gameObject.SetActive(false);
            }
        }
        void AskRetreat()
        {
            if (!CanInput) return; StopAim();
            var threats = State.RetreatThreats();
            bool home = RetreatsHome;
            if (RetreatBody) RetreatBody.text = string.Format(threats.Count == 0 ? (home ? RetreatHomeQuietBody : RetreatQuietBody) : (home ? RetreatHomeThreatBody : RetreatThreatBody), threats.Count, Rules.RetreatHitChance, RetreatRoom, Rules.EnemyDamage);
            var confirmLabel = RetreatConfirm ? RetreatConfirm.GetComponentInChildren<Text>(true) : null;
            if (confirmLabel) confirmLabel.text = home ? RetreatHomeConfirmLabel : RetreatConfirmLabel;
            RetreatReview.SetActive(true); Workspace.interactable = Workspace.blocksRaycasts = false;
        }
        public void CancelRetreat()
        {
            RetreatReview.SetActive(false); Workspace.interactable = Workspace.blocksRaycasts = true;
        }
        void ConfirmRetreat()
        {
            if (!IsOpen || !RetreatReview.activeSelf || Busy || !State.PlayerTurn) return;
            CancelRetreat(); displayActor = State.Actor; feedbackOverride = "원정대가 등을 보이며 물러납니다"; State.Retreat(); StartCoroutine(RunRetreat());
        }
        IEnumerator RunRetreat()
        {
            Busy = true; Refresh();
            yield return Replay();
            if (Presentation) yield return Presentation.Withdraw(Enumerable.Range(0, State.Units.Count).Where(i => !State.Units[i].Enemy && State.Units[i].Alive));
            feedbackOverride = null; Sync(); ShowResult(); Busy = false;
        }
        public void Escape()
        {
            if (RetreatReview.activeSelf) CancelRetreat();
            else if (itemMode) CloseItems();
            else if (aiming) { StopAim(); Refresh(); }
        }
        void ShowResult()
        {
            Result.SetActive(true); Workspace.interactable = Workspace.blocksRaycasts = false;
            var o = State.Outcome;
            // A retreat from the first room goes out through the exit and home (EncounterPanel.FinishBattle -> FinishReturn): travel time, no exploration turn.
            bool home = o == FieldBattleOutcome.Retreated && RetreatsHome; int minutes = home ? HomeMinutes : -1;
            ResultTitle.text = o == FieldBattleOutcome.Victory ? "주변이 조용해졌습니다" : o == FieldBattleOutcome.Retreated ? "교전 중단" : "원정대 행동 불능";
            var members = arrival.Participants.Select(p =>
            {
                int before = startingHealth[p], change = p.Health - before;
                return p.Name + "  체력 " + before + " → " + p.Health + " / " + p.MaxHealth + (change < 0 ? "  <color=#D9826A>(" + change + ")</color>" : "");
            });
            // Large parties drop the blank spacer lines so every member fits the fixed result box.
            string gap = arrival.Participants.Count > 3 ? "\n" : "\n\n";
            ResultBody.text = "전투 " + State.Round + "라운드  ·  처치 " + State.Kills + "  ·  사격 " + State.AmmoSpent + "발" + (State.ItemsUsed > 0 ? "  ·  치료 " + State.ItemsUsed + "회" : "") + gap
                + string.Join("\n", members) + gap
                + (o == FieldBattleOutcome.Defeat ? "돌아갈 수 있는 대원이 없습니다.\n이번 원정을 더 진행할 수 없습니다."
                : home ? RetreatHomeLabel + (minutes >= 0 ? " · 이동 " + minutes + "분" : "") + "\n챙긴 물건은 그대로 가져갑니다."
                : o == FieldBattleOutcome.Retreated ? RetreatRoom + "로 이동 · 탐험 1턴" + (State.ResultNoise > 0 ? " · 소음 +" + State.ResultNoise : "") + "\n가방과 기존 수색도는 유지됩니다."
                : "교전 처리 · 탐험 1턴 · 소음 +" + State.ResultNoise + (State.AmmoSpent > 0 ? " (총성 포함)" : "") + "\n발견물과 수색도를 유지하고 이어갑니다.");
            Result.GetComponent<BattleResultSummary>()?.Show(this, arrival, startingHealth, RetreatRoom, home, minutes);
            ResultContinueLabel.text = o == FieldBattleOutcome.Defeat ? "시작 화면으로" : home ? RetreatHomeLabel : o == FieldBattleOutcome.Retreated ? RetreatRoom + "로 물러나기" : "수색으로 돌아가기";
        }
        public void Continue()
        {
            if (!IsOpen || !Result.activeSelf || settled) return;
            settled = true; var outcome = State.Outcome; int noise = State.ResultNoise;
            if (outcome == FieldBattleOutcome.Defeat) { SceneManager.LoadScene("StartMenu"); return; }
            Cleanup(); arrival.Main.gameObject.SetActive(true); arrival.PawnRoot.gameObject.SetActive(true);
            encounter.Workspace.gameObject.SetActive(true); arrival.RefreshFieldBags();
            for (int i = 0; i < arrival.Cards.Count; i++)
            {
                var p = arrival.Participants[i]; arrival.Cards[i].Health.text = p.Health + " / " + p.MaxHealth;
                SegmentedHealthGraphic.Set(arrival.Cards[i].HealthFill,p.Health,p.MaxHealth);
                if (i < arrival.PartyPawns.Count && arrival.PartyPawns[i]) arrival.PartyPawns[i].SetActive(p.Health > 0);
            }
            encounter.FinishBattle(outcome == FieldBattleOutcome.Retreated, noise);
        }
        void Cleanup()
        {
            if (Presentation) Presentation.Clear();
            itemMode = aiming = false; pan = panTarget = 0; SetItemLayout(false); HideAim(); EndLesson();
            if (phaseRoutine != null) { StopCoroutine(phaseRoutine); phaseRoutine = null; } if (PhaseBanner) PhaseBanner.Hide();
            foreach (var s in slots) if (s) Destroy(s.gameObject); foreach (var c in chips) if (c) Destroy(c.gameObject); foreach (var o in orderCards) if (o) Destroy(o.gameObject);
            slots.Clear(); slotIds.Clear(); chips.Clear(); chipUnits.Clear(); orderCards.Clear();
            if (previousBackground)
            {
                arrival.Rooms.Background.sprite = previousBackground; arrival.Rooms.Background.transform.localScale = previousBackgroundScale;
                arrival.Rooms.Background.transform.localPosition = backgroundHome;
            }
            if (world) { world.SetActive(false); Destroy(world); }
            foreach (var mesh in gridMeshes) Destroy(mesh); gridMeshes.Clear(); if (gridMaterial) Destroy(gridMaterial);
            foreach (var card in cards) if (card) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            foreach (var hud in huds) if (hud) Destroy(hud.gameObject);
            foreach (var line in threatLines) if (line) Destroy(line.gameObject);
            cards.Clear(); views.Clear(); huds.Clear(); threatLines.Clear(); shown.Clear(); down.Clear(); intents.Clear();
            Result.SetActive(false); View.SetActive(false);
        }
        void OnDestroy() { if (world) Destroy(world); foreach (var mesh in gridMeshes) Destroy(mesh); if (gridMaterial) Destroy(gridMaterial); }

        // ---- board geometry ----
        // Both formations share the floor's vanishing point. Equal board rows grow toward the camera.
        public static Vector2 BoardVertex(int side, float column, float row)
        {
            // Calibrated against arcade-unlit-v1 floor seams (1672x941 -> 1920x1080).
            const float vanishX = 932, vanishY = -562, backY = 365, frontY = 652;
            float ratio = 1f / (1f - (1f - (backY - vanishY) / (frontY - vanishY)) * row / 3f);
            float backX = (side == 0 ? 483 : 1092) + column * 135;
            return new Vector2(vanishX + (backX - vanishX) * ratio, vanishY + (backY - vanishY) * ratio);
        }
        public static Vector2 Point(bool enemy, int depth, int lane)
        {
            return BoardVertex(enemy ? 1 : 0, (enemy ? depth : 2 - depth) + .5f, lane + .8f);
        }
        Vector3 LocalFromCanvas(Vector2 canvasPoint)
        {
            var camera = canvasCamera ? canvasCamera : Frame.GetComponentInParent<Canvas>().worldCamera;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, Frame.TransformPoint(new Vector3(canvasPoint.x, -canvasPoint.y, 0)));
            var pos = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z)); pos.z = 0;
            return world ? world.transform.InverseTransformPoint(pos) : pos;
        }
        Vector2 LayerPoint(RectTransform layer, Vector3 worldPosition)
        {
            var camera = canvasCamera ? canvasCamera : Frame.GetComponentInParent<Canvas>().worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer ? layer : Frame, RectTransformUtility.WorldToScreenPoint(camera, worldPosition), camera, out var local);
            return local;
        }
        Vector3 ChestOf(int unit, int moveDepth, int moveLane)
        {
            var v = views[unit];
            if (unit == State.Actor && moveDepth >= 0) return world.transform.TransformPoint(CellHome(false, moveDepth, moveLane)) + new Vector3(0, v.Height * .52f, 0);
            return v.Chest;
        }
        void LateUpdate()
        {
            if (!IsOpen || State == null || !world) return;
            var screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize != lastScreen || lastFrameScale != Frame.lossyScale) { layoutDirty = true; lastScreen = screenSize; lastFrameScale = Frame.lossyScale; }
            if (layoutDirty) foreach (var v in views) if (v) v.Home = CellHome(v.Enemy, v.Depth, v.Lane);
            if (dangerCells.Count > 0) { PulseDanger(); tintDirty = true; }
            if (layoutDirty || tintDirty) RebuildGrid();
            layoutDirty = tintDirty = false;
            pan = Mathf.MoveTowards(pan, panTarget, Time.unscaledDeltaTime / Mathf.Max(.01f, PanDuration));
            var shake = (Presentation ? Presentation.ShakeOffset : Vector3.zero) + Vector3.left * ItemPan * Mathf.SmoothStep(0, 1, pan);
            stage.localPosition = shake; arrival.Rooms.Background.transform.localPosition = backgroundHome + shake;
            PlaceOverlays(); PlaceAim(); PlaceLesson();
        }
        void RebuildGrid()
        {
            // World meshes render beneath the lit pawns. The UI polygons only handle hit testing.
            for (int i = 0; i < Cells.Length; i++)
            {
                var cell = Cells[i]; cell.canvasRenderer.SetAlpha(0);
                Vector2[] points = { cell.TopLeft, cell.TopRight, cell.BottomRight, cell.BottomLeft };
                var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
                Action<Vector2, Color> add = (p, color) => { vertices.Add(LocalFromCanvas(new Vector2(p.x, -p.y))); colors.Add(color); };
                foreach (var p in points) add(p, cell.color);
                triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
                var edgeColor = edgeTints[i].a > 0 ? edgeTints[i] : cell.Edge; float thickness = cell.Thickness * (edgeWide[i] ? 1.6f : 1);
                for (int edge = 0; edge < 4; edge++)
                {
                    var a = points[edge]; var b = points[(edge + 1) % 4]; var direction = (b - a).normalized;
                    var off = new Vector2(-direction.y, direction.x) * thickness; int n = vertices.Count;
                    add(a, edgeColor); add(b, edgeColor); add(b + off, edgeColor); add(a + off, edgeColor);
                    triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                }
                var mesh = gridMeshes[i]; mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
                mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds();
            }
        }
        readonly List<Rect> bodies = new List<Rect>(), labels = new List<Rect>();
        [Header("머리 위 정보 배치 · 다른 말을 가리지 않도록")]
        public float LabelWidth = 132, BodyHalfWidth = 42, TopBandBottom = 150;
        // Keep labels attached to their own head. Nearby bodies are a soft constraint; small offsets resolve label collisions.
        // Placement uses logical cells so labels do not hop while someone lunges; animation offsets are added afterwards.
        void PlaceOverlays()
        {
            bodies.Clear(); labels.Clear();
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (!v || !v.gameObject.activeSelf || down[i]) { bodies.Add(Rect.zero); continue; }
                var foot = LayerPoint(HudLayer, world.transform.TransformPoint(v.Home)); var top = LayerPoint(HudLayer, world.transform.TransformPoint(v.Home + new Vector3(0, v.Height, 0)));
                bodies.Add(Rect.MinMaxRect(foot.x - BodyHalfWidth, foot.y, foot.x + BodyHalfWidth, top.y));
            }
            foreach (int i in Enumerable.Range(0, views.Count).OrderBy(i => views[i] ? views[i].Lane : 9))
            {
                var hud = i < huds.Count ? huds[i] : null; if (!hud) continue;
                var v = views[i]; bool visible = v && v.gameObject.activeSelf && !down[i] && v.Alpha > .5f;
                if (hud.gameObject.activeSelf != visible) hud.gameObject.SetActive(visible);
                if (!visible) continue;
                var body = bodies[i]; float height = hud.ContentHeight, half = LabelWidth * .5f;
                float outer = v.Enemy ? 1 : -1;
                Vector2[] options = { new Vector2(body.center.x, body.yMax + 8), new Vector2(body.center.x + outer * 24, body.yMax + 8), new Vector2(body.center.x - outer * 24, body.yMax + 8), new Vector2(body.center.x + outer * 48, body.yMax + 8), new Vector2(body.center.x - outer * 48, body.yMax + 8) };
                Vector2 best = options[0]; float bestCost = float.MaxValue;
                foreach (var o in options)
                {
                    var rect = new Rect(o.x - half, o.y, LabelWidth, height); float cost = Mathf.Abs(o.x - body.center.x) * height * 2;
                    for (int k = 0; k < bodies.Count; k++) if (k != i) cost += Overlap(rect, bodies[k]);
                    foreach (var placed in labels) cost += Overlap(rect, placed) * 4;
                    cost += Mathf.Max(0, rect.yMax + TopBandBottom) * LabelWidth;
                    if (cost < bestCost - .5f) { bestCost = cost; best = o; }
                }
                labels.Add(new Rect(best.x - half, best.y, LabelWidth, height));
                var motion = LayerPoint(HudLayer, v.Head) - LayerPoint(HudLayer, world.transform.TransformPoint(v.Home + new Vector3(0, v.Height + .08f, 0)));
                ((RectTransform)hud.transform).anchoredPosition = best + motion;
            }
            int lines = 0; HoverMove(out int moveDepth, out int moveLane);
            UpdateLinks();
            if (ThreatPrefab && HudLayer)
                foreach (var intent in intents)
                {
                    if ((intent.Kind != EnemyIntentKind.Attack && intent.Kind != EnemyIntentKind.Strike) || intent.Hits == null || !views[intent.Enemy]) continue;
                    foreach (var hit in intent.Hits)
                    {
                        if (hit.Target < 0 || hit.Target >= views.Count || !views[hit.Target]) continue;
                        // Enemy plans are read from the floor marks and head tags; a line only joins them on request, never while aiming.
                        if (aiming || !(ThreatLines || intent.Enemy == linkEnemy || hit.Target == linkAlly)) continue;
                        if (threatLines.Count <= lines) { var made = Instantiate(ThreatPrefab, HudLayer); made.transform.SetAsFirstSibling(); threatLines.Add(made); }
                        var line = threatLines[lines++]; if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
                        line.Set(LayerPoint(HudLayer, views[intent.Enemy].Chest), LayerPoint(HudLayer, ChestOf(hit.Target, moveDepth, moveLane)));
                    }
                }
            for (int k = lines; k < threatLines.Count; k++) if (threatLines[k].gameObject.activeSelf) threatLines[k].gameObject.SetActive(false);
        }
        static float Overlap(Rect a, Rect b)
        {
            if (b.width <= 0) return 0;
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0 && h > 0 ? w * h : 0;
        }
        bool HoverMove(out int depth, out int lane)
        {
            depth = lane = -1;
            if (hover < 0 || hover >= 9 || !CanInput) return false;
            int d = hover % 3, l = hover % 9 / 3;
            if (!State.CanMove(d, l)) return false;
            depth = d; lane = l; return true;
        }

        // ---- screen text and markers ----
        void Refresh()
        {
            tintDirty = true;
            bool input = CanInput;
            int shownActor = Busy ? displayActor : State.Actor; if (shownActor < 0 || shownActor >= State.Units.Count) shownActor = State.Actor;
            var actor = State.Units[shownActor];
            // Whose phase it is, in its colour: the side that is moving also lights its board edge below.
            bool enemyPhase = actor.Enemy && State.Outcome == FieldBattleOutcome.Playing;
            var phaseColor = enemyPhase ? EnemyPhaseColor : AllyPhaseColor;
            Round.supportRichText = true;
            Round.text = "<color=#" + ColorUtility.ToHtmlStringRGB(phaseColor) + ">" + (enemyPhase ? EnemyPhaseTitle : AllyPhaseTitle) + "</color>  ·  " + (Busy ? displayRound : State.Round) + "라운드";
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i]) continue;
                if (cards[i].gameObject.activeSelf == down[i]) cards[i].gameObject.SetActive(!down[i]);
                cards[i].Paper.color = i == shownActor ? Gold : State.Units[i].Enemy ? Red : Color.white;
            }
            for (int i = 0; i < views.Count; i++)
                if (views[i]) views[i].BaseTint = i == shownActor ? Gold : State.Units[i].Enemy ? Red : State.Units[i].Guarding ? GuardBase : AllyBase;
            ActorName.text = actor.Name; ActorPortrait.sprite = Portrait(actor);
            ActorInfo.text = "체력  " + shown[shownActor] + " / " + actor.Maximum + "\n" + (actor.Enemy ? EnemyInfo(actor)
                : "가방  " + arrival.Inventory.SlotsFor(actor.Person) + " / " + actor.Person.BagCapacity + "\n휴대 탄약  " + arrival.Inventory.CountFor(actor.Person, "ammo"));
            SegmentedHealthGraphic.Set(ActorHealth,shown[shownActor],actor.Maximum);

            bool targetValid = SelectedTarget >= 0 && SelectedTarget < State.Units.Count && State.Units[SelectedTarget].Alive && State.Units[SelectedTarget].Enemy;
            int detailTarget = -1;
            int chance = State.HitChance(SelectedTarget, Ranged), damage = SelectedTarget >= 0 && SelectedTarget < State.Units.Count ? State.ExpectedDamage(SelectedTarget, Ranged) : State.DamageFor(Ranged);
            if (Busy && focusTarget >= 0 && focusTarget < State.Units.Count)
            {
                var t = State.Units[focusTarget]; TargetPortrait.gameObject.SetActive(true);
                detailTarget = focusTarget;
                TargetName.text = t.Name; TargetPortrait.sprite = Portrait(t);
                TargetInfo.text = "체력  " + shown[focusTarget] + " / " + t.Maximum + "\n" + (t.Guarding ? (focusCommitted ? "방어 중 · 피해 -" + State.GuardReductionFor(focusTarget,true) : "방어 중 · 물림 -" + Rules.GuardHitPenalty + "%p") : "받을 피해  " + focusDamage);
                SegmentedHealthGraphic.Set(TargetHealth,shown[focusTarget],t.Maximum); Chance.text = focusCommitted ? "예고 공격 · 반드시 맞음" : "명중률  " + focusChance + "%";
            }
            else if (Busy && cardFrozen && cardTarget >= 0 && cardTarget < State.Units.Count)
            {
                var t = State.Units[cardTarget]; TargetPortrait.gameObject.SetActive(true);
                detailTarget = cardTarget;
                TargetName.text = t.Name; TargetPortrait.sprite = Portrait(t);
                TargetInfo.text = "체력  " + shown[cardTarget] + " / " + t.Maximum + (down[cardTarget] ? "  ·  쓰러짐" : "") + "\n예상 피해  " + cardDamage + "  ·  급소 " + Rules.CriticalChance + "%";
                SegmentedHealthGraphic.Set(TargetHealth,shown[cardTarget],t.Maximum);
                Chance.text = cardChance > 0 ? "명중률  " + cardChance + "%" : "공격 대상 선택";
            }
            else if (targetValid)
            {
                var t = State.Units[SelectedTarget]; TargetPortrait.gameObject.SetActive(true);
                detailTarget = SelectedTarget;
                TargetName.text = t.Name; TargetPortrait.sprite = Portrait(t);
                bool lethal = chance > 0 && damage >= shown[SelectedTarget];
                TargetInfo.text = "체력  " + shown[SelectedTarget] + " / " + t.Maximum + (lethal ? "  ·  처치 가능" : "") + "\n예상 피해  " + damage + "  ·  급소 " + Rules.CriticalChance + "%";
                SegmentedHealthGraphic.Set(TargetHealth,shown[SelectedTarget],t.Maximum);
                Chance.text = chance > 0 ? "명중률  " + chance + "%" : Ranged ? "사격 불가" : "근접 거리 밖";
            }
            else { TargetPortrait.gameObject.SetActive(false); TargetName.text = "적 선택"; TargetInfo.text = "오른쪽 진형의 적을 선택하세요."; SegmentedHealthGraphic.Set(TargetHealth,0,0); Chance.text = "공격 대상 선택"; }
            RefreshRoleDetails(shownActor,detailTarget);

            Melee.interactable = Shoot.interactable = Guard.interactable = Retreat.interactable = Items.interactable = input;
            Melee.GetComponentInChildren<Image>().color = !Ranged ? Gold : Color.white; Shoot.GetComponentInChildren<Image>().color = Ranged ? Gold : Color.white;
            Execute.interactable = input && State.CanAttack(SelectedTarget, Ranged);
            ExecuteLabel.text = Ranged ? "사격 확정 · 탄약 1 · 소음 +" + Rules.ShotNoise : "근접 공격 확정";
            bool previewMove = HoverMove(out int moveDepth, out int moveLane);
            bool forecast = input || ItemInput;
            intents = forecast ? (previewMove ? State.PredictIntents(State.Actor, moveDepth, moveLane) : State.PredictIntents()) : new List<EnemyIntent>();
            linkEnemy = linkAlly = int.MinValue; // the linked tags follow the new forecast on the next frame
            Hint.text = HintText(actor, targetValid, previewMove);
            Feedback.text = feedbackOverride ?? LessonLine() ?? State.Message;
            if (NoiseValue) NoiseValue.text = State.ReinforcementsLeft ? State.Noise + " / " + State.NextReinforcementNoise : State.Noise.ToString();
            if (NoiseFill)
            {
                NoiseFill.fillAmount = State.ReinforcementsLeft ? Mathf.Clamp01((float)State.Noise / State.NextReinforcementNoise) : 1;
                NoiseFill.color = State.ReinforcementPending ? Red : State.ReinforcementsLeft ? NoiseColor : new Color(.6f, .6f, .56f);
            }

            liveThreats.Clear();
            if (!forecast)
                for (int e = 0; e < State.Units.Count; e++)
                    foreach (var h in State.PendingHits(e)) liveThreats.Add((e, State.Units[e].Pending.Attack, h));
            for (int i = 0; i < huds.Count; i++)
            {
                var hud = huds[i]; if (!hud) continue; var u = State.Units[i];
                if (u.Enemy)
                {
                    var intent = intents.FirstOrDefault(p => p.Enemy == i && p.Kind != EnemyIntentKind.None);
                    // A marked or opened creature keeps its tag through the enemy phase, so the promise stays readable while it plays out.
                    if (!forecast && threatFrozen && !down[i] && frozenTags.TryGetValue(i, out var kept)) intent = kept;
                    else if (!forecast && u.Alive && (u.Pending != null || u.Stagger > 0))
                        intent = new EnemyIntent { Kind = u.Stagger > 0 ? EnemyIntentKind.Recover : EnemyIntentKind.Strike, Enemy = i, Attack = u.Pending?.Attack ?? CreatureAttack.Bite };
                    bool tagged = forecast || intent.Kind == EnemyIntentKind.Strike || intent.Kind == EnemyIntentKind.Recover;
                    var shownKind = intent.Kind == EnemyIntentKind.Strike || intent.Kind == EnemyIntentKind.Windup ? EnemyIntentKind.Attack : intent.Kind == EnemyIntentKind.Recover ? EnemyIntentKind.Wait : intent.Kind;
                    hud.ShowIntent(tagged ? shownKind : EnemyIntentKind.None, IntentText(intent, forecast), intent.Kind == EnemyIntentKind.Advance ? 180 : intent.Lane > u.Lane ? -90 : 90);
                    bool aimed = input && i == SelectedTarget && chance > 0;
                    hud.ShowAim(aimed ? "명중 " + chance + "%" : null);
                    hud.SetPreview(aimed ? Mathf.Min(damage, shown[i]) : 0);
                    hud.Guard.SetActive(false); hud.ShowDanger(null);
                }
                else
                {
                    // The tag names the incoming attack the same way the attacker's own tag does, so the pair reads without a line.
                    // Marked strikes keep their victim tags through the enemy phase, as the attacker keeps its own.
                    var threats = forecast
                        ? intents.Where(p => (p.Kind == EnemyIntentKind.Attack || p.Kind == EnemyIntentKind.Strike) && p.Hits != null)
                            .SelectMany(p => p.Hits.Where(h => h.Target == i).Select(h => (enemy: p.Enemy, attack: p.Attack, hit: h, rolled: p.Kind == EnemyIntentKind.Attack))).ToList()
                        : (threatFrozen ? frozenThreats : liveThreats).Where(t => t.hit.Target == i).Select(t => (t.enemy, t.attack, t.hit, rolled: false)).ToList();
                    hud.ShowDanger(!u.Alive || threats.Count == 0 ? null : threats.Count > 1 ? "공격 ×" + threats.Count : StrikeTag(threats[0].enemy, threats[0].attack, threats[0].hit, threats[0].rolled));
                    hud.Guard.SetActive(u.Guarding && u.Alive); hud.ShowAim(null); hud.SetPreview(0);
                    hud.Name.text = u.Name + (u.Bound > 0 ? " · 묶임" : "") + (u.Veiled > 0 ? " · 가림" : "");
                    // Bound allies sink into the puddle; veiled ones carry a grey film until their own turn is over.
                    if (views[i])
                    {
                        if (u.Alive && (u.Bound > 0 || u.Veiled > 0)) views[i].SetPose(0, u.Bound > 0 ? .93f : 1, u.Bound > 0 ? -.06f : 0, u.Bound > 0 ? .4f : 0, 3, u.Veiled > 0 ? .3f : 0, Presentation ? Presentation.VeilColor : Color.grey);
                        else if (views[i].Posed) views[i].ClearPose();
                    }
                }
            }
            dangerCells.Clear(); foreach (int c in threatFrozen ? (IEnumerable<int>)frozenDanger : State.DangerCells()) dangerCells.Add(c);
            for (int i = 0; i < Cells.Length; i++)
            {
                int side = i / 9, lane = i % 9 / 3, depth = i % 3;
                bool selected = targetValid && side == 1 && State.Units[SelectedTarget].Depth == depth && State.Units[SelectedTarget].Lane == lane;
                int standing = side == 0 ? State.Units.FindIndex(u => !u.Enemy && u.Alive && u.Depth == depth && u.Lane == lane) : -1;
                bool current = side == (actor.Enemy ? 1 : 0) && actor.Depth == depth && actor.Lane == lane;
                bool threatened = standing >= 0 && intents.Any(p => (p.Kind == EnemyIntentKind.Attack || p.Kind == EnemyIntentKind.Strike) && p.Hits != null && p.Hits.Any(h => h.Target == standing)) && !(previewMove && standing == State.Actor);
                bool movable = side == 0 && input && State.CanMove(depth, lane);
                Color tint = selected ? new Color(.94f, .51f, .38f, .22f) : current ? new Color(1, .83f, .4f, .16f)
                    : movable ? new Color(.62f, .83f, .72f, hover == i ? .3f : .12f) : Color.clear;
                if (threatened && !current) tint = new Color(.9f, .38f, .3f, .2f);
                if (previewMove && i == hover && ActorThreatened()) tint = new Color(.95f, .45f, .35f, .28f);
                baseTints[i] = tint; Cells[i].Tint(tint);
                bool moving = State.Outcome == FieldBattleOutcome.Playing && (side == 1) == enemyPhase;
                var idle = Cells[i].Edge; idle.a *= .45f;
                edgeTints[i] = moving ? new Color(phaseColor.r, phaseColor.g, phaseColor.b, Mathf.Max(.85f, Cells[i].Edge.a)) : idle; edgeWide[i] = moving;
                CellButtons[i].interactable = input;
            }
            if (itemMode) RefreshItems();
        }
        string HintText(FieldBattleState.Unit actor, bool targetValid, bool previewMove)
        {
            // Follows the side on display (like the phase tag), so the party's own replay never reads as the enemy's.
            if (Busy) return actor.Enemy && State.Outcome == FieldBattleOutcome.Playing ? "적이 움직입니다 · 결과를 확인하세요." : "행동 결과를 확인하고 있습니다.";
            if (actor.Enemy || actor.Person == null || !State.PlayerTurn) return "";
            if (itemMode) return ItemHint;
            if (aiming) return AimHint;
            if (previewMove) return MoveHint();
            var lesson = LessonHint(); if (lesson != null) return lesson;
            if (actor.Bound > 0 && !State.Moved) return "발이 묶여 이번 차례에는 움직일 수 없습니다 · 행동은 할 수 있습니다.";
            if (!State.Moved && dangerCells.Contains(FieldBattleState.CellOf(actor.Depth, actor.Lane)))
            {
                if (intents.Any(p => p.Kind == EnemyIntentKind.Strike && p.Hits != null && p.Hits.Any(h => h.Target == State.Actor)))
                    return "붉게 깜빡이는 칸은 다음 적 차례에 공격받습니다 · 빈 칸으로 피하거나 방어(피해 -" + Rules.GuardStrikeReduction + ", 밀림 없음)";
                int cell = FieldBattleState.CellOf(actor.Depth, actor.Lane);
                int cover = intents.Where(p => p.Kind == EnemyIntentKind.Strike && p.Cells != null && p.Cells.Contains(cell) && p.HitCount > 0).Select(p => p.Hits[0].Target).Where(t => t != State.Actor).DefaultIfEmpty(-1).First();
                if (cover >= 0) return "지금은 " + Subject(State.Units[cover].Name) + " 앞을 막아 줍니다 · 앞이 비면 이 칸이 맞습니다.";
            }
            if (actor.Veiled > 0) return "시야가 가려져 이번 공격 명중률 -" + Rules.VeilPenalty + "%p";
            if (Ranged && arrival.Inventory.CountFor(actor.Person, "ammo") == 0) return "이 대원의 가방에 탄약이 없습니다. 근접 또는 방어를 선택하세요.";
            if (!Ranged && actor.Depth != 0) return "근접 공격은 전열(가운데 쪽 줄)에서 가능합니다. 빈 칸으로 이동하세요.";
            if (!Ranged && targetValid && !State.InMeleeReach(SelectedTarget)) return "그 적은 근접 거리 밖입니다 · 앞줄에서 가까운 줄의 적만 칠 수 있습니다.";
            if (State.ReinforcementPending) return State.Units.Any(u => u.Enemy && u.Creature != null) ? "소리에 끌린 무언가가 다음 라운드에 합류합니다." : "총성에 끌린 감염자가 다음 라운드에 합류합니다.";
            if (State.Moved) return "위치 변경 완료 · 행동 1회 남음";
            return "빈 칸으로 위치 변경 1회 + 행동 1회 · 고른 적을 한 번 더 누르면 바로 공격";
        }
        // Hovering an empty cell to move: safe, inside a marked strike, still in reach of a rolled attack, or only safe behind a friend.
        string MoveHint()
        {
            HoverMove(out int d, out int l);
            var mine = intents.Where(p => (p.Kind == EnemyIntentKind.Attack || p.Kind == EnemyIntentKind.Strike) && p.Hits != null && p.Hits.Any(h => h.Target == State.Actor)).ToList();
            if (mine.Any(p => p.Kind == EnemyIntentKind.Strike)) return "이 칸도 예고된 공격 범위입니다 · 붉은 칸을 피하세요.";
            if (mine.Count > 0) return "이 칸으로 옮겨도 " + AttackLabel(mine[0].Enemy, mine[0].Attack, true) + " 대상이 됩니다 · 이어진 선이 그 적입니다.";
            int cell = FieldBattleState.CellOf(d, l);
            var cover = intents.Where(p => p.Kind == EnemyIntentKind.Strike && p.Cells != null && p.Cells.Contains(cell) && p.HitCount > 0).Select(p => p.Hits[0].Target).Where(t => t != State.Actor).DefaultIfEmpty(-1).First();
            if (dangerCells.Contains(cell) && cover >= 0 && State.Units[cover].Alive)
                return "지금은 " + Subject(State.Units[cover].Name) + " 앞을 막아 줍니다 · 비키면 이 칸이 맞습니다.";
            return "이 칸으로 옮기면 이번 적 차례에 공격받지 않습니다.";
        }
        // Korean subject particle: 이 after a final consonant, 가 otherwise.
        static string Subject(string name) { if (string.IsNullOrEmpty(name)) return name; char c = name[name.Length - 1]; return name + (c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 == 0 ? "가" : "이"); }
        string Announce(BattleEvent e)
        {
            if (e == null || e.Actor < 0 || e.Actor >= State.Units.Count) return null;
            string who = State.Units[e.Actor].Name;
            switch (e.Kind)
            {
                case BattleEventKind.Move: return who + " · 자리를 옮깁니다";
                case BattleEventKind.Guard: return who + " · 방어 자세";
                case BattleEventKind.Item: return who + " · 소지품 사용";
                case BattleEventKind.Attack: return who + (e.Ranged ? " · 사격" : " · 근접 공격");
                default: return null;
            }
        }
        // Short tags for the head-top intent paper (about six characters fit).
        string IntentText(EnemyIntent intent, bool forecast = true)
        {
            var c = intent.Enemy >= 0 && intent.Enemy < State.Units.Count ? State.Units[intent.Enemy].Creature : null;
            string attack = c != null ? c.AttackName : "공격";
            switch (intent.Kind)
            {
                case EnemyIntentKind.Attack: return AttackLabel(intent.Enemy, intent.Attack, true) + " " + intent.Chance + "%";
                case EnemyIntentKind.Advance: return "전진";
                case EnemyIntentKind.Shift: return "옆 줄로";
                case EnemyIntentKind.Wait: return intent.Resting ? "숨 고름" : "대기";
                case EnemyIntentKind.Windup: return c != null ? c.WindupName : "준비";
                case EnemyIntentKind.Recover: return "빈틈";
                case EnemyIntentKind.Strike:
                {
                    if (intent.Attack == CreatureAttack.Broadcast) return attack + " +" + Rules.BroadcastNoise;
                    // One wording on both heads: the attacker shows its victims' tag when they all read the same.
                    // While input is locked the victims read the frozen or live marked hits, so the attacker reads those too.
                    var hits = forecast ? intent.Hits : (threatFrozen ? frozenThreats : liveThreats).Where(t => t.enemy == intent.Enemy).Select(t => t.hit).ToList();
                    if (hits == null || hits.Count == 0) return forecast ? attack + " 피함" : attack;
                    var tags = hits.Select(h => StrikeTag(intent.Enemy, intent.Attack, h, false)).Distinct().ToList();
                    return tags.Count == 1 ? tags[0] : attack + " ×" + hits.Count;
                }
                default: return "";
            }
        }
        string EnemyInfo(FieldBattleState.Unit u)
        {
            var c = u.Creature;
            if (c == null) return "감염자 · 물기 피해 " + Rules.EnemyDamage;
            return (c.Attack == CreatureAttack.Broadcast ? c.AttackName + " · 소음 +" + Rules.BroadcastNoise : c.AttackName + " · 피해 " + c.Damage)
                + (u.Stagger > 0 ? "\n빈틈 · 방어 0" : "");
        }
        // Rolled attacks: 감염자 bite, a quiet 귀기울임 only gropes, the swarm uses its own name. Marked strikes use the creature's attack name.
        string AttackLabel(int enemy, CreatureAttack attack, bool rolled)
        {
            var c = enemy >= 0 && enemy < State.Units.Count ? State.Units[enemy].Creature : null;
            if (c == null) return "물기";
            if (!rolled || attack == CreatureAttack.Swarm) return c.AttackName;
            return attack == CreatureAttack.Listen ? "더듬기" : "물기";
        }
        // Tag for one hit: damage, or what it does instead (veil, brace, bind) when no damage lands.
        string StrikeTag(int enemy, CreatureAttack attack, BattleHit h, bool rolled)
        {
            string name = AttackLabel(enemy, attack, rolled);
            if (rolled) return name + " " + h.Chance + "%";
            return name + (h.Damage > 0 ? " -" + h.Damage : h.Veiled ? " 가림" : h.Braced ? " 버팀" : h.Bound ? " 묶임" : "");
        }
        // Pointer on an infected (not aiming): link it to whoever it will hit. Pointer on an ally: link it to whoever will hit it.
        void UpdateLinks()
        {
            int enemy = -1, ally = -1;
            if (!aiming && CanInput)
            {
                enemy = HoveredEnemy();
                if (enemy < 0)
                {
                    ally = HoveredAlly();
                    // An empty cell being previewed: link whoever would hit the actor there.
                    if (ally < 0 && HoverMove(out _, out _)) ally = State.Actor;
                }
            }
            if (enemy == linkEnemy && ally == linkAlly) return;
            linkEnemy = enemy; linkAlly = ally;
            for (int i = 0; i < huds.Count; i++)
            {
                if (!huds[i]) continue;
                bool linked = intents.Any(p => p.Hits != null && (p.Kind == EnemyIntentKind.Attack || p.Kind == EnemyIntentKind.Strike)
                    && p.Hits.Any(h => (p.Enemy == linkEnemy && (i == p.Enemy || h.Target == i)) || (h.Target == linkAlly && (i == p.Enemy || i == linkAlly))));
                huds[i].Emphasize(linked || LessonEmphasis(i));
            }
        }
        bool ActorThreatened() => intents.Any(p => (p.Kind == EnemyIntentKind.Attack || p.Kind == EnemyIntentKind.Strike) && p.Hits != null && p.Hits.Any(h => h.Target == State.Actor));
        // Committed cells breathe red; the ones a wind-up just claimed flash harder for a moment.
        void PulseDanger()
        {
            float now = Time.unscaledTime, pulse = .5f + .5f * Mathf.Sin(now * DangerPulse * Mathf.PI * 2), blink = now < blinkUntil ? Mathf.Abs(Mathf.Sin(now * 18)) : 0;
            foreach (int cell in dangerCells)
            {
                if (cell < 0 || cell >= 9) continue;
                var strong = DangerCell; strong.a = Mathf.Lerp(DangerCell.a * .55f, DangerCell.a, pulse) + blink * .25f;
                Cells[cell].Tint(Color.Lerp(baseTints[cell], strong, .85f));
            }
        }
    }
}
