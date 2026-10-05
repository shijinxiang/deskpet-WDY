using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    void LoadSettings();
    void SaveSettings();
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    event EventHandler? SettingsChanged;
}
