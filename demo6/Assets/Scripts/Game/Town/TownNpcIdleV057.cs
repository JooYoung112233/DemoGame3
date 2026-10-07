using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Approved five-frame idle. Fixed ground anchor and body; no synthetic turning or scaling.</summary>
    public sealed class TownNpcIdleV057 : MonoBehaviour
    {
        static readonly int[] Sequence = { 0, 1, 2, 3, 4, 4, 3, 2, 1, 0 };
        Sprite[] _frames;
        SpriteRenderer _renderer;
        float _started;
        public string Role { get; private set; }
        public int CurrentFrame { get; private set; }

        public static TownNpcIdleV057 Attach(Transform parent, string role, Vector2 localPosition)
        {
            var frames = new Sprite[5];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Resources.Load<Sprite>("TownArtV057/npc/" + role + "/idle-" + i.ToString("00"));
                if (!frames[i]) return null;
            }
            var go = new GameObject("Approved idle " + role);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var idle = go.AddComponent<TownNpcIdleV057>();
            idle.Role = role;
            idle._frames = frames;
            idle._started = Time.unscaledTime;
            idle._renderer = go.AddComponent<SpriteRenderer>();
            idle._renderer.sprite = frames[0];
            // Contact shadow belongs to the ground, not the animated head layer.
            WorldProps.Shape(go.transform, "Ground contact shadow", new Vector2(0f, -.12f),
                new Vector2(.9f, .55f), WorldProps.SoftDot, new Color(.07f, .065f, .06f, .35f), -940, false);
            idle.Apply();
            return idle;
        }

        void LateUpdate() => Apply();
        void Apply()
        {
            if (!_renderer || _frames == null) return;
            CurrentFrame = Sequence[Mathf.FloorToInt((Time.unscaledTime - _started) / .18f) % Sequence.Length];
            _renderer.sprite = _frames[CurrentFrame];
            float y = transform.position.y;
            int order = TownApprovedArtV057.SurfaceOrder(y);
            var player = PlayerController.Instance;
            // A flat NPC must not split the player's existing +41..56 body-part band
            // when their ground anchors nearly coincide (for example beside the counter).
            // Keep the player rig and combat rendering unchanged.
            if (player && Mathf.Abs(player.Position.y - y) < .8f &&
                (player.Position - (Vector2)transform.position).sqrMagnitude < 2.25f)
                order = WorldProps.SortY(player.Position.y, player.Position.y >= y ? 57 : 40);
            _renderer.sortingOrder = order;
        }
    }
}
