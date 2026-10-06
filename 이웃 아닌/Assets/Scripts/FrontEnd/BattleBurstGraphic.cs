using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Rough ink/paper burst drawn as a jittered star. Vector, so impacts stay crisp at any size.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleBurstGraphic : MaskableGraphic
    {
        [Range(3, 24)] public int Spikes = 9;
        [Range(.1f, .95f)] public float Inner = .42f;
        [Range(0, .6f)] public float Jitter = .25f;
        public int Seed = 7;
        public void Reseed(int seed) { Seed = seed; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; var c = r.center; float radius = Mathf.Min(r.width, r.height) * .5f;
            var random = new System.Random(Seed); int n = Spikes * 2;
            vh.AddVert(c, color, Vector2.zero);
            for (int i = 0; i < n; i++)
            {
                float angle = i * Mathf.PI * 2 / n + (float)(random.NextDouble() - .5) * Jitter * .5f;
                float length = (i % 2 == 0 ? 1 : Inner) * radius * (1 - (float)random.NextDouble() * Jitter);
                vh.AddVert(c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length, color, Vector2.zero);
            }
            for (int i = 0; i < n; i++) vh.AddTriangle(0, i + 1, (i + 1) % n + 1);
        }
    }
}
