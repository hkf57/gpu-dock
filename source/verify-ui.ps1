$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Windows.Forms,System.Drawing @'
using System;using System.Text;using System.Runtime.InteropServices;using System.Windows.Forms;using System.Drawing;using System.Threading;
public class DockVerify {
 delegate bool CB(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]static extern bool EnumWindows(CB c,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int n);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]static extern bool AttachThreadInput(uint a,uint b,bool yes);
 static void Focus(IntPtr h){uint id;uint other=GetWindowThreadProcessId(GetForegroundWindow(),out id);uint me=GetCurrentThreadId();bool attached=AttachThreadInput(me,other,true);try{SetForegroundWindow(h);}finally{if(attached)AttachThreadInput(me,other,false);}}
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point p);
 struct RECT {public int Left,Top,Right,Bottom;}
 static void Wait(int ms){var end=DateTime.Now.AddMilliseconds(ms);while(DateTime.Now<end){Application.DoEvents();Thread.Sleep(25);}}
 public static string Run(){SetProcessDPIAware();IntPtr dock=IntPtr.Zero;EnumWindows(delegate(IntPtr h,IntPtr l){var b=new StringBuilder(256);GetWindowText(h,b,256);if(b.ToString()=="GPU Dock")dock=h;return true;},IntPtr.Zero);
 if(dock==IntPtr.Zero)throw new Exception("GPU Dock window not found");
 var output=new StringBuilder();int style=GetWindowLong(dock,-20);bool flags=(style&0x080800A0)==0x080800A0;output.AppendLine("Click-through, layered, no-activate, toolwindow styles: "+flags);
 RECT r;GetWindowRect(dock,out r);output.AppendLine("Position: "+r.Left+","+r.Top+" to "+r.Right+","+r.Bottom);
 using(var f=new Form()){f.Text="GPU Dock verification (temporary)";f.StartPosition=FormStartPosition.Manual;f.Bounds=new Rectangle(100,400,400,150);f.Show();Focus(f.Handle);Wait(1600);bool before=IsWindowVisible(dock);output.AppendLine("Visible with regular foreground window: "+before);
 bool passes=WindowFromPoint(new Point(r.Left+20,r.Top+15))!=dock;output.AppendLine("Mouse hit testing passes through panel: "+passes);
 f.FormBorderStyle=FormBorderStyle.None;f.Bounds=Screen.PrimaryScreen.Bounds;Focus(f.Handle);Wait(1400);output.AppendLine("Fullscreen fixture has focus: "+(GetForegroundWindow()==f.Handle));bool hidden=!IsWindowVisible(dock);output.AppendLine("Hidden with fullscreen foreground window: "+hidden);
 f.Bounds=new Rectangle(100,400,400,150);Wait(1800);bool returned=IsWindowVisible(dock);output.AppendLine("Returned after leaving fullscreen: "+returned);f.Close();
 if(!flags||!before||!passes||!hidden||!returned)throw new Exception(output.ToString());}
 return output.ToString();}
}
'@
[DockVerify]::Run() | Set-Content (Join-Path $PSScriptRoot '..\ui-checks.txt')
