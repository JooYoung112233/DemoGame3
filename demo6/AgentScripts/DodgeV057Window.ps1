Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Text;using System.Runtime.InteropServices;using System.Collections.Generic;
public static class DodgeWindow {
 public delegate bool Callback(IntPtr h,IntPtr p);
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int left,top,right,bottom;}
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")]static extern bool EnumWindows(Callback f,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,Callback f,IntPtr p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 public static string[] Read(){var a=new List<string>();EnumWindows((h,p)=>{uint id;GetWindowThreadProcessId(h,out id);if(id==219740){var b=new StringBuilder(500);GetWindowText(h,b,500);a.Add(h+" | "+b);EnumChildWindows(h,(ch,cp)=>{var t=new StringBuilder(500);GetWindowText(ch,t,500);if(t.Length>0)a.Add("  "+ch+" | "+t);return true;},IntPtr.Zero);}return true;},IntPtr.Zero);return a.ToArray();}
}
'@
[DodgeWindow]::Read()
$targetHandle = [IntPtr]591256
$rect = New-Object DodgeWindow+Rect
[DodgeWindow]::GetWindowRect($targetHandle,[ref]$rect) | Out-Null
$shot = New-Object System.Drawing.Bitmap ($rect.right-$rect.left),($rect.bottom-$rect.top)
$graphics = [System.Drawing.Graphics]::FromImage($shot)
$dc = $graphics.GetHdc()
[DodgeWindow]::PrintWindow($targetHandle,$dc,2)
$graphics.ReleaseHdc($dc)
$shot.Save('E:\personalProject\Demo3\demo6\검증\dodge-v057\editor-window.png')
$graphics.Dispose()
$shot.Dispose()
