using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 한손검과 방패 방어 게이지 막대(기획/세-무기-우클릭-소켓-1차.md 2-5·5-9, 0-3의 28). IMGUI 기능만이고 배치 다듬기는 Unity 개발 단계에서 한다.
    /// - 보이는 때: PlayerController.GuardGaugeAlpha(막는 동안·깨진 뒤·차는 동안, 가득 찬 뒤 0.6초 머물고 0.25초에 사라짐). 다른 무기에서는 안 보인다.
    /// - 던전 HUD에서는 행동 버튼 바로 위 가로 막대. 캐릭터 발밑에는 회피 준비 게이지를 그린다(2026-10-06).
    ///   던전 HUD가 없는 전투 시험장에서는 기존 방어 게이지 위치를 보존한다.
    /// - 모양(다크 판타지): 어두운 쇠 테 + 검은 홈 + 청동빛 채움 + 윗면 옅은 광, 양 끝 쇠 징. 깎인 몫은 잿빛 꼬리로 잠깐 남는다.
    ///   0.35 아래(깨짐 직전)는 붉게 맥박친다. 깨진 뒤에는 녹슨 붉은빛으로 차고, 다시 들 수 있는 문턱(절반)에 뼈색 눈금이 선다. 패링 환급은 0.2초 밝게 번쩍인다.
    /// </summary>
    public sealed partial class WorldOverlay
    {
        /// <summary>이번 프레임 적 머리 위 막대 자리(GUI 좌표, DrawBars가 채움).</summary>
        readonly List<Rect> _enemyBarRects = new List<Rect>(16);

        /// <summary>
        /// 몸 중심에서 막대 가운데까지 아래로(유닛). 0.62는 귀여운 원화 몸(아래 끝 약 0.7)의 하반신을 가렸다.
        /// 사용자 원문(2026-10-05) "방패게이지? 는 캐릭터 안가리는선에서 나오게 하면좋을듯"으로 몸 아래 끝 밖으로 내렸다.
        /// </summary>
        const float GaugeDrop = 0.95f;
        const float GaugeWidthUnits = 0.9f;
        const float GaugeHeightUnits = 0.075f;
        const float GaugeMinHeightPx = 4f;
        /// <summary>적 막대를 피해 아래로 비키는 최대 거리(유닛).</summary>
        const float GaugeMaxDodgeUnits = 0.8f;

        static readonly Color GaugeFrame = new Color(0.16f, 0.145f, 0.13f, 0.95f);
        static readonly Color GaugeFrameEdge = new Color(0.36f, 0.33f, 0.29f, 0.9f);
        static readonly Color GaugeRivet = new Color(0.47f, 0.43f, 0.37f, 1f);
        static readonly Color GaugeGroove = new Color(0.03f, 0.02f, 0.015f, 0.9f);
        static readonly Color GaugeBronze = new Color(0.66f, 0.52f, 0.30f, 1f);
        /// <summary>
        /// 깎인 몫: 채움(청동)과 다른 잿빛. 예전 옅은 청동은 검은 홈 위에서 채움과 거의 같아 보여, 내려찍기로 0이 된 순간 막대가 가득 찬 것처럼 읽혔다(2026-10-05 확인).
        /// </summary>
        static readonly Color GaugeTrail = new Color(0.78f, 0.74f, 0.66f, 0.40f);
        static readonly Color GaugeShine = new Color(1f, 0.9f, 0.7f, 0.22f);
        static readonly Color GaugeLow = new Color(0.70f, 0.13f, 0.09f, 1f);
        static readonly Color GaugeBroken = new Color(0.42f, 0.11f, 0.08f, 1f);
        static readonly Color GaugeTick = new Color(0.75f, 0.70f, 0.60f, 1f);
        static readonly Color GaugeFlash = new Color(1f, 0.95f, 0.8f, 0.65f);

        void DrawGuardGauge()
        {
            var p = PlayerController.Instance;
            if (!p) return;
            if (DungeonHud.Instance) DrawDodgeGauge(p);
            float alpha = p.GuardGaugeAlpha;
            if (alpha <= 0.001f) return;

            Rect bar;
            if (DungeonHud.Instance)
            {
                var hud = UiV45.GuardRect;
                float scale = DungeonUi.Scale;
                bar = new Rect(hud.x * scale, hud.y * scale, hud.width * scale, hud.height * scale);
            }
            else
            {
                float pxPerUnit = Screen.height / (_cam.orthographicSize * 2f);
                var center = _cam.WorldToScreenPoint(p.Position + Vector2.down * GaugeDrop);
                if (center.z < 0f) return;
                float w = GaugeWidthUnits * pxPerUnit;
                float height = Mathf.Max(GaugeMinHeightPx, GaugeHeightUnits * pxPerUnit);
                bar = new Rect(center.x - w * 0.5f, Screen.height - center.y - height * 0.5f, w, height);
                bar = DodgeEnemyBars(bar, GaugeMaxDodgeUnits * pxPerUnit);
            }
            float h = bar.height;

            float fraction = Mathf.Clamp01(p.GuardMeterFraction);
            float trail = Mathf.Clamp(p.GuardMeterTrailFraction, fraction, 1f);
            bool recovering = p.GuardRecovering;
            float now = Time.unscaledTime;

            // 쇠 테(바깥 2px) + 윗변 밝은 쇠 + 검은 홈.
            var frame = new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f);
            Fill(frame, GaugeFrame, alpha);
            Fill(new Rect(frame.x, frame.y, frame.width, 1f), GaugeFrameEdge, alpha);
            Fill(bar, GaugeGroove, alpha);

            // 깎인 몫(잿빛 꼬리).
            if (trail > fraction)
                Fill(new Rect(bar.x + bar.width * fraction, bar.y, bar.width * (trail - fraction), bar.height), GaugeTrail, alpha);

            // 채움: 깨진 뒤 = 녹슨 붉은빛, 깨짐 직전(0.35 아래) = 붉은 맥박, 그 밖 = 청동.
            Color fillColor;
            if (recovering) fillColor = GaugeBroken;
            else if (fraction < ShieldRule.LowMeterFraction)
            {
                float pulse = 0.78f + 0.22f * Mathf.Sin(now * 14f);
                fillColor = new Color(GaugeLow.r * pulse, GaugeLow.g * pulse, GaugeLow.b * pulse, 1f);
            }
            else fillColor = GaugeBronze;
            var fill = new Rect(bar.x, bar.y, bar.width * fraction, bar.height);
            if (fill.width > 0f)
            {
                Fill(fill, fillColor, alpha);
                Fill(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f)), GaugeShine, alpha);
            }

            // 깨진 뒤: 다시 들 수 있는 문턱(최대치의 절반)에 뼈색 눈금.
            if (recovering)
            {
                float tx = bar.x + bar.width * ShieldRule.RaiseAfterBreakFraction;
                Fill(new Rect(tx - 1f, bar.y - 2f, 2f, bar.height + 4f), GaugeTick, alpha);
            }

            // 패링 환급 번쩍임.
            float flash = p.GuardParryFlash;
            if (flash > 0f) Fill(frame, GaugeFlash, alpha * flash);

            // 양 끝 쇠 징.
            float rv = Mathf.Max(2f, h * 0.5f);
            Fill(new Rect(frame.x - rv * 0.5f, bar.y + (bar.height - rv) * 0.5f, rv, rv), GaugeRivet, alpha);
            Fill(new Rect(frame.xMax - rv * 0.5f, bar.y + (bar.height - rv) * 0.5f, rv, rv), GaugeRivet, alpha);

            GUI.color = Color.white;
        }

        /// <summary>적 머리 위 막대와 겹치면 그 막대 아래로 비킨다(아래로만, 최대 maxShift픽셀). 다 비키지 못하면 마지막 자리에 그린다(위에 덮어 그림).</summary>
        Rect DodgeEnemyBars(Rect bar, float maxShift)
        {
            float startY = bar.y;
            for (int pass = 0; pass < 6; pass++)
            {
                bool moved = false;
                var frame = new Rect(bar.x - 3f, bar.y - 3f, bar.width + 6f, bar.height + 6f);
                for (int i = 0; i < _enemyBarRects.Count; i++)
                {
                    var r = _enemyBarRects[i];
                    if (!r.Overlaps(frame)) continue;
                    float y = r.yMax + 3f;
                    if (y - startY > maxShift) return bar;
                    bar.y = y;
                    moved = true;
                    break;
                }
                if (!moved) break;
            }
            return bar;
        }

        static void Fill(Rect r, Color c, float alpha)
        {
            c.a *= alpha;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }
    }
}
