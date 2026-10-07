using System;
using System.Linq;
using Demo6.Core.Town;
using Demo6.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.EditorTools
{
    /// <summary>
    /// 마을 씬 만들기(기획/마을-의뢰-첫판.md 9-2 '장면 만들기 메뉴'): DungeonTestSetup 틀 그대로 Main 씬을 복사해 "Town" 물체에 TownRoot를 놓는다.
    /// 장면의 2D 조명은 지운다(전역 빛은 TownLighting이 실행할 때 만듦, 전역 빛이 둘이 되지 않게). 빛 무시 재질과 그림 묶음(CombatArtSet)을 넣고
    /// 카메라를 orthographic·크기 8·배경 #15120F로 맞춘다. 저장한 뒤 빌드 목록에 Town을 0번으로 둔다(이미 있으면 0번으로 옮김, 빌드를 켜면 마을에서 시작).
    /// DungeonTest가 목록에 없으면 경고. 여러 번 실행해도 결과가 같다. 메뉴: Demo6/마을 씬 만들기, Demo6/마을 씬 열기.
    /// AgentScripts/SetupProject.cs가 빌드 목록을 Main 하나로 덮어쓰면 이 메뉴를 다시 돌린다(11장 위험 3).
    /// 여러 세션이 같은 편집기를 쓰므로 AssetDatabase.SaveAssets()(남의 저장 안 한 에셋까지 씀)는 빌드 목록이 실제로 바뀔 때만 부른다.
    /// 다시 돌릴 때(Town이 이미 0번)는 이 메뉴가 만든 재질과 Town 장면만 쓴다.
    /// 메뉴는 만든 Town을 열어 두고, 코드에서 부르는 Run()(eval 등)은 끝에 원래 열려 있던 장면으로 돌아간다(다음 Play가 바뀌지 않게).
    /// </summary>
    public static class TownSetup
    {
        public const string ScenePath = SceneTravel.TownPath;
        const string MainScenePath = "Assets/Scenes/Main.unity";
        /// <summary>전투 시험장·던전과 같은 그림 묶음(CombatTestSetup.ArtSetPath와 같은 경로).</summary>
        const string ArtSetPath = "Assets/Data/Combat/CombatArtSet.asset";

        [MenuItem("Demo6/마을 씬 만들기")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Run(stayInTown: true));
        }

        [MenuItem("Demo6/마을 씬 열기")]
        static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogWarning($"[마을] {ScenePath}가 없다 — 'Demo6/마을 씬 만들기'를 먼저 돌린다");
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>코드에서 부를 때(eval 등): 만든 뒤 원래 열려 있던 장면으로 돌아간다.</summary>
        public static string Run() => Run(stayInTown: false);

        /// <summary>stayInTown = 끝에 Town을 열어 둠(메뉴). false면 시작 때 열려 있던 장면 구성을 되살린다(이름 없는 새 장면이었으면 Town에 남음).</summary>
        public static string Run(bool stayInTown)
        {
            // 1. 플레이 중이면 멈춤, demo6인지, 저장 안 한 장면이 있으면 멈춤.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 모드를 끝낸 뒤 실행하세요.");
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!dataPath.EndsWith("/demo6/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("demo6 프로젝트가 아닙니다: " + dataPath);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("저장하지 않은 씬이 있습니다: " + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path);
            var before = EditorSceneManager.GetSceneManagerSetup();

            // 2. 2D 중력 0, 충돌 레이어 표.
            Physics2D.gravity = Vector2.zero;
            Layers.ApplyCollisionMatrix();

            // 3. 빛 무시 재질(던전과 같은 것, 없으면 만듦).
            if (!AssetDatabase.IsValidFolder("Assets/Data/Dungeon"))
                AssetDatabase.CreateFolder("Assets/Data", "Dungeon");
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(DungeonTestSetup.UnlitMaterialPath);
            if (unlit == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("Sprite-Unlit-Default 셰이더를 찾지 못했습니다.");
                unlit = new Material(shader) { name = "SpriteUnlit" };
                AssetDatabase.CreateAsset(unlit, DungeonTestSetup.UnlitMaterialPath);
                AssetDatabase.SaveAssetIfDirty(unlit);
            }
            // 그림 묶음은 있는 것만 넣는다(이 메뉴는 그림 묶음 에셋을 만들거나 고치지 않는다).
            var art = AssetDatabase.LoadAssetAtPath<CombatArtSet>(ArtSetPath);
            if (art == null) Debug.LogWarning($"[마을] 그림 묶음({ArtSetPath})이 없어 도형으로 그린다");

            // 4. Town.unity가 없으면 Main.unity를 복사해 연다.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(MainScenePath, ScenePath))
                    throw new InvalidOperationException("Main 씬을 복사하지 못했습니다.");
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Single로 열면 쓰지 않는 에셋이 내려가 앞에서 읽은 참조가 비므로(그림 묶음이 null로 들어감) 연 뒤에 다시 읽는다.
            unlit = AssetDatabase.LoadAssetAtPath<Material>(DungeonTestSetup.UnlitMaterialPath);
            art = AssetDatabase.LoadAssetAtPath<CombatArtSet>(ArtSetPath);

            // 5. 장면의 2D 조명을 지운다.
            int removed = 0;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            {
                UnityEngine.Object.DestroyImmediate(light.gameObject);
                removed++;
            }

            // 6. "Town" 물체에 TownRoot + 재질·그림 묶음.
            var root = UnityEngine.Object.FindAnyObjectByType<TownRoot>(FindObjectsInactive.Include);
            if (root == null) root = new GameObject("Town").AddComponent<TownRoot>();
            root.gameObject.name = "Town";
            var so = new SerializedObject(root);
            so.FindProperty("unlitMaterial").objectReferenceValue = unlit;
            so.FindProperty("projectArt").objectReferenceValue = art;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 7. 카메라 orthographic, 크기 8, 배경 #15120F.
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam)
            {
                cam.orthographic = true;
                cam.orthographicSize = TownLayout.CameraSize;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
                var p = cam.transform.position;
                cam.transform.position = new Vector3(TownLayout.ReturnPoint.X, TownLayout.ReturnPoint.Y, p.z < -0.01f ? p.z : -10f);
            }
            else Debug.LogWarning("[마을] 장면에 카메라가 없다 — Main 씬을 확인");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 8. 빌드 목록 0번에 Town. DungeonTest가 없으면 경고.
            var current = EditorBuildSettings.scenes;
            bool listReady = current.Length > 0 && current[0].path == ScenePath && current[0].enabled && current.Count(s => s.path == ScenePath) == 1;
            var scenes = current.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            bool dungeonListed = scenes.Any(s => s.path == SceneTravel.DungeonPath && s.enabled);
            if (!dungeonListed) Debug.LogWarning($"[마을] {SceneTravel.DungeonPath}가 빌드 목록에 없다 — 'Demo6/던전 시험 씬 만들기'를 돌린다(편집기에서는 경로로 불러온다)");
            // 빌드 목록 대입은 메모리만 바꾸고 파일(ProjectSettings/EditorBuildSettings.asset)은 SaveAssets 때 쓰인다(2026-10-04 확인:
            // SaveAssetIfDirty·ForceReserializeAssets로는 쓰이지 않음). 그래서 목록이 실제로 바뀔 때만 대입하고 SaveAssets를 부른다.
            // 이미 Town이 0번이면 SaveAssets를 부르지 않는다(공유 편집기에서 다른 세션의 저장 안 한 에셋까지 쓰지 않게).
            if (!listReady)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
            }

            // 9. 코드에서 불렀으면 원래 장면 구성으로 돌아간다(경로가 모두 있을 때만).
            string back = "Town 열어 둠";
            if (!stayInTown && before.Length > 0 && before.All(s => !string.IsNullOrEmpty(s.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(before);
                back = "원래 장면으로 돌아감: " + string.Join(", ", before.Select(s => s.path));
            }
            return $"마을 씬 준비 완료: {ScenePath} (빌드 목록 0번{(listReady ? "(그대로)" : "(바꿔 저장)")}, 2D 조명 {removed}개 지움, 그림 묶음 {(art ? "연결" : "없음")}, 던전 목록 {(dungeonListed ? "있음" : "없음")}, {back}).";
        }
    }
}
