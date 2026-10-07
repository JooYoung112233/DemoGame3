using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 무기 행동 연출 한 입구(기획/세-무기-우클릭-소켓-1차.md 2-5·2-6·3-6·3-8·4-6·5-6·5-9·7-3 '효과'). 부르는 곳: PlayerController(꾸러미 ③)만.
    /// 소리(SfxKind.Block·Parry·GuardBreak·ChargeRing·ArmorHold), 막은 숫자(NumberKind.Blocked), 머리 위 글(WorldOverlay.Text),
    /// 불꽃(HitEffects.Sparks), 칼 그림(SwingVisual.ShowRelease·ShowFlurry), 휘두르기 소리(WeaponSfx)를 낸다.
    /// 판정·피해·넉백·무적 규칙은 PlayerController가 정하고, 여기는 보이고 들리는 것만 낸다. 예외: 패링 히트스톱 0.06·흔들림 0.04/0.06은 여기서 준다(2-6).
    /// </summary>
    public static class WeaponActFx
    {
        /// <summary>머리 위 글 높이(플레이어 몸 위, 피해 숫자 Radius보다 조금 위).</summary>
        const float WordLift = 0.5f;
        /// <summary>방패 앞 불꽃 자리(몸 중심에서 보는 쪽으로).</summary>
        const float ShieldReach = 0.55f;

        /// <summary>'튕겨 냄!' 밝은 회백(던전에서도 순수 흰색이 아니라 뼈색으로 바뀌지 않음).</summary>
        static readonly Color ParryWordColor = new Color(0.93f, 0.94f, 0.96f, 1f);
        /// <summary>'끊김' 회색.</summary>
        static readonly Color InterruptWordColor = new Color(0.64f, 0.64f, 0.66f, 1f);
        /// <summary>'버팀' 청록 #5FD6C8(버팀 룬 색과 같음).</summary>
        static readonly Color SuperArmorWordColor = new Color(0x5F / 255f, 0xD6 / 255f, 0xC8 / 255f, 1f);
        /// <summary>'튕겨 냄!' 크기(보통 짧은 글자 대비).</summary>
        const float ParryWordScale = 1.1f;

        /// <summary>패링 흰 불꽃.</summary>
        static readonly Color ParrySparkA = new Color(1f, 1f, 1f, 1f);
        static readonly Color ParrySparkB = new Color(0.86f, 0.9f, 1f, 1f);
        /// <summary>막음 작은 불꽃(쇠테 불꽃 ~ 판자 부스러기).</summary>
        static readonly Color BlockSparkA = new Color(0.95f, 0.86f, 0.62f, 1f);
        static readonly Color BlockSparkB = new Color(0.52f, 0.4f, 0.27f, 1f);
        /// <summary>막기 깨짐 판자 부스러기.</summary>
        static readonly Color BreakChipA = new Color(0.56f, 0.43f, 0.29f, 1f);
        static readonly Color BreakChipB = new Color(0.33f, 0.24f, 0.16f, 1f);

        static Vector2 WordAt(Vector2 playerPos) => playerPos + Vector2.up * (PlayerController.Radius + WordLift);

        static Vector2 Unit(Vector2 v) => v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.right;

        /// <summary>
        /// 방패로 막음(2-5): 소리 '퉁'(SfxKind.Block, 보스 타는 조금 크고 낮게), 머리 위 회색 숫자(NumberKind.Blocked, 0.8배, damage가 0이어도 0을 띄움),
        /// 방패 앞 작은 불꽃(보통 3개, 보스 5개). 깜빡임·피격 흔들림은 없다(보스 타만 짧은 흔들림 0.04/0.08).
        /// playerPos = 플레이어 자리, shieldDir = 몸이 보는 쪽(단위), boss = 보스 타(반동이 큼).
        /// </summary>
        public static void Blocked(Vector2 playerPos, Vector2 shieldDir, int damage, bool boss)
        {
            Vector2 dir = Unit(shieldDir);
            if (boss) Sfx.PlayScaled(SfxKind.Block, 1.15f, 0.85f);
            else Sfx.Play(SfxKind.Block);
            WorldOverlay.Number(playerPos + Vector2.up * PlayerController.Radius, Mathf.Max(0, damage), NumberKind.Blocked);
            if (!ContactReactionV046.Shield(playerPos, dir, false))
                HitEffects.Sparks(playerPos + dir * ShieldReach, dir, boss ? 5 : 3, BlockSparkA, BlockSparkB, boss ? 0.75f : 0.6f, 0.8f);
            if (boss) ScreenShake.Add(0.04f, 0.08f);
        }

        /// <summary>
        /// 패링(2-6): 방패 앞 흰 불꽃 8개(넓게), 소리 '쨍'(SfxKind.Parry), 머리 위 '튕겨 냄!'(밝은 회백, 1.1배),
        /// 히트스톱 0.06(ShieldRule.ParryHitStop), 화면 흔들림 0.04/0.06(ShieldRule.ParryShakeAmount·ParryShakeTime).
        /// </summary>
        public static void Parried(Vector2 playerPos, Vector2 shieldDir)
        {
            Vector2 dir = Unit(shieldDir);
            if (!ContactReactionV046.Shield(playerPos, dir, true))
                HitEffects.Sparks(playerPos + dir * ShieldReach, dir, ShieldRule.ParrySparks, ParrySparkA, ParrySparkB, 1.05f, 1.1f);
            Sfx.Play(SfxKind.Parry);
            WorldOverlay.Text(WordAt(playerPos), WeaponActCommon.ParriedWord, ParryWordColor, ParryWordScale);
            TimeScaleService.HitStop(ShieldRule.ParryHitStop);
            ScreenShake.Add(ShieldRule.ParryShakeAmount, ShieldRule.ParryShakeTime);
        }

        /// <summary>
        /// 막기 깨짐(2-5): 방패가 바깥으로 젖혀지며 갈라지는 소리(SfxKind.GuardBreak), 판자 부스러기, 짧은 흔들림 0.06/0.1.
        /// 같은 프레임의 막음 '퉁'과 다른 종류라 함께 들린다.
        /// </summary>
        public static void GuardBroken(Vector2 playerPos, Vector2 shieldDir)
        {
            Vector2 dir = Unit(shieldDir);
            Sfx.Play(SfxKind.GuardBreak);
            HitEffects.Sparks(playerPos + dir * ShieldReach, dir, 5, BreakChipA, BreakChipB, 0.7f, 1.3f);
            ScreenShake.Add(0.06f, 0.1f);
        }

        /// <summary>
        /// 맞아서 무기 행동이 끊김(5-6): 머리 위 회색 '끊김'. 대검(Charge)이면 낮은 툭 소리(벽 '퉁'을 낮고 작게)를 더한다.
        /// 피격 소리·숫자·깜빡임은 PlayerController가 그대로 낸다.
        /// </summary>
        public static void Interrupted(Vector2 playerPos, WeaponActKind kind)
        {
            WorldOverlay.Text(WordAt(playerPos), WeaponActCommon.InterruptedWord, InterruptWordColor, 1f);
            if (kind == WeaponActKind.Charge) Sfx.PlayScaled(SfxKind.Thud, 1.6f, 0.75f);
        }

        /// <summary>버팀 룬으로 버팀(3-8): 머리 위 청록 '버팀', 짧은 쿵(SfxKind.ArmorHold). (0.5초에 한 번은 피격 무적이 보장한다. 칼날 빛 번쩍은 그림 몫.)</summary>
        public static void SuperArmorHeld(Vector2 playerPos)
        {
            WorldOverlay.Text(WordAt(playerPos), WeaponActCommon.SuperArmorWord, SuperArmorWordColor, 1f);
            Sfx.Play(SfxKind.ArmorHold);
        }

        /// <summary>기 모으기 단계에 닿음(3-6): 짧은 쇠 울림(SfxKind.ChargeRing), 음 높이 GreatswordCharge.LevelPitch[level − 1](0.8 → 0.95 → 1.1). 단계마다 조금 커진다.</summary>
        public static void ChargeLevel(Vector2 playerPos, int level)
        {
            var pitches = GreatswordCharge.LevelPitch;
            if (pitches == null || pitches.Length == 0 || level < 1) return;
            int i = Mathf.Clamp(level - 1, 0, pitches.Length - 1);
            Sfx.PlayScaled(SfxKind.ChargeRing, 0.85f + 0.15f * i, pitches[i]);
        }

        /// <summary>
        /// 놓아 베기 판정 순간 칼 그림·휘두르기 소리(3-7). 그림은 대검 ① 걷어 베기 그림을 놓기 단계 크기로(2·3단계는 바깥 잔상 호 하나 더, SwingVisual.ShowRelease).
        /// 소리는 단계 전용 소리(그림 칸 ① swingSound)가 있으면 그것, 없으면 대검 휘두르기 값에서 단계마다 조금 크고 낮게(WeaponSfx.ReleaseSwing).
        /// </summary>
        public static void ReleaseSwing(WeaponAttackRule weapon, int level, ComboStep step, Vector2 origin, Vector2 dir)
        {
            if (weapon == null || step == null) return;
            SwingVisual.ShowRelease(weapon, level, step, origin, dir);
            var set = ArtRuntime.Active;
            var stepArt = set ? set.Weapon(weapon.id)?.Step(0) : null;
            if (stepArt != null && stepArt.swingSound)
            {
                Sfx.Play(SfxKind.Swing, stepArt.swingSound);
                return;
            }
            WeaponSfx.ReleaseSwing(level, out float volume, out float pitch);
            Sfx.PlayScaled(SfxKind.Swing, volume, pitch);
        }

        /// <summary>지금 난사 판정 번호(FlurrySwing이 DealHit 바로 앞에 적음, StepHitEffects가 읽음).</summary>
        static int s_flurryIndex = -1;

        /// <summary>
        /// 난사 한 타 칼 그림·소리(4-6, 7-3 효과): 작은 타는 쌍검 ③ 엇베기 그림을 방향 표(TwinFlurry.EffectDirDeg)만큼 돌리고 베인 자국 2개·번쩍임을,
        /// 마지막 타는 ③ 그림 두 장 X + 자국 4개 + 불꽃 6개(SwingVisual.ShowFlurry). 맞은 적의 자국 방향도 이 타의 표 각만큼 돌린다(StepHitEffects).
        /// 휘두르기 소리는 홀수 번째 작은 타만, 음높이 1.18 ± 0.06(번갈아), 마지막 X는 늘(WeaponSfx.FlurrySwing). index = 0부터 판정 번호.
        /// </summary>
        public static void FlurrySwing(WeaponAttackRule weapon, int index, ComboStep step, Vector2 origin, Vector2 dir)
        {
            if (weapon == null || step == null) return;
            s_flurryIndex = index;
            SwingVisual.ShowFlurry(weapon, index, step, origin, dir);
            if (WeaponSfx.FlurrySwing(index, out float volume, out float pitch)) Sfx.PlayScaled(SfxKind.Swing, volume, pitch);
        }

        /// <summary>
        /// 한 대상 타격 효과(콤보·놓아 베기·난사 공용 DealHit가 부름). step.blunt(방패 치기)면 피 튀김을 줄인다(HitEffects.OnHit blunt 판:
        /// 피 파편 셋에 하나, 나머지는 뼛빛 부스러기, 살아 있는 적에게는 GoreSystem 피를 내지 않음). blunt가 아니면 지금 HitEffects.OnHit 그대로다.
        /// 둔탁한 타격 소리('퉁' + 타격음)는 WeaponSfx.HitClip이 고른다(PlayerController 타격음 자리).
        /// </summary>
        public static void StepHitEffects(Enemy enemy, Vector2 dir, CritTier tier, bool heavy, int extraSparks, ComboStep step)
        {
            // 난사 작은 타: 몸에 남는 자국 방향을 타마다 표 각만큼 돌려 여러 방향으로 벤 것처럼(넉백 0이라 판정·밀림과 무관).
            if (ReferenceEquals(step, TwinFlurry.Small) && s_flurryIndex >= 0 && s_flurryIndex < TwinFlurry.EffectDirDeg.Length)
            {
                float a = TwinFlurry.EffectDirDeg[s_flurryIndex] * Mathf.Deg2Rad;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                dir = new Vector2(dir.x * c - dir.y * sn, dir.x * sn + dir.y * c);
            }
            if (step != null && step.blunt) HitEffects.OnHit(enemy, dir, tier, heavy, extraSparks, true);
            else HitEffects.OnHit(enemy, dir, tier, heavy, extraSparks);
        }
    }
}
