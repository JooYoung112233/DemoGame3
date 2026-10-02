using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 3차 초안 3-4 굴쥐 둥지(시원함 장치). 반경 6에 다가가면 깨어 3초마다 굴쥐 3마리(1층 4)를 부른다(동시에 최대 8).
    /// 처음에는 껍질이 있어 맞아도 '팅' 소리만 난다. 부른 굴쥐를 10마리(1층 12) 잡으면 껍질이 깨져 칠 수 있고 더 부르지 않는다.
    /// 부른 굴쥐는 보상이 없고, 연속 처치 10마리마다 회복 구슬 1개를 준다.
    /// </summary>
    public sealed class NestBrain : Enemy
    {
        const float WakeRadius = 6f;
        const float SummonInterval = 3f;
        const int MaxAlive = 8;
        const float StreakWindow = 2f;

        readonly List<Enemy> _rats = new List<Enemy>();
        bool _shell = true;
        float _summonTimer;
        int _killed;
        int _streak;
        float _lastKill = -999f;
        SpriteRenderer _shellRing;

        int SummonCount => Floor <= 1 ? 4 : 3;
        int ShellBreakKills => Floor <= 1 ? 12 : 10;

        public bool HasShell => _shell;
        public int KilledForShell => _killed;
        public int ShellGoal => ShellBreakKills;

        protected override bool CanBeDamaged => !_shell;

        protected override void OnSpawned()
        {
            var ring = new GameObject("Shell");
            ring.transform.SetParent(transform, false);
            ring.transform.localScale = Vector3.one * (ShapeDiameter * 1.1f);
            _shellRing = ring.AddComponent<SpriteRenderer>();
            _shellRing.sprite = ShapeSprites.Ring;
            _shellRing.color = new Color(0.82f, 0.8f, 0.76f, 0.9f);
            _shellRing.sortingOrder = 1;
            GetComponent<YSort>()?.Refresh();
            Sleep(Vector2.down);
        }

        protected override bool DetectsPlayer(PlayerController player) =>
            (player.Position - Position).magnitude <= WakeRadius;

        protected override void Think(float dt)
        {
            SetPose(EnemyPose.Idle);
            DesiredVelocity = Vector2.zero;
            if (!_shell) return;
            _rats.RemoveAll(r => !r || r.Dead);
            _summonTimer -= dt;
            if (_summonTimer > 0f) return;
            _summonTimer = SummonInterval;
            int room = MaxAlive - _rats.Count;
            for (int i = 0; i < Mathf.Min(room, SummonCount); i++) Summon();
        }

        void Summon()
        {
            Vector2 pos = Position;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 p = Position + Random.insideUnitCircle.normalized * Random.Range(1.2f, 1.8f);
                if (Physics2D.OverlapCircle(p, 0.3f, Layers.WallMask)) continue;
                pos = p;
                break;
            }
            var rat = EnemySpawner.Create(MonsterKind.Rat, Floor, pos);
            rat.NoReward = true;
            rat.GroupId = GroupId;
            if (Territory.HasValue)
            {
                // 칸에 묶인 둥지(3차 초안 2-6): 부른 굴쥐도 같은 칸을 벗어나지 않고, 놓아주면 굴로 돌아가 사라진다.
                rat.BindToTerritory(Territory.Value, pos, Facing);
                rat.DespawnAtHome = true;
            }
            _rats.Add(rat);
            rat.Health.Died += () => OnRatKilled(rat);
            EnemySpawner.Track(rat);
        }

        void OnRatKilled(Enemy rat)
        {
            _streak = Time.time - _lastKill <= StreakWindow ? _streak + 1 : 1;
            _lastKill = Time.time;
            if (_streak % 10 == 0) HealOrb.Spawn(rat.Position);
            if (!_shell) return;
            _killed++;
            if (_killed < ShellBreakKills) return;
            // 껍질이 깨진다: 이제 칠 수 있고 더 부르지 않는다.
            _shell = false;
            if (_shellRing) _shellRing.enabled = false;
            Sfx.Play(SfxKind.Break);
            ScreenShake.Add(0.08f, 0.12f);
            Flash.Flash(Color.white, 0.12f);
            WorldOverlay.Text(Position + Vector2.up * (Radius + 0.6f), "껍질 깨짐!", Palette.NumberCrit);
        }

        protected override void OnBlocked()
        {
            Sfx.Play(SfxKind.Shell);
            var player = Player;
            HitEffects.OnBlocked(Position, player ? Position - player.Position : Vector2.right);
        }
    }
}
