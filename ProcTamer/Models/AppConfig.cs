using System.Collections.Generic;

namespace ProcTamer.Models
{
 // Configuration model persisted to config.json
 public class AppConfig
 {
 public List<string> SelectedProcesses { get; set; } = new();
 public string Preset { get; set; } = "ACEÏÞÖÆ";
 public bool AutoStart { get; set; } = false;
 public bool ShowOnStartup { get; set; } = false;
 public int CheckInterval { get; set; } =3; // seconds
 }
}