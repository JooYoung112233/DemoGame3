using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 피 튀김·바닥 얼룩·피 웅덩이·시체·살 조각·플레이어 피격 붉은 가장자리(기획/다크판타지-분위기-1차.md '잔혹', 계약서 C '뚜렷하게').
    /// 던전과 전투 시험장 루트 둘 다에 붙는다. 던전(DungeonRoot)에서는 원정 다시 시작(DungeonEvents.ExpeditionRestarted)까지 남고
    /// (시체 150·얼룩 500 상한, 넘치면 오래된 것부터 흐려짐), 전투 시험장에서는 쌓이지 않게 25초 뒤 흐려 사라진다(기획 '지키는 것').
    /// 손맛 수치(콤보·히트스톱·넉백·흔들림·처치 날림)는 건드리지 않고 그림·소리만 더한다. 빨간 예고(−60)보다 아래에 남고,
    /// 날아가는 피는 효과 정렬(3050)이라 시야 덮개(5000) 밑에서 어두워진다.
    /// 부르는 곳: HitEffects.OnHit → EnemyHit, Enemy 처치 연출 끝 → LeaveCorpse, CombatEvents(피해 양·플레이어 피격).
    /// 전투·보스·무기 다듬기 1차(기획/전투-보스-무기-다듬기-1차.md 4장·묶음 4): 벽 박기 자국(WallSplat ③), 출혈 방울(BleedDrip ⑥),
    /// 처형 고어(ExecutionGore ⑤), 피 진하기(BloodAt)·시체 밟기(CorpseAt)·피 발자국(Footprint, GoreFootprints),
    /// 낮은 체력 심장 시계(Heart*: 붉은 가장자리 맥동과 DungeonAudio 심장 소리가 같은 박자, 4-3 '한 층').
    /// </summary>
    public sealed class GoreSystem : MonoBehaviour
    {
        /// <summary>던전 시체 상한(계약서 C).</summary>
        public const int CorpseCap = 150;
        /// <summary>던전 얼룩 상한(계약서 C).</summary>
        public const int DecalCap = 500;
        /// <summary>피 웅덩이: 시체마다 하나라 시체와 같은 상한(얼룩에 밀려 시체만 남지 않게 따로 둔다).</summary>
        public const int PoolCap = 150;
        /// <summary>굴쥐 조각(3~5조각/마리).</summary>
        public const int ChunkCap = 200;
        const int DropCount = 512;
        /// <summary>전투 시험장: 시체·웅덩이·얼룩이 이 시간 뒤 흐려 사라진다.</summary>
        public const float TestLifetime = 25f;

        /// <summary>정렬: 바닥(−1000) 바로 위, 벽(−900)·예고(−60) 아래. 얼룩 &lt; 웅덩이 &lt; 시체 &lt; 조각.</summary>
        public const int StainOrder = -993;
        public const int PoolOrder = -992;
        public const int CorpseOrder = -990;
        public const int ChunkOrder = -989;

        /// <summary>IMGUI 순서: 떠오르는 글자(10)·HUD(0~5) 뒤에 그린다.</summary>
        const int EdgeGuiDepth = 20;
        /// <summary>피격 붉음이 사라지는 시간(가장 진할 때 기준).</summary>
        const float HurtFade = 0.6f;

        /// <summary>
        /// 낮은 체력 심장(4-3 '낮은 체력 소리는 한 층'): 체력이 이 비율 아래면 심장이 뛰고 가장자리가 그 박자로 맥동한다(초당 HeartRate번).
        /// 예전 가장자리 맥동(25% 아래, 1.3초 주기)을 심장 소리와 같은 문턱·박자로 합쳤다(7장 #19: 숨·심장·맥동이 세 겹이 되지 않게).
        /// </summary>
        public const float HeartBelow = 0.20f;
        /// <summary>이 비율 아래면 심장이 빨라진다(초당 HeartFastRate번).</summary>
        public const float HeartFastBelow = 0.10f;
        public const float HeartRate = 1.1f;
        public const float HeartFastRate = 1.5f;

        /// <summary>시체 밑 피 웅덩이 크기(몸 지름 배율). 처형은 여기에 ExecutionRule.BloodPoolScale(1.4)을 곱한다.</summary>
        public const float CorpsePoolScale = 1.7f;
        /// <summary>BloodAt: 그림 크기 대비 덮는 반지름(얼룩·줄무늬는 덩이 반지름 약 0.25, 웅덩이는 덩이·혹 약 0.36).</summary>
        const float StainCover = 0.22f;
        const float PoolCover = 0.36f;
        /// <summary>CorpseAt: 시체는 그린 상자 짧은 쪽 반폭의 이만큼, 조각은 그림 크기(1유닛 그림)의 이만큼을 덮는다.</summary>
        const float CorpseCover = 0.9f;
        const float ChunkCover = 0.4f;

        /// <summary>피 발자국(GoreFootprints): 상한, 남는 시간, 흐려지는 시간. 바닥 얼룩과 따로 두어 BloodAt에 세지 않는다(발자국이 발자국을 낳지 않게).</summary>
        public const int PrintCap = 96;
        public const float PrintLifetime = 24f;
        const float PrintFade = 6f;
        /// <summary>발자국 크기: 기존 얼룩 그림을 걷는 쪽으로 길쭉하게(길이 × 폭).</summary>
        const float PrintLength = 0.3f;
        const float PrintWidth = 0.15f;

        public static GoreSystem Instance { get; private set; }

        /// <summary>지금 심장이 뛰는가(체력 HeartBelow 아래, 쓰러지지 않음). 전투 시험장에서도 돌고, 소리는 던전 DungeonAudio만 낸다.</summary>
        public static bool HeartBeating => Instance && Instance._heartRate > 0f;
        /// <summary>지금 박자(초당 번, 안 뛰면 0).</summary>
        public static float HeartRateNow => Instance ? Instance._heartRate : 0f;
        /// <summary>박자 위상 0~1(0 = '쿵'이 나는 순간, 가장자리가 가장 진함). 실제 시간으로 돈다(히트스톱에 멈추지 않고, 일시 정지에는 멈춘다).</summary>
        public static float HeartPhase => Instance ? Instance._heartPhase : 0f;
        /// <summary>지금까지 뛴 박 수. DungeonAudio가 바뀐 것을 보고 심장 소리를 낸다(같은 시계 하나라 박자가 어긋나지 않음).</summary>
        public static int HeartBeats => Instance ? Instance._heartBeats : 0;

        /// <summary>던전인가(원정 다시 시작까지 남음). 아니면 전투 시험장(25초 뒤 사라짐).</summary>
        public bool IsDungeon => _dungeon;

        GoreLayer _decals;
        GoreLayer _pools;
        GoreLayer _corpses;
        GoreLayer _chunks;
        GoreLayer _prints;
        GoreDrops _drops;
        bool _dungeon;
        DamageDealt _lastDealt;
        int _lastDealtFrame = -1;
        float _hurt;
        float _pendingBlood;
        float _edgeAlpha;
        Texture2D _edgeTex;
        Camera _cam;
        float _heartRate;
        float _heartPhase;
        int _heartBeats;

        /// <summary>도메인 다시 불러오기 꺼짐 대비. 통합 작업자가 DungeonRoot·CombatTestRoot의 ResetStatics에서 부른다.</summary>
        public static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            Instance = this;
            // 던전 루트와 같은 물체에 붙는다(DungeonRoot.Awake가 Instance를 먼저 세운다).
            _dungeon = GetComponent<DungeonRoot>() != null || DungeonRoot.Instance != null;
            GoreSprites.Prewarm();
            _edgeTex = GoreSprites.EdgeTexture;

            _decals = new GoreLayer(this, Container("Gore stains"), "Blood stain", DecalCap, 24);
            _pools = new GoreLayer(this, Container("Gore pools"), "Blood pool", PoolCap, 12);
            _corpses = new GoreLayer(this, Container("Gore corpses"), "Corpse", CorpseCap, 12);
            _chunks = new GoreLayer(this, Container("Gore chunks"), "Flesh chunk", ChunkCap, 16);
            _prints = new GoreLayer(this, Container("Gore footprints"), "Footprint", PrintCap, 8);
            float life = _dungeon ? float.PositiveInfinity : TestLifetime;
            _decals.Lifetime = life;
            _pools.Lifetime = life;
            _corpses.Lifetime = life;
            _chunks.Lifetime = life;
            // 발자국은 던전에서도 흐려져 사라진다.
            _prints.Lifetime = Mathf.Min(life, PrintLifetime);
            _prints.LifeFade = PrintFade;
            _drops = new GoreDrops(this, Container("Gore drops"), DropCount);

            // 루트가 ResetStatics로 구독을 비운 뒤에 붙으므로 여기서 구독한다.
            CombatEvents.PlayerDealtDamage += OnDealt;
            CombatEvents.PlayerDamaged += OnPlayerDamaged;
            if (_dungeon) DungeonEvents.ExpeditionRestarted += Clear;
        }

        void OnDestroy()
        {
            CombatEvents.PlayerDealtDamage -= OnDealt;
            CombatEvents.PlayerDamaged -= OnPlayerDamaged;
            DungeonEvents.ExpeditionRestarted -= Clear;
            if (Instance == this) Instance = null;
        }

        Transform Container(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float now = Time.time;
            _drops.Tick(dt);
            _decals.Tick(dt, now);
            _pools.Tick(dt, now);
            _corpses.Tick(dt, now);
            _chunks.Tick(dt, now);
            _prints.Tick(dt, now);
            UpdateEdge();
        }

        // ───────────────────────── 공개 API ─────────────────────────

        /// <summary>
        /// 적이 맞았다(HitEffects.OnHit이 부름: 피해·넉백 뒤라 처치면 이미 Dead).
        /// 살 있는 적은 맞은 방향으로 검붉은 피(피해량·치명·마무리에 비례 5~16방울)와 작은 얼룩, 둥지는 흙·고름.
        /// 처치면 크게 넘치게 벤 굴쥐는 조각나고, 무거운 적(멧돼지·정예)은 큰 피 터짐.
        /// </summary>
        public static void EnemyHit(Enemy enemy, Vector2 dir, bool crit, bool heavy)
        {
            var g = Instance;
            if (!g || !enemy) return;
            g.HandleHit(enemy, dir, crit, heavy);
        }

        /// <summary>
        /// 치명 세기를 아는 판(장비 문서 3-4). 피 +30%(세기·방울 +3·좁게 뿜음)는 보통·무거운 치명만이고,
        /// 가벼운 치명(쌍검 연타 등)은 보통 타와 같은 피를 낸다(노란 숫자·불꽃으로만 치명을 읽힘).
        /// </summary>
        public static void EnemyHit(Enemy enemy, Vector2 dir, CritTier tier, bool heavy) =>
            EnemyHit(enemy, dir, tier >= CritTier.Normal, heavy);

        /// <summary>처치 연출이 끝나면 시체로 남는가(날림 동안 몸을 흐리지 않고 끝까지 보인다).</summary>
        public static bool KeepsCorpse(Enemy enemy) =>
            Instance && enemy && !enemy.Dismembered && GoreColors.MatterOf(enemy) != GoreMatter.None;

        /// <summary>
        /// 처치 연출이 끝난 자리에 시체를 남기고(몸 그림을 어둡게·납작하게·기울여, 빛을 받음) 그 밑으로 피 웅덩이가 1초에 걸쳐 번진다(몸 크기 비례).
        /// 무거운 적은 몸 쓰러짐 소리. Enemy가 물체를 지우기 직전에 부른다.
        /// </summary>
        /// <param name="body">지금 몸 렌더러(날림이 끝난 자리·각도·크기).</param>
        /// <param name="restScale">쓰러지기 전 몸 크기(날림이 줄인 크기 대신 시체 크기 기준).</param>
        /// <param name="shapeColor">도형 몸 색(그림이면 무시).</param>
        /// <param name="art">그림 모드로 보이는 중인가.</param>
        /// <returns>시체를 남겼는가(남겼으면 Enemy는 몸을 줄여 사라지게 하지 않는다).</returns>
        public static bool LeaveCorpse(Enemy enemy, SpriteRenderer body, Vector3 restScale, Color shapeColor, bool art)
        {
            var g = Instance;
            if (!g || !body || !KeepsCorpse(enemy)) return false;
            g.SpawnCorpse(enemy, body, restScale, shapeColor, art);
            return true;
        }

        // ── 전투·보스·무기 다듬기 1차(기획/전투-보스-무기-다듬기-1차.md 4장·묶음 4). 꾸러미 ④ 소리와 피가 채웠다 ──
        // 모두 바닥 정렬 규칙(얼룩 −993 < 웅덩이 < 시체 < 조각, 빨간 예고 −60 아래)과 던전 상한·시험장 25초 흐림을 그대로 따른다.

        /// <summary>
        /// 벽 박기 피 자국(4-2 [1]): 닿은 벽 밑동에 벽을 따라 납작한 얼룩(반쯤 벽 그림 밑으로 들어감) + 벽 바깥(normal) 쪽으로 번진 줄무늬 2~3개와 얼룩,
        /// 벽에서 되튀는 방울·피 안개, 철퍽 소리. point = 벽에 닿은 자리, normal = 벽에서 방 쪽(비면 적 쪽). 허수아비·재질 없는 적은 없음, 둥지는 흙·고름.
        /// 부르는 곳: WallSlam(③).
        /// </summary>
        public static void WallSplat(Enemy enemy, Vector2 point, Vector2 normal)
        {
            var g = Instance;
            if (!g) return;
            var matter = enemy ? GoreColors.MatterOf(enemy) : GoreMatter.Flesh;
            if (matter == GoreMatter.None) return;
            g.SplatWall(enemy, point, normal, matter);
        }

        /// <summary>
        /// 출혈 틱 피 방울 자국(도끼, BleedRule 0.5초마다 6틱): 몸 둘레에서 떨어지는 검붉은 방울 2~3개와 발밑 작은 얼룩. 소리는 없다(틱이 잦아 시끄럽지 않게).
        /// 살 있는 적만(둥지·허수아비 없음). 부르는 곳: EnemyBleed(⑥), 틱마다.
        /// </summary>
        public static void BleedDrip(Enemy enemy)
        {
            var g = Instance;
            if (!g || !enemy || GoreColors.MatterOf(enemy) != GoreMatter.Flesh) return;
            g.Drip(enemy);
        }

        /// <summary>
        /// 처형 고어(4-2 [3]): 앞으로 크게 뿜는 피·뒤로 흩어지는 피·큰 얼룩. 적이 이미 쓰러졌으면(Execute 다음에 부름) 무거운 적도 몸이 조각 4~6개로 나뉘어 흩어지고
        /// (시체 대신, ExecutionRule.GorePieces*), 피 웅덩이는 시체 웅덩이의 1.4배(ExecutionRule.BloodPoolScale)로 번진다.
        /// 살아 있으면(기습으로 무너지기만 한 멧돼지) 피만 낸다. dir = 플레이어 → 적. 부르는 곳: ExecutionFx(⑤).
        /// </summary>
        public static void ExecutionGore(Enemy enemy, Vector2 dir)
        {
            var g = Instance;
            if (!g || !enemy) return;
            var matter = GoreColors.MatterOf(enemy);
            if (matter == GoreMatter.None) return;
            g.Execution(enemy, dir, matter);
        }

        /// <summary>
        /// 그 자리 바닥 피의 진하기(0~1): 반경 radius의 원(발)을 바닥 피 얼룩·웅덩이가 얼마나 덮는가(알파·흐려짐 반영, 흙·고름·발자국 제외).
        /// 작은 방울 자국 하나는 0.1 안팎, 웅덩이·큰 얼룩 위는 0.8~1. 피 발자국(GoreFootprints)이 밟았는지 볼 때 쓴다. 할당 없음.
        /// </summary>
        public static float BloodAt(Vector2 position, float radius)
        {
            var g = Instance;
            if (!g) return 0f;
            float pool = g._pools.Coverage(position, radius, PoolCover, true);
            if (pool >= 1f) return 1f;
            return Mathf.Min(1f, pool + g._decals.Coverage(position, radius, StainCover, true));
        }

        /// <summary>그 자리에 시체나 바닥에 떨어진 살 조각이 있는가(시체 밟기 '질척', DungeonAudio 발소리). 할당 없음.</summary>
        public static bool CorpseAt(Vector2 position, float radius)
        {
            var g = Instance;
            if (!g) return false;
            return g._corpses.AnyNear(position, radius, CorpseCover, true) || g._chunks.AnyNear(position, radius, ChunkCover, false);
        }

        /// <summary>
        /// 피 발자국 하나(GoreFootprints): 기존 피 얼룩 그림을 걷는 쪽(angleDeg)으로 길쭉하게 줄여 찍는다(크기·회전만 바꿈).
        /// alpha = 진하기(얼룩 색 알파에 곱함). 바닥 얼룩과 따로 두는 겹이라 BloodAt에 세지 않고, PrintLifetime 뒤 PrintFade초에 걸쳐 흐려 사라진다.
        /// </summary>
        public static void Footprint(Vector2 position, float angleDeg, float alpha)
        {
            var g = Instance;
            if (!g || alpha <= 0.01f) return;
            var c = GoreColors.Stain;
            c.a *= Mathf.Clamp01(alpha);
            float k = Random.Range(0.9f, 1.1f);
            var scale = new Vector3(PrintLength * k, PrintWidth * Random.Range(0.85f, 1.1f), 1f);
            g._prints.Spawn(RandomSplat(), position, angleDeg + Random.Range(-8f, 8f), scale, c, StainOrder);
        }

        /// <summary>모든 피·시체를 지운다(원정 다시 시작: 암전 중이라 바로 지운다).</summary>
        public void Clear()
        {
            _pendingBlood = 0f;
            _drops.Clear();
            _decals.Clear();
            _pools.Clear();
            _corpses.Clear();
            _chunks.Clear();
            _prints.Clear();
        }

        // ───────────────────────── 적 피격·처치 ─────────────────────────

        void OnDealt(DamageDealt d)
        {
            // 같은 호출 흐름에서 곧바로 HitEffects.OnHit → EnemyHit이 온다. 피해 양을 거기서 쓴다.
            _lastDealt = d;
            _lastDealtFrame = Time.frameCount;
        }

        void HandleHit(Enemy e, Vector2 dir, bool crit, bool heavy)
        {
            var matter = GoreColors.MatterOf(e);
            if (matter == GoreMatter.None) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            bool killed = e.Dead;
            int amount = _lastDealtFrame == Time.frameCount && ReferenceEquals(_lastDealt.Target, e) ? _lastDealt.Amount : 0;
            int maxHp = e.Health ? Mathf.Max(1, e.Health.Max) : 1;
            float frac = Mathf.Clamp01(amount / (float)maxHp);
            float body = Mathf.Max(0.3f, e.ShapeDiameter);
            float sizeK = Mathf.Lerp(0.85f, 1.4f, Mathf.InverseLerp(0.5f, 1.6f, body));
            float power = 1f + (crit ? 0.3f : 0f) + (heavy ? 0.2f : 0f) + (killed ? 0.25f : 0f);
            int count = Mathf.Clamp(5 + Mathf.RoundToInt(8f * Mathf.Sqrt(frac)) + (crit ? 3 : 0) + (heavy ? 2 : 0) + (killed ? 3 : 0), 5, 16);
            Vector2 at = e.Position + dir * (e.Radius * 0.3f);

            if (matter == GoreMatter.Nest)
            {
                // 둥지: 흙 부스러기와 누런 고름.
                _drops.Emit(at, dir, 50f, 2f * power, 5.5f * power, 1.2f, 3.6f, 0.07f, 0.14f, GoreColors.Dirt, GoreColors.DirtLight, 0.35f, matter, count);
                _drops.Emit(at, dir, 35f, 1.8f * power, 4.5f * power, 1.2f, 3.2f, 0.06f, 0.12f, GoreColors.PusDark, GoreColors.Pus, 0.4f, matter, Mathf.Max(2, count / 3));
                _drops.Puff(at + dir * 0.2f, 0.45f * power, DirtPuff, 0.2f, RandomSplat());
                AddSplat(at + dir * Random.Range(0.2f, 0.5f), Random.Range(0.3f, 0.45f), matter, float.NaN);
                if (killed) NestBurst(e, dir, power);
                return;
            }

            _drops.Emit(at, dir, crit ? 26f : 38f, 2.6f * power, 7.5f * power, 1.4f, 4.2f, 0.07f * sizeK, 0.15f * sizeK,
                GoreColors.BloodMid, GoreColors.BloodBright, 0.45f, matter, count);
            _drops.Puff(at + dir * 0.18f, 0.32f * sizeK * power, GoreColors.Puff, 0.16f, RandomSplat());
            AddSplat(at + dir * Random.Range(0.15f, 0.4f), Random.Range(0.26f, 0.4f) * sizeK, matter, float.NaN);

            float sound = 0.45f + 0.5f * (count / 16f);
            if (killed)
            {
                // 크게 넘치게 벤 굴쥐(남은 체력의 2배 이상)는 조각난다. 무거운 적(멧돼지·정예)은 크게 터진다.
                int before = e.HpBeforeLastHit;
                bool overkill = e.Kind == MonsterKind.Rat && amount > 0 && before > 0 && amount >= 2 * before;
                bool heavyBody = e.Weight == EnemyWeight.Heavy;
                if (overkill) Gib(e, dir, power, sizeK);
                if (heavyBody) HeavyBurst(e, dir, power, sizeK);
                if (overkill || heavyBody) sound = 1.35f;
                else sound = Mathf.Max(sound, 0.9f);
            }
            // 한 프레임에 여러 적을 베면 Sfx가 같은 종류를 한 번만 내므로, 가장 큰 철퍽만 LateUpdate에서 낸다.
            _pendingBlood = Mathf.Max(_pendingBlood, sound);
        }

        void LateUpdate()
        {
            if (_pendingBlood <= 0f) return;
            GoreSfx.Blood(_pendingBlood);
            _pendingBlood = 0f;
        }

        /// <summary>조각남: 몸을 숨기고 살 조각 3~5개가 맞은 쪽으로 흩어진다(빛을 받음, 땅에 닿으면 철퍽 자국). 작은 피 웅덩이.</summary>
        void Gib(Enemy e, Vector2 dir, float power, float sizeK)
        {
            e.Dismember();
            Vector2 at = e.Position;
            Color shape = GoreColors.BodyColor(e);
            Color flesh = Color.Lerp(shape, GoreColors.Darken(shape), 0.5f);
            int n = Random.Range(3, 6);
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            int flyingOrder = ActorOrder(at.y) + 2;
            for (int i = 0; i < n; i++)
            {
                float ang = baseAngle + Random.Range(-75f, 75f) * Mathf.Deg2Rad;
                float up = Random.Range(2.6f, 5f);
                float h = Random.Range(0.15f, 0.35f);
                Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (Random.Range(2.2f, 5.2f) * power);
                v = ClampToWalls(at, v, up, h);
                float size = Random.Range(0.18f, 0.3f) * sizeK;
                var scale = new Vector3(size * Random.Range(0.85f, 1.2f), size * Random.Range(0.7f, 1f), 1f);
                ref var piece = ref _chunks.Spawn(GoreSprites.Chunk(Random.Range(0, GoreSprites.ChunkVariants)), at + Random.insideUnitCircle * 0.1f,
                    Random.Range(0f, 360f), scale, flesh, ChunkOrder);
                float spin = Random.Range(360f, 900f) * (Random.value < 0.5f ? -1f : 1f);
                GoreLayer.Launch(ref piece, v, h, up, spin, flyingOrder, GoreMatter.Flesh);
            }
            _drops.Emit(at, dir, 180f, 1.5f * power, 6f * power, 1.5f, 4.5f, 0.09f * sizeK, 0.18f * sizeK,
                GoreColors.BloodMid, GoreColors.BloodBright, 0.5f, GoreMatter.Flesh, 10);
            _drops.Puff(at, 0.6f * sizeK, GoreColors.Puff, 0.22f, RandomSplat());
            AddPool(at, Mathf.Max(0.3f, e.ShapeDiameter) * 1.4f, 0.8f, GoreMatter.Flesh);
        }

        /// <summary>무거운 적 처치: 앞으로 크게 뿜고 뒤로도 흩어지며, 맞은 쪽 바닥에 큰 피 자국.</summary>
        void HeavyBurst(Enemy e, Vector2 dir, float power, float sizeK)
        {
            Vector2 at = e.Position;
            float body = Mathf.Max(0.5f, e.ShapeDiameter);
            _drops.Emit(at, dir, 70f, 3.5f * power, 10f * power, 2f, 5f, 0.1f * sizeK, 0.2f * sizeK,
                GoreColors.BloodMid, GoreColors.BloodBright, 0.55f, GoreMatter.Flesh, 16);
            _drops.Emit(at, -dir, 180f, 1.5f, 4.5f, 1.5f, 3.5f, 0.08f * sizeK, 0.15f * sizeK,
                GoreColors.BloodDark, GoreColors.BloodMid, 0.4f, GoreMatter.Flesh, 8);
            _drops.Puff(at + dir * (body * 0.3f), 0.75f * body, GoreColors.Puff, 0.24f, RandomSplat());
            _drops.Puff(at + dir * (body * 0.7f), 0.5f * body, GoreColors.Puff, 0.2f, RandomSplat());
            AddSplat(at + dir * (body * 0.75f), body * 1.15f, GoreMatter.Flesh, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>둥지가 무너짐: 흙·고름이 사방으로 터지고 흙덩이가 흩어진다.</summary>
        void NestBurst(Enemy e, Vector2 dir, float power)
        {
            Vector2 at = e.Position;
            float body = Mathf.Max(0.8f, e.ShapeDiameter);
            _drops.Emit(at, dir, 180f, 2f, 6f, 2f, 4.5f, 0.1f, 0.2f, GoreColors.Dirt, GoreColors.DirtLight, 0.45f, GoreMatter.Nest, 16);
            _drops.Emit(at, dir, 120f, 1.5f, 4.5f, 1.5f, 3.5f, 0.08f, 0.16f, GoreColors.PusDark, GoreColors.Pus, 0.5f, GoreMatter.Nest, 10);
            _drops.Puff(at, 0.9f * body, DirtPuff, 0.3f, RandomSplat());
            int n = Random.Range(4, 7);
            int flyingOrder = ActorOrder(at.y) + 2;
            for (int i = 0; i < n; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float up = Random.Range(2f, 4f);
                float h = Random.Range(0.2f, 0.5f);
                Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (Random.Range(1.2f, 3.2f) * power);
                v = ClampToWalls(at, v, up, h);
                float size = Random.Range(0.2f, 0.34f);
                Color c = Random.value < 0.3f ? GoreColors.Pus : GoreColors.Dirt;
                ref var piece = ref _chunks.Spawn(GoreSprites.Chunk(Random.Range(0, GoreSprites.ChunkVariants)), at + Random.insideUnitCircle * (body * 0.3f),
                    Random.Range(0f, 360f), new Vector3(size, size * Random.Range(0.7f, 1f), 1f), c, ChunkOrder);
                GoreLayer.Launch(ref piece, v, h, up, Random.Range(-500f, 500f), flyingOrder, GoreMatter.Nest);
            }
        }

        /// <summary>몸 지름에 따른 피 크기 배율(HandleHit과 같은 식: 굴쥐 0.85 ~ 큰 몸 1.4).</summary>
        static float SizeK(Enemy e)
        {
            float body = e ? Mathf.Max(0.3f, e.ShapeDiameter) : 0.6f;
            return Mathf.Lerp(0.85f, 1.4f, Mathf.InverseLerp(0.5f, 1.6f, body));
        }

        /// <summary>벽 박기 자국(WallSplat).</summary>
        void SplatWall(Enemy e, Vector2 point, Vector2 normal, GoreMatter matter)
        {
            if (normal.sqrMagnitude < 0.0001f && e) normal = e.Position - point;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;
            normal.Normalize();
            float sizeK = SizeK(e);
            float outAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            bool nest = matter == GoreMatter.Nest;

            // 벽 밑동: 벽을 따라 납작하게 퍼진 얼룩. 벽 그림(−900)이 얼룩(−993) 위라 벽 쪽 절반은 벽 밑으로 숨어 '벽에 처박힌' 자리로 읽힌다.
            float s = 0.62f * sizeK;
            _decals.Spawn(RandomSplat(), point + normal * 0.06f, outAngle + 90f,
                new Vector3(s * Random.Range(1.15f, 1.45f), s * Random.Range(0.45f, 0.6f), 1f), StainColor(matter), StainOrder);
            // 벽 바깥으로 번진 줄무늬: 머리(진한 쪽)는 벽 쪽, 가는 꼬리는 방 쪽(OnDropLanded와 같은 줄무늬 그림 규칙).
            int streaks = Random.Range(2, 4);
            for (int i = 0; i < streaks; i++)
            {
                float ang = outAngle + Random.Range(-35f, 35f);
                float rad = ang * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float len = Random.Range(0.5f, 0.85f) * sizeK;
                _decals.Spawn(GoreSprites.Streak(Random.Range(0, GoreSprites.StreakVariants)), point + dir * (len * 0.3f + 0.08f), ang + 180f,
                    new Vector3(len, len, 1f), StainColor(matter), StainOrder);
            }
            AddSplat(point + normal * Random.Range(0.3f, 0.5f) * sizeK, Random.Range(0.35f, 0.5f) * sizeK, matter, float.NaN);

            // 벽에서 되튀는 방울과 안개(방울 출발점은 벽에서 조금 띄워 OnDropLanded의 벽 가림 검사에 걸리지 않게).
            Vector2 from = point + normal * 0.12f;
            if (nest)
            {
                _drops.Emit(from, normal, 70f, 1.5f, 4f, 1f, 3f, 0.07f, 0.13f, GoreColors.Dirt, GoreColors.DirtLight, 0.4f, matter, 8);
                _drops.Puff(from, 0.45f * sizeK, DirtPuff, 0.2f, RandomSplat());
            }
            else
            {
                _drops.Emit(from, normal, 70f, 1.5f, 4.5f, 1f, 3f, 0.07f * sizeK, 0.14f * sizeK,
                    GoreColors.BloodMid, GoreColors.BloodBright, 0.5f, matter, Random.Range(8, 11));
                _drops.Puff(from, 0.4f * sizeK, GoreColors.Puff, 0.18f, RandomSplat());
            }
            _pendingBlood = Mathf.Max(_pendingBlood, 0.9f);
        }

        /// <summary>출혈 틱 방울(BleedDrip).</summary>
        void Drip(Enemy e)
        {
            float sizeK = SizeK(e);
            Vector2 at = e.Position + Random.insideUnitCircle * (e.Radius * 0.6f);
            _drops.Emit(at, Random.insideUnitCircle, 180f, 0.2f, 0.9f, 0.3f, 1.2f, 0.05f, 0.09f,
                GoreColors.BloodDark, GoreColors.BloodMid, 0.6f, GoreMatter.Flesh, Random.Range(2, 4));
            AddSplat(at + Random.insideUnitCircle * 0.12f, Random.Range(0.14f, 0.22f) * sizeK, GoreMatter.Flesh, float.NaN);
        }

        /// <summary>처형 고어(ExecutionGore).</summary>
        void Execution(Enemy e, Vector2 dir, GoreMatter matter)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = e.FacingDirection.sqrMagnitude > 0.0001f ? -e.FacingDirection : Vector2.right;
            dir.Normalize();
            if (matter == GoreMatter.Nest)
            {
                // 둥지는 처형하지 않지만, 불려도 흙·고름만.
                NestBurst(e, dir, 1.2f);
                return;
            }
            float body = Mathf.Max(0.4f, e.ShapeDiameter);
            float sizeK = SizeK(e);
            const float power = 1.35f;
            Vector2 at = e.Position;
            float dirAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _drops.Emit(at, dir, 60f, 3.5f * power, 10f * power, 2f, 5.5f, 0.1f * sizeK, 0.2f * sizeK,
                GoreColors.BloodMid, GoreColors.BloodBright, 0.6f, GoreMatter.Flesh, 16);
            _drops.Emit(at, -dir, 180f, 1.5f, 5f, 1.5f, 4f, 0.08f * sizeK, 0.16f * sizeK,
                GoreColors.BloodDark, GoreColors.BloodMid, 0.45f, GoreMatter.Flesh, 10);
            _drops.Puff(at, 0.85f * body, GoreColors.Puff, 0.26f, RandomSplat());
            _drops.Puff(at + dir * (body * 0.6f), 0.55f * body, GoreColors.Puff, 0.22f, RandomSplat());
            AddSplat(at + dir * (body * 0.7f), body * 1.25f, GoreMatter.Flesh, dirAngle);
            _pendingBlood = Mathf.Max(_pendingBlood, 1.4f);
            if (!e.Dead) return;

            // 쓰러졌으면 무거운 적도 조각난다: 몸을 숨기고(시체 대신) 조각 4~6개가 맞은 쪽으로 흩어진다. 큰 몸일수록 조각도 크다.
            e.Dismember();
            Color shape = GoreColors.BodyColor(e);
            Color flesh = Color.Lerp(shape, GoreColors.Darken(shape), 0.5f);
            int n = Random.Range(ExecutionRule.GorePiecesMin, ExecutionRule.GorePiecesMax + 1);
            float pieceK = sizeK * Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(0.6f, 1.6f, body));
            float baseAngle = dirAngle * Mathf.Deg2Rad;
            int flyingOrder = ActorOrder(at.y) + 2;
            for (int i = 0; i < n; i++)
            {
                float ang = baseAngle + Random.Range(-80f, 80f) * Mathf.Deg2Rad;
                float up = Random.Range(2.6f, 5.2f);
                float h = Random.Range(0.2f, 0.4f);
                Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (Random.Range(2.2f, 5.5f) * power);
                v = ClampToWalls(at, v, up, h);
                float size = Random.Range(0.2f, 0.34f) * pieceK;
                var scale = new Vector3(size * Random.Range(0.85f, 1.2f), size * Random.Range(0.7f, 1f), 1f);
                ref var piece = ref _chunks.Spawn(GoreSprites.Chunk(Random.Range(0, GoreSprites.ChunkVariants)), at + Random.insideUnitCircle * (body * 0.2f),
                    Random.Range(0f, 360f), scale, flesh, ChunkOrder);
                float spin = Random.Range(360f, 900f) * (Random.value < 0.5f ? -1f : 1f);
                GoreLayer.Launch(ref piece, v, h, up, spin, flyingOrder, GoreMatter.Flesh);
            }
            AddPool(at, body * CorpsePoolScale * ExecutionRule.BloodPoolScale, 1.2f, GoreMatter.Flesh);
        }

        void SpawnCorpse(Enemy e, SpriteRenderer body, Vector3 restScale, Color shapeColor, bool art)
        {
            var matter = GoreColors.MatterOf(e);
            bool nest = matter == GoreMatter.Nest;
            Transform bt = body.transform;
            Vector2 pos = bt.position;
            float angle = bt.eulerAngles.z;
            Vector3 nowScale = bt.lossyScale;
            var sprite = body.sprite ? body.sprite : ShapeSprites.Circle;
            Color start = art ? Color.white : new Color(shapeColor.r, shapeColor.g, shapeColor.b, 1f);
            Color end = art ? GoreColors.ArtCorpse : GoreColors.Darken(shapeColor);
            // 납작하게(몸 기준 세로 0.58, 둥지는 주저앉아 0.42) + 조금 더 기울여 눕는다.
            // 정수리 시점은 위에서 본 몸을 돌려 둔 그림이라 로컬 세로가 몸 폭이다: 거의 그대로 두고(0.92) 더 크게 비틀어 쓰러짐을 보인다.
            bool td = TopDownView.Active;
            var to = new Vector3(Mathf.Abs(restScale.x) * (nest ? 1.15f : td ? 1.0f : 1.06f), Mathf.Abs(restScale.y) * (nest ? 0.42f : td ? 0.92f : 0.58f), 1f);
            float tiltRange = td ? 35f : 20f;
            float tilt = nest ? 0f : Random.Range(-tiltRange, tiltRange);
            ref var corpse = ref _corpses.Spawn(sprite, pos, angle, new Vector3(Mathf.Abs(nowScale.x), Mathf.Abs(nowScale.y), 1f), start, CorpseOrder, body.flipX);
            GoreLayer.Animate(ref corpse, to, end, angle + tilt, 0.28f, false);

            // 피 웅덩이(둥지는 고름 웅덩이): 몸 크기에 비례해 1초에 걸쳐 번진다.
            AddPool(pos, Mathf.Max(0.35f, e.ShapeDiameter) * (nest ? 1.4f : CorpsePoolScale), 1f, matter);
            if (!nest && e.Weight == EnemyWeight.Heavy) GoreSfx.BodyFall(e.IsElite);
        }

        // ───────────────────────── 플레이어 피격 ─────────────────────────

        void OnPlayerDamaged(int amount)
        {
            var p = PlayerController.Instance;
            if (!p || amount <= 0) return;
            int maxHp = p.Health ? Mathf.Max(1, p.Health.Max) : 1;
            float ratio = amount / (float)maxHp;
            // 화면 가장자리가 잠깐 붉게(피해 비율에 비례).
            _hurt = Mathf.Max(_hurt, Mathf.Clamp(0.3f + ratio * 3f, 0.3f, 0.9f));
            Vector2 at = p.Position;
            Vector2 away = at - p.LastHitFrom;
            Vector2 dir = away.sqrMagnitude > 0.0001f ? away.normalized : AwayFromNearestEnemy(at);
            int count = Mathf.Clamp(6 + Mathf.RoundToInt(ratio * 40f), 6, 14);
            _drops.Emit(at, dir, 55f, 1.8f, 5.5f, 1.2f, 3.6f, 0.07f, 0.13f, GoreColors.BloodMid, GoreColors.BloodBright, 0.4f, GoreMatter.Flesh, count);
            _drops.Puff(at + dir * 0.15f, 0.3f, GoreColors.Puff, 0.14f, RandomSplat());
            if (ratio >= 0.04f) AddSplat(at + dir * Random.Range(0.1f, 0.3f), Random.Range(0.22f, 0.34f), GoreMatter.Flesh, float.NaN);
        }

        /// <summary>가장 가까운(6유닛 안) 살아 있는 적의 반대쪽. 없으면 아무 쪽. 할당 없음.</summary>
        static Vector2 AwayFromNearestEnemy(Vector2 at)
        {
            Enemy best = null;
            float bestSq = 36f;
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                // 시야 밖 적은 방향으로도 드러내지 않는다(좀보이드식 시야).
                if (!e || e.Dead || (e.VisionHidden && !e.VisionInSight)) continue;
                float sq = (e.Position - at).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = e;
                }
            }
            Vector2 dir = best ? at - best.Position : Random.insideUnitCircle;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        }

        void UpdateEdge()
        {
            bool paused = TimeScaleService.Paused;
            if (!paused) _hurt = Mathf.MoveTowards(_hurt, 0f, Time.unscaledDeltaTime / HurtFade);
            float pulse = 0f;
            float rate = 0f;
            float low = 0f;
            var p = PlayerController.Instance;
            if (p && !p.IsDown && p.Health && !p.Health.Dead)
            {
                float f = p.Health.Fraction;
                if (f < HeartBelow)
                {
                    rate = f < HeartFastBelow ? HeartFastRate : HeartRate;
                    low = 1f - f / HeartBelow;
                }
            }
            UpdateHeart(rate, paused);
            if (rate > 0f)
            {
                // 체력 20% 아래: 심장 박자(초당 1.1번, 10% 아래 1.5번)로 은은하게, '쿵'(위상 0)에 가장 진하고 낮을수록 조금 더 진하다.
                float beat = 0.5f + 0.5f * Mathf.Cos(_heartPhase * Mathf.PI * 2f);
                pulse = (0.1f + 0.14f * beat) * (0.55f + 0.45f * low);
            }
            _edgeAlpha = Mathf.Max(_hurt, pulse);
        }

        /// <summary>
        /// 심장 시계 하나(맥동과 소리가 함께 봄). 뛰기 시작하는 순간 바로 한 박('쿵')을 세고, 그 뒤 실제 시간으로 위상을 돌린다.
        /// 일시 정지 동안은 멈춘다. 박자가 바뀌어도(20% → 10%) 위상은 이어 간다.
        /// </summary>
        void UpdateHeart(float rate, bool paused)
        {
            if (rate <= 0f)
            {
                _heartRate = 0f;
                _heartPhase = 0f;
                return;
            }
            if (_heartRate <= 0f)
            {
                _heartRate = rate;
                _heartPhase = 0f;
                _heartBeats++;
                return;
            }
            _heartRate = rate;
            if (paused) return;
            _heartPhase += Time.unscaledDeltaTime * rate;
            if (_heartPhase >= 1f)
            {
                _heartPhase -= Mathf.Floor(_heartPhase);
                _heartBeats++;
            }
        }

        void OnGUI()
        {
            if (_edgeAlpha <= 0.004f || !_edgeTex) return;
            GUI.depth = EdgeGuiDepth;
            if (Event.current.type != EventType.Repaint) return;
            var prevColor = GUI.color;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            var c = GoreColors.Edge;
            c.a = _edgeAlpha;
            GUI.color = c;
            GUI.DrawTexture(ViewRect(), _edgeTex, ScaleMode.StretchToFill, true);
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        /// <summary>카메라가 그리는 화면 영역(전투 시험장은 오른쪽 시험 패널을 뺀 부분). IMGUI 좌표(위가 0).</summary>
        Rect ViewRect()
        {
            if (!_cam) _cam = Camera.main;
            if (!_cam) return new Rect(0f, 0f, Screen.width, Screen.height);
            Rect px = _cam.pixelRect;
            return new Rect(px.x, Screen.height - px.yMax, px.width, px.height);
        }

        // ───────────────────────── 바닥 자국 ─────────────────────────

        /// <summary>날아간 피 방울이 땅에 닿음(GoreDrops). 벽 너머로 넘어간 방울은 자국을 남기지 않는다.</summary>
        public void OnDropLanded(Vector2 origin, Vector2 pos, Vector2 vel, float size, GoreMatter matter)
        {
            if (Physics2D.Linecast(origin, pos, Layers.WallMask).collider != null) return;
            Color c = StainColor(matter);
            float sp = vel.magnitude;
            if (sp > 2.2f)
            {
                // 빠르게 떨어진 방울은 길쭉한 자국: 가는 꼬리가 날아간 쪽을 가리킨다.
                float len = size * Random.Range(3.5f, 5f) * Mathf.Clamp(sp / 4f, 0.8f, 1.6f);
                float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg + 180f;
                _decals.Spawn(GoreSprites.Streak(Random.Range(0, GoreSprites.StreakVariants)), pos, ang, new Vector3(len, len, 1f), c, StainOrder);
                return;
            }
            float s = size * Random.Range(2.2f, 3.4f);
            _decals.Spawn(RandomSplat(), pos, Random.Range(0f, 360f), new Vector3(s, s * Random.Range(0.8f, 1f), 1f), c, StainOrder);
        }

        /// <summary>살 조각·흙덩이가 땅에 닿음(GoreLayer): 작은 철퍽 자국. 벽 안이면 남기지 않는다.</summary>
        public void OnPieceLanded(Vector2 pos, float size, GoreMatter matter)
        {
            if (Physics2D.OverlapPoint(pos, Layers.WallMask) != null) return;
            float s = Mathf.Max(0.12f, size * Random.Range(1.3f, 1.8f));
            _decals.Spawn(RandomSplat(), pos, Random.Range(0f, 360f), new Vector3(s, s * Random.Range(0.75f, 1f), 1f), StainColor(matter), StainOrder);
        }

        /// <param name="angleDeg">NaN이면 무작위.</param>
        void AddSplat(Vector2 pos, float size, GoreMatter matter, float angleDeg)
        {
            float a = float.IsNaN(angleDeg) ? Random.Range(0f, 360f) : angleDeg;
            var scale = new Vector3(size * Random.Range(0.95f, 1.25f), size * Random.Range(0.75f, 1f), 1f);
            _decals.Spawn(RandomSplat(), pos, a, scale, StainColor(matter), StainOrder);
        }

        /// <summary>웅덩이: 0.22배에서 seconds 동안 번진다(끝이 느려지는 번짐).</summary>
        void AddPool(Vector2 pos, float size, float seconds, GoreMatter matter)
        {
            Color c = matter == GoreMatter.Nest ? GoreColors.PusDark : GoreColors.Pool;
            float angle = Random.Range(0f, 360f);
            var to = new Vector3(size * Random.Range(1f, 1.25f), size * Random.Range(0.72f, 0.92f), 1f);
            ref var pool = ref _pools.Spawn(GoreSprites.Pool(Random.Range(0, GoreSprites.PoolVariants)), pos, angle, to * 0.22f, c, PoolOrder);
            GoreLayer.Animate(ref pool, to, c, angle, seconds, true);
        }

        static Color StainColor(GoreMatter matter)
        {
            if (matter == GoreMatter.Nest) return Random.value < 0.4f ? GoreColors.PusDark : new Color(GoreColors.Dirt.r, GoreColors.Dirt.g, GoreColors.Dirt.b, 0.85f);
            var c = GoreColors.Stain;
            c.a *= Random.Range(0.8f, 1f);
            return c;
        }

        static readonly Color DirtPuff = new Color(0.36f, 0.29f, 0.21f, 0.7f);

        static Sprite RandomSplat() => GoreSprites.Splat(Random.Range(0, GoreSprites.SplatVariants));

        /// <summary>액터 정렬(YSort: 1000 − y×20). 날아가는 조각이 몸들과 같은 층에서 겹치게.</summary>
        static int ActorOrder(float y) => 1000 - Mathf.RoundToInt(y * 20f);

        /// <summary>날아갈 거리 안에 벽이 있으면 벽 앞에서 떨어지게 속도를 줄인다(조각이 다른 칸으로 넘어가지 않게).</summary>
        static Vector2 ClampToWalls(Vector2 from, Vector2 vel, float up, float height)
        {
            float speed = vel.magnitude;
            if (speed < 0.01f) return vel;
            float g = GoreLayer.Gravity;
            float t = (up + Mathf.Sqrt(up * up + 2f * g * height)) / g;
            float dist = speed * t * 0.85f + 0.15f;
            var hit = Physics2D.Raycast(from, vel / speed, dist, Layers.WallMask);
            if (hit.collider == null) return vel;
            return vel * Mathf.Clamp01((hit.distance - 0.12f) / dist);
        }
    }
}
