const fs=require('fs'),c=require('crypto');const root='E:/personalProject/Demo3/demo6',ev=root+'/검증/정수리-질감수정-v10';const path='Assets/Scripts/Game/Dungeon/UI/DungeonUi.cs';const report=JSON.parse(fs.readFileSync(ev+'/applied-files.json','utf8'));const entry=report.files.find(x=>x.path===path);const hash=b=>c.createHash('sha256').update(b).digest('hex');const before=fs.readFileSync(root+'/'+path,'utf8');if(hash(before)!==entry.after)throw Error('Concurrent UI change preserved');const start=before.indexOf('        public static void ShadowLabel('),end=before.indexOf('\n        /// <summary>',start);if(start<0||end<0)throw Error('Method not found');const method=`        public static void ShadowLabel(Rect r, string text, GUIStyle style, Color color, float shadow = 0.85f)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text) || color.a <= 0.002f) return;
            var prev = GUI.color;
            var content = new GUIContent(text);
            GUI.color = new Color(0f, 0f, 0f, color.a * shadow);
            style.Draw(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), content, false, false, false, false);
            GUI.color = color;
            style.Draw(r, content, false, false, false, false);
            GUI.color = prev;
        }
`;
const after=before.slice(0,start)+method+before.slice(end);fs.writeFileSync(root+'/'+path,after);entry.after=hash(after);entry.bytes=Buffer.byteLength(after);fs.writeFileSync(ev+'/applied-files.json',JSON.stringify(report,null,2));fs.writeFileSync(ev+'/passive-text-draw-applied.json',JSON.stringify({path,before:hash(before),after:hash(after)},null,2));
