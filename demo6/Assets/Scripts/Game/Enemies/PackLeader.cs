using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정예 접두사 '무리 거느린'(3차 초안 3-4): 굴쥐 4와 함께 나오고, 깨어 있는 동안 12초마다 2마리를 더 부른다(최대 6, 보상 없음).
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
            _pack.AddRange(startingPack);
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
                _pack.Add(rat);
                EnemySpawner.Track(rat);
            }
        }
    }
}
