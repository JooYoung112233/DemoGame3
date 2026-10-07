using System;
using System.Linq;
using Demo6.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Demo6.EditorTools
{
    /// <summary>
    /// 바로 가기 시험 메뉴 장면(Assets/Scenes/TestLauncher.unity). Run(): TownSetup 틀 그대로 Main 씬을 복사해(없을 때만) "TestLauncher" 물체에 TestLauncher를 놓고
    /// 카메라를 orthographic·단색 배경으로 맞춰 저장, 빌드 목록에 없을 때만 끝에 더한다(0번 Town은 그대로 — 정식 시작은 바뀌지 않음,
    /// 이미 있으면 목록을 건드리지 않고 SaveAssets도 부르지 않음: 공유 편집기에서 다른 세션의 저장 안 한 에셋까지 쓰지 않게).
    /// 메뉴: Demo6/시험 메뉴 열기(없으면 만들고 연다), Demo6/시험 메뉴로 Play(없으면 만들고, 열고 바로 Play). 여러 번 실행해도 같다.
    /// 코드에서 부르는 Run()(eval 등)은 끝에 원래 열려 있던 장면으로 돌아간다(다음 Play가 바뀌지 않게).
    /// </summary>
    public static class TestLauncherSetup
    {
        public const string ScenePath = TestLaunchSession.LauncherPath;
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string ObjectName = "TestLauncher";

        static bool SceneExists => AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;

        [MenuItem("Demo6/시험 메뉴 열기")]
        static void OpenMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[시험 메뉴] 플레이 모드를 끝낸 뒤 연다");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            OpenOrCreate();
        }

        [MenuItem("Demo6/시험 메뉴로 Play")]
        static void PlayMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (OpenOrCreate()) EditorApplication.EnterPlaymode();
        }

        /// <summary>메뉴: 없으면 만들고(장면을 열어 둠), 있으면 연다. 열었으면 true.</summary>
        static bool OpenOrCreate()
        {
            try
            {
                if (!SceneExists) Debug.Log(Run(stayInLauncher: true));
                else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[시험 메뉴] 장면을 열지 못했다: " + e.Message);
                return false;
            }
        }

        /// <summary>코드에서 부를 때(eval 등): 만든 뒤 원래 열려 있던 장면으로 돌아간다.</summary>
        public static string Run() => Run(stayInLauncher: false);

        /// <summary>stayInLauncher = 끝에 TestLauncher를 열어 둠(메뉴). false면 시작 때 열려 있던 장면 구성을 되살린다(이름 없는 새 장면이었으면 TestLauncher에 남음).</summary>
        public static string Run(bool stayInLauncher)
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

            // 2. TestLauncher.unity가 없으면 Main.unity를 복사해 연다.
            bool created = false;
            if (!SceneExists)
            {
                if (!AssetDatabase.CopyAsset(MainScenePath, ScenePath))
                    throw new InvalidOperationException("Main 씬을 복사하지 못했습니다.");
                created = true;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3. "TestLauncher" 물체에 TestLauncher(없으면 만듦).
            var launcher = UnityEngine.Object.FindAnyObjectByType<TestLauncher>(FindObjectsInactive.Include);
            if (launcher == null) launcher = new GameObject(ObjectName).AddComponent<TestLauncher>();
            launcher.gameObject.name = ObjectName;
            launcher.gameObject.SetActive(true);

            // 4. 카메라 orthographic, 단색 배경 #15120F(IMGUI 한 화면이라 크기·자리는 그대로).
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam)
            {
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
            }
            else Debug.LogWarning("[시험 메뉴] 장면에 카메라가 없다 — Main 씬을 확인");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 5. 빌드 목록: 없을 때만 끝에 더한다. 0번(Town)과 다른 장면 차례는 절대 옮기지 않는다.
            // 빌드 목록 대입은 메모리만 바꾸고 파일은 SaveAssets 때 쓰이므로(TownSetup 2026-10-04 확인) 목록이 바뀔 때만 대입하고 SaveAssets를 부른다.
            var current = EditorBuildSettings.scenes;
            bool listed = current.Any(s => s.path == ScenePath);
            if (!listed)
            {
                var scenes = current.ToList();
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
            }
            var after = EditorBuildSettings.scenes;
            string first = after.Length > 0 ? after[0].path : "(빈 목록)";
            int index = Array.FindIndex(after, s => s.path == ScenePath);
            if (first != SceneTravel.TownPath)
                Debug.LogWarning($"[시험 메뉴] 빌드 목록 0번이 {first}다 — 정식 시작은 'Demo6/마을 씬 만들기'로 Town을 0번에 둔다(이 메뉴는 옮기지 않음)");

            // 6. 코드에서 불렀으면 원래 장면 구성으로 돌아간다(경로가 모두 있을 때만).
            string back = "TestLauncher 열어 둠";
            if (!stayInLauncher && before.Length > 0 && before.All(s => !string.IsNullOrEmpty(s.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(before);
                back = "원래 장면으로 돌아감: " + string.Join(", ", before.Select(s => s.path));
            }
            return $"시험 메뉴 장면 준비 완료: {ScenePath}{(created ? "(새로 만듦)" : "(그대로)")} · 빌드 목록 {index}번{(listed ? "(그대로)" : "(끝에 더해 저장)")} · 0번 {first} · {back}.";
        }
    }
}
