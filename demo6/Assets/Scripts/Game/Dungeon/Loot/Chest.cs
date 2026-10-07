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
    /// 장비 부위(장비 문서 8-2): 1~4층 그 층 보장 상자가 아닌 첫 쇠 궤짝은 빈 자리 부위로, 3층 "epic"은 무기·갑옷 가운데 약한 쪽으로. 궤짝 씨앗은 (원정, id) 그대로.
    /// 그림은 빛을 받아 등잔 빛 안에서만 보인다. 쇠 궤짝만 아주 희미한 반짝임(빛 무시)을 가끔 낸다(막다른 곳 단서).
    /// 룬(기획/세-무기-우클릭-소켓-1차.md 6-3): 나무 15‰·쇠 100‰. 같은 궤짝 씨앗에 따로 흐르는 난수(RuneRules.ChestStream)라 기존 보상 굴림은 그대로다.
    /// 구석 보상(기획/1-2층-탐험-맛-1차.md 4-7·4-9, 생성 덧칠 FloorSpice가 Param에 표시): 나무 'wage' = 광부 품삯 궤짝(놋쇠빛 띠·자물쇠,
    /// CornerLoot.RollWage + 회복 구슬 1), 나무 'keepsake' = 광부 유품 상자(0.8배·어두운 나무, CornerLoot.RollKeepsake + 유품 글 한 줄),
    /// 쇠 'buried' = 흙 묻은 쇠 궤짝(흙더미 도형을 덮음, 2.0초·웅크리면 3.0초 눌러 파내면 반경 12 큰 소리 뒤 바로 열림, 보상은 쇠 궤짝 그대로).
    /// 'rare-weapon'·Epic 보장 상자, 룬 굴림, 빈 자리 채우기 규칙은 그대로다. 그림 자리(사용자가 바꿔 끼울 곳): BuildVisual의 색, BuildDirt(흙더미).
    /// </summary>
    public sealed class Chest : Interactable
    {
        const ulong RngStream = 41;
        /// <summary>기름 병 굴림 흐름(묶음 5-6).</summary>
        const ulong OilStream = 79;
        const float RatSpawnRadius = 1.3f;

        static readonly Color WoodBody = new Color(0.43f, 0.37f, 0.30f);
        static readonly Color WoodLid = new Color(0.53f, 0.45f, 0.36f);
        static readonly Color WoodBand = new Color(0.21f, 0.22f, 0.20f);
        static readonly Color IronBody = new Color(0.40f, 0.42f, 0.39f);
        static readonly Color IronLid = new Color(0.51f, 0.53f, 0.49f);
        static readonly Color IronBand = new Color(0.22f, 0.24f, 0.22f);
        static readonly Color Lock = new Color(0.62f, 0.54f, 0.3f);
        static readonly Color Inside = new Color(0.07f, 0.05f, 0.04f);
        static readonly Color Outline = new Color(0.10f, 0.11f, 0.10f);
        // 1-2층 탐험 맛 1차 4-7: 품삯 궤짝은 띠·자물쇠를 놋쇠빛으로, 유품 상자는 어두운 나무로.
        static readonly Color WageBand = new Color(0.60f, 0.48f, 0.26f);
        static readonly Color WageLock = new Color(0.80f, 0.66f, 0.34f);
        static readonly Color KeepsakeBody = new Color(0.30f, 0.24f, 0.19f);
        static readonly Color KeepsakeLid = new Color(0.37f, 0.30f, 0.24f);
        // 4-9 흙 묻은 쇠 궤짝: 흔적(ExpeditionTraces) 흙더미와 같은 색 값.
        static readonly Color DirtShade = new Color(0.16f, 0.12f, 0.09f, 0.9f);
        static readonly Color DirtLoose = new Color(0.35f, 0.27f, 0.2f);
        static readonly Color DirtTop = new Color(0.44f, 0.35f, 0.26f);
        static readonly Color DirtPebble = new Color(0.36f, 0.34f, 0.31f);
        /// <summary>유품 상자 크기 배율(4-7).</summary>
        const float KeepsakeScale = 0.8f;
        /// <summary>품삯 궤짝 회복 구슬이 놓이는 거리(플레이어 쪽).</summary>
        const float WageOrbDistance = 0.7f;

        string _id;
        string _label;
        string _param;
        bool _iron;
        bool _opened;
        Transform _lid;
        SpriteRenderer _pngLid;
        Sprite _pngOpenedLid;
        SpriteRenderer _inside;
        SpriteRenderer _lock;
        Transform _glint;
        SpriteRenderer[] _glintSprites;
        float _glintClock;
        /// <summary>흙 묻은 쇠 궤짝이 아직 흙에 덮여 있는가(4-9). 파내면 거짓.</summary>
        bool _buried;
        /// <summary>흙더미 도형(파내면 지운다).</summary>
        Transform _dirt;

        public bool Iron => _iron;
        public bool Opened => _opened;
        /// <summary>자리 표시 Param(덧칠 표시 'wage'·'keepsake'·'buried', 보장 상자 'rare-weapon'·'epic'). F1 칸 설명에 없어 확인할 때 읽는다.</summary>
        public string Param => _param;
        /// <summary>흙 묻은 쇠 궤짝이 아직 흙에 덮여 있는가.</summary>
        public bool Buried => _buried;

        /// <summary>광부 품삯 궤짝(나무 + CornerLoot.WageParam, 4-7 ①).</summary>
        bool IsWage => !_iron && _param == CornerLoot.WageParam;
        /// <summary>광부 유품 상자(나무 + CornerLoot.KeepsakeParam, 4-7 ②).</summary>
        bool IsKeepsake => !_iron && _param == CornerLoot.KeepsakeParam;

        /// <summary>보이는 이름: 구석 보상 표시가 있으면 그 이름(ExploreText), 아니면 자리 표시 이름.</summary>
        string DisplayLabel
        {
            get
            {
                if (IsWage) return ExploreText.WageLabel;
                if (IsKeepsake) return ExploreText.KeepsakeLabel;
                if (_iron && _param == CornerLoot.BuriedParam) return ExploreText.BuriedLabel;
                return string.IsNullOrEmpty(_label) ? (_iron ? "쇠 궤짝" : "나무 궤짝") : _label;
            }
        }

        public override string Prompt
        {
            get
            {
                if (_buried) return ExploreText.BuriedPrompt;
                if (IsWage) return ExploreText.WagePrompt;
                if (IsKeepsake) return ExploreText.KeepsakePrompt;
                return DisplayLabel + " 열기";
            }
        }

        public override bool Available => !_opened;

        /// <summary>흙 묻은 쇠 궤짝 파내기(4-9): 서서 2.0초, 웅크리면 3.0초. 다른 궤짝은 바로 연다.</summary>
        public override float HoldSeconds
        {
            get
            {
                if (!_buried || _opened) return 0f;
                var player = PlayerController.Instance;
                return player && player.Crouching ? CornerLoot.BuriedDigCrouchSeconds : CornerLoot.BuriedDigSeconds;
            }
        }

        public static Chest Create(DungeonCell cell, CellFeature f, Vector2 pos, bool iron)
        {
            var go = new GameObject(iron ? "Chest (쇠)" : "Chest (나무)");
            go.transform.position = pos;
            var chest = go.AddComponent<Chest>();
            chest._iron = iron;
            chest._id = f != null ? f.Id : null;
            chest._label = f != null ? f.Label : "";
            chest._param = f != null ? f.Param : "";
            chest._buried = iron && chest._param == CornerLoot.BuriedParam;
            chest.BuildVisual();

            var root = DungeonRoot.Instance;
            if (root != null && root.State != null && root.State.IsDone(chest._id)) chest.ShowOpened();
            return chest;
        }

        /// <summary>빛을 받는 상자 몸통·뚜껑·띠·자물쇠. YSort로 앞뒤 겹침을 맞춘다.</summary>
        void BuildVisual()
        {
            if (TryBuildPngVisual()) return;
            var body = _iron ? IronBody : IsKeepsake ? KeepsakeBody : WoodBody;
            var lid = _iron ? IronLid : IsKeepsake ? KeepsakeLid : WoodLid;
            var band = _iron ? IronBand : IsWage ? WageBand : WoodBand;
            float w = _iron ? 1.4f : 1.3f;
            float h = 0.8f;
            float lidW = w * 0.94f;
            float lidH = h * 0.86f;
            var t = transform;
            var bodyFrame = LootVisuals.Part(t, "Body", ShapeSprites.Square, Outline, -1, new Vector2(0f, 0f), new Vector2(w, h));
            LootVisuals.Part(bodyFrame.transform, "Body face", ShapeSprites.Square, body, 0, Vector2.zero, new Vector2(1f - 0.12f / w, 1f - 0.12f / h));
            _inside = LootVisuals.Part(t, "Inside", ShapeSprites.Square, Inside, 1, Vector2.zero, new Vector2(w * 0.84f, h * 0.68f));
            _inside.enabled = false;
            // A closed lid covers the footprint in plan view; the existing opening state foreshortens its hinge edge.
            _lid = LootVisuals.Part(t, "Lid", ShapeSprites.Square, Outline, 1, Vector2.zero, new Vector2(lidW, lidH)).transform;
            LootVisuals.Part(_lid, "Lid face", ShapeSprites.Square, lid, 2, Vector2.zero, new Vector2(1f - 0.08f / lidW, 1f - 0.08f / lidH));
            LootVisuals.Part(_lid, "BandL", ShapeSprites.Square, band, 3, new Vector2(-0.3f, 0f), new Vector2(0.1f / lidW, 0.98f));
            LootVisuals.Part(_lid, "BandR", ShapeSprites.Square, band, 3, new Vector2(0.3f, 0f), new Vector2(0.1f / lidW, 0.98f));
            if (_iron)
            {
                // 쇠 궤짝: 아래 띠와 귀퉁이 못으로 나무와 구별.
                LootVisuals.Part(_lid, "BandBottom", ShapeSprites.Square, band, 3, new Vector2(0f, -0.42f), new Vector2(1f, 0.08f / lidH));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1f : 1f) * 0.44f;
                    float y = (i < 2 ? -1f : 1f) * 0.3f;
                    LootVisuals.Part(_lid, "Rivet", ShapeSprites.Circle, band, 4, new Vector2(x, y), new Vector2(0.08f / lidW, 0.08f / lidH));
                }
            }
            _lock = LootVisuals.Part(_lid, "Lock", ShapeSprites.Square, IsWage ? WageLock : Lock, 4, new Vector2(0f, -0.36f), new Vector2(0.16f / lidW, 0.16f / lidH));
            // 흙 묻은 쇠 궤짝은 흙에 덮여 반짝이지 않는다(파낸 뒤 바로 열린다).
            if (_iron && !_buried)
            {
                _glint = LootVisuals.BuildSparkle(t, "Glint", new Color(1f, 0.97f, 0.88f, 0f), 5, new Vector2(w * 0.38f, h * 0.45f), 0.3f);
                _glintSprites = _glint.GetComponentsInChildren<SpriteRenderer>(true);
                _glintClock = UnityEngine.Random.Range(0f, 3f);
            }
            if (_buried) BuildDirt(w, h);
            // YSort는 붙는 순간의 렌더러를 모으므로 흙더미까지 지은 뒤에 붙인다.
            gameObject.AddComponent<YSort>();
            if (IsKeepsake) t.localScale = Vector3.one * KeepsakeScale;
        }

        // v062: only the view changes; all variants keep their original state, range, rewards and root size.
        bool TryBuildPngVisual()
        {
            string variant = _iron ? "iron" : IsWage ? "wage" : "wood";
            string lockId = _iron ? "iron-lock" : "wood-lock";
            if (!PropsV062Art.TryLoad(new[] {variant + "-base", variant + "-lid", variant + "-lid-open", lockId}, out var sprites)) return false;
            float w = _iron ? 1.4f : 1.3f, h = .8f;
            float lidW = w * .94f, lidH = h * .86f;
            Color bodyTint = IsKeepsake ? new Color(.70f, .65f, .63f, 1f) : Color.white;
            Color lidTint = IsKeepsake ? new Color(.70f, .67f, .67f, 1f) : Color.white;
            // Base PNG already contains the cavity. The closed lid covers it, revealing it when opened.
            PropsV062Art.Part(transform, "Body", sprites[0], Vector2.zero, new Vector2(w, h), bodyTint, 0);
            var lid = PropsV062Art.Part(transform, "Lid", sprites[1], Vector2.zero, new Vector2(lidW, lidH), lidTint, 1);
            _lid = lid.Carrier;
            _pngLid = lid.Renderer;
            _pngOpenedLid = sprites[2];
            _lock = PropsV062Art.Part(_lid, "Lock", sprites[3], new Vector2(0f, -.36f),
                new Vector2(.16f / lidW, .16f / lidH), Color.white, 4).Renderer;
            if (_iron && !_buried)
            {
                _glint = LootVisuals.BuildSparkle(transform, "Glint", new Color(1f, .97f, .88f, 0f), 5, new Vector2(w * .38f, h * .45f), .3f);
                _glintSprites = _glint.GetComponentsInChildren<SpriteRenderer>(true);
                _glintClock = UnityEngine.Random.Range(0f, 3f);
            }
            if (_buried) BuildDirt(w, h);
            // Register every renderer once, before YSort starts using absolute world orders.
            gameObject.AddComponent<YSort>();
            if (IsKeepsake) transform.localScale = Vector3.one * KeepsakeScale;
            return true;
        }

        /// <summary>
        /// 흙 묻은 쇠 궤짝의 흙더미(4-9, 빛을 받는 도형): 그늘 → 흙 몸통 → 덩이 → 밝은 윗면 → 잔돌. 뚜껑 오른쪽 귀만 조금 드러난다.
        /// 모양은 자리 id로 정해 같은 궤짝이면 같다.
        /// </summary>
        void BuildDirt(float w, float h)
        {
            _dirt = new GameObject("Dirt").transform;
            _dirt.SetParent(transform, false);
            var rng = new System.Random((int)(LootRules.StableHash(_id ?? "buried") & 0x7FFFFFFF));
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            LootVisuals.Part(_dirt, "DirtShade", ShapeSprites.Circle, DirtShade, 6, new Vector2(0f, -h * 0.12f), new Vector2(w * 1.45f, h * 1.15f));
            LootVisuals.Part(_dirt, "Dirt", ShapeSprites.Circle, DirtLoose, 7, new Vector2(-w * 0.04f, -h * 0.08f), new Vector2(w * 1.3f, h * 0.98f));
            float[,] lumps = { { -0.32f, 0.12f, 0.62f, 0.5f }, { 0.18f, -0.2f, 0.66f, 0.46f }, { -0.1f, 0.3f, 0.5f, 0.34f }, { 0.36f, 0.05f, 0.4f, 0.32f } };
            for (int i = 0; i < lumps.GetLength(0); i++)
                LootVisuals.Part(_dirt, "Lump", ShapeSprites.Circle, Color.Lerp(DirtLoose, DirtTop, R(0f, 0.4f)), 8,
                    new Vector2(w * lumps[i, 0] + R(-0.05f, 0.05f), h * lumps[i, 1] + R(-0.04f, 0.04f)), new Vector2(w * lumps[i, 2], h * lumps[i, 3]), R(-20f, 20f));
            for (int i = 0; i < 3; i++)
                LootVisuals.Part(_dirt, "DirtTop", ShapeSprites.Circle, DirtTop, 9, new Vector2(R(-w * 0.4f, w * 0.25f), R(0f, h * 0.3f)),
                    new Vector2(R(0.18f, 0.32f), R(0.1f, 0.18f)), R(-30f, 30f));
            for (int i = 0; i < 4; i++)
            {
                float s = R(0.08f, 0.16f);
                LootVisuals.Part(_dirt, "Pebble", i % 2 == 0 ? ShapeSprites.Square : ShapeSprites.Circle, DirtPebble, 9,
                    new Vector2(R(-w * 0.6f, w * 0.6f), R(-h * 0.55f, -h * 0.2f)), new Vector2(s, s * R(0.7f, 1f)), R(0f, 360f));
            }
        }

        /// <summary>이미 연 모습: 뚜껑을 뒤로 젖히고 속을 보인다.</summary>
        void ShowOpened()
        {
            _opened = true;
            _buried = false;
            if (_dirt) Destroy(_dirt.gameObject);
            _dirt = null;
            if (_lid)
            {
                _lid.localPosition = new Vector3(0f, _pngLid ? .43f : .62f, 0f);
                _lid.localScale = new Vector3(_lid.localScale.x, 0.14f, 1f);
            }
            if (_pngLid) PropsV062Art.SetSprite(_pngLid, _pngOpenedLid);
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

        /// <summary>층 보장 상자(1층 '희귀 이상 무기', 3층 '영웅 이상')인가.</summary>
        bool Guaranteed => _param == LootRules.RareWeaponParam || _param == LootRules.EpicParam;

        /// <summary>이 장면(이번 층 방문)에서 보장 상자가 아닌 쇠 궤짝을 이미 열었는가(빈 자리 채우기는 그 층 첫 쇠 궤짝 한 번).</summary>
        static bool PlainIronOpenedElsewhere(Chest self)
        {
            foreach (var it in Interactable.All)
                if (it is Chest c && c != self && c._iron && c._opened && !c.Guaranteed) return true;
            return false;
        }

        /// <summary>
        /// 장비 굴림 조건(장비 문서 8-2): 부위 보정·가진 전설(Inventory.RollContext)에 더해
        /// 빈 자리 채우기 = 1~4층에서 그 층 보장 상자가 아닌 첫 쇠 궤짝이고 빈 자리가 있으면 그 빈 부위들 가운데서만(묶음 안 비율은 RollGear),
        /// 3층 '영웅 이상' = 무기·갑옷 가운데 장비 점수(GearMath.ItemScore)가 낮은 쪽(같으면 무기). 1층 '희귀 이상 무기'는 LootRules가 무기로 정한다.
        /// </summary>
        GearRollContext RollContext(int floor, bool firstPlainIron)
        {
            var inv = Inventory.Instance;
            var ctx = inv ? inv.RollContext() : new GearRollContext();
            if (!_iron || _param == LootRules.RareWeaponParam) return ctx;
            if (_param == LootRules.EpicParam)
            {
                var eq = inv ? inv.Equipment : null;
                double weapon = eq != null ? GearMath.ItemScore(eq[GearSlot.Weapon]) : 0;
                double armor = eq != null ? GearMath.ItemScore(eq[GearSlot.Armor]) : 0;
                ctx.OnlyParts = new[] { armor < weapon ? GearPart.Armor : GearPart.Weapon };
            }
            else if (firstPlainIron && floor >= 1 && floor <= FillEmptyMaxFloor && inv)
            {
                var empty = inv.EmptyParts();
                if (empty.Length > 0) ctx.OnlyParts = empty;
            }
            return ctx;
        }

        /// <summary>빈 자리 채우기가 듣는 가장 깊은 층(8-2: 1~4층).</summary>
        const int FillEmptyMaxFloor = 4;

        public override void Interact()
        {
            if (_opened) return;
            // 4-9 흙 묻은 쇠 궤짝: 처음 누르기를 다 채우면 흙을 걷고(큰 소리) 같은 자리에서 바로 쇠 궤짝으로 연다.
            if (_buried) Dig();
            var root = DungeonRoot.Instance;
            bool firstPlainIron = _iron && !Guaranteed && !PlainIronOpenedElsewhere(this);
            ShowOpened();
            Sfx.Play(SfxKind.Chest);
            Vector2 pos = transform.position;
            string label = DisplayLabel;
            int floor = root != null ? root.Floor : 1;
            int expedition = 1;
            if (root != null && root.State != null)
            {
                if (!string.IsNullOrEmpty(_id)) root.State.Complete(_id, _iron ? DiscoveryKind.IronChest : DiscoveryKind.WoodChest, pos, label);
                expedition = root.State.Expedition;
            }

            ulong salt = root != null && root.State != null ? root.State.RunSalt * 0x9E3779B97F4A7C15UL : 0UL;
            ulong seed = LootRules.ChestSeed(expedition, _id ?? label) ^ salt;
            var rng = new Pcg32Random(seed, RngStream);
            var context = RollContext(floor, firstPlainIron);
            // 4-7 구석 보상: 품삯·유품은 CornerLoot 굴림(같은 궤짝 씨앗·난수 흐름), 그 밖(흙 묻은 쇠 궤짝 포함)은 지금 궤짝 굴림 그대로.
            LootBundle bundle;
            if (IsWage) bundle = CornerLoot.RollWage(floor, rng, context);
            else if (IsKeepsake) bundle = CornerLoot.RollKeepsake(floor, rng, context);
            else bundle = LootRules.RollChest(_iron, floor, _param, rng, context);
            RuneRules.AddTo(bundle, _iron ? RuneSource.IronChest : RuneSource.WoodChest, new Pcg32Random(seed, RuneRules.ChestStream));
            var player = PlayerController.Instance;
            Vector2 toward = player ? player.Position - pos : Vector2.down;
            LootSpawner.Spawn(bundle, pos, toward, 0.1f);
            // 나무 궤짝 바닥의 기름 병(묶음 5-6): 35%, 같은 궤짝 씨앗으로 정한다.
            if (!_iron && root != null && root.Leg != null && new Pcg32Random(seed, OilStream).NextDouble() < DownRules.ChestOilChance)
                OilFlask.Place(pos + (toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector2.down) * 0.9f + new Vector2(0.45f, 0f));

            if (IsWage)
            {
                // 품삯 궤짝: 회복 구슬 확정(플레이어 쪽 0.7). 묶음 5(빛을 개수로)가 들어오면 '기름 병 또는 회복 구슬'.
                Vector2 dir = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector2.down;
                for (int i = 0; i < CornerLoot.WageHealOrbs; i++) HealOrb.Spawn(LootSpawner.SafeLanding(pos, dir, WageOrbDistance));
                DungeonEvents.Say(ExploreText.WageOpened);
            }
            else if (IsKeepsake) DungeonEvents.Say(ExploreText.KeepsakeLine(seed));

            if (bundle.RatChest) SpawnRats(pos, toward, floor);
        }

        /// <summary>
        /// 흙 걷기(4-9): 흙더미를 지우고 흙 조각을 튀기고, 반경 12 큰 소리(웅크렸으면 CellEncounters가 지금 규칙대로 × 0.3)와 알림 한 줄.
        /// </summary>
        void Dig()
        {
            _buried = false;
            Vector2 pos = transform.position;
            if (_dirt) Destroy(_dirt.gameObject);
            _dirt = null;
            var player = PlayerController.Instance;
            Vector2 away = player ? pos - player.Position : Vector2.up;
            WorldDebris.Burst(pos, new Vector2(1.6f, 0.9f), away, DirtLoose, DirtTop, 12);
            Sfx.PlayScaled(SfxKind.WallBreak, 0.6f, 0.8f);
            DungeonEvents.RaiseNoise(pos, CornerLoot.BuriedNoiseRadius);
            DungeonEvents.Say(ExploreText.BuriedDug);
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
