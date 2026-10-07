using System;using System.Collections.Generic;using System.Runtime.InteropServices;
namespace ReviewCleanup {
 [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]public interface Item{
  void BindToHandler(IntPtr a,ref Guid b,ref Guid c,out IntPtr d);void GetParent(out Item p);void GetDisplayName(uint kind,out IntPtr text);void GetAttributes(uint mask,out uint value);void Compare(Item other,uint hint,out int order);
 }
 [ComImport,Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]public interface Operation{
  void Advise(Sink sink,out uint cookie);void Unadvise(uint cookie);void SetOperationFlags(uint flags);void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)]string text);void SetProgressDialog(IntPtr p);void SetProperties(IntPtr p);void SetOwnerWindow(IntPtr p);
  void ApplyPropertiesToItem(Item p);void ApplyPropertiesToItems(IntPtr p);void RenameItem(Item p,[MarshalAs(UnmanagedType.LPWStr)]string name,Sink s);void RenameItems(IntPtr p,[MarshalAs(UnmanagedType.LPWStr)]string name);void MoveItem(Item a,Item b,[MarshalAs(UnmanagedType.LPWStr)]string name,Sink s);void MoveItems(IntPtr a,Item b);void CopyItem(Item a,Item b,[MarshalAs(UnmanagedType.LPWStr)]string name,Sink s);void CopyItems(IntPtr a,Item b);void DeleteItem(Item p,Sink s);void DeleteItems(IntPtr p);void NewItem(Item p,uint attr,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,Sink s);void PerformOperations();void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)]out bool aborted);
 }
 [ComVisible(true),Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]public interface Sink{
  [PreserveSig]int StartOperations();[PreserveSig]int FinishOperations(int hr);[PreserveSig]int PreRenameItem(uint f,Item a,[MarshalAs(UnmanagedType.LPWStr)]string n);[PreserveSig]int PostRenameItem(uint f,Item a,[MarshalAs(UnmanagedType.LPWStr)]string n,int hr,Item b);
  [PreserveSig]int PreMoveItem(uint f,Item a,Item b,[MarshalAs(UnmanagedType.LPWStr)]string n);[PreserveSig]int PostMoveItem(uint f,Item a,Item b,[MarshalAs(UnmanagedType.LPWStr)]string n,int hr,Item c);
  [PreserveSig]int PreCopyItem(uint f,Item a,Item b,[MarshalAs(UnmanagedType.LPWStr)]string n);[PreserveSig]int PostCopyItem(uint f,Item a,Item b,[MarshalAs(UnmanagedType.LPWStr)]string n,int hr,Item c);
  [PreserveSig]int PreDeleteItem(uint f,Item a);[PreserveSig]int PostDeleteItem(uint f,Item a,int hr,Item b);
  [PreserveSig]int PreNewItem(uint f,Item a,[MarshalAs(UnmanagedType.LPWStr)]string n);[PreserveSig]int PostNewItem(uint f,Item a,[MarshalAs(UnmanagedType.LPWStr)]string n,[MarshalAs(UnmanagedType.LPWStr)]string t,uint attr,int hr,Item b);
  [PreserveSig]int UpdateProgress(uint total,uint done);[PreserveSig]int ResetTimer();[PreserveSig]int PauseTimer();[PreserveSig]int ResumeTimer();
 }
 [ComVisible(true),ClassInterface(ClassInterfaceType.None)]public sealed class Guard:Sink{
  public readonly List<string> Recycled=new List<string>();public readonly List<uint> Flags=new List<uint>();public bool Refused;public int Error;
  public int StartOperations(){return 0;}public int FinishOperations(int h){Error=h;return 0;}
  public int PreDeleteItem(uint f,Item a){Flags.Add(f);if((f&0x80)==0){Refused=true;return unchecked((int)0x80004004);}return 0;}
  public int PostDeleteItem(uint f,Item a,int h,Item b){if(h<0){Error=h;return h;}if(b==null){Refused=true;return unchecked((int)0x80004004);}IntPtr p=IntPtr.Zero;try{b.GetDisplayName(0x80058000,out p);Recycled.Add(Marshal.PtrToStringUni(p));}finally{if(p!=IntPtr.Zero)Marshal.FreeCoTaskMem(p);}return 0;}
  public int PreRenameItem(uint f,Item a,string n){return 0;}public int PostRenameItem(uint f,Item a,string n,int h,Item b){return 0;}public int PreMoveItem(uint f,Item a,Item b,string n){return 0;}public int PostMoveItem(uint f,Item a,Item b,string n,int h,Item c){return 0;}public int PreCopyItem(uint f,Item a,Item b,string n){return 0;}public int PostCopyItem(uint f,Item a,Item b,string n,int h,Item c){return 0;}public int PreNewItem(uint f,Item a,string n){return 0;}public int PostNewItem(uint f,Item a,string n,string t,uint at,int h,Item b){return 0;}public int UpdateProgress(uint a,uint b){return 0;}public int ResetTimer(){return 0;}public int PauseTimer(){return 0;}public int ResumeTimer(){return 0;}
 }
 public static class Recycle {
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]static extern void SHCreateItemFromParsingName(string path,IntPtr context,ref Guid id,out Item item);
  public static string[] Only(string path){
   Guid iid=typeof(Item).GUID;Item item;SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out item);
   var op=(Operation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));var sink=new Guard();
   try{op.SetOperationFlags(0x20000000|0x00080000|0x00100000|0x4000|0x0400|0x0010|0x0004);op.DeleteItem(item,sink);op.PerformOperations();bool aborted;op.GetAnyOperationsAborted(out aborted);if(aborted||sink.Refused||sink.Error<0||sink.Recycled.Count==0)throw new Exception("Recycle failed or permanent deletion refused; no fallback. Error="+sink.Error+" Flags="+string.Join(",",sink.Flags));return sink.Recycled.ToArray();}finally{Marshal.FinalReleaseComObject(op);Marshal.FinalReleaseComObject(item);}
  }
 }
}
