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
    public sealed class ExpeditionBattlePanel : MonoBehaviour
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
        public FieldBattleState State { get; private set; }
        public bool IsOpen => View.activeSelf;
        public bool Busy { get; private set; }
        public int SelectedTarget { get; private set; } = -1;
        public bool Ranged { get; private set; }
        public float ActionPause = .65f;

        ExpeditionArrivalPanel arrival;
        ExpeditionEncounterPanel encounter;
        readonly List<GameObject> pawns = new List<GameObject>();
        readonly List<BattleTurnCard> cards = new List<BattleTurnCard>();
        readonly Dictionary<Adventurer, int> startingHealth = new Dictionary<Adventurer, int>();
        GameObject world;
        Sprite previousBackground;
        Vector3 previousBackgroundScale;
        readonly List<Mesh> gridMeshes = new List<Mesh>();
        Material gridMaterial;
        bool visualsDirty;
        Vector2 lastScreen;
        Vector3 lastFrameScale;
        bool settled;
        static readonly Color Gold = new Color(1, .78f, .38f), Red = new Color(.83f, .43f, .34f);

        public void Initialize(ExpeditionArrivalPanel a, ExpeditionEncounterPanel e)
        {
            arrival = a; encounter = e; View.SetActive(false); Result.SetActive(false); RetreatReview.SetActive(false);
            Melee.onClick.AddListener(() => SelectAction(false)); Shoot.onClick.AddListener(() => SelectAction(true));
            Execute.onClick.AddListener(Attack); Guard.onClick.AddListener(Defend);
            Retreat.onClick.AddListener(AskRetreat); RetreatCancel.onClick.AddListener(CancelRetreat);
            RetreatConfirm.onClick.AddListener(ConfirmRetreat); ResultContinue.onClick.AddListener(Continue);
            Items.interactable = false;
            for (int i = 0; i < CellButtons.Length; i++) { int index = i; CellButtons[i].onClick.AddListener(() => ClickCell(index)); }
        }
        public void Begin(int enemyCount)
        {
            if (IsOpen || !encounter.IsOpen || !arrival.Participants.Any(p => p.Health > 0)) return;
            State = new FieldBattleState(arrival.Participants, enemyCount, p => arrival.Inventory.CountFor(p, "ammo"),
                p => arrival.Inventory.TransferField(p, "ammo", 1, false), () => UnityEngine.Random.Range(0, 100));
            startingHealth.Clear(); foreach (var p in arrival.Participants) startingHealth[p] = p.Health;
            settled = Busy = Ranged = false; SelectedTarget = State.Units.FindIndex(u => u.Enemy);
            arrival.Main.gameObject.SetActive(false); arrival.PawnRoot.gameObject.SetActive(false);
            encounter.Workspace.gameObject.SetActive(false);
            View.SetActive(true); Result.SetActive(false); RetreatReview.SetActive(false); Workspace.interactable = Workspace.blocksRaycasts = true;
            world = new GameObject("BattlePawns"); world.transform.SetParent(arrival.World.transform, false);
            previousBackground = arrival.Rooms.Background.sprite; previousBackgroundScale = arrival.Rooms.Background.transform.localScale;
            if (BattleBackground)
            {
                arrival.Rooms.Background.sprite = BattleBackground;
                arrival.Rooms.Background.transform.localScale = new Vector3(19.2f / BattleBackground.bounds.size.x, 10.8f / BattleBackground.bounds.size.y, 1);
            }
            gridMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            gridMaterial.mainTexture = Texture2D.whiteTexture;
            foreach (var cell in Cells)
            {
                var grid = new GameObject(cell.name + "Floor", typeof(MeshFilter), typeof(MeshRenderer)); grid.transform.SetParent(world.transform, false);
                var mesh = new Mesh { name = cell.name }; gridMeshes.Add(mesh); grid.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = grid.GetComponent<MeshRenderer>(); renderer.sharedMaterial = gridMaterial; renderer.sortingOrder = 15;
            }
            foreach (var unit in State.Units)
            {
                var pawn = Instantiate(PawnPrefab, world.transform); pawn.name = unit.Name;
                var body = pawn.transform.Find("Body").GetComponent<SpriteRenderer>();
                int index = unit.Enemy ? -1 : arrival.Participants.ToList().IndexOf(unit.Person);
                body.sprite = unit.Enemy ? EnemyBody : arrival.PawnRoot.GetChild(index).Find("Body").GetComponent<SpriteRenderer>().sprite;
                var pixels = VisibleBounds?.FirstOrDefault(v => v.Sprite == body.sprite)?.Pixels ?? new Rect(0, 0, body.sprite.rect.width, body.sprite.rect.height);
                float scale = 1.72f * body.sprite.pixelsPerUnit / pixels.height;
                body.transform.localScale = Vector3.one * scale;
                body.transform.localPosition = new Vector3((body.sprite.pivot.x - pixels.center.x) * scale / body.sprite.pixelsPerUnit,
                    (body.sprite.pivot.y - pixels.y) * scale / body.sprite.pixelsPerUnit + .035f, 0);
                pawns.Add(pawn);
                var card = Instantiate(TurnPrefab, TurnContent); card.Portrait.sprite = Portrait(unit);
                card.Label.text = unit.Enemy ? "적" : unit.Name; cards.Add(card);
            }
            Place.text = arrival.Place.text; Clock.text = arrival.Clock.text;
            Refresh();
        }
        Sprite Portrait(FieldBattleState.Unit u) => u.Enemy ? EnemyPortrait : arrival.Cards[arrival.Participants.ToList().IndexOf(u.Person)].Portrait.sprite;
        public void SelectAction(bool ranged)
        {
            if (!CanInput) return; Ranged = ranged; Refresh();
        }
        bool CanInput => IsOpen && State != null && State.PlayerTurn && !Busy && !Result.activeSelf && !RetreatReview.activeSelf;
        public void ClickCell(int index)
        {
            if (!CanInput || index < 0 || index >= 18) return;
            int side = index / 9, lane = index % 9 / 3, depth = index % 3;
            if (side == 1) { int target = State.Units.FindIndex(u => u.Enemy && u.Alive && u.Depth == depth && u.Lane == lane); if (target >= 0) SelectedTarget = target; }
            else State.Move(depth, lane);
            Refresh();
        }
        void Attack() { if (CanInput && State.Attack(SelectedTarget, Ranged)) StartCoroutine(ResolveActions()); }
        void Defend() { if (CanInput && State.Guard()) StartCoroutine(ResolveActions()); }
        IEnumerator ResolveActions()
        {
            Busy = true; Refresh();
            yield return new WaitForSecondsRealtime(ActionPause);
            while (State.Outcome == FieldBattleOutcome.Playing && State.Current.Enemy)
            {
                State.EnemyStep(); Refresh(); yield return new WaitForSecondsRealtime(ActionPause);
            }
            Busy = false;
            if (State.Outcome != FieldBattleOutcome.Playing) ShowResult();
            else { if (SelectedTarget < 0 || !State.Units[SelectedTarget].Alive) SelectedTarget = State.Units.FindIndex(u => u.Enemy && u.Alive); Refresh(); }
        }
        void AskRetreat()
        {
            if (!CanInput) return; RetreatReview.SetActive(true); Workspace.interactable = Workspace.blocksRaycasts = false;
        }
        public void CancelRetreat()
        {
            RetreatReview.SetActive(false); Workspace.interactable = Workspace.blocksRaycasts = true;
        }
        void ConfirmRetreat()
        {
            if (!IsOpen || !RetreatReview.activeSelf || Busy || !State.PlayerTurn) return;
            CancelRetreat(); State.Retreat(); ShowResult();
        }
        public void Escape() { if (RetreatReview.activeSelf) CancelRetreat(); }
        void ShowResult()
        {
            Result.SetActive(true); Workspace.interactable = Workspace.blocksRaycasts = false;
            ResultTitle.text = State.Outcome == FieldBattleOutcome.Victory ? "주변이 조용해졌습니다" : State.Outcome == FieldBattleOutcome.Retreated ? "교전 중단" : "원정대 행동 불능";
            ResultBody.text = "전투 " + State.Round + "라운드  ·  사용 탄약 " + State.AmmoSpent + "\n\n"
                + string.Join("\n", arrival.Participants.Select(p => p.Name + "  체력 " + startingHealth[p] + " → " + p.Health + " / " + p.MaxHealth))
                + "\n\n" + (State.Outcome == FieldBattleOutcome.Defeat ? "돌아갈 수 있는 대원이 없습니다.\n이번 원정을 더 진행할 수 없습니다."
                : State.Outcome == FieldBattleOutcome.Retreated ? "복도로 이동 · 탐험 1턴\n가방과 기존 수색도는 유지됩니다."
                : "교전 처리 · 탐험 1턴 / 소음 +2\n발견물과 수색도를 유지하고 이어갑니다.");
            ResultContinueLabel.text = State.Outcome == FieldBattleOutcome.Defeat ? "시작 화면으로" : State.Outcome == FieldBattleOutcome.Retreated ? "복도로 물러나기" : "수색으로 돌아가기";
        }
        public void Continue()
        {
            if (!IsOpen || !Result.activeSelf || settled) return;
            settled = true; var outcome = State.Outcome;
            if (outcome == FieldBattleOutcome.Defeat) { SceneManager.LoadScene("StartMenu"); return; }
            Cleanup(); arrival.Main.gameObject.SetActive(true); arrival.PawnRoot.gameObject.SetActive(true);
            encounter.Workspace.gameObject.SetActive(true); arrival.RefreshFieldBags();
            for (int i = 0; i < arrival.Cards.Count; i++)
            {
                var p = arrival.Participants[i]; arrival.Cards[i].Health.text = p.Health + " / " + p.MaxHealth;
                arrival.Cards[i].HealthFill.fillAmount = (float)p.Health / p.MaxHealth;
                arrival.PawnRoot.GetChild(i).gameObject.SetActive(p.Health > 0);
            }
            encounter.FinishBattle(outcome == FieldBattleOutcome.Retreated);
        }
        void Cleanup()
        {
            if (previousBackground) { arrival.Rooms.Background.sprite = previousBackground; arrival.Rooms.Background.transform.localScale = previousBackgroundScale; }
            if (world) { world.SetActive(false); Destroy(world); }
            foreach (var mesh in gridMeshes) Destroy(mesh); gridMeshes.Clear(); if (gridMaterial) Destroy(gridMaterial);
            foreach (var card in cards) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            cards.Clear(); pawns.Clear(); Result.SetActive(false); View.SetActive(false);
        }
        void OnDestroy() { if (world) Destroy(world); foreach (var mesh in gridMeshes) Destroy(mesh); if (gridMaterial) Destroy(gridMaterial); }
        // Both formations share the floor's vanishing point. Equal board rows grow toward the camera.
        public static Vector2 BoardVertex(int side, float column, float row)
        {
            const float vanishX = 960, vanishY = -900, backY = 365, frontY = 647;
            float ratio = 1f / (1f - (1f - (backY - vanishY) / (frontY - vanishY)) * row / 3f);
            float backX = (side == 0 ? 455 : 1060) + column * 135;
            return new Vector2(vanishX + (backX - vanishX) * ratio, vanishY + (backY - vanishY) * ratio);
        }
        public static Vector2 Point(bool enemy, int depth, int lane)
        {
            return BoardVertex(enemy ? 1 : 0, (enemy ? depth : 2 - depth) + .5f, lane + .8f);
        }
        void LateUpdate()
        {
            if (!IsOpen || State == null || !world) return;
            var screenSize = new Vector2(Screen.width, Screen.height);
            if (!visualsDirty && screenSize == lastScreen && lastFrameScale == Frame.lossyScale) return;
            visualsDirty = false; lastScreen = screenSize; lastFrameScale = Frame.lossyScale;
            var canvas = Frame.GetComponentInParent<Canvas>(); var camera = canvas.worldCamera;
            // World meshes render beneath the lit pawns. The UI polygons only handle hit testing.
            for (int i = 0; i < Cells.Length; i++)
            {
                var cell = Cells[i]; cell.canvasRenderer.SetAlpha(0);
                Vector2[] points = { cell.TopLeft, cell.TopRight, cell.BottomRight, cell.BottomLeft };
                var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
                Action<Vector2, Color> add = (p, color) =>
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(camera, Frame.TransformPoint(p));
                    var position = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z)); position.z = 0;
                    vertices.Add(world.transform.InverseTransformPoint(position)); colors.Add(color);
                };
                foreach (var p in points) add(p, cell.color);
                triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
                for (int edge = 0; edge < 4; edge++)
                {
                    var a = points[edge]; var b = points[(edge + 1) % 4]; var direction = (b - a).normalized;
                    var off = new Vector2(-direction.y, direction.x) * cell.Thickness; int n = vertices.Count;
                    add(a, cell.Edge); add(b, cell.Edge); add(b + off, cell.Edge); add(a + off, cell.Edge);
                    triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                }
                var mesh = gridMeshes[i]; mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
                mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds();
            }
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i]; var pawn = pawns[i]; pawn.SetActive(u.Alive);
                if (!u.Alive) continue;
                Vector2 pt = Point(u.Enemy, u.Depth, u.Lane);
                var screen = RectTransformUtility.WorldToScreenPoint(camera, Frame.TransformPoint(new Vector3(pt.x, -pt.y, 0)));
                var pos = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z)); pos.z = 0;
                pawn.transform.position = pos;
                var body = pawn.transform.Find("Body").GetComponent<SpriteRenderer>(); var baseSprite = pawn.transform.Find("Base").GetComponent<SpriteRenderer>();
                baseSprite.color = i == State.Actor ? Gold : u.Enemy ? Red : new Color(.64f, .79f, .74f);
                baseSprite.sortingOrder = 40 + u.Lane * 4; body.sortingOrder = baseSprite.sortingOrder + 1;
            }
        }
        void Refresh()
        {
            visualsDirty = true;
            var actor = State.Current; bool input = CanInput;
            Round.text = "현재 차례  ·  " + State.Round + "라운드";
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].gameObject.SetActive(State.Units[i].Alive);
                cards[i].Paper.color = i == State.Actor ? Gold : State.Units[i].Enemy ? Red : Color.white;
            }
            ActorName.text = actor.Name; ActorPortrait.sprite = Portrait(actor);
            ActorInfo.text = "체력  " + actor.Health + " / " + actor.Maximum + "\n" + (actor.Enemy ? "감염자 · 접근 공격" : "가방  " + arrival.Inventory.SlotsFor(actor.Person) + " / " + actor.Person.BagCapacity + "\n휴대 탄약  " + arrival.Inventory.CountFor(actor.Person, "ammo"));
            ActorHealth.fillAmount = (float)actor.Health / actor.Maximum;
            bool targetValid = SelectedTarget >= 0 && State.Units[SelectedTarget].Alive;
            TargetPortrait.gameObject.SetActive(targetValid);
            if (targetValid)
            {
                var t = State.Units[SelectedTarget]; TargetName.text = t.Name; TargetPortrait.sprite = Portrait(t);
                TargetInfo.text = "체력  " + t.Health + " / " + t.Maximum + "\n예상 피해  " + (Ranged ? 3 : 2); TargetHealth.fillAmount = (float)t.Health / t.Maximum;
            }
            else { TargetName.text = "적 선택"; TargetInfo.text = "오른쪽 진형의 적을 선택하세요."; TargetHealth.fillAmount = 0; }
            int chance = State.HitChance(SelectedTarget, Ranged);
            Chance.text = chance > 0 ? "명중률  " + chance + "%" : "공격 대상 선택";
            Melee.interactable = Shoot.interactable = Guard.interactable = Retreat.interactable = input;
            Melee.GetComponentInChildren<Image>().color = !Ranged ? Gold : Color.white; Shoot.GetComponentInChildren<Image>().color = Ranged ? Gold : Color.white;
            Execute.interactable = input && State.CanAttack(SelectedTarget, Ranged);
            ExecuteLabel.text = Ranged ? "사격 확정 · 탄약 1" : "근접 공격 확정";
            Hint.text = Busy ? "행동 결과를 확인하고 있습니다." : Ranged && State.PlayerTurn && arrival.Inventory.CountFor(actor.Person, "ammo") == 0 ? "이 대원의 가방에 탄약이 없습니다. 근접 또는 방어를 선택하세요."
                : State.PlayerTurn && !Ranged && actor.Depth != 0 ? "근접 공격은 전열에서 가능합니다. 빈 칸으로 이동하세요."
                : State.Moved ? "위치 변경 완료 · 행동 1회 남음" : "빈 칸으로 위치 변경 1회 + 행동 1회 · 적 칸을 눌러 대상 선택";
            Feedback.text = State.Message;
            for (int i = 0; i < Cells.Length; i++)
            {
                int side = i / 9, lane = i % 9 / 3, depth = i % 3;
                bool selected = targetValid && side == 1 && State.Units[SelectedTarget].Depth == depth && State.Units[SelectedTarget].Lane == lane;
                bool current = side == (actor.Enemy ? 1 : 0) && actor.Depth == depth && actor.Lane == lane;
                Cells[i].Tint(selected ? new Color(.94f, .51f, .38f, .2f) : current ? new Color(1, .83f, .4f, .16f) : side == 0 && input && State.CanMove(depth, lane) ? new Color(.62f, .83f, .72f, .12f) : Color.clear);
                CellButtons[i].interactable = input;
            }
        }
    }
}

