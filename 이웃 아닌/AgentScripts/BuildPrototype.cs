using System.IO;
using Demo5.NightRun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BuildPrototype
{
    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before building.");
        Directory.CreateDirectory("Assets/Prefabs/NightRun");
        Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
        camera.GetComponent<Camera>().orthographic = true;
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = new Color(.05f, .08f, .1f);
        var root = new GameObject("NightRun");
        var view = root.AddComponent<NightRunView>();
        view.PanelPrefab = Primitive("Panel", false);
        view.ButtonPrefab = Primitive("ActionButton", true);
        view.TilePrefab = Primitive("BoardTile", true);
        view.TokenPrefab = Token();
        view.PropPrefab = Prop();
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/NightRun/Expedition.prefab");
        Object.DestroyImmediate(root);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NightRun/Expedition.prefab"));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/NightExpedition.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/NightExpedition.unity", true) };
        PlayerSettings.productName = "Demo5 - Night Expedition";
        PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        return "Created NightExpedition scene and 6 reusable prefabs.";
    }
    static GameObject Primitive(string name, bool button)
    {
        var go = new GameObject(name, typeof(RectTransform), name == "BoardTile" ? typeof(DiamondImage) : typeof(Image));
        if (button) go.AddComponent<Button>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/NightRun/" + name + ".prefab");
        Object.DestroyImmediate(go); return prefab;
    }
    static GameObject Token()
    {
        var go = new GameObject("SurvivorToken", typeof(RectTransform));
        var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Part(go.transform, "Base", new Vector2(33, -43), new Vector2(64, 18), circle);
        Part(go.transform, "Body", new Vector2(33, -27), new Vector2(27, 33), circle);
        Part(go.transform, "Head", new Vector2(33, -7), new Vector2(22, 22), circle);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/NightRun/SurvivorToken.prefab");
        Object.DestroyImmediate(go); return prefab;
    }
    static GameObject Prop()
    {
        var go = new GameObject("PropToken", typeof(RectTransform));
        Part(go.transform, "Body", new Vector2(33, -25), new Vector2(64, 42), null);
        Part(go.transform, "UpperBand", new Vector2(33, -13), new Vector2(38, 4), null);
        Part(go.transform, "LowerBand", new Vector2(33, -36), new Vector2(38, 4), null);
        go.transform.Find("UpperBand").GetComponent<Image>().color = new Color(.25f, .3f, .29f);
        go.transform.Find("LowerBand").GetComponent<Image>().color = new Color(.25f, .3f, .29f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/NightRun/PropToken.prefab");
        Object.DestroyImmediate(go); return prefab;
    }
    static void Part(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().sprite = sprite; go.GetComponent<Image>().raycastTarget = false;
    }
}
