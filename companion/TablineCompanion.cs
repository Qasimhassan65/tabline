using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class TablineCompanion {
 private delegate bool EnumProc(IntPtr handle,IntPtr state);
 [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback,IntPtr state);
 [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
 [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr handle);
 [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr handle);
 [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr handle,uint command);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr handle,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle,StringBuilder text,int length);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr handle,StringBuilder text,int length);
 [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr handle,out Rect rectangle);
 [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr handle,out Rect rectangle);
 [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle,out uint processId);
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
 [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint process);
 [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo{public IntPtr Reserved,Peb,Reserved2,Reserved3,Id,Parent;}
 [DllImport("ntdll.dll")] private static extern int NtQueryInformationProcess(IntPtr process,int info,out ProcessInfo data,int size,out int returned);
 [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr handle,int command);
 [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
 [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr handle,int attribute,out int value,int size);
 [DllImport("dwmapi.dll")] private static extern int DwmFlush();
 internal sealed class Card {public string Id,Title,App,Note,ImageData,IconData,Domain;public Image Image,Icon;public bool Native,Current;public IntPtr Window;}
 private sealed class WindowInfo {public IntPtr Handle;public string Title,App;}
 private sealed class Thumb {public string Image;public uint Owner;}
 private static readonly Dictionary<long,Thumb> WindowThumbs=new Dictionary<long,Thumb>();
 private static readonly object OutputGate=new object();
 private static bool SelfTestMode,CopyBusy;
 private static int CaptureCount;
 private static uint ParentBrowser;
 private static Stream Output;
 internal static RopeForm Rope;
 private static ToastForm Toast;
 private static readonly System.Windows.Forms.Timer PendingTimer=new System.Windows.Forms.Timer{Interval=350};
 private static string PendingOperation;
 internal static Rectangle NoticeBounds;
 private static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=64*1024*1024};}
 [STAThread] private static int Main(string[] args){
  try{if(!SetProcessDpiAwarenessContext(new IntPtr(-4)))SetProcessDPIAware();}catch(EntryPointNotFoundException){SetProcessDPIAware();}
  Application.EnableVisualStyles();
  try{ProcessInfo info;int returned;if(NtQueryInformationProcess(new IntPtr(-1),0,out info,Marshal.SizeOf(typeof(ProcessInfo)),out returned)==0){using(var parent=Process.GetProcessById((int)info.Parent.ToInt64()))if(parent.ProcessName.Equals("chrome",StringComparison.OrdinalIgnoreCase))ParentBrowser=(uint)parent.Id;}}catch{}
  if(args.Length>0&&args[0]=="--self-test"){try{return SelfTest();}catch(Exception error){Console.Error.WriteLine("FAIL: "+error.Message);return 1;}}
  Rope=new RopeForm();Toast=new ToastForm();PendingTimer.Tick+=(s,e)=>{PendingTimer.Stop();Toast.ShowStatus(PendingOperation=="save"?"Saving…":"Capturing…",false,true,NoticeBounds);};var nativeHandle=Rope.Handle;
  Output=Console.OpenStandardOutput();Stream input=Console.OpenStandardInput();
  Task.Run(()=>{
   try{while(true){var length=new byte[4];if(!ReadFully(input,length))break;int size=BitConverter.ToInt32(length,0);if(size<1||size>64*1024*1024)break;var data=new byte[size];if(!ReadFully(input,data))break;var request=Serializer().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(data));Rope.BeginInvoke(new Action(()=>Reply(request)));}}
   catch{}finally{try{Rope.BeginInvoke(new Action(()=>Application.ExitThread()));}catch{}}
  });
  Application.Run();Toast.Dispose();Rope.Dispose();WindowThumbs.Clear();return 0;
 }
 private static bool ReadFully(Stream stream,byte[] buffer){int pos=0;while(pos<buffer.Length){int count=stream.Read(buffer,pos,buffer.Length-pos);if(count==0)return false;pos+=count;}return true;}
 internal static void Send(object value){
  if(Output==null)return;byte[] bytes=Encoding.UTF8.GetBytes(Serializer().Serialize(value));if(bytes.Length>1024*1024)return;
  try{lock(OutputGate){Output.Write(BitConverter.GetBytes(bytes.Length),0,4);Output.Write(bytes,0,bytes.Length);Output.Flush();}}catch{}
 }
 private static async void Reply(Dictionary<string,object> request){
  object id=request.ContainsKey("requestId")?request["requestId"]:null;
  try{var response=await Dispatch(request);response["ok"]=true;response["requestId"]=id;Send(response);}catch(Exception error){Send(new{ok=false,error=error.Message,requestId=id});}
 }
 private static string Text(Dictionary<string,object> request,string key){return request.ContainsKey(key)?Convert.ToString(request[key]):"";}
 private static List<Card> ParseTabs(Dictionary<string,object> request){
  var result=new List<Card>();object value;if(!request.TryGetValue("tabs",out value))return result;
  foreach(var item in (System.Collections.IEnumerable)value){var tab=item as Dictionary<string,object>;if(tab==null)continue;result.Add(new Card{Id=Text(tab,"id"),Title=Text(tab,"title"),App="Chrome",IconData=Text(tab,"icon"),Domain=Text(tab,"domain"),Note=Text(tab,"note"),ImageData=Text(tab,"image"),Current=tab.ContainsKey("current")&&Convert.ToBoolean(tab["current"])});}return result;
 }
 private static List<Card> AppCards(){
  var result=new List<Card>();var windows=ListWindows();
  var obsolete=new List<long>();foreach(var pair in WindowThumbs){uint owner;GetWindowThreadProcessId(new IntPtr(pair.Key),out owner);if(owner!=pair.Value.Owner||!windows.Exists(w=>w.Handle.ToInt64()==pair.Key))obsolete.Add(pair.Key);}foreach(long key in obsolete)WindowThumbs.Remove(key);
  foreach(var window in windows){Thumb thumb;WindowThumbs.TryGetValue(window.Handle.ToInt64(),out thumb);result.Add(new Card{Id="app:"+window.Handle.ToInt64(),Title=window.App+" · "+window.Title,App=window.App,Window=window.Handle,Native=true,IconData=WindowIcon(window.Handle),Domain=window.App,ImageData=thumb==null?null:thumb.Image,Note="Click to open this app and copy a fresh screenshot."});}return result;
 }
 private static async Task<Dictionary<string,object>> Dispatch(Dictionary<string,object> request){
  string action=Text(request,"action");var response=new Dictionary<string,object>();
  if(action=="ping"){response["version"]="2.2.0";return response;}
  if(action=="show"){
   var cards=ParseTabs(request);if(!request.ContainsKey("desktop")||Convert.ToBoolean(request["desktop"]))cards.AddRange(AppCards());
   NoticeBounds=WindowBounds(GetForegroundWindow());Rope.Open(cards,request);return response;
  }
  if(action=="hide"){Rope.Dismiss();return response;}
  if(action=="update-tabs"){Rope.UpdateTabs(ParseTabs(request));return response;}
  if(action=="notice"){Notify(Text(request,"text"),request.ContainsKey("success")&&Convert.ToBoolean(request["success"]));return response;}
  if(action=="clear"){WindowThumbs.Clear();Rope.ClearImages();return response;}
  if(action=="warm")return response; // Compatibility: never perform background app captures.
  if(action=="list"){
   var windows=new List<object>();int budget=450000;
   foreach(var card in AppCards()){string image=card.ImageData;if(image!=null&&image.Length>budget)image=null;if(image!=null)budget-=image.Length;windows.Add(new{handle=card.Window.ToInt64().ToString(),title=card.Title,appName=card.App,image=image,minimized=IsIconic(card.Window)});}response["windows"]=windows;return response;
  }
  if(action=="stats"){
   long bytes=0;foreach(var thumb in WindowThumbs.Values)bytes+=thumb.Image.Length*2;
   response["processId"]=Process.GetCurrentProcess().Id;response["toastLeft"]=Toast.Left;response["toastTop"]=Toast.Top;response["cardIds"]=Rope.CardIds;response["lastClickPoint"]=Rope.LastClickPoint;response["lastClickedId"]=Rope.LastClickedId;response["frameBytes"]=0;response["thumbBytes"]=bytes;response["captureCount"]=CaptureCount;
   response["dismissReason"]=Rope.DismissReason;response["visible"]=Rope.Visible;response["offset"]=Rope.Offset;response["targetOffset"]=Rope.TargetOffset;response["animating"]=Rope.Animating;response["cards"]=Rope.CardCount;
   response["decoded"]=Rope.DecodedCount;response["dwmPreviews"]=Rope.DwmPreviewCount;response["hwnd"]=Rope.Handle.ToInt64().ToString();response["foreground"]=GetForegroundWindow().ToInt64().ToString();response["scale"]=Rope.PixelScale;response["left"]=Rope.Left;response["top"]=Rope.Top;response["width"]=Rope.Width;response["height"]=Rope.Height;
   response["toastVisible"]=Toast.Visible;response["toastTitle"]=Toast.LastTitle;response["toastSuccess"]=Toast.LastSuccess;
   return response;
  }
  if(action=="render-preview"){response["image"]=request.ContainsKey("composited")&&Convert.ToBoolean(request["composited"])?Rope.CompositedData():Rope.RenderingData();return response;}
  if(action=="render-toast"){response["image"]=Toast.RenderingData();return response;}
  if(action=="clipboard"){
   string data=Text(request,"dataUrl");const string prefix="data:image/png;base64,";
   if(!data.StartsWith(prefix,StringComparison.Ordinal)||data.Length>48*1024*1024)throw new InvalidOperationException("Clipboard input must be a PNG screenshot within the size limit.");
   byte[] bytes=Convert.FromBase64String(data.Substring(prefix.Length));
   using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream)){
    if(image.Width>16000||image.Height>16000||(long)image.Width*image.Height>32000000)throw new InvalidOperationException("Screenshot exceeds the pixel limit.");
    using(var bitmap=new Bitmap(image))response["cancelled"]=!FinishImage(bitmap,Text(request,"operation")=="save");
   }
   return response;
  }
  if(action=="copy"||action=="copy-background"){
   long number;if(!long.TryParse(Text(request,"handle"),out number))throw new InvalidOperationException("Invalid window handle.");
   var window=ListWindows().Find(w=>w.Handle.ToInt64()==number);if(window==null)throw new InvalidOperationException("This app window has closed or is unavailable.");
   await CopyWindow(window.Handle,Text(request,"operation")=="save");return response;
  }
  if(action=="browser-copy"){
   IntPtr handle=await FocusBrowser(request);uint owner;GetWindowThreadProcessId(handle,out owner);
   using(var process=Process.GetProcessById((int)owner))if(!process.ProcessName.Equals("chrome",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Chrome is not the foreground window.");
   var title=new StringBuilder(2048);GetWindowText(handle,title,title.Capacity);
   if(String.IsNullOrEmpty(Text(request,"title"))||!title.ToString().StartsWith(Text(request,"title"),StringComparison.Ordinal))throw new InvalidOperationException("The Chrome tab changed before capture.");
   Toast.Dismiss();Rope.Dismiss();DwmFlush();
   var bounds=WindowBounds(handle);object raw;var viewport=request.TryGetValue("viewport",out raw)?raw as Dictionary<string,object>:null;
   float dpi;using(var g=Graphics.FromHwnd(handle))dpi=g.DpiX/96f;
   if(viewport!=null&&viewport.ContainsKey("height")){
    float ratio=viewport.ContainsKey("scale")?Convert.ToSingle(viewport["scale"]):dpi;
    int height=(int)(Convert.ToDouble(viewport["height"])*ratio),width=viewport.ContainsKey("width")?(int)(Convert.ToDouble(viewport["width"])*ratio):bounds.Width;
    bounds=new Rectangle(bounds.Left+8,Math.Max(bounds.Top,bounds.Bottom-height-8),Math.Min(width,bounds.Width-16),Math.Min(height,bounds.Height));
   }else{int toolbar=(int)(88*dpi);bounds=new Rectangle(bounds.Left+8,bounds.Top+toolbar,bounds.Width-16,bounds.Height-toolbar-8);}
   using(var bitmap=ScreenCapture(handle,bounds))response["cancelled"]=!FinishImage(bitmap,Text(request,"operation")=="save");
   return response;
  }
  throw new InvalidOperationException("Unknown companion action.");
 }
 internal static async void Select(Card card,string operation="copy"){
  Rope.Dismiss();Toast.Dismiss();PendingOperation=operation;PendingTimer.Stop();PendingTimer.Start();
  if(!card.Native){if(ParentBrowser!=0)AllowSetForegroundWindow(ParentBrowser);Send(new{@event="copy-tab",id=card.Id,operation=operation});return;}
  try{await CopyWindow(card.Window,operation=="save");}catch(Exception error){Notify(error.Message,false);}
 }
 private static async Task CopyWindow(IntPtr handle,bool save=false){
  if(CopyBusy)throw new InvalidOperationException("Another screenshot is being copied.");
  CopyBusy=true;
  try{
   if(!IsWindow(handle))throw new InvalidOperationException("This app window has closed.");
   Rope.Dismiss();Toast.Dismiss();bool minimized=IsIconic(handle);
   if(minimized)ShowWindowAsync(handle,9);
   SetForegroundWindow(handle);
   for(int i=0;i<12&&GetForegroundWindow()!=handle;i++){await Task.Delay(25);SetForegroundWindow(handle);}
   if(GetForegroundWindow()!=handle)throw new InvalidOperationException("Windows could not bring this app forward. Open it and try again.");
   await Task.Delay(minimized?220:65);DwmFlush();
   using(var bitmap=ScreenCapture(handle,WindowBounds(handle))){
    FinishImage(bitmap,save);
    uint owner;GetWindowThreadProcessId(handle,out owner);WindowThumbs.Remove(handle.ToInt64());WindowThumbs[handle.ToInt64()]=new Thumb{Owner=owner,Image=SmallPreview(bitmap)};
    long bytes=0;foreach(var thumb in WindowThumbs.Values)bytes+=thumb.Image.Length*2;
    while((bytes>1024*1024||WindowThumbs.Count>32)&&WindowThumbs.Count>0){long victim=0;foreach(long key in WindowThumbs.Keys){victim=key;break;}bytes-=WindowThumbs[victim].Image.Length*2;WindowThumbs.Remove(victim);}
   }
  }finally{CopyBusy=false;}
 }
 private static async Task<IntPtr> FocusBrowser(Dictionary<string,object> request){
  string expected=Text(request,"title");if(String.IsNullOrEmpty(expected))throw new InvalidOperationException("No Chrome target was supplied.");
  object raw;var requested=request.TryGetValue("bounds",out raw)?raw as Dictionary<string,object>:null;
  var candidates=new List<IntPtr>();
  EnumWindows((handle,state)=>{
   if(!IsWindowVisible(handle))return true;var title=new StringBuilder(2048);GetWindowText(handle,title,title.Capacity);if(!title.ToString().StartsWith(expected,StringComparison.Ordinal))return true;
   uint owner;GetWindowThreadProcessId(handle,out owner);try{using(var process=Process.GetProcessById((int)owner))if(!process.ProcessName.Equals("chrome",StringComparison.OrdinalIgnoreCase))return true;}catch{return true;}
   if(requested!=null){Rect r;if(!GetWindowRect(handle,out r))return true;if(Math.Abs(r.Left-Convert.ToInt32(requested["left"]))>24||Math.Abs(r.Top-Convert.ToInt32(requested["top"]))>24||Math.Abs((r.Right-r.Left)-Convert.ToInt32(requested["width"]))>32)return true;}
   candidates.Add(handle);return true;
  },IntPtr.Zero);
  if(candidates.Count!=1)throw new InvalidOperationException("The selected Chrome window could not be identified safely.");
  IntPtr target=candidates[0];SetForegroundWindow(target);for(int i=0;i<8&&GetForegroundWindow()!=target;i++){await Task.Delay(20);SetForegroundWindow(target);}
  if(GetForegroundWindow()!=target)throw new InvalidOperationException("Windows could not focus the selected Chrome window.");DwmFlush();return target;
 }
 private static Rectangle WindowBounds(IntPtr handle){Rect r;if(!GetWindowRect(handle,out r))throw new InvalidOperationException("Cannot read this window's bounds.");return new Rectangle(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
 private static Bitmap ScreenCapture(IntPtr handle,Rectangle bounds){
  if(GetForegroundWindow()!=handle)throw new InvalidOperationException("Another window took focus before capture.");
  bounds=Rectangle.Intersect(bounds,SystemInformation.VirtualScreen);
  if(bounds.Width<1||bounds.Height<1||(long)bounds.Width*bounds.Height>32000000)throw new InvalidOperationException("This window cannot be captured at its current size.");
  var bitmap=new Bitmap(bounds.Width,bounds.Height);
  try{using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);if(GetForegroundWindow()!=handle)throw new InvalidOperationException("Another window took focus during capture.");CaptureCount++;return bitmap;}
  catch{bitmap.Dispose();throw;}
 }
 private static string SmallPreview(Bitmap image){
  double ratio=Math.Min(360.0/image.Width,220.0/image.Height);
  using(var bitmap=new Bitmap(Math.Max(1,(int)(image.Width*ratio)),Math.Max(1,(int)(image.Height*ratio)))){
   using(var g=Graphics.FromImage(bitmap)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(image,0,0,bitmap.Width,bitmap.Height);}
   using(var stream=new MemoryStream()){var codec=Array.Find(ImageCodecInfo.GetImageEncoders(),c=>c.MimeType=="image/jpeg");using(var parameters=new EncoderParameters(1)){parameters.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,62L);bitmap.Save(stream,codec,parameters);}return "data:image/jpeg;base64,"+Convert.ToBase64String(stream.ToArray());}
  }
 }
 internal static void Notify(string text,bool success){PendingTimer.Stop();if(Toast!=null)Toast.ShowStatus(success?"Screenshot copied":"Screenshot failed",success,false,NoticeBounds);}
 private static bool FinishImage(Bitmap bitmap,bool save){
  PendingTimer.Stop();Toast.Dismiss();
  if(save){
   using(var dialog=new SaveFileDialog{Title="Save screenshot",Filter="PNG image (*.png)|*.png",DefaultExt="png",AddExtension=true,OverwritePrompt=true,FileName="Tabline-"+DateTime.Now.ToString("yyyy-MM-dd-HHmmss")+".png",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)}){
    if(dialog.ShowDialog()!=DialogResult.OK)return false;
    bitmap.Save(dialog.FileName,ImageFormat.Png);
   }
  }else Clipboard.SetDataObject(bitmap,true,3,30);
  Toast.ShowStatus(save?"Screenshot saved":"Screenshot copied",true,false,NoticeBounds);return true;
 }
 private static string WindowIcon(IntPtr handle){
  try{uint owner;GetWindowThreadProcessId(handle,out owner);using(var process=Process.GetProcessById((int)owner))using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName))using(var image=icon.ToBitmap())using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}}catch{return null;}
 }
 internal static bool IsOwnWindow(IntPtr window){uint owner;GetWindowThreadProcessId(window,out owner);return owner==(uint)Process.GetCurrentProcess().Id;}
 internal static void StopRefresh(){} // No background app refresh loop in this edition.
    private static List<WindowInfo> ListWindows() {
        var windows=new List<WindowInfo>();
        EnumWindows((handle,state)=>{
            if(!IsWindowVisible(handle) || GetWindow(handle,4)!=IntPtr.Zero)return true;
            if((GetWindowLongPtr(handle,-20).ToInt64() & 0x80)!=0)return true;
            var title=new StringBuilder(2048);GetWindowText(handle,title,title.Capacity);if(title.Length==0)return true;
            var name=new StringBuilder(256);GetClassName(handle,name,name.Capacity);
            if(name.ToString()=="Progman" || name.ToString()=="WorkerW" || name.ToString().StartsWith("Shell_Tray"))return true;
            int cloaked;if(DwmGetWindowAttribute(handle,14,out cloaked,4)==0 && cloaked!=0)return true;
            uint processId;GetWindowThreadProcessId(handle,out processId);
            try {
                using(var process=Process.GetProcessById((int)processId)){
                string app=process.ProcessName;
                if(app.Equals("chrome",StringComparison.OrdinalIgnoreCase) || (!SelfTestMode && processId==Process.GetCurrentProcess().Id))return true;
                if(app.Equals("Code",StringComparison.OrdinalIgnoreCase))app="VS Code";
                if(app.Equals("mspaint",StringComparison.OrdinalIgnoreCase))app="Paint";
                Rect rect;if(!GetWindowRect(handle,out rect) || (!IsIconic(handle)&&(rect.Right-rect.Left<30 || rect.Bottom-rect.Top<30)))return true;
                windows.Add(new WindowInfo{Handle=handle,Title=title.ToString(),App=app});
                }
            } catch { }
            return true;
        },IntPtr.Zero);
        return windows;
    }


 private static int SelfTest(){
  SelfTestMode=true;Rope=new RopeForm();Toast=new ToastForm();int result=1;
  using(var target=new Form{Text="Tabline on-demand test",Size=new Size(440,280),BackColor=Color.CornflowerBlue}){
   target.Controls.Add(new Label{Text="Full quality",Font=new Font("Segoe UI",20),AutoSize=true,Location=new Point(30,60)});
   target.Shown+=async(s,e)=>{
    try{
     await Task.Delay(100);TestInput.FocusOwnedWindow(target);await CopyWindow(target.Handle);
     using(var copied=Clipboard.GetImage()){if(copied==null||copied.Width<400)throw new InvalidOperationException("Clipboard image missing.");}
     var cards=new List<Card>();for(int i=0;i<12;i++)cards.Add(new Card{Id=i.ToString(),Title="Tab "+i,App="Chrome"});
     Rope.Open(cards,new Dictionary<string,object>());Rope.ScrollCards(280);await Task.Delay(600);
     if(Rope.Offset<270||Rope.Animating)throw new InvalidOperationException("Scroll animation did not settle.");
     Rope.SaveRendering(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rope-self-test.png"));Rope.Dismiss();
     Toast.ShowNotice(true,"Ready to paste with Ctrl + V.",Screen.PrimaryScreen);Toast.SaveRendering(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"toast-self-test.png"));
     Console.Error.WriteLine("PASS: fresh foreground screenshot, clipboard, animated rope scrolling and success notification");result=0;
    }catch(Exception error){Console.Error.WriteLine("FAIL: "+error.Message);}
    finally{Rope.Dispose();Toast.Dispose();target.Close();}
   };
   Application.Run(target);
  }
  return result;
 }
}


