using UnityEditor;
using UnityEngine;

namespace Live49.EditorTools
{
    // Everything under Assets/_Project is UI/stage art: native-size sprites, no mipmaps, clamped for blur taps.
    public class Live49TextureImportRules : AssetPostprocessor
    {
        const string Root = "Assets/_Project/";

        void OnPreprocessTexture()
        {
            // Only first imports; later hand-tuning in the Inspector is kept.
            if (!assetPath.StartsWith(Root) || !assetImporter.importSettingsMissing) return;
            Apply((TextureImporter)assetImporter, assetPath);
        }

        public static void Apply(TextureImporter importer, string path)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            // Title buttons are drawn at 2x (720x128) and 9-sliced; border from design/ui/ch00-01/manifest.json.
            if (path.Contains("/Title/UI/title-button-"))
                importer.spriteBorder = new Vector4(100f, 24f, 100f, 24f);
        }
    }
}
