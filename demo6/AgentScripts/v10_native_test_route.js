const fs=require('fs');const p=__dirname+'/V10UiChecks.cs';let s=fs.readFileSync(p,'utf8');
const old=s.slice(s.indexOf(' static void QueueClick('),s.indexOf(' static async Task Click('));
const replacement=` delegate bool EnumProc(IntPtr h,IntPtr p);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool EnumWindows(EnumProc cb,IntPtr p);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,EnumProc cb,IntPtr p);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out NativeBounds r);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct NativeBounds{public int x,y,r,b;}
 static void QueueClick(EventType type,Vector2 source){Guard();var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var v=EditorWindow.GetWindow(t);var r=(Rect)t.GetProperty("targetInParent",F).GetValue(v);var at=r.position+Vector2.Scale(source,new Vector2(r.width/Screen.width,r.height/Screen.height));IntPtr target=IntPtr.Zero;uint own=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;EnumWindows((h,p)=>{GetWindowThreadProcessId(h,out uint id);if(id==own)EnumChildWindows(h,(child,a)=>{GetWindowRect(child,out var b);if(Math.Abs(b.x-v.position.x)<3&&Math.Abs(b.y-v.position.y)<3)target=child;return true;},p);return true;},IntPtr.Zero);if(target==IntPtr.Zero)throw new Exception("Owned GameView not found");var l=(IntPtr)((int)at.x|((int)at.y<<16));if(type==EventType.MouseDown)PostMessage(target,0x200,IntPtr.Zero,l);PostMessage(target,type==EventType.MouseDown?0x201u:0x202u,type==EventType.MouseDown?(IntPtr)1:IntPtr.Zero,l);}
`;
if(!old)throw Error('QueueClick missing');s=s.replace(old,replacement);fs.writeFileSync(p,s);
