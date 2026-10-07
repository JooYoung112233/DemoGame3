using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Time;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 회피 반격 연출(기획/전투-보스-무기-다듬기-1차.md 4-2 [4], CounterRule 값): 창이 열리면 느린 화면 0.12초 × 0.4(SlowPriority.Counter),
    /// 구른 자리 흰 잔상 1장 0.3초, '스릉' 소리, 칼 흰 번쩍 0.15초. 창이 열린 1.0초 동안 칼이 빛을 받지 않는 흰빛(그림은 ⑦이 PlayerController.CounterReady를 봄).
    /// CombatEvents.CounterOpened를 듣는다(한 번 구르기에 한 번). PlayerController.Create가 플레이어에 붙인다.
    /// 칼 흰 번쩍은 그림 쪽(⑦)이 SwordFlash(0~1)를 읽어 칼에 더한다. 잔상은 그 순간 플레이어 몸 그림을 흰 실루엣으로 한 장 찍어 둔다(빛 무시).
    /// </summary>
    public sealed class CounterFx : MonoBehaviour
    {
        const int SampleRate = 44100;
        const float ShingSeconds = 0.38f;
        /// <summary>잔상 처음 진하기(몸 투명도에 곱함).</summary>
        const float GhostAlpha = 0.6f;

        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        static AudioClip _shing;
        static float _flashUntil = -999f;

        /// <summary>창이 열린 수(시험 확인용). 플레이 시작 때 비운다.</summary>
        public static int OpenedCount { get; private set; }

        /// <summary>칼 흰 번쩍 세기(0~1): 창이 열린 순간 1에서 0.15초(실제 시간)에 걸쳐 0. 그림(⑦)이 칼 색에 더한다.</summary>
        public static float SwordFlash => Mathf.Clamp01((_flashUntil - Time.unscaledTime) / CounterRule.FlashSeconds);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (_shing) Destroy(_shing);
            _shing = null;
            _flashUntil = -999f;
            OpenedCount = 0;
        }

        readonly List<(SpriteRenderer sr, float alpha)> _ghosts = new List<(SpriteRenderer, float)>(8);
        GameObject _ghostRoot;
        float _ghostTime;
        MaterialPropertyBlock _block;

        void OnEnable() => CombatEvents.CounterOpened += OnCounterOpened;

        void OnDisable()
        {
            CombatEvents.CounterOpened -= OnCounterOpened;
            ClearGhost();
        }

        void OnCounterOpened()
        {
            OpenedCount++;
            TimeScaleService.SlowMotion(CounterRule.SlowSeconds, CounterRule.SlowScale, SlowPriority.Counter);
            _flashUntil = Time.unscaledTime + CounterRule.FlashSeconds;
            Sfx.Play(SfxKind.Crit, Shing);
            SpawnGhost();
        }

        void Update()
        {
            if (!_ghostRoot) return;
            if (!TimeScaleService.Paused) _ghostTime += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(_ghostTime / CounterRule.AfterimageSeconds);
            if (k <= 0f)
            {
                ClearGhost();
                return;
            }
            foreach (var (sr, alpha) in _ghosts)
                if (sr) sr.color = new Color(1f, 1f, 1f, alpha * k);
        }

        /// <summary>지금 플레이어 몸 그림(켜진 스프라이트 모두)을 흰 실루엣으로 한 장 찍는다. 번쩍임 재질이 없으면 빛 무시 재질로 옅게 찍는다.</summary>
        void SpawnGhost()
        {
            ClearGhost();
            var player = PlayerController.Instance;
            if (!player) return;
            _ghostRoot = new GameObject("회피 반격 잔상");
            _ghostTime = 0f;
            var mat = ArtRuntime.FlashMaterial;
            _block ??= new MaterialPropertyBlock();
            foreach (var src in player.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (!src || !src.enabled || !src.sprite || src.color.a < 0.05f) continue;
                var go = new GameObject(src.name);
                go.transform.SetParent(_ghostRoot.transform, false);
                go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                go.transform.localScale = src.transform.lossyScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = src.sprite;
                sr.flipX = src.flipX;
                sr.flipY = src.flipY;
                sr.drawMode = src.drawMode;
                if (src.drawMode != SpriteDrawMode.Simple) sr.size = src.size;
                sr.sortingLayerID = src.sortingLayerID;
                sr.sortingOrder = src.sortingOrder - 1;
                if (mat)
                {
                    sr.sharedMaterial = mat;
                    _block.Clear();
                    _block.SetFloat(FlashAmountId, 1f);
                    _block.SetColor(FlashColorId, Color.white);
                    sr.SetPropertyBlock(_block);
                }
                else RenderMaterials.MakeUnlit(sr);
                float alpha = src.color.a * GhostAlpha;
                sr.color = new Color(1f, 1f, 1f, alpha);
                _ghosts.Add((sr, alpha));
            }
        }

        void ClearGhost()
        {
            _ghosts.Clear();
            if (_ghostRoot) Destroy(_ghostRoot);
            _ghostRoot = null;
        }

        /// <summary>'스릉': 칼날이 칼집을 긁고 울리는 짧은 쇳소리(합성, 처음 한 번 굽는다).</summary>
        static AudioClip Shing
        {
            get
            {
                if (_shing) return _shing;
                int count = Mathf.CeilToInt(ShingSeconds * SampleRate);
                var data = new float[count];
                var noise = new System.Random(20261004);
                float prev = 0f;
                float fade = ShingSeconds * 0.08f;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)SampleRate;
                    // 긁는 소리: 높은 잡음(앞뒤 차이로 낮은 소리를 뺌), 처음 0.06초.
                    float n = (float)(noise.NextDouble() * 2.0 - 1.0);
                    float hiss = (n - prev) * 0.5f;
                    prev = n;
                    float scrape = hiss * Mathf.Exp(-t * 32f) * 0.45f;
                    // 울림: 음이 살짝 올라가며 사라지는 쇳소리 부분음 셋.
                    float rise = 1f + 0.06f * (1f - Mathf.Exp(-t * 18f));
                    float ring = Mathf.Sin(2f * Mathf.PI * 2350f * rise * t) * 0.5f
                                 + Mathf.Sin(2f * Mathf.PI * 3530f * rise * t + 0.4f) * 0.3f
                                 + Mathf.Sin(2f * Mathf.PI * 5180f * rise * t + 1.1f) * 0.2f;
                    ring *= Mathf.Exp(-t * 9f) * Mathf.Clamp01(t / 0.012f) * 0.55f;
                    float tail = Mathf.Clamp01((ShingSeconds - t) / fade);
                    data[i] = Mathf.Clamp((scrape + ring) * Mathf.Clamp01(t / 0.003f) * tail, -1f, 1f);
                }
                _shing = AudioClip.Create("sfx_counter_shing", count, 1, SampleRate, false);
                _shing.SetData(data, 0);
                return _shing;
            }
        }
    }
}
