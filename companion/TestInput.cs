using System;using System.Runtime.InteropServices;using System.Drawing;
internal static class TestInput {
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 internal static void FocusOwnedWindow(System.Windows.Forms.Form form){form.TopMost=true;var point=new Point(form.Left+20,form.Top+45);if(WindowFromPoint(point)!=form.Handle)throw new InvalidOperationException("Test window is covered; no input was sent.");SetCursorPos(point.X,point.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);System.Windows.Forms.Application.DoEvents();}
}
