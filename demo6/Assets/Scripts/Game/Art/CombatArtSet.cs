using System;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 그림·소리 교체 자리(기획 '타격감 리소스 명세'). 칸이 비어 있으면 도형과 코드 효과음을 쓴다.
    /// 그림은 모두 오른쪽을 보는 방향으로 그린다. 몸은 좌우만 뒤집고, 베기 이펙트는 조준 방향으로 돌린다.
    /// </summary>
    [CreateAssetMenu(menuName = "Demo6/전투 그림·소리 묶음", fileName = "CombatArtSet")]
    public sealed class CombatArtSet : ScriptableObject
    {
        [Tooltip("그림 피격 번쩍임용 재질(Demo6/SpriteFlash). 비우면 실행 중 셰이더로 만든다.")]
        public Material flashMaterial;
        public PlayerArt player = new PlayerArt();
        public WeaponArt[] weapons = Array.Empty<WeaponArt>();
        public EnemyArt rat = new EnemyArt();
        public EnemyArt boar = new EnemyArt();
        public EnemyArt archer = new EnemyArt();
        public EffectArt effects = new EffectArt();
        public SoundArt sounds = new SoundArt();

        public WeaponArt Weapon(string weaponId)
        {
            if (weapons == null) return null;
            foreach (var w in weapons)
                if (w != null && w.weaponId == weaponId) return w;
            return null;
        }

        public EnemyArt Enemy(Demo6.Core.Combat.MonsterKind kind)
        {
            switch (kind)
            {
                case Demo6.Core.Combat.MonsterKind.Rat: return rat;
                case Demo6.Core.Combat.MonsterKind.Boar: return boar;
                default: return archer;
            }
        }

        /// <summary>그림이 하나라도 들어 있는가(없으면 '연결된 그림' 모드가 의미 없음).</summary>
        public bool HasAnyContent =>
            player.HasAny || rat.HasAny || boar.HasAny || archer.HasAny || effects.HasAny || sounds.HasAny ||
            (weapons != null && Array.Exists(weapons, w => w != null && w.HasAny));
    }

    /// <summary>프레임 묶음. 공격 클립은 '타격 프레임'이 판정 순간에 시작한다.</summary>
    [Serializable]
    public sealed class SpriteClip
    {
        public Sprite[] frames = Array.Empty<Sprite>();
        [Tooltip("반복·한 번 재생 클립의 초당 프레임. 공격 클립은 동작 길이에 맞춰 늘이고 줄이므로 무시.")]
        public float fps = 12f;
        [Tooltip("공격 클립의 타격 프레임 번호(1부터 센 번호, 명세 표의 값). 0이면 동작 길이에 고르게 나눈다.")]
        public int keyFrameNumber;

        public bool Has => frames != null && frames.Length > 0;
        /// <summary>코드용 0부터 번호. 없으면 -1.</summary>
        public int KeyIndex => keyFrameNumber - 1;
    }

    [Serializable]
    public sealed class PlayerArt
    {
        [Tooltip("그림 크기 배율. 그림의 PPU가 정해지기 전 화면 크기를 맞출 때 쓴다.")]
        public float scale = 1f;
        [Tooltip("그림 위치 보정(유닛). 발밑이 충돌 원 중심에 오게 맞춘다.")]
        public Vector2 offset;
        [Tooltip("정지 원화 확인용: 동작 그림이 비어 있으면 대기 그림을 유지한다. 완성 애니메이션이 아님.")]
        public bool previewIdleForMissingClips;
        public SpriteClip idle = new SpriteClip();
        public SpriteClip move = new SpriteClip();
        public SpriteClip dodge = new SpriteClip();
        public SpriteClip whirl = new SpriteClip();
        public SpriteClip waveCast = new SpriteClip();
        public SpriteClip hurt = new SpriteClip();
        public SpriteClip down = new SpriteClip();

        public bool HasAny => idle.Has || move.Has || dodge.Has || whirl.Has || waveCast.Has || hurt.Has || down.Has;
    }

    [Serializable]
    public sealed class WeaponArt
    {
        [Tooltip("wpn_longsword / wpn_greatsword / wpn_twinblades")]
        public string weaponId;
        [Tooltip("콤보 단계 순서대로.")]
        public StepArt[] steps = Array.Empty<StepArt>();

        public StepArt Step(int index) => steps != null && index >= 0 && index < steps.Length ? steps[index] : null;
        public bool HasAny => steps != null && Array.Exists(steps, s => s != null && (s.body.Has || s.slash.Has || s.swingSound || s.impactSound));
    }

    [Serializable]
    public sealed class StepArt
    {
        [Tooltip("몸 동작. 동작 길이에 맞춰 재생, keyFrame이 판정 순간.")]
        public SpriteClip body = new SpriteClip();
        [Tooltip("베기 이펙트. 오른쪽을 향하게 그리고, 조준 방향으로 돌려 판정 순간에 한 번 재생.")]
        public SpriteClip slash = new SpriteClip();
        [Tooltip("이펙트 크기 배율(판정 범위에 맞춤).")]
        public float slashScale = 1f;
        [Tooltip("부채꼴 단계에서 단계마다 위아래를 뒤집어 좌우로 엇갈려 베는 느낌을 낸다.")]
        public bool mirrorAlternate = true;
        public AudioClip swingSound;
        public AudioClip impactSound;
    }

    [Serializable]
    public sealed class EnemyArt
    {
        public float scale = 1f;
        public Vector2 offset;
        public SpriteClip idle = new SpriteClip();
        public SpriteClip move = new SpriteClip();
        [Tooltip("공격 준비(굴쥐 움찔, 멧돼지 앞발 긁기, 궁수 조준). 준비 시간에 맞춰 재생.")]
        public SpriteClip windup = new SpriteClip();
        [Tooltip("공격(물기, 돌진, 머리치기, 쏘기).")]
        public SpriteClip attack = new SpriteClip();
        [Tooltip("피격·넉백·기절 중.")]
        public SpriteClip hit = new SpriteClip();
        [Tooltip("처치. 날아가는 움직임과 회전은 코드가 한다. 한두 장이면 충분.")]
        public SpriteClip death = new SpriteClip();

        public bool HasAny => idle.Has || move.Has || windup.Has || attack.Has || hit.Has || death.Has;
    }

    [Serializable]
    public sealed class EffectArt
    {
        [Tooltip("타격 파편 조각(여러 장이면 무작위).")]
        public Sprite[] sparks = Array.Empty<Sprite>();
        [Tooltip("베인 자국(가로로 긴 줄, 오른쪽 방향).")]
        public Sprite slashMark;

        public bool HasAny => (sparks != null && sparks.Length > 0) || slashMark;
    }

    [Serializable]
    public sealed class SoundArt
    {
        public AudioClip swing;
        public AudioClip hit;
        public AudioClip crit;
        public AudioClip hurt;
        public AudioClip kill;
        public AudioClip dodge;
        public AudioClip boarCharge;
        public AudioClip archerShot;

        public bool HasAny => swing || hit || crit || hurt || kill || dodge || boarCharge || archerShot;
    }
}
