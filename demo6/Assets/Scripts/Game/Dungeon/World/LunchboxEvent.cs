using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 사건 '광부 도시락통'(3차 초안 2-5 사건 표, 1~5층). F로 창("event")을 열어 [연다] / [둔다]를 고른다(키 1·2도 됨).
    /// 연다: 50% 회복 구슬 2, 50% 굴쥐 3(깨어 있는 채, 무리 없음). 둔다: 그냥 지나간다.
    /// 어느 쪽이든 고르면 이번 원정에서 사건을 본 것으로 적는다. Esc는 고르지 않고 닫는다.
    /// 원정마다 다시 나온다(매판 새 탐험 1차 2-6: 장면을 다시 불러오면 새로 놓임). 사건 경험치 10U는 층에 한 번이다(PlayerProgress, 꾸러미 EventXpFloors).
    /// 묶음 5-7(시스템-컨텐츠-다듬기-검토-1차.md): 안에 든 것은 놓일 때 정한다. 웅크려 다가가 열면 창 글이 '식은 밥 냄새' / '긁는 소리'로 갈린다.
    /// 뚜껑을 열면 반경 12의 큰 소리(웅크림 규칙은 소리를 듣는 쪽이 그대로 적용), 나온 굴쥐는 보상이 없다.
    /// 그냥 두면 식은 주먹밥을 챙긴다(원정마다 1개까지, 물약이 없을 때 R로 먹으면 체력 15%).
    /// </summary>
    public sealed class LunchboxEvent : Interactable
    {
        public const string ModalName = "event";
        const string Situation = "주인 잃은 도시락통이 녹슨 채 놓여 있다.";
        const string SituationRice = "주인 잃은 도시락통이 녹슨 채 놓여 있다. 틈새로 식은 밥 냄새가 희미하게 난다.";
        const string SituationRats = "주인 잃은 도시락통이 녹슨 채 놓여 있다. 안에서 무언가 긁는 소리가 난다.";
        const string SituationHint = "(웅크려 다가가면 안의 기척을 들을 수 있다)";
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
        /// <summary>안에 굴쥐가 있는가(놓일 때 정함).</summary>
        bool _rats;
        /// <summary>창을 열 때 웅크리고 있었나(안의 기척이 들림).</summary>
        bool _listened;
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
            box._rats = Random.value >= OrbChance;
            var state = WorldProps.State;
            if (state != null && state.IsDone(f.Id)) box._decided = true;
            return box;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Box", Vector2.zero, new Vector2(0.75f, 0.45f), ShapeSprites.Square, BoxColor, order, false);
            _inside = WorldProps.Shape(transform, "Inside", Vector2.zero, new Vector2(0.62f, 0.32f), ShapeSprites.Square, InsideColor, order + 1, false);
            _inside.enabled = false;
            // The overhead lid covers the closed box footprint; its band moves with the opened lid.
            _lid = WorldProps.Shape(transform, "Lid", Vector2.zero, new Vector2(0.79f, 0.45f), ShapeSprites.Square, LidColor, order + 2, false);
            WorldProps.Shape(_lid.transform, "Band", Vector2.zero, new Vector2(0.1f / 0.79f, 0.43f / 0.45f), ShapeSprites.Square, BandColor, order + 3, false);
        }

        public override void Interact()
        {
            if (_decided) return;
            var player = PlayerController.Instance;
            _listened = player && player.Crouching;
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
                // 녹슨 뚜껑을 비틀어 여는 큰 소리(반경 12). 웅크려 열면 듣는 쪽 규칙대로 줄어든다.
                DungeonEvents.RaiseNoise(pos, DownRules.LunchboxNoise);
                // 뚜껑 안쪽에 묶인 기름 병 하나(묶음 5-6: 병은 도시락통에서도 나온다).
                OilFlask.Place(FreeSpot(pos, new Vector2(0f, -0.8f)));
                if (!_rats)
                {
                    foreach (var o in OrbOffsets) HealOrb.Spawn(FreeSpot(pos, o));
                    DungeonEvents.Say("식은 주먹밥 두 덩이 — 주인은 끝내 돌아오지 않았다");
                    result = "광부 도시락통: 열었다 — 회복 구슬 2";
                }
                else
                {
                    SpawnRats(pos);
                    DungeonEvents.Say("뚜껑을 밀치고 굴쥐들이 기어 나온다");
                    result = "광부 도시락통: 열었다 — 굴쥐 3";
                }
            }
            else
            {
                var root = DungeonRoot.Instance;
                var leg = root ? root.Leg : null;
                if (leg != null && leg.RiceBalls < DownRules.RiceBallCap)
                {
                    leg.RiceBalls++;
                    DungeonEvents.Say("뚜껑은 닫아 두고, 곁에 굴러다니던 식은 주먹밥 하나를 챙긴다 — 물약이 떨어지면 R");
                    result = "광부 도시락통: 두고 지나갔다 — 식은 주먹밥";
                }
                else
                {
                    DungeonEvents.Say(_rats ? "뚜껑은 닫아 둔다. 긁는 소리가 등 뒤에 남는다" : "뚜껑은 닫아 둔다. 주먹밥은 이미 하나 챙겼다");
                    result = "광부 도시락통: 두고 지나갔다";
                }
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
                if (!rat) continue;
                // 나온 굴쥐는 보상이 없다(묶음 5-7: '늘 연다'가 낫지 않게).
                rat.NoReward = true;
                if (!rat.Aware) rat.Wake(false);
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
            GUI.Label(new Rect(x, r.y + 14f, w-60f, 36f), "사건 — " + _label, DungeonUi.Title);
            string situation = _listened ? (_rats ? SituationRats : SituationRice) : Situation + "\n" + SituationHint;
            GUI.Label(new Rect(x, r.y + 56f, w, 60f), situation, DungeonUi.Label);
            float bw = (w - 20f) * 0.5f;
            bool openIt = GUI.Button(new Rect(x, r.y + 132f, bw, 46f), "[1] 뚜껑을 연다");
            bool leaveIt = GUI.Button(new Rect(x + bw + 20f, r.y + 132f, bw, 46f), "[2] 그냥 둔다");
            GUI.Label(new Rect(x, r.yMax - 52f, w, 28f), "Esc: 고르지 않고 닫기", DungeonUi.Small);
            bool close = DungeonUi.CloseButton(r);
            GUI.matrix = prevMatrix;
            if (openIt) Choose(true);
            else if (leaveIt) Choose(false);
            else if (close) CloseModal();
        }
    }
}
