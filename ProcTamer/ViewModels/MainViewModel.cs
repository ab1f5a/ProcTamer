using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ProcTamer.Models;
using System.Text.Json;
using System.IO;
using Microsoft.Win32;
using System.Collections.Generic;

namespace ProcTamer.ViewModels
{
 // Simple item representing a process for the UI
 public class ProcessItem : INotifyPropertyChanged
 {
 public string Name { get; set; } = "";
 public int Pid { get; set; }
 bool _isChecked;
 public bool IsChecked { get => _isChecked; set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); } }
 public event PropertyChangedEventHandler? PropertyChanged;
 }

 // Minimal ICommand implementation
 public class RelayCommand : ICommand
 {
 private readonly Action<object?> _execute;
 private readonly Func<object?, bool>? _canExecute;
 public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
 public event EventHandler? CanExecuteChanged;
 public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
 public void Execute(object? parameter) => _execute(parameter);
 }

 // Main ViewModel handling processes and config
 public class MainViewModel : INotifyPropertyChanged
 {
 // UI collections and properties
 // AllProcesses holds the full list; Processes is the filtered ObservableCollection bound to UI
 private List<ProcessItem> AllProcesses { get; set; } = new();
 public ObservableCollection<ProcessItem> Processes { get; set; } = new();
 public ObservableCollection<string> Presets { get; set; } = new() { "ACE����" };
 public ObservableCollection<string> Logs { get; set; } = new();

 AppConfig _config = new();
 string _selectedPreset = "";
 public string SelectedPreset
 {
 get => _selectedPreset;
 set
 {
 _selectedPreset = value;
 // when ACE preset selected, ensure the two ACE process names are being monitored
 if (string.Equals(value, "ACE����", StringComparison.OrdinalIgnoreCase))
 {
 // store process names without .exe
 AddPresetProcesses(new[] { "SGuard64", "ACE-Tray" });
 AddLog("��ѡ��Ԥ��: ACE���ƣ������ SGuard64 �� ACE-Tray �������б�");
 }
 _config.Preset = value;
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
 }
 }

 string _searchText = string.Empty;
 public string SearchText
 {
 get => _searchText;
 set { _searchText = value ?? string.Empty; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText))); ApplyFilter(); }
 }

 public int CheckInterval { get => _config.CheckInterval; set { _config.CheckInterval = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckInterval))); } }
 public bool AutoStart { get => _config.AutoStart; set { _config.AutoStart = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoStart))); TrySetStartup(value); } }
 public bool ShowOnStartup { get => _config.ShowOnStartup; set { _config.ShowOnStartup = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowOnStartup))); } }

 CancellationTokenSource? _cts;
 public string Status { get; set; } = "δ����";

 public ICommand SaveCommand { get; }
 public ICommand LoadCommand { get; }
 public ICommand StartStopCommand { get; }
 public ICommand ApplyNowCommand { get; }
 public ICommand ClearLogsCommand { get; }

 public MainViewModel()
 {
 SaveCommand = new RelayCommand(_ => SaveConfig());
 LoadCommand = new RelayCommand(_ => { LoadConfig(); RefreshProcesses(); });
 StartStopCommand = new RelayCommand(_ => ToggleListen());
 ApplyNowCommand = new RelayCommand(_ => ApplyNow());
 ClearLogsCommand = new RelayCommand(_ => { Logs.Clear(); AddLog("��־�����"); });

 // ��ʼ���ؽ���������
 LoadConfig();
 RefreshProcesses();
 }

 // add preset process names into config selected list if not present
 private void AddPresetProcesses(IEnumerable<string> names)
 {
 foreach (var n in names)
 {
 if (!_config.SelectedProcesses.Contains(n, StringComparer.OrdinalIgnoreCase))
 {
 _config.SelectedProcesses.Add(n);
 }
 }
 // update checked state in UI list
 foreach (var it in AllProcesses)
 {
 if (_config.SelectedProcesses.Contains(it.Name, StringComparer.OrdinalIgnoreCase)) it.IsChecked = true;
 }
 ApplyFilter();
 }

 // Refresh process list shown to user; builds AllProcesses then applies current SearchText filter
 public void RefreshProcesses()
 {
 AllProcesses.Clear();
 Processes.Clear();
 try
 {
 var all = Process.GetProcesses().OrderBy(p => p.ProcessName).ToList();
 foreach (var p in all)
 {
 var name = p.ProcessName; // no extension
 var item = new ProcessItem { Name = name, Pid = p.Id, IsChecked = _config.SelectedProcesses.Contains(name, StringComparer.OrdinalIgnoreCase) };
 AllProcesses.Add(item);
 p.Dispose();
 }
 }
 catch (Exception)
 {
 // ignore but keep app stable
 }
 ApplyFilter();
 }

 // Apply SearchText filter to AllProcesses and update Processes collection
 private void ApplyFilter()
 {
 Processes.Clear();
 var terms = (SearchText ?? string.Empty).Trim();
 IEnumerable<ProcessItem> items = AllProcesses;
 if (!string.IsNullOrEmpty(terms))
 {
 items = items.Where(i => i.Name.IndexOf(terms, StringComparison.OrdinalIgnoreCase) >=0);
 }
 foreach (var it in items)
 {
 Processes.Add(it);
 }
 }

 // Save configuration to config.json in current directory
 public void SaveConfig()
 {
 try
 {
 _config.SelectedProcesses = AllProcesses.Where(p => p.IsChecked).Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
 File.WriteAllText("config.json", json);
 AddLog("�ѱ������õ� config.json");
 }
 catch (Exception ex)
 {
 AddLog("��������ʧ��: " + ex.Message);
 }
 }

 // Load configuration from config.json
 public void LoadConfig()
 {
 try
 {
 if (File.Exists("config.json"))
 {
 var json = File.ReadAllText("config.json");
 var cfg = JsonSerializer.Deserialize<AppConfig>(json);
 if (cfg != null) _config = cfg;
 }
 }
 catch (Exception ex) { AddLog("��������ʧ��: " + ex.Message); }
 // notify UI
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
 }

 // Start or stop the background listener
 public void ToggleListen()
 {
 if (_cts != null)
 {
 // stop
 _cts.Cancel();
 _cts = null;
 Status = "��ֹͣ";
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
 AddLog("������ֹͣ");
 }
 else
 {
 _cts = new CancellationTokenSource();
 var token = _cts.Token;
 Task.Run(() => ListenLoop(token), token);
 Status = $"���ڼ�����Ƶ��={CheckInterval}��";
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
 AddLog($"��ʼ������Ƶ��={CheckInterval}��");
 }
 }

 // Background loop that applies priority and affinity to selected processes
 async Task ListenLoop(CancellationToken token)
 {
 while (!token.IsCancellationRequested)
 {
 try
 {
 var selected = _config.SelectedProcesses ?? new List<string>();
 var procs = Process.GetProcesses().ToList();
 foreach (var name in selected)
 {
 var targets = procs.Where(p => string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase)).ToList();
 if (targets.Count ==0)
 {
 // no running processes with that name currently
 AddLog($"��δ���ֽ���: {name}");
 }
 foreach (var p in targets)
 {
 try
 {
 p.PriorityClass = ProcessPriorityClass.BelowNormal;
 // set affinity to last core
 var cpuCount = Environment.ProcessorCount;
 if (cpuCount >0)
 {
 long mask =1L << (cpuCount -1);
 p.ProcessorAffinity = (IntPtr)mask;
 }
 AddLog($"�ѶԽ��� {p.ProcessName} (pid={p.Id}) Ӧ������: BelowNormal, affinity=last core");
 }
 catch (Exception ex)
 {
 AddLog($"�Խ��� {p.ProcessName} (pid={p.Id}) Ӧ������ʧ��: {ex.Message}");
 }
 finally { p.Dispose(); }
 }
 }
 }
 catch (Exception ex) { AddLog("����ѭ������: " + ex.Message); }
 try
 {
 await Task.Delay(TimeSpan.FromSeconds(CheckInterval), token);
 }
 catch (TaskCanceledException) { break; }
 }
 }

 // Immediately apply limits to currently checked processes that are running
 public void ApplyNow()
 {
 try
 {
 var toApply = AllProcesses.Where(p => p.IsChecked).Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 var procs = Process.GetProcesses().ToList();
 foreach (var name in toApply)
 {
 var targets = procs.Where(p => string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase)).ToList();
 if (targets.Count ==0)
 {
 AddLog($"Ӧ�ã���δ�������еĽ��� {name}");
 }
 foreach (var p in targets)
 {
 try
 {
 p.PriorityClass = ProcessPriorityClass.BelowNormal;
 var cpuCount = Environment.ProcessorCount;
 if (cpuCount >0)
 {
 long mask =1L << (cpuCount -1);
 p.ProcessorAffinity = (IntPtr)mask;
 }
 AddLog($"�������Խ��� {p.ProcessName} (pid={p.Id}) Ӧ������");
 }
 catch (Exception ex)
 {
 AddLog($"����Ӧ��ʧ��: {p.ProcessName} (pid={p.Id}) => {ex.Message}");
 }
 finally { p.Dispose(); }
 }
 }
 }
 catch (Exception ex) { AddLog("ApplyNow ����: " + ex.Message); }
 }

 // Try to write HKCU Run key when user toggles AutoStart
 private void TrySetStartup(bool enable)
 {
 try
 {
 using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
 if (key == null) return;
 var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
 if (enable)
 {
 key.SetValue("ProcTamer", exe);
 AddLog("�����ÿ�������");
 }
 else
 {
 key.DeleteValue("ProcTamer", false);
 AddLog("��ȡ����������");
 }
 }
 catch (Exception ex) { AddLog("���ÿ�������ʧ��: " + ex.Message); }
 }

 private void AddLog(string text)
 {
 try
 {
 var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
 Logs.Insert(0, $"[{ts}] {text}");
 // keep log list at reasonable size
 if (Logs.Count >500) Logs.RemoveAt(Logs.Count -1);
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Logs)));
 }
 catch { }
 }

 public event PropertyChangedEventHandler? PropertyChanged;
 }
}
