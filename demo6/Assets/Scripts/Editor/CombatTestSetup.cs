using System;
using System.Linq;
using Demo6.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Demo6.EditorTools
{
    /// <summary>
    /// 전투 시험 씬 만들기. 레이어 이름, 2D 물리(중력 0, 충돌 표), CombatTest 씬(Main 복사 + CombatTestRoot)을 맞춘다.
    /// 여러 번 실행해도 같은 결과다. 메뉴: Demo6/전투 시험 씬 만들기.
    /// </summary>
    public static class CombatTestSetup
    {
        public const string ScenePath = "Assets/Scenes/CombatTest.unity";
        public const string ArtSetPath = "Assets/Data/Combat/CombatArtSet.asset";
        public const string FlashMaterialPath = "Assets/Data/Combat/SpriteFlash.mat";
        const string MainScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Demo6/전투 시험 씬 만들기")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Run());
        }

        [MenuItem("Demo6/전투 시험 씬 열기")]
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
            // 자동화에서도 부르므로 확인 창을 띄우지 않고, 저장하지 않은 씬이 있으면 멈춘다.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("저장하지 않은 씬이 있습니다: " + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path);

            NameLayers();
            Physics2D.gravity = Vector2.zero;
            Layers.ApplyCollisionMatrix();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(MainScenePath, ScenePath))
                    throw new InvalidOperationException("Main 씬을 복사하지 못했습니다.");
            }

            var artSet = EnsureArtSet();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = UnityEngine.Object.FindAnyObjectByType<CombatTestRoot>();
            if (root == null) root = new GameObject("CombatTest").AddComponent<CombatTestRoot>();
            var so = new SerializedObject(root);
            var artProp = so.FindProperty("projectArt");
            if (artProp.objectReferenceValue == null)
            {
                artProp.objectReferenceValue = artSet;
                so.ApplyModifiedPropertiesWithoutUndo();
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
            return $"전투 시험 씬 준비 완료: {ScenePath} (프로젝트 {dataPath}). 레이어 8~11 이름, 2D 중력 0, 충돌 표 적용.";
        }

        /// <summary>교체 자리 에셋과 그림 번쩍임 재질을 만든다(이미 있으면 그대로).</summary>
        static CombatArtSet EnsureArtSet()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Combat"))
                AssetDatabase.CreateFolder("Assets/Data", "Combat");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("Demo6/SpriteFlash");
                if (shader == null) throw new InvalidOperationException("Demo6/SpriteFlash 셰이더를 찾지 못했습니다.");
                mat = new Material(shader) { name = "SpriteFlash" };
                AssetDatabase.CreateAsset(mat, FlashMaterialPath);
            }
            var set = AssetDatabase.LoadAssetAtPath<CombatArtSet>(ArtSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CombatArtSet>();
                set.flashMaterial = mat;
                // 무기 칸은 WeaponPresets.All 차례(숫자키 차례)대로 만든다. 새 무기가 All에 붙으면 저절로 칸이 생긴다.
                set.weapons = Demo6.Core.Combat.WeaponPresets.All.Select(NewWeaponArt).ToArray();
                AssetDatabase.CreateAsset(set, ArtSetPath);
            }
            else if (set.flashMaterial == null)
            {
                set.flashMaterial = mat;
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>무기마다 콤보 단계 수만큼 빈 칸을 미리 만든다(인스펙터에서 바로 채우게).</summary>
        static WeaponArt NewWeaponArt(Demo6.Core.Combat.WeaponAttackRule weapon)
        {
            var steps = new StepArt[weapon.combo.Length];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = new StepArt { mirrorAlternate = weapon.combo[i].shape == Demo6.Core.Combat.ComboShape.Arc };
            return new WeaponArt { weaponId = weapon.id, steps = steps };
        }

        static void NameLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var (index, name) in Layers.Named)
            {
                var prop = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(prop.stringValue) && prop.stringValue != name)
                    throw new InvalidOperationException($"레이어 {index}에 이미 '{prop.stringValue}'가 있습니다.");
                prop.stringValue = name;
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
