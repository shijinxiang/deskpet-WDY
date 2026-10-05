using System.Text.Json;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly string _configFilePath;
    private AppSettings _settings;

    public event EventHandler? SettingsChanged;

    public AppSettings CurrentSettings => _settings;

    public SettingsService(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            _configFilePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(appData, "TranslatePet");
            Directory.CreateDirectory(appDir);
            _configFilePath = Path.Combine(appDir, "settings.json");
        }

        _settings = new AppSettings();
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
        }
        catch
        {
            // Fallback to defaults on read error
        }

        _settings = new AppSettings();
        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            await File.WriteAllTextAsync(_configFilePath, json);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
