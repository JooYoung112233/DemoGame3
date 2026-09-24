using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    /// <summary>Native UI ink treatment. Only attach to the portrait image, never its paper card.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class PaperPortraitStyle : MonoBehaviour
    {
        public Material StyleMaterial;
        Image portrait;
        Material originalMaterial, appliedMaterial;
        bool capturedOriginal;

        public void Configure(Material sharedMaterial) { StyleMaterial = sharedMaterial; ApplyNow(); }
        void OnEnable() => ApplyNow();
        void OnValidate() { if (isActiveAndEnabled) ApplyNow(); }
        void LateUpdate()
        {
            if (!portrait) portrait = GetComponent<Image>();
            if (portrait && StyleMaterial && (appliedMaterial != StyleMaterial || portrait.material != StyleMaterial)) ApplyNow();
        }
        public void ApplyNow()
        {
            if (!portrait) portrait = GetComponent<Image>();
            if (!portrait || !StyleMaterial) return;
            if (!capturedOriginal) { originalMaterial = portrait.material; capturedOriginal = true; }
            portrait.material = StyleMaterial;
            appliedMaterial = StyleMaterial;
        }
        void OnDisable()
        {
            if (portrait && capturedOriginal && portrait.material == appliedMaterial) portrait.material = originalMaterial;
            capturedOriginal = false;
            appliedMaterial = null;
        }
    }
}
