using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    [Serializable] public sealed class SavedDevelopment
    {
        public bool Bed, Workbench, Cooker, Research, Tools, Comfort, Storage;
        public int Warehouse;
    }

    // Restoration and research use the same reservation/worker/time pipeline as crafting.
    public sealed class SettlementDevelopment : MonoBehaviour
    {
        public Button OpenButton, ResearchButton;
        public Text OpenLabel;
        public SavedDevelopment State = new SavedDevelopment();
        SettlementController owner;
        public int Capacity => State.Warehouse >= 2 ? 80 : State.Warehouse == 1 ? 40 : 12;
        public int StockUsed => owner.CraftPanel.Materials.Sum(m => m.Initial);
        public int FreeSpace => Mathf.Max(0, Capacity - StockUsed);
        public string StockSummary => (State.Warehouse == 0 ? "임시 보관" : "창고 Lv." + State.Warehouse) + " · " + StockUsed + " / " + Capacity + "개";
        public void Initialize(SettlementController c)
        {
            owner = c;
            OpenButton.onClick.AddListener(Open);
            ResearchButton.onClick.AddListener(() => { Open(); owner.CraftPanel.FocusRecipe(State.Research ? "research-tools" : "build-research"); });
        }
        public void Open() { owner.CraftPanel.Open(); owner.CraftPanel.FocusRecipe(NextProject); }
        public string NextProject => State.Warehouse == 0 ? "build-stock" : !State.Workbench ? "build-bench" : !State.Bed ? "build-bed" : !State.Cooker ? "build-cooker" : !State.Research ? "build-research" : !State.Tools ? "research-tools" : !State.Comfort ? "research-comfort" : !State.Storage ? "research-storage" : "expand-stock";
        public bool IsDone(string id)
        {
            switch(id) {
                case "build-bed": return State.Bed;
                case "build-bench": return State.Workbench;
                case "build-cooker": return State.Cooker;
                case "build-research": return State.Research;
                case "build-stock": return State.Warehouse >= 1;
                case "expand-stock": return State.Warehouse >= 2;
                case "research-tools": return State.Tools;
                case "research-comfort": return State.Comfort;
                case "research-storage": return State.Storage;
                default: return false;
            }
        }
        public string Block(SettlementCraftPanel.Recipe r)
        {
            if (r == null) return null;
            if (r.Category == 0 && !State.Workbench) return "작업대 복구 필요";
            if ((r.Id == "build-cooker" || r.Id == "build-research") && !State.Workbench) return "작업대 복구 필요";
            if (r.Id.StartsWith("research-") && !State.Research) return "연구대 설치 필요";
            if (r.Id == "expand-stock" && (!State.Storage || State.Warehouse == 0)) return "창고 복구·보관 연구 필요";
            if (r.Id == "upgrade-bench" && (!State.Workbench || !State.Tools)) return "공구 연구 필요";
            if (r.Id == "repair-bed" && (!State.Bed || !State.Comfort)) return "잠자리 복구·생활 연구 필요";
            if (r.Id == "upgrade-cooker" && (!State.Cooker || !State.Comfort)) return "조리대 복구·생활 연구 필요";
            if ((r.Id == "open-side-room" || r.Id == "prepare-side-room") && !State.Tools) return "공구 연구 필요";
            if (r.Id == "build-bed" && owner.WorkPanel.Orders.Count > 0) return "휴식 완료 후 복구 가능";
            return null;
        }
        public bool Complete(string id)
        {
            switch(id) {
                case "build-bed": State.Bed=true; break;
                case "build-bench": State.Workbench=true; break;
                case "build-cooker": State.Cooker=true; break;
                case "build-research": State.Research=true; break;
                case "build-stock": State.Warehouse=1; break;
                case "expand-stock": State.Warehouse=2; break;
                case "research-tools": State.Tools=true; break;
                case "research-comfort": State.Comfort=true; break;
                case "research-storage": State.Storage=true; break;
                default: return false;
            }
            return true;
        }
        void LateUpdate()
        {
            if (!owner || owner.Campaign == null) return;
            bool show = owner.Main.gameObject.activeInHierarchy && owner.Main.interactable && (!owner.Introduction || owner.Introduction.Allows(5));
            bool storyAction=false;
            if(owner.TutorialSkipped&&owner.Opening&&owner.Opening.Active){owner.Opening.Evaluate();storyAction=owner.Opening.CurrentAction>=4;}
            // These buttons share the notice footer. Keep the genuine record/event reachable;
            // the workbench itself still opens construction while a story action is offered.
            OpenButton.gameObject.SetActive(show && (owner.TutorialSkipped&&!storyAction || !owner.Opening || !owner.Opening.Active)); ResearchButton.gameObject.SetActive(show && State.Research);
            OpenLabel.text = "시설 복구 · 연구";
        }
    }
}
