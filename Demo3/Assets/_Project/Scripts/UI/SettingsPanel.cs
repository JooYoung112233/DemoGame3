using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Live49.UI
{
    // A shared title overlay, constructed in the existing 1920 x 1080 viewport.
    public class SettingsPanel : MonoBehaviour
    {
        static readonly Color Cream = new Color32(242, 228, 196, 255);
        static readonly Color Muted = new Color32(178, 159, 128, 255);
        static readonly Color Gold = new Color32(174, 137, 81, 255);
        TMP_FontAsset _font;
        TMP_Text _volume, _speed, _instant, _preview, _saveStatus;
        Image _volumeFill, _speedFill;
        readonly Image[] _rows = new Image[6];
        Action _openSaveLoad;
        int _selected;
        int _openedFrame;
        public event Action Closed;
        public bool IsOpen => gameObject.activeSelf;

        public static SettingsPanel Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("SettingsOverlay", typeof(RectTransform), typeof(Image), typeof(SettingsPanel));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0, 0, 0, .68f);
            var view = go.GetComponent<SettingsPanel>();
            view._font = font;
            view.Build();
            go.SetActive(false);
            return view;
        }

        void Build()
        {
            var sheet = Box(transform, "SettingsCard", 500, 140, 920, 800, new Color32(34, 29, 24, 250));
            Box(sheet, "TopRule", 48, 0, 824, 2, Gold);
            Label(sheet, "설정", 56, 46, 600, 65, 44, Cream);
            Label(sheet, "나에게 편안한 속도로", 58, 118, 700, 36, 20, Muted);
            Box(sheet, "HeaderRule", 56, 178, 808, 1, new Color(Gold.r, Gold.g, Gold.b, .4f));

            for (int i = 0; i < 3; i++)
            {
                _rows[i] = Box(sheet, "Row" + i, 40, 202 + 100 * i, 840, 88, Color.clear).GetComponent<Image>();
                _rows[i].raycastTarget = false;
            }
            Label(sheet, "전체 음량", 62, 223, 260, 42, 26, Cream);
            Label(sheet, "대사 출력 속도", 62, 323, 260, 42, 26, Cream);
            Label(sheet, "대사 즉시 출력", 62, 423, 260, 42, 26, Cream);
            _volume = Label(sheet, "", 632, 225, 122, 38, 24, Cream, TextAlignmentOptions.Center);
            _speed = Label(sheet, "", 632, 325, 122, 38, 24, Cream, TextAlignmentOptions.Center);
            _instant = Label(sheet, "", 490, 425, 250, 38, 24, Cream, TextAlignmentOptions.Center);
            Box(sheet, "VolumeTrack", 354, 242, 220, 3, new Color32(75, 64, 49, 255));
            Box(sheet, "SpeedTrack", 354, 342, 220, 3, new Color32(75, 64, 49, 255));
            _volumeFill = Box(sheet, "VolumeFill", 354, 242, 220, 3, Gold).GetComponent<Image>();
            _speedFill = Box(sheet, "SpeedFill", 354, 342, 220, 3, Gold).GetComponent<Image>();
            Button(sheet, "−", 590, 216, 42, 54, () => Adjust(0, -1));
            Button(sheet, "+", 770, 216, 42, 54, () => Adjust(0, 1));
            Button(sheet, "−", 590, 316, 42, 54, () => Adjust(1, -1));
            Button(sheet, "+", 770, 316, 42, 54, () => Adjust(1, 1));
            Button(sheet, "변경", 754, 416, 76, 54, () => Adjust(2, 1));

            Box(sheet, "PreviewRule", 56, 510, 808, 1, new Color(Gold.r, Gold.g, Gold.b, .4f));
            Label(sheet, "대사 미리보기", 62, 536, 730, 28, 17, Muted);
            _preview = Label(sheet, "", 62, 584, 795, 58, 27, Cream);
            _saveStatus = Label(sheet, "", 56, 656, 808, 28, 17, Muted);
            _rows[3] = Button(sheet, "기본값으로", 56, 716, 222, 60, ResetValues).GetComponent<Image>();
            _rows[4] = Button(sheet, "저장·불러오기", 298, 716, 306, 60, RequestSaveLoad).GetComponent<Image>();
            _rows[4].gameObject.SetActive(false);
            _rows[5] = Button(sheet, "돌아가기", 634, 716, 230, 60, Close).GetComponent<Image>();
            Label(transform, "↑ ↓ 항목   ·   ← → 조절   ·   Enter 선택   ·   Esc 돌아가기", 500, 968, 920, 32, 18, Muted, TextAlignmentOptions.Center);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _selected = 0;
            _openedFrame = Time.frameCount;
            if(_openSaveLoad!=null)
            {
                var slot=Core.SaveSlots.Read(0);
                _saveStatus.text=Core.SaveSystem.AutoSaveError!=null?"자동 저장을 확인해주세요. 저장·불러오기에서 다시 저장할 수 있어요.":
                    slot.Readable?"마지막 자동 저장  ·  "+Core.SaveSlots.Stamp(slot.State):"장소 이동 · 탐색 · 생활 행동 · 하루 전환 후 자동 저장";
            }
            Refresh();
            Preview();
        }

        public void Close()
        {
            GamePreferences.Save();
            StopAllCoroutines();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }
        public void ConfigureSaveLoad(Action open)
        {
            _openSaveLoad=open;
            _rows[4].gameObject.SetActive(open!=null);
        }
        public void RequestSaveLoad()
        {
            if(!IsOpen||_openSaveLoad==null)return;
            GamePreferences.Save();StopAllCoroutines();gameObject.SetActive(false);
            _openSaveLoad();
        }
        void MoveSelection(int direction)
        {
            do{_selected=(_selected+direction+_rows.Length)%_rows.Length;}
            while(!_rows[_selected].gameObject.activeSelf);
            Refresh();
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null || Time.frameCount == _openedFrame) return;
            if (k.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (k.upArrowKey.wasPressedThisFrame) MoveSelection(-1);
            if (k.downArrowKey.wasPressedThisFrame) MoveSelection(1);
            if (_selected < 3)
            {
                if (k.leftArrowKey.wasPressedThisFrame) Adjust(_selected, -1);
                if (k.rightArrowKey.wasPressedThisFrame) Adjust(_selected, 1);
            }
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
            {
                if (_selected == 2) Adjust(2, 1);
                else if (_selected == 3) ResetValues();
                else if (_selected == 4) RequestSaveLoad();
                else if (_selected == 5) Close();
            }
        }

        public void Adjust(int row, int direction)
        {
            _selected = row;
            if (row == 0) GamePreferences.Volume += .05f * direction;
            else if (row == 1) GamePreferences.TextSpeed += .25f * direction;
            else if (row == 2) GamePreferences.InstantText = !GamePreferences.InstantText;
            Refresh();
            if (row != 0) Preview();
        }

        void ResetValues()
        {
            GamePreferences.Volume = .8f;
            GamePreferences.TextSpeed = 1f;
            GamePreferences.InstantText = false;
            _selected = 3;
            Refresh(); Preview();
        }

        void Refresh()
        {
            _volume.text = Mathf.RoundToInt(GamePreferences.Volume * 100) + "%";
            _speed.text = GamePreferences.TextSpeed.ToString("0.00") + "배";
            _speed.color = GamePreferences.InstantText ? Muted : Cream;
            _instant.text = GamePreferences.InstantText ? "켜짐" : "꺼짐";
            _volumeFill.rectTransform.sizeDelta = new Vector2(220 * GamePreferences.Volume, 3);
            _speedFill.rectTransform.sizeDelta = new Vector2(220 * (GamePreferences.TextSpeed - .5f) / 1.5f, 3);
            for (int i = 0; i < _rows.Length; i++) _rows[i].color = i == _selected ? new Color(Gold.r, Gold.g, Gold.b, .16f) : new Color(Gold.r, Gold.g, Gold.b, i > 2 ? .06f : 0);
        }

        void Preview() { StopAllCoroutines(); StartCoroutine(PlayPreview()); }
        IEnumerator PlayPreview()
        {
            _preview.text = "오늘의 작은 순간을, 오래 기억하고 싶어.";
            _preview.ForceMeshUpdate();
            int count = _preview.textInfo.characterCount;
            _preview.maxVisibleCharacters = GamePreferences.InstantText ? count : 0;
            while (_preview.maxVisibleCharacters < count)
            {
                _preview.maxVisibleCharacters++;
                yield return new WaitForSecondsRealtime(.05f / GamePreferences.TextSpeed);
            }
        }

        RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            return r;
        }
        RectTransform Box(Transform p, string name, float x, float y, float w, float h, Color color)
        {
            var r = Rect(p, name, x, y, w, h);
            r.gameObject.AddComponent<Image>().color = color;
            return r;
        }
        TMP_Text Label(Transform p, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var t = Rect(p, "Label_" + text, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
        RectTransform Button(Transform p, string text, float x, float y, float w, float h, Action action)
        {
            var r = Box(p, "Button_" + text, x, y, w, h, new Color(Gold.r, Gold.g, Gold.b, .08f));
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            var colors = b.colors; colors.highlightedColor = new Color(1, .92f, .75f); colors.pressedColor = Gold; b.colors = colors;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => action());
            Label(r, text, 0, 0, w, h, 24, Cream, TextAlignmentOptions.Center);
            return r;
        }
        void OnApplicationPause(bool paused) { if (paused) GamePreferences.Save(); }
        void OnApplicationQuit() => GamePreferences.Save();
    }
}
