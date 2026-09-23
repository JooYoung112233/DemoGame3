using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class VerifyBattleReadability {
 public static string Release(){var b=Object.FindAnyObjectByType<ExpeditionBattlePanel>(FindObjectsInactive.Include);b.ScriptedPointer=false;b.Escape();return "Manual pointer restored.";}
 public static string Slots(){
 var go=new GameObject("HealthSlotVerification",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
 try {
 ((RectTransform)go.transform).sizeDelta=new Vector2(160,16);var source=go.GetComponent<Image>();
 foreach(var values in new[]{new[]{0,4,0},new[]{4,4,4},new[]{3,4,1},new[]{0,0,0}}){
 SegmentedHealthGraphic.Set(source,values[0],values[1],values[2]);var slots=go.GetComponentInChildren<SegmentedHealthGraphic>();
 using(var mesh=new VertexHelper()){
 typeof(SegmentedHealthGraphic).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly).Invoke(slots,new object[]{mesh});
 if(mesh.currentVertCount!=values[1]*4||slots.Current!=values[0]||slots.After!=values[2])throw new System.Exception("Health slots mismatch");}
 }
 return "PASS: empty/full/partial/predicted damage/zero maximum each render the exact number of HP slots.";
 }finally{Object.DestroyImmediate(go);}
 }
 public static async Task<string> Aim(){
 var b=Object.FindAnyObjectByType<ExpeditionBattlePanel>(FindObjectsInactive.Include);
 b.Shoot.onClick.Invoke();b.ScriptedPointer=true;
 int target=b.State.Units.FindIndex(u=>u.Enemy&&u.Alive);b.ScriptedPointerPosition=b.AimPoint(target);
 await Task.Delay(600);Canvas.ForceUpdateCanvases();
 return string.Join("\n",b.AimTooltip.GetComponentsInChildren<Text>(true).Select(t=>t.name+" = "+t.text+" height="+t.rectTransform.rect.height+" preferred="+t.preferredHeight))+"\nSlots="+Object.FindObjectsByType<SegmentedHealthGraphic>(FindObjectsInactive.Include).Length;
 }
}


