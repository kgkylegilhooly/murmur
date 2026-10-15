namespace Murmur.Settings;

public sealed class AppSettings
{
    /// <summary>Push-to-talk key, e.g. "RightCtrl", "F9" or "Ctrl+Shift+Space".</summary>
    public string Hotkey { get; set; } = Input.Hotkey.Default.ToString();

    /// <summary>WinMM input device index; -1 is the Windows default microphone.</summary>
    public int MicrophoneDeviceNumber { get; set; } = -1;

    /// <summary>Recordings shorter than this are treated as accidental taps and discarded.</summary>
    public int MinimumRecordingMilliseconds { get; set; } = 300;

    /// <summary>How many recent WAV files to keep on disk.</summary>
    public int RecordingsToKeep { get; set; } = 20;
}
