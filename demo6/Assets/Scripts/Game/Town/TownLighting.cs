using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 저녁 빛(기획/마을-의뢰-첫판.md 2-2). Light2D를 직접 만든다(빛 깜빡임을 고치는 중인 던전 조명 코드에 기대지 않음).
    /// 전역 빛: 차가운 푸른 회색(던전 빛 밖 색과 같음), 세기 0.32(던전 0.06), F1 밀대 0.10~0.70.
    /// 따뜻한 점광(기존 등잔빛 색 Palette.TorchLight, 전설 주황은 쓰지 않음): 화덕·주막 창·초소 화로·권양기 등불 줄·귀환 지점 등(TownLayout.Lights) +
    /// 플레이어를 따라가는 약한 빛. 권양기 등불 줄 세기는 0.30 + 0.45 × 켜진 등불/12(SetLitLamps).
    /// 화덕·화로만 일렁인다: 던전 등잔과 같은 크기(세기 ±7%·반경 ±2.5%)의 느린 노이즈라 깜빡이지 않고 너울거린다. 그림자는 끈다. 늘 저녁이다(시간 흐름 없음).
    /// </summary>
    public sealed class TownLighting : MonoBehaviour
    {
        public const float FlickerIntensity = 0.07f;
        public const float FlickerRadius = 0.025f;
        /// <summary>점광 안쪽 반경 비율(가운데가 넓게 밝고 바깥으로 부드럽게 줄어듦).</summary>
        const float InnerRatio = 0.35f;

        sealed class Lamp
        {
            public Light2D Light;
            public float Radius;
            public float Intensity;
            public bool Flicker;
            public float Seed;
        }

        readonly List<Lamp> _lamps = new List<Lamp>(8);
        Light2D _global;
        Light2D _player;
        float _globalIntensity = TownLayout.GlobalLight;

        /// <summary>전역 빛 세기(F1 밀대, 0.10~0.70).</summary>
        public float GlobalIntensity
        {
            get => _globalIntensity;
            set => _globalIntensity = Mathf.Clamp(value, TownLayout.GlobalLightMin, TownLayout.GlobalLightMax);
        }

        /// <summary>지금 권양기 등불 줄 세기를 정한 켜진 등불 수.</summary>
        public int LitLamps { get; private set; } = TownLayout.LampCount;

        void Awake()
        {
            var go = new GameObject("Evening Light");
            go.transform.SetParent(transform, false);
            _global = go.AddComponent<Light2D>();
            _global.lightType = Light2D.LightType.Global;
            _global.color = Palette.AmbientCold;
            _global.intensity = _globalIntensity;
        }

        /// <summary>점광을 만든다(TownLayout.Lights 차례 그대로, SetLitLamps가 같은 차례로 세기를 다시 넣는다).</summary>
        public void Init(int litLamps)
        {
            LitLamps = Mathf.Clamp(litLamps, 0, TownLayout.LampCount);
            var defs = TownLayout.Lights(LitLamps);
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var go = new GameObject("Light " + d.Name);
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector2(d.Pos.X, d.Pos.Y);
                var l = Point(go, d.Radius, d.Intensity);
                _lamps.Add(new Lamp { Light = l, Radius = d.Radius, Intensity = d.Intensity, Flicker = d.Flicker, Seed = 2.3f + i * 7.31f });
            }
        }

        /// <summary>플레이어를 따라가는 약한 빛(반경 3, 세기 0.2).</summary>
        public void AttachPlayer(Transform player)
        {
            if (!player) return;
            var go = new GameObject("Light player");
            go.transform.SetParent(player, false);
            _player = Point(go, TownLayout.PlayerLightRadius, TownLayout.PlayerLightIntensity);
        }

        /// <summary>권양기 등불 줄 세기를 켜진 등불 수로 다시 넣는다.</summary>
        public void SetLitLamps(int litLamps)
        {
            LitLamps = Mathf.Clamp(litLamps, 0, TownLayout.LampCount);
            var defs = TownLayout.Lights(LitLamps);
            for (int i = 0; i < _lamps.Count && i < defs.Length; i++)
            {
                _lamps[i].Intensity = defs[i].Intensity;
                if (_lamps[i].Light && !_lamps[i].Flicker) _lamps[i].Light.intensity = defs[i].Intensity;
            }
        }

        static Light2D Point(GameObject go, float radius, float intensity)
        {
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * InnerRatio;
            l.intensity = intensity;
            l.color = Palette.TorchLight;
            l.falloffIntensity = 0.6f;
            l.shadowsEnabled = false;
            return l;
        }

        /// <summary>−1~1 불 노이즈: 느린 너울 + 조금 빠른 떨림(던전 등잔과 같은 모양).</summary>
        static float FlickerNoise(float t, float seed)
        {
            float slow = Mathf.PerlinNoise(t * 2.4f, seed) - 0.5f;
            float fast = Mathf.PerlinNoise(t * 9.5f, seed + 5.3f) - 0.5f;
            return Mathf.Clamp(slow * 1.5f + fast * 0.7f, -1f, 1f);
        }

        void Update()
        {
            if (_global) _global.intensity = _globalIntensity;
            float t = Time.unscaledTime;
            for (int i = 0; i < _lamps.Count; i++)
            {
                var lamp = _lamps[i];
                if (!lamp.Light) continue;
                if (!lamp.Flicker)
                {
                    lamp.Light.intensity = lamp.Intensity;
                    continue;
                }
                float n = FlickerNoise(t, lamp.Seed);
                float n2 = FlickerNoise(t * 1.3f, lamp.Seed + 11.7f);
                float rk = 1f + FlickerRadius * n;
                float ik = 1f + FlickerIntensity * (0.6f * n + 0.4f * n2);
                lamp.Light.intensity = lamp.Intensity * ik;
                lamp.Light.pointLightOuterRadius = lamp.Radius * rk;
                lamp.Light.pointLightInnerRadius = lamp.Radius * InnerRatio * rk;
            }
        }
    }
}
