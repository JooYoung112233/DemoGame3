const fs=require('fs');
let s=fs.readFileSync('AgentScripts/V11Compare.cs','utf8').split(' const string Art=')[0].replaceAll('V11Compare','V11Inspect');
s+=String.raw`
 public static async Task<object> Capture(){
  Guard();var p=PlayerController.Instance;var old=p.Position;bool paused=EditorApplication.isPaused;var rows=new List<object>();
  try{using(var input=new InputScope())using(var size=new SizeScope(1920,1080)){
   Application.runInBackground=true;EditorApplication.isPaused=false;input.Aim=Vector2.up;await Task.Delay(1200);
   foreach(var spot in new[]{new{name="entry",at=new Vector2(-6,16)},new{name="wall",at=new Vector2(-5,21)}}){
    p.Teleport(spot.at);Camera.main.GetComponent<DungeonCamera>().Snap();await Task.Delay(900);
    string dir=Root+"/diagnosis";Directory.CreateDirectory(dir);Shot(dir+"/current-"+spot.name+"-world.png",1920,1080);
    ScreenCapture.CaptureScreenshot(dir+"/current-"+spot.name+"-game.png");await Task.Delay(500);
    rows.Add(new{spot=spot.name,pose=p.Pose.ToString(),position=p.Position.ToString(),camera=Camera.main.orthographicSize});
   }
  }}finally{p.Teleport(old);Camera.main.GetComponent<DungeonCamera>().Snap();EditorApplication.isPaused=paused;}
  File.WriteAllText(Root+"/diagnosis/capture.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));return rows;
 }
}
`;
fs.writeFileSync('AgentScripts/V11Inspect.cs',s);
