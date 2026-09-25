using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // The 07 window's finds preview: remaining turns and time, the turn's noise (the object's own, 망보기 −1), and each drop's chance.
 // No pace since 2026-09-25 (기획/탐험-수색쪽지와-협동-1차.md); a search an older save started keeps its stored pace (its ±15 stays in the chances).
 public sealed class SearchDropPreview:MonoBehaviour {
  public RectTransform Content;public SearchDropRow RowPrefab;public Text Summary,Note;
  [Header("문구")]
  [Tooltip("{0}: 남은 턴 · {1}: 남은 시간 (분) · {2}: 이번 턴 소음")] public string SummaryLine="남은 {0}턴 · {1}분\n턴당 소음 +{2}";
  [Tooltip("{0}: 남은 턴 · {1}: 남은 시간 (분) · 소음 없는 수색")] public string SummaryQuiet="남은 {0}턴 · {1}분\n소음 없음";
  [Tooltip("모든 물건이 100%일 때 뒤에 붙음")] public string CertainSuffix=" · 확정 발견";
  [Tooltip("{0}: 조명 보정 (%p)")] public string LightSuffix=" · 조명 +{0}%p";
  [Tooltip("속도 삭제 전 저장의 진행 중 함께 수색 보정 ({0}: %p)")] public string TogetherSuffix=" · 함께 +{0}%p";
  public string NoteRunning="진행 중 · 역할과 남은 턴 유지", NoteNew="각 물건을 개별 판정 · 완료 시 결과 확인";
  public readonly List<SearchDropRow> Rows=new List<SearchDropRow>();
  // First visit: the loot panel's own numbers (what Advance applies) for this role.
  public void Refresh(ExpeditionArrivalPanel arrival,int site,int duty){var s=arrival.Loot.State(site);Refresh(arrival,site,s.Progress>0?s.Pace:1,duty,arrival.Loot.BonusFor(site,duty),arrival.Loot.NoiseFor(site,duty),arrival.Loot.RequiredFor(site,duty));}
  // pace: legacy and ignored (kept so older callers compile); same as Refresh(arrival, site, duty).
  public void Refresh(ExpeditionArrivalPanel arrival,int site,int pace,int duty)=>Refresh(arrival,site,duty);
  public void Refresh(ExpeditionArrivalPanel arrival,int site,int pace,int duty,int bonus,int noise)=>Refresh(arrival,site,pace,duty,bonus,noise,arrival.Loot.RequiredFor(site,duty));
  // pace: the search's stored pace (1 for every new one); bonus / noise / required: as the turn applies them (the site board passes its FieldRun).
  public void Refresh(ExpeditionArrivalPanel arrival,int site,int pace,int duty,int bonus,int noise,int required){
   foreach(var row in Rows){row.gameObject.SetActive(false);Destroy(row.gameObject);}Rows.Clear();
   var state=arrival.Loot.State(site);int remaining=Mathf.Max(0,(state.Required>0?state.Required:required)-state.Progress),minutes=remaining*arrival.Rooms.MinutesPerTurn;
   Summary.text=(noise>0?string.Format(SummaryLine,remaining,minutes,noise):string.Format(SummaryQuiet,remaining,minutes))
    +(arrival.Loot.Sites[site].Drops.All(d=>d.Chance>=100)?CertainSuffix:bonus>0?string.Format(duty==2?LightSuffix:TogetherSuffix,bonus):"");
   foreach(var drop in arrival.Loot.Sites[site].Drops){var item=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==drop.Id);var row=Instantiate(RowPrefab,Content);row.Icon.sprite=item?.Icon;row.Name.text=item?.Name??drop.Id;row.Quantity.text="×"+drop.Count;row.Chance.text=ExpeditionLootPanel.ChanceFor(drop,pace,bonus)+"%";Rows.Add(row);}
   Note.text=state.Progress>0?NoteRunning:NoteNew;
   Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
  }
 }
}
