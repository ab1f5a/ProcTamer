using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ProcTamer.Tray;
using ProcTamer.ViewModels;
using Microsoft.Win32;

namespace ProcTamer
{
 /// <summary>
 /// Interaction logic for MainWindow.xaml
 /// </summary>
 public partial class MainWindow : Window
 {
 private TrayHelper? _tray;
 private MainViewModel _vm => DataContext as MainViewModel ?? new MainViewModel();
 public MainWindow()
 {
 InitializeComponent();
 // ensure DataContext is MainViewModel
 if (DataContext == null) DataContext = new MainViewModel();
 _tray = new TrayHelper();
 _tray.ShowRequested += () => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });
 _tray.ExitRequested += () => Dispatcher.Invoke(() => { _tray?.Dispose(); System.Windows.Application.Current.Shutdown(); });

 // handle close to minimize to tray
 Closing += MainWindow_Closing;
 Loaded += MainWindow_Loaded;
 }

 private void MainWindow_Loaded(object sender, RoutedEventArgs e)
 {
 // Apply autostart if needed
 try
 {
 if (_vm.AutoStart)
 {
 SetStartup(true);
 }
 }
 catch { }
 }

 private void MainWindow_Closing(object? sender, CancelEventArgs e)
 {
 // hide instead of close so tray remains
 e.Cancel = true;
 Hide();
 }

 protected override void OnStateChanged(EventArgs e)
 {
 base.OnStateChanged(e);
 if (WindowState == WindowState.Minimized)
 {
 Hide();
 }
 }

 private void SetStartup(bool enable)
 {
 try
 {
 using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
 if (key == null) return;
 var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
 if (enable)
 {
 key.SetValue("ProcTamer", exe);
 }
 else
 {
 key.DeleteValue("ProcTamer", false);
 }
 }
 catch { }
 }
 }
}