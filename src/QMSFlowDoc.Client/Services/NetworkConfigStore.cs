using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

/// <summary>
/// Configuración local para almacenamiento de red
/// </summary>
public class NetworkConfig
{
    public string NetworkBasePath { get; set; } = "";
    public string LocalBasePath { get; set; } = "";
    public bool UseNetworkStorage { get; set; } = true;
    public bool AutoSyncOnStartup { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 5;
    public DateTime LastSyncAt { get; set; }
}

/// <summary>
/// Almacenamiento local de configuración de red (reemplaza LocalConfigStore)
/// </summary>
public class NetworkConfigStore
{
    private readonly string _configPath;
    private NetworkConfig? _config;

    public NetworkConfigStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "QMSFlowDoc");
        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, "network_config.json");
    }

    public async Task<NetworkConfig> LoadAsync()
    {
        if (_config != null) return _config;

        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _config = JsonSerializer.Deserialize<NetworkConfig>(json) ?? new NetworkConfig();
            }
            else
            {
                _config = new NetworkConfig();
            }
        }
        catch
        {
            _config = new NetworkConfig();
        }

        return _config;
    }

    public async Task SaveAsync(NetworkConfig config)
    {
        _config = config;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_configPath, json);
    }

    public async Task<string?> GetNetworkBasePathAsync()
    {
        var config = await LoadAsync();
        return string.IsNullOrWhiteSpace(config.NetworkBasePath) ? null : config.NetworkBasePath;
    }

    public async Task SetNetworkBasePathAsync(string? path)
    {
        var config = await LoadAsync();
        config.NetworkBasePath = path ?? "";
        await SaveAsync(config);
    }

    public async Task<string?> GetLocalBasePathAsync()
    {
        var config = await LoadAsync();
        return string.IsNullOrWhiteSpace(config.LocalBasePath) ? null : config.LocalBasePath;
    }

    public async Task SetLocalBasePathAsync(string? path)
    {
        var config = await LoadAsync();
        config.LocalBasePath = path ?? "";
        await SaveAsync(config);
    }

    public async Task<bool> ValidatePathsAsync()
    {
        var config = await LoadAsync();
        
        if (string.IsNullOrWhiteSpace(config.NetworkBasePath) || 
            string.IsNullOrWhiteSpace(config.LocalBasePath))
            return false;
        
        // Validar que ambas rutas existen
        return Directory.Exists(config.NetworkBasePath) && Directory.Exists(config.LocalBasePath);
    }

    public async Task InitializeStructureAsync()
    {
        var config = await LoadAsync();
        
        if (string.IsNullOrWhiteSpace(config.NetworkBasePath) || 
            string.IsNullOrWhiteSpace(config.LocalBasePath))
            throw new InvalidOperationException("Network and local paths must be configured first");
        
        // Crear estructura QMS en ambas ubicaciones
        var folders = new[]
        {
            "Auditoria",
            "Documentos",
            "Documentos/VERSIONES ANTIGUAS",
            "Equipos",
            "Incidencias",
            "Personal",
            "Inventario",
            "Base datos",
            "_Trash/local",
            "_Trash/network"
        };
        
        foreach (var folder in folders)
        {
            Directory.CreateDirectory(Path.Combine(config.NetworkBasePath, folder));
            Directory.CreateDirectory(Path.Combine(config.LocalBasePath, folder));
        }
    }
}
