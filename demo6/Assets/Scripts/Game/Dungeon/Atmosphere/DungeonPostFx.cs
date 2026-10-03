using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 후처리(기획/다크판타지-분위기-1차.md '색', 계약서 A). 실행 중 전역 Volume과 VolumeProfile 인스턴스를 만들고
    /// 카메라 후처리를 켠다. 전투 시험장(손맛 비교용)에는 붙이지 않는다(DungeonAtmosphere만 만든다).
    /// ① 색 보정: 채도 −30, 대비 +15, 노출 −0.1. 통합 확인(2026-10-03)에서 대비 +17·노출 −0.15·비네트 0.4는 등잔 빛 안 바닥 평균이 후처리 전보다
    ///    25% 어두워 적·피를 읽기 어려워서, 계약서 범위 안 밝은 쪽 끝으로 올렸다.
    /// ② 그림자 남청·밝은 곳 호박(Shadows Midtones Highlights). 이 프로젝트 URP 색 보정은 LDR(선형 32칸 LUT)이라
    ///    아주 어두운 값은 LUT 첫 두 칸 사이에서 이어 붙여진다 — 그래서 그림자 범위를 넉넉히(0~0.09) 잡고 색조는 은은하게 둔다.
    ///    빛 밖이 차갑고 횃불 안이 따뜻한 대비는 주로 빛 색(DungeonLighting)이 만든다.
    /// ③ 빨간 예고·등급색은 그대로 또렷하게(공정 규칙·2차 색 규칙): 색 곡선 Hue vs Sat로 빨강 계열 채도는 전체 −30을 되돌리고,
    ///    Lum vs Sat로 밝게 빛나는 것(빛 무시 예고·빛기둥·횃불)의 채도를 거의 되돌린다. 어두운 흙·돌만 채도가 빠진다.
    /// ④ 비네트 0.36, 옅은 필름 잡티, 블룸(문턱 0.9: 빛 무시로 그린 횃불 불꽃·불티·빛기둥 심이 번진다. 빛을 받는 물체는 등잔 가운데 밝기 1.0에서도 0.8 안팎이라 거의 번지지 않는다).
    /// </summary>
    public sealed class DungeonPostFx : MonoBehaviour
    {
        public const float Saturation = -30f;
        public const float Contrast = 15f;
        public const float Exposure = -0.1f;
        public const float VignetteIntensity = 0.36f;
        public const float VignetteSmoothness = 0.42f;
        public const float GrainIntensity = 0.16f;
        public const float BloomThreshold = 0.9f;
        public const float BloomIntensity = 0.6f;
        public const float BloomScatter = 0.62f;
        /// <summary>Volume 우선순위(다른 전역 Volume이 생겨도 이 값이 이긴다).</summary>
        const float Priority = 10f;

        Volume _volume;
        VolumeProfile _profile;
        TextureCurve _hueVsSat;
        TextureCurve _lumVsSat;
        UniversalAdditionalCameraData _cameraData;
        bool _cameraPostBefore;

        /// <summary>시험·비교용: 끄면 후처리 없이 본다(카메라 후처리 플래그는 켠 채 Volume만 끈다).</summary>
        public bool On
        {
            get => _volume && _volume.enabled;
            set
            {
                if (_volume) _volume.enabled = value;
            }
        }

        /// <summary>던전 분위기가 부른다. cam이 없으면 Volume만 만들고 카메라 플래그는 건드리지 않는다.</summary>
        public static DungeonPostFx Create(Transform parent, Camera cam)
        {
            var go = new GameObject("Dungeon Post FX");
            // 카메라 Volume 레이어 마스크 기본값(Default)과 맞춘다. Wall 레이어(11)에 두면 안 된다.
            go.layer = 0;
            if (parent) go.transform.SetParent(parent, false);
            var fx = go.AddComponent<DungeonPostFx>();
            fx.Build(cam);
            return fx;
        }

        void Build(Camera cam)
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Dungeon Post FX (runtime)";

            var color = _profile.Add<ColorAdjustments>(false);
            color.postExposure.Override(Exposure);
            color.contrast.Override(Contrast);
            color.saturation.Override(Saturation);

            // 그림자: 차가운 남청(파랑 ↑, 빨강 ↓), 밝은 곳: 호박색(빨강 ↑, 파랑 ↓). 등급색 파랑·보라가 크게 틀어지지 않게 은은하게.
            var smh = _profile.Add<ShadowsMidtonesHighlights>(false);
            smh.shadows.Override(new Vector4(0.94f, 0.97f, 1.07f, 0f));
            smh.midtones.Override(new Vector4(1.01f, 1f, 0.98f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1f, 0.92f, 0f));
            smh.shadowsStart.Override(0f);
            smh.shadowsEnd.Override(0.09f);
            smh.highlightsStart.Override(0.14f);
            smh.highlightsEnd.Override(0.7f);

            // 빨강 계열(예고·피)과 밝게 빛나는 것은 채도를 지킨다. 곡선 0.5 = 그대로, 0.714 × 2 × 0.7(채도 −30) ≈ 1.
            var curves = _profile.Add<ColorCurves>(false);
            _hueVsSat = new TextureCurve(new[]
            {
                new Keyframe(0f, 0.714f),
                new Keyframe(0.045f, 0.62f),
                new Keyframe(0.09f, 0.5f),
                new Keyframe(0.9f, 0.5f),
                new Keyframe(0.96f, 0.66f),
            }, 0.5f, true, new Vector2(0f, 1f));
            _lumVsSat = new TextureCurve(new[]
            {
                new Keyframe(0f, 0.5f),
                new Keyframe(0.12f, 0.5f),
                new Keyframe(0.45f, 0.64f),
                new Keyframe(1f, 0.7f),
            }, 0.5f, false, new Vector2(0f, 1f));
            curves.hueVsSat.Override(_hueVsSat);
            curves.lumVsSat.Override(_lumVsSat);

            var vignette = _profile.Add<Vignette>(false);
            vignette.color.Override(Color.black);
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(VignetteSmoothness);
            vignette.rounded.Override(false);

            var grain = _profile.Add<FilmGrain>(false);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(GrainIntensity);
            grain.response.Override(0.8f);

            var bloom = _profile.Add<Bloom>(false);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.tint.Override(new Color(1f, 0.88f, 0.72f));
            bloom.highQualityFiltering.Override(false);

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = Priority;
            _volume.weight = 1f;
            _volume.sharedProfile = _profile;

            if (!cam) return;
            _cameraData = cam.GetUniversalAdditionalCameraData();
            if (!_cameraData) return;
            _cameraPostBefore = _cameraData.renderPostProcessing;
            _cameraData.renderPostProcessing = true;
            // Volume을 Default 레이어에 두었으니 카메라 Volume 마스크에 Default가 빠져 있으면 넣는다.
            _cameraData.volumeLayerMask = _cameraData.volumeLayerMask | 1;
        }

        void OnDestroy()
        {
            if (_cameraData) _cameraData.renderPostProcessing = _cameraPostBefore;
            if (_volume) _volume.sharedProfile = null;
            if (_profile)
            {
                for (int i = 0; i < _profile.components.Count; i++)
                    if (_profile.components[i]) Destroy(_profile.components[i]);
                _profile.components.Clear();
                Destroy(_profile);
            }
            _hueVsSat?.Release();
            _lumVsSat?.Release();
        }
    }
}
