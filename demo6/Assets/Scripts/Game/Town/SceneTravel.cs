using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demo6.Game
{
    /// <summary>
    /// 장면 오가기(마을 ↔ 던전, 기획/마을-의뢰-첫판.md 1-3·1-4·9-2). 꾸러미(ProfileCarry)와 도착 쪽지(TownTravel)만 들고 장면을 통째로 바꾼다.
    /// 빌드 장면 목록 번호로 불러오고, 목록에 없으면 편집기에서만 경로로 불러온다(던전 루트의 장면 다시 불러오기와 같은 방식).
    /// 목록 밖에서 불러올 때는 경고를 남긴다: AgentScripts/SetupProject.cs가 목록을 덮어쓰면 생기는 일이고 'Demo6/마을 씬 만들기'를 다시 돌리면 복구된다(11장 위험 3).
    /// 빌드(플레이어)에서는 목록에 없는 장면을 부르지 못한다(CanLoad = false → 부르는 쪽이 옛 흐름으로).
    /// 불러오기를 부른 실제 시각을 남겨 새 장면이 불러오기 시간을 잰다(1-7 '불러오기 시간을 로그로 잰다'). 플레이를 새로 시작할 때 비운다.
    /// </summary>
    public static class SceneTravel
    {
        public const string TownPath = "Assets/Scenes/Town.unity";
        public const string DungeonPath = "Assets/Scenes/DungeonTest.unity";

        /// <summary>마지막으로 불러오기를 부른 실제 시각(Time.realtimeSinceStartup). 없으면 -1.</summary>
        public static float RequestedRealtime { get; private set; } = -1f;
        /// <summary>마지막으로 부른 장면 경로(없으면 null).</summary>
        public static string RequestedPath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            RequestedRealtime = -1f;
            RequestedPath = null;
        }

        /// <summary>빌드 장면 목록 번호(없으면 -1).</summary>
        public static int BuildIndex(string path) => string.IsNullOrEmpty(path) ? -1 : SceneUtility.GetBuildIndexByScenePath(path);

        /// <summary>빌드 장면 목록에 있는가.</summary>
        public static bool InBuild(string path) => BuildIndex(path) >= 0;

        /// <summary>불러올 수 있는가: 빌드 목록에 있거나, 편집기에서 장면 파일이 있다.</summary>
        public static bool CanLoad(string path)
        {
            if (InBuild(path)) return true;
#if UNITY_EDITOR
            return !string.IsNullOrEmpty(path) && UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null;
#else
            return false;
#endif
        }

        /// <summary>
        /// 장면을 통째로 바꾼다(Single). 불러오기 시각을 적는다. 불러올 수 없으면 경고를 남기고 false(부르는 쪽이 대신 길로).
        /// 정적 상태는 새 장면 루트의 Awake 첫 줄(SceneStatics.Reset)이 비운다.
        /// </summary>
        public static bool Load(string path)
        {
            if (!CanLoad(path))
            {
                Debug.LogWarning($"[장면] {path}를 불러올 수 없다(빌드 목록에도 파일에도 없음)");
                return false;
            }
            RequestedRealtime = Time.realtimeSinceStartup;
            RequestedPath = path;
            int index = BuildIndex(path);
            if (index >= 0)
            {
                SceneManager.LoadScene(index, LoadSceneMode.Single);
                return true;
            }
#if UNITY_EDITOR
            Debug.LogWarning($"[장면] {path}가 빌드 목록에 없어 편집기 경로로 불러온다 — 'Demo6/마을 씬 만들기'를 다시 돌리면 목록이 복구된다");
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 새 장면이 부른다: 옛 장면이 불러오기를 부른 때부터 지금까지(초)를 돌려주고 기록을 비운다. path가 있으면 그 장면을 불렀을 때만. 기록이 없으면 -1.
        /// </summary>
        public static float TakeLoadSeconds(string path = null)
        {
            if (RequestedRealtime < 0f) return -1f;
            if (path != null && RequestedPath != path) return -1f;
            float seconds = Time.realtimeSinceStartup - RequestedRealtime;
            RequestedRealtime = -1f;
            RequestedPath = null;
            return seconds;
        }
    }
}
