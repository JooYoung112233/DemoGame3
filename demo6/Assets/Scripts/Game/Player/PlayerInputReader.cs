using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 시험용 입력. 기획 3-1 키 배치를 코드로 만든다(프로젝트 입력 에셋은 정식 단계에서 고친다).
    /// 스킬·구르기는 0.15초 동안 버퍼에 둔다(게임 시간).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public const float BufferTime = 0.15f;

        InputActionMap _map;
        InputAction _move;
        InputAction _point;
        InputAction _attack;
        InputAction _skill1;
        InputAction _skill2;
        InputAction _dodge;
        InputAction _potion;
        InputAction _weapon1;
        InputAction _weapon2;
        InputAction _weapon3;
        InputAction _interact;
        InputAction _equip;

        /// <summary>던전 창(지도·말뚝 메뉴·사건 고르기)이 열려 있으면 공격 입력을 막는다.</summary>
        public static bool Blocked { get; set; }

        public Vector2 Move => _move.ReadValue<Vector2>();
        public Vector2 PointerScreen => _point.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed() && !OverPanel && !Blocked;

        /// <summary>마우스가 시험 패널(전투 시험 HUD, 던전 F1 패널) 위에 있으면 클릭을 공격으로 쓰지 않는다.</summary>
        static bool OverPanel => CombatHud.PointerOverPanel || (ExplorationLog.Instance && ExplorationLog.Instance.PointerOverPanel);
        /// <summary>F: 상호작용(누르기·길게 누르기).</summary>
        public bool InteractHeld => _interact.IsPressed() && !Blocked && !TimeScaleService.Paused;
        public bool InteractPressed { get; private set; }
        /// <summary>G: 바닥 장비 바로 끼기.</summary>
        public bool EquipPressed { get; private set; }
        public bool PotionPressed { get; private set; }
        /// <summary>이번 프레임에 고른 무기 번호(0~2). 없으면 -1.</summary>
        public int WeaponSelected { get; private set; } = -1;

        float _skill1At = -999f;
        float _skill2At = -999f;
        float _dodgeAt = -999f;

        public bool Skill1Buffered => Time.time - _skill1At <= BufferTime;
        public bool Skill2Buffered => Time.time - _skill2At <= BufferTime;
        public bool DodgeBuffered => Time.time - _dodgeAt <= BufferTime;

        public void ConsumeSkill1() => _skill1At = -999f;
        public void ConsumeSkill2() => _skill2At = -999f;
        public void ConsumeDodge() => _dodgeAt = -999f;

        void Awake()
        {
            _map = new InputActionMap("CombatTest");
            _move = _map.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _point = _map.AddAction("Point", InputActionType.Value, "<Mouse>/position");
            _attack = _map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            _skill1 = _map.AddAction("Skill1", InputActionType.Button, "<Mouse>/rightButton");
            _skill2 = _map.AddAction("Skill2", InputActionType.Button, "<Keyboard>/q");
            _dodge = _map.AddAction("Dodge", InputActionType.Button, "<Keyboard>/space");
            _potion = _map.AddAction("Potion", InputActionType.Button, "<Keyboard>/r");
            _weapon1 = _map.AddAction("Weapon1", InputActionType.Button, "<Keyboard>/1");
            _weapon2 = _map.AddAction("Weapon2", InputActionType.Button, "<Keyboard>/2");
            _weapon3 = _map.AddAction("Weapon3", InputActionType.Button, "<Keyboard>/3");
            _interact = _map.AddAction("Interact", InputActionType.Button, "<Keyboard>/f");
            _equip = _map.AddAction("Equip", InputActionType.Button, "<Keyboard>/g");
        }

        void OnEnable() => _map?.Enable();

        void OnDisable() => _map?.Disable();

        void OnDestroy() => _map?.Dispose();

        void Update()
        {
            if (TimeScaleService.Paused)
            {
                // 멈춘 동안 누른 입력이 다시 시작할 때 저절로 나가지 않게 한다.
                ConsumeDodge();
                ConsumeSkill1();
                ConsumeSkill2();
                PotionPressed = false;
                WeaponSelected = -1;
                InteractPressed = false;
                EquipPressed = false;
                return;
            }
            InteractPressed = _interact.WasPressedThisFrame() && !Blocked;
            EquipPressed = _equip.WasPressedThisFrame() && !Blocked;
            bool overPanel = OverPanel;
            if (_skill1.WasPressedThisFrame() && !overPanel) _skill1At = Time.time;
            if (_skill2.WasPressedThisFrame()) _skill2At = Time.time;
            if (_dodge.WasPressedThisFrame()) _dodgeAt = Time.time;
            PotionPressed = _potion.WasPressedThisFrame();
            WeaponSelected = _weapon1.WasPressedThisFrame() ? 0 : _weapon2.WasPressedThisFrame() ? 1 : _weapon3.WasPressedThisFrame() ? 2 : -1;
        }
    }
}
