using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 시설·물체 F(기획/마을-의뢰-첫판.md 2-4·5-8): 의뢰 게시판 'F 의뢰 목록' → 의뢰 목록 창(읽기만, 받기·보고는 주민에게서만),
    /// 주막 덧문·수레 'F 살펴보기' → 나레이션 모양 글, 모루 'F 모루' → 모루 창(ForgeWindow, 기획/재화-쓸-곳-1차.md 2-1).
    /// 안내·글은 TownScript 물체 표, 자리·반경은 TownLayout(시설 1.4, 이야기 물건 1.2). 시설 창 제목·물체 글에 주민 이름을 쓰지 않는다.
    /// 안내 문구는 지금 상호작용 코드(InteractionSystem)가 '[F] {안내}'로 그린다.
    /// </summary>
    public sealed class TownFacility : Interactable
    {
        string _id = "";
        string _prompt = "";
        float _range = TownLayout.FacilityRadius;

        /// <summary>물체 id(TownScript.ObjBoard 등).</summary>
        public string Id => _id;
        public override float Range => _range;
        public override string Prompt => _prompt;
        public override bool Available => TownRoot.Instance && !TownRoot.Instance.Busy;

        public static TownFacility Create(Transform parent, TownSpot spot)
        {
            var go = new GameObject("F " + spot.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector2(spot.Pos.X, spot.Pos.Y);
            var f = go.AddComponent<TownFacility>();
            f._id = spot.Id;
            f._range = spot.Radius;
            f._prompt = PromptOf(TownScript.Object(spot.Id));
            return f;
        }

        /// <summary>물체 표 안내('F 의뢰 목록')에서 'F '를 뗀 글(상호작용 코드가 '[F] '를 붙인다).</summary>
        public static string PromptOf(TownObjectText text)
        {
            string hint = text != null ? text.Hint : "";
            if (string.IsNullOrEmpty(hint)) return "살펴보기";
            return hint.StartsWith("F ") ? hint.Substring(2) : hint;
        }

        public override void Interact()
        {
            var root = TownRoot.Instance;
            if (!root) return;
            if (_id == TownScript.ObjBoard)
            {
                root.OpenQuestBoard();
                return;
            }
            // 모루 F → 모루 창(재화 쓸 곳 1차 2-1). 떠나는 중이거나 다른 창이 열려 있으면 열리지 않는다.
            if (_id == TownScript.ObjAnvil) { ForgeWindow.Open(); return; }
            var text = TownScript.Object(_id);
            if (text != null) root.Narrate(text.Text);
        }
    }
}
