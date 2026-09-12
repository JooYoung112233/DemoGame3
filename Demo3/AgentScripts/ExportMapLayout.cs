using System.IO;
using Live49.UI;
using UnityEngine;
public static class ExportMapLayout
{
    public static string Run()
    {
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/ui/city-map-v1"));Directory.CreateDirectory(folder);
        var root=new GameObject("MapLayoutExport",typeof(RectTransform),typeof(Canvas));
        var cameraObject=new GameObject("MapLayoutExportCamera",typeof(Camera));
        var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=396;
        camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        camera.cullingMask=1<<30;root.layer=30;
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        var rt=(RectTransform)root.transform;rt.sizeDelta=new Vector2(1220,792);
        var graphic=root.AddComponent<MapPaperGraphic>();graphic.Current=MapPaperGraphic.Camp;graphic.Selected=null;
        var target=new RenderTexture(3660,2376,24);camera.targetTexture=target;
        var previous=RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(3660,2376,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,3660,2376),0,0);texture.Apply();
            var file=Path.Combine(folder,"layout-reference.png");File.WriteAllBytes(file,texture.EncodeToPNG());
            Object.DestroyImmediate(texture);return file;
        }
        finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);}
    }
}
