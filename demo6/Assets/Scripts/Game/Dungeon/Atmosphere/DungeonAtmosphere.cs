using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 다크 판타지 화면 분위기(기획/다크판타지-분위기-1차.md, 계약서 A '화면 분위기'). 던전에서만 붙는다(전투 시험장에는 없음).
    /// Init에서 ① 후처리(DungeonPostFx: 색 보정·그림자 남청/밝은 곳 호박·비네트·필름 잡티·블룸), ② 플레이어 도형 색(어두운 망토 + 뼈색 방향 표시),
    /// ③ 바닥 장식(DungeonDecor: 뼈·해골·사슬·나무·돌무더기·오래된 핏자국·거미줄), ④ 공기(DungeonAir: 먼지·불티·천장 흙먼지)를 만든다.
    /// 횃불 빛 색·일렁임은 DungeonLighting, 바닥·벽·기둥 그림은 DungeonWorld가 맡는다.
    /// </summary>
    public sealed class DungeonAtmosphere : MonoBehaviour
    {
        public static DungeonAtmosphere Instance { get; private set; }

        public DungeonPostFx PostFx { get; private set; }
        public DungeonAir Air { get; private set; }
        /// <summary>놓은 바닥 장식 스프라이트 수(상한 DungeonDecor.MaxSprites).</summary>
        public int DecorCount { get; private set; }

        /// <summary>시험·비교용: 후처리 켜기/끄기.</summary>
        public bool PostFxOn
        {
            get => PostFx && PostFx.On;
            set
            {
                if (PostFx) PostFx.On = value;
            }
        }

        /// <summary>시험·비교용: 먼지·불티·흙먼지 켜기/끄기.</summary>
        public bool AirOn
        {
            get => Air && Air.On;
            set
            {
                if (Air) Air.On = value;
            }
        }

        bool _ready;

        /// <summary>도메인 다시 불러오기가 꺼져 있어 DungeonRoot.ResetStatics에서 부른다.</summary>
        public static void ResetStatics() => Instance = null;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>플레이어·카메라를 만든 뒤 DungeonRoot가 부른다(벽·물체·자리 표시도 이미 놓여 있다).</summary>
        public void Init(DungeonRoot root)
        {
            if (_ready || !root) return;
            _ready = true;
            Camera cam = null;
            if (root.CameraRig) cam = root.CameraRig.GetComponent<Camera>();
            if (!cam) cam = Camera.main;
            PostFx = DungeonPostFx.Create(transform, cam);
            TintPlayer(root.Player);
            if (root.World != null) DecorCount = DungeonDecor.Build(root.World, transform);
            var air = new GameObject("Dungeon Air");
            air.transform.SetParent(transform, false);
            Air = air.AddComponent<DungeonAir>();
        }

        /// <summary>
        /// 밝은 베이지 플레이어 도형이 어둠 속에서 너무 튀어(진단) 던전에서는 어두운 망토색 몸 + 뼈색 방향 표시로 바꾼다.
        /// 플레이어 생성 코드는 그대로 두고, 피격 번쩍임이 끝나면 돌아갈 색(SpriteFlash.baseColor)도 함께 바꾼다. 그림 모드면 그림 색은 그대로다.
        /// </summary>
        static void TintPlayer(PlayerController player)
        {
            if (!player) return;
            var flash = player.GetComponent<SpriteFlash>();
            if (flash)
            {
                flash.baseColor = Palette.DungeonPlayer;
                if (flash.target && !flash.useShader) flash.target.color = Palette.DungeonPlayer;
            }
            var mark = player.transform.Find("Facing");
            if (mark && mark.TryGetComponent(out SpriteRenderer markSprite)) markSprite.color = Palette.DungeonPlayerMark;
        }
    }
}
