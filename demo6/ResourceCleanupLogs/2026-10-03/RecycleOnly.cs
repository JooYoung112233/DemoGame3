using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Demo6Cleanup {
 [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 public interface IShellItem {
  void BindToHandler(IntPtr a, ref Guid b, ref Guid c, out IntPtr d);
  void GetParent(out IShellItem p);
  void GetDisplayName(uint kind, out IntPtr p);
  void GetAttributes(uint mask, out uint attributes);
  void Compare(IShellItem other, uint hint, out int order);
 }
 [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IFileOperation {
  void Advise(IntPtr sink, out uint cookie); void Unadvise(uint cookie);
  void SetOperationFlags(uint flags); void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string s);
  void SetProgressDialog(IntPtr x); void SetProperties(IntPtr x); void SetOwnerWindow(IntPtr x);
  void ApplyPropertiesToItem(IntPtr x); void ApplyPropertiesToItems(IntPtr x);
  void RenameItem(IntPtr a, IntPtr b, IntPtr c); void RenameItems(IntPtr a, IntPtr b);
  void MoveItem(IntPtr a, IntPtr b, IntPtr c, IntPtr d); void MoveItems(IntPtr a, IntPtr b);
  void CopyItem(IntPtr a, IntPtr b, IntPtr c, IntPtr d); void CopyItems(IntPtr a, IntPtr b);
  void DeleteItem(IShellItem item, IProgressSink sink); void DeleteItems(IntPtr a);
  void NewItem(IntPtr a, uint b, IntPtr c, IntPtr d, IntPtr e);
  void PerformOperations(); void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
 }
 [ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 public interface IProgressSink {
  [PreserveSig] int StartOperations(); [PreserveSig] int FinishOperations(int result);
  [PreserveSig] int PreRenameItem(uint flags,IShellItem item,[MarshalAs(UnmanagedType.LPWStr)]string name);
  [PreserveSig] int PostRenameItem(uint flags,IShellItem item,[MarshalAs(UnmanagedType.LPWStr)]string name,int hr,IShellItem created);
  [PreserveSig] int PreMoveItem(uint flags,IShellItem item,IShellItem dest,[MarshalAs(UnmanagedType.LPWStr)]string name);
  [PreserveSig] int PostMoveItem(uint flags,IShellItem item,IShellItem dest,[MarshalAs(UnmanagedType.LPWStr)]string name,int hr,IShellItem created);
  [PreserveSig] int PreCopyItem(uint flags,IShellItem item,IShellItem dest,[MarshalAs(UnmanagedType.LPWStr)]string name);
  [PreserveSig] int PostCopyItem(uint flags,IShellItem item,IShellItem dest,[MarshalAs(UnmanagedType.LPWStr)]string name,int hr,IShellItem created);
  [PreserveSig] int PreDeleteItem(uint flags,IShellItem item);
  [PreserveSig] int PostDeleteItem(uint flags,IShellItem item,int hr,IShellItem created);
  [PreserveSig] int PreNewItem(uint flags,IShellItem dest,[MarshalAs(UnmanagedType.LPWStr)]string name);
  [PreserveSig] int PostNewItem(uint flags,IShellItem dest,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,uint attrs,int hr,IShellItem created);
  [PreserveSig] int UpdateProgress(uint total,uint done); [PreserveSig] int ResetTimer();
  [PreserveSig] int PauseTimer(); [PreserveSig] int ResumeTimer();
 }
 [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
 public class RecycleSink : IProgressSink {
  public string Expected; public string Destination; public uint DeleteFlags; public int Result=unchecked((int)0x80004005);
  public int StartOperations(){return 0;} public int FinishOperations(int hr){return 0;}
  public int PreRenameItem(uint f,IShellItem i,string n){return unchecked((int)0x80004004);} public int PostRenameItem(uint f,IShellItem i,string n,int h,IShellItem c){return 0;}
  public int PreMoveItem(uint f,IShellItem i,IShellItem d,string n){return unchecked((int)0x80004004);} public int PostMoveItem(uint f,IShellItem i,IShellItem d,string n,int h,IShellItem c){return 0;}
  public int PreCopyItem(uint f,IShellItem i,IShellItem d,string n){return unchecked((int)0x80004004);} public int PostCopyItem(uint f,IShellItem i,IShellItem d,string n,int h,IShellItem c){return 0;}
  public int PreDeleteItem(uint flags,IShellItem item){
   DeleteFlags=flags;
   // Abort if Windows is not offering recycling, or if the item differs from the validated target.
   if((flags & 0x80)==0 || !String.Equals(Recycler.Name(item),Expected,StringComparison.OrdinalIgnoreCase)) return unchecked((int)0x80004004);
   return 0;
  }
  public int PostDeleteItem(uint flags,IShellItem item,int hr,IShellItem created){Result=hr;Destination=created==null?null:Recycler.Name(created);return 0;}
  public int PreNewItem(uint f,IShellItem d,string n){return unchecked((int)0x80004004);} public int PostNewItem(uint f,IShellItem d,string n,string t,uint a,int h,IShellItem c){return 0;}
  public int UpdateProgress(uint a,uint b){return 0;} public int ResetTimer(){return 0;} public int PauseTimer(){return 0;} public int ResumeTimer(){return 0;}
 }
 public static class Recycler {
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
  static extern void SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IShellItem item);
  public static string Name(IShellItem item){IntPtr p;item.GetDisplayName(0x80058000,out p);try{return Marshal.PtrToStringUni(p);}finally{Marshal.FreeCoTaskMem(p);}}
  public static RecycleSink Send(string path){
   path=Path.GetFullPath(path);
   string root=Path.GetFullPath(@"E:\personalProject\Demo3\demo6")+Path.DirectorySeparatorChar;
   if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Outside project");
   if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Reparse point");
   var sink=new RecycleSink{Expected=path};
   var op=(IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));
   IShellItem item=null;
   try {
    // Windows 8+ recycle-on-delete + undo record, early failure, silent errors, and permanent-delete warning.
    op.SetOperationFlags(0x00080000|0x20000000|0x00100000|0x00004000|0x00000400|0x00000010|0x00000004);
    var iid=typeof(IShellItem).GUID; SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out item);
    op.DeleteItem(item,sink); op.PerformOperations(); bool aborted;op.GetAnyOperationsAborted(out aborted);
    if(aborted || sink.Result<0)throw new IOException("Recycle cancelled/failed: "+sink.Result.ToString("X8")+" flags="+sink.DeleteFlags);
    if(String.IsNullOrEmpty(sink.Destination)||sink.Destination.IndexOf("$Recycle.Bin",StringComparison.OrdinalIgnoreCase)<0)throw new IOException("No verified Recycle Bin destination");
    return sink;
   } finally {if(item!=null)Marshal.ReleaseComObject(item);Marshal.ReleaseComObject(op);GC.KeepAlive(sink);}
  }
 }
}
