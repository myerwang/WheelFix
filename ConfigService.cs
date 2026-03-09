using System;
using System.IO;
using System.Text.Json;

namespace WheelFix;

public sealed class ConfigService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public ConfigService()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WheelFix");
        Directory.CreateDirectory(baseDir);
        _configPath = Path.Combine(baseDir, "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                var defaults = new AppConfig();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            config.Normalize();
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        config.Normalize();
        var json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(_configPath, json);
    }
}
