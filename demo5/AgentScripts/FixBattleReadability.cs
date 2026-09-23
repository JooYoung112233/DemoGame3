using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Demo5.FrontEnd;
public static class FixBattleReadability {
 const string Dir="Assets/Prefabs/Settlement/";
 static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void Label(Text t,float x,float y,float w,float h,int size){Rect(t.transform,x,y,w,h);t.fontSize=size;t.alignment=TextAnchor.MiddleLeft;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;t.resizeTextForBestFit=false;}
 static void Tip(BattleAimTooltip t){
 t.Root.sizeDelta=new Vector2(350,278);t.AimHeight=278;t.HoverHeight=158;
 Label(t.Title,16,8,214,40,25);Rect(t.Armor.transform.parent,242,12,92,34);
 Label(t.Health,16,46,318,36,20);
 Rect(t.Root.Find("HealthBack"),16,85,318,18);Rect(t.HealthLost.transform,18,87,314,14);Rect(t.HealthAfter.transform,18,87,314,14);
 Rect(t.AttackBlock.transform,0,110,350,160);
 Label(t.Chance,16,0,318,48,30);Label(t.Factors,16,49,318,30,17);Label(t.Damage,16,80,318,34,21);Label(t.Note,16,116,318,36,18);
 Rect(t.IntentBlock.transform,0,111,350,38);Label(t.Intent,16,0,318,36,20);
 }
 static Vector2 V(int side,int c,int r){var p=ExpeditionBattlePanel.BoardVertex(side,c,r);return new Vector2(p.x,-p.y);}
 public static string Run(){
 foreach(var name in new[]{"BattleAimTooltip","ExpeditionBattlePanel"}){
 var path=Dir+name+".prefab";var go=PrefabUtility.LoadPrefabContents(path);
 try {foreach(var t in go.GetComponentsInChildren<BattleAimTooltip>(true))Tip(t);
 var b=go.GetComponent<ExpeditionBattlePanel>();if(b){for(int side=0;side<2;side++)for(int lane=0;lane<3;lane++)for(int depth=0;depth<3;depth++){
 var c=b.Cells[side*9+lane*3+depth];int col=side==0?2-depth:depth;c.TopLeft=V(side,col,lane);c.TopRight=V(side,col+1,lane);c.BottomLeft=V(side,col,lane+1);c.BottomRight=V(side,col+1,lane+1);c.Thickness=1.4f;
 }}PrefabUtility.SaveAsPrefabAsset(go,path);
 }finally{PrefabUtility.UnloadPrefabContents(go);}}
 AssetDatabase.SaveAssets();return "Saved calibrated 18 floor cells and readable aim/hover card prefabs.";
 }
}
