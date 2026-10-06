using System;
using System.IO;
using Demo5.NightRun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class ConfigureArtAndLighting
{
    static Sprite Import(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;
        t.spritePixelsPerUnit=100;t.maxTextureSize=4096;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.filterMode=FilterMode.Bilinear;t.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite WhiteBase()
    {
        const int w=512,h=192;var t=new Texture2D(w,h,TextureFormat.RGBA32,false);var p=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float xx=(x-w/2f)/(w*.48f),yy=(y-h/2f)/(h*.45f);float d=Mathf.Sqrt(xx*xx+yy*yy);
            p[y*w+x]=new Color(1,1,1,Mathf.Clamp01((1-d)*80));
        }
        t.SetPixels(p);t.Apply();File.WriteAllBytes("Assets/Art/Tokens/base-white.png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);return Import("Assets/Art/Tokens/base-white.png");
    }
    static Light2D Light(string name,Transform parent,Light2D.LightType type,Vector2 point,float intensity,Color color,float radius)
    {
        var go=new GameObject(name,typeof(Light2D));go.transform.SetParent(parent,false);go.transform.position=WorldStage.WorldPoint(point.x,point.y);
        var l=go.GetComponent<Light2D>();l.lightType=type;l.intensity=intensity;l.color=color;l.pointLightOuterRadius=radius;l.pointLightInnerRadius=.25f;return l;
    }
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
        Directory.CreateDirectory("Assets/Settings");AssetDatabase.Refresh();
        var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Demo5Renderer2D.asset");
        if(renderer==null){renderer=ScriptableObject.CreateInstance<Renderer2DData>();AssetDatabase.CreateAsset(renderer,"Assets/Settings/Demo5Renderer2D.asset");}
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Demo5URP.asset");
        if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,"Assets/Settings/Demo5URP.asset");}
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        int oldQuality=QualitySettings.GetQualityLevel();
        for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}
        QualitySettings.SetQualityLevel(oldQuality,false);
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/StandeeSpriteLit.mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));AssetDatabase.CreateAsset(material,"Assets/Settings/StandeeSpriteLit.mat");}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(UniversalAdditionalCameraData));camGo.tag="MainCamera";camGo.transform.position=new Vector3(0,0,-10);
        var cam=camGo.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=5.4f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.04f,.05f,.05f);
        var worldGo=new GameObject("World - sprites and real 2D lights",typeof(WorldStage));var world=worldGo.GetComponent<WorldStage>();
        world.Shelter=Import("Assets/Art/Backgrounds/shelter-unlit-v2.png");world.Arcade=Import("Assets/Art/Backgrounds/arcade-unlit-v1.png");
        world.ScoutBody=Import("Assets/Art/Tokens/scout-body.png");world.SentryBody=Import("Assets/Art/Tokens/sentry-body.png");world.InfectedBody=Import("Assets/Art/Tokens/infected-body.png");world.WhiteBase=WhiteBase();world.LitMaterial=material;
        world.SupplyCrate=Import("Assets/Art/Tokens/supply-crate.png");
        world.Ambient=Light("Ambient - Global 2D",worldGo.transform,Light2D.LightType.Global,Vector2.zero,.68f,Color.white,1);
        world.ShelterLamp=Light("Shelter wall lamp - Point 2D",worldGo.transform,Light2D.LightType.Point,new Vector2(1075,235),.85f,new Color(1,.79f,.47f),2.7f);
        world.ExitLamp=Light("Exit lamp - Point 2D",worldGo.transform,Light2D.LightType.Point,new Vector2(1783,362),.7f,new Color(1,.83f,.58f),2.5f);
        var go=new GameObject("NightRun",typeof(NightRunView));var view=go.GetComponent<NightRunView>();view.World=world;
        view.PanelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NightRun/Panel.prefab");view.ButtonPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NightRun/ActionButton.prefab");
        // Store a separately editable, white-base standee prefab with original silhouette.
        var token=new GameObject("TintableStandee");
        var baseGo=new GameObject("WhiteBase",typeof(SpriteRenderer));baseGo.transform.SetParent(token.transform,false);var b=baseGo.GetComponent<SpriteRenderer>();b.sprite=world.WhiteBase;b.sharedMaterial=material;b.color=Color.white;
        baseGo.transform.localScale=new Vector3(.8f/world.WhiteBase.bounds.size.x,.26f/world.WhiteBase.bounds.size.y,1);
        var bodyGo=new GameObject("Silhouette",typeof(SpriteRenderer));bodyGo.transform.SetParent(token.transform,false);var body=bodyGo.GetComponent<SpriteRenderer>();body.sprite=world.ScoutBody;body.sharedMaterial=material;body.sortingOrder=1;bodyGo.transform.localPosition=new Vector3(0,.67f,0);bodyGo.transform.localScale=Vector3.one*(1.7f/world.ScoutBody.bounds.size.y);
        world.StandeePrefab=PrefabUtility.SaveAsPrefabAsset(token,"Assets/Prefabs/NightRun/TintableStandee.prefab");UnityEngine.Object.DestroyImmediate(token);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/NightExpedition.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/NightExpedition.unity",true)};AssetDatabase.SaveAssets();
        return "URP 2D renderer, Sprite-Lit background/standees, Global and 2 Point lights, separate white base configured. No player build.";
    }
}
