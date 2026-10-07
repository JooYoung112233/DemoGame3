using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 낙석(기획/1-2층-탐험-맛-1차.md 4-5, 규칙 TrapRules). 생성 덧칠(FloorSpice)이 놓은 자리 표시(FeatureKind.RockfallTrap)를 세상에 놓는다.
    /// 단서: 바닥 잔돌 7~10개(반경 2.2, 빛을 받는 도형)와 플레이어가 14 안에 있을 때 4~6초마다 떨어지는 작은 흙 부스러기(빛 안·시야 안에서만 낸다).
    /// 걸림: 서서 걸어 몸 가운데가 반경 1.1 안에 들면 걸린다. 웅크려 지나가면 걸리지 않고, 적이 밟아도 걸리지 않는다.
    /// 예고: 반경 1.6 빨간 원 1.0초(Think 밖에서 만든 주인 없는 예고라 늘 보인다, 시야와 문 1차 4-5) + 천장 삐걱(오우거 낙석 소리를 높고 작게).
    /// 떨어짐: 원 안 플레이어는 '위에서 떨어짐'(HitKind.FromAbove, 방패 불가·구르기로 피함), 그 층 돌충이 공격의 90%, 밀림 0.6.
    /// 원 안 적도 맞는다(굴쥐 즉사, 궁수 40%, 돌충이 25% + 무너짐, 정예 12% + 버팀 50%, 둥지·오우거 없음, 플레이어 몫 환경 피해).
    /// 적 위 '낙석!'은 그 적이 보일 때만 띄운다(합친 계획 2026-10-06 — 어둠·등 뒤 적의 자리를 글로 알리지 않게). 피해 숫자는 Enemy가 시야 규칙대로.
    /// 그 뒤 반경 12 큰 소리, 화면 흔들림, 돌 조각, 충돌 없는 돌무더기가 남는다. 한 번뿐이고, 상태는 이 장면(층 방문)에서만 기억한다(꾸러미에 남기지 않음).
    /// 숨은 위험이라 DungeonState에 등록하지 않는다(경험치·큰 지도·조사율 밖).
    /// 그림·소리 자리(사용자가 바꿔 끼울 곳): BuildClues(잔돌), DropDust(흙 부스러기), BuildRubble(돌무더기), 예고 소리·떨어짐 소리(OgreSound.Rockfall).
    /// </summary>
    public sealed class RockfallTrap : MonoBehaviour
    {
        /// <summary>흙 부스러기를 내는 플레이어 거리(4-5 단서).</summary>
        const float DustRange = 14f;
        /// <summary>천장 삐걱(예고): 오우거 낙석 소리를 작고 높게.</summary>
        const float CueVolume = 0.4f;
        const float CuePitch = 1.3f;
        /// <summary>떨어질 때 화면 흔들림.</summary>
        const float FallShake = 0.1f;
        const float FallShakeSeconds = 0.2f;
        /// <summary>정예가 맞을 때 움찔(초).</summary>
        const float EliteFlinch = 0.3f;
        /// <summary>'낙석!' 글 높이(몸 위).</summary>
        const float EnemyTextLift = 0.9f;

        // 흔적(ExpeditionTraces)의 돌·흙 색과 같은 값(그 파일의 색은 감춰져 있어 값을 옮겨 적는다).
        static readonly Color Pebble = new Color(0.36f, 0.34f, 0.31f);
        static readonly Color StoneLight = new Color(0.58f, 0.56f, 0.52f);
        static readonly Color StoneMid = new Color(0.44f, 0.42f, 0.39f);
        static readonly Color StoneDark = new Color(0.29f, 0.27f, 0.25f);
        static readonly Color DirtLoose = new Color(0.35f, 0.27f, 0.2f);
        static readonly Color DirtTop = new Color(0.44f, 0.35f, 0.26f);
        static readonly Color RubbleShade = new Color(0.16f, 0.12f, 0.09f, 0.9f);

        /// <summary>
        /// '장면에 한 번' 낙석 알림(4-5, 합친 계획 11장): SceneStatics를 고치지 않고 이 장면의 DungeonRoot를 열쇠로 센다.
        /// 장면이 바뀌면 DungeonRoot가 새로 생겨 열쇠가 달라지므로 저절로 풀린다. (Unity 6.6에서 GetInstanceID는 쓸 수 없어 물체 자체를 견준다.)
        /// </summary>
        static bool _announced;
        static DungeonRoot _announcedScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _announced = false;
            _announcedScene = null;
        }

        string _id;
        int _floor = 1;
        bool _triggered;
        bool _fallen;
        float _timer;
        float _dustClock;
        Telegraph _telegraph;

        /// <summary>걸렸는가(예고가 시작됨). 확인용.</summary>
        public bool Triggered => _triggered;
        /// <summary>이미 떨어졌는가(한 번뿐). 확인용.</summary>
        public bool Fallen => _fallen;
        /// <summary>자리 표시 id('f{층}.{칸}.rockfall'). 원정마다 다시 쓰이는 id라 꾸러미에 적지 않는다.</summary>
        public string Id => _id;

        public static RockfallTrap Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            string id = f != null && !string.IsNullOrEmpty(f.Id) ? f.Id : "rockfall";
            var go = WorldProps.Root("RockfallTrap " + id, pos);
            var trap = go.AddComponent<RockfallTrap>();
            trap._id = id;
            trap._floor = WorldProps.Floor;
            trap._dustClock = Random.Range(1f, TrapRules.RockDustMaxSeconds);
            trap.BuildClues(pos, LootRules.StableHash(id));
            return trap;
        }

        /// <summary>
        /// 바닥 잔돌(4-5 단서): 흔적의 돌 색 작은 네모·동그라미 7~10개를 반경 2.2 안에 흩뿌린다. 빛을 받는 도형이라 등잔 빛 안에서만 보인다.
        /// 모양은 자리 표시 id의 씨앗으로 정해 같은 원정이면 같다.
        /// </summary>
        void BuildClues(Vector2 pos, uint seed)
        {
            var rng = new System.Random((int)(seed & 0x7FFFFFFF));
            int count = TrapRules.RockClueStonesMin + rng.Next(TrapRules.RockClueStonesMax - TrapRules.RockClueStonesMin + 1);
            int order = WorldProps.FloorDecalOrder + 6;
            var holder = new GameObject("Clues").transform;
            holder.SetParent(transform, false);
            for (int i = 0; i < count; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = Mathf.Sqrt((float)rng.NextDouble()) * TrapRules.RockClueRadius;
                var at = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                float s = Rand(rng, 0.12f, 0.28f);
                bool square = rng.NextDouble() < 0.4;
                var color = Color.Lerp(Pebble, rng.NextDouble() < 0.5 ? StoneMid : StoneDark, (float)rng.NextDouble() * 0.6f);
                WorldProps.Shape(holder, "Pebble", at, new Vector2(s, s * Rand(rng, 0.7f, 1f)), square ? ShapeSprites.Square : ShapeSprites.Circle,
                    color, order, false, Rand(rng, 0f, 360f));
            }
        }

        static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _fallen) return;
            if (_triggered)
            {
                _timer += dt;
                if (_timer >= TrapRules.RockWindup) Fall();
                return;
            }
            var player = PlayerController.Instance;
            if (!player) return;
            Vector2 center = transform.position;
            float d2 = (player.Position - center).sqrMagnitude;
            // 4-5 걸림: 서서 걸을 때만(웅크려 지나가면 걸리지 않음). 적은 걸지 않는다.
            if (!player.IsDown && TrapRules.RockTriggers(player.Crouching) && d2 <= TrapRules.RockTriggerRadius * TrapRules.RockTriggerRadius)
            {
                Trigger();
                return;
            }
            if (d2 > DustRange * DustRange) return;
            _dustClock -= dt;
            if (_dustClock > 0f) return;
            _dustClock = Random.Range(TrapRules.RockDustMinSeconds, TrapRules.RockDustMaxSeconds);
            DropDust(center);
        }

        /// <summary>
        /// 천장 흙 부스러기(4-5 단서). 조각 효과(WorldDebris)는 빛을 무시하는 재질이라, 그 자리가 빛 안이고 시야 안일 때만 낸다
        /// (그래야 '빛을 받아야 보인다'가 지켜진다). 판자벽 조각을 작게, 아래로.
        /// </summary>
        static void DropDust(Vector2 center)
        {
            var vision = VisionSystem.Instance;
            if (vision && vision.VisionOn && !vision.IsVisible(center, 0.5f)) return;
            if (!FloorSpikes.LitAt(center)) return;
            WorldDebris.Burst(center + Random.insideUnitCircle * 0.8f, new Vector2(0.5f, 0.2f), Vector2.down, DirtLoose, DirtTop, 4);
        }

        /// <summary>
        /// 걸림(4-5): 빨간 원 1.0초 + 천장 삐걱, 장면 첫 낙석이면 알림 한 줄. 이미 걸렸거나 떨어졌으면 아무것도 하지 않는다(확인용으로도 부른다).
        /// 예고는 늘 주인 없이 만든다(바닥 예고라 주인이 안 보여도 그린다).
        /// </summary>
        public void Trigger()
        {
            if (_triggered || _fallen) return;
            _triggered = true;
            _timer = 0f;
            Vector2 center = transform.position;
            var owner = Telegraph.CreatingOwner;
            Telegraph.CreatingOwner = null;
            try
            {
                _telegraph = Telegraph.Circle(center, TrapRules.RockRadius, TrapRules.RockWindup);
            }
            finally
            {
                Telegraph.CreatingOwner = owner;
            }
            var player = PlayerController.Instance;
            if (_telegraph && player) _telegraph.Avoidable = _telegraph.Contains(player.Position, PlayerController.Radius) && player.DodgeReady;
            OgreSounds.Play(OgreSound.Rockfall, CueVolume, CuePitch);
            if (TakeFirstAnnouncement()) DungeonEvents.Say(ExploreText.RockfallFirst);
        }

        /// <summary>이 장면에서 아직 낙석 알림을 띄우지 않았으면 표시하고 true.</summary>
        static bool TakeFirstAnnouncement()
        {
            var key = DungeonRoot.Instance;
            if (_announced && ReferenceEquals(_announcedScene, key)) return false;
            _announced = true;
            _announcedScene = key;
            return true;
        }

        /// <summary>떨어짐(4-5): 플레이어·적 피해, 소리·흔들림·돌 조각, 큰 소리, 돌무더기. 한 번뿐.</summary>
        void Fall()
        {
            if (_fallen) return;
            _fallen = true;
            Vector2 c = transform.position;
            var player = PlayerController.Instance;
            // 위에서 떨어짐: 방패로 못 막고(ShieldRule.Blockable) 구르기 무적으로 피한다. 피하면 회피 반격 창이 열린다(오우거 낙석과 같음).
            if (player && !player.IsDown && (player.Position - c).magnitude <= TrapRules.RockRadius + PlayerController.Radius)
                player.ReceiveHit(TrapRules.RockAttack(_floor), TrapRules.RockPercentOfBoar, c, TrapRules.RockKnockback, true, HitKind.FromAbove);
            HitEnemies(c);
            if (_telegraph) _telegraph.Resolve();
            _telegraph = null;
            OgreSounds.Play(OgreSound.Rockfall);
            ScreenShake.Add(FallShake, FallShakeSeconds);
            var spread = new Vector2(TrapRules.RockRadius * 1.4f, TrapRules.RockRadius);
            WorldDebris.Burst(c, spread, Vector2.up, StoneLight, StoneDark, 8);
            WorldDebris.Burst(c, spread, Vector2.down, StoneMid, StoneDark, 8);
            // 함정 소리: 걸린 뒤 웅크려도 반경 12 그대로(웅크림 배율 없음).
            DungeonEvents.RaiseNoise(c, TrapRules.RockNoiseRadius, false);
            BuildRubble(c);
        }

        /// <summary>
        /// 원 안의 적(4-5): 최대 체력 × 몫을 환경 피해(플레이어 몫)로. 굴쥐(몫 100%)는 처형 길로 확실히 쓰러뜨린다.
        /// 정예 아닌 돌충이는 바로 무너지고, 정예는 버팀 50%를 깎는다. 둥지·오우거·보스는 맞지 않는다.
        /// </summary>
        static void HitEnemies(Vector2 c)
        {
            var vision = VisionSystem.Instance;
            bool visionOn = vision && vision.VisionOn;
            var targets = new List<Enemy>(Enemy.All);
            foreach (var e in targets)
            {
                if (!e || e.Dead || e.IsBoss || e.Health == null) continue;
                double fraction = TrapRules.RockEnemyFraction(e.Kind, e.IsElite);
                if (fraction <= 0) continue;
                if ((e.Position - c).magnitude > TrapRules.RockRadius + e.Radius) continue;
                // 맞기 전에 정한다(지난 LateUpdate 값 = 화면에 보이던 적인가).
                bool seen = !visionOn || e.VisionInSight;
                Vector2 textAt = e.Position + Vector2.up * (e.Radius + EnemyTextLift);
                int applied;
                if (fraction >= 1.0) applied = e.Execute(DamageSource.Environment);
                else
                {
                    int amount = Mathf.Max(1, Mathf.RoundToInt((float)(e.Health.Max * fraction)));
                    applied = e.TakeHit(amount, false, DamageSource.Environment);
                    if (applied > 0 && !e.Dead)
                    {
                        if (TrapRules.RockBreaks(e.Kind, e.IsElite)) e.BreakNow();
                        else if (e.IsElite && e.Poise != null) e.ApplyPoiseHit(e.Poise.Max * TrapRules.RockElitePoiseFraction, EliteFlinch);
                    }
                }
                if (applied > 0 && seen) WorldOverlay.Text(textAt, ExploreText.RockfallEnemy, Palette.Trap);
            }
        }

        /// <summary>떨어진 자리의 돌무더기(충돌 없음, 빛을 받는 도형): 그늘 + 돌무더기 그림 + 큰 돌 몇 개.</summary>
        void BuildRubble(Vector2 c)
        {
            var rng = new System.Random((int)(LootRules.StableHash(_id + ".fallen") & 0x7FFFFFFF));
            int order = WorldProps.FloorDecalOrder + 8;
            var holder = new GameObject("Rubble").transform;
            holder.SetParent(transform, false);
            WorldProps.Shape(holder, "Shade", Vector2.zero, new Vector2(2.6f, 1.9f), ShapeSprites.Circle, RubbleShade, order, false, Rand(rng, 0f, 360f));
            WorldProps.Shape(holder, "Pile", Vector2.zero, Vector2.one * 1.8f, ShapeSprites.Rubble(rng.Next(4)), StoneLight, order + 1, false, Rand(rng, -20f, 20f));
            for (int i = 0; i < 4; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                var at = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Rand(rng, 0.3f, 1.1f);
                float s = Rand(rng, 0.3f, 0.55f);
                WorldProps.Shape(holder, "Rock", at, new Vector2(s, s * Rand(rng, 0.7f, 0.95f)), ShapeSprites.Circle,
                    Color.Lerp(StoneMid, StoneLight, (float)rng.NextDouble()), order + 2, false, Rand(rng, 0f, 360f));
            }
        }

        void OnDestroy()
        {
            // 사라질 때 남은 예고를 지운다.
            if (_telegraph) _telegraph.Cancel();
            _telegraph = null;
        }
    }
}
