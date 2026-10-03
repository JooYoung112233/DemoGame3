using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점(완전 탑다운) 시험판: 플레이어·적을 위에서 내려다본 임시 그림으로 바꾸고 무기만 콤보 궤적대로 휘두른다(1인 개발용: 몸 동작 그림 없이 무기만 움직임).
    /// 두 시험장(전투 시험장·던전) 루트에 붙고, 시험 패널 토글(Enabled)로 지금 횡 그림·도형과 바로 바꿔 비교한다.
    /// 순서: 꺼지면 Update에서 도형 상태로 되돌려(PlayerVisual·EnemyVisual이 그 프레임 LateUpdate부터 다시 맡음), 켜져 있으면
    /// PlayerVisual·EnemyVisual(기본 순서)이 도형으로 물러난 뒤 이 LateUpdate(순서 50, YSort·Enemy.LateUpdate 뒤)가 몸을 맡아 그린다.
    /// 판정 반지름·충돌·손맛 수치는 건드리지 않는다. 매 프레임 할당 없음(그림은 처음 한 번 만들어 캐시).
    /// 그림 칸: '연결된 그림' 모드면 CombatArtSet.topDown 칸의 그림을 쓰고, 빈 칸과 다른 모드는 코드로 그린 임시 그림(TopDownSprites)을 쓴다
    /// (기획/타격감-리소스-명세.md 2026-10-03판). 패널에서 모드를 바꾸면 다음 프레임에 바로 바뀐다.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class TopDownView : MonoBehaviour
    {
        public static TopDownView Instance { get; private set; }
        /// <summary>시험 패널에서 켜고 끈다(지금 횡 그림·도형과 비교). 패널(OnGUI)에서만 바꾼다고 보고 다음 프레임에 반영한다.</summary>
        public static bool Enabled { get; set; } = true;
        /// <summary>이 장면에서 정수리 시점이 몸을 맡는가(두 시험장 루트가 있을 때만).</summary>
        public static bool Active => Enabled && Instance;
        /// <summary>시험·확인용: 지금 플레이어 검사 틀(겉모습 고른 횟수·고른 그림을 읽는다). 없으면 null.</summary>
        public static TopDownPlayerRig PlayerRig => Instance ? Instance._player : null;

        TopDownPlayerRig _player;
        readonly List<TopDownEnemyRig> _enemies = new List<TopDownEnemyRig>(64);
        readonly Dictionary<Enemy, TopDownEnemyRig> _byEnemy = new Dictionary<Enemy, TopDownEnemyRig>(64, SameObject.Comparer);
        bool _anyApplied;

        void Awake()
        {
            Instance = this;
            // 그림을 장면을 여는 동안 미리 만든다(처음 보는 적이 나올 때 전투 중에 멈칫하지 않게).
            TopDownSprites.Prewarm();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>도메인 다시 불러오기 꺼짐: 두 루트의 ResetStatics에서 부른다. 시험 기본값은 켜짐.</summary>
        public static void ResetStatics()
        {
            Instance = null;
            Enabled = true;
        }

        void Update()
        {
            if (Active || !_anyApplied) return;
            // 꺼짐: 도형 상태로 되돌린다. 이번 프레임 LateUpdate부터 PlayerVisual·EnemyVisual이 그림 모드를 다시 맡는다.
            _player?.Restore();
            for (int i = 0; i < _enemies.Count; i++) _enemies[i].Restore();
            _anyApplied = false;
        }

        /// <summary>
        /// 지금 쓸 정수리 그림 칸: '연결된 그림' 모드면 그 장면 루트의 CombatArtSet.topDown, 시험용 그림 모드면 시험 묶음의 빈 칸(→ 임시 그림),
        /// 도형 모드면 null(→ 임시 그림). 칸 하나하나가 비어 있으면 그 부품만 임시 그림을 쓴다.
        /// </summary>
        public static TopDownArt CurrentArt()
        {
            var set = ArtRuntime.Active;
            return set ? set.topDown : null;
        }

        void LateUpdate()
        {
            Prune();
            if (!Active) return;
            float dt = Time.deltaTime;
            var art = CurrentArt();

            var p = PlayerController.Instance;
            if (p && (_player == null || _player.Player != p))
            {
                _player?.Restore();
                _player = new TopDownPlayerRig(p);
            }
            if (_player != null && _player.Valid)
            {
                _player.Apply();
                _player.Drive(dt, art);
                _anyApplied = true;
            }

            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!e) continue;
                if (!_byEnemy.ContainsKey(e))
                {
                    // 쓰러지는 중에 처음 보는 적은 처치 연출을 그대로 둔다.
                    if (e.Dead) continue;
                    var rig = TopDownEnemyRig.Create(e);
                    if (rig == null) continue;
                    _byEnemy[e] = rig;
                    _enemies.Add(rig);
                }
            }
            for (int i = 0; i < _enemies.Count; i++)
            {
                var rig = _enemies[i];
                rig.Apply();
                rig.Drive(dt, art);
                _anyApplied = true;
            }
        }

        /// <summary>지워진 적의 틀을 뺀다(부품은 적과 함께 지워짐).</summary>
        void Prune()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var rig = _enemies[i];
                if (rig.Alive) continue;
                _byEnemy.Remove(rig.Enemy);
                int last = _enemies.Count - 1;
                _enemies[i] = _enemies[last];
                _enemies.RemoveAt(last);
            }
            if (_player != null && !_player.Valid) _player = null;
        }

        /// <summary>
        /// 부품 스프라이트 하나를 만든다(정렬 층·시야 숨김 상태는 몸을 따른다). 정렬 순서는 매 프레임 몸 순서 기준으로 다시 넣으므로 YSort.Refresh가 필요 없다.
        /// </summary>
        public static SpriteRenderer Part(string name, Transform parent, Sprite sprite, Material material, Color color, SpriteRenderer like)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material) sr.sharedMaterial = material;
            sr.color = color;
            if (like)
            {
                sr.sortingLayerID = like.sortingLayerID;
                sr.sortingOrder = like.sortingOrder;
                sr.forceRenderingOff = like.forceRenderingOff;
            }
            return sr;
        }

        /// <summary>
        /// 적을 C# 참조 그대로 비교한다. 지워진 Unity 물체끼리는 == 가 같다고 나오므로(둘 다 '없음') 사전 키로는 참조 비교를 쓴다.
        /// </summary>
        sealed class SameObject : IEqualityComparer<Enemy>
        {
            public static readonly SameObject Comparer = new SameObject();

            public bool Equals(Enemy a, Enemy b) => ReferenceEquals(a, b);

            public int GetHashCode(Enemy e) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(e);
        }
    }
}
