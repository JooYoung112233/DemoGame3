using Demo6.Core.Save;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 설정 창 내용(기획/저장-처음화면-멈춤창-1차.md 6-1·6-4, 11-7 'D'). 창 틀·제목 '설정'·'돌아가기'·Esc는 멈춤 창과 처음 화면(작성자 C)이 그리고,
    /// 이 파일은 그 안(area, DungeonUi 기준 좌표)에 줄만 그린다: 소리 켬·끔, 소리 크기 n%, 화면 흔들림 n%, 피해 숫자·타격 멈춤·피 발자국·
    /// 대사 글 바로 보이기 켬·끔, '기본값으로', 아래 흐린 줄. OnGUI 안에서 DungeonUi.Begin 뒤에 부른다(스타일이 없으면 기본 스킨으로 그림).
    /// 값이 실제로 바뀐 때만(맞춘 뒤 값이 같으면 쓰지 않음) SettingsStore.Set으로 바로 넣고 기기 설정에 쓴다. 저장 파일과는 따로다.
    /// 기능만 있는 IMGUI 줄이다 — 배치·반응형 다듬기는 Unity 개발 단계로 둔다.
    /// </summary>
    public static class SettingsPanel
    {
        /// <summary>아래 흐린 줄(6-4).</summary>
        public const string FootLine = "설정은 이 컴퓨터에만 남는다(저장 파일과 따로).";

        const float LabelWidth = 240f;
        const float SwitchWidth = 96f;
        const float SliderWidth = 280f;
        const float RowHeight = 34f;
        const float RowGap = 6f;
        /// <summary>슬라이더를 줄 높이 가운데쯤에 두는 위 여백.</summary>
        const float SliderTop = 13f;

        static GUIStyle _label;
        static GUIStyle _labelBase;
        static GUIStyle _dim;
        static GUIStyle _dimBase;

        /// <summary>도메인 다시 불러오기 꺼짐 대비: 플레이 시작 때 스타일 캐시를 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _label = null;
            _labelBase = null;
            _dim = null;
            _dimBase = null;
        }

        /// <summary>설정 줄을 area 안에 그린다(제목·'돌아가기'는 그리지 않음). OnGUI 안에서만 부른다.</summary>
        public static void Draw(Rect area)
        {
            EnsureStyles();
            var current = SettingsStore.Current;
            var next = current.Clone();
            bool reset;

            GUILayout.BeginArea(area);
            next.Sound = SwitchRow("소리", next.Sound);
            next.Volume = SliderRow("소리 크기", next.Volume);
            next.Shake = SliderRow("화면 흔들림", next.Shake);
            next.DamageNumbers = SwitchRow("피해 숫자", next.DamageNumbers);
            next.HitStop = SwitchRow("타격 멈춤", next.HitStop);
            next.Footprints = SwitchRow("피 발자국", next.Footprints);
            next.InstantText = SwitchRow("대사 글 바로 보이기", next.InstantText);
            GUILayout.Space(RowGap * 2f);
            GUILayout.BeginHorizontal();
            reset = GUILayout.Button("기본값으로", GUILayout.Width(170f), GUILayout.Height(RowHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(FootLine, _dim);
            GUILayout.EndArea();

            // 값이 실제로 바뀐 때만 쓴다(6-4). 슬라이더는 맞춘(5%·10% 단위) 값이 달라질 때만 쓰게 된다.
            if (reset)
            {
                if (!current.SameAs(new GameSettings())) SettingsStore.ResetToDefaults();
            }
            else if (!next.Normalize().SameAs(current))
            {
                SettingsStore.Set(next);
            }
        }

        /// <summary>'이름 [켬]' 한 줄. 단추를 누르면 뒤집은 값을 돌려준다.</summary>
        static bool SwitchRow(string name, bool on)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label(name, _label, GUILayout.Width(LabelWidth), GUILayout.Height(RowHeight));
            if (GUILayout.Button(on ? "켬" : "끔", GUILayout.Width(SwitchWidth), GUILayout.Height(RowHeight))) on = !on;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(RowGap);
            return on;
        }

        /// <summary>'이름 n%' + 0~1 슬라이더 한 줄. 끌어 놓은 값을 그대로 돌려준다(맞추기는 Normalize가 함).</summary>
        static float SliderRow(string name, float value)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label(name + " " + Mathf.RoundToInt(value * 100f) + "%", _label, GUILayout.Width(LabelWidth), GUILayout.Height(RowHeight));
            GUILayout.BeginVertical(GUILayout.Width(SliderWidth));
            GUILayout.Space(SliderTop);
            value = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.Width(SliderWidth));
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(RowGap);
            return value;
        }

        /// <summary>DungeonUi 스타일(Label·Small)로 만든다. DungeonUi가 플레이마다 스타일을 새로 만들면 따라 새로 만든다.</summary>
        static void EnsureStyles()
        {
            var labelBase = DungeonUi.Label ?? GUI.skin.label;
            if (_label == null || _labelBase != labelBase)
            {
                _labelBase = labelBase;
                _label = new GUIStyle(labelBase) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            }
            var dimBase = DungeonUi.Small ?? GUI.skin.label;
            if (_dim == null || _dimBase != dimBase)
            {
                _dimBase = dimBase;
                _dim = new GUIStyle(dimBase) { wordWrap = true };
                _dim.normal.textColor = DungeonUi.BoneDim;
            }
        }
    }
}
