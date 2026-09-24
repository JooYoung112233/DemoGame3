using UnityEngine;
using UnityEngine.Sprites;

namespace Demo5.FrontEnd
{
    /// <summary>
    /// Shares ink/paper rendering across the existing field pawn, settlement and combat.
    /// Does not change a sprite, pivot, scale, position, sorting, base, facing or combat flash.
    /// Source sprites must use FullRect, Simple draw mode, and an unrotated texture region.
    /// </summary>
    // Facing and motion use the default order; sample their final sprite/flip this frame.
    [DefaultExecutionOrder(100), ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class PaperStandeeStyle : MonoBehaviour
    {
        public Material StyleMaterial;
        [Tooltip("Only old dark bodies with a baked light paper edge belong here. Never list textured creature artwork.")]
        public Sprite[] LegacyPaperSprites = new Sprite[0];
        [Range(.1f, .9f)] public float LegacyPaperCutoff = .38f;

        SpriteRenderer body;
        Sprite appliedSprite;
        Material appliedMaterial, originalMaterial;
        MaterialPropertyBlock properties;
        bool capturedOriginal;
        bool appliedFlipX, appliedFlipY;
        float appliedCutoff = -1;

        static readonly int BoundsId = Shader.PropertyToID("_SpriteBounds");
        static readonly int UvId = Shader.PropertyToID("_UvRect");
        static readonly int RemovePaperId = Shader.PropertyToID("_RemovePaper");
        static readonly int PaperCutoffId = Shader.PropertyToID("_PaperCutoff");
        static readonly int RendererFlipId = Shader.PropertyToID("_RendererFlip");

        public void Configure(Material sharedMaterial, params Sprite[] legacyPaperSprites)
        {
            StyleMaterial = sharedMaterial;
            LegacyPaperSprites = legacyPaperSprites ?? new Sprite[0];
            ApplyNow();
        }

        void OnEnable() => ApplyNow();
        void OnValidate() { appliedSprite = null; appliedCutoff = -1; }
        void LateUpdate()
        {
            if (!body) body = GetComponent<SpriteRenderer>();
            if (body && (body.sprite != appliedSprite || StyleMaterial != appliedMaterial ||
                         body.sharedMaterial != StyleMaterial || LegacyPaperCutoff != appliedCutoff ||
                         body.flipX != appliedFlipX || body.flipY != appliedFlipY)) ApplyNow();
        }

        public void ApplyNow()
        {
            if (!body) body = GetComponent<SpriteRenderer>();
            if (!body || !body.sprite || !StyleMaterial) return;
            if (!capturedOriginal) { originalMaterial = body.sharedMaterial; capturedOriginal = true; }
            body.sharedMaterial = StyleMaterial;
            var sprite = body.sprite;
            var bounds = sprite.bounds;
            var uv = DataUtility.GetOuterUV(sprite);
            bool removePaper = false;
            if (LegacyPaperSprites != null)
                for (int i = 0; i < LegacyPaperSprites.Length; i++)
                    if (LegacyPaperSprites[i] == sprite) { removePaper = true; break; }
            if (properties == null) properties = new MaterialPropertyBlock();
            // Preserve properties owned by another effect; update only this component's shape properties.
            body.GetPropertyBlock(properties);
            properties.SetVector(BoundsId, new Vector4(bounds.center.x, bounds.center.y, bounds.extents.x, bounds.extents.y));
            properties.SetVector(UvId, uv);
            properties.SetFloat(RemovePaperId, removePaper ? 1 : 0);
            properties.SetFloat(PaperCutoffId, LegacyPaperCutoff);
            properties.SetVector(RendererFlipId, new Vector4(body.flipX ? -1 : 1, body.flipY ? -1 : 1, 0, 0));
            body.SetPropertyBlock(properties);
            appliedSprite = sprite;
            appliedMaterial = StyleMaterial;
            appliedCutoff = LegacyPaperCutoff;
            appliedFlipX = body.flipX;
            appliedFlipY = body.flipY;
        }

        void OnDisable()
        {
            if (body && capturedOriginal && body.sharedMaterial == appliedMaterial) body.sharedMaterial = originalMaterial;
            capturedOriginal = false;
            appliedSprite = null;
            appliedMaterial = null;
        }
    }
}
