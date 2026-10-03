using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 시야(3차 초안 2-4): 빛 밖 밝기 0.08(벽 윤곽만), 갱도지기 등잔 밝은 4.5 / 흐린 7.5, 싸울 때 반경 +1.5.
    /// 벽 등잔은 밝은 5 / 흐린 8이고 켜면 영구. 등잔은 밝은 원(세기 0.55)과 흐린 테두리(세기 0.45) 두 겹으로 그리고, 벽은 그림자를 드리운다.
    /// 다크 판타지(기획/다크판타지-분위기-1차.md '빛'): 빛 밖 전역 빛은 차가운 푸른 회색, 등잔·벽 등잔은 주황 횃불빛
    /// (채도 0.4, 전설 주황보다 낮음)이고 세기 ±7%·반경 ±2.5%가 노이즈로 일렁인다.
    /// 시야 판정(VisionSystem·DarkVision·WorldOverlay)이 쓰는 BrightRadius·DimRadius는 일렁임을 뺀 반경이라 뜻이 그대로다.
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
        /// <summary>횃불 일렁임 세기 폭(계약서 A: 6~8%).</summary>
        public const float FlickerIntensity = 0.07f;
        /// <summary>횃불 일렁임 반경 폭(계약서 A: 2~3%). 빛 그림만 흔들고 시야 판정 반경은 흔들지 않는다.</summary>
        public const float FlickerRadius = 0.025f;
        /// <summary>등잔 두 겹 세기: 가운데 밝기 1.0(0.55 + 0.45), 흐린 테두리 0.45에서 0으로.</summary>
        const float LampBrightIntensity = 0.55f;
        const float LampDimIntensity = 0.45f;

        /// <summary>등잔·벽 등잔·말뚝·처치 몸빛 색: 주황 횃불빛(Palette.TorchLight).</summary>
        public static readonly Color LampColor = Palette.TorchLight;
        /// <summary>빛 밖 전역 빛 색: 차가운 푸른 회색(Palette.AmbientCold). 밝기는 AmbientDark 그대로.</summary>
        public static readonly Color AmbientColor = Palette.AmbientCold;

        public static DungeonLighting Instance { get; private set; }

        /// <summary>시험 패널: 어둠 끄기(밝기 1).</summary>
        public bool DarknessOn { get; set; } = true;
        /// <summary>심지 단계 등으로 늘어나는 반경(M0b는 0).</summary>
        public float RadiusBonus { get; set; }
        /// <summary>등잔 밝은 반경(일렁임을 뺀 값, 싸울 때 +1.5 포함).</summary>
        public float BrightRadius => _bright ? _brightNominal : LampBright;
        /// <summary>등잔 흐린 반경(일렁임을 뺀 값, 싸울 때 +1.5 포함). 시야 판정 '빛 안'의 기준.</summary>
        public float DimRadius => _dim ? _dimNominal : LampDim;
        public bool InCombat { get; private set; }

        /// <summary>일렁이는 벽 등잔 빛 하나. WallLamp가 매 프레임 세기를 다시 쓰므로 그 값 위에 곱한다.</summary>
        sealed class Flame
        {
            public Light2D Light;
            public float Inner;
            public float Outer;
            public float Seed;
            public float LastSource;
            public float LastWritten;
            public bool Written;
        }

        readonly List<Flame> _wallFlames = new List<Flame>(16);
        Light2D _global;
        Light2D _bright;
        Light2D _dim;
        float _combatBlend;
        float _brightNominal = LampBright;
        float _dimNominal = LampDim;
        const float LampSeed = 3.7f;

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
            _bright = Point(player, "Lamp (bright)", LampBright * 0.85f, LampBright, LampBrightIntensity, LampColor, true);
            _dim = Point(player, "Lamp (dim)", LampBright, LampDim, LampDimIntensity, LampColor, true);
            _brightNominal = LampBright;
            _dimNominal = LampDim;
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

        /// <summary>벽 등잔 빛(밝은 5 / 흐린 8, 영구). 두 겹을 같은 노이즈로 일렁이게 등록한다(WallLamp.cs는 그대로).</summary>
        public static void WallLampLight(Transform parent)
        {
            var bright = Point(parent, "Wall lamp (bright)", WallLampBright * 0.8f, WallLampBright, 0.5f, LampColor, true);
            var dim = Point(parent, "Wall lamp (dim)", WallLampBright, WallLampDim, 0.4f, LampColor, true);
            var inst = Instance;
            if (!inst) return;
            float seed = 1.3f + inst._wallFlames.Count * 7.31f;
            inst.AddFlame(bright, seed);
            inst.AddFlame(dim, seed);
        }

        void AddFlame(Light2D light, float seed)
        {
            if (!light) return;
            _wallFlames.Add(new Flame
            {
                Light = light,
                Inner = light.pointLightInnerRadius,
                Outer = light.pointLightOuterRadius,
                Seed = seed,
            });
        }

        /// <summary>−1~1 횃불 노이즈: 느린 너울 + 빠른 떨림.</summary>
        static float FlickerNoise(float t, float seed)
        {
            float slow = Mathf.PerlinNoise(t * 2.4f, seed) - 0.5f;
            float fast = Mathf.PerlinNoise(t * 9.5f, seed + 5.3f) - 0.5f;
            return Mathf.Clamp(slow * 1.5f + fast * 0.7f, -1f, 1f);
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
            _brightNominal = LampBright + bonus;
            _dimNominal = LampDim + bonus;

            // 횃불 일렁임: 반경은 빛 그림에만, 시야 판정은 위 이름 반경을 쓴다.
            float t = Time.unscaledTime;
            float n = FlickerNoise(t, LampSeed);
            float n2 = FlickerNoise(t * 1.3f, LampSeed + 11.7f);
            float rk = 1f + FlickerRadius * n;
            float ik = 1f + FlickerIntensity * (0.6f * n + 0.4f * n2);
            if (_bright)
            {
                _bright.pointLightOuterRadius = _brightNominal * rk;
                _bright.pointLightInnerRadius = _brightNominal * 0.85f * rk;
                _bright.intensity = LampBrightIntensity * ik;
            }
            if (_dim)
            {
                _dim.pointLightInnerRadius = _brightNominal * rk;
                _dim.pointLightOuterRadius = _dimNominal * rk;
                _dim.intensity = LampDimIntensity * ik;
            }
        }

        /// <summary>
        /// 벽 등잔 일렁임. WallLamp.Update가 이번 프레임에 세기(바탕 × 판자벽 바람 떨림)를 다시 썼으면 그 위에 곱하고,
        /// 다시 쓰지 않았으면(멈춘 등) 지난 바탕값에 곱해 곱이 쌓이지 않게 한다. 반경은 만들 때 반경 기준으로 흔든다.
        /// </summary>
        void LateUpdate()
        {
            if (_wallFlames.Count == 0) return;
            float t = Time.unscaledTime;
            for (int i = _wallFlames.Count - 1; i >= 0; i--)
            {
                var f = _wallFlames[i];
                var l = f.Light;
                if (!l)
                {
                    _wallFlames.RemoveAt(i);
                    continue;
                }
                float n = FlickerNoise(t, f.Seed);
                float n2 = FlickerNoise(t * 1.3f, f.Seed + 11.7f);
                float rk = 1f + FlickerRadius * n;
                float ik = 1f + FlickerIntensity * (0.6f * n + 0.4f * n2);
                float current = l.intensity;
                float source = f.Written && current == f.LastWritten ? f.LastSource : current;
                f.LastSource = source;
                f.LastWritten = source * ik;
                f.Written = true;
                l.intensity = f.LastWritten;
                l.pointLightInnerRadius = f.Inner * rk;
                l.pointLightOuterRadius = f.Outer * rk;
            }
        }
    }
}
