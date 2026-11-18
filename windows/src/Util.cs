using System.Text.Json;
namespace SpaceWall;

static class Dir
{
    public static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceWall");
    public static string F(string n) => Path.Combine(Data, n);
    static Dir() { Directory.CreateDirectory(Data); }
}

static class Log
{
    static readonly object lk = new();
    public static string Path_ => Dir.F("log.txt");
    public static void W(string s)
    {
        try
        {
            lock (lk)
            {
                var fi = new FileInfo(Path_);
                if (fi.Exists && fi.Length > 1_000_000) File.Move(Path_, Dir.F("log.old.txt"), true);
                File.AppendAllText(Path_, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {s}\n");
            }
        }
        catch { }
    }
}

class Settings
{
    public bool On { get; set; } = true;
    public string Mode { get; set; } = "auto";       // auto workerw progman progman-bottom bottom window
    public string Quality { get; set; } = "normal";  // low normal high
    public bool Demo { get; set; }
    public bool Debug { get; set; }
    public bool Span { get; set; }
    public double Sens { get; set; } = 1.0;
    static string P => Dir.F("state.json");
    public static Settings Load()
    {
        try { if (File.Exists(P)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(P)) ?? new Settings(); }
        catch (Exception e) { Log.W("state.json unreadable: " + e.Message); }
        return new Settings();
    }
    public void Save() { try { File.WriteAllText(P, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); } catch (Exception e) { Log.W("save failed: " + e.Message); } }
}

static class AutoStart
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "SpaceWallShips";
    public static bool Get() { try { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(Name) != null; } catch { return false; } }
    public static void Set(bool on)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key, true);
            if (on) k.SetValue(Name, "\"" + Environment.ProcessPath + "\" --autostart"); else k.DeleteValue(Name, false);
        }
        catch (Exception e) { Log.W("autostart failed: " + e.Message); }
    }
}
