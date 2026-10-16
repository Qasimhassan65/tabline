using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
internal sealed class ToastForm:Form {
 private readonly Timer Timer=new Timer{Interval=16};
 private readonly Stopwatch Clock=new Stopwatch();
 private Bitmap Surface;
 private bool Pending;
 private float UiScale=1;
 internal string LastTitle;
 internal bool LastSuccess;
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;return p;}}
 internal ToastForm(){Text="Tabline notification";FormBorderStyle=FormBorderStyle.None;TopMost=true;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Timer.Tick+=(s,e)=>Animate();}
 internal void ShowNotice(bool success,string detail,Screen screen){ShowStatus(success?"Screenshot copied":"Screenshot failed",success,false,screen.WorkingArea);}
 internal void ShowStatus(string title,bool success,bool pending,Rectangle window){
  Timer.Stop();Timer.Interval=16;Pending=pending;LastSuccess=success;LastTitle=title;
  float scale;using(var g=Graphics.FromHwnd(TablineCompanion.GetForegroundWindow()))scale=Math.Max(1,g.DpiX/96f);
  var area=Screen.FromHandle(TablineCompanion.GetForegroundWindow()).WorkingArea;if(window.Width<100||window.Height<100)window=area;window=Rectangle.Intersect(window,area);if(window.Width<100)window=area;
  UiScale=scale;Size=new Size((int)Math.Ceiling(297*scale),(int)Math.Ceiling(78*scale));Location=new Point(Math.Max(area.Left,Math.Min(area.Right-Width,window.Left+(window.Width-Width)/2)),Math.Max(area.Top,window.Bottom-Height-(int)(42*scale)));
  if(Surface!=null)Surface.Dispose();Surface=DrawNotice(success,null,scale);Clock.Restart();Show();SetWindowDisplayAffinity(Handle,0x11);LayeredCanvas.Present(this,Surface,0);Timer.Start();
 }
 [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
 private void Animate(){
  double ms=Clock.Elapsed.TotalMilliseconds;
  if(Pending){if(ms>10000){Dismiss();return;}if(Surface!=null)Surface.Dispose();Surface=DrawNotice(false,null,UiScale);LayeredCanvas.Present(this,Surface,255);Timer.Interval=60;return;}
  if(ms>=2900){Dismiss();return;}
  double opacity=ms<140?ms/140:ms>2600?(2900-ms)/300:1;
  if(ms>160&&ms<2560){if(Surface!=null)LayeredCanvas.Present(this,Surface,255);Timer.Interval=2400;return;}Timer.Interval=16;
  if(Surface!=null)LayeredCanvas.Present(this,Surface,(byte)Math.Max(0,Math.Min(255,opacity*255)));
 }
 internal void Dismiss(){Timer.Stop();Hide();if(Surface!=null){Surface.Dispose();Surface=null;}}
 internal string RenderingData(){if(Surface==null)return null;using(var stream=new MemoryStream()){Surface.Save(stream,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}}
 internal void SaveRendering(string path){if(Surface!=null)Surface.Save(path,ImageFormat.Png);}
 private static GraphicsPath Rounded(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 private Bitmap DrawNotice(bool success,string detail,float scale){
  var image=new Bitmap(Width,Height,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(image)){
   g.Clear(Color.Transparent);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   // A legible tinted material, with edge light separated from the content.
   // All geometry is in logical pixels; the existing transform applies DPI once.
   var capsule=new RectangleF(14,8,269,54);
   for(int layer=7;layer>=1;layer--){
    float spread=layer*.65f;
    var shadow=new RectangleF(capsule.X-spread,capsule.Y+3-spread,capsule.Width+spread*2,capsule.Height+spread*2);
    using(var path=Rounded(shadow,27+spread))using(var brush=new SolidBrush(Color.FromArgb(5,0,0,0)))g.FillPath(brush,path);
   }
   Color accent=success?Color.FromArgb(103,221,88):Color.FromArgb(244,88,78);
   using(var path=Rounded(capsule,27)){
    using(var fill=new LinearGradientBrush(capsule,Color.FromArgb(239,33,43,54),Color.FromArgb(247,21,29,38),90f))g.FillPath(fill,path);
    var saved=g.Save();g.SetClip(path);
    using(var lightPath=new GraphicsPath()){
     lightPath.AddEllipse(capsule.X-34,capsule.Y-40,218,106);
     using(var light=new PathGradientBrush(lightPath)){light.CenterColor=Color.FromArgb(17,185,207,229);light.SurroundColors=new[]{Color.FromArgb(0,185,207,229)};g.FillPath(light,lightPath);}
    }
    if(!Pending){
     using(var tintPath=new GraphicsPath()){
      tintPath.AddEllipse(capsule.X-28,capsule.Y-12,117,78);
      using(var tint=new PathGradientBrush(tintPath)){tint.CenterColor=Color.FromArgb(success?23:16,accent);tint.SurroundColors=new[]{Color.FromArgb(0,accent)};g.FillPath(tint,tintPath);}
     }
    }
    g.Restore(saved);
    Color upper=!Pending&&!success?Color.FromArgb(174,176,78,76):Color.FromArgb(121,100,117,132);
    Color lower=!Pending&&!success?Color.FromArgb(136,149,62,64):Color.FromArgb(75,84,101,117);
    using(var edge=new LinearGradientBrush(capsule,upper,lower,90f))using(var pen=new Pen(edge,1.25f))g.DrawPath(pen,path);
   }
   var inset=new RectangleF(capsule.X+1.25f,capsule.Y+1.25f,capsule.Width-2.5f,capsule.Height-2.5f);
   using(var path=Rounded(inset,25.75f))using(var light=new LinearGradientBrush(inset,Color.FromArgb(38,196,215,233),Color.FromArgb(4,125,151,174),90f))using(var pen=new Pen(light,.75f))g.DrawPath(pen,path);
   if(!Pending&&success){
    var saved=g.Save();using(var clip=Rounded(capsule,27))g.SetClip(clip);
    using(var tint=new Pen(Color.FromArgb(61,113,191,108),1.1f))g.DrawArc(tint,capsule.X+.4f,capsule.Y+.4f,53.2f,53.2f,113,139);
    g.Restore(saved);
   }
   const float centerX=48,centerY=35;
   if(Pending){
    using(var track=new Pen(Color.FromArgb(63,157,178,198),1.8f))g.DrawEllipse(track,centerX-12.5f,centerY-12.5f,25,25);
    using(var arc=new Pen(Color.FromArgb(224,209,226,242),1.9f)){arc.StartCap=arc.EndCap=LineCap.Round;g.DrawArc(arc,centerX-12.5f,centerY-12.5f,25,25,(float)(Clock.Elapsed.TotalMilliseconds*.3%360)-110,272);}
   }else{
    using(var glow=new SolidBrush(Color.FromArgb(19,accent)))g.FillEllipse(glow,centerX-15.5f,centerY-15.5f,31,31);
    var disk=new RectangleF(centerX-13.5f,centerY-13.5f,27,27);
    Color lighter=success?Color.FromArgb(117,231,101):Color.FromArgb(253,106,95);
    using(var fill=new LinearGradientBrush(disk,lighter,accent,90f))g.FillEllipse(fill,disk);
    using(var edge=new Pen(Color.FromArgb(65,205,245,208),.7f))g.DrawEllipse(edge,disk);
    using(var pen=new Pen(Color.FromArgb(30,49,40),2.35f)){
     pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
     if(success)g.DrawLines(pen,new[]{new PointF(centerX-5.4f,centerY+.2f),new PointF(centerX-1.3f,centerY+4.2f),new PointF(centerX+6,centerY-4.2f)});
     else{g.DrawLine(pen,centerX-4.2f,centerY-4.2f,centerX+4.2f,centerY+4.2f);g.DrawLine(pen,centerX+4.2f,centerY-4.2f,centerX-4.2f,centerY+4.2f);}
    }
   }
   using(var font=new Font("Segoe UI",20,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(Color.FromArgb(229,235,242)))using(var format=(StringFormat)StringFormat.GenericTypographic.Clone()){
    format.Alignment=StringAlignment.Near;format.LineAlignment=StringAlignment.Center;format.FormatFlags|=StringFormatFlags.NoWrap;format.Trimming=StringTrimming.EllipsisCharacter;
    g.DrawString(LastTitle,font,brush,new RectangleF(82,8,190,54),format);
   }
  }
  return image;
 }
 protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
 protected override void Dispose(bool disposing){if(disposing){Timer.Dispose();if(Surface!=null)Surface.Dispose();}base.Dispose(disposing);}
}

