using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 시야(3차 초안 2-4): 빛 밖 밝기 0.08(벽 윤곽만), 갱도지기 등잔 밝은 4.5 / 흐린 7.5, 싸울 때 반경 +1.5.
    /// 벽 등잔은 밝은 5 / 흐린 8이고 켜면 영구. 등잔 빛은 따뜻한 흰색·연노랑(전설 주황보다 채도를 낮춤).
    /// 등잔은 밝은 원(세기 1)과 흐린 테두리(세기 0.45) 두 겹으로 그리고, 벽은 그림자를 드리운다.
    /// </summary>
    public sealed class DungeonLighting : MonoBehaviour
    {
        /// <summary>
        /// 빛 밖 전역 빛 0.08(3차 초안 2-4, 벽 윤곽만). 선형 색 공간이라 밝은 몸 색은 이 밝기에서도 드러나므로
        /// 적은 VisionSystem이 빛 밖에서 숨기고(눈 두 점만), 안 가 본 곳은 기억 안개가 가린다.
        /// </summary>
        public const float AmbientDark = 0.08f;
        public const float LampBright = 4.5f;
        public const float LampDim = 7.5f;
        public const float CombatBonus = 1.5f;
        public const float WallLampBright = 5f;
        public const float WallLampDim = 8f;
        public static readonly Color LampColor = new Color(1f, 0.93f, 0.8f);
        public static readonly Color AmbientColor = new Color(0.7f, 0.75f, 0.9f);

        public static DungeonLighting Instance { get; private set; }

        /// <summary>시험 패널: 어둠 끄기(밝기 1).</summary>
        public bool DarknessOn { get; set; } = true;
        /// <summary>심지 단계 등으로 늘어나는 반경(M0b는 0).</summary>
        public float RadiusBonus { get; set; }
        public float BrightRadius => _bright ? _bright.pointLightOuterRadius : LampBright;
        public float DimRadius => _dim ? _dim.pointLightOuterRadius : LampDim;
        public bool InCombat { get; private set; }

        Light2D _global;
        Light2D _bright;
        Light2D _dim;
        float _combatBlend;

        void Awake()
        {
            Instance = this;
            var go = new GameObject("Ambient Light");
            go.transform.SetParent(transform, false);
            _global = go.AddComponent<Light2D>();
            _global.lightType = Light2D.LightType.Global;
            _global.color = AmbientColor;
            _global.intensity = AmbientDark;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AttachPlayer(Transform player)
        {
            // 두 겹을 더해 가운데 밝기 1.0(0.55 + 0.45), 흐린 테두리 0.45에서 0으로.
            _bright = Point(player, "Lamp (bright)", LampBright * 0.85f, LampBright, 0.55f, LampColor, true);
            _dim = Point(player, "Lamp (dim)", LampBright, LampDim, 0.45f, LampColor, true);
        }

        /// <summary>점 조명 하나(벽 등잔, 처치 날림 몸빛 등). parent가 있으면 따라다닌다.</summary>
        public static Light2D Point(Transform parent, string name, float inner, float outer, float intensity, Color color, bool shadows)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightInnerRadius = inner;
            l.pointLightOuterRadius = outer;
            l.intensity = intensity;
            l.color = color;
            l.falloffIntensity = 0.6f;
            l.shadowsEnabled = shadows;
            l.shadowIntensity = 0.85f;
            return l;
        }

        /// <summary>벽 등잔 빛(밝은 5 / 흐린 8, 영구).</summary>
        public static void WallLampLight(Transform parent)
        {
            Point(parent, "Wall lamp (bright)", WallLampBright * 0.8f, WallLampBright, 0.5f, LampColor, true);
            Point(parent, "Wall lamp (dim)", WallLampBright, WallLampDim, 0.4f, LampColor, true);
        }

        void Update()
        {
            if (_global) _global.intensity = DarknessOn ? AmbientDark : 1f;
            var player = PlayerController.Instance;
            InCombat = false;
            if (player)
            {
                foreach (var e in Enemy.All)
                {
                    if (!e || e.Dead || !e.Aware || e.IsReturning) continue;
                    if ((e.Position - player.Position).sqrMagnitude <= 12f * 12f)
                    {
                        InCombat = true;
                        break;
                    }
                }
            }
            _combatBlend = Mathf.MoveTowards(_combatBlend, InCombat ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            float bonus = RadiusBonus + CombatBonus * _combatBlend;
            if (_bright)
            {
                _bright.pointLightOuterRadius = LampBright + bonus;
                _bright.pointLightInnerRadius = (LampBright + bonus) * 0.85f;
            }
            if (_dim)
            {
                _dim.pointLightInnerRadius = LampBright + bonus;
                _dim.pointLightOuterRadius = LampDim + bonus;
            }
        }
    }
}
