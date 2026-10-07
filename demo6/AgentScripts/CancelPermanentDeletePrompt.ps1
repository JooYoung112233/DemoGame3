Add-Type @'
using System;using System.Text;using System.Runtime.InteropServices;
public static class CancelCleanupPrompt {
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern int GetDlgCtrlID(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 public static string Cancel(){var button=new IntPtr(1640390);uint pid;GetWindowThreadProcessId(button,out pid);var label=new StringBuilder(100);GetWindowText(button,label,100);if(pid!=1764184||!label.ToString().StartsWith("아니요"))throw new Exception("Refusing unmatched dialog button");SendMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero);return "Pressed No on owned cleanup permanent-delete prompt; permanent deletion refused.";}
}
'@
[CancelCleanupPrompt]::Cancel()
