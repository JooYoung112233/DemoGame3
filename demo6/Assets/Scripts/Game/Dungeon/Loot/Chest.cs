using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 나무·쇠 궤짝(3차 초안 2-5, 프로필에 한 번). F로 열면 뚜껑이 열리고 보상(Core LootRules가 확정)이 플레이어 쪽으로 튀어나온다.
    /// 나무: 장비 10%, 강화석 25%로 1개, 골드 4 + 2 × 층, 10%는 '쥐 궤짝'(굴쥐 3, 보상은 그대로).
    /// 쇠: 장비 1개 확정, 강화석 max(1, 층 − 2), 골드 6 + 3 × 층. Param "rare-weapon"(1층 숨은 방 H)은 희귀 이상 무기.
    /// 그림은 빛을 받아 등잔 빛 안에서만 보인다. 쇠 궤짝만 아주 희미한 반짝임(빛 무시)을 가끔 낸다(막다른 곳 단서).
    /// </summary>
    public sealed class Chest : Interactable
    {
        const ulong RngStream = 41;
        const float RatSpawnRadius = 1.3f;

        static readonly Color WoodBody = new Color(0.45f, 0.3f, 0.17f);
        static readonly Color WoodLid = new Color(0.37f, 0.24f, 0.13f);
        static readonly Color WoodBand = new Color(0.28f, 0.18f, 0.1f);
        static readonly Color IronBody = new Color(0.24f, 0.26f, 0.29f);
        static readonly Color IronLid = new Color(0.19f, 0.21f, 0.23f);
        static readonly Color IronBand = new Color(0.45f, 0.48f, 0.52f);
        static readonly Color Lock = new Color(0.62f, 0.54f, 0.3f);
        static readonly Color Inside = new Color(0.07f, 0.05f, 0.04f);

        string _id;
        string _label;
        string _param;
        bool _iron;
        bool _opened;
        Transform _lid;
        SpriteRenderer _inside;
        SpriteRenderer _lock;
        Transform _glint;
        SpriteRenderer[] _glintSprites;
        float _glintClock;

        public bool Iron => _iron;
        public bool Opened => _opened;

        public override string Prompt => (string.IsNullOrEmpty(_label) ? (_iron ? "쇠 궤짝" : "나무 궤짝") : _label) + " 열기";
        public override bool Available => !_opened;

        public static Chest Create(DungeonCell cell, CellFeature f, Vector2 pos, bool iron)
        {
            var go = new GameObject(iron ? "Chest (쇠)" : "Chest (나무)");
            go.transform.position = pos;
            var chest = go.AddComponent<Chest>();
            chest._iron = iron;
            chest._id = f != null ? f.Id : null;
            chest._label = f != null ? f.Label : "";
            chest._param = f != null ? f.Param : "";
            chest.BuildVisual();

            var root = DungeonRoot.Instance;
            if (root != null && root.State != null && root.State.IsDone(chest._id)) chest.ShowOpened();
            return chest;
        }

        /// <summary>빛을 받는 상자 몸통·뚜껑·띠·자물쇠. YSort로 앞뒤 겹침을 맞춘다.</summary>
        void BuildVisual()
        {
            var body = _iron ? IronBody : WoodBody;
            var lid = _iron ? IronLid : WoodLid;
            var band = _iron ? IronBand : WoodBand;
            float w = _iron ? 1.4f : 1.3f;
            float h = 0.8f;
            var t = transform;
            LootVisuals.Part(t, "Body", ShapeSprites.Square, body, 0, new Vector2(0f, 0f), new Vector2(w, h));
            _inside = LootVisuals.Part(t, "Inside", ShapeSprites.Square, Inside, 1, new Vector2(0f, h * 0.36f), new Vector2(w * 0.86f, h * 0.2f));
            _inside.enabled = false;
            _lid = LootVisuals.Part(t, "Lid", ShapeSprites.Square, lid, 2, new Vector2(0f, h * 0.38f), new Vector2(w * 1.05f, h * 0.32f)).transform;
            LootVisuals.Part(t, "BandL", ShapeSprites.Square, band, 3, new Vector2(-w * 0.3f, 0f), new Vector2(0.1f, h * 0.98f));
            LootVisuals.Part(t, "BandR", ShapeSprites.Square, band, 3, new Vector2(w * 0.3f, 0f), new Vector2(0.1f, h * 0.98f));
            if (_iron)
            {
                // 쇠 궤짝: 아래 띠와 귀퉁이 못으로 나무와 구별.
                LootVisuals.Part(t, "BandBottom", ShapeSprites.Square, band, 3, new Vector2(0f, -h * 0.42f), new Vector2(w, 0.08f));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1f : 1f) * w * 0.44f;
                    float y = (i < 2 ? -1f : 1f) * h * 0.3f;
                    LootVisuals.Part(t, "Rivet", ShapeSprites.Circle, band, 4, new Vector2(x, y), Vector2.one * 0.08f);
                }
            }
            _lock = LootVisuals.Part(t, "Lock", ShapeSprites.Square, Lock, 4, new Vector2(0f, h * 0.15f), new Vector2(0.16f, 0.2f));
            if (_iron)
            {
                _glint = LootVisuals.BuildSparkle(t, "Glint", new Color(1f, 0.97f, 0.88f, 0f), 5, new Vector2(w * 0.38f, h * 0.45f), 0.3f);
                _glintSprites = _glint.GetComponentsInChildren<SpriteRenderer>(true);
                _glintClock = UnityEngine.Random.Range(0f, 3f);
            }
            gameObject.AddComponent<YSort>();
        }

        /// <summary>이미 연 모습: 뚜껑을 뒤로 젖히고 속을 보인다.</summary>
        void ShowOpened()
        {
            _opened = true;
            if (_lid)
            {
                _lid.localPosition = new Vector3(0f, 0.62f, 0f);
                _lid.localScale = new Vector3(_lid.localScale.x, 0.14f, 1f);
            }
            if (_inside) _inside.enabled = true;
            if (_lock) _lock.enabled = false;
            if (_glint) Destroy(_glint.gameObject);
            _glint = null;
        }

        void Update()
        {
            if (!_glint || _opened) return;
            // 쇠 궤짝의 희미한 반짝임: 3.2초마다 0.4초, 최대 알파 0.35.
            _glintClock += Time.deltaTime;
            float cycle = Mathf.Repeat(_glintClock, 3.2f);
            float flash = cycle < 0.4f ? Mathf.Sin(cycle / 0.4f * Mathf.PI) : 0f;
            _glint.localScale = Vector3.one * (0.6f + 0.6f * flash);
            LootVisuals.SetAlpha(_glintSprites, 0.35f * flash);
        }

        public override void Interact()
        {
            if (_opened) return;
            var root = DungeonRoot.Instance;
            ShowOpened();
            Sfx.Play(SfxKind.Chest);
            Vector2 pos = transform.position;
            string label = string.IsNullOrEmpty(_label) ? (_iron ? "쇠 궤짝" : "나무 궤짝") : _label;
            int floor = root != null ? root.Floor : 1;
            int expedition = 1;
            if (root != null && root.State != null)
            {
                if (!string.IsNullOrEmpty(_id)) root.State.Complete(_id, _iron ? DiscoveryKind.IronChest : DiscoveryKind.WoodChest, pos, label);
                expedition = root.State.Expedition;
            }

            ulong salt = root != null && root.State != null ? root.State.RunSalt * 0x9E3779B97F4A7C15UL : 0UL;
            var rng = new Pcg32Random(LootRules.ChestSeed(expedition, _id ?? label) ^ salt, RngStream);
            var bundle = LootRules.RollChest(_iron, floor, _param, rng);
            var player = PlayerController.Instance;
            Vector2 toward = player ? player.Position - pos : Vector2.down;
            LootSpawner.Spawn(bundle, pos, toward, 0.1f);

            if (bundle.RatChest) SpawnRats(pos, toward, floor);
        }

        /// <summary>
        /// 쥐 궤짝: 깨어 있는 굴쥐 3(무리 없음, 칸 경계 없음 — 플레이어를 쫓는다).
        /// 플레이어 몸 위가 아니라 궤짝 양옆·뒤에서 튀어나와 한 박자 볼 틈을 준다.
        /// </summary>
        void SpawnRats(Vector2 pos, Vector2 toward, int floor)
        {
            if (toward.sqrMagnitude < 0.0001f) toward = Vector2.down;
            float baseAngle = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
            for (int i = 0; i < LootRules.RatChestRats; i++)
            {
                float angle = (baseAngle + 90f + i * 90f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 at = LootSpawner.SafeLanding(pos, dir, RatSpawnRadius);
                var rat = EnemySpawner.Create(MonsterKind.Rat, floor, at);
                if (!rat) continue;
                rat.GroupId = -1;
                rat.Wake(false);
            }
            DungeonEvents.Say("궤짝 속에서 굴쥐들이 쏟아져 나온다");
        }
    }
}
