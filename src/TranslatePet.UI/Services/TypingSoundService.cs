using System.IO;
using System.Media;

namespace TranslatePet.UI.Services;

/// <summary>
/// TypeWords 极简轻量级机械键盘打字音效引擎 (零外部依赖、高频低延迟合成)
/// </summary>
public static class TypingSoundService
{
    public static bool IsEnabled { get; set; } = true;

    private static SoundPlayer? _clickPlayer;
    private static SoundPlayer? _errorPlayer;
    private static SoundPlayer? _successPlayer;
    private static readonly object _initLock = new();
    private static bool _initialized = false;

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            try
            {
                _clickPlayer = CreatePlayer(GenerateClickWav());
                _errorPlayer = CreatePlayer(GenerateErrorWav());
                _successPlayer = CreatePlayer(GenerateSuccessWav());
            }
            catch
            {
                // Ignore fallback
            }
            _initialized = true;
        }
    }

    private static SoundPlayer CreatePlayer(byte[] wavBytes)
    {
        var ms = new MemoryStream(wavBytes);
        var player = new SoundPlayer(ms);
        player.Load();
        return player;
    }

    public static void PlayClick()
    {
        if (!IsEnabled) return;
        try
        {
            EnsureInitialized();
            _clickPlayer?.Play();
        }
        catch { }
    }

    public static void PlayError()
    {
        if (!IsEnabled) return;
        try
        {
            EnsureInitialized();
            _errorPlayer?.Play();
        }
        catch { }
    }

    public static void PlaySuccess()
    {
        if (!IsEnabled) return;
        try
        {
            EnsureInitialized();
            _successPlayer?.Play();
        }
        catch { }
    }

    private static byte[] GenerateClickWav()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.035); // 35ms 脆爽机械键击声
        short[] buffer = new short[samples];
        var rand = new Random(42);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double decay = Math.Exp(-t * 220.0);
            double tone = Math.Sin(2.0 * Math.PI * 2600.0 * t);
            double noise = (rand.NextDouble() * 2.0 - 1.0) * Math.Exp(-t * 800.0);
            double val = (0.7 * tone + 0.3 * noise) * decay;
            buffer[i] = (short)(Math.Clamp(val, -1.0, 1.0) * 26000);
        }

        return BuildWavFile(buffer, sampleRate);
    }

    private static byte[] GenerateErrorWav()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.06); // 60ms 错误沉闷按压声
        short[] buffer = new short[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double decay = Math.Exp(-t * 80.0);
            double tone = Math.Sin(2.0 * Math.PI * 180.0 * t);
            double val = tone * decay;
            buffer[i] = (short)(Math.Clamp(val, -1.0, 1.0) * 22000);
        }

        return BuildWavFile(buffer, sampleRate);
    }

    private static byte[] GenerateSuccessWav()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.18); // 180ms 通关和弦双音
        short[] buffer = new short[samples];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double decay = Math.Exp(-t * 18.0);
            double tone1 = Math.Sin(2.0 * Math.PI * 1046.5 * t); // C6
            double tone2 = Math.Sin(2.0 * Math.PI * 1318.5 * t); // E6
            double val = (0.5 * tone1 + 0.5 * tone2) * decay;
            buffer[i] = (short)(Math.Clamp(val, -1.0, 1.0) * 24000);
        }

        return BuildWavFile(buffer, sampleRate);
    }

    private static byte[] BuildWavFile(short[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int byteRate = sampleRate * 2;
        int dataSize = samples.Length * 2;

        bw.Write("RIFF"u8.ToArray());
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8.ToArray());

        bw.Write("fmt "u8.ToArray());
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)2);
        bw.Write((short)16);

        bw.Write("data"u8.ToArray());
        bw.Write(dataSize);
        foreach (var sample in samples)
        {
            bw.Write(sample);
        }

        return ms.ToArray();
    }
}
