using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 이야기 물건(3차 초안 2-5): 광부 명패(F 1초로 줍고 사라짐, 마을 등불이 하나 꺼짐)와 쪽지(F로 창 "note"에 글을 띄움, 처음 읽을 때 기록).
    /// 2-8 '이야기 물건 흰 등불 20': 빛을 무시하는 작은 흰 불빛이 20유닛 안에서 보인다. 경험치 3U는 PlayerProgress.
    /// 화면 글에는 인물 이름을 쓰지 않는다(화자 이름 공개 규칙).
    /// </summary>
    public sealed class StoryItem : Interactable
    {
        public const string ModalName = "note";
        const float GlowRange = 20f;
        const float PanelWidth = 560f;
        const float PanelHeight = 300f;

        static readonly Color Brass = new Color(0.66f, 0.56f, 0.34f);
        static readonly Color BrassDark = new Color(0.4f, 0.33f, 0.2f);
        static readonly Color Paper = new Color(0.78f, 0.74f, 0.64f);
        static readonly Color Ink = new Color(0.35f, 0.32f, 0.28f);
        static readonly Color GlowColor = new Color(1f, 1f, 1f, 0.4f);

        string _id;
        string _label;
        string _param;
        bool _nameplate;
        bool _taken;
        bool _read;
        bool _open;
        float _seed;
        SpriteRenderer _glow;

        public override string Prompt => _nameplate ? "광부 명패 줍기" : "쪽지 읽기";
        public override float HoldSeconds => _nameplate ? 1f : 0f;
        public override bool Available => !_taken;

        public static StoryItem Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("StoryItem " + f.Id, pos);
            var item = go.AddComponent<StoryItem>();
            item._id = f.Id;
            item._nameplate = f.Kind == FeatureKind.Nameplate;
            item._label = !string.IsNullOrEmpty(f.Label) ? f.Label : item._nameplate ? "광부 명패" : "쪽지";
            item._param = f.Param ?? "";
            item._seed = Random.value * 10f;
            item.Build(pos);
            var state = WorldProps.State;
            if (state != null && state.IsDone(f.Id))
            {
                if (item._nameplate)
                {
                    item._taken = true;
                    go.SetActive(false);
                }
                else item._read = true;
            }
            return item;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            if (_nameplate)
            {
                WorldProps.Shape(transform, "Plate", Vector2.zero, new Vector2(0.5f, 0.3f), ShapeSprites.Square, Brass, order, false, -8f);
                WorldProps.Shape(transform, "Engrave", new Vector2(0f, 0.02f), new Vector2(0.32f, 0.05f), ShapeSprites.Square, BrassDark, order + 1, false, -8f);
                WorldProps.Shape(transform, "Hole", new Vector2(-0.18f, 0.06f), new Vector2(0.06f, 0.06f), ShapeSprites.Circle, BrassDark, order + 1, false);
            }
            else
            {
                WorldProps.Shape(transform, "Paper", Vector2.zero, new Vector2(0.45f, 0.34f), ShapeSprites.Square, Paper, order, false, 6f);
                for (int i = 0; i < 3; i++)
                    WorldProps.Shape(transform, "Line", new Vector2(-0.02f, 0.08f - i * 0.08f), new Vector2(0.3f, 0.025f), ShapeSprites.Square, Ink, order + 1, false, 6f);
            }
            _glow = WorldProps.Shape(transform, "WhiteLamp", new Vector2(0f, 0.35f), new Vector2(0.5f, 0.5f), WorldProps.SoftDot, GlowColor, order + 2, true);
            _glow.enabled = false;
        }

        public override void Interact()
        {
            if (_taken) return;
            if (_nameplate)
            {
                TakeNameplate();
                return;
            }
            if (!DungeonUi.TryOpen(ModalName)) return;
            _open = true;
            if (_read) return;
            _read = true;
            Sfx.Play(SfxKind.Pickup);
            var state = WorldProps.State;
            if (state != null) state.Complete(_id, DiscoveryKind.Story, transform.position, _label);
        }

        void TakeNameplate()
        {
            _taken = true;
            Vector2 pos = transform.position;
            Sfx.Play(SfxKind.Pickup);
            var state = WorldProps.State;
            if (state != null) state.Complete(_id, DiscoveryKind.Story, pos, _label);
            WorldOverlay.Text(pos + Vector2.up * 1.1f, "광부 명패", Color.white);
            DungeonEvents.Say("광부 명패를 주웠다 — 마을에 맡기면 등불 하나가 꺼진다");
            Destroy(gameObject);
        }

        /// <summary>쪽지 글(인물 이름 없음). note1 = 곡괭이방 광부의 메모, note2 = 광업소 출입 장부 한 장.</summary>
        string NoteText()
        {
            switch (_param)
            {
                case "note1":
                    return "— 곡괭이방 벽 틈에 끼워 둔 쪽지 —\n\n교대 끝. 아래 갱이 또 울린다.\n반장은 괜찮다고 했지만 다들 등잔 기름을 한 통씩 더 챙겼다.\n내일은 우리 조가 내려간다. 돌아오면 이 방 궤짝에 품삯을 넣어 두겠다.";
                case "note2":
                    return "— 광업소 출입 장부 한 장 —\n\n교대조 열두 명, 아래로 내려감.\n'올라옴' 칸은 비어 있다.\n누군가 그 칸에 '스스로'라고 적었다가 지운 자국이 남아 있다.";
                default:
                    return "글씨가 번져 읽을 수 없다.";
            }
        }

        void Update()
        {
            if (_open)
            {
                if (DungeonUi.Modal != ModalName) _open = false;
                else
                {
                    var kb = Keyboard.current;
                    if (kb != null && kb.escapeKey.wasPressedThisFrame) CloseModal();
                }
            }
            if (!_glow) return;
            bool show = !_taken && WorldProps.PlayerDistance(transform.position) <= GlowRange;
            _glow.enabled = show;
            if (!show) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.8f + _seed);
            var c = GlowColor;
            c.a = 0.22f + 0.2f * pulse;
            _glow.color = c;
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
            DungeonUi.Box(r, 0.94f);
            float x = r.x + 24f;
            float w = PanelWidth - 48f;
            GUI.Label(new Rect(x, r.y + 14f, w, 30f), _label, DungeonUi.Title);
            GUI.Label(new Rect(x, r.y + 52f, w, PanelHeight - 120f), NoteText(), DungeonUi.Label);
            bool close = GUI.Button(new Rect(x, r.yMax - 58f, w, 40f), "닫기 (Esc)");
            GUI.matrix = prevMatrix;
            if (close) CloseModal();
        }
    }
}
