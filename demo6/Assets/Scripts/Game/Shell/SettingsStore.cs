using System;
using Demo6.Core.Save;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 설정 보관소(기획/저장-처음화면-멈춤창-1차.md 6-3, 11-7 'D'). 설정 값(GameSettings)을 저장 파일과 따로 기기 설정 PlayerPrefs 키
    /// `demo6.settings`에 글로 둔다(시험 메뉴 설정 `demo6.testlauncher.preset`과 같은 방식, TestLauncher.cs 23). 읽기·쓰기는 모두 try/catch,
    /// 읽지 못하면 기본값이고 쓰지 못하면 경고만 남긴다.
    /// 게임을 켤 때(BeforeSceneLoad — 정적 값 되돌리기 SubsystemRegistration, 예: GoreFootprints.ResetStatics보다 늦게) 한 번 읽어 넣고,
    /// 설정 창(SettingsPanel)에서 바꾸면 바로 넣고 바로 쓴다. 넣는 곳: Tuning.Sound·SoundVolume·ShakeScale·DamageNumbers·HitStopEnabled,
    /// GoreFootprints.Enabled, TalkWindow.InstantText. 저장 파일(GameSave)은 건드리지 않는다.
    /// F1 '손맛 기본값으로'나 전투 시험장 손잡이로 바꾼 같은 값은 다음에 게임을 켤 때 설정 값으로 돌아간다(6-3, 에디터 전용이라 받아들임).
    /// </summary>
    public static class SettingsStore
    {
        /// <summary>기기 설정 키(6-3).</summary>
        public const string PrefsKey = "demo6.settings";

        static GameSettings _current;

        /// <summary>지금 설정(복사본). 고쳐도 넣어지지 않는다 — 바꾸려면 Set으로 넘긴다.</summary>
        public static GameSettings Current
        {
            get
            {
                if (_current == null) Boot();
                return _current.Clone();
            }
        }

        /// <summary>도메인 다시 불러오기 꺼짐 대비: 플레이 시작 때 캐시를 비운다(곧이어 Boot가 다시 읽음).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        /// <summary>게임을 켤 때 기기 설정을 읽어 넣는다(6-3). 정적 값 되돌리기(SubsystemRegistration) 뒤에 돈다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            _current = GameSettings.Parse(ReadPrefs());
            Apply(_current);
        }

        /// <summary>설정을 바꾼다: 맞추고(Normalize) → 바로 넣고 → 기기 설정에 쓴다. 부르는 쪽(설정 창)이 값이 실제로 바뀐 때만 부른다.</summary>
        public static void Set(GameSettings s)
        {
            if (s == null) return;
            s.Normalize();
            _current = s.Clone();
            Apply(_current);
            WritePrefs(_current.ToText());
        }

        /// <summary>설정 창 '기본값으로'.</summary>
        public static void ResetToDefaults() => Set(new GameSettings());

        static string ReadPrefs()
        {
            try
            {
                return PlayerPrefs.GetString(PrefsKey, "");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[설정] 기기 설정을 읽지 못해 기본값으로 시작한다: " + e.Message);
                return "";
            }
        }

        static void WritePrefs(string text)
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, text);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[설정] 기기 설정을 쓰지 못했다(이번 판에는 그대로 적용됨): " + e.Message);
            }
        }

        /// <summary>설정 값을 게임 값에 넣는다(6-1 표). Tuning.cs는 고치지 않고 값만 쓴다.</summary>
        static void Apply(GameSettings s)
        {
            Tuning.Sound = s.Sound;
            Tuning.SoundVolume = s.Volume;
            Tuning.ShakeScale = s.Shake;
            Tuning.DamageNumbers = s.DamageNumbers;
            Tuning.HitStopEnabled = s.HitStop;
            GoreFootprints.Enabled = s.Footprints;
            TalkWindow.InstantText = s.InstantText;
        }
    }
}
