using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Reversible art-only pilot. Existing CombatArtSet references remain intact.</summary>
    public sealed class CuteArtProfileV15 : ScriptableObject
    {
        public bool characterEnabled = true;
        public bool floorEnabled = true;
        public Sprite body;
        public Sprite cape;
        public Sprite weaponArm;
        public Sprite shoulder;
        public Sprite toe;
        public bool wholeBodyEnabled = true;
        public Sprite torso;
        public Sprite head;
        public Sprite leftShoulder;
        public Sprite leftHand;
        public Sprite floor;

        static CuteArtProfileV15 _current;
        static bool _searched;
        public static CuteArtProfileV15 Current
        {
            get
            {
                if (!_searched) { _current = Resources.Load<CuteArtProfileV15>("CuteV15/Style"); _searched = true; }
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { _current = null; _searched = false; }

        public static bool ApplyFloor(DungeonCell cell, SpriteRenderer renderer)
        {
            var art = Current;
            if (!art || !art.floorEnabled || !art.floor) return false;
            renderer.sprite = art.floor;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = cell.Bounds.size;
            renderer.transform.localScale = Vector3.one;
            // 4x4 world-unit period divides the existing 28x16 cells exactly.
            // No collider, wall, material, lighting, visibility or gameplay changes.
            return true;
        }
    }
}
