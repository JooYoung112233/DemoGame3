using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Review-only fixtures. Saves are redirected before entering Play; no production slot is written.
public static class ReviewExplorationPolish
{
    public static string ProtectSaves()
    {
        CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/ExplorationPolishSlots");
        return "Review save directory selected; active scene unchanged.";
    }
    public static string Finish()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before ending the review.");
        if(CampaignSaveStore.TestDirectory==Path.GetFullPath("Temp/ExplorationPolishSlots"))CampaignSaveStore.TestDirectory=null;
        return "Exploration review finished in stopped Editor; review-only save override cleared.";
    }
    public static string ApplyHint()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        foreach(var file in new[]{"Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab","Assets/Prefabs/Settlement/SettlementScreen.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(file);
            try
            {
                var a=root.GetComponentInChildren<ExpeditionArrivalPanel>(true);
                var hint=a.Main.transform.Find("Hint").GetComponent<Text>();
                hint.text="사물 클릭 · 대원 카드: 가방 · Alt: 사물 이름 표시";
                PrefabUtility.SaveAsPrefabAsset(root,file);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();return "Exploration input hint updated in source and assembled prefab.";
    }
    public static async Task<string> Depart()
    {
        var c=Object.FindAnyObjectByType<SettlementController>();
        if(!c || !c.Campaign.Party.Any())throw new Exception("Use VerifyFieldBattle.Enter fixture first.");
        if(!c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")))throw new Exception("Departure failed.");
        await Task.Delay(1000);
        return await Shot("exploration-before-arcade");
    }
    public static async Task<string> Start()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play first.");
        ProtectSaves();PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");await Task.Delay(600);
        var p=Object.FindAnyObjectByType<PartySelectionController>();
        foreach(var card in p.Cards.Where(x=>x.gameObject.activeInHierarchy&&x.Button.IsInteractable()))
        {if(PartySelectionSession.Selected.Count>=2)break;card.Button.onClick.Invoke();}
        p.Continue.onClick.Invoke();await Task.Delay(600);
        var h=Object.FindAnyObjectByType<HomeSelectionController>();h.Cards[0].Button.onClick.Invoke();h.Continue.onClick.Invoke();await Task.Delay(700);
        var c=Object.FindAnyObjectByType<SettlementController>();c.Introduction.Restore(10);
        if(!c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")))throw new Exception("Departure failed.");
        await Task.Delay(900);
        return "Fresh review party in arcade; production saves untouched.";
    }
    public static async Task<string> Rooms()
    {
        var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var n=a.Rooms;
        if(n.CurrentRoom!=0)throw new Exception("Start review in arcade.");
        string report=RaycastCurrent()+"\n"+await Shot("exploration-after-arcade");
        n.AskMove();n.ConfirmMove();await Task.Delay(2350);
        if(n.CurrentRoom!=1||a.InTransit)throw new Exception("Corridor travel failed.");
        report+="\n"+RaycastCurrent()+"\n"+await Shot("exploration-after-corridor");
        c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,true);
        n.AskStorage();n.ConfirmMove();await Task.Delay(2350);
        if(n.CurrentRoom!=2||a.InTransit)throw new Exception("Storage travel failed.");
        report+="\n"+RaycastCurrent()+"\n"+await Shot("exploration-after-storage");
        n.AskMove();
        if(!a.PopupTitle.text.Contains("복도"))throw new Exception("Storage return names wrong destination.");
        n.ConfirmMove();await Task.Delay(2350);
        if(n.CurrentRoom!=1)throw new Exception("Return to corridor failed.");
        report+="\n"+await Shot("exploration-after-return-corridor");
        return report;
    }
    public static string RaycastCurrent()
    {
        var a=Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;
        Canvas.ForceUpdateCanvases();int count=0;
        foreach(var h in a.Main.GetComponentsInChildren<ExplorationHotspot>().Where(h=>h.WorldInputAllowed))
        {
            var r=(RectTransform)h.transform;var canvas=h.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var hits=new List<RaycastResult>();var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,r.TransformPoint(r.rect.center))};
            EventSystem.current.RaycastAll(data,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=h.Button)throw new Exception("Target blocked: "+h.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));
            count++;
        }
        if(count!=new[]{4,5,3}[a.Rooms.CurrentRoom])throw new Exception("Missing room targets: "+count);
        return "PASS room "+a.Rooms.CurrentRoom+": all "+count+" painted-object targets receive real UI raycasts.";
    }
    public static async Task<string> Resolutions()
    {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        var assembly=typeof(Editor).Assembly;var st=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(st);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=st.GetMethod("GetGroup",flags).Invoke(sizes,new[]{st.GetProperty("currentGroupType",flags).GetValue(sizes)});
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",flags);
        int original=(int)selected.GetValue(view);var done=new List<string>();
        try
        {
            foreach(var wh in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,1024),new Vector2Int(2560,1080)})
            {
                var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
                int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null),index=-1;
                for(int i=0;i<count;i++){var size=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});if((int)sizeType.GetProperty("width",flags).GetValue(size)==wh.x&&(int)sizeType.GetProperty("height",flags).GetValue(size)==wh.y){index=i;break;}}
                if(index<0){var size=Activator.CreateInstance(sizeType,flags,null,new object[]{Enum.ToObject(kind,1),wh.x,wh.y,"Review "+wh.x+"x"+wh.y},null);group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});index=count;}
                selected.SetValue(view,index);view.Repaint();await Task.Delay(450);Canvas.ForceUpdateCanvases();
                var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var n=a.Rooms;
                var buttons=a.Objects.Concat(new[]{n.CorridorBack,n.LockedDoor,n.OfficeDoor,n.StorageBack}).Where(b=>b&&b.IsActive()&&b.IsInteractable()).Distinct().ToArray();
                var canvas=a.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                foreach(var b in buttons)
                {
                    var r=(RectTransform)b.transform;var position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center));
                    var e=new PointerEventData(EventSystem.current){position=position};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
                    if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b)throw new Exception("Object hit blocked at "+wh+": "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
                    var point=a.Main.transform.InverseTransformPoint(r.TransformPoint(r.rect.center));
                    var world=Camera.main.WorldToScreenPoint(new Vector3(point.x*.01f-9.6f,point.y*.01f+5.4f,0));
                    if(Vector2.Distance(position,world)>2)throw new Exception("Paint/UI registration drift: "+b.name+" "+Vector2.Distance(position,world));
                }
                done.Add(wh+": "+buttons.Length+" object targets raycast and artwork registration PASS");
                await Shot("exploration-resolution-"+wh.x+"x"+wh.y);
            }
        }
        finally{selected.SetValue(view,original);view.Repaint();}
        return string.Join("\n",done)+"\nOriginal Game View size restored.";
    }
    public static async Task<string> Interaction()
    {
        var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;
        var button=a.Objects.First(b=>b&&b.IsActive());var hotspot=button.GetComponent<ExplorationHotspot>();
        if(!hotspot||!hotspot.WorldInputAllowed)throw new Exception("Missing or blocked object interaction.");
        int turns=a.Rooms.Turns,noise=a.Rooms.Noise,minute=c.Campaign.MinuteOfDay;
        hotspot.OnPointerEnter(null);await Task.Delay(80);
        if(!hotspot.IsExpanded||hotspot.Caption.alpha<.9f||hotspot.SearchStatus.alpha<.9f)throw new Exception("Hover details missing.");
        var text=hotspot.Caption.GetComponentInChildren<Text>();
        if(string.IsNullOrWhiteSpace(text.text))throw new Exception("Object name missing.");
        foreach(var label in hotspot.GetComponentsInChildren<Text>().Where(t=>t.enabled&&t.gameObject.activeInHierarchy))
            if(label.preferredHeight>label.rectTransform.rect.height+2)throw new Exception("Object label clips: "+label.name+" "+label.text);
        await Shot("exploration-after-object-hover");
        hotspot.OnPointerExit(null);hotspot.RefreshPresentation();
        a.OpenPopup("검수","입력 차단 확인");hotspot.OnPointerEnter(null);hotspot.RefreshPresentation();
        if(hotspot.WorldInputAllowed||hotspot.HitArea.raycastTarget||hotspot.IsExpanded)throw new Exception("World interaction leaks through popup.");
        a.ClosePopup();hotspot.OnPointerExit(null);hotspot.RefreshPresentation();
        if(turns!=a.Rooms.Turns||noise!=a.Rooms.Noise||minute!=c.Campaign.MinuteOfDay)throw new Exception("Hover/popup consumed a turn.");
        var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
        var evt=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        ExecuteEvents.Execute(button.gameObject,evt,ExecuteEvents.pointerClickHandler);await Task.Delay(80);
        if(!a.Search.IsOpen&&!a.Loot.IsOpen)throw new Exception("Object click did not open its search/loot.");
        if(a.Search.IsOpen)a.Search.Close();if(a.Loot.IsOpen)a.Loot.Dismiss();
        if(turns!=a.Rooms.Turns||noise!=a.Rooms.Noise||minute!=c.Campaign.MinuteOfDay)throw new Exception("Opening search consumed a turn.");
        return "PASS: hover reveals name/status, label bounds, popup blocks world input, real pointer click opens search, opening/cancel costs no time/noise.";
    }
    public static string AltAndKeyboard()
    {
        var a=Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;
        var targets=a.Main.GetComponentsInChildren<ExplorationHotspot>().Where(h=>h.WorldInputAllowed).ToArray();
        var oldSelection=EventSystem.current.currentSelectedGameObject;
        var keys=Keyboard.current.allKeys.Where(k=>k.isPressed).Select(k=>k.keyCode).ToArray();
        try
        {
            EventSystem.current.SetSelectedGameObject(targets[0].gameObject);targets[0].RefreshPresentation();
            if(!targets[0].IsExpanded)throw new Exception("Keyboard focus does not reveal details.");
            EventSystem.current.SetSelectedGameObject(null);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys.Concat(new[]{Key.LeftAlt}).Distinct().ToArray()));InputSystem.Update();
            foreach(var h in targets){h.RefreshPresentation();if(!h.IsExpanded)throw new Exception("Alt did not reveal "+h.name);}
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys.Where(k=>k!=Key.LeftAlt&&k!=Key.RightAlt).ToArray()));InputSystem.Update();
            foreach(var h in targets){h.OnPointerExit(null);h.RefreshPresentation();if(h.IsExpanded)throw new Exception("Alt release leaves detail stuck: "+h.name);}
            return "PASS: real InputSystem Alt press/release reveals and collapses all "+targets.Length+" active room targets; keyboard selection reveals detail.";
        }
        finally
        {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));InputSystem.Update();
            EventSystem.current.SetSelectedGameObject(oldSelection&&oldSelection.activeInHierarchy?oldSelection:null);
            foreach(var h in targets)h.RefreshPresentation();
        }
    }
    public static async Task<string> Shot(string name)
    {
        Canvas.ForceUpdateCanvases();
        string nativePath=Path.GetFullPath("Temp/"+name+"-native.png");
        if(File.Exists(nativePath))File.Delete(nativePath);
        ScreenCapture.CaptureScreenshot(nativePath);
        for(int i=0;i<30&&!File.Exists(nativePath);i++)await Task.Delay(100);
        if(!File.Exists(nativePath))throw new Exception("Unity did not finish its frame capture.");
        await Task.Delay(150);
        var source=new Texture2D(2,2,TextureFormat.RGB24,false);ImageConversion.LoadImage(source,File.ReadAllBytes(nativePath));
        var rt=new RenderTexture(1440,Mathf.RoundToInt(1440f*Screen.height/Screen.width),0);
        var previous=RenderTexture.active;
        try
        {
            Graphics.Blit(source,rt);
            RenderTexture.active=rt;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
            string path="아트/리소스검토/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.Destroy(texture);
            return Path.GetFullPath(path)+" · screen "+Screen.width+"x"+Screen.height;
        }
        finally{RenderTexture.active=previous;Object.Destroy(source);Object.Destroy(rt);}
    }
    public static string Inspect()
    {
        var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;
        return "Camera "+Camera.main.orthographicSize+" / "+Camera.main.aspect+"\n"+
            string.Join("\n",a.World.GetComponentsInChildren<Component>(true).Where(x=>x is MonoBehaviour).Select(x=>x.name+": "+x.GetType().Name))+"\n"+
            string.Join("\n",a.Main.GetComponentsInChildren<UnityEngine.UI.Button>(true).Select(b=>b.name+" "+((RectTransform)b.transform).anchoredPosition+" / "+((RectTransform)b.transform).sizeDelta));
    }
    public static string Previous()
    {
        var source=new Texture2D(2,2);ImageConversion.LoadImage(source,File.ReadAllBytes("Assets/Screenshots/UIAudit/25-field.png"));
        var rt=new RenderTexture(1440,Mathf.RoundToInt(1440f*source.height/source.width),0);var previous=RenderTexture.active;
        Graphics.Blit(source,rt);RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();
        const string path="아트/리소스검토/exploration-historical-reference.png";File.WriteAllBytes(path,t.EncodeToPNG());RenderTexture.active=previous;Object.DestroyImmediate(source);Object.DestroyImmediate(t);Object.DestroyImmediate(rt);return path;
    }
}
