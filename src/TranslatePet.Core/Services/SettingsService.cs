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

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
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
        SaveSettings();
    }

    public void SaveSettings()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_configFilePath, json);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public Task LoadSettingsAsync()
    {
        LoadSettings();
        return Task.CompletedTask;
    }

    public Task SaveSettingsAsync()
    {
        SaveSettings();
        return Task.CompletedTask;
    }
}
