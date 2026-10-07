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
    /// 정수리 시점 검사(장비 문서 9-1). 몸은 한 장을 돌리고, 투구는 몸 위에 덧그림으로, 소매·주먹·장화는 코드가 몸 둘레에 놓는다
    /// (소매·장화는 몸 밑에 깔려 몸 밖으로 나온 부분만 보이고, 주먹은 무기 위에 덮인다).
    /// 갑옷·투구·장갑·장화는 낀 종류 id(GearBaseTable: arm_leather 등)로 아래 묶음 배열(armors·helms·gloves·boots)에서 고른다.
    /// 묶음이 없거나 그 칸이 비면 예전 칸(body·bodyHurt·bodyDown·boot·sleeve·fist)이 모든 id의 기본값이고, 그것도 비면 무게 색 임시 그림(TopDownSprites)을 쓴다.
    /// 등급은 겉모습에 넣지 않는다(이름 색·빛기둥만). 피격 번쩍임·젖힘·숨쉬기·구르기 회전·쓰러짐 어둡게는 코드가 한다(투구도 몸과 같이 받는다).
    /// </summary>
    [Serializable]
    public sealed class TopDownPlayerArt
    {
        [Tooltip("시각 전용 짧은 망토. +x가 전방, 어깨 피벗을 몸 중심에 맞춘 분리 원본. 장비·능력치와 무관.")]
        public Sprite cape;
        [Tooltip("기본 몸(모든 갑옷 id의 기본값): 위에서 본 어깨·망토, 맨머리까지(투구는 따로 덮음)(얼굴 쪽 +x). 피벗 = 몸 중심(충돌 원 중심). 어깨 폭 약 0.8~0.9유닛.")]
        public Sprite body;
        [Tooltip("선택: 기본 몸의 맞은 동안(0.2초) 그림. 몸 칸이 있을 때만 쓴다. 비우면 몸 그림(젖힘·번쩍임은 코드).")]
        public Sprite bodyHurt;
        [Tooltip("선택: 기본 몸의 쓰러진 동안 그림. 몸 칸이 있을 때만 쓴다. 코드가 90° 돌리고 어둡게 하는 것은 그대로다.")]
        public Sprite bodyDown;
        [Tooltip("기본 장화(모든 장화 id의 기본값): 오른발 한 짝(발끝 +x, 피벗 가운데, 약 0.17×0.1유닛). 왼발은 코드가 위아래로 뒤집는다.")]
        public Sprite boot;
        [Tooltip("기본 소매(모든 갑옷 id의 기본값): 어깨 → 손 한 토막(길이 방향 +x, 피벗 가운데, 굵기 약 0.085유닛). 코드가 길이만 늘이고 줄인다.")]
        public Sprite sleeve;
        [Tooltip("기본 주먹(모든 장갑 id의 쥔 주먹·빈 주먹 기본값, 피벗 가운데, 지름 약 0.1유닛). 무기를 쥔 손과 장검일 때 빈 왼손에 놓는다.")]
        public Sprite fist;
        [Tooltip("기본 몸의 쓰러진 그림에서 머리(투구) 자리(유닛, 몸 피벗 기준, 돌리기 전). 0이면 서 있는 몸과 같은 자리.")]
        public Vector2 downHeadOffset;
        [Tooltip("몸 그림 배율(PPU를 잘못 넣었을 때 맞춤용, 기본 1). 몸·투구 그림에 곱하고 장화·소매·주먹·무기에는 곱하지 않는다.")]
        public float scale = 1f;

        [Tooltip("갑옷 종류(arm_leather·arm_chain·arm_plate)마다 몸 묶음. 비우면 위 기본 몸·소매.")]
        public TopDownArmorArt[] armors = Array.Empty<TopDownArmorArt>();
        [Tooltip("투구 종류(hlm_leather·hlm_chain·hlm_plate)마다 머리 위 덧그림. 비우면 무게 색 임시 그림.")]
        public TopDownHelmArt[] helms = Array.Empty<TopDownHelmArt>();
        [Tooltip("장갑 종류(glv_leather·glv_chain·glv_plate)마다 쥔 주먹·빈 주먹. 비우면 위 기본 주먹.")]
        public TopDownGlovesArt[] gloves = Array.Empty<TopDownGlovesArt>();
        [Tooltip("장화 종류(bts_leather·bts_chain·bts_plate)마다 오른발 장화. 비우면 위 기본 장화.")]
        public TopDownBootsArt[] boots = Array.Empty<TopDownBootsArt>();

        /// <summary>갑옷 id의 묶음(없으면 null).</summary>
        public TopDownArmorArt Armor(string armorId)
        {
            if (armors == null || string.IsNullOrEmpty(armorId)) return null;
            foreach (var a in armors)
                if (a != null && a.armorId == armorId) return a;
            return null;
        }

        public TopDownHelmArt Helm(string helmId)
        {
            if (helms == null || string.IsNullOrEmpty(helmId)) return null;
            foreach (var h in helms)
                if (h != null && h.helmId == helmId) return h;
            return null;
        }

        public TopDownGlovesArt Gloves(string glovesId)
        {
            if (gloves == null || string.IsNullOrEmpty(glovesId)) return null;
            foreach (var g in gloves)
                if (g != null && g.glovesId == glovesId) return g;
            return null;
        }

        public TopDownBootsArt Boots(string bootsId)
        {
            if (boots == null || string.IsNullOrEmpty(bootsId)) return null;
            foreach (var b in boots)
                if (b != null && b.bootsId == bootsId) return b;
            return null;
        }

        /// <summary>
        /// 갑옷 id의 몸 세 장(서 있는·맞은·쓰러진)과 쓰러진 머리 자리. 묶음에 몸이 있으면 그 묶음(맞은·쓰러진 몸이 비면 그 묶음의 몸),
        /// 없으면 기본 칸(body·bodyHurt·bodyDown·downHeadOffset). 둘 다 비면 false(→ 무게 색 임시 몸).
        /// 다른 갑옷 그림이 섞여 보이지 않게, 묶음에 몸이 있으면 기본 칸의 맞은·쓰러진 몸은 쓰지 않는다.
        /// </summary>
        public bool TryBody(string armorId, out Sprite standing, out Sprite hurt, out Sprite down, out Vector2 headWhenDown)
        {
            var a = Armor(armorId);
            if (a != null && a.body)
            {
                standing = a.body;
                hurt = a.bodyHurt ? a.bodyHurt : a.body;
                down = a.bodyDown ? a.bodyDown : a.body;
                headWhenDown = a.downHeadOffset;
                return true;
            }
            if (body)
            {
                standing = body;
                hurt = bodyHurt ? bodyHurt : body;
                down = bodyDown ? bodyDown : body;
                headWhenDown = downHeadOffset;
                return true;
            }
            standing = hurt = down = null;
            headWhenDown = Vector2.zero;
            return false;
        }

        /// <summary>갑옷 id의 소매: 묶음 → 기본 칸. 없으면 null(→ 임시 팔 막대에 무게 색).</summary>
        public Sprite SleeveFor(string armorId)
        {
            var a = Armor(armorId);
            if (a != null && a.sleeve) return a.sleeve;
            return sleeve ? sleeve : null;
        }

        /// <summary>투구 id의 덧그림. 없으면 null(→ 무게 색 임시 투구). 투구는 예전 기본 칸이 없다(예전 몸이 머리까지 그렸음).</summary>
        public Sprite HelmFor(string helmId)
        {
            var h = Helm(helmId);
            return h != null && h.sprite ? h.sprite : null;
        }

        /// <summary>장갑 id의 주먹(closed = 무기를 쥔 주먹, 아니면 빈 주먹): 묶음의 그 칸 → 묶음의 다른 칸 → 기본 주먹. 없으면 null(→ 무게 색 임시 주먹).</summary>
        public Sprite FistFor(string glovesId, bool closed)
        {
            var g = Gloves(glovesId);
            if (g != null)
            {
                Sprite want = closed ? g.fistClosed : g.fistOpen;
                if (want) return want;
                Sprite other = closed ? g.fistOpen : g.fistClosed;
                if (other) return other;
            }
            return fist ? fist : null;
        }

        /// <summary>장화 id의 오른발 장화: 묶음 → 기본 칸. 없으면 null(→ 무게 색 임시 장화).</summary>
        public Sprite BootFor(string bootsId)
        {
            var b = Boots(bootsId);
            if (b != null && b.boot) return b.boot;
            return boot ? boot : null;
        }

        public bool HasAny =>
            body || bodyHurt || bodyDown || boot || sleeve || fist ||
            (armors != null && Array.Exists(armors, a => a != null && a.HasAny)) ||
            (helms != null && Array.Exists(helms, h => h != null && h.sprite)) ||
            (gloves != null && Array.Exists(gloves, g => g != null && (g.fistClosed || g.fistOpen))) ||
            (boots != null && Array.Exists(boots, b => b != null && b.boot));
    }

    /// <summary>
    /// 갑옷 한 종류의 몸 묶음(장비 문서 9-1·9-2): 맨머리 몸·맞은 몸·쓰러진 몸 + 소매. 무게가 실루엣으로 보이게 그린다(가죽 좁은 어깨·끈, 사슬 고리 무늬, 판금 넓은 어깨받이).
    /// 규격은 기본 몸·소매와 같다(PPU 256, 피벗 = 몸 중심, 얼굴 쪽 +x).
    /// </summary>
    [Serializable]
    public sealed class TopDownArmorArt
    {
        [Tooltip("갑옷 종류 id: arm_leather / arm_chain / arm_plate")]
        public string armorId;
        [Tooltip("몸 한 장: 맨머리까지(투구가 위를 덮음). 피벗 = 몸 중심. 기본 몸과 같은 규격.")]
        public Sprite body;
        [Tooltip("선택: 맞은 동안 몸. 비우면 이 묶음의 몸.")]
        public Sprite bodyHurt;
        [Tooltip("선택: 쓰러진 동안 몸. 비우면 이 묶음의 몸(코드가 90° 돌리고 어둡게 함).")]
        public Sprite bodyDown;
        [Tooltip("선택: 소매 한 토막(기본 소매와 같은 규격). 비우면 기본 소매.")]
        public Sprite sleeve;
        [Tooltip("쓰러진 몸 그림에서 머리(투구) 자리(유닛, 몸 피벗 기준, 돌리기 전). 0이면 서 있는 몸과 같은 자리.")]
        public Vector2 downHeadOffset;

        public bool HasAny => body || bodyHurt || bodyDown || sleeve;
    }

    /// <summary>
    /// 투구 한 종류의 머리 위 덧그림(장비 문서 9-2 ①). 머리 원(임시 몸 단위 중심 약 (0.02, 0), 반지름 약 0.18 → 유닛 약 (0.017, 0)·0.155)을 다 덮는다.
    /// 피벗 = 몸 피벗과 같은 자리(그래서 몸 중심에 그대로 겹쳐 놓는다). 코드가 몸 Transform의 자식으로 놓고 몸 + 1 순서로 그린다.
    /// </summary>
    [Serializable]
    public sealed class TopDownHelmArt
    {
        [Tooltip("투구 종류 id: hlm_leather / hlm_chain / hlm_plate")]
        public string helmId;
        [Tooltip("머리 위 덧그림(PPU 256, 피벗 = 몸 피벗 자리, 얼굴 쪽 +x). 몸 그림 배율(scale)을 같이 받는다.")]
        public Sprite sprite;
    }

    /// <summary>장갑 한 종류의 주먹 두 장(장비 문서 9-2 ③). 지름 약 0.1유닛, 피벗 가운데, 손가락 쪽 +x. 무기 위(무기 + 1 순서)에 놓는다.</summary>
    [Serializable]
    public sealed class TopDownGlovesArt
    {
        [Tooltip("장갑 종류 id: glv_leather / glv_chain / glv_plate")]
        public string glovesId;
        [Tooltip("무기 손잡이를 쥔 주먹(손잡이는 무기 그림이 밑에 깜). 피벗 = 쥔 점.")]
        public Sprite fistClosed;
        [Tooltip("아무것도 쥐지 않은 빈 주먹(장검일 때 왼손).")]
        public Sprite fistOpen;
    }

    /// <summary>장화 한 종류(장비 문서 9-2 ⑤): 오른발 한 짝, 약 0.17×0.10유닛, 발끝 +x, 피벗 가운데. 왼발은 코드가 위아래로 뒤집는다.</summary>
    [Serializable]
    public sealed class TopDownBootsArt
    {
        [Tooltip("장화 종류 id: bts_leather / bts_chain / bts_plate")]
        public string bootsId;
        [Tooltip("오른발 장화(발끝 +x, 피벗 가운데).")]
        public Sprite boot;
    }

    /// <summary>
    /// 정수리 시점 무기 아홉 종류(기존 3 + 기획/전투-보스-무기-다듬기-1차.md 2-7의 새 무기 6). 손잡이까지만 그리고 주먹은 그리지 않는다(주먹은 장갑 부품: TopDownPlayerArt.gloves, 장비 문서 9-2 ②).
    /// 코드가 쥔 주먹을 손잡이 위(무기 + 1 순서)에 덮는다. 휘두르기는 코드(TopDownSwing)가 한다.
    /// 새 무기 공통 규격(2-7): 위에서 곧게 본 모습, +x(오른쪽)가 끝, 피벗 = 오른 주먹 가운데(손잡이 쥔 점), PPU 256, 테두리 #0B0A0D 2px,
    /// 쇠 #8F949C·#54575E·#E6E8ED, 손잡이 가죽 #332114. 원본은 512px/유닛 PNG와 실제 레이어 PSD로 따로 보관한다. 칸이 비면 코드 임시 그림(TopDownSprites).
    /// </summary>
    [Serializable]
    public sealed class TopDownWeaponArt
    {
        [Tooltip("장검(주먹 제외, 주먹은 장갑 부품). 피벗 = 오른 주먹 자리(손잡이를 쥔 점), 칼끝 +x 0.985유닛.")]
        public Sprite longsword;
        [Tooltip("대검(주먹 제외, 주먹은 장갑 부품: 코드가 0과 −0.105유닛에 쥔 주먹을 놓음). 피벗 = 앞 주먹 자리, 칼끝 +x 1.405유닛.")]
        public Sprite greatsword;
        [Tooltip("쌍검 한 자루(주먹 제외, 주먹은 장갑 부품). 왼손 칼은 코드가 위아래로 뒤집는다. 피벗 = 주먹 자리, 칼끝 +x 0.62유닛.")]
        public Sprite twinblade;
        [Tooltip("쇠망치(주먹 제외, 코드가 0과 −0.12유닛에 쥔 주먹을 놓음). 피벗 = 오른 주먹 자리, 자루 끝 −0.15 ~ 머리 끝 +x 1.20유닛, 머리 0.90~1.20·반폭 0.17(한쪽 뭉툭·한쪽 쐐기). 게임 368×96 / 피벗 (46, 48), 원본 736×192 이상(내려칠 때 1.5배로 커짐).")]
        public Sprite maul;
        [Tooltip("창(주먹 제외, 코드가 0과 −0.35유닛에 쥔 주먹을 놓음). 피벗 = 오른 주먹 자리, 물미 −0.55 ~ 창끝 +x 1.75유닛, 창날 1.40~1.75·반폭 0.07. 게임 608×64 / 피벗 (149, 32), 원본 1216×128 이상.")]
        public Sprite spear;
        [Tooltip("큰 낫(주먹 제외, 코드가 0과 −0.30유닛에 쥔 주먹을 놓음). 피벗 = 오른 주먹 자리, 자루 −0.30 ~ +x 1.35유닛, 날은 자루 끝에서 −y로 약 0.75 휜다. 반대로 쓸 때 코드가 위아래로 뒤집는다. 게임 448×232 / 피벗 (85, 약 200), 원본 896×464 이상.")]
        public Sprite scythe;
        [Tooltip("도끼(주먹 제외, 한 손). 피벗 = 오른 주먹 자리, 자루 끝 −0.15 ~ +x 약 1.05유닛, 머리는 자루 끝에서 −y로 치우친 비대칭(날 끝 약 −0.33).")]
        public Sprite axe;
        [Tooltip("단검(주먹 제외, 한 손). 피벗 = 오른 주먹 자리, 칼끝 +x 0.43유닛. 게임 144×40 / 피벗 (28, 20).")]
        public Sprite dagger;
        [Tooltip("사슬 철퇴 손잡이(주먹 제외, 한 손, 사슬은 코드 선). 피벗 = 오른 주먹 자리, 손잡이 끝 고리 +x 0.355유닛(사슬이 매달리는 자리).")]
        public Sprite flail;
        [Tooltip("사슬 철퇴 쇠공(피벗 = 공 가운데, 사슬 고리 쪽 −x, 지름 약 0.3유닛 가시 포함). 코드가 사슬 방향으로 돌려 놓는다.")]
        public Sprite flailBall;
        [Tooltip("한손검과 방패의 방패(위에서 본 둥근 앞면, 왼손 부품 ShieldPart). 피벗 = 가운데, 지름 0.56유닛, +x = 방패 바깥면이 보는 쪽(코드가 방패 깊이만큼 x로 납작하게 함). 쇠테 폭 0.03·가운데 돌기 지름 0.12·세로 판자 4장. 원본은 지름 576px 이상(캔버스 1024 권장) PNG와 레이어 PSD(판자·쇠테·돌기·긁힘·그늘).")]
        public Sprite shield;

        /// <summary>무기 id의 그림(없으면 null). 모르는 id는 장검(TopDownWeaponLook.Of와 같은 규칙). 새 무기 칸이 비면 null(→ 그 무기의 임시 그림, 장검 그림을 빌리지 않음).</summary>
        public Sprite For(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_greatsword": return greatsword;
                case "wpn_twinblades": return twinblade;
                case "wpn_maul": return maul;
                case "wpn_spear": return spear;
                case "wpn_scythe": return scythe;
                case "wpn_axe": return axe;
                case "wpn_dagger": return dagger;
                case "wpn_flail": return flail;
                default: return longsword;
            }
        }

        public bool HasAny => longsword || greatsword || twinblade || maul || spear || scythe || axe || dagger || flail || flailBall;
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
        [Tooltip("몸 그림에 발이 이미 포함되어 있으면 켠다. 별도 발 그림·임시 발 점을 숨기며 몸의 간단한 이동·공격·피격 반응은 유지한다.")]
        public bool feetInBody;
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
        [Tooltip("wpn_longsword / wpn_greatsword / wpn_twinblades / wpn_maul / wpn_spear / wpn_scythe / wpn_axe / wpn_dagger / wpn_flail")]
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
        [Tooltip("장검·대검·쌍검의 +x 방향 칼끝 호. 빈 칸은 코드 궤적을 사용한다.")]
        public Sprite arcLong, arcHeavy, arcTwin;
        [Tooltip("중심 피벗의 찌르기, 회전 칼날, +x로 움직이는 검기.")]
        public Sprite thrust, whirl, wave;
        [Tooltip("중심이 투명한 지면 먼지. 보상 발밑에도 재사용한다.")]
        public Sprite impactDust;
        [Tooltip("아래 중앙 피벗의 좁은 보상 빛줄기.")]
        public Sprite rewardBeam;
        [Tooltip("타격 파편 조각(여러 장이면 무작위).")]
        public Sprite[] sparks = Array.Empty<Sprite>();
        [Tooltip("베인 자국(가로로 긴 줄, 오른쪽 방향).")]
        public Sprite slashMark;

        public bool HasAny => (sparks != null && sparks.Length > 0) || slashMark || arcLong || arcHeavy || arcTwin || thrust || whirl || wave || impactDust || rewardBeam;
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
