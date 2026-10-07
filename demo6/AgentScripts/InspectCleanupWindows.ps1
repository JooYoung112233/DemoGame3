Add-Type @'
using System;using System.Text;using System.Runtime.InteropServices;using System.Collections.Generic;
public static class CleanupWindows {
 public delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumWindows(Callback f,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,Callback f,IntPtr p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 static string Text(IntPtr h){var b=new StringBuilder(500);GetWindowText(h,b,500);return b.ToString();}
 static string Class(IntPtr h){var b=new StringBuilder(100);GetClassName(h,b,100);return b.ToString();}
 public static string[] Read(){var a=new List<string>();EnumWindows((h,p)=>{uint id;GetWindowThreadProcessId(h,out id);if(id==1764184||Class(h)=="#32770"){a.Add(id+" | "+h+" | "+Class(h)+" | "+Text(h));EnumChildWindows(h,(ch,cp)=>{var t=Text(ch);if(!string.IsNullOrEmpty(t))a.Add("  "+ch+" | "+Class(ch)+" | "+t);return true;},IntPtr.Zero);}return true;},IntPtr.Zero);return a.ToArray();}
}
'@
[CleanupWindows]::Read()
