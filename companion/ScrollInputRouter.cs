using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

// Captures only wheel input for the visible picker, including its transparent
// gaps. The hook has its own message thread so painting cannot stall input.
// No mouse movement, clicks, keys or screenshot data are retained or forwarded.
internal sealed class ScrollInputRouter:IDisposable {
 internal const int WheelMessage=0x8033;
 private Session Current;
 private long Generation;
 internal bool Installed{get{return Current!=null&&Current.Installed;}}
 internal void Start(IntPtr source,IntPtr target,Rectangle area){Stop();Current=new Session(source,target,area,++Generation);Current.Start();}
 internal void UpdateArea(Rectangle area){if(Current!=null)Current.UpdateArea(area);}
 internal bool Accepts(IntPtr marker){return Current!=null&&!Current.Stopped&&(marker.ToInt64()>>1)==Current.Generation;}
 internal void Stop(){var session=Current;Current=null;if(session!=null)session.Stop();}
 public void Dispose(){Stop();}

 private sealed class Scope {
  internal readonly Rectangle Area;
  internal Scope(Rectangle area){Area=area;}
 }
 private sealed class Session {
  private delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
  [StructLayout(LayoutKind.Sequential)]private struct MouseData{public Point Point;public uint Data,Flags,Time;public UIntPtr Extra;}
  [StructLayout(LayoutKind.Sequential)]private struct MessageData{public IntPtr Window;public uint Message;public UIntPtr WParam;public IntPtr LParam;public uint Time;public Point Point;public uint Private;}
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int kind,HookProc callback,IntPtr module,uint thread);
  [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
  [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]private static extern IntPtr WindowFromPoint(Point point);
  [DllImport("user32.dll")]private static extern IntPtr GetAncestor(IntPtr window,uint flags);
  [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool PostMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool PostThreadMessage(uint thread,int message,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetMessage(out MessageData message,IntPtr window,uint first,uint last);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool PeekMessage(out MessageData message,IntPtr window,uint first,uint last,uint remove);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string name);
  [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
  private readonly IntPtr Source,Target;
  internal readonly long Generation;
  private readonly HookProc Callback;
  private volatile Scope ActiveScope;
  private volatile uint ThreadId;
  private int Stopping;
  private IntPtr Hook;
  internal bool Stopped{get{return Thread.VolatileRead(ref Stopping)!=0;}}
  internal bool Installed{get{return !Stopped&&Interlocked.CompareExchange(ref Hook,IntPtr.Zero,IntPtr.Zero)!=IntPtr.Zero;}}
  internal Session(IntPtr source,IntPtr target,Rectangle area,long generation){Source=source;Target=target;ActiveScope=new Scope(area);Generation=generation;Callback=OnMouse;}
  internal void Start(){new Thread(Run){IsBackground=true,Name="Tabline picker scroll"}.Start();}
  internal void UpdateArea(Rectangle area){ActiveScope=new Scope(area);}
  internal void Stop(){
   Interlocked.Exchange(ref Stopping,1);var hook=Interlocked.Exchange(ref Hook,IntPtr.Zero);if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);
   uint thread=ThreadId;if(thread!=0)PostThreadMessage(thread,0x0012,IntPtr.Zero,IntPtr.Zero);
  }
  private void Run(){
   try{
    // Create the queue before publishing its ID so Stop can always wake it.
    MessageData message;PeekMessage(out message,IntPtr.Zero,0,0,0);ThreadId=GetCurrentThreadId();if(Stopped)return;
    var hook=SetWindowsHookEx(14,Callback,GetModuleHandle(null),0);if(hook==IntPtr.Zero)return;
    Interlocked.Exchange(ref Hook,hook);if(Stopped)return;
    while(!Stopped&&GetMessage(out message,IntPtr.Zero,0,0)>0){}
   }finally{var hook=Interlocked.Exchange(ref Hook,IntPtr.Zero);if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);ThreadId=0;}
  }
  private IntPtr OnMouse(int code,IntPtr message,IntPtr data){
   int kind=message.ToInt32();
   if(code>=0&&!Stopped&&(kind==0x020A||kind==0x020E)){
    try{
     var mouse=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));var scope=ActiveScope;
     // Pinch/Control-wheel is not picker navigation. Do not consume
     // another app, the Chrome toolbar, or an obscuring window's input.
     if(scope.Area.Contains(mouse.Point)&&(GetAsyncKeyState(17)&0x8000)==0){
      var foreground=GetForegroundWindow();
      if(foreground==Source||GetAncestor(foreground,3)==Target){
       var under=WindowFromPoint(mouse.Point);var root=GetAncestor(under,2);var owner=GetAncestor(under,3);
       if(root==Source||owner==Target){
        int delta=(short)(mouse.Data>>16);long marker=(Generation<<1)|(kind==0x020E?1L:0L);
        // Never render, invoke the UI synchronously, or wait inside this hook.
        if(delta!=0&&PostMessage(Target,WheelMessage,new IntPtr(delta),new IntPtr(marker)))return new IntPtr(1);
       }
      }
     }
    }catch{ /* Preserve normal input if routing is unavailable. */ }
   }
   return CallNextHookEx(IntPtr.Zero,code,message,data);
  }
 }
}
