using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Run after body asset imports/mapping are complete. Edits only pawn styling and sprite import geometry.
/// Does not open scenes, change character positions, regenerate art, modify UI layouts or combat rules.
/// </summary>
public static class ApplySharedPaperStandeeStyle
{
    public const string WorldMaterialPath = "Assets/Settings/PaperStandee.mat";
    public const string PortraitMaterialPath = "Assets/Settings/PaperPortrait.mat";
    static readonly string[] Prefabs = {
        "Assets/Prefabs/Settlement/FieldPawn.prefab",
        "Assets/Prefabs/Settlement/SettlementWorld.prefab"
    };
    static readonly string[] LegacyPaths = {
        "Assets/Art/Settlement/standee-scout.png",
        "Assets/Art/Settlement/standee-medic.png"
    };

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before applying prefab art style.");
        var worldMaterial = EnsureMaterial(WorldMaterialPath, "Demo5/PaperStandee");
        var uiMaterial = EnsureMaterial(PortraitMaterialPath, "Demo5/PaperPortrait");
        var sprites = CollectBodySprites();
        int imports = 0;
        foreach (var sprite in sprites) if (EnsureFullRect(sprite)) imports++;
        var legacy = LegacyPaths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s).ToArray();
        int bodies = 0;
        foreach (var path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var body in root.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Body"))
                {
                    var style = body.GetComponent<PaperStandeeStyle>() ?? body.gameObject.AddComponent<PaperStandeeStyle>();
                    style.Configure(worldMaterial, legacy);
                    EditorUtility.SetDirty(style);
                    EditorUtility.SetDirty(body);
                    bodies++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return $"Shared paper ink/edge applied to {bodies} bodies; {imports} FullRect importer updates. UI ink material ready at {PortraitMaterialPath}. No scenes or gameplay data changed.";
    }

    public static Material EnsureMaterial(string path, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (!shader) throw new InvalidOperationException("Missing shader: " + shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool created = !material;
        if (created) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        if (material.shader != shader) material.shader = shader;
        // Do not reset an art-directed shared material value on every builder run.
        if (created)
        {
            material.SetColor("_BodyColor", new Color(17 / 255f, 20 / 255f, 19 / 255f, 1));
            if (material.HasProperty("_RimColor")) material.SetColor("_RimColor", new Color(166 / 255f, 158 / 255f, 140 / 255f, 1));
            if (material.HasProperty("_RimPixels")) material.SetFloat("_RimPixels", 2.5f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    public static bool EnsureFullRect(Sprite sprite)
    {
        if (!sprite) return false;
        var path = AssetDatabase.GetAssetPath(sprite);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return false;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool changed = settings.spriteMeshType != SpriteMeshType.FullRect || !importer.alphaIsTransparency || importer.mipmapEnabled;
        if (!changed) return false;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return true;
    }

    static List<Sprite> CollectBodySprites()
    {
        var result = new HashSet<Sprite>();
        foreach (var path in Prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Missing prefab: " + path);
            foreach (var body in prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Body")) if (body.sprite) result.Add(body.sprite);
        }
        foreach (var path in LegacyPaths) { var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (sprite) result.Add(sprite); }
        // Include runtime-swapped creature and party bodies, without changing their definitions.
        foreach (var id in AssetDatabase.FindAssets("t:BattleCreatureRoster"))
        {
            var roster = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>(AssetDatabase.GUIDToAssetPath(id));
            foreach (var creature in roster.Creatures) if (creature != null && creature.Body) result.Add(creature.Body);
        }
        foreach (var id in AssetDatabase.FindAssets("t:PartyRoster"))
        {
            var roster = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(id));
            // Serialized inspection keeps this builder compatible before/after the per-person Body field is introduced.
            var serialized = new SerializedObject(roster);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name == "Body" && property.objectReferenceValue is Sprite body)
                    result.Add(body);
        }
        foreach (var path in new[] { "Assets/Art/Tokens/infected-body.png", "Assets/Art/Tokens/scout-body.png", "Assets/Art/Tokens/sentry-body.png" })
        { var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (sprite) result.Add(sprite); }
        return result.ToList();
    }
}
