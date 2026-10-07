using System;
using Demo6.Core.Save;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 멈춤 창(기획/저장-처음화면-멈춤창-1차.md 5-3·5-4·5-5, IMGUI 기능만 — 배치 다듬기는 Unity 개발 단계). GameShell 물체에 붙어 장면을 넘어 산다.
    /// 창 이름 "pause"(ShellRules.PauseModal)로 DungeonUi.TryOpen을 거쳐 열므로 다른 창과 똑같이 시간이 멈추고 입력이 막힌다. Esc는 GameShell이 받아 Open·Back·Close를 부른다.
    /// 첫 화면 단추는 ShellRules.PauseItems(곳, 저장하는 판): 마을·저장하는 판 = 계속·설정·지난 알림·저장하고 처음 화면으로·저장하고 끝내기,
    /// 그 밖 = 계속·설정·지난 알림·처음 화면으로·끝내기. 아래 흐린 줄은 마지막 저장 시각(ShellText.PauseFooter).
    /// 던전·저장하지 않는 판에서 떠나기 전에 묻는다(5-4): 던전·저장하는 판은 '이번 원정은 남지 않는다'(저장은 마을에서만, 사용자 결정 2026-10-07).
    /// '저장하고 …'에서 저장이 실패하면 '저장하지 못했다 — 그래도 …?'를 묻는다. 설정·지난 알림 내용은 SettingsPanel.Draw·NoticeLog.DrawList가 그린다.
    /// 단추 일은 OnGUI에서 받아 다음 Update에서 한 번 한다(GUILayout을 쓰는 화면이 같은 OnGUI 안에서 바뀌지 않게). GUI.depth −90(도착 카드 −60·대화창 −50보다 위,
    /// 마을 밝아짐 막 −100·밤 카드 −200 아래 — 그 둘이 있을 때는 열리지 않는다). 처음 화면이 보이는 동안은 그리지 않는다.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        const int GuiDepth = -90;
        const float Pad = 24f;
        const float TitleHeight = 46f;
        const float ButtonHeight = 46f;
        const float ButtonGap = 8f;
        const float MainWidth = 440f;
        const float ConfirmWidth = 580f;
        const float SettingsWidth = 640f;
        const float SettingsHeight = 600f;
        const float NoticesWidth = 780f;
        const float NoticesHeight = 640f;

        enum Page { Main, Settings, Notices, ConfirmLeave, SaveFailed }

        static PauseMenu s_instance;

        Page _page;
        /// <summary>떠나기 확인·저장 실패 확인이 '처음 화면으로' 길인가(아니면 끝내기).</summary>
        bool _toTitle;
        Action _pending;
        GUIStyle _foot;
        GUIStyle _body;
        GUIStyle _styleSource;
        readonly GUIContent _measure = new GUIContent();

        /// <summary>멈춤 창이 열려 있다(DungeonUi 창 이름이 "pause").</summary>
        public static bool IsOpen => DungeonUi.Modal == ShellRules.PauseModal;

        /// <summary>멈춤 창 안의 설정·지난 알림·확인 화면이 열려 있다(첫 화면이 아님) — Esc가 첫 화면으로 돌아간다(5-1 차례 3).</summary>
        public static bool SubOpen => IsOpen && s_instance && s_instance._page != Page.Main;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => s_instance = null;

        /// <summary>멈춤 창을 연다(다른 창이 열려 있으면 열리지 않음). 열면 첫 화면부터.</summary>
        public static void Open()
        {
            if (!DungeonUi.TryOpen(ShellRules.PauseModal)) return;
            if (s_instance) s_instance.ResetScreen();
        }

        /// <summary>멈춤 창을 닫는다(= 계속). 열려 있지 않으면 아무것도 하지 않는다.</summary>
        public static void Close()
        {
            DungeonUi.Close(ShellRules.PauseModal);
            if (s_instance) s_instance.ResetScreen();
        }

        /// <summary>Esc: 하위 화면이면 첫 화면으로, 첫 화면이면 닫는다.</summary>
        public static void Back()
        {
            if (!IsOpen) return;
            if (s_instance && s_instance._page != Page.Main) s_instance.ResetScreen();
            else Close();
        }

        void Awake() => s_instance = this;

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        void ResetScreen()
        {
            _page = Page.Main;
            _toTitle = false;
            _pending = null;
        }

        void Update()
        {
            // 장면이 바뀌어 창이 비워졌거나(DungeonUi.ResetStatics) 다른 길로 닫혔으면 첫 화면으로 되돌리고, 닫힌 창에서 누른 단추는 버린다.
            if (!IsOpen)
            {
                if (_page != Page.Main || _pending != null) ResetScreen();
                return;
            }
            if (_pending == null) return;
            var action = _pending;
            _pending = null;
            action();
        }

        /// <summary>단추 일을 다음 Update로 넘긴다(한 프레임에 하나).</summary>
        void Do(Action action)
        {
            if (_pending == null) _pending = action;
        }

        void OnItem(PauseItem item)
        {
            var place = GameShell.Place;
            bool saving = GameSave.SavingSession;
            switch (item)
            {
                case PauseItem.Resume:
                    Close();
                    break;
                case PauseItem.Settings:
                    _page = Page.Settings;
                    break;
                case PauseItem.Notices:
                    _page = Page.Notices;
                    break;
                case PauseItem.SaveAndTitle:
                    if (!GameFlow.LeaveToTitle(true)) ShowSaveFailed(true);
                    break;
                case PauseItem.SaveAndQuit:
                    if (!GameFlow.Quit(true)) ShowSaveFailed(false);
                    break;
                case PauseItem.ToTitle:
                    if (ShellRules.ConfirmBeforeLeaving(place, saving)) ShowConfirm(true);
                    else GameFlow.LeaveToTitle(false);
                    break;
                case PauseItem.Quit:
                    if (ShellRules.ConfirmBeforeLeaving(place, saving)) ShowConfirm(false);
                    else GameFlow.Quit(false);
                    break;
            }
        }

        void ShowConfirm(bool toTitle)
        {
            _toTitle = toTitle;
            _page = Page.ConfirmLeave;
        }

        void ShowSaveFailed(bool toTitle)
        {
            _toTitle = toTitle;
            _page = Page.SaveFailed;
        }

        static void Leave(bool toTitle)
        {
            if (toTitle) GameFlow.LeaveToTitle(false);
            else GameFlow.Quit(false);
        }

        /// <summary>마지막 저장 시각 글(이 기기 시각, SaveText.When). 저장이 없으면 null.</summary>
        static string LastSavedWhen()
        {
            var utc = GameSave.LastSavedUtc;
            return utc.HasValue ? SaveText.When(utc.Value.ToLocalTime()) : null;
        }

        void EnsureStyles()
        {
            if (_foot != null && _styleSource == DungeonUi.Small) return;
            _styleSource = DungeonUi.Small;
            _foot = new GUIStyle(DungeonUi.Small) { wordWrap = true };
            _body = new GUIStyle(DungeonUi.Label) { wordWrap = true };
        }

        void OnGUI()
        {
            if (!IsOpen || GameSession.TitleShowing) return;
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = GuiDepth;

            // 멈춘 놀이 화면을 어둡게(창 밖 클릭은 아무 일도 하지 않는다). 두 번 누름은 Do가 첫 단추만 받는다.
            DungeonUi.Fill(new Rect(0f, 0f, DungeonUi.Width, DungeonUi.Height), new Color(0f, 0f, 0f, 0.55f));

            var place = GameShell.Place;
            bool saving = GameSave.SavingSession;
            switch (_page)
            {
                case Page.Settings:
                    DrawSub(ShellText.Settings, SettingsWidth, SettingsHeight, true);
                    break;
                case Page.Notices:
                    DrawSub(ShellText.Notices, NoticesWidth, NoticesHeight, false);
                    break;
                case Page.ConfirmLeave:
                    DrawConfirm(ShellText.LeaveConfirmTitle(place, saving) ?? ShellText.PauseTitle,
                        ShellText.LeaveConfirmBody(place, saving, LastSavedWhen(), GameSave.LastSaveWasTrip) ?? "",
                        ShellText.LeaveConfirmButton(_toTitle));
                    break;
                case Page.SaveFailed:
                    DrawConfirm(ShellText.SaveFailedTitle, ShellText.SaveFailedBody(GameSave.LastError, _toTitle), ShellText.SaveFailedButton(_toTitle));
                    break;
                default:
                    DrawMain(place, saving);
                    break;
            }

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        static Rect Centered(float width, float height)
            => DungeonUi.KeepOnScreen(new Rect((DungeonUi.Width - width) * 0.5f, (DungeonUi.Height - height) * 0.5f, width, height));

        void DrawMain(ShellPlace place, bool saving)
        {
            var items = ShellRules.PauseItems(place, saving);
            string footer = ShellText.PauseFooter(place, saving, LastSavedWhen(), GameSave.LastSaveWasTrip, GameSave.LastError);
            float inner = MainWidth - Pad * 2f;
            float footerHeight = 0f;
            if (!string.IsNullOrEmpty(footer))
            {
                _measure.text = footer;
                footerHeight = Mathf.Max(24f, _foot.CalcHeight(_measure, inner));
            }
            float height = Pad + TitleHeight + items.Count * (ButtonHeight + ButtonGap) + (footerHeight > 0f ? 8f + footerHeight : 0f) + Pad;
            var r = Centered(MainWidth, height);
            DungeonUi.Box(r, 0.96f);
            float x = r.x + Pad;
            float y = r.y + Pad;
            GUI.Label(new Rect(x, y - 4f, inner, 38f), ShellText.PauseTitle, DungeonUi.Title);
            y += TitleHeight;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (GUI.Button(new Rect(x, y, inner, ButtonHeight), ShellText.PauseItemLabel(item))) Do(() => OnItem(item));
                y += ButtonHeight + ButtonGap;
            }
            if (footerHeight > 0f)
                DungeonUi.ShadowLabel(new Rect(x, y + 8f, inner, footerHeight), footer, _foot, DungeonUi.BoneDim);
        }

        /// <summary>설정·지난 알림 화면: 제목 + 내용(작성자 D의 Draw) + '돌아가기'.</summary>
        void DrawSub(string title, float width, float height, bool settings)
        {
            var r = Centered(width, height);
            DungeonUi.Box(r, 0.96f);
            float inner = r.width - Pad * 2f;
            float x = r.x + Pad;
            GUI.Label(new Rect(x, r.y + Pad - 4f, inner, 38f), title, DungeonUi.Title);
            var area = new Rect(x, r.y + Pad + TitleHeight, inner, r.height - Pad * 2f - TitleHeight - ButtonHeight - 12f);
            if (settings) SettingsPanel.Draw(area);
            else NoticeLog.DrawList(area);
            if (GUI.Button(new Rect(x, r.yMax - Pad - ButtonHeight, 220f, ButtonHeight), ShellText.Back)) Do(ResetScreen);
        }

        /// <summary>확인 화면(떠나기 확인·저장 실패): 제목 + 본문 + ['예' 단추] [돌아가기].</summary>
        void DrawConfirm(string title, string body, string yes)
        {
            float inner = ConfirmWidth - Pad * 2f;
            _measure.text = title;
            float titleHeight = Mathf.Max(38f, DungeonUi.Title.CalcHeight(_measure, inner));
            _measure.text = body;
            float bodyHeight = string.IsNullOrEmpty(body) ? 0f : Mathf.Max(26f, _body.CalcHeight(_measure, inner));
            float height = Pad + titleHeight + 10f + bodyHeight + 20f + ButtonHeight + Pad;
            var r = Centered(ConfirmWidth, height);
            DungeonUi.Box(r, 0.96f);
            float x = r.x + Pad;
            float y = r.y + Pad - 4f;
            GUI.Label(new Rect(x, y, inner, titleHeight), title, DungeonUi.Title);
            y += titleHeight + 10f;
            if (bodyHeight > 0f) GUI.Label(new Rect(x, y, inner, bodyHeight), body, _body);
            float half = (inner - 12f) * 0.5f;
            float by = r.yMax - Pad - ButtonHeight;
            bool toTitle = _toTitle;
            if (GUI.Button(new Rect(x, by, half, ButtonHeight), yes)) Do(() => Leave(toTitle));
            if (GUI.Button(new Rect(x + half + 12f, by, half, ButtonHeight), ShellText.Back)) Do(ResetScreen);
        }
    }
}
