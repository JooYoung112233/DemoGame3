using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 시험용 입력. 기획 3-1 키 배치를 코드로 만든다(프로젝트 입력 에셋은 정식 단계에서 고친다).
    /// 스킬·구르기·무기 행동은 0.15초 동안 버퍼에 둔다(게임 시간).
    /// 세 무기·오른쪽 클릭 1차(기획/세-무기-우클릭-소켓-1차.md 5-1): 오른쪽 클릭 = 무기 행동(WeaponAct), 회오리(Skill1) = E.
    /// 코드 이름 Skill1과 HUD 클릭 비트 2(회오리)는 그대로다.
    /// 숫자키 1~9는 놀이 중 아무 일도 하지 않는다(창 안 고르기는 창이 직접 읽음, 기획/키-배치-1차.md 0장 2). 무기 종류 바꾸기는 F1 시험 패널 단추로 한다(0장 3).
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
        /// <summary>오른쪽 클릭: 무기 행동(막기·기 모으기·난사).</summary>
        InputAction _weaponAct;
        InputAction _dodge;
        InputAction _potion;
        InputAction _crouch;
        InputAction _interact;
        InputAction _equip;
        InputAction _shift;

        /// <summary>던전 창(지도·말뚝 메뉴·사건 고르기)이 열려 있으면 공격 입력을 막는다.</summary>
        public static bool Blocked { get; set; }

        /// <summary>
        /// 마을에서는 싸우지 않는다(기획/마을-의뢰-첫판.md 2-3): 공격·회오리·검풍·구르기·물약·웅크리기·G를 막고 이동·F는 둔다.
        /// 플레이어가 만들어질 때 마을(TownRoot)이 있으면 켜진다. 플레이어에 붙은 값이라 장면을 바꾸면 저절로 사라진다(Blocked는 F까지 막아 마을에 못 씀).
        /// </summary>
        public bool CombatLocked { get; set; }

        public Vector2 Move => _move.ReadValue<Vector2>();
        Vector2 _lastWorldPointer;
        int _hudActions;
        bool _hudAttackThisFrame;
        public Vector2 PointerScreen => DungeonHud.PointerOverHud ? _lastWorldPointer : _point.ReadValue<Vector2>();
        public void QueueHudAction(int index)
        {
            if(index>=0&&index<5&&!Blocked&&!CombatLocked&&!TimeScaleService.Paused)_hudActions |= 1 << index;
        }
        public bool AttackHeld => ((_attack.IsPressed() && !OverPanel) || _hudAttackThisFrame) && !Blocked && !CombatLocked;

        /// <summary>마우스가 시험 패널(전투 시험 HUD, 던전 F1 패널) 위에 있으면 클릭을 공격으로 쓰지 않는다.</summary>
        static bool OverPanel => DungeonHud.PointerOverHud || CombatHud.PointerOverPanel || (ExplorationLog.Instance && ExplorationLog.Instance.PointerOverPanel);
        /// <summary>F: 상호작용(누르기·길게 누르기). 막기가 아닌 무기 행동 중(기 모으기·놓아 베기·난사·끊김 경직·막기 깨짐)에는 막는다(5-3).</summary>
        public bool InteractHeld => _interact.IsPressed() && !Blocked && !TimeScaleService.Paused && !ActBlocksInteract;
        public bool InteractPressed { get; private set; }
        /// <summary>G: 바닥 장비 바로 끼기(Shift+G에서도 true).</summary>
        public bool EquipPressed { get; private set; }
        /// <summary>Shift+G: 반지를 G가 고를 자리의 다른 쪽에 끼기(장비 문서 8-6). 이때 EquipPressed도 true다.</summary>
        public bool EquipOtherPressed { get; private set; }
        public bool PotionPressed { get; private set; }
        /// <summary>C: 웅크리기 켜고 끄기(이번 프레임에 눌렀나, 결정 ③). 던전 창이 열렸거나 멈춘 동안에는 false.</summary>
        public bool CrouchPressed { get; private set; }

        float _skill1At = -999f;
        float _skill2At = -999f;
        float _dodgeAt = -999f;
        float _weaponActAt = -999f;
        /// <summary>패널 밖에서 시작한 오른쪽 누름을 아직 들고 있나(떼면 false). 패널 위에서 시작한 누름은 무기 행동으로 쓰지 않는다.</summary>
        bool _weaponActDown;
        PlayerController _player;

        public bool Skill1Buffered => Time.time - _skill1At <= BufferTime;
        public bool Skill2Buffered => Time.time - _skill2At <= BufferTime;
        public bool DodgeBuffered => Time.time - _dodgeAt <= BufferTime;

        public void ConsumeSkill1() => _skill1At = -999f;
        public void ConsumeSkill2() => _skill2At = -999f;
        public void ConsumeDodge() => _dodgeAt = -999f;

        /// <summary>
        /// 오른쪽 버튼을 누르고 있나(무기 행동: 막기·기 모으기의 '누르는 동안'). 패널 위에서 시작한 누름이 아니고, 던전 창(Blocked)·마을 잠금(CombatLocked)이 아닐 때.
        /// 누른 뒤 커서가 패널 위로 지나가도 떼지 않은 것으로 본다(막기·기 모으기가 커서 위치로 저절로 끊기지 않게).
        /// </summary>
        public bool WeaponActHeld => _weaponActDown && _weaponAct.IsPressed() && !Blocked && !CombatLocked;
        /// <summary>오른쪽 클릭을 눌렀나(눌린 순간부터 0.15초 버퍼, 게임 시간).</summary>
        public bool WeaponActBuffered => Time.time - _weaponActAt <= BufferTime;
        public void ConsumeWeaponAct() => _weaponActAt = -999f;
        /// <summary>이번 프레임에 오른쪽 버튼을 뗐나(멈춤·마을 잠금에서는 false). PlayerController는 '누르고 있지 않음'(WeaponActHeld)으로 놓기를 본다.</summary>
        public bool WeaponActReleased { get; private set; }
        /// <summary>
        /// 패널 밖에서 시작한 오른쪽 누름 번호(누를 때마다 1씩). PlayerController가 끊김·깨짐·행동 없음으로 쓴 누름을 적어 두고,
        /// 같은 누름을 계속 누르고 있어도 다시 시작하지 않는다('새로 눌러야 한다', 5-6).
        /// </summary>
        public int WeaponActPresses { get; private set; }

        /// <summary>막기가 아닌 무기 행동 중이면 F를 막는다(PlayerController.InteractBlockedByAct, 지난 프레임 상태).</summary>
        bool ActBlocksInteract
        {
            get
            {
                if (!_player) _player = GetComponent<PlayerController>();
                return _player && _player.InteractBlockedByAct;
            }
        }

        /// <summary>오른쪽 버튼 입력을 비운다(멈춤·마을 잠금). 누르고 있던 것도 뗀 것으로 본다.</summary>
        void ClearWeaponAct()
        {
            ConsumeWeaponAct();
            _weaponActDown = false;
            WeaponActReleased = false;
        }

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
            // 회오리 베기는 E(5-1). 오른쪽 클릭은 무기 행동에 내준다.
            _skill1 = _map.AddAction("Skill1", InputActionType.Button, "<Keyboard>/e");
            _skill2 = _map.AddAction("Skill2", InputActionType.Button, "<Keyboard>/q");
            _weaponAct = _map.AddAction("WeaponAct", InputActionType.Button, "<Mouse>/rightButton");
            _dodge = _map.AddAction("Dodge", InputActionType.Button, "<Keyboard>/space");
            _potion = _map.AddAction("Potion", InputActionType.Button, "<Keyboard>/r");
            _crouch = _map.AddAction("Crouch", InputActionType.Button, "<Keyboard>/c");
            _interact = _map.AddAction("Interact", InputActionType.Button, "<Keyboard>/f");
            _equip = _map.AddAction("Equip", InputActionType.Button, "<Keyboard>/g");
            _shift = _map.AddAction("Shift", InputActionType.Button, "<Keyboard>/shift");
        }

        void Start()
        {
            // 마을 플레이어는 싸움 입력을 잠근다. TownRoot가 Awake에서 플레이어를 만들어도 Start는 그 뒤라 TownRoot.Instance가 정해져 있다.
            if (TownRoot.Instance) CombatLocked = true;
        }

        void OnEnable() => _map?.Enable();

        void OnDisable() => _map?.Disable();

        void OnDestroy() => _map?.Dispose();

        void Update()
        {
            int requested = _hudActions; _hudActions = 0; _hudAttackThisFrame = false;
            if (!DungeonHud.PointerOverHud) _lastWorldPointer = _point.ReadValue<Vector2>();
            // 오른쪽 버튼을 떼면(멈춘 동안 뗀 것도) 누름이 끝난다. 다시 누를 때까지 WeaponActHeld는 false.
            if (!_weaponAct.IsPressed()) _weaponActDown = false;
            if (TimeScaleService.Paused)
            {
                // 멈춘 동안 누른 입력이 다시 시작할 때 저절로 나가지 않게 한다(멈춘 동안 새로 누른 오른쪽 버튼도 쓰지 않음).
                ConsumeDodge();
                ConsumeSkill1();
                ConsumeSkill2();
                ConsumeWeaponAct();
                WeaponActReleased = false;
                PotionPressed = false;
                CrouchPressed = false;
                InteractPressed = false;
                EquipPressed = false;
                EquipOtherPressed = false;
                return;
            }
            InteractPressed = _interact.WasPressedThisFrame() && !Blocked && !ActBlocksInteract;
            if (CombatLocked)
            {
                // 마을: F만 받고 싸움 입력은 버린다(버퍼도 비워 둠).
                ConsumeDodge();
                ConsumeSkill1();
                ConsumeSkill2();
                ClearWeaponAct();
                PotionPressed = false;
                CrouchPressed = false;
                EquipPressed = false;
                EquipOtherPressed = false;
                return;
            }
            EquipPressed = _equip.WasPressedThisFrame() && !Blocked;
            EquipOtherPressed = EquipPressed && _shift.IsPressed();
            bool overPanel = OverPanel;
            if(!Blocked)
            {
                _hudAttackThisFrame = (requested & 1) != 0;
                if((requested & 2) != 0)_skill1At = Time.time;
                if((requested & 4) != 0)_skill2At = Time.time;
                if((requested & 8) != 0)_dodgeAt = Time.time;
            }
            // 회오리는 이제 키보드(E)라 Q처럼 커서가 패널 위여도 받는다.
            if (_skill1.WasPressedThisFrame()) _skill1At = Time.time;
            if (_skill2.WasPressedThisFrame()) _skill2At = Time.time;
            if (_dodge.WasPressedThisFrame()) _dodgeAt = Time.time;
            // 오른쪽 클릭(무기 행동): 패널 위 클릭·던전 창이 열린 동안의 클릭은 무시한다.
            if (_weaponAct.WasPressedThisFrame() && !overPanel && !Blocked)
            {
                _weaponActAt = Time.time;
                _weaponActDown = true;
                WeaponActPresses++;
            }
            WeaponActReleased = _weaponAct.WasReleasedThisFrame();
            PotionPressed = _potion.WasPressedThisFrame() || (!Blocked && (requested & 16) != 0);
            CrouchPressed = _crouch.WasPressedThisFrame() && !Blocked;
        }
    }
}
