using UnityEngine;
using UnityEngine.UI;

namespace Live49.UI
{
    // Per-image material for stage art: blur in 1920-logical pixels, optional pixelated face mask, shared glitch.
    [RequireComponent(typeof(Image))]
    public class StageImageFX : MonoBehaviour
    {
        static readonly int BlurId = Shader.PropertyToID("_BlurTexels");
        static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        static readonly int MaskOnId = Shader.PropertyToID("_MaskOn");
        static readonly int CellsId = Shader.PropertyToID("_Cells");
        static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");

        [SerializeField] Material baseMaterial;
        [SerializeField] bool receivesGlitch;
        [SerializeField] Texture faceMask;
        [SerializeField] Vector2 maskCells = new Vector2(76f, 43f);

        Image _image;
        Material _material;
        float _blurLogicalPx;
        public float BlurLogicalPx => _blurLogicalPx;
        public void UseBaseMaterial(Material material) { baseMaterial = material; }

        Material Mat
        {
            get
            {
                if (_material == null)
                {
                    _image = GetComponent<Image>();
                    _material = new Material(baseMaterial) { name = baseMaterial.name + " (Instance)" };
                    _material.SetFloat(GlitchAmountId, receivesGlitch ? 1f : 0f);
                    if (faceMask != null)
                    {
                        _material.SetTexture(MaskTexId, faceMask);
                        _material.SetVector(CellsId, maskCells);
                    }
                    _image.material = _material;
                }
                return _material;
            }
        }

        void Awake() => _ = Mat;

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        public void SetMask(bool on) => Mat.SetFloat(MaskOnId, on ? 1f : 0f);

        public void SetBlur(float logicalPx)
        {
            _blurLogicalPx = logicalPx;
            ApplyBlur();
        }

        // The rect can animate (polaroid zoom), so the texel radius is refreshed every frame while blurred.
        void LateUpdate()
        {
            if (_blurLogicalPx > 0f)
                ApplyBlur();
        }

        void ApplyBlur()
        {
            var mat = Mat;
            var tex = _image.sprite != null ? _image.sprite.texture : null;
            if (tex == null || _blurLogicalPx <= 0f)
            {
                mat.SetFloat(BlurId, 0f);
                return;
            }

            var rt = (RectTransform)transform;
            var root = _image.canvas != null ? _image.canvas.rootCanvas.transform : null;
            float logicalWidth = root != null ? rt.rect.width * rt.lossyScale.x / root.lossyScale.x : rt.rect.width;
            mat.SetFloat(BlurId, logicalWidth > 0f ? _blurLogicalPx * tex.width / logicalWidth : 0f);
        }
    }
}
