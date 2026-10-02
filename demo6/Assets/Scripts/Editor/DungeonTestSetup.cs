using System;
using System.Linq;
using Demo6.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.EditorTools
{
    /// <summary>
    /// 던전 시험 씬(M0b ② 탐험 층) 만들기: Main 씬을 복사해 DungeonRoot를 놓고, 씬에 있던 전역 2D 조명은 지운다(조명은 실행할 때 만든다).
    /// 빛을 무시하는 스프라이트 재질 에셋도 만들어 연결한다. 여러 번 실행해도 같은 결과다. 메뉴: Demo6/던전 시험 씬 만들기.
    /// </summary>
    public static class DungeonTestSetup
    {
        public const string ScenePath = "Assets/Scenes/DungeonTest.unity";
        public const string UnlitMaterialPath = "Assets/Data/Dungeon/SpriteUnlit.mat";
        const string MainScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Demo6/던전 시험 씬 만들기")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Run());
        }

        [MenuItem("Demo6/던전 시험 씬 열기")]
        static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 모드를 끝낸 뒤 실행하세요.");
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!dataPath.EndsWith("/demo6/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("demo6 프로젝트가 아닙니다: " + dataPath);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("저장하지 않은 씬이 있습니다: " + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path);

            Physics2D.gravity = Vector2.zero;
            Layers.ApplyCollisionMatrix();

            if (!AssetDatabase.IsValidFolder("Assets/Data/Dungeon"))
                AssetDatabase.CreateFolder("Assets/Data", "Dungeon");
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (unlit == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("Sprite-Unlit-Default 셰이더를 찾지 못했습니다.");
                unlit = new Material(shader) { name = "SpriteUnlit" };
                AssetDatabase.CreateAsset(unlit, UnlitMaterialPath);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(MainScenePath, ScenePath))
                    throw new InvalidOperationException("Main 씬을 복사하지 못했습니다.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int removed = 0;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            {
                UnityEngine.Object.DestroyImmediate(light.gameObject);
                removed++;
            }
            var root = UnityEngine.Object.FindAnyObjectByType<DungeonRoot>();
            if (root == null) root = new GameObject("DungeonTest").AddComponent<DungeonRoot>();
            var so = new SerializedObject(root);
            var prop = so.FindProperty("unlitMaterial");
            prop.objectReferenceValue = unlit;
            so.ApplyModifiedPropertiesWithoutUndo();
            var cam = Camera.main;
            if (cam)
            {
                cam.orthographic = true;
                cam.orthographicSize = DungeonCamera.Size;
                cam.backgroundColor = Color.black;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            return $"던전 시험 씬 준비 완료: {ScenePath} (전역 2D 조명 {removed}개 지움, 빛 무시 재질 {UnlitMaterialPath}).";
        }
    }
}
