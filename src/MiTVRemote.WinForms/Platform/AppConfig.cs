using System.Text.Json;

namespace MiTVRemote.Platform;

public sealed class AppConfig
{
    private readonly object _gate = new();
    private readonly string _path;
    private Dictionary<string, string> _values;

    private AppConfig(string path, Dictionary<string, string> values)
    {
        _path = path;
        _values = values;
    }

    public static AppConfig Load()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiTVRemote");
        var path = Path.Combine(directory, "config.json");
        try
        {
            if (File.Exists(path))
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                if (values is not null) return new AppConfig(path, values);
            }
        }
        catch { }
        return new AppConfig(path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    public string? Get(string key)
    {
        lock (_gate) return _values.TryGetValue(key, out var value) ? value : null;
    }

    public void Set(string key, string value)
    {
        lock (_gate)
        {
            _values[key] = value;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, _path, true);
            }
            catch { }
        }
    }
}
