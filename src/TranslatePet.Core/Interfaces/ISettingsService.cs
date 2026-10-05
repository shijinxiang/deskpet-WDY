using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    event EventHandler? SettingsChanged;
}
