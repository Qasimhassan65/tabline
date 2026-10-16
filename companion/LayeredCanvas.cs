using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
internal static class LayeredCanvas {
 [StructLayout(LayoutKind.Sequential)] private struct PointN{public int X,Y;public PointN(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)] private struct SizeN{public int X,Y;public SizeN(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential,Pack=1)] private struct Blend{public byte Operation,Flags,Alpha,Format;}
 [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dst,ref PointN pos,ref SizeN size,IntPtr src,ref PointN source,int color,ref Blend blend,int flags);
 [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
 [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
 [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
 [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr destination,int x,int y,int width,int height,IntPtr source,int sourceX,int sourceY,uint flags);
 internal static Bitmap ReadScreen(Rectangle bounds){
  var bitmap=new Bitmap(bounds.Width,bounds.Height);IntPtr screen=GetDC(IntPtr.Zero);
  try{using(var g=Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{if(!BitBlt(dc,0,0,bounds.Width,bounds.Height,screen,bounds.X,bounds.Y,0x40CC0020))throw new InvalidOperationException("Windows could not read the screen.");}finally{g.ReleaseHdc(dc);}}return bitmap;}
  catch{bitmap.Dispose();throw;}finally{ReleaseDC(IntPtr.Zero,screen);}
 }
 internal static void Present(Form form,Bitmap bitmap,byte alpha){
  IntPtr screen=GetDC(IntPtr.Zero),memory=CreateCompatibleDC(screen),handle=bitmap.GetHbitmap(Color.FromArgb(0)),old=SelectObject(memory,handle);
  try{var position=new PointN(form.Left,form.Top);var size=new SizeN(bitmap.Width,bitmap.Height);var source=new PointN(0,0);var blend=new Blend{Alpha=alpha,Format=1};UpdateLayeredWindow(form.Handle,screen,ref position,ref size,memory,ref source,0,ref blend,2);}
  finally{SelectObject(memory,old);DeleteObject(handle);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
 }
}
