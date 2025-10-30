using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows;

namespace ProcTamer.Tray
{
 public class TrayHelper : IDisposable
 {
 private NotifyIcon _notifyIcon;
 private Icon? _loadedIcon;
 public event Action? ShowRequested;
 public event Action? ExitRequested;

 public TrayHelper()
 {
 _notifyIcon = new NotifyIcon();
 _notifyIcon.Visible = true;
 _notifyIcon.Text = "ProcTamer";

 // Try to load custom tray icon from WPF resources (Assets/tray.ico or Assets/app.ico)
 try
 {
 Icon? icon = TryLoadIconFromResource("pack://application:,,,/Assets/tray.ico");
 if (icon == null)
 {
 icon = TryLoadIconFromResource("pack://application:,,,/Assets/app.ico");
 }

 _loadedIcon = icon ?? SystemIcons.Application;
 }
 catch
 {
 _loadedIcon = SystemIcons.Application;
 }

 _notifyIcon.Icon = _loadedIcon;
 _notifyIcon.DoubleClick += (s, e) => ShowRequested?.Invoke();
 var cms = new ContextMenuStrip();
 var show = new ToolStripMenuItem("显示窗口");
 show.Click += (s, e) => ShowRequested?.Invoke();
 var exit = new ToolStripMenuItem("退出程序");
 exit.Click += (s, e) => ExitRequested?.Invoke();
 cms.Items.Add(show);
 cms.Items.Add(exit);
 _notifyIcon.ContextMenuStrip = cms;
 }

 private Icon? TryLoadIconFromResource(string packUri)
 {
 try
 {
 var uri = new Uri(packUri, UriKind.Absolute);
 var streamInfo = System.Windows.Application.GetResourceStream(uri);
 if (streamInfo?.Stream != null)
 {
 using var ms = new MemoryStream();
 streamInfo.Stream.CopyTo(ms);
 ms.Seek(0, SeekOrigin.Begin);
 return new Icon(ms);
 }
 }
 catch { }
 return null;
 }

 public void Dispose()
 {
 try
 {
 if (_notifyIcon != null)
 {
 _notifyIcon.Visible = false;
 _notifyIcon.Dispose();
 }
 }
 catch { }

 try
 {
 if (_loadedIcon != null && !ReferenceEquals(_loadedIcon, SystemIcons.Application))
 {
 _loadedIcon.Dispose();
 }
 }
 catch { }
 }
 }
}
