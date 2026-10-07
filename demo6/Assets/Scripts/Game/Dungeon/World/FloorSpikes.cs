using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥 가시 덫(기획/1-2층-탐험-맛-1차.md 4-6, 규칙 TrapRules). 궁수가 놓는 덫(SpikeTrap)과 따로, 생성 덧칠(FloorSpice)이 놓은 자리 표시다.
    /// 보임: 시야 안(VisionSystem.IsVisible)이고 빛 안(내 등잔 흐린 반경 또는 켠 벽 등잔 빛)일 때 '봤다'가 되고, 한 번 보면 계속 보인다.
    /// 시야를 끄면 빛만 본다. 보기 전에는 그리지 않는다(빛을 받는 도형이라 본 뒤에도 빛 밖에서는 어둡다).
    /// 밟음: 반경 0.6 + 몸 반지름 × 0.5 안에 들면(웅크림과 상관없음) 그 층 궁수 공격의 100%, 밀림 0.4(HitKind.Trap — 방패 불가, 무기 행동 끊김).
    /// 맞았을 때만(구르기 무적이면 그냥 지나감) 덫이 사라지고 '찰칵' 큰 소리(반경 8)가 가까운 잠든 무리 하나를 깨운다.
    /// 걷어 내기: 본 덫 앞에서 웅크린 채 F를 1.2초. 서 있으면 막힘 글. 이 장면(층 방문)에서 처음 걷어 낸 덫만 강화석 1을 떨군다(CornerLoot.SpikeStones).
    /// 적은 밟지 않는다. 숨은 위험이라 DungeonState에 등록하지 않고, 상태는 이 장면에서만 기억한다(꾸러미에 남기지 않음).
    /// 그림·소리 자리(사용자가 바꿔 끼울 곳): Build(고리 + 세모 4), Stepped의 '찰칵' 소리·글, Interact의 쇳조각 효과·소리.
    /// </summary>
    public sealed class FloorSpikes : Interactable
    {
        /// <summary>쓸 수 있는 거리(4-6).</summary>
        const float UseRange = 1.6f;
        /// <summary>'봤다' 확인 간격(실제 시간). 본 뒤로는 확인하지 않는다.</summary>
        const float SeenCheckSeconds = 0.15f;
        /// <summary>시야 다각형 판정 여유(덫 몸 반경쯤).</summary>
        const float SeenPad = 0.3f;
        /// <summary>밟았을 때 머리 위 글(4-6 '찰칵'). 글은 ExploreText 한 곳에 모은다.</summary>
        const string ClickText = ExploreText.SpikeClick;

        static readonly Color BaseColor = new Color(0.16f, 0.15f, 0.13f, 0.85f);
        static readonly Color SpikeDark = new Color(0.45f, 0.42f, 0.36f);
        static readonly Color ClickColor = new Color(0.86f, 0.84f, 0.78f);

        /// <summary>
        /// '장면에 한 번' 덫 강화석(4-6, 합친 계획 11장): SceneStatics를 고치지 않고 이 장면의 DungeonRoot를 열쇠로 센다.
        /// 장면이 바뀌면 DungeonRoot가 새로 생겨 열쇠가 달라지므로 저절로 풀린다. (Unity 6.6에서 GetInstanceID는 쓸 수 없어 물체 자체를 견준다.)
        /// </summary>
        static bool _stoneSceneSet;
        static DungeonRoot _stoneScene;
        static int _stonesGiven;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSpikeStatics()
        {
            _stoneSceneSet = false;
            _stoneScene = null;
            _stonesGiven = 0;
        }

        string _id;
        int _floor = 1;
        bool _seen;
        bool _done;
        float _nextSeenCheck;
        SpriteRenderer[] _parts;

        /// <summary>한 번이라도 빛 안에서 봤는가(본 뒤로 계속 참).</summary>
        public bool Seen => _seen;
        /// <summary>밟았거나 걷어 내 끝났는가.</summary>
        public bool Done => _done;
        /// <summary>자리 표시 id('f{층}.{칸}.spikes'). 원정마다 다시 쓰이는 id라 꾸러미에 적지 않는다.</summary>
        public string Id => _id;

        public override float Range => UseRange;
        public override float HoldSeconds => TrapRules.SpikeDisarmSeconds;
        public override string Prompt => ExploreText.SpikePrompt;
        public override bool Available => _seen && !_done;
        public override string BlockedReason
        {
            get
            {
                var player = PlayerController.Instance;
                return player && player.Crouching ? null : ExploreText.SpikeNeedCrouch;
            }
        }

        public static FloorSpikes Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            string id = f != null && !string.IsNullOrEmpty(f.Id) ? f.Id : "spikes";
            var go = WorldProps.Root("FloorSpikes " + id, pos);
            var spikes = go.AddComponent<FloorSpikes>();
            spikes._id = id;
            spikes._floor = WorldProps.Floor;
            spikes.Build();
            spikes.SetVisible(false);
            return spikes;
        }

        /// <summary>덫 색(Palette.Trap) 고리 + 바깥을 향한 작은 세모 4, 아래 어두운 바탕. 모두 빛을 받는 도형.</summary>
        void Build()
        {
            int order = WorldProps.FloorDecalOrder + 10;
            float d = TrapRules.SpikeRadius * 2f;
            var parts = new System.Collections.Generic.List<SpriteRenderer>
            {
                WorldProps.Shape(transform, "Base", Vector2.zero, Vector2.one * (d * 0.92f), ShapeSprites.Circle, BaseColor, order, false),
                WorldProps.Shape(transform, "Ring", Vector2.zero, Vector2.one * d, ShapeSprites.Ring, Palette.Trap, order + 1, false),
            };
            for (int i = 0; i < 4; i++)
            {
                float deg = 45f + i * 90f;
                Vector2 at = WorldProps.Rotate(Vector2.right, deg) * (TrapRules.SpikeRadius * 0.45f);
                parts.Add(WorldProps.Shape(transform, "Spike", at, new Vector2(0.3f, 0.24f), ShapeSprites.Triangle, i % 2 == 0 ? Palette.Trap : SpikeDark, order + 2, false, deg));
            }
            _parts = parts.ToArray();
        }

        void SetVisible(bool on)
        {
            if (_parts == null) return;
            foreach (var sr in _parts)
                if (sr) sr.enabled = on;
        }

        void Update()
        {
            if (_done) return;
            var player = PlayerController.Instance;
            if (!player) return;
            Vector2 pos = transform.position;
            if (!_seen && Time.unscaledTime >= _nextSeenCheck)
            {
                _nextSeenCheck = Time.unscaledTime + SeenCheckSeconds;
                if (SeesNow(pos))
                {
                    _seen = true;
                    SetVisible(true);
                }
            }
            if (Time.deltaTime <= 0f || player.IsDown) return;
            // 4-6 밟음: 웅크림과 상관없이. 적은 밟지 않는다.
            float reach = TrapRules.SpikeRadius + PlayerController.Radius * 0.5f;
            if ((player.Position - pos).sqrMagnitude > reach * reach) return;
            // 덫(방패 표 2-7 Trap): 방패로 못 막고 무기 행동은 끊긴다. 회피 반격 창은 열지 않는다(궁수 덫과 같음).
            if (player.ReceiveHit(TrapRules.SpikeAttack(_floor), TrapRules.SpikePercentOfArcher, pos, TrapRules.SpikeKnockback, false, HitKind.Trap))
                Stepped(player);
        }

        /// <summary>지금 보이는가: 시야 안(시야를 끄면 늘)이고 빛 안.</summary>
        static bool SeesNow(Vector2 pos)
        {
            var vision = VisionSystem.Instance;
            if (vision && vision.VisionOn && !vision.IsVisible(pos, SeenPad)) return false;
            return LitAt(pos);
        }

        /// <summary>
        /// 이 자리가 빛 안인가: 어둠을 껐거나, 내 등잔 흐린 반경(DungeonLighting.DimRadius) 안이거나, 켠 벽 등잔(보스방 등잔 포함) 흐린 반경 안이고 벽이 가리지 않음.
        /// VisionSystem이 적에게 쓰는 '빛 안'과 같은 셈(그 함수는 감춰져 있어 여기서 다시 센다). 낙석 흙 부스러기도 쓴다.
        /// </summary>
        internal static bool LitAt(Vector2 p)
        {
            var lighting = DungeonLighting.Instance;
            if (!lighting || !lighting.DarknessOn) return true;
            var player = PlayerController.Instance;
            float r = lighting.DimRadius;
            if (player && (p - player.Position).sqrMagnitude <= r * r) return true;
            float wallR = DungeonLighting.WallLampDim;
            foreach (var it in Interactable.All)
            {
                Vector2 at;
                if (it is WallLamp lamp && lamp && lamp.Lit) at = lamp.Position;
                else if (it is ArenaLamp arena && arena && arena.Lit) at = arena.Position;
                else continue;
                if ((p - at).sqrMagnitude <= wallR * wallR && !VisionSystem.WallBetween(at, p)) return true;
            }
            return false;
        }

        /// <summary>밟음(4-6): 덫이 사라지고 '찰칵' 큰 소리(반경 8). 피해는 ReceiveHit이 이미 넣었다.</summary>
        void Stepped(PlayerController player)
        {
            _done = true;
            Vector2 pos = transform.position;
            SetVisible(false);
            Sfx.PlayScaled(SfxKind.Parry, 0.7f, 0.8f);
            WorldOverlay.Text(player.Position + Vector2.up * (PlayerController.Radius + 0.9f), ClickText, ClickColor);
            DungeonEvents.Say(ExploreText.SpikeStepped);
            // 함정 소리: 웅크려 밟아도 반경 8 그대로(웅크림 배율 없음).
            DungeonEvents.RaiseNoise(pos, TrapRules.SpikeNoiseRadius, false);
        }

        /// <summary>걷어 내기(4-6): 웅크린 채 1.2초(InteractionSystem이 막힘·누르기를 맡는다). 쇳조각 효과, 장면에 한 번 강화석 1.</summary>
        public override void Interact()
        {
            if (_done || !_seen) return;
            var player = PlayerController.Instance;
            if (!player || !player.Crouching) return;
            _done = true;
            Vector2 pos = transform.position;
            SetVisible(false);
            Sfx.PlayScaled(SfxKind.Stake, 0.5f, 1.3f);
            Vector2 toward = player.Position - pos;
            WorldDebris.Burst(pos, new Vector2(0.8f, 0.5f), -toward, Palette.Trap, SpikeDark, 8);
            if (TakeStoneSlot())
            {
                var bundle = new LootBundle { Stones = CornerLoot.SpikeStones };
                LootSpawner.Spawn(bundle, pos, toward, 0.1f);
                DungeonEvents.Say(ExploreText.SpikeDisarmedStone);
            }
            else DungeonEvents.Say(ExploreText.SpikeDisarmed);
        }

        /// <summary>이 장면에서 덫 강화석을 아직 TrapRules.SpikeStonesPerScene번 주지 않았으면 한 번 세고 true.</summary>
        static bool TakeStoneSlot()
        {
            var key = DungeonRoot.Instance;
            if (!_stoneSceneSet || !ReferenceEquals(_stoneScene, key))
            {
                _stoneSceneSet = true;
                _stoneScene = key;
                _stonesGiven = 0;
            }
            if (_stonesGiven >= TrapRules.SpikeStonesPerScene) return false;
            _stonesGiven++;
            return true;
        }
    }
}
