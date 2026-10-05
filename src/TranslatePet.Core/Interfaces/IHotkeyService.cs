namespace TranslatePet.Core.Interfaces;

public interface IHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;
    bool Register(bool alt, bool ctrl, bool shift, bool win, string key);
    void Unregister();
    bool IsRegistered { get; }
}
