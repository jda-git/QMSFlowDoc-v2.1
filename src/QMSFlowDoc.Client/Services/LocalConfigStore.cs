using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public class LocalConfigStore
{
    private readonly string _configPath;
    private LocalConfig? _config;

    public LocalConfigStore()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QMSFlowDoc");
        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, "client_config.json");
    }

    public async Task<LocalConfig> LoadAsync()
    {
        if (_config != null) return _config;

        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _config = JsonSerializer.Deserialize<LocalConfig>(json) ?? new LocalConfig();
            }
            else
            {
                _config = new LocalConfig();
            }
        }
        catch
        {
            _config = new LocalConfig();
        }

        return _config;
    }

    public async Task SaveAsync(LocalConfig config)
    {
        _config = config;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_configPath, json);
    }

    public async Task<string?> GetDriveFolderIdAsync()
    {
        var config = await LoadAsync();
        return config.DriveFolderId;
    }

    public async Task SetDriveFolderIdAsync(string? folderId)
    {
        var config = await LoadAsync();
        config.DriveFolderId = folderId;
        await SaveAsync(config);
    }
}

public class LocalConfig
{
    public string? DriveFolderId { get; set; }
}
