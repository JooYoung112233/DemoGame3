using UnityEngine;

namespace Demo6.Game
{
    public sealed partial class WorldOverlay
    {
        // Reads the existing dodge cooldown; this is readiness, not an invented stamina pool.
        void DrawDodgeGauge(PlayerController player)
        {
            if (player.IsDown || !_cam.orthographic || _cam.orthographicSize <= 0f) return;
            float pxPerUnit = Screen.height / (_cam.orthographicSize * 2f);
            var center = _cam.WorldToScreenPoint(player.Position + Vector2.down * GaugeDrop);
            if (center.z < 0f) return;
            float width = GaugeWidthUnits * pxPerUnit;
            float height = Mathf.Max(GaugeMinHeightPx, GaugeHeightUnits * pxPerUnit);
            var bar = new Rect(center.x - width * .5f, Screen.height - center.y - height * .5f, width, height);
            bar = DodgeEnemyBars(bar, GaugeMaxDodgeUnits * pxPerUnit);
            float ready = player.DodgeCooldownMax > 0f ? 1f - Mathf.Clamp01(player.DodgeCooldown / player.DodgeCooldownMax) : 1f;
            var previous = GUI.color;
            Fill(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), GaugeFrameEdge, 1f);
            Fill(bar, GaugeGroove, 1f);
            if (ready > 0f)
            {
                var fill = new Rect(bar.x, bar.y, bar.width * ready, bar.height);
                Fill(fill, new Color32(77, 149, 176, 255), 1f);
                Fill(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * .3f)), new Color32(172, 211, 217, 150), 1f);
            }
            GUI.color = previous;
        }
    }
}
