using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정예 접두사 '무리 거느린'(3차 초안 3-4): 굴쥐 4와 함께 나오고, 깨어 있는 동안 12초마다 2마리를 더 부른다(최대 6, 보상 없음).
    /// 우두머리가 쓰러지면 PackFear가 ScatterPack으로 졸개를 4초 겁먹게 하고 절반은 어둠으로 사라지게 한다(기획/전투-보스-무기-다듬기-1차.md 4-2 [2]).
    /// </summary>
    public sealed class PackLeader : MonoBehaviour
    {
        const float Interval = 12f;
        const int MaxPack = 6;

        Enemy _leader;
        readonly List<Enemy> _pack = new List<Enemy>();
        float _timer = Interval;

        public void Bind(Enemy leader, IEnumerable<Enemy> startingPack)
        {
            _leader = leader;
            foreach (var e in startingPack) Add(e);
        }

        void Add(Enemy e)
        {
            if (!e) return;
            _pack.Add(e);
            // 둥지 굴쥐(공포 1.0초)와 가려내도록 졸개라고 적어 둔다.
            if (e is RatBrain rat) rat.InPack = true;
        }

        /// <summary>거느린 무리인가(살아 있든 아니든 목록에 있으면).</summary>
        public bool Has(Enemy e) => e && _pack.Contains(e);

        /// <summary>살아 있는 졸개 수.</summary>
        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var e in _pack)
                    if (e && !e.Dead) n++;
                return n;
            }
        }

        /// <summary>
        /// 우두머리가 쓰러짐(PackFear): 살아 있는 졸개를 seconds 동안 겁먹게 하고, 마리마다 vanishChance 확률로 달아나다 흐려져 사라지게 한다
        /// (보상 없는 졸개라 손해가 없다). 잠든 졸개도 깨운다(무리를 다시 깨우지는 않음). 겁먹인 수를 돌려준다.
        /// </summary>
        public int ScatterPack(float seconds, float vanishChance, Vector2 from)
        {
            _pack.RemoveAll(r => !r || r.Dead);
            int n = 0;
            foreach (var e in _pack.ToArray())
            {
                if (!(e is RatBrain rat)) continue;
                if (!rat.Aware) rat.Wake(false);
                if (PackFear.Frighten(rat, from, seconds, Random.value < vanishChance)) n++;
            }
            return n;
        }

        void Update()
        {
            if (!_leader || _leader.Dead || !_leader.Aware) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = Interval;
            _pack.RemoveAll(r => !r || r.Dead);
            int room = MaxPack - _pack.Count;
            for (int i = 0; i < Mathf.Min(2, room); i++)
            {
                Vector2 pos = _leader.Position + Random.insideUnitCircle.normalized * 1.6f;
                // 벽에 붙은 우두머리 너머(방 밖)로 나오지 않게, 벽과 겹치거나 벽 너머면 우두머리 자리에서 나온다.
                if (Physics2D.OverlapCircle(pos, 0.3f, Layers.WallMask) || Physics2D.Linecast(_leader.Position, pos, Layers.WallMask)) pos = _leader.Position;
                var rat = EnemySpawner.Create(MonsterKind.Rat, _leader.Floor, pos);
                rat.NoReward = true;
                rat.GroupId = _leader.GroupId;
                Add(rat);
                EnemySpawner.Track(rat);
            }
        }
    }
}
