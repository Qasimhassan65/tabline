using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
internal sealed class RopeForm:Form {
 [StructLayout(LayoutKind.Sequential)] private struct RectN{public int Left,Top,Right,Bottom;public RectN(Rectangle r){Left=r.Left;Top=r.Top;Right=r.Right;Bottom=r.Bottom;}}
 [StructLayout(LayoutKind.Sequential)] private struct SizeN{public int Width,Height;}
 [StructLayout(LayoutKind.Sequential)] private struct ThumbnailProperties{public uint Flags;public RectN Destination,Source;public byte Opacity;[MarshalAs(UnmanagedType.Bool)]public bool Visible;[MarshalAs(UnmanagedType.Bool)]public bool ClientOnly;}
 [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
 [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
 [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail,out SizeN size);
 [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref ThumbnailProperties properties);
 [DllImport("dwmapi.dll")] private static extern int DwmFlush();
 [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
 [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
 private readonly List<TablineCompanion.Card> Cards=new List<TablineCompanion.Card>();
 private readonly Dictionary<string,AppThumbnailWindow> DwmThumbnails=new Dictionary<string,AppThumbnailWindow>();
 private readonly Timer InputTimer=new Timer{Interval=40},MotionTimer=new Timer{Interval=15};
 private readonly Stopwatch MotionClock=new Stopwatch();
 private readonly ToolTip Tip=new ToolTip{InitialDelay=700,AutoPopDelay=4000};
 private float CardWidth=240,Gap=24,RowPadding=24,PageInset=88;
 private Rectangle SourceBounds;
 [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr window);
 private float Pitch{get{return CardWidth+Gap;}}
 private float UiScale=1,VisualOffset;
 private const float CableToCard=19,ClipAboveCable=6,ClipWidth=16,ClipHeight=37,FastenerFromTop=29.5f;
 private Bitmap CableSurface;
 private float CableSurfaceScale;
 private int CableSurfaceWidth;
 private double LastMotionTime;
 private bool MouseWasDown;
 private IntPtr SourceWindow;
 private string Hover;
 private int FocusMismatchAt;
 internal string DismissReason;
 internal string LastClickedId;
 internal string LastClickPoint;
 internal string[] CardIds{get{return Cards.ConvertAll(c=>c.Id).ToArray();}}
 internal int Offset{get{return (int)Math.Round(VisualOffset);}}
 internal int TargetOffset{get;private set;}
 internal bool Animating{get{return MotionTimer.Enabled;}}
 internal float PixelScale{get{return UiScale;}}
 internal int CardCount{get{return Cards.Count;}}
 internal int DecodedCount{get{return Cards.FindAll(c=>c.Image!=null).Count;}}
 internal int DwmPreviewCount{get{return DwmThumbnails.Count;}}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;return p;}}
 internal RopeForm(){Text="Tabline rope";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;InputTimer.Tick+=(s,e)=>CheckInput();MotionTimer.Tick+=(s,e)=>AnimateScroll();}
 internal void Open(List<TablineCompanion.Card> cards,Dictionary<string,object> request){
  ReleaseThumbnails();DisposeImages();Cards.Clear();Cards.AddRange(cards);VisualOffset=0;TargetOffset=0;MotionTimer.Stop();Hover=null;FocusMismatchAt=0;DismissReason=null;
  // Picker-only order: never move real browser tabs or change app focus.
  int current=Cards.FindIndex(c=>c.Current);
  if(current>=0){var active=Cards[current];Cards.RemoveAt(current);Cards.Insert(Cards.Count/2,active);}
  SourceWindow=TablineCompanion.GetForegroundWindow();TablineCompanion.Rect bounds;Rectangle screen=Screen.FromHandle(SourceWindow).WorkingArea;
  if(!TablineCompanion.GetWindowRect(SourceWindow,out bounds)||bounds.Right-bounds.Left<300)bounds=new TablineCompanion.Rect{Left=screen.Left,Top=screen.Top,Right=screen.Right,Bottom=screen.Bottom};
  using(var g=Graphics.FromHwnd(SourceWindow))UiScale=Math.Max(.75f,g.DpiX/96f);
  int pageTop=bounds.Top+(int)(88*UiScale);object raw;var viewport=request.TryGetValue("viewport",out raw)?raw as Dictionary<string,object>:null;
  if(viewport!=null&&viewport.ContainsKey("height")){float ratio=viewport.ContainsKey("scale")?Convert.ToSingle(viewport["scale"]):UiScale;int top=bounds.Bottom-(int)(Convert.ToDouble(viewport["height"])*ratio)-8;pageTop=Math.Max(bounds.Top+(int)(40*UiScale),Math.Min(bounds.Top+(int)(160*UiScale),top));}
  PageInset=(pageTop-bounds.Top)/UiScale;
  LayoutForSource(new Rectangle(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top),UiScale,false);
  current=Cards.FindIndex(c=>c.Current);
  if(current>=0){VisualOffset=current*Pitch;TargetOffset=(int)Math.Round(VisualOffset);}
  MouseWasDown=(GetAsyncKeyState(1)&0x8000)!=0;SetWindowDisplayAffinity(Handle,0x11);PresentRope();Show();SyncThumbnails();InputTimer.Start();
 }
 internal void Dismiss(string reason="requested"){if(!Visible)return;DismissReason=reason;MotionTimer.Stop();InputTimer.Stop();Tip.Hide(this);ReleaseThumbnails();Hide();DisposeImages();TablineCompanion.Send(new{@event="closed"});}
 private void DisposeImages(){foreach(var card in Cards){if(card.Image!=null){card.Image.Dispose();card.Image=null;}if(card.Icon!=null){card.Icon.Dispose();card.Icon=null;}}}
 private void CheckInput(){
  TablineCompanion.Rect current;
  if(TablineCompanion.GetWindowRect(SourceWindow,out current)){
   var source=new Rectangle(current.Left,current.Top,current.Right-current.Left,current.Bottom-current.Top);
   float scale=UiScale;try{uint dpi=GetDpiForWindow(SourceWindow);if(dpi>0)scale=dpi/96f;}catch(EntryPointNotFoundException){}
   if(source!=SourceBounds||Math.Abs(scale-UiScale)>.01f){LayoutForSource(source,scale,true);Render();}
  }
  if(!Animating)UpdateHover(PointToClient(Cursor.Position));
  if((GetAsyncKeyState(27)&0x8000)!=0){Dismiss();return;}
  var foreground=TablineCompanion.GetForegroundWindow();
  if(foreground==SourceWindow||TablineCompanion.IsOwnWindow(foreground)){FocusMismatchAt=0;}
  else if(foreground!=IntPtr.Zero){if(FocusMismatchAt==0)FocusMismatchAt=Environment.TickCount;else if(unchecked((uint)(Environment.TickCount-FocusMismatchAt))>180){Dismiss("focus-changed");return;}}
  bool down=(GetAsyncKeyState(1)&0x8000)!=0;if(down&&!MouseWasDown&&Hit(PointToClient(Cursor.Position))==null){Dismiss();return;}MouseWasDown=down;
 }
 internal void ScrollCards(int amount){
  int max=MaximumOffset(),next=Math.Max(0,Math.Min(max,TargetOffset+amount));
  if(next==TargetOffset&&Math.Abs(next-VisualOffset)<.2)return;
  TargetOffset=next;Tip.Hide(this);bool hadHover=Hover!=null;Hover=null;if(hadHover)Render();
  if(Math.Abs(TargetOffset-VisualOffset)<.2)return;
  if(!MotionTimer.Enabled){MotionClock.Restart();LastMotionTime=0;MotionTimer.Start();}
 }
 private void AnimateScroll(){
  double now=MotionClock.Elapsed.TotalMilliseconds,delta=now-LastMotionTime;LastMotionTime=now;
  VisualOffset+=(float)((TargetOffset-VisualOffset)*(1-Math.Exp(-delta/55)));
  if(Math.Abs(TargetOffset-VisualOffset)<.45){VisualOffset=TargetOffset;MotionTimer.Stop();}
  Render();
  if(!Animating)UpdateHover(PointToClient(Cursor.Position));
 }
 protected override void OnMouseWheel(MouseEventArgs e){ScrollCards(-e.Delta*2);base.OnMouseWheel(e);}
 private void UpdateHover(Point point){var card=Hit(point);string id=card==null?null:card.Id;if(Hover!=id){Tip.Hide(this);Hover=id;Cursor=card==null?Cursors.Default:Cursors.Hand;Render();}}
 protected override void OnMouseMove(MouseEventArgs e){if(!Animating)UpdateHover(e.Location);base.OnMouseMove(e);}
 internal void ClickAt(Point point){var card=Hit(point);LastClickPoint=point.X+","+point.Y+" offset="+VisualOffset;LastClickedId=card==null?null:card.Id;if(card==null)return;string operation=OperationAt(Cards.IndexOf(card),point);TablineCompanion.Select(card,operation);}
 private string OperationAt(int index,Point point){return new RectangleF(193,182,112,47).Contains(CardLocalPoint(index,point))?"save":"copy";}
 protected override void OnMouseUp(MouseEventArgs e){if(e.Button==MouseButtons.Left)ClickAt(e.Location);base.OnMouseUp(e);}
 private static RectangleF Part(RectangleF r,float x,float y,float w,float h){float k=r.Width/376f;return new RectangleF(r.X+x*k,r.Y+y*k,w*k,h*k);}
 private static RectangleF SaveBounds(RectangleF r){return Part(r,193,182,112,47);}
 private static RectangleF CopyBounds(RectangleF r){return Part(r,72,182,114,47);}
 private static RectangleF PictureBounds(RectangleF r){return Part(r,13,14,350,218);}
 private int MaximumOffset(){return (int)Math.Round(Math.Max(0,(Cards.Count-1)*Pitch));}
 private float CableSag{get{return Math.Max(14,Math.Min(26,.016f*Width/UiScale));}}
 private void ConfigureMetrics(float width,float height){
  Gap=Math.Min(24,Math.Max(12,width*.025f));
  CardWidth=Math.Min(300,Math.Max(240,(width-152)/5));
  CardWidth=Math.Min(CardWidth,Math.Max(72,width-12));
  CardWidth=Math.Min(CardWidth,Math.Max(72,(height-18-Math.Max(14,Math.Min(26,.016f*width)))*376/318));
  RowPadding=(width-CardWidth)/2;
 }
 private void LayoutForSource(Rectangle source,float scale,bool preserve){
  float previousPitch=Pitch,position=VisualOffset/previousPitch,target=TargetOffset/previousPitch;
  SourceBounds=source;UiScale=Math.Max(.75f,scale);
  var screen=Screen.FromHandle(SourceWindow).WorkingArea;
  int left=Math.Max(screen.Left,source.Left+8),right=Math.Min(screen.Right,source.Right-8);
  int top=Math.Max(screen.Top,Math.Min(screen.Bottom-1,source.Top+(int)Math.Round(PageInset*UiScale)));
  int available=Math.Max(1,Math.Min(screen.Bottom,source.Bottom-8)-top);
  int width=Math.Max(1,right-left);
  ConfigureMetrics(width/UiScale,available/UiScale);
  int height=(int)Math.Ceiling((18+Math.Max(14,Math.Min(26,.016f*width/UiScale))+318*CardWidth/376)*UiScale);
  Bounds=new Rectangle(left,top,width,Math.Max(1,Math.Min(available,height)));
  if(preserve){VisualOffset=Math.Min(MaximumOffset(),position*Pitch);TargetOffset=Math.Min(MaximumOffset(),(int)Math.Round(target*Pitch));}
 }
 private float CableY(float x){float half=Math.Max(1,Width/UiScale/2),u=Math.Max(-1,Math.Min(1,(x-half)/half));return 14+CableSag*(1-u*u);}
 private float CableSlope(float x){float half=Math.Max(1,Width/UiScale/2),u=Math.Max(-1,Math.Min(1,(x-half)/half));return -2*CableSag*u/half;}
 private float CardRoll(int index){
  // A DWM thumbnail has rectangular destinations, so keep its complete card
  // upright on the arc rather than rotating a frame around an upright image.
  if(Cards[index].Native)return 0;
  var r=CardBounds(index);return Math.Max(-1.8f,Math.Min(1.8f,(float)(Math.Atan(CableSlope(r.X+r.Width/2))*90/Math.PI)));
 }
 private Matrix CardTransform(int index){
  var r=CardBounds(index);float k=r.Width/376,angle=CardRoll(index)*(float)Math.PI/180;
  float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle),px=188,py=FastenerFromTop-CableToCard-ClipAboveCable;
  return new Matrix(k*c,k*s,-k*s,k*c,r.X+k*(px-c*px+s*py),r.Y+k*(py-s*px-c*py));
 }
 private void ApplyCardTransform(Graphics g,int index){using(var matrix=CardTransform(index))g.MultiplyTransform(matrix);}
 private PointF CardLocalPoint(int index,Point point){var p=new[]{new PointF(point.X/UiScale,point.Y/UiScale)};using(var matrix=CardTransform(index)){matrix.Invert();matrix.TransformPoints(p);}return p[0];}
 protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}if(m.Msg==0x020E){int delta=(short)((m.WParam.ToInt64()>>16)&65535);ScrollCards(delta*2);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 private RectangleF CardBounds(int i){float x=RowPadding+i*Pitch-VisualOffset;return new RectangleF(x,CableY(x+CardWidth/2)+CableToCard*CardWidth/376,CardWidth,285*CardWidth/376);}
 private TablineCompanion.Card Hit(Point p){
  float x=p.X/UiScale;
  for(int i=Cards.Count-1;i>=0;i--){var r=CardBounds(i);if(x<r.Left-12||x>r.Right+12)continue;var local=CardLocalPoint(i,p);using(var face=Rounded(new RectangleF(0,0,376,285),18))if(face.IsVisible(local))return Cards[i];}
  return null;
 }
 internal void UpdateTabs(List<TablineCompanion.Card> tabs){
  int anchor=Cards.Count==0?-1:Math.Max(0,Math.Min(Cards.Count-1,(int)Math.Round(VisualOffset/Pitch)));string anchorId=anchor<0?null:Cards[anchor].Id;
  bool changed=false;foreach(var incoming in tabs){var existing=Cards.Find(c=>!c.Native&&c.Id==incoming.Id);if(existing==null){Cards.Add(incoming);changed=true;}else{if(existing.Title!=incoming.Title){existing.Title=incoming.Title;changed=true;}if(existing.ImageData!=incoming.ImageData){if(existing.Image!=null){existing.Image.Dispose();existing.Image=null;}existing.ImageData=incoming.ImageData;changed=true;}}}
  for(int i=Cards.Count-1;i>=0;i--)if(!Cards[i].Native&&!tabs.Exists(c=>c.Id==Cards[i].Id)){if(Cards[i].Image!=null)Cards[i].Image.Dispose();if(Cards[i].Icon!=null)Cards[i].Icon.Dispose();Cards.RemoveAt(i);changed=true;}
  if(changed){int next=Cards.FindIndex(c=>c.Id==anchorId);if(next>=0){float shift=(next-anchor)*Pitch;VisualOffset+=shift;TargetOffset+=(int)Math.Round(shift);}VisualOffset=Math.Max(0,Math.Min(MaximumOffset(),VisualOffset));TargetOffset=Math.Max(0,Math.Min(MaximumOffset(),TargetOffset));if(Hover!=null&&!Cards.Exists(c=>c.Id==Hover))Hover=null;if(Visible)Render();}
 }
 internal void ClearImages(){DisposeImages();foreach(var card in Cards)card.ImageData=null;if(Visible)Render();}
 private static GraphicsPath Rounded(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 private static Image Decode(string data){if(String.IsNullOrEmpty(data)||data.Length>1500000)return null;try{using(var stream=new MemoryStream(Convert.FromBase64String(data.Substring(data.IndexOf(',')+1))))using(var image=Image.FromStream(stream))return new Bitmap(image);}catch{return null;}}
 private void DrawIcon(Graphics g,TablineCompanion.Card card,RectangleF bounds){
  if(card.Icon==null)card.Icon=Decode(card.IconData);
  if(card.Icon!=null){float fit=Math.Min(bounds.Width/card.Icon.Width,bounds.Height/card.Icon.Height);float w=card.Icon.Width*fit,h=card.Icon.Height*fit;g.DrawImage(card.Icon,new RectangleF(bounds.X+(bounds.Width-w)/2,bounds.Y+(bounds.Height-h)/2,w,h));return;}
  using(var path=Rounded(bounds,Math.Min(8,bounds.Width/4)))using(var fill=new SolidBrush(Color.FromArgb(58,119,190)))g.FillPath(fill,path);
  using(var pen=new Pen(Color.FromArgb(230,240,255),Math.Max(1,bounds.Width/22))){g.DrawRectangle(pen,bounds.X+bounds.Width*.23f,bounds.Y+bounds.Height*.25f,bounds.Width*.54f,bounds.Height*.43f);g.DrawLine(pen,bounds.X+bounds.Width*.35f,bounds.Bottom-bounds.Height*.21f,bounds.Right-bounds.Width*.35f,bounds.Bottom-bounds.Height*.21f);}
 }
 private void DrawAction(Graphics g,RectangleF box,bool save){
  for(int i=3;i>=1;i--){var shadow=new RectangleF(box.X-i*.4f,box.Y+1.5f-i*.4f,box.Width+i*.8f,box.Height+i*.8f);using(var path=Rounded(shadow,box.Height/2+i*.4f))using(var brush=new SolidBrush(Color.FromArgb(8,0,0,0)))g.FillPath(brush,path);}
  using(var path=Rounded(box,box.Height/2)){
   using(var fill=new LinearGradientBrush(box,Color.FromArgb(241,36,50,59),Color.FromArgb(246,19,34,42),90f))g.FillPath(fill,path);
   using(var edge=new LinearGradientBrush(box,Color.FromArgb(174,140,163,174),Color.FromArgb(112,77,105,117),90f))using(var pen=new Pen(edge,1.25f))g.DrawPath(pen,path);
  }
  var inset=new RectangleF(box.X+1.4f,box.Y+1.4f,box.Width-2.8f,box.Height-2.8f);
  using(var path=Rounded(inset,inset.Height/2))using(var light=new LinearGradientBrush(inset,Color.FromArgb(38,218,232,240),Color.FromArgb(2,135,171,185),90f))using(var pen=new Pen(light,.7f))g.DrawPath(pen,path);
  string title=save?"Save":"Copy";
  using(var font=new Font("Segoe UI",20,FontStyle.Regular,GraphicsUnit.Pixel))using(var format=(StringFormat)StringFormat.GenericTypographic.Clone()){
   format.FormatFlags|=StringFormatFlags.NoWrap;format.LineAlignment=StringAlignment.Center;
   float textWidth=g.MeasureString(title,font,100,format).Width;
   float x=box.X+(box.Width-(21+11+textWidth))/2,cy=box.Y+box.Height/2;
   using(var pen=new Pen(Color.FromArgb(231,239,245),1.9f)){
    pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
    if(save){g.DrawLine(pen,x+10.5f,cy-10,x+10.5f,cy+3);g.DrawLines(pen,new[]{new PointF(x+5,cy-2),new PointF(x+10.5f,cy+3.5f),new PointF(x+16,cy-2)});using(var tray=new GraphicsPath()){tray.AddLine(x+1,cy+4,x+1,cy+8);tray.AddArc(x+1,cy+5,6,6,180,-90);tray.AddLine(x+4,cy+11,x+17,cy+11);tray.AddArc(x+14,cy+5,6,6,90,-90);tray.AddLine(x+20,cy+8,x+20,cy+4);g.DrawPath(pen,tray);}}
    else{using(var rear=Rounded(new RectangleF(x+1,cy-11,14,17),1.6f))g.DrawPath(pen,rear);using(var front=Rounded(new RectangleF(x+6,cy-7,14,17),1.6f))g.DrawPath(pen,front);}
   }
   using(var ink=new SolidBrush(Color.FromArgb(234,240,246)))g.DrawString(title,font,ink,new RectangleF(x+32,box.Y,textWidth+2,box.Height),format);
  }
 }
 private void DrawActions(Graphics g,RectangleF r){var saved=g.Save();g.TranslateTransform(r.X,r.Y);float k=r.Width/376;g.ScaleTransform(k,k);DrawAction(g,new RectangleF(72,182,114,47),false);DrawAction(g,new RectangleF(193,182,112,47),true);g.Restore(saved);}
 private Bitmap PaintRope(){
  var bitmap=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(bitmap)){
   g.Clear(Color.Transparent);g.ScaleTransform(UiScale,UiScale);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   g.InterpolationMode=InterpolationMode.HighQualityBicubic;float width=Width/UiScale;
   // The return jaw is behind the cable; the front plate is painted over the card.
   for(int i=0;i<Cards.Count;i++){
    var r=CardBounds(i);if(r.Right<0||r.Left>width)continue;
    var saved=g.Save();g.TranslateTransform(r.X,r.Y);float k=r.Width/376;g.ScaleTransform(k,k);DrawClipRear(g,188,-CableToCard-ClipAboveCable);g.Restore(saved);
   }
   DrawCable(g);
   for(int i=0;i<Cards.Count;i++){
    var card=Cards[i];var r=CardBounds(i);bool hovered=Hover==card.Id;
    if(r.Right<-12||r.Left>width+12){if(card.Image!=null){card.Image.Dispose();card.Image=null;}if(card.Icon!=null){card.Icon.Dispose();card.Icon=null;}continue;}
    var saved=g.Save();ApplyCardTransform(g,i);
    var frame=new RectangleF(0,0,376,285);
    for(int layer=5;layer>=1;layer--){float spread=layer*.75f;using(var path=Rounded(new RectangleF(-spread,4-spread,376+spread*2,285+spread*2),18+spread))using(var shadow=new SolidBrush(Color.FromArgb(8,0,0,0)))g.FillPath(shadow,path);}
    using(var path=Rounded(frame,18)){
     using(var fill=new LinearGradientBrush(frame,Color.FromArgb(244,32,44,57),Color.FromArgb(247,20,29,39),90f))g.FillPath(fill,path);
     using(var edge=new LinearGradientBrush(frame,Color.FromArgb(150,123,145,161),Color.FromArgb(90,74,99,124),90f))using(var pen=new Pen(edge,1.2f))g.DrawPath(pen,path);
    }
    var picture=new RectangleF(13,14,350,218);
    if(card.Image==null)card.Image=Decode(card.ImageData);
    if(card.Image!=null){var clipState=g.Save();using(var clip=Rounded(picture,9))g.SetClip(clip);float zoom=Math.Max(picture.Width/card.Image.Width,picture.Height/card.Image.Height);var source=new RectangleF((card.Image.Width-picture.Width/zoom)/2,(card.Image.Height-picture.Height/zoom)/2,picture.Width/zoom,picture.Height/zoom);g.DrawImage(card.Image,picture,source,GraphicsUnit.Pixel);g.Restore(clipState);}
    else if(!card.Native||!DwmThumbnails.ContainsKey(card.Id)){
     DrawIcon(g,card,new RectangleF(157,34,62,62));
     using(var format=(StringFormat)StringFormat.GenericTypographic.Clone()){
      format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;format.Trimming=StringTrimming.EllipsisCharacter;format.FormatFlags|=StringFormatFlags.NoWrap;
      using(var font=new Font("Segoe UI",20,FontStyle.Regular,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(236,243,250)))g.DrawString(card.Title,font,ink,new RectangleF(24,108,328,28),format);
      using(var font=new Font("Segoe UI",16,FontStyle.Regular,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(163,184,205)))g.DrawString(card.Domain??card.App,font,ink,new RectangleF(24,141,328,24),format);
      if(!hovered)using(var font=new Font("Segoe UI",16,FontStyle.Regular,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(189,203,218)))g.DrawString("Capture on click",font,ink,new RectangleF(24,188,328,24),format);
     }
    }
    DrawIcon(g,card,new RectangleF(21,240,29,33));
    using(var font=new Font("Segoe UI",20,FontStyle.Regular,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(235,241,247)))using(var format=(StringFormat)StringFormat.GenericTypographic.Clone()){
     format.Trimming=StringTrimming.EllipsisCharacter;format.FormatFlags|=StringFormatFlags.NoWrap;format.LineAlignment=StringAlignment.Center;g.DrawString(card.Title,font,ink,new RectangleF(61,240,295,33),format);
    }
    if(hovered){
     using(var path=Rounded(frame,18)){
      using(var glow=new Pen(Color.FromArgb(24,74,172,245),7))g.DrawPath(glow,path);
      using(var edge=new LinearGradientBrush(frame,Color.FromArgb(108,206,250),Color.FromArgb(60,170,245),90f))using(var pen=new Pen(edge,2.3f))g.DrawPath(pen,path);
     }
     DrawAction(g,new RectangleF(72,182,114,47),false);DrawAction(g,new RectangleF(193,182,112,47),true);
    }
    g.Restore(saved);
    // The card pivots around the fastener; the folded cap stays on the cable.
    saved=g.Save();g.TranslateTransform(r.X,r.Y);float k=r.Width/376;g.ScaleTransform(k,k);DrawClip(g,188,-CableToCard-ClipAboveCable);g.Restore(saved);
   }
  }
  return bitmap;
 }
 // One analytic curve supplies both the cable ribbons and card attachment.
 private PointF[] CableBand(float offset){
  float width=Width/UiScale;int count=Math.Max(16,(int)Math.Ceiling(width/2));var points=new PointF[count+1];
  for(int i=0;i<=count;i++){
   float x=width*i/count,slope=CableSlope(x);
   float normal=(float)Math.Sqrt(1+slope*slope);
   points[i]=new PointF(x-offset*slope/normal,CableY(x)+offset/normal);
  }
  return points;
 }
 private void PaintCableBand(Graphics g,float offset,float thickness,Brush brush){using(var pen=new Pen(brush,thickness)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawLines(pen,CableBand(offset));}}
 private void DrawCable(Graphics g){
  if(CableSurface==null||CableSurfaceWidth!=Width||Math.Abs(CableSurfaceScale-UiScale)>.001f){
   if(CableSurface!=null)CableSurface.Dispose();CableSurfaceWidth=Width;CableSurfaceScale=UiScale;
   CableSurface=new Bitmap(Math.Max(1,Width),(int)Math.Ceiling(48*UiScale),PixelFormat.Format32bppArgb);
   using(var cg=Graphics.FromImage(CableSurface)){
    cg.Clear(Color.Transparent);cg.ScaleTransform(UiScale,UiScale);cg.SmoothingMode=SmoothingMode.AntiAlias;
    using(var b=new SolidBrush(Color.FromArgb(32,1,5,12)))PaintCableBand(cg,2.1f,5.8f,b);
    using(var b=new SolidBrush(Color.FromArgb(200,58,76,101)))PaintCableBand(cg,.65f,3.1f,b);
    using(var b=new SolidBrush(Color.FromArgb(236,156,173,193)))PaintCableBand(cg,0,2.75f,b);
    using(var b=new SolidBrush(Color.FromArgb(185,113,139,169)))PaintCableBand(cg,.75f,1.1f,b);
    using(var b=new SolidBrush(Color.FromArgb(235,201,213,228)))PaintCableBand(cg,-.35f,1.65f,b);
    var bounds=new RectangleF(0,0,Width/UiScale,48);
    using(var shine=new LinearGradientBrush(bounds,Color.White,Color.White,0f)){
     shine.InterpolationColors=new ColorBlend{Positions=new[]{0f,.32f,.52f,.73f,1f},Colors=new[]{Color.FromArgb(222,220,230,240),Color.FromArgb(255,247,249,252),Color.FromArgb(247,228,237,248),Color.FromArgb(255,244,248,252),Color.FromArgb(230,223,233,242)}};
     PaintCableBand(cg,-.92f,.92f,shine);
    }
    using(var b=new SolidBrush(Color.FromArgb(130,213,226,240)))PaintCableBand(cg,-1.43f,.42f,b);
   }
  }
  g.DrawImage(CableSurface,new RectangleF(0,0,Width/UiScale,CableSurface.Height/UiScale),new RectangleF(0,0,CableSurface.Width,CableSurface.Height),GraphicsUnit.Pixel);
 }
 private static void DrawClipRear(Graphics g,float center,float y){
  var rear=new RectangleF(center-6.3f,y+.9f,12.6f,10.9f);
  using(var path=Rounded(new RectangleF(rear.X+.7f,rear.Y+.7f,rear.Width,rear.Height),2.8f))using(var shadow=new SolidBrush(Color.FromArgb(88,0,0,0)))g.FillPath(shadow,path);
  using(var path=Rounded(rear,2.8f))using(var fill=new LinearGradientBrush(rear,Color.FromArgb(166,181,199),Color.FromArgb(52,65,82),90f))using(var edge=new Pen(Color.FromArgb(145,203,219,234),.65f)){g.FillPath(fill,path);g.DrawPath(edge,path);}
 }
 private static void DrawClip(Graphics g,float center,float y){
  var body=new RectangleF(center-ClipWidth/2,y,ClipWidth,ClipHeight);
  // Contact belongs to the card face, not a floating rectangular drop shadow.
  var saved=g.Save();using(var face=Rounded(new RectangleF(0,0,376,285),18))g.SetClip(face);
  for(int i=3;i>=1;i--){float spread=i*.55f;using(var path=Rounded(new RectangleF(body.X+1-spread,body.Y+2-spread,body.Width+spread*2,body.Height+spread*2),1.5f+spread))using(var brush=new SolidBrush(Color.FromArgb(18,0,0,0)))g.FillPath(brush,path);}
  g.Restore(saved);
  using(var path=Rounded(body,1.5f)){
   using(var metal=new LinearGradientBrush(body,Color.White,Color.White,0f)){
    metal.InterpolationColors=new ColorBlend{Positions=new[]{0f,.09f,.2f,.49f,.76f,.91f,1f},Colors=new[]{Color.FromArgb(77,92,112),Color.FromArgb(220,228,239),Color.FromArgb(184,195,211),Color.FromArgb(167,179,195),Color.FromArgb(192,204,219),Color.FromArgb(229,236,244),Color.FromArgb(91,108,129)}};
    g.FillPath(metal,path);
   }
   using(var edge=new Pen(Color.FromArgb(150,207,222,237),.65f))g.DrawPath(edge,path);
   var state=g.Save();g.SetClip(path);
   using(var glaze=new LinearGradientBrush(body,Color.Transparent,Color.Transparent,90f)){
    glaze.InterpolationColors=new ColorBlend{Positions=new[]{0f,.1f,.2f,.32f,.6f,.88f,1f},Colors=new[]{Color.FromArgb(35,255,255,255),Color.FromArgb(30,24,36,52),Color.FromArgb(12,39,52,72),Color.FromArgb(12,229,238,251),Color.FromArgb(35,243,248,255),Color.FromArgb(18,37,53,75),Color.FromArgb(65,36,49,68)}};
    g.FillPath(glaze,path);
   }
   g.Restore(state);
  }
  // Rolled cap: light crown, recessed fold around the rope, then front plate.
  var cap=new RectangleF(body.X+.35f,y+.15f,body.Width-.7f,4.1f);
  using(var path=Rounded(cap,1.1f))using(var fill=new LinearGradientBrush(cap,Color.FromArgb(250,251,253),Color.FromArgb(190,204,221),90f))g.FillPath(fill,path);
  using(var fold=new LinearGradientBrush(new RectangleF(body.X+.7f,y+4.1f,body.Width-1.4f,4.2f),Color.FromArgb(171,76,90,109),Color.FromArgb(214,117,134,155),90f))g.FillRectangle(fold,body.X+.7f,y+4.1f,body.Width-1.4f,4.2f);
  using(var seam=new Pen(Color.FromArgb(145,228,238,250),.7f))g.DrawLine(seam,body.X+1,y+8.6f,body.Right-1,y+8.6f);
  using(var bevel=new Pen(Color.FromArgb(193,237,244,252),.75f))g.DrawLine(bevel,body.X+1.5f,y+10,body.X+1.5f,body.Bottom-2);
  float cy=y+FastenerFromTop;
  var rivet=new RectangleF(center-2.25f,cy-2.6f,4.5f,5.2f);
  using(var seat=new SolidBrush(Color.FromArgb(85,32,43,59)))g.FillEllipse(seat,rivet.X-.5f,rivet.Y-.2f,rivet.Width+1,rivet.Height+.7f);
  using(var fill=new LinearGradientBrush(rivet,Color.FromArgb(108,123,143),Color.FromArgb(42,53,70),90f))g.FillEllipse(fill,rivet);
  using(var glint=new Pen(Color.FromArgb(223,223,235,248),.7f))g.DrawArc(glint,rivet,202,155);
  using(var shade=new Pen(Color.FromArgb(155,28,40,56),.65f))g.DrawArc(shade,rivet,20,137);
  using(var slot=new Pen(Color.FromArgb(202,36,48,64),.8f)){slot.StartCap=slot.EndCap=LineCap.Round;g.DrawLine(slot,center-.9f,cy-.6f,center+.7f,cy+.7f);}
 }
 private void PresentRope(){using(var bitmap=PaintRope())LayeredCanvas.Present(this,bitmap,255);}
 private void Render(){if(!Visible)return;PresentRope();SyncThumbnails();}
 // DWM reuses the desktop compositor's existing window surfaces; no WGC stream or bitmap capture.
 private void SyncThumbnails(){
  var visible=new HashSet<string>();
  for(int i=0;i<Cards.Count;i++){
   var card=Cards[i];if(!card.Native)continue;var r=CardBounds(i);var p=PictureBounds(r);var photo=Rectangle.FromLTRB((int)Math.Round(p.Left*UiScale),(int)Math.Round(p.Top*UiScale),(int)Math.Round(p.Right*UiScale),(int)Math.Round(p.Bottom*UiScale));var clipped=Rectangle.Intersect(photo,new Rectangle(0,0,Width,Height));if(clipped.Width<1||clipped.Height<1)continue;
   visible.Add(card.Id);AppThumbnailWindow window;
   if(!DwmThumbnails.TryGetValue(card.Id,out window)){window=new AppThumbnailWindow(this,card);DwmThumbnails[card.Id]=window;}
   if(!window.Place(photo,clipped,9*r.Width/376*UiScale)){window.Dispose();DwmThumbnails.Remove(card.Id);}
   else{
    // Corner masks and controls live above the DWM surface; the image itself
    // stays full-height. Only these tiny UI overlays are retained, not captures.
    bool hovered=Hover==card.Id;
    if(window.NeedsOverlay(photo.Size,hovered)){
     using(var surface=new Bitmap(Math.Max(1,photo.Width),Math.Max(1,photo.Height),PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(surface)){
      g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
      float radius=9*r.Width/376*UiScale;
      using(var corners=new GraphicsPath(FillMode.Alternate)){
       corners.AddRectangle(new RectangleF(0,0,photo.Width,photo.Height));
       var q=new RectangleF(0,0,photo.Width,photo.Height);using(var inside=Rounded(q,radius))corners.AddPath(inside,false);
       using(var fill=new LinearGradientBrush(q,Color.FromArgb(31,43,55),Color.FromArgb(23,31,41),90f))g.FillPath(fill,corners);
      }
      g.TranslateTransform(-photo.X,-photo.Y);g.ScaleTransform(UiScale,UiScale);if(hovered)DrawActions(g,r);
      window.ShowActions(surface,new Rectangle(Left+photo.Left,Top+photo.Top,photo.Width,photo.Height),new Rectangle(Left,Top,Width,Height),hovered);
     }
    }else window.ShowActions(null,new Rectangle(Left+photo.Left,Top+photo.Top,photo.Width,photo.Height),new Rectangle(Left,Top,Width,Height),hovered);
   }
  }
  var obsolete=new List<string>();foreach(var pair in DwmThumbnails)if(!visible.Contains(pair.Key))obsolete.Add(pair.Key);
  foreach(string id in obsolete){DwmThumbnails[id].Dispose();DwmThumbnails.Remove(id);}
 }
 internal void HoverFromPreview(){if(!Animating)UpdateHover(PointToClient(Cursor.Position));}
 private void ReleaseThumbnails(){foreach(var window in DwmThumbnails.Values)window.Dispose();DwmThumbnails.Clear();}
 internal void SaveRendering(string path){using(var bitmap=PaintRope())bitmap.Save(path,ImageFormat.Png);}
 internal string RenderingData(){using(var bitmap=PaintRope())using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}}
 internal string CompositedData(){
  SetWindowDisplayAffinity(Handle,0);foreach(var window in DwmThumbnails.Values)window.ExcludeFromCapture(false);
  try{Render();Application.DoEvents();System.Threading.Thread.Sleep(60);DwmFlush();using(var bitmap=LayeredCanvas.ReadScreen(Bounds))using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}}
  finally{SetWindowDisplayAffinity(Handle,0x11);foreach(var window in DwmThumbnails.Values)window.ExcludeFromCapture(true);}
 }
 protected override void Dispose(bool disposing){if(disposing){if(CableSurface!=null){CableSurface.Dispose();CableSurface=null;}ReleaseThumbnails();InputTimer.Dispose();MotionTimer.Dispose();Tip.Dispose();DisposeImages();}base.Dispose(disposing);}
}







