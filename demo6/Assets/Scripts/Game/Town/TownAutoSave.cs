using Demo6.Core.Save;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 자동 저장(기획/저장-처음화면-멈춤창-1차.md 3-2·3-3). TownRoot가 Awake에서 붙인다(처음 화면 동안에는 붙이지 않음).
    /// 시점: ① 도착 처리 직후(Start, 바구니 도착이고 처음 처리한 도착일 때만 — 도착 카드가 뜨기 전에 바로 씀)
    /// ② 창이 닫힐 때(DungeonUi.Modal이 어떤 이름에서 다른 값 — 없음이나 다른 창 — 으로 바뀐 프레임. 대화 장면 끝·가방·스킬·모루·의뢰 목록·권양기 취소·도착 카드)
    /// ③ 게임 창을 닫을 때(OnApplicationQuit). 권양기 출발 직전 저장은 TownRoot.Depart가, 멈춤 창 '저장하고 …'는 GameFlow가 부른다.
    /// 창 닫힘은 창 이름으로 고르지 않는다. 아무것도 바뀌지 않은 창은 GameSave의 '같으면 안 씀'이 거른다(3-2). 미뤄 둔 요청은 하나만 들고 매 프레임 다시 본다(3-3).
    /// 이어하기·읽지 못해 새로 시작한 판의 알림은 마을이 밝아진 뒤 한 번 띄운다(4-8).
    /// </summary>
    public sealed class TownAutoSave : MonoBehaviour
    {
        TownRoot _root;
        /// <summary>지난 프레임의 창 이름(없으면 null).</summary>
        string _lastModal;
        /// <summary>창 닫힘 저장을 미뤄 두었다(하나만).</summary>
        bool _pending;

        void Awake() => _root = GetComponent<TownRoot>();

        /// <summary>도착 처리 직후 저장(3-2): 바구니 도착이고 처음 처리한 도착일 때만(새 플레이·같은 도착은 빼기, SaveRules.SaveOnArrival).</summary>
        void Start()
        {
            if (_root && _root.Arrival != null && SaveRules.SaveOnArrival(_root.ArrivalType, _root.Arrival.Repeated))
                GameSave.SaveTown(SaveReason.Arrival);
        }

        void LateUpdate()
        {
            // 어떤 창이든 닫혔거나 같은 프레임에 다른 창으로 바뀜. '없음으로 바뀜'만 보면 A 닫고 B 열기를 놓친다(3-2).
            // 기다림(창이 열려 있음·밝아지는 중·3초 안)은 SaveRules.Decide가 건다.
            string m = DungeonUi.Modal;
            if (_lastModal != null && m != _lastModal) _pending = true;
            _lastModal = m;
            if (_pending)
            {
                var o = GameSave.SaveTown(SaveReason.WindowClosed);
                if (o != SaveOutcome.Waiting) _pending = false;
            }
            // 처음 화면에서 시작한 판에서 F1을 쓰면 바로 '이제 저장하지 않는다'를 한 번 알린다(3-1).
            GameSave.WarnIfDevPanelUsed();
            // 이어서 알림(4-8): 마을이 밝아진 뒤 한 번. 저장하는 판이 아니면(밝아지기 전에 시험 메뉴로 갔다가 들어온 판) 띄우지 않고 버린다.
            if (_root && !_root.Busy)
            {
                var lines = GameSave.TakeArrivalNotice();
                if (lines != null && GameSession.FromTitle)
                    foreach (var line in lines)
                        if (!string.IsNullOrEmpty(line)) DungeonEvents.Say(line);
            }
        }

        /// <summary>게임 창을 닫음(Alt+F4·창 닫기·에디터 Play 끝, 3-2). 떠나는 중·저장하지 않는 판·오프닝 전이면 쓰지 않는다(SaveRules.Decide).</summary>
        void OnApplicationQuit() => GameSave.SaveTown(SaveReason.AppQuit);
    }
}
