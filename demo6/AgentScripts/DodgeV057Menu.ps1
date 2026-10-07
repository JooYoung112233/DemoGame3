param([string]$Action='Read')
Add-Type @'
using System;using System.Text;using System.Runtime.InteropServices;using System.Collections.Generic;
public static class DodgeMenu{
 [DllImport("user32.dll")]static extern IntPtr GetMenu(IntPtr w);
 [DllImport("user32.dll")]static extern IntPtr GetSubMenu(IntPtr m,int p);
 [DllImport("user32.dll")]static extern int GetMenuItemCount(IntPtr m);
 [DllImport("user32.dll")]static extern uint GetMenuItemID(IntPtr m,int p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetMenuString(IntPtr m,uint p,StringBuilder s,int n,uint f);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr w,uint m,IntPtr a,IntPtr b);
 static List<string> entries=new List<string>();static uint command;
 static void Walk(IntPtr m,string prefix,string target){for(int i=0;i<GetMenuItemCount(m);i++){var b=new StringBuilder(300);GetMenuString(m,(uint)i,b,300,0x400);string n=prefix+b.ToString().Replace("&","").Split('\t')[0];var sub=GetSubMenu(m,i);if(n.Contains("Pipeline")||prefix=="")entries.Add(n+"="+GetMenuItemID(m,i));if(n==target)command=GetMenuItemID(m,i);if(sub!=IntPtr.Zero)Walk(sub,n+"/",target);}}
 public static string[] Run(string target){entries.Clear();command=0;Walk(GetMenu(new IntPtr(591256)),"",target);if(!string.IsNullOrEmpty(target)){if(command==0||command==0xffffffff)throw new Exception("Menu not found");entries.Add("Posted "+target+" "+PostMessage(new IntPtr(591256),0x111,new IntPtr(command),IntPtr.Zero));}return entries.ToArray();}
}
'@
if($Action -eq 'Read'){[DodgeMenu]::Run('')}else{[DodgeMenu]::Run($Action)}
