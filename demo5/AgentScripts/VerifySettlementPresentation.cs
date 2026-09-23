using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Demo5.FrontEnd;

public static class VerifySettlementPresentation
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    public static string Standard()=>Resolution(1920,1080);
    public static string Narrow()=>Resolution(1280,1024);
    public static string Wide()=>Resolution(2560,1080);
    static string Resolution(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var groupType=sizesType.GetProperty("currentGroupType",Flags).GetValue(sizes);
        var group=sizesType.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{groupType});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,Flags,null,new object[]{Enum.ToObject(kind,1),width,height,"Review "+width+"x"+height},null);
        var total=(int)group.GetType().GetMethod("GetTotalCount",Flags).Invoke(group,null);
        group.GetType().GetMethod("AddCustomSize",Flags).Invoke(group,new[]{size});
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,total);
        view.Repaint();
        return "Game View set to "+width+"x"+height;
    }
    public static string CheckFrame()
    {
        var camera=Camera.main;
        float expected=Mathf.Max(5.4f,9.6f/((float)Screen.width/Screen.height));
        if(Mathf.Abs(camera.orthographicSize-expected)>.01f)throw new Exception("World frame mismatch");
        var owner=UnityEngine.Object.FindAnyObjectByType<SettlementController>();
        Canvas.ForceUpdateCanvases();
        foreach(var b in new[]{owner.Bed,owner.Workbench,owner.Cabinet,owner.Exit,owner.Advance})
        {
            var rect=(RectTransform)b.transform;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            foreach(var point in corners)
            {
                var s=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,point);
                if(s.x < -1 || s.x>Screen.width+1 || s.y < -1 || s.y>Screen.height+1)throw new Exception("Offscreen "+b.name);
            }
        }
        return "PASS "+Screen.width+"x"+Screen.height+" world frame and facility bounds";
    }
    public static async Task<string> Movement()
    {
        var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();
        var m=UnityEngine.Object.FindAnyObjectByType<SettlementPawnMotion>();
        var p=c.CraftPanel;
        int minute=c.Campaign.MinuteOfDay,supply=c.Campaign.Supplies;
        var home=m.Pawns[0].position;var other=m.Pawns[1].position;
        p.Open();p.RecipeRows[1].Button.onClick.Invoke();p.WorkerRows[0].Button.onClick.Invoke();
        if(!p.Confirm.interactable)throw new Exception("Cannot assign fixture");
        p.Confirm.onClick.Invoke();await Task.Delay(100);
        if(Mathf.Abs(m.Pawns[0].position.x-home.x)>.02f && Mathf.Abs(m.Bases[0].position.y-m.AisleY)>.02f)throw new Exception("Initial path cuts diagonally");
        for(int i=0;i<200&&m.IsMoving;i++)await Task.Delay(100);
        if(m.IsMoving||Vector3.Distance(m.Bases[0].position,m.WorkPositions[0].position)>.02f)throw new Exception("Wrong work destination");
        if(m.Pawns[1].position!=other||minute!=c.Campaign.MinuteOfDay||supply!=c.Campaign.Supplies)throw new Exception("Presentation changed state");
        p.Open();p.OrderRows[0].Cancel.onClick.Invoke();p.CancelYes.onClick.Invoke();p.Close();await Task.Delay(100);
        for(int i=0;i<200&&m.IsMoving;i++)await Task.Delay(100);
        if(m.IsMoving||Vector3.Distance(m.Pawns[0].position,home)>.02f)throw new Exception("Return failed");
        return "PASS aisle approach, work arrival, cancel return, other pawn/time/supplies unchanged";
    }
}
