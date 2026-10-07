using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Painted icons share the approved iron frame. Native sources are retained outside Assets.</summary>
    public static class DungeonHudArt
    {
        static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        public static void Draw(Rect rect, string id, Color tint)
        {
            if (!Icons.TryGetValue(id, out var image) || !image)
                Icons[id] = image = Resources.Load<Texture2D>("UI/V10/" + id);
            if (!image) return;
            var color = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit, true);
            GUI.color = color;
        }
    }
}
