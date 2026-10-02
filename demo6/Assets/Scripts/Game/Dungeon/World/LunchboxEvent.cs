using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 사건 '광부 도시락통'(3차 초안 2-5 사건 표, 1~5층). F로 창("event")을 열어 [연다] / [둔다]를 고른다(키 1·2도 됨).
    /// 연다: 50% 회복 구슬 2, 50% 굴쥐 3(깨어 있는 채, 무리 없음). 둔다: 그냥 지나간다.
    /// 어느 쪽이든 고르면 사건을 본 것으로 적는다(프로필에 한 번, 경험치 10U는 PlayerProgress). Esc는 고르지 않고 닫는다.
    /// </summary>
    public sealed class LunchboxEvent : Interactable
    {
        public const string ModalName = "event";
        const string Situation = "녹슨 도시락통이 놓여 있다. 안에서 무언가 바스락거린다.";
        const float OrbChance = 0.5f;
        const float PanelWidth = 560f;
        const float PanelHeight = 236f;

        static readonly Color BoxColor = new Color(0.46f, 0.34f, 0.24f);
        static readonly Color LidColor = new Color(0.55f, 0.42f, 0.3f);
        static readonly Color BandColor = new Color(0.3f, 0.29f, 0.28f);
        static readonly Color InsideColor = new Color(0.12f, 0.1f, 0.08f);

        static readonly Vector2[] RatOffsets = { new Vector2(1.3f, 0.7f), new Vector2(1.3f, -0.7f), new Vector2(-1.1f, 0.9f) };
        static readonly Vector2[] OrbOffsets = { new Vector2(-0.5f, 0.7f), new Vector2(0.5f, 0.7f) };

        string _id;
        string _label;
        bool _decided;
        bool _open;
        SpriteRenderer _lid;
        SpriteRenderer _inside;

        public override string Prompt => "광부 도시락통";
        public override bool Available => !_decided;

        public static LunchboxEvent Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("LunchboxEvent " + f.Id, pos);
            var box = go.AddComponent<LunchboxEvent>();
            box._id = f.Id;
            box._label = string.IsNullOrEmpty(f.Label) ? "광부 도시락통" : f.Label;
            box.Build(pos);
            var state = WorldProps.State;
            if (state != null && state.IsDone(f.Id)) box._decided = true;
            return box;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Box", Vector2.zero, new Vector2(0.75f, 0.45f), ShapeSprites.Square, BoxColor, order, false);
            _inside = WorldProps.Shape(transform, "Inside", new Vector2(0f, 0.02f), new Vector2(0.62f, 0.32f), ShapeSprites.Square, InsideColor, order + 1, false);
            _inside.enabled = false;
            _lid = WorldProps.Shape(transform, "Lid", new Vector2(0f, 0.08f), new Vector2(0.79f, 0.34f), ShapeSprites.Square, LidColor, order + 2, false);
            WorldProps.Shape(transform, "Band", new Vector2(0f, 0.08f), new Vector2(0.1f, 0.36f), ShapeSprites.Square, BandColor, order + 3, false);
        }

        public override void Interact()
        {
            if (_decided) return;
            if (DungeonUi.TryOpen(ModalName)) _open = true;
        }

        void Update()
        {
            if (!_open) return;
            if (DungeonUi.Modal != ModalName)
            {
                _open = false;
                return;
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(true);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(false);
            else if (kb.escapeKey.wasPressedThisFrame) CloseModal();
        }

        void CloseModal()
        {
            _open = false;
            DungeonUi.Close(ModalName);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_open) CloseModal();
        }

        void Choose(bool open)
        {
            if (_decided) return;
            _decided = true;
            CloseModal();
            Vector2 pos = transform.position;
            string result;
            if (open)
            {
                ShowOpened();
                Sfx.Play(SfxKind.Chest);
                if (Random.value < OrbChance)
                {
                    foreach (var o in OrbOffsets) HealOrb.Spawn(FreeSpot(pos, o));
                    DungeonEvents.Say("남은 주먹밥이 아직 따뜻하다…?");
                    result = "광부 도시락통: 열었다 — 회복 구슬 2";
                }
                else
                {
                    SpawnRats(pos);
                    DungeonEvents.Say("굴쥐가 튀어나왔다!");
                    result = "광부 도시락통: 열었다 — 굴쥐 3";
                }
            }
            else
            {
                DungeonEvents.Say("도시락통을 그대로 두고 지나간다");
                result = "광부 도시락통: 두고 지나갔다";
            }
            var state = WorldProps.State;
            if (state != null) state.Complete(_id, DiscoveryKind.Event, pos, _label);
            DungeonEvents.RaiseChoice(result);
        }

        /// <summary>깨어 있는 굴쥐 3마리(무리 id -1, 칸 경계 없음 → 플레이어를 쫓는다. 계약서 '모듈 사이 약속').</summary>
        static void SpawnRats(Vector2 pos)
        {
            int floor = WorldProps.Floor;
            foreach (var o in RatOffsets)
            {
                var rat = EnemySpawner.Create(MonsterKind.Rat, floor, FreeSpot(pos, o));
                if (rat && !rat.Aware) rat.Wake(false);
            }
        }

        /// <summary>벽 안이면 도시락통 자리로 물린다.</summary>
        static Vector2 FreeSpot(Vector2 pos, Vector2 offset)
        {
            Vector2 p = pos + offset;
            return Physics2D.OverlapCircle(p, 0.35f, Layers.WallMask) ? pos : p;
        }

        void ShowOpened()
        {
            if (_lid)
            {
                _lid.transform.localPosition = new Vector3(0.12f, 0.42f, 0f);
                _lid.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            }
            if (_inside) _inside.enabled = true;
        }

        void OnGUI()
        {
            if (!_open) return;
            if (DungeonUi.Modal != ModalName)
            {
                _open = false;
                return;
            }
            var prevMatrix = GUI.matrix;
            DungeonUi.Begin();
            GUI.depth = -20;
            var r = new Rect((DungeonUi.Width - PanelWidth) * 0.5f, (DungeonUi.Height - PanelHeight) * 0.5f, PanelWidth, PanelHeight);
            DungeonUi.Box(r, 0.93f);
            float x = r.x + 20f;
            float w = PanelWidth - 40f;
            GUI.Label(new Rect(x, r.y + 14f, w, 30f), "사건 — " + _label, DungeonUi.Title);
            GUI.Label(new Rect(x, r.y + 56f, w, 60f), Situation, DungeonUi.Label);
            float bw = (w - 20f) * 0.5f;
            bool openIt = GUI.Button(new Rect(x, r.y + 132f, bw, 46f), "[1] 연다");
            bool leaveIt = GUI.Button(new Rect(x + bw + 20f, r.y + 132f, bw, 46f), "[2] 둔다");
            GUI.Label(new Rect(x, r.yMax - 36f, w, 22f), "Esc: 고르지 않고 닫기", DungeonUi.Small);
            GUI.matrix = prevMatrix;
            if (openIt) Choose(true);
            else if (leaveIt) Choose(false);
        }
    }
}
