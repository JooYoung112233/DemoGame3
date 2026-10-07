using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Demo6.Game;
using Demo6.Core.Town;
using Newtonsoft.Json;

public static class TownV058InputReview
{
    const string Out="검증/town-v058";
    static void Image(string name)
    {
        var cam=Camera.main;var target=cam.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(Screen.width,Screen.height,24);var tex=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);tex.Apply();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());}
        finally{cam.targetTexture=target;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    public static async Task<object> Run()
    {
        var root=TownRoot.Instance;if(!root||GameSession.FromTitle)throw new Exception("Unsaved town Play only");
        var player=root.Player;var old=player.Position;var realKeyboard=Keyboard.current;var realMouse=Mouse.current;
        var inputMap=(InputActionMap)typeof(PlayerInputReader).GetField("_map",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(player.GetComponent<PlayerInputReader>());
        var originalDevices=inputMap.devices;
        Keyboard keyboard=null;Mouse mouse=null;var rows=new List<object>();var inputs=new List<object>();
        try{
            // Only this transient test player's map is scoped; restore its device list in finally.
            player.Teleport(TownModularArtV058.FromPixel(900,680));await Task.Delay(250);
            foreach(var talk in UnityEngine.Object.FindObjectsByType<TalkWindow>())if(talk.IsOpen)talk.SkipAll();
            await Task.Delay(250);
            keyboard=InputSystem.AddDevice<Keyboard>("TownReviewKeyboard");mouse=InputSystem.AddDevice<Mouse>("TownReviewMouse");
            inputMap.devices=new InputDevice[]{keyboard,mouse};
            var dirs=new[]{Vector2.up,new Vector2(1,1).normalized,Vector2.right,new Vector2(1,-1).normalized,Vector2.down,new Vector2(-1,-1).normalized,Vector2.left,new Vector2(-1,1).normalized};
            for(int i=0;i<dirs.Length;i++){
                var origin=TownModularArtV058.FromPixel(900,680);player.Teleport(origin);root.CameraRig.Snap();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(100);
                var keys=new List<Key>();if(dirs[i].x>.1f)keys.Add(Key.D);if(dirs[i].x<-.1f)keys.Add(Key.A);if(dirs[i].y>.1f)keys.Add(Key.W);if(dirs[i].y<-.1f)keys.Add(Key.S);
                var point=Camera.main.WorldToScreenPoint(origin+dirs[i]*4f);InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y)});
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));await Task.Delay(220);
                var end=player.Position;float distance=Vector2.Distance(origin,end);var pose=player.Pose.ToString();
                Image("walk-"+i);rows.Add(new{direction=i,pose,distance,moved=distance>.2f,facingDot=Vector2.Dot(player.FacingDirection,dirs[i]),modal=DungeonUi.Modal});
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(180);
            }
            // F uses the same mapped action as native keyboard input, with no facility method invoked directly.
            foreach(var spot in new[]{TownLayout.Anvil,TownLayout.Board,TownLayout.Gate}){
                player.Teleport(new Vector2(spot.Pos.X,spot.Pos.Y-.85f));root.CameraRig.Snap();await Task.Delay(200);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));await Task.Delay(200);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(100);
                inputs.Add(new{id=spot.Id,modal=DungeonUi.Modal,opened=DungeonUi.ModalOpen});
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Out+"/interaction-"+spot.Id+".png"));await Task.Delay(100);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Task.Delay(120);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(120);
                foreach(var talk in UnityEngine.Object.FindObjectsByType<TalkWindow>())if(talk.IsOpen)talk.SkipAll();
            }
        }
        finally{
            inputMap.devices=originalDevices;
            if(keyboard!=null&&keyboard.added){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);}
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(realKeyboard!=null&&realKeyboard.added)realKeyboard.MakeCurrent();if(realMouse!=null&&realMouse.added)realMouse.MakeCurrent();
            if(player){player.Teleport(old);root.CameraRig.Snap();}
        }
        var result=new{kind="Temporary synthetic keyboard/mouse action test; NOT native user validation",rows,inputs,
            deviceScopeRestored=Nullable.Equals(inputMap.devices,originalDevices),devices=InputSystem.devices.Select(d=>new{d.name,d.enabled}).ToArray(),combat="Town intentionally locks attack, dodge and guard; these systems were not modified"};
        File.WriteAllText(Out+"/input-validation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}
