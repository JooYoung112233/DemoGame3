using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 의뢰 셈(기획/마을-의뢰-첫판.md 4-3). 던전 사건을 QuestEvent로 바꿔 QuestBook.Handle에 넣는다. DungeonRoot에만 붙인다
    /// (전투 시험장 처치는 꾸러미 흐름 밖이라 세지 않음). 의뢰 상태는 모두 꾸러미(ProfileCarry)에 바로 적히므로 쓰러지거나 장면을 바꿔도 잃지 않는다.
    /// 층 들어섬(FloorEntered, 다시 짓기는 세지 않음) · 바구니로 올라감(ExpeditionEnding) · 말뚝 켬(StakeLit) ·
    /// 처치: 스킬 목표는 피해 사건(PlayerDealtDamage, d.Killed)에서만, 보스 목표는 처치 사건(EnemyKilled)에서만 센다.
    /// 처치 사건이 피해 사건보다 먼저 나가므로(Enemy.TakeHit) 이렇게 갈라야 한 번 처치가 같은 목표를 두 번 채우지 않는다(11장 위험 7).
    /// 오우거 굴의 보스 처치도 처치 사건 하나로만 센다(DungeonEvents.BossDefeated는 듣지 않는다 — 같은 처치를 두 번 세지 않게).
    /// 도움 2초(SkillAssist): 적이 맞을 때마다 출처를 적어 두고, 처치 타격이 스킬이 아니어도 2초 안에 그 스킬에 맞았으면 인정한다.
    /// 쓰러지면 다음 대화 반응(rx:downed, 살아 있는 보스가 있으면 rx:ogre_lost와 그 판에 가장 많이 맞은 패턴 힌트)을 적고, 보스를 쓰러뜨리면 rx:ogre_lost를 지운다.
    /// 알림(DungeonEvents.Say)에 사람을 가리키는 글은 QuestBook이 SpeakerIdentity.ReportTarget을 거쳐 만든다(이름 공개 전에는 자리 이름).
    /// </summary>
    public sealed class QuestTracker : MonoBehaviour
    {
        /// <summary>적 → 마지막으로 맞힌 스킬과 시각(게임 시간). 장면이 사라질 때 비운다.</summary>
        readonly SkillAssist _assist = new SkillAssist();
        /// <summary>도움 2초 열쇠: 적마다 이 장면에서 처음 맞을 때 붙인 번호(같은 물체만 같게 봄).</summary>
        readonly Dictionary<Enemy, int> _ids = new Dictionary<Enemy, int>(EnemyRef.Comparer);
        int _nextId;

        void Awake()
        {
            DungeonEvents.FloorEntered += OnFloorEntered;
            DungeonEvents.ExpeditionEnding += OnExpeditionEnding;
            DungeonEvents.StakeLit += OnStakeLit;
            CombatEvents.PlayerDealtDamage += OnDealt;
            CombatEvents.EnemyKilled += OnEnemyKilled;
            CombatEvents.PlayerDowned += OnPlayerDowned;
            // 묶음 3 나-10 새 의뢰: 벽 박기·찾은 것(광맥·금고)·오우거 기둥 무너짐.
            CombatEvents.EnemyWallSlam += OnWallSlam;
            DungeonEvents.Discovered += OnDiscovered;
            OgreBrain.PillarBroken += OnPillarBroken;
        }

        void OnDestroy()
        {
            DungeonEvents.FloorEntered -= OnFloorEntered;
            DungeonEvents.ExpeditionEnding -= OnExpeditionEnding;
            DungeonEvents.StakeLit -= OnStakeLit;
            CombatEvents.PlayerDealtDamage -= OnDealt;
            CombatEvents.EnemyKilled -= OnEnemyKilled;
            CombatEvents.PlayerDowned -= OnPlayerDowned;
            CombatEvents.EnemyWallSlam -= OnWallSlam;
            DungeonEvents.Discovered -= OnDiscovered;
            OgreBrain.PillarBroken -= OnPillarBroken;
            _assist.Clear();
            _ids.Clear();
        }

        static int Floor => DungeonRoot.Instance ? DungeonRoot.Instance.Floor : 0;

        void OnFloorEntered(int floor, bool firstVisit, ArrivalKind arrival) =>
            Feed(QuestEvent.FloorEntered(floor, arrival == ArrivalKind.Rebuild));

        void OnExpeditionEnding() => Feed(QuestEvent.Ascended(Floor));

        void OnStakeLit(int floor, bool stairsFront) => Feed(QuestEvent.StakeLit(floor, stairsFront));

        void OnWallSlam(Enemy e, WallSlamHit hit)
        {
            if (!e || e.IsDummy || e.NoReward) return;
            Feed(QuestEvent.WallSlammed(e.Kind, Floor));
        }

        void OnDiscovered(DiscoveryKind kind, Vector2 pos, string label)
        {
            if (kind == DiscoveryKind.Ore || kind == DiscoveryKind.Safe) Feed(QuestEvent.Found(kind, Floor));
        }

        void OnPillarBroken() => Feed(QuestEvent.PillarBroken(MonsterKind.Ogre, Floor));

        /// <summary>
        /// 피해 사건: 맞을 때마다 도움 2초 기록을 남기고, 처치 타격이면 스킬 목표용 처치(출처 Whirlwind·SwordWave·Other)를 넣는다.
        /// 적의 피해(DamageSource.Enemy)는 플레이어 몫이 아니라 넣지 않는다. 허수아비·보상 없는 적은 NoReward로 보내 세지 않게 한다.
        /// </summary>
        void OnDealt(DamageDealt d)
        {
            var e = d.Target;
            if (!e || d.Source == DamageSource.Enemy) return;
            int id = IdOf(e);
            double now = Time.timeAsDouble;
            var source = ToKillSource(d.Source);
            _assist.Hit(id, source, now);
            if (!d.Killed) return;
            var assist = _assist.AssistFor(id, now);
            _assist.Forget(id);
            _ids.Remove(e);
            Feed(QuestEvent.Killed(e.Kind, source, e.NoReward || e.IsDummy, e.IsBoss, assist, Floor));
        }

        int IdOf(Enemy e)
        {
            if (!_ids.TryGetValue(e, out int id))
            {
                id = ++_nextId;
                _ids[e] = id;
            }
            return id;
        }

        /// <summary>
        /// 처치 사건: 출처를 따지지 않는 처치(보스 목표만 센다). 허수아비는 처치 사건이 나가지 않는다.
        /// 보스를 쓰러뜨리면 지난 패배 반응(rx:ogre_lost와 힌트)을 지운다(TownSave.NoteBossKilled, 이긴 뒤 무진 첫 줄이 패배 공략으로 시작하지 않게).
        /// </summary>
        void OnEnemyKilled(Enemy e)
        {
            if (!e || e.IsDummy) return;
            if (e.IsBoss && !e.NoReward) TownSave.NoteBossKilled(ProfileCarry.Ensure());
            Feed(QuestEvent.Killed(e.Kind, KillSource.None, e.NoReward, e.IsBoss, KillSource.None, Floor));
        }

        void OnPlayerDowned() => TownSave.NoteDowned(ProfileCarry.Ensure(), BossAlive(), LossHint());

        static bool BossAlive()
        {
            foreach (var e in Enemy.All)
                if (e && !e.Dead && e.IsBoss) return true;
            return false;
        }

        /// <summary>
        /// 무진 반응 줄 힌트(전투 문서 3-7): 살아 있는 오우거가 이번 판에 가장 많이 맞힌 패턴(OgreBrain.MostHitPattern).
        /// 돌진 → Charge, 내려찍기 → Slam, 그 밖·맞은 적 없음·오우거 없음 → None(두 줄 그대로). 판 기록은 다음 판이 깰 때 비우므로 쓰러진 순간에도 남아 있다.
        /// </summary>
        static OgreLossHint LossHint()
        {
            var ogre = OgreBrain.Current;
            return ogre && !ogre.Dead ? TownSave.LossHintFor(ogre.MostHitPattern) : OgreLossHint.None;
        }

        /// <summary>피해 출처 → 의뢰 처치 출처. 회오리·검풍만 스킬이고, 평타·전설·출혈·환경은 Other(스킬 목표는 도움 2초로만 셈).</summary>
        public static KillSource ToKillSource(DamageSource source)
        {
            switch (source)
            {
                case DamageSource.Whirlwind: return KillSource.Whirlwind;
                case DamageSource.SwordWave: return KillSource.SwordWave;
                default: return KillSource.Other;
            }
        }

        /// <summary>
        /// 사건 하나를 꾸러미의 의뢰에 넣고, 바뀐 의뢰마다 알림 한 줄(진행 "회오리로 굴쥐 3/8", 달성 "의뢰 끝 — …")을 띄운다.
        /// 시험 패널('오프닝 의뢰 받기' 뒤 층 들어섬 다시 넣기)도 부른다.
        /// </summary>
        public static List<QuestUpdate> Feed(QuestEvent e)
        {
            var updates = new QuestBook(ProfileCarry.Ensure()).Handle(e);
            foreach (var u in updates)
            {
                if (u.Notice != null) DungeonEvents.Say(u.Notice);
                Debug.Log($"[의뢰] {e} → {u}");
            }
            if (updates.Count > 0) QuestHud.Refresh();
            return updates;
        }
    }
}
