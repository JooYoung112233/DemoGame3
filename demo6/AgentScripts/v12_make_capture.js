const fs=require('fs');
let s=fs.readFileSync('AgentScripts/V11Compare.cs','utf8').split(' const string Art=')[0].replaceAll('V11Compare','V12Capture').replaceAll('화풍조화-교정-v11','승인형상-대표샘플-v12');
s+=String.raw`
 const string Art="E:/personalProject/Demo3/demo6/아트/승인형상-대표샘플-v12/PNG/";
 static Texture2D Load(string id){var t=new Texture2D(2,2,TextureFormat.RGBA32,true);t.LoadImage(File.ReadAllBytes(Art+id+".png"));t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Clamp;return t;}
 public static async Task<object> Capture(){
  Guard();if(System.Diagnostics.Process.GetCurrentProcess().Id!=int.Parse(File.ReadAllText(Root+"/review-pid.txt")))throw new Exception("Owned sample session only");
  var p=PlayerController.Instance;var origin=p.Position;bool paused=EditorApplication.isPaused;var rows=new List<object>();
  var groundTex=Load("connected-bedrock");var heroTex=Load("hero-approved-shape");Sprite ground=null,hero=null;GameObject study=null;
  try{using(var input=new InputScope()){
   Application.runInBackground=true;input.Aim=Vector2.up;
   foreach(int height in new[]{720,1080})using(var size=new SizeScope(height==720?1280:1920,height)){
    int width=height==720?1280:1920;EditorApplication.isPaused=false;p.Teleport(new Vector2(-5,20));Camera.main.GetComponent<DungeonCamera>().Snap();await Task.Delay(1600);
    EditorApplication.isPaused=true;int frame=Time.frameCount;string lights=LightState(),walls=WallState();
    var floor=UnityEngine.Object.FindObjectsByType<SpriteRenderer>().Single(r=>r.sprite&&AssetDatabase.GetAssetPath(r.sprite).EndsWith("TopDownPilotV9/entry_floor.png")&&r.bounds.Contains(new Vector3(-5,20,r.bounds.center.z)));
    var oldFloor=floor.sprite;var oldBounds=floor.bounds;
    var renderers=p.GetComponentsInChildren<Renderer>().Where(r=>r.name!="Shadow").ToArray();var enabled=renderers.Select(r=>r.enabled).ToArray();
    var body=p.GetComponent<SpriteRenderer>();if(!body)body=p.GetComponentsInChildren<SpriteRenderer>().First(r=>r.sprite&&AssetDatabase.GetAssetPath(r.sprite).Contains("body_leather"));
    string folder=Root+"/sample/"+height;Directory.CreateDirectory(folder);
    Shot(folder+"/before-world.png",width,height);ScreenCapture.CaptureScreenshot(folder+"/before-game.png");await Task.Delay(500);
    try{
     if(!ground)ground=Sprite.Create(groundTex,new Rect(16,0,2016,1152),new Vector2(.5f,.5f),72f,0,SpriteMeshType.FullRect);
     floor.sprite=ground;
     float ppu=8f*1080f/(2f*Camera.main.orthographicSize);
     if(!hero)hero=Sprite.Create(heroTex,new Rect(0,0,heroTex.width,heroTex.height),new Vector2(472f/1280f,772f/1536f),ppu,0,SpriteMeshType.FullRect);
     study=new GameObject("V12 static design sample - never saved");study.transform.position=p.transform.position;var sr=study.AddComponent<SpriteRenderer>();sr.sprite=hero;sr.sharedMaterial=body.sharedMaterial;sr.color=body.color;sr.sortingLayerID=body.sortingLayerID;sr.sortingOrder=body.sortingOrder+5;
     for(int i=0;i<renderers.Length;i++)renderers[i].enabled=false;
     Shot(folder+"/sample-world.png",width,height);ScreenCapture.CaptureScreenshot(folder+"/sample-game.png");await Task.Delay(600);
     rows.Add(new{height,frameStart=frame,frameEnd=Time.frameCount,pose=p.Pose.ToString(),position=p.Position.ToString(),lightsIdentical=lights==LightState(),collidersIdentical=walls==WallState(),floorBoundsBefore=oldBounds.ToString(),floorBoundsAfter=floor.bounds.ToString(),floorBoundsIdentical=oldBounds==floor.bounds,heroPpu=ppu,cameraSize=Camera.main.orthographicSize,staticArtProxy=true,originalGameFilesModified=false});
    }finally{
     floor.sprite=oldFloor;for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];if(study)UnityEngine.Object.DestroyImmediate(study);study=null;
    }
    EditorApplication.isPaused=false;
   }
  }}finally{
   p.Teleport(origin);Camera.main.GetComponent<DungeonCamera>().Snap();EditorApplication.isPaused=paused;
   if(study)UnityEngine.Object.DestroyImmediate(study);if(ground)UnityEngine.Object.DestroyImmediate(ground);if(hero)UnityEngine.Object.DestroyImmediate(hero);UnityEngine.Object.DestroyImmediate(groundTex);UnityEngine.Object.DestroyImmediate(heroTex);
  }
  File.WriteAllText(Root+"/sample/capture-checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));return rows;
 }
}
`;
fs.writeFileSync('AgentScripts/V12Capture.cs',s);
