using System;
using System.Collections.Generic;
using System.Reflection;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// No scene, save or navigation mutation: inactive game components and disposable active UI targets.
public static class VerifyTutorialRoomReturn
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void Call(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    static void Room(ExpeditionRoomNavigation rooms,int room)=>typeof(ExpeditionRoomNavigation).GetProperty("CurrentRoom").SetValue(rooms,room);
    static GameObject Child(Transform parent,string name,bool active=true){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);g.SetActive(active);return g;}
    static void Refresh(SettlementTutorialGuide guide){Call(guide,"Clear");Call(guide,"Field");}
    static void Place(RectTransform rect,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
    static Rect Bounds(RectTransform space,RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);foreach(var corner in corners){Vector2 point=space.InverseTransformPoint(corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
    static Rect Union(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));
    static bool Near(Vector2 a,Vector2 b)=>Vector2.Distance(a,b)<.05f;

    public static string Run()
    {
        var root=new GameObject("Tutorial room fixture");root.SetActive(false);
        var ui=new GameObject("Tutorial target fixture",typeof(RectTransform));
        ((RectTransform)ui.transform).sizeDelta=new Vector2(1920,1080);
        try
        {
            var owner=root.AddComponent<SettlementController>();var a=root.AddComponent<ExpeditionArrivalPanel>();
            var g=root.AddComponent<SettlementTutorialGuide>();var rooms=root.AddComponent<ExpeditionRoomNavigation>();
            owner.ArrivalPanel=a;owner.Opening=root.AddComponent<SettlementOpeningChapter>();owner.Opening.State.Enabled=true;
            owner.InventoryPanel=root.AddComponent<SettlementInventoryPanel>();owner.PackingPanel=root.AddComponent<ExpeditionPackingPanel>();owner.PackingPanel.View=Child(root.transform,"Packing",false);
            a.Rooms=rooms;a.Loot=root.AddComponent<ExpeditionLootPanel>();a.Loot.View=Child(root.transform,"Loot",false);
            a.Search=root.AddComponent<ExpeditionSearchPanel>();a.Search.View=Child(root.transform,"Search",false);
            a.FieldBags=root.AddComponent<ExpeditionBagPanel>();a.FieldBags.View=Child(root.transform,"Bags",false);
            a.Encounter=root.AddComponent<ExpeditionEncounterPanel>();a.Encounter.View=Child(root.transform,"Encounter",false);
            a.Encounter.Battle=root.AddComponent<ExpeditionBattlePanel>();a.Encounter.Battle.View=Child(root.transform,"Battle",false);
            a.Popup=Child(root.transform,"Popup",false);
            a.View=Child(root.transform,"Arrival");a.Main=Child(root.transform,"Main").AddComponent<CanvasGroup>();
            a.PopupBack=Child(ui.transform,"PopupBack").AddComponent<Button>();a.ReturnConfirm=Child(ui.transform,"ReturnConfirm").AddComponent<Button>();
            a.Objects=new[]{Child(ui.transform,"ArcadeCrate").AddComponent<Button>()};a.Return=Child(ui.transform,"HomeExit").AddComponent<Button>();
            rooms.CorridorBack=Child(ui.transform,"CorridorToArcade").AddComponent<Button>();rooms.StorageBack=Child(ui.transform,"StorageToCorridor").AddComponent<Button>();
            g.Owner=owner;g.Banner=Child(ui.transform,"Banner",false);g.Title=Child(g.Banner.transform,"Title").AddComponent<Text>();g.Instruction=Child(g.Banner.transform,"Instruction").AddComponent<Text>();
            g.Marker=Child(ui.transform,"Marker",false).AddComponent<TutorialTargetGraphic>();
            g.Spotlight=Child(ui.transform,"Spotlight",false).AddComponent<TutorialSpotlight>();g.Spotlight.rectTransform.sizeDelta=new Vector2(1920,1080);
            g.Pointer=Child(ui.transform,"Pointer",false).AddComponent<TutorialPointer>();
            foreach(var door in new[]{rooms.CorridorBack,rooms.StorageBack})
            {
                door.gameObject.SetActive(false);Place((RectTransform)door.transform,new Vector2(220,-120),new Vector2(330,620));
                var hot=door.gameObject.AddComponent<ExplorationHotspot>();hot.Arrival=a;hot.Button=door;hot.IsDoor=true;
                hot.HitArea=door.gameObject.AddComponent<Image>();hot.HitArea.color=Color.clear;
                hot.Marker=Child(door.transform,"ExplorationMarkerPaper").AddComponent<Image>();Place(hot.Marker.rectTransform,new Vector2(80,-340),new Vector2(40,40));
                hot.Caption=Child(door.transform,"Caption").AddComponent<CanvasGroup>();Place((RectTransform)hot.Caption.transform,new Vector2(26,-387),new Vector2(148,34));
                door.gameObject.SetActive(true);
            }
            ((RectTransform)g.Banner.transform).sizeDelta=new Vector2(650,140);g.Title.fontSize=32;g.Instruction.fontSize=24;
            g.Title.rectTransform.sizeDelta=new Vector2(602,42);g.Instruction.rectTransform.sizeDelta=new Vector2(602,72);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementTutorialGuide>();
            g.Title.font=source.Title.font;g.Instruction.font=source.Instruction.font;
            string openingBefore=JsonUtility.ToJson(owner.Opening.State);var state=a.Loot.State(0);
            foreach(bool complete in new[]{false,true})
            {
                state.Complete=complete;state.Progress=complete?1:0;state.Required=1;
                foreach(int room in new[]{2,1,0})
                {
                    Room(rooms,room);a.Objects[0].gameObject.SetActive(room==0);a.Return.gameObject.SetActive(room==0);
                    rooms.CorridorBack.gameObject.SetActive(room==1);rooms.StorageBack.gameObject.SetActive(room==2);
                    Refresh(g);var target=room==2?rooms.StorageBack:room==1?rooms.CorridorBack:complete?a.Return:a.Objects[0];
                    Check(g.Target==target&&g.Target.IsActive()&&g.Target.IsInteractable()&&g.Banner.activeSelf&&g.Marker.gameObject.activeSelf,"Wrong or unavailable tutorial target in room "+room+", complete="+complete);
                    string expected=room==2?"복도로 돌아가는 문을 누르세요. 다음은 오락실입니다.":room==1?(complete?"오락실로 돌아가는 문을 누르세요. 출구에서 거점으로 귀환합니다.":"오락실로 돌아가는 문을 누르세요. 입구 상자는 오락실에 있습니다."):(complete?"챙긴 물건을 가지고 ‘거점으로 귀환’을 누르세요.":"입구의 상자를 눌러 담당자를 선택하세요.");
                    Check(g.Instruction.text==expected,"Wrong room-specific direction: "+g.Instruction.text);
                    var banner=(RectTransform)g.Banner.transform;
                    Check(banner.anchoredPosition==new Vector2(80,-776)&&banner.sizeDelta==new Vector2(480,148),"Field guide does not fit the existing lower-left paper.");
                    Check(g.Title.fontSize==26&&g.Instruction.fontSize==22&&g.Title.rectTransform.sizeDelta==new Vector2(432,40)&&g.Instruction.rectTransform.sizeDelta==new Vector2(432,78),"Field guide text geometry is wrong.");
                    Check(g.Title.preferredHeight<=40.1f&&g.Instruction.preferredHeight<=78.1f,"Field guide text overflows.");
                    if(room!=0)
                    {
                        var hot=target.GetComponent<ExplorationHotspot>();var space=(RectTransform)ui.transform;
                        var mark=Bounds(space,hot.Marker.rectTransform);var caption=Bounds(space,(RectTransform)hot.Caption.transform);var display=Union(mark,caption);var frame=Bounds(space,g.Marker.rectTransform);
                        Check(Near(frame.center,display.center)&&frame.width>=display.width+11.9f&&frame.width<=display.width+12+g.PulseGrow+.1f&&frame.height>=display.height+11.9f&&frame.height<=display.height+12+g.PulseGrow+.1f,"Door frame followed full hit area instead of marker+caption.");
                        var holes=(List<Rect>)typeof(TutorialSpotlight).GetField("holes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g.Spotlight);
                        var holeDisplay=Union(Bounds(g.Spotlight.rectTransform,hot.Marker.rectTransform),Bounds(g.Spotlight.rectTransform,(RectTransform)hot.Caption.transform));
                        Check(holes.Count>=1&&Near(holes[0].center,holeDisplay.center)&&Near(holes[0].size,holeDisplay.size+Vector2.one*g.HolePadding*2),"Door spotlight still follows full hit area.");
                        var arrow=Bounds(space,g.Pointer.rectTransform);float gap=arrow.yMin-display.yMax;
                        Check(Mathf.Abs(arrow.center.x-display.center.x)<.05f&&gap>=g.PointerGap-.1f&&gap<=g.PointerGap+g.PointerBounce+.1f&&arrow.yMax<space.rect.yMax-140,"Door arrow no longer follows small display or overlaps upper HUD.");
                        Check(((RectTransform)target.transform).sizeDelta==new Vector2(330,620)&&hot.HitArea.raycastTarget&&g.Target==target,"Tutorial changed door click target/hit area.");
                        target.gameObject.SetActive(false);Refresh(g);Check(!g.Target&&!g.Banner.activeSelf&&!g.Marker.gameObject.activeSelf,"Inactive room door still showed guidance.");
                        target.gameObject.SetActive(true);target.interactable=false;Refresh(g);Check(!g.Target&&!g.Banner.activeSelf&&!g.Marker.gameObject.activeSelf,"Disabled room door still showed guidance.");target.interactable=true;
                    }
                    else
                    {
                        var display=Bounds((RectTransform)ui.transform,(RectTransform)target.transform);var frame=Bounds((RectTransform)ui.transform,g.Marker.rectTransform);
                        Check(Near(frame.center,display.center)&&frame.width>=display.width+11.9f&&frame.width<=display.width+12+g.PulseGrow+.1f,"Ordinary target no longer uses its original bounds.");
                    }
                    Check(state.Complete==complete&&state.Progress==(complete?1:0)&&rooms.CurrentRoom==room&&rooms.Turns==0&&rooms.Noise==0,"Guidance changed progress, location or cost.");
                }
            }
            a.Popup.SetActive(true);Refresh(g);
            Check(g.Target==a.ReturnConfirm&&((RectTransform)g.Banner.transform).anchoredPosition==g.HeaderPosition&&((RectTransform)g.Banner.transform).sizeDelta==new Vector2(650,140),"Popup failed to restore original banner geometry.");
            Check(g.Title.fontSize==32&&g.Instruction.fontSize==24&&g.Title.rectTransform.sizeDelta==new Vector2(602,42)&&g.Instruction.rectTransform.sizeDelta==new Vector2(602,72),"Popup failed to restore original text layout/fonts.");a.Popup.SetActive(false);
            Check(JsonUtility.ToJson(owner.Opening.State)==openingBefore,"Guidance changed opening/save progression.");
            owner.Opening.State.FirstReturn=true;Refresh(g);Check(!g.Target&&!g.Banner.activeSelf,"First-trip guide returned after first return.");
            return "PASS: first-search and completed-search guidance targets storage→corridor→arcade one active door at a time; door frame/spotlight/arrow follow marker+caption while full click area stays intact; inactive/disabled doors hide guidance; field guide fits lower-left 480×148 paper with 26/22 text and popup restores original geometry/fonts; movement freedom, turns/noise and opening/search progression unchanged.";
        }
        finally{Object.DestroyImmediate(root);Object.DestroyImmediate(ui);}
    }
}
