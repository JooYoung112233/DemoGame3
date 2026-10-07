using Demo6.Core.Combat;
using Demo6.Core.Progression;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 투지와 배운 기술(기획/스킬-자원-트리-1차.md, 사용자 결정 2026-10-07).
    /// - 회오리·검풍은 배워야 쓴다(PlayerProgress.ApplyToPlayer가 WhirlKnown·WaveKnown을 넣음). 진행이 없는 장면(전투 시험장)은 기본값 true.
    /// - 투지(SpiritRules): 일반 공격·무기 행동 피해·받아치기·처치로 차고, 싸움이 끝나면 빠진다. 회오리 40, 검풍 50.
    /// - 갈림 칸: 끌어당기는 회오리·피의 회오리, 세 갈래 검풍·벽 울림. 마무리 일격 칸이 있으면 마무리 적중 +8, 끓는 피는 얻는 양을 늘린다.
    /// 막혔을 때(모름·투지 모자람) 입력을 먹고 머리 위 짧은 글을 1초에 한 번 띄운다.
    /// </summary>
    public sealed partial class PlayerController
    {
        static readonly Color RefuseColor = new Color(0.85f, 0.8f, 0.72f, 0.9f);
        const float RefuseGap = 1f;

        /// <summary>F1 손잡이: 투지를 쓰지 않는다(스킬 수치 시험용).</summary>
        public static bool SpiritFree;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSpiritStatics() => SpiritFree = false;

        public float Spirit { get; private set; }
        public float SpiritMax => SpiritRules.Max;
        /// <summary>마지막으로 투지를 얻은 실제 시각(HUD 막대 번쩍임).</summary>
        public float SpiritGainTime { get; private set; } = -999f;
        /// <summary>마지막으로 투지가 모자라 막힌 실제 시각(HUD 막대 붉게).</summary>
        public float SpiritShortTime { get; private set; } = -999f;

        public bool WhirlKnown { get; set; } = true;
        public bool WaveKnown { get; set; } = true;
        public bool PullWhirl { get; set; }
        public bool BloodWhirl { get; set; }
        public bool ThreeWave { get; set; }
        public bool WallBurst { get; set; }
        public bool FinisherSpirit { get; set; }
        public int BoilingRank { get; set; }

        /// <summary>HUD 칸: 지금 쓸 수 있나(배움 + 투지, 재사용 대기는 따로).</summary>
        public bool WhirlAffordable => WhirlKnown && (SpiritFree || SpiritRules.CanPay(Spirit, SpiritRules.WhirlCost));
        public bool WaveAffordable => WaveKnown && (SpiritFree || SpiritRules.CanPay(Spirit, SpiritRules.WaveCost));

        float _refuseAt = -999f;
        float _bloodGiven;

        /// <summary>HUD 구슬 오른쪽 '자원' 몫(UiV45.DrawOrb, HudSkillResource.Provider)에 투지를 잇는다.</summary>
        static HudSkillResource? SpiritForHud(PlayerController p) => p ? new HudSkillResource("투지", p.Spirit, p.SpiritMax) : (HudSkillResource?)null;

        void SpiritAwake()
        {
            HudSkillResource.Provider = SpiritForHud;
            CombatEvents.PlayerDealtDamage += OnSpiritDealt;
            CombatEvents.PlayerBasicHit += OnSpiritBasicHit;
            CombatEvents.EnemyKilled += OnSpiritKill;
        }

        void SpiritDestroy()
        {
            if (Instance == this || Instance == null) HudSkillResource.Provider = null;
            CombatEvents.PlayerDealtDamage -= OnSpiritDealt;
            CombatEvents.PlayerBasicHit -= OnSpiritBasicHit;
            CombatEvents.EnemyKilled -= OnSpiritKill;
        }

        /// <summary>원정·층 시작, 다시 서기: 투지 0.</summary>
        public void ClearSpirit() => Spirit = 0f;

        /// <summary>시험 패널: 투지를 가득.</summary>
        public void FillSpirit() => Spirit = SpiritRules.Max;

        void GainSpirit(float raw)
        {
            float gain = SpiritRules.Scaled(raw, BoilingRank);
            if (gain <= 0f || _state == State.Down) return;
            float before = Spirit;
            Spirit = SpiritRules.Add(Spirit, gain);
            if (Spirit > before) SpiritGainTime = Time.unscaledTime;
        }

        void TickSpirit(float dt)
        {
            if (Spirit <= 0f) return;
            var lighting = DungeonLighting.Instance;
            bool inCombat = lighting ? lighting.InCombat : Time.time - LastCombatActionTime < SpiritRules.DecayDelay;
            Spirit = SpiritRules.Decay(Spirit, inCombat, Time.time - LastCombatActionTime, dt);
        }

        void OnSpiritDealt(DamageDealt d)
        {
            // 일반 공격·무기 행동·패링 피해(Basic)만. 스킬·전설·출혈·환경 피해는 투지를 주지 않는다.
            if (d.Source != DamageSource.Basic || !d.Target) return;
            GainSpirit(SpiritRules.FromDamage(d.Amount, Attack));
        }

        void OnSpiritBasicHit(BasicHitInfo info)
        {
            if (FinisherSpirit && info.Finisher && info.HitIndex == 0) GainSpirit(SpiritRules.FinisherGain);
        }

        void OnSpiritKill(Enemy e)
        {
            if (e && !e.IsDummy) GainSpirit(SpiritRules.KillGain);
        }

        /// <summary>받아치기(패링) 성공(ParryHit).</summary>
        void SpiritOnParry() => GainSpirit(SpiritRules.ParryGain);

        /// <summary>
        /// 회오리(1)·검풍(2)을 지금 쓸 수 있나. 못 쓰면 그 입력(버퍼·대기)을 먹고 까닭을 머리 위에 띄운다. 재사용 대기는 부르는 쪽이 먼저 본다.
        /// </summary>
        bool SkillGate(int which)
        {
            bool known = which == 1 ? WhirlKnown : WaveKnown;
            float cost = which == 1 ? SpiritRules.WhirlCost : SpiritRules.WaveCost;
            if (known && (SpiritFree || SpiritRules.CanPay(Spirit, cost))) return true;
            if (which == 1)
            {
                _input.ConsumeSkill1();
                _pendingSkill1 = false;
            }
            else
            {
                _input.ConsumeSkill2();
                _pendingSkill2 = false;
            }
            if (known) SpiritShortTime = Time.unscaledTime;
            float now = Time.unscaledTime;
            if (now - _refuseAt >= RefuseGap)
            {
                _refuseAt = now;
                WorldOverlay.Text(Position + Vector2.up * 1.1f, known ? "투지가 모자라다" : "아직 모르는 기술", RefuseColor, 0.85f);
                if (!known) DungeonEvents.Say((which == 1 ? "회오리 베기" : "검풍") + "는 아직 모른다 — 레벨 2부터 마을 " + Demo6.Core.Town.SpeakerIdentity.TeachAt(ProfileCarry.Data) + " 배운다");
            }
            return false;
        }

        void PaySpirit(float cost)
        {
            if (!SpiritFree) Spirit = Mathf.Max(0f, Spirit - cost);
        }

        /// <summary>이번 원정에 챙긴 식은 주먹밥 수(던전 원정 몫, 없으면 0).</summary>
        public int RiceBalls
        {
            get
            {
                var root = DungeonRoot.Instance;
                return root && root.Leg != null ? root.Leg.RiceBalls : 0;
            }
        }

        /// <summary>식은 주먹밥(묶음 5-7): 물약이 없을 때 R. 체력 15%, 물약과 같은 재사용 대기.</summary>
        void TryEatRice()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.Leg == null || root.Leg.RiceBalls <= 0) return;
            if (_potionCooldown > 0f || _health.Current >= _health.Max) return;
            int healed = _health.Heal(Mathf.RoundToInt(_health.Max * Demo6.Core.Dungeon.DownRules.RiceHealFraction));
            root.Leg.RiceBalls--;
            _potionCooldown = PotionCooldownTime;
            WorldOverlay.Number(Position + Vector2.up * Radius, healed, NumberKind.Heal);
            DungeonEvents.Say("식은 주먹밥을 삼켰다 — 딱딱하지만 배는 찬다");
        }

        /// <summary>피의 회오리: 회오리 한 번에 맞힌 수만큼(상한 SpiritRules.BloodWhirlCap).</summary>
        void BloodWhirlHit()
        {
            if (!BloodWhirl || _bloodGiven >= SpiritRules.BloodWhirlCap) return;
            float add = Mathf.Min(SpiritRules.BloodWhirlPerHit, SpiritRules.BloodWhirlCap - _bloodGiven);
            _bloodGiven += add;
            GainSpirit(add);
        }

        /// <summary>검풍 내보내기: 세 갈래면 ±15° 둘을 더하고 하나에 60%.</summary>
        void SpawnWaves()
        {
            Vector2 origin = Position + _waveDir * (Radius + 0.1f);
            if (!ThreeWave)
            {
                SwordWave.Spawn(origin, _waveDir, this);
                return;
            }
            float scale = SkillTree.ThreeWaveScale;
            SwordWave.Spawn(origin, _waveDir, this, scale);
            SwordWave.Spawn(origin, Rotate(_waveDir, SkillTree.ThreeWaveSpread), this, scale);
            SwordWave.Spawn(origin, Rotate(_waveDir, -SkillTree.ThreeWaveSpread), this, scale);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>
        /// 벽 울림(SwordWave가 벽에 닿을 때): 그 자리 반경 1.5 안 적에게 100%(치명 없음, 스킬 피해·보스 피해 보너스는 받음). 넉백은 터진 자리에서 바깥으로 0.6.
        /// </summary>
        public void WaveBurst(Vector2 at)
        {
            if (!WallBurst) return;
            SwingVisual.ShowRing(at, SkillTree.WallBurstRadius);
            Sfx.Play(SfxKind.Hit);
            ScreenShake.Add(0.05f, 0.08f);
            bool any = false;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead) continue;
                Vector2 away = e.Position - at;
                if (away.magnitude - e.Radius > SkillTree.WallBurstRadius) continue;
                int damage = DamageMath.ToMonster(Attack, SkillTree.WallBurstPercent, false, CritDamage, DamageMath.Roll(_rng), 0, SkillDamageBonus, true, BossDamageBonus, e.IsBoss);
                int applied = e.TakeHit(damage, false, DamageSource.SwordWave, WavePoise * 0.5f, false, 1f, out _, out _);
                if (applied <= 0) continue;
                any = true;
                e.ApplyKnockback(away.sqrMagnitude > 0.0001f ? away : Vector2.up, 0.6f, KnockInfo.Player(DamageSource.SwordWave, applied));
                HitEffects.OnHit(e, away, CritTier.None, true);
                if (e.Dead) OnKills(1);
            }
            if (any) TimeScaleService.HitStop(0.04f);
        }
    }
}
