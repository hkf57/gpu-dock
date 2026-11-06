using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace GpuDock {
public class Settings {
 public int Right = 24, Top = 68, Rows = 3, Monitor = 0;
 public float FontSize = 14;
 public bool AutoHide = true;
 public string Adapter = "";
 public string AdapterName = "";
 public List<string> Games = new List<string>();
 public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GpuDock"); } }
 public static Settings Load() { try { using(var f=File.OpenRead(Path.Combine(Folder,"settings.xml"))) return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(f); } catch { return new Settings(); } }
 public void Save() { Directory.CreateDirectory(Folder); using(var f=File.Create(Path.Combine(Folder,"settings.xml"))) new XmlSerializer(typeof(Settings)).Serialize(f,this); }
}
public class Adapter { public string Name, Luid; public override string ToString() { return Name; } }
public static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Desc {
  [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Description;
  public uint Vendor, Device, SubSys, Revision; public UIntPtr DedicatedVideo, DedicatedSystem, Shared;
  public uint Low; public int High; public uint Flags;
 }
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumAdapters(IntPtr self,uint index,out IntPtr adapter);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDesc(IntPtr self,out Desc desc);
 [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(ref Guid iid,out IntPtr factory);
 public static List<Adapter> Adapters() {
  var result=new List<Adapter>(); IntPtr factory=IntPtr.Zero;
  try { var iid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387"); Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref iid,out factory));
   var enumerate=(EnumAdapters)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory),12*IntPtr.Size),typeof(EnumAdapters));
   for(uint i=0;i<16;i++) { IntPtr a; if(enumerate(factory,i,out a)!=0) break; try {
    var get=(GetDesc)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(a),10*IntPtr.Size),typeof(GetDesc)); Desc d;
    if(get(a,out d)==0 && (d.Flags&2)==0) result.Add(new Adapter {Name=d.Description,Luid=String.Format("luid_0x{0:x8}_0x{1:x8}",d.High,d.Low)});
   } finally { Marshal.Release(a); } }
  } catch {} finally { if(factory!=IntPtr.Zero) Marshal.Release(factory); } return result;
 }
 [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte Operation,Flags,Alpha,Format;}
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h,IntPtr dc);
 [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
 [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
 [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dst,ref Point pos,ref Size size,IntPtr src,ref Point origin,uint key,ref Blend blend,uint flags);
 public static void Present(IntPtr handle,Point position,Bitmap bitmap) {
  IntPtr dc=GetDC(IntPtr.Zero), memory=CreateCompatibleDC(dc), hb=IntPtr.Zero,old=IntPtr.Zero;
  try {hb=bitmap.GetHbitmap(Color.FromArgb(0));old=SelectObject(memory,hb);var size=bitmap.Size;var origin=Point.Empty;var blend=new Blend{Operation=0,Alpha=255,Format=1};UpdateLayeredWindow(handle,dc,ref position,ref size,memory,ref origin,0,ref blend,2);}
  finally {if(old!=IntPtr.Zero)SelectObject(memory,old);if(hb!=IntPtr.Zero)DeleteObject(hb);DeleteDC(memory);ReleaseDC(IntPtr.Zero,dc);}
 }
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int n);
 [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h,int n,int v);
 [DllImport("user32.dll")] public static extern bool ReleaseCapture();
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 public static bool Covers(Rect r,Rectangle screen) { return r.Left<=screen.Left+2 && r.Top<=screen.Top+2 && r.Right>=screen.Right-2 && r.Bottom>=screen.Bottom-2; }
 public static bool GamePath(string p) { p=p.ToLowerInvariant(); return p.Contains("\\steamapps\\common\\") || p.Contains("\\xboxgames\\") || p.Contains("\\epic games\\"); }
 public static string ForegroundExe() { try { uint id; GetWindowThreadProcessId(GetForegroundWindow(),out id); using(var p=Process.GetProcessById((int)id)) return p.MainModule.FileName; } catch { return ""; } }
 public static string HideReason(Settings settings) {
  if(!settings.AutoHide) return "";
  var h=GetForegroundWindow(); if(h==IntPtr.Zero) return "";
  var c=new StringBuilder(256); GetClassName(h,c,c.Capacity);
  if(new[]{"Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd"}.Contains(c.ToString())) return "";
  uint id; GetWindowThreadProcessId(h,out id); if(id==(uint)Process.GetCurrentProcess().Id) return "";
  string path=ForegroundExe();
  if(path.Length>0 && (GamePath(path) || settings.Games.Contains(path,StringComparer.OrdinalIgnoreCase))) return "Foreground game";
  int state; if(SHQueryUserNotificationState(out state)==0 && state==3) return "Exclusive fullscreen";
  Rect r; if(GetWindowRect(h,out r) && Covers(r,Screen.FromHandle(h).Bounds)) return "Fullscreen app";
  return "";
 }
}
public class Row { public string Name; public double Usage, Memory; public bool MemoryValid=true; public Dictionary<string,double> Engines=new Dictionary<string,double>(); }
public class Snapshot { public List<Row> Rows=new List<Row>(); public string Error=""; public DateTime Time=DateTime.Now; }
public class Sampler {
 Dictionary<string,CounterSample> previous=new Dictionary<string,CounterSample>();
 Dictionary<string,double> smoothing=new Dictionary<string,double>();
 static Regex pidPattern=new Regex(@"pid_(\d+)_",RegexOptions.Compiled|RegexOptions.IgnoreCase);
 public string Adapter="";
 public Snapshot Read() {
  var snapshot=new Snapshot(); var groups=new Dictionary<string,Row>(); var names=new Dictionary<int,string>();
  Func<string,Row> rowFor=delegate(string instance) {
   var match=pidPattern.Match(instance); int id; if(!match.Success || !Int32.TryParse(match.Groups[1].Value,out id)) return null;
   string name; if(!names.TryGetValue(id,out name)) { try { using(var p=Process.GetProcessById(id)) name=p.ProcessName; } catch { name="PID "+id; } names[id]=name; }
   Row row; if(!groups.TryGetValue(name,out row)) { row=new Row{Name=name}; groups[name]=row; } return row;
  };
  try {
   string filter=Adapter;
   var engineData=new PerformanceCounterCategory("GPU Engine").ReadCategory()["Utilization Percentage"];
   var current=new Dictionary<string,CounterSample>();
   foreach(InstanceData entry in engineData.Values) {
    string key=entry.InstanceName.ToLowerInvariant(); if(filter.Length>0 && !key.Contains(filter)) continue;
    current[key]=entry.Sample; CounterSample old;
    if(!previous.TryGetValue(key,out old)) continue;
    double value=Math.Max(0,Math.Min(100,CounterSample.Calculate(old,entry.Sample)));
    if(Double.IsNaN(value)||Double.IsInfinity(value)) continue;
    Row row=rowFor(key); if(row==null) continue;
    string engine=Regex.Replace(key,@"^pid_\d+_",""); double total;
    row.Engines.TryGetValue(engine,out total); row.Engines[engine]=total+value;
   }
   previous=current;
   var memory=new PerformanceCounterCategory("GPU Process Memory").ReadCategory()["Dedicated Usage"];
   foreach(InstanceData entry in memory.Values) {
    string key=entry.InstanceName.ToLowerInvariant(); if(filter.Length>0&&!key.Contains(filter)) continue;
    Row row=rowFor(key); if(row!=null) row.Memory+=Math.Max(0,entry.Sample.RawValue);
   }
   double adapterUsed=0;
   var totals=new PerformanceCounterCategory("GPU Adapter Memory").ReadCategory()["Dedicated Usage"];
   foreach(InstanceData entry in totals.Values) if(filter.Length==0||entry.InstanceName.ToLowerInvariant().Contains(filter)) adapterUsed+=Math.Max(0,entry.Sample.RawValue);
   foreach(var row in groups.Values) if(row.Memory>adapterUsed+64*1024*1024) {row.MemoryValid=false;row.Memory=0;}
   var nextSmooth=new Dictionary<string,double>();
   foreach(var row in groups.Values) {
    double actual=row.Engines.Count==0?0:Math.Min(100,row.Engines.Values.Max()); double last;
    row.Usage=smoothing.TryGetValue(row.Name,out last)?0.45*actual+0.55*last:actual;
    nextSmooth[row.Name]=row.Usage;
   }
   smoothing=nextSmooth;
   snapshot.Rows=groups.Values.Where(r=>r.Usage>=0.3 || r.Memory>=64*1024*1024).OrderByDescending(r=>Math.Round(r.Usage/2)).ThenByDescending(r=>r.Memory).ToList();
  } catch(Exception e) { snapshot.Error=e.Message; }
  return snapshot;
 }
}
public class Dock : Form {
 readonly Settings settings; readonly List<Adapter> adapters; readonly Sampler sampler=new Sampler();
 Snapshot snapshot=new Snapshot(); NotifyIcon tray; System.Windows.Forms.Timer visibilityTimer, sampleTimer; int reading;
 bool adjusting, paused; DateTime showAfter=DateTime.MinValue; string hidden=""; Font textFont;
 public Dock(Settings s) {
  settings=s; adapters=Native.Adapters(); if(settings.Adapter=="" && adapters.Count>0) settings.Adapter=(adapters.FirstOrDefault(a=>a.Name.IndexOf("NVIDIA",StringComparison.OrdinalIgnoreCase)>=0)??adapters[0]).Luid;
  if(settings.Adapter!="" && !adapters.Any(a=>a.Luid==settings.Adapter)) settings.Adapter=adapters.Count>0?(adapters.FirstOrDefault(a=>a.Name==settings.AdapterName)??adapters.FirstOrDefault(a=>a.Name.Contains("NVIDIA"))??adapters[0]).Luid:"";
  var selected=adapters.FirstOrDefault(a=>a.Luid==settings.Adapter); if(selected!=null) settings.AdapterName=selected.Name;
  sampler.Adapter=settings.Adapter;
  Text="GPU Dock"; Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); StartPosition=FormStartPosition.Manual; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
  BackColor=Color.Fuchsia; DoubleBuffered=true;
  SetFont(); Position(); tray=new NotifyIcon { Icon=Icon, Text="GPU Dock", Visible=true };
  tray.MouseDoubleClick+=delegate { SetAdjusting(!adjusting); }; BuildMenu(); visibilityTimer=new System.Windows.Forms.Timer {Interval=250}; visibilityTimer.Tick+=delegate { CheckVisibility(); }; visibilityTimer.Start();
  sampleTimer=new System.Windows.Forms.Timer {Interval=1500}; sampleTimer.Tick+=delegate { Collect(); }; sampleTimer.Start();
  Shown+=delegate { Render(); Native.RegisterHotKey(Handle,1,0x4003,0x47); Collect(); CheckVisibility(); };
  MouseDown+=delegate(object sender,MouseEventArgs e) { if(adjusting&&e.Button==MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero); SavePosition(); } };
  MouseDoubleClick+=delegate { if(adjusting) SetAdjusting(false); };
 }

 protected override bool ShowWithoutActivation {get{return true;}}
 protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x08000000|0x80|0x80000; if(!adjusting) p.ExStyle|=0x20; return p; } }
 void SetFont() { if(textFont!=null) textFont.Dispose(); textFont=new Font("Segoe UI",settings.FontSize,FontStyle.Bold,GraphicsUnit.Point); Size=new Size((int)(settings.FontSize*34),(int)(settings.FontSize*2.2)*(settings.Rows+1)); }
 void Position() { var screens=Screen.AllScreens; int m=Math.Max(0,Math.Min(screens.Length-1,settings.Monitor)); var b=screens[m].WorkingArea; Location=new Point(Math.Max(b.Left,b.Right-settings.Right-Width),Math.Max(b.Top,Math.Min(b.Bottom-Height,b.Top+settings.Top))); }
 void SavePosition() { var screen=Screen.FromControl(this); settings.Monitor=Array.IndexOf(Screen.AllScreens,screen); settings.Right=Math.Max(0,screen.WorkingArea.Right-Right); settings.Top=Math.Max(0,Top-screen.WorkingArea.Top); settings.Save(); }
 void SetAdjusting(bool value) { adjusting=value; BackColor=value?Color.FromArgb(28,31,36):Color.Fuchsia; int style=Native.GetWindowLong(Handle,-20); Native.SetWindowLong(Handle,-20,value?style&~0x20:style|0x20); if(!value) SavePosition(); CheckVisibility(); Render(); BuildMenu(); }
 void BuildMenu() {
  var old=tray.ContextMenuStrip; var menu=new ContextMenuStrip();
  var label=new ToolStripMenuItem("GPU Dock · "+(adapters.FirstOrDefault(a=>a.Luid==settings.Adapter)??new Adapter{Name="All GPUs"}).Name) {Enabled=false}; menu.Items.Add(label);
  menu.Items.Add("Adjust layout (drag; double-click to finish)",null,delegate { SetAdjusting(!adjusting); });
  menu.Items.Add(paused?"Show panel":"Hide panel",null,delegate { paused=!paused; CheckVisibility(); BuildMenu(); });
  var auto=new ToolStripMenuItem("Hide for games / fullscreen apps") {Checked=settings.AutoHide}; auto.Click+=delegate {settings.AutoHide=!settings.AutoHide;settings.Save();BuildMenu();}; menu.Items.Add(auto);
  var gpu=new ToolStripMenuItem("GPU");
  foreach(var a in adapters) { var captured=a; var item=new ToolStripMenuItem(a.Name){Checked=settings.Adapter==a.Luid}; item.Click+=delegate { settings.Adapter=captured.Luid; settings.AdapterName=captured.Name; sampler.Adapter=captured.Luid; snapshot=new Snapshot(); settings.Save(); BuildMenu(); Collect();}; gpu.DropDownItems.Add(item); } menu.Items.Add(gpu);
  var rows=new ToolStripMenuItem("Visible apps"); foreach(int n in new[]{3,5,8}) { int count=n; var item=new ToolStripMenuItem(n.ToString()){Checked=settings.Rows==n}; item.Click+=delegate {settings.Rows=count;SetFont();Position();settings.Save();BuildMenu();};rows.DropDownItems.Add(item); } menu.Items.Add(rows);
  var font=new ToolStripMenuItem("Text size"); foreach(int n in new[]{10,12,14,16}) {int size=n;var item=new ToolStripMenuItem(n+" pt"){Checked=settings.FontSize==n};item.Click+=delegate {settings.FontSize=size;SetFont();Position();settings.Save();BuildMenu();};font.DropDownItems.Add(item);}menu.Items.Add(font);
  var monitors=new ToolStripMenuItem("Monitor"); for(int i=0;i<Screen.AllScreens.Length;i++) {int index=i; var item=new ToolStripMenuItem("Monitor "+(i+1)){Checked=settings.Monitor==i};item.Click+=delegate{settings.Monitor=index;Position();settings.Save();BuildMenu();};monitors.DropDownItems.Add(item);}menu.Items.Add(monitors);
  menu.Items.Add("Reset position beneath NVIDIA overlay",null,delegate { settings.Right=24;settings.Top=68;Position();settings.Save(); });
  var games=new ToolStripMenuItem("Remembered games (Ctrl+Alt+G to toggle)"); foreach(string path in settings.Games.ToArray()) {string captured=path;games.DropDownItems.Add("Remove "+Path.GetFileName(path),null,delegate{settings.Games.Remove(captured);settings.Save();BuildMenu();});} menu.Items.Add(games);
  var startup=new ToolStripMenuItem("Start with Windows"){Checked=StartupEnabled()};startup.Click+=delegate{SetStartup(!StartupEnabled());BuildMenu();};menu.Items.Add(startup);
  menu.Items.Add("Save diagnostics",null,delegate { SaveDiagnostics(); });
  menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit",null,delegate {Close();});tray.ContextMenuStrip=menu;if(old!=null)old.Dispose();
 }
 static bool StartupEnabled() { using(var k=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return k!=null&&k.GetValue("GpuDock")!=null; }
 static void SetStartup(bool enabled) {using(var k=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)k.SetValue("GpuDock","\""+Application.ExecutablePath+"\"");else k.DeleteValue("GpuDock",false);}}
 void Collect() { if(Interlocked.Exchange(ref reading,1)!=0)return; ThreadPool.QueueUserWorkItem(delegate { var next=sampler.Read(); try { if(!IsDisposed&&IsHandleCreated) BeginInvoke((Action)delegate {snapshot=next;Render();}); } catch(InvalidOperationException) {} finally {Interlocked.Exchange(ref reading,0);} }); }
 void CheckVisibility() {
  hidden=Native.HideReason(settings); bool hide=paused || (!adjusting&&hidden.Length>0);
  sampleTimer.Interval=hide?5000:1500;
  if(hide) {showAfter=DateTime.Now.AddMilliseconds(900);if(Visible)Hide();}
  else if(DateTime.Now>=showAfter&&!Visible) Show();
 }
 public static string Friendly(string name) { switch(name.ToLowerInvariant()) {case "vmwp":return "Virtual machine";case "vmmem":case "vmmemwsl":return "WSL / virtual machine";case "dwm":return "Desktop compositor";case "csrss":return "Windows session";case "wallpaper64":case "wallpaper32":return "Wallpaper Engine";default:return name.Length>0?char.ToUpper(name[0])+name.Substring(1):name;} }
 public static void PaintRows(Graphics g,Size size,Font font,IEnumerable<Row> rows,string status,bool adjust) {
  g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
  float height=font.SizeInPoints*2.2f; int y=0;
  foreach(var row in rows) {
   string mem=!row.MemoryValid?"—":row.Memory>=1024.0*1024*1024?(row.Memory/(1024.0*1024*1024)).ToString("0.0")+" GB":(row.Memory/(1024*1024)).ToString("0")+" MB";
   Draw(g,Friendly(row.Name),font,new RectangleF(4,y,size.Width*0.56f,height),false);
   Draw(g,row.Usage.ToString("0")+" %",font,new RectangleF(size.Width*0.56f,y,size.Width*0.17f,height),true);
   Draw(g,mem,font,new RectangleF(size.Width*0.73f,y,size.Width*0.27f-4,height),true); y+=(int)height;
  }
  if(status.Length>0)Draw(g,status,font,new RectangleF(4,y,size.Width-8,height),false);
  if(adjust) { using(var pen=new Pen(Color.FromArgb(110,190,160)))g.DrawRectangle(pen,0,0,size.Width-1,size.Height-1);using(var f=new Font("Segoe UI",9))Draw(g,"Drag to move · double-click to finish",f,new RectangleF(5,size.Height-22,size.Width-10,20),false); }
 }
 static void Draw(Graphics g,string text,Font font,RectangleF rect,bool right) { using(var format=new StringFormat{Alignment=right?StringAlignment.Far:StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}) {var shadow=rect;shadow.Offset(1,1);g.DrawString(text,font,Brushes.Black,shadow,format);g.DrawString(text,font,Brushes.WhiteSmoke,rect,format);} }
 protected override void OnPaintBackground(PaintEventArgs e) {}
 protected override void OnPaint(PaintEventArgs e) {Render();}
 void Render() {if(!IsHandleCreated||IsDisposed)return; string status=snapshot.Error.Length>0?"GPU readings unavailable":snapshot.Rows.Count==0?"GPU idle":"";using(var bitmap=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(bitmap)) {g.Clear(adjusting?Color.FromArgb(235,28,31,36):Color.Transparent);PaintRows(g,ClientSize,textFont,snapshot.Rows.Take(settings.Rows),status,adjusting);Native.Present(Handle,Location,bitmap);} }
 void SaveDiagnostics() { Directory.CreateDirectory(Settings.Folder);File.WriteAllText(Path.Combine(Settings.Folder,"diagnostics.txt"),Program.Describe(snapshot,adapters)+"\nHidden: "+hidden+"\nClick-through: "+(!adjusting)+"\nBounds: "+Bounds);Process.Start("notepad.exe",Path.Combine(Settings.Folder,"diagnostics.txt")); }
 protected override void WndProc(ref Message m) {if(m.Msg==0x312&&m.WParam.ToInt32()==1){string exe=Native.ForegroundExe();if(exe.Length>0&&!exe.Equals(Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)){if(settings.Games.Contains(exe,StringComparer.OrdinalIgnoreCase))settings.Games.RemoveAll(p=>p.Equals(exe,StringComparison.OrdinalIgnoreCase));else settings.Games.Add(exe);settings.Save();BuildMenu();}}base.WndProc(ref m);}
 protected override void OnFormClosed(FormClosedEventArgs e) {visibilityTimer.Stop();sampleTimer.Stop();Native.UnregisterHotKey(Handle,1);tray.Visible=false;tray.Dispose();textFont.Dispose();base.OnFormClosed(e);}
}
public static class Program {
 public static string Describe(Snapshot s,List<Adapter> adapters) {return "GPU Dock | "+s.Time.ToString("O")+"\n"+String.Join("\n",adapters.Select(a=>a.Name+" | "+a.Luid))+"\nError: "+s.Error+"\n"+String.Join("\n",s.Rows.Select(r=>r.Name+" | "+r.Usage.ToString("F2")+" % | "+(r.MemoryValid?(r.Memory/(1024*1024)).ToString("F1")+" MiB":"unavailable (counter exceeds adapter usage)")));}
 [STAThread] public static void Main(string[] args) {
  Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>0&&args[0]=="--diagnose") { var adapters=Native.Adapters();var sampler=new Sampler {Adapter=(adapters.FirstOrDefault(a=>a.Name.Contains("NVIDIA"))??new Adapter{Luid=""}).Luid};sampler.Read();Thread.Sleep(1600);var s=sampler.Read();File.WriteAllText(args[1],Describe(s,adapters));Environment.Exit(s.Error.Length>0?1:0);return; }
  if(args.Length>0&&args[0]=="--self-test") {bool ok=Native.Covers(new Native.Rect{Left=0,Top=0,Right=1920,Bottom=1080},new Rectangle(0,0,1920,1080))&&!Native.Covers(new Native.Rect{Left=0,Top=0,Right=1920,Bottom=1040},new Rectangle(0,0,1920,1080))&&Native.GamePath(@"D:\SteamLibrary\steamapps\common\Nioh3\game.exe")&&!Native.GamePath(@"C:\Windows\explorer.exe");File.WriteAllText(args[1],ok?"PASS: fullscreen bounds, taskbar exclusion, game path recognition":"FAIL");Environment.Exit(ok?0:1);return;}
  if(args.Length>0&&args[0]=="--preview") {using(var b=new Bitmap(408,106))using(var g=Graphics.FromImage(b))using(var f=new Font("Segoe UI",12,FontStyle.Bold)){g.Clear(Color.FromArgb(20,30,38));Dock.PaintRows(g,b.Size,f,new[]{new Row{Name="Local AI",Usage=58,Memory=6.8*1024*1024*1024},new Row{Name="OBS",Usage=14,Memory=0.7*1024*1024*1024},new Row{Name="Chrome",Usage=6,Memory=0.5*1024*1024*1024}},"",false);b.Save(args[1]);}return;}
  bool created;using(var mutex=new Mutex(true,"Local\\Charles.GpuDock",out created)){if(!created)return;try{Application.Run(new Dock(Settings.Load()));}catch(Exception ex){Directory.CreateDirectory(Settings.Folder);File.WriteAllText(Path.Combine(Settings.Folder,"error.txt"),ex.ToString());MessageBox.Show("GPU Dock could not start. Details: "+Path.Combine(Settings.Folder,"error.txt"));}}
 }
}

}
