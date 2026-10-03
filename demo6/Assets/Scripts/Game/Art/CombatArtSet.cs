using System;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 그림·소리 교체 자리(기획 '타격감 리소스 명세'). 칸이 비어 있으면 도형과 코드 효과음을 쓴다.
    /// 게임 화면은 2026-10-03부터 정수리 시점(topDown 칸)이다. 몸 한 장을 코드가 돌리고 무기는 코드가 휘두른다.
    /// player·weapons[].steps[].body·rat·boar·archer의 프레임 칸은 예전 옆모습(좌우 2방향) 칸으로, 이미 넣은 에셋이 그대로 읽히게 남겨 둔다.
    /// 베기 이펙트(weapons[].steps[].slash)·공용 이펙트·소리는 두 시점이 함께 쓴다(오른쪽을 보게 그리고 조준 방향으로 돌린다).
    /// </summary>
    [CreateAssetMenu(menuName = "Demo6/전투 그림·소리 묶음", fileName = "CombatArtSet")]
    public sealed class CombatArtSet : ScriptableObject
    {
        [Tooltip("그림 피격 번쩍임용 재질(Demo6/SpriteFlash). 비우면 실행 중 셰이더로 만든다.")]
        public Material flashMaterial;
        [Tooltip("정수리 시점 그림(지금 게임 화면). 칸이 비어 있으면 코드로 그린 임시 그림을 쓴다. PPU 256, 오른쪽(+x)이 앞.")]
        public TopDownArt topDown = new TopDownArt();
        [Tooltip("옆모습(좌우 2방향) 검사 프레임. 2026-10-03 정수리 시점 채택 전 칸이다(정수리 시점을 끄면 쓴다).")]
        public PlayerArt player = new PlayerArt();
        [Tooltip("무기별 콤보 단계: 베기 이펙트·소리는 두 시점 공용, 몸(body) 프레임은 옆모습 전용.")]
        public WeaponArt[] weapons = Array.Empty<WeaponArt>();
        [Tooltip("옆모습 굴쥐 프레임(정수리 시점 전 칸).")]
        public EnemyArt rat = new EnemyArt();
        [Tooltip("옆모습 뿔멧돼지 프레임(정수리 시점 전 칸).")]
        public EnemyArt boar = new EnemyArt();
        [Tooltip("옆모습 가시 궁수 프레임(정수리 시점 전 칸).")]
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
            (topDown != null && topDown.HasAny) ||
            player.HasAny || rat.HasAny || boar.HasAny || archer.HasAny || effects.HasAny || sounds.HasAny ||
            (weapons != null && Array.Exists(weapons, w => w != null && w.HasAny));
    }

    // ───────────────────────── 정수리 시점 칸(2026-10-03) ─────────────────────────

    /// <summary>
    /// 정수리 시점 그림 칸(기획 '타격감 리소스 명세' 2026-10-03판). 모두 위에서 곧게 내려다본 한 장이고 오른쪽(+x)이 앞(얼굴·주둥이·칼끝)이다.
    /// 크기는 모두 유닛 기준이다: 가져오기 PPU 256(1유닛 = 256px), 투명 배경. 몸은 코드가 바라보는 쪽으로 돌리고 무기는 코드가 휘두르므로 동작 프레임이 없다.
    /// 칸이 비어 있으면 코드로 그린 임시 그림(TopDownSprites)을 쓴다. '연결된 그림' 모드에서만 읽는다(도형·시험용 그림 모드는 임시 그림).
    /// </summary>
    [Serializable]
    public sealed class TopDownArt
    {
        public TopDownPlayerArt player = new TopDownPlayerArt();
        public TopDownWeaponArt weapons = new TopDownWeaponArt();
        public TopDownCreatureArt rat = new TopDownCreatureArt();
        public TopDownCreatureArt boar = new TopDownCreatureArt();
        public TopDownArcherArt archer = new TopDownArcherArt();
        public TopDownNestArt nest = new TopDownNestArt();
        [Tooltip("쥐 허수아비(지름 0.5, 돌리지 않음). 짚 인형.")]
        public TopDownBodyArt ratDummy = new TopDownBodyArt();
        [Tooltip("나무 허수아비(지름 1.1, 돌리지 않음).")]
        public TopDownBodyArt woodDummy = new TopDownBodyArt();

        public bool HasAny =>
            (player != null && player.HasAny) || (weapons != null && weapons.HasAny) ||
            (rat != null && rat.HasAny) || (boar != null && boar.HasAny) || (archer != null && archer.HasAny) ||
            (nest != null && nest.HasAny) || (ratDummy != null && ratDummy.HasAny) || (woodDummy != null && woodDummy.HasAny);
    }

    /// <summary>
    /// 정수리 시점 검사. 몸은 한 장을 돌리고, 장화·소매·빈 주먹은 코드가 몸 둘레에 놓는다(모두 몸 밑에 깔려 몸 밖으로 나온 부분만 보인다).
    /// 피격 번쩍임·젖힘·숨쉬기·구르기 회전·쓰러짐 어둡게는 코드가 한다.
    /// </summary>
    [Serializable]
    public sealed class TopDownPlayerArt
    {
        [Tooltip("몸 한 장: 위에서 본 어깨·망토·정수리(얼굴 쪽 +x). 피벗 = 몸 중심(충돌 원 중심). 어깨 폭 약 0.8~0.9유닛.")]
        public Sprite body;
        [Tooltip("선택: 맞은 동안(0.2초) 바꿔 끼우는 몸. 몸 칸이 있을 때만 쓴다. 비우면 몸 그림(젖힘·번쩍임은 코드).")]
        public Sprite bodyHurt;
        [Tooltip("선택: 쓰러진 동안 몸. 몸 칸이 있을 때만 쓴다. 코드가 90° 돌리고 어둡게 하는 것은 그대로다.")]
        public Sprite bodyDown;
        [Tooltip("오른발 장화 한 짝(발끝 +x, 피벗 가운데, 약 0.17×0.1유닛). 왼발은 코드가 위아래로 뒤집는다.")]
        public Sprite boot;
        [Tooltip("소매 한 토막(어깨 → 손, 길이 방향 +x, 피벗 가운데, 굵기 약 0.085유닛). 코드가 길이만 늘이고 줄인다.")]
        public Sprite sleeve;
        [Tooltip("장검일 때 빈 왼손 주먹(피벗 가운데, 지름 약 0.1유닛).")]
        public Sprite fist;
        [Tooltip("몸 그림 배율(PPU를 잘못 넣었을 때 맞춤용, 기본 1). 장화·소매·주먹·무기에는 곱하지 않는다.")]
        public float scale = 1f;

        public bool HasAny => body || bodyHurt || bodyDown || boot || sleeve || fist;
    }

    /// <summary>정수리 시점 무기 세 자루. 손잡이를 쥔 주먹까지 한 장에 그린다. 휘두르기는 코드(TopDownSwing)가 한다.</summary>
    [Serializable]
    public sealed class TopDownWeaponArt
    {
        [Tooltip("장검(오른 주먹 포함). 피벗 = 오른 주먹 가운데(손잡이를 쥔 점), 칼끝 +x 0.985유닛.")]
        public Sprite longsword;
        [Tooltip("대검(두 주먹 포함: 0과 −0.105유닛). 피벗 = 앞 주먹 가운데, 칼끝 +x 1.405유닛.")]
        public Sprite greatsword;
        [Tooltip("쌍검 한 자루(오른 주먹 포함). 왼손 칼은 코드가 위아래로 뒤집는다. 피벗 = 주먹 가운데, 칼끝 +x 0.62유닛.")]
        public Sprite twinblade;

        /// <summary>무기 id의 그림(없으면 null). 모르는 id는 장검(TopDownWeaponLook.Of와 같은 규칙).</summary>
        public Sprite For(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_greatsword": return greatsword;
                case "wpn_twinblades": return twinblade;
                default: return longsword;
            }
        }

        public bool HasAny => longsword || greatsword || twinblade;
    }

    /// <summary>정수리 시점 적·물체 몸 한 장. 처치되면 이 그림(또는 처치 그림)을 GoreSystem이 복사해 어둡게 시체로 남긴다.</summary>
    [Serializable]
    public class TopDownBodyArt
    {
        [Tooltip("몸 한 장(머리 쪽 +x, 피벗 = 충돌 원 중심). 정예는 코드가 1.35배로 키운다.")]
        public Sprite body;
        [Tooltip("선택: 처치 그림. 몸 칸이 있을 때만, 처치 순간부터 날아가는 동안과 시체에 쓴다. 비우면 몸 그림을 복사해 어둡게.")]
        public Sprite dead;
        [Tooltip("몸 그림 배율(PPU를 잘못 넣었을 때 맞춤용, 기본 1). 발·활 부품에는 곱하지 않는다.")]
        public float scale = 1f;

        public virtual bool HasAny => body || dead;
    }

    /// <summary>걷는 적(굴쥐·멧돼지·궁수): 몸 + 발 하나(코드가 대각선 짝으로 엇갈려 놓는다).</summary>
    [Serializable]
    public class TopDownCreatureArt : TopDownBodyArt
    {
        [Tooltip("선택: 발 하나(발끝 +x, 피벗 가운데, 유닛 크기). 비우면 코드 점. 오른쪽 발로 그리고 왼쪽 발은 코드가 위아래로 뒤집는다.")]
        public Sprite foot;

        public override bool HasAny => base.HasAny || foot;
    }

    /// <summary>가시 궁수: 몸에 활까지 그리고, 시위·당기는 아래팔·당김 손·걸어 둔 화살은 코드가 조준 시간에 맞춰 움직인다.</summary>
    [Serializable]
    public sealed class TopDownArcherArt : TopDownCreatureArt
    {
        [Tooltip("당기는 아래팔 소매(길이 방향 +x, 피벗 가운데). 몸 칸이 있을 때만 쓴다. 코드가 팔꿈치~당김 손 사이로 늘인다.")]
        public Sprite forearm;
        [Tooltip("시위를 당기는 손(피벗 가운데, 지름 약 0.066유닛). 몸 칸이 있을 때만 쓴다.")]
        public Sprite drawHand;
        [Tooltip("시위에 건 화살(피벗 = 오늬 끝, 촉 +x, 길이 약 0.4유닛). 몸 칸이 있을 때만 쓴다.")]
        public Sprite arrow;
        [Tooltip("몸 그림의 활 위쪽 끝(유닛, 몸 피벗 기준). 아래쪽 끝은 y를 뒤집는다. 시위가 여기서 당김 점까지 이어진다.")]
        public Vector2 bowTip = new Vector2(0.18f, 0.24f);
        [Tooltip("몸 그림의 오른 팔꿈치(유닛, 몸 피벗 기준). 아래팔이 여기서 당김 손까지 이어진다.")]
        public Vector2 drawElbow = new Vector2(0.06f, -0.126f);

        public override bool HasAny => base.HasAny || forearm || drawHand || arrow;
    }

    /// <summary>굴쥐 둥지: 돌리지 않는 흙더미. 껍질(코드 고리)이 깨지면 열린 그림으로 바꾼다.</summary>
    [Serializable]
    public sealed class TopDownNestArt : TopDownBodyArt
    {
        [Tooltip("선택: 껍질이 깨진 뒤 둥지(굴이 드러나고 흙이 무너진 모습). 몸 칸이 있을 때만 쓴다. 비우면 몸 그림 그대로.")]
        public Sprite opened;

        public override bool HasAny => base.HasAny || opened;
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
