using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
// DWM thumbnails need an ordinary compositor destination, separate from the alpha rope.
internal sealed class AppThumbnailWindow:Form {
 [StructLayout(LayoutKind.Sequential)]private struct RectN{public int Left,Top,Right,Bottom;public RectN(Rectangle r){Left=r.Left;Top=r.Top;Right=r.Right;Bottom=r.Bottom;}}
 [StructLayout(LayoutKind.Sequential)]private struct SizeN{public int Width,Height;}
 [StructLayout(LayoutKind.Sequential)]private struct Properties{public uint Flags;public RectN Destination,Source;public byte Opacity;[MarshalAs(UnmanagedType.Bool)]public bool Visible;[MarshalAs(UnmanagedType.Bool)]public bool ClientOnly;}
 [DllImport("dwmapi.dll")]private static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
 [DllImport("dwmapi.dll")]private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
 [DllImport("dwmapi.dll")]private static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail,out SizeN size);
 [DllImport("dwmapi.dll")]private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref Properties properties);
 [DllImport("user32.dll")]private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
 private IntPtr Thumbnail;
 private readonly RopeForm Rope;
 private readonly TablineCompanion.Card Card;
 private PreviewActionsWindow Actions;
 private Size OverlaySize;
 private byte OverlayOpacity;
 private Rectangle RoundedBounds;
 private float RoundedRadius;
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80|0x08000000;return p;}}
 internal AppThumbnailWindow(RopeForm rope,TablineCompanion.Card card){Rope=rope;Card=card;Owner=rope;Text="Tabline app preview";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(28,35,44);Cursor=Cursors.Hand;}
 internal bool Place(Rectangle photo,Rectangle clipped,float radius){
  if(Thumbnail==IntPtr.Zero){if(DwmRegisterThumbnail(Handle,Card.Window,out Thumbnail)!=0)return false;}
  SizeN size;if(DwmQueryThumbnailSourceSize(Thumbnail,out size)!=0||size.Width<1||size.Height<1)return false;
  Bounds=new Rectangle(Rope.Left+clipped.Left,Rope.Top+clipped.Top,clipped.Width,clipped.Height);
  var rounded=new Rectangle(photo.Left-clipped.Left,photo.Top-clipped.Top,photo.Width,photo.Height);
  if(rounded!=RoundedBounds||Math.Abs(radius-RoundedRadius)>.01f){
   RoundedBounds=rounded;RoundedRadius=radius;
   using(var path=new GraphicsPath()){
    float d=radius*2;path.AddArc(rounded.Left,rounded.Top,d,d,180,90);path.AddArc(rounded.Right-d,rounded.Top,d,d,270,90);path.AddArc(rounded.Right-d,rounded.Bottom-d,d,d,0,90);path.AddArc(rounded.Left,rounded.Bottom-d,d,d,90,90);path.CloseFigure();
    var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose();
   }
  }
  double zoom=Math.Max((double)photo.Width/size.Width,(double)photo.Height/size.Height),sourceWidth=photo.Width/zoom,sourceHeight=photo.Height/zoom;
  var source=new Rectangle((int)((size.Width-sourceWidth)/2+(clipped.Left-photo.Left)/zoom),(int)((size.Height-sourceHeight)/2+(clipped.Top-photo.Top)/zoom),(int)(clipped.Width/zoom),(int)(clipped.Height/zoom));
  var properties=new Properties{Flags=31,Destination=new RectN(new Rectangle(0,0,Width,Height)),Source=new RectN(source),Opacity=255,Visible=true,ClientOnly=false};
  if(DwmUpdateThumbnailProperties(Thumbnail,ref properties)!=0)return false;
  if(!Visible){Show();SetWindowDisplayAffinity(Handle,0x11);}return true;
 }
 internal bool NeedsOverlay(Size size,byte opacity){return Actions==null||OverlaySize!=size||OverlayOpacity!=opacity;}
 internal void ShowActions(Bitmap surface,Rectangle bounds,Rectangle ropeBounds,byte opacity){if(Actions==null)Actions=new PreviewActionsWindow(Rope,this);if(surface!=null){OverlaySize=surface.Size;OverlayOpacity=opacity;}Actions.Place(surface,bounds,ropeBounds);}
 internal void HideActions(){if(Actions!=null)Actions.Hide();}
 internal void ExcludeFromCapture(bool exclude){SetWindowDisplayAffinity(Handle,exclude?0x11u:0);if(Actions!=null)Actions.ExcludeFromCapture(exclude);}
 protected override void OnMouseMove(MouseEventArgs e){Rope.HoverFromPreview();base.OnMouseMove(e);}
 protected override void OnMouseUp(MouseEventArgs e){if(e.Button==MouseButtons.Left)Rope.ClickAt(Rope.PointToClient(PointToScreen(e.Location)));base.OnMouseUp(e);}
 protected override void OnMouseWheel(MouseEventArgs e){Rope.ScrollCards(-e.Delta*2);base.OnMouseWheel(e);}
 protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}if(m.Msg==0x020E){int delta=(short)((m.WParam.ToInt64()>>16)&65535);Rope.ScrollCards(delta*2);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 protected override void Dispose(bool disposing){if(disposing&&Actions!=null){Actions.Dispose();Actions=null;}if(Thumbnail!=IntPtr.Zero){DwmUnregisterThumbnail(Thumbnail);Thumbnail=IntPtr.Zero;}base.Dispose(disposing);}
}

