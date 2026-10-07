using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 피 발자국(기획/전투-보스-무기-다듬기-1차.md 4-1 #6, 묶음 4): 바닥 피(웅덩이·큰 얼룩)를 밟으면(GoreSystem.BloodAt ≥ 문턱) 그곳을 벗어난 뒤
    /// 약 6걸음 동안 기존 피 얼룩 그림을 크기·회전만 바꿔 왼발·오른발 번갈아 발자국을 찍는다(GoreSystem.Footprint). 뒤로 갈수록 옅어지고,
    /// 찍힌 발자국은 GoreSystem.PrintLifetime 뒤 흐려져 사라진다. 발자국은 바닥 얼룩과 따로 두어 발자국이 다시 발자국을 낳지 않는다.
    /// 시체 밟기 소리는 발소리와 함께 DungeonAudio가 낸다(GoreSystem.CorpseAt). 던전에만 붙는다(DungeonRoot, 전투 시험장에는 안 붙임).
    /// 게임 시간으로 재고 매 프레임 할당이 없다.
    /// </summary>
    public sealed class GoreFootprints : MonoBehaviour
    {
        /// <summary>던전 F1 패널 '피 발자국 끔/켬'. 꾸러미 ①이 ExplorationLog에 토글을 둔다.</summary>
        public static bool Enabled = true;

        /// <summary>도메인 다시 불러오기 꺼짐 대비: 플레이 시작 때 토글을 켬으로 되돌린다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics() => Enabled = true;

        /// <summary>밟았다고 보는 피 진하기(BloodAt, 발 반경 FootRadius). 작은 방울 자국 하나(0.1 안팎)로는 묻지 않고 웅덩이·큰 얼룩·겹친 얼룩이면 묻는다.</summary>
        public const float Threshold = 0.35f;
        public const float FootRadius = 0.22f;
        /// <summary>피를 벗어난 뒤 찍는 걸음 수.</summary>
        public const int Steps = 6;
        /// <summary>발자국 사이 거리(유닛, 왼발·오른발 번갈아). 발소리 한 걸음(1.9)의 절반쯤.</summary>
        public const float Spacing = 0.8f;
        /// <summary>몸 가운데에서 발까지 옆 거리.</summary>
        const float SideOffset = 0.12f;
        /// <summary>첫 발자국·마지막 발자국 진하기(얼룩 색 알파에 곱함).</summary>
        const float FirstAlpha = 0.85f;
        const float LastAlpha = 0.22f;
        /// <summary>피를 살피는 간격(초). 걸음 4.24면 0.42유닛마다.</summary>
        const float CheckInterval = 0.1f;
        /// <summary>한 프레임에 이보다 멀리 움직이면 순간 이동(다시 섬·말뚝 이동)으로 보고 발자국을 끊는다.</summary>
        const float TeleportJump = 1.5f;

        /// <summary>남은 발자국 걸음 / 지금까지 찍은 발자국 / 지금 피 위에 서 있나(eval 확인용).</summary>
        public int StepsLeft => _stepsLeft;
        public int PrintsMade { get; private set; }
        public bool InBlood => _inBlood;

        Vector2 _last;
        Vector2 _dir = Vector2.up;
        bool _tracking;
        float _strideLeft;
        bool _leftFoot;
        int _stepsLeft;
        float _strength;
        bool _inBlood;
        float _checkAt;

        void OnEnable() => DungeonEvents.ExpeditionRestarted += OnExpeditionRestarted;

        void OnDisable() => DungeonEvents.ExpeditionRestarted -= OnExpeditionRestarted;

        void OnExpeditionRestarted()
        {
            _stepsLeft = 0;
            _tracking = false;
        }

        /// <summary>이번 프레임 이동이 끝난 뒤 위치로 잰다(DungeonAudio 발소리와 같은 때).</summary>
        void LateUpdate()
        {
            var p = PlayerController.Instance;
            if (!Enabled || !p)
            {
                _stepsLeft = 0;
                _inBlood = false;
                _tracking = false;
                return;
            }
            Vector2 pos = p.Position;
            if (!_tracking)
            {
                _last = pos;
                _tracking = true;
                _strideLeft = Spacing * 0.5f;
                return;
            }
            Vector2 delta = pos - _last;
            float moved = delta.magnitude;
            _last = pos;
            if (moved > TeleportJump || p.IsDown)
            {
                _stepsLeft = 0;
                _strideLeft = Spacing * 0.5f;
                return;
            }
            if (moved > 0.0001f) _dir = delta / moved;

            float now = Time.time;
            if (now >= _checkAt)
            {
                _checkAt = now + CheckInterval;
                float blood = GoreSystem.BloodAt(pos, FootRadius);
                _inBlood = blood >= Threshold;
                if (_inBlood)
                {
                    _stepsLeft = Steps;
                    _strength = Mathf.Clamp01(blood);
                }
            }
            if (_stepsLeft <= 0 || moved <= 0f) return;

            _strideLeft -= moved;
            if (_strideLeft > 0f) return;
            _strideLeft += Spacing;
            // 한 프레임이 길어도 발자국은 한 번만.
            if (_strideLeft < Spacing * 0.5f) _strideLeft = Spacing * 0.5f;
            _leftFoot = !_leftFoot;
            // 피 위에서는 찍지 않는다(웅덩이를 벗어난 뒤 6걸음).
            if (_inBlood) return;

            float k = (Steps - _stepsLeft) / (float)Mathf.Max(1, Steps - 1);
            float alpha = Mathf.Lerp(FirstAlpha, LastAlpha, k) * Mathf.Lerp(0.75f, 1f, _strength);
            Vector2 side = new Vector2(-_dir.y, _dir.x) * (_leftFoot ? SideOffset : -SideOffset);
            GoreSystem.Footprint(pos + side, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg, alpha);
            _stepsLeft--;
            PrintsMade++;
        }
    }
}
