using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Native rendering properties and prefab wiring checks. Visual proof is captured separately by the art review.
public static class VerifySharedPaperStandeeStyle
{
    public static string Run()
    {
        var checkedItems = new List<string>();
        var worldMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperStandee.mat");
        var uiMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperPortrait.mat");
        Require(worldMaterial && uiMaterial, "Run ApplySharedPaperStandeeStyle.Run first.");
        foreach (var material in new[] { worldMaterial, uiMaterial })
        {
            Require(material.shader && material.shader.isSupported, "Unsupported shader: " + material.name);
            var errors = ShaderUtil.GetShaderMessages(material.shader).Where(m => m.severity.ToString() == "Error").ToArray();
            Require(errors.Length == 0, string.Join("; ", errors.Select(e => e.message)));
        }
        checkedItems.Add("Both shaders compile and are supported");

        var scout = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/standee-scout.png");
        var medic = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/standee-medic.png");
        Require(scout && medic, "Legacy reference bodies are missing.");
        Require(scout.vertices.Length == 4 && medic.vertices.Length == 4, "Standee imports must use FullRect quads.");
        var temp = new GameObject("Temporary paper style validation") { hideFlags = HideFlags.HideAndDontSave };
        var uiTemp = new GameObject("Temporary portrait style validation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var body = temp.AddComponent<SpriteRenderer>();
            body.sprite = scout;
            body.flipX = true;
            body.flipY = true;
            body.color = new Color(.3f, .4f, .6f, .42f);
            body.sortingOrder = 91;
            body.transform.localPosition = new Vector3(2, 3, 0);
            body.transform.localScale = new Vector3(.7f, 1.2f, 1);
            var sourceMaterial = body.sharedMaterial;
            var originalColor = body.color;
            var style = temp.AddComponent<PaperStandeeStyle>();
            style.Configure(worldMaterial, scout);
            Require(body.sprite == scout && body.flipX && body.flipY && body.sortingOrder == 91 && body.color == originalColor, "Style changed facing, sorting, source or fade opacity.");
            Require(body.transform.localPosition == new Vector3(2, 3, 0) && body.transform.localScale == new Vector3(.7f, 1.2f, 1), "Style changed placement.");
            var properties = new MaterialPropertyBlock();
            body.GetPropertyBlock(properties);
            Require(properties.GetFloat("_RemovePaper") == 1, "Legacy paper filter not applied to its allowlist.");
            Require(properties.GetVector("_UvRect") == DataUtility.GetOuterUV(scout), "Incorrect source UV bounds.");
            body.sprite = medic;
            style.ApplyNow();
            body.GetPropertyBlock(properties);
            Require(body.sprite == medic && properties.GetVector("_UvRect") == DataUtility.GetOuterUV(medic), "Sprite replacement did not refresh UVs.");
            Require(properties.GetFloat("_RemovePaper") == 0, "Legacy paper filter leaked to an unlisted sprite.");
            var b = medic.bounds;
            Require(properties.GetVector("_SpriteBounds") == new Vector4(b.center.x, b.center.y, b.extents.x, b.extents.y), "Sprite replacement did not refresh geometry parameters.");
            style.enabled = false;
            Require(body.sharedMaterial == sourceMaterial, "Disabling the style did not restore its previous material.");
            checkedItems.Add("Sprite swapping, alpha/tint preservation, facing, transforms, sorting, legacy allowlist and disable restoration");

            var portrait = uiTemp.GetComponent<Image>();
            portrait.sprite = scout;
            portrait.color = new Color(.5f, .4f, .3f, .55f);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var portraitStyle = uiTemp.AddComponent<PaperPortraitStyle>();
            portraitStyle.Configure(uiMaterial);
            Require(portrait.sprite == scout && portrait.preserveAspect && !portrait.raycastTarget && Mathf.Approximately(portrait.color.a, .55f), "Portrait treatment changed layout, hit testing or opacity.");
            Require(uiMaterial.HasProperty("_Stencil") && uiMaterial.HasProperty("_StencilComp") && uiMaterial.HasProperty("_UseUIAlphaClip"), "Portrait shader is missing UI mask properties.");
            checkedItems.Add("UI portrait leaves existing layout, raycast and alpha intact; stencil/clip support present");
        }
        finally { UnityEngine.Object.DestroyImmediate(temp); UnityEngine.Object.DestroyImmediate(uiTemp); }

        int count = 0;
        foreach (var path in new[] { "Assets/Prefabs/Settlement/FieldPawn.prefab", "Assets/Prefabs/Settlement/SettlementWorld.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab, "Missing prefab " + path);
            foreach (var body in prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Body"))
            {
                var style = body.GetComponent<PaperStandeeStyle>();
                Require(style && style.StyleMaterial == worldMaterial, "Unstyled body in " + path);
                count++;
            }
            Require(prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BaseNeck"), "Existing shared base is missing in " + path);
        }
        checkedItems.Add(count + " body renderers wired; existing common base retained");
        return string.Join("\n", checkedItems);
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