// Small alpha overlay above an intact DWM thumbnail. Transparent pixels pass
// input to the preview; the same root-card geometry handles both button clicks.
internal sealed class PreviewActionsWindow:Form {
 [DllImport("user32.dll")]private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
 private readonly RopeForm Rope;
 private Bitmap Surface;
 private Rectangle LastCrop;
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;return p;}}
 internal PreviewActionsWindow(RopeForm rope,Form owner){Rope=rope;Owner=owner;Text="Tabline preview actions";FormBorderStyle=FormBorderStyle.None;TopMost=true;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Cursor=Cursors.Hand;}
 internal void Place(Bitmap image,Rectangle complete,Rectangle limit){
  bool changed=image!=null;if(changed){if(Surface!=null)Surface.Dispose();Surface=new Bitmap(image);}
  if(Surface==null)return;
  var clipped=Rectangle.Intersect(complete,limit);if(clipped.Width<1||clipped.Height<1){Hide();return;}
  Bounds=clipped;if(!Visible){Show();SetWindowDisplayAffinity(Handle,0x11);changed=true;}
  var crop=new Rectangle(clipped.Left-complete.Left,clipped.Top-complete.Top,clipped.Width,clipped.Height);
  if(!changed&&crop==LastCrop)return;LastCrop=crop;
  if(clipped==complete)LayeredCanvas.Present(this,Surface,255);
  else using(var cropped=new Bitmap(clipped.Width,clipped.Height,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(cropped)){g.DrawImageUnscaled(Surface,complete.Left-clipped.Left,complete.Top-clipped.Top);LayeredCanvas.Present(this,cropped,255);}
 }
 protected override void Dispose(bool disposing){if(disposing&&Surface!=null){Surface.Dispose();Surface=null;}base.Dispose(disposing);}
 internal void ExcludeFromCapture(bool exclude){SetWindowDisplayAffinity(Handle,exclude?0x11u:0);}
 protected override void OnMouseMove(MouseEventArgs e){Rope.HoverFromPreview();base.OnMouseMove(e);}
 protected override void OnMouseUp(MouseEventArgs e){if(e.Button==MouseButtons.Left)Rope.ClickAt(Rope.PointToClient(PointToScreen(e.Location)));base.OnMouseUp(e);}
 protected override void OnMouseWheel(MouseEventArgs e){Rope.ScrollCards(-e.Delta*2);base.OnMouseWheel(e);}
 protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}if(m.Msg==0x020E){int delta=(short)((m.WParam.ToInt64()>>16)&65535);Rope.ScrollCards(delta*2);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
}
