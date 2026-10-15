using System.Diagnostics;
using System.Windows;
using NAudio;
using Murmur.Audio;
using Murmur.Input;
using Murmur.Settings;
using Murmur.Tray;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace Murmur;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppSettings _settings = new();
    private TrayIcon? _tray;
    private PushToTalkHook? _hook;
    private MicrophoneRecorder? _recorder;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\Murmur", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Murmur is already running. Look for the microphone in the system tray.",
                "Murmur", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _settings = SettingsStore.Load();
        if (!Hotkey.TryParse(_settings.Hotkey, out var hotkey))
            hotkey = Hotkey.Default;

        _recorder = new MicrophoneRecorder(SettingsStore.RecordingsFolder);

        _tray = new TrayIcon();
        _tray.SetHotkey(hotkey.ToString());
        _tray.ChangeHotkeyClicked += (_, _) => ChangeHotkey();
        _tray.OpenRecordingsClicked += (_, _) => OpenRecordingsFolder();
        _tray.ExitClicked += (_, _) => Shutdown();

        _hook = new PushToTalkHook(hotkey);
        // The hook callback must return fast, so do the real work after it returns.
        _hook.Pressed += (_, _) => Dispatcher.BeginInvoke(StartRecording);
        _hook.Released += (_, _) => Dispatcher.BeginInvoke(StopRecording);

        _tray.ShowMessage("Murmur is running", $"Hold {hotkey} to record.");
    }

    private void StartRecording()
    {
        if (_recorder is null || _recorder.IsRecording) return;

        try
        {
            _recorder.Start(_settings.MicrophoneDeviceNumber);
            _tray?.SetRecording(true);
        }
        catch (MmException ex)
        {
            _tray?.ShowMessage("Couldn't open the microphone", ex.Message, ToolTipIcon.Error);
        }
    }

    private async void StopRecording()
    {
        if (_recorder is null || !_recorder.IsRecording) return;

        _tray?.SetRecording(false);
        try
        {
            var result = await _recorder.StopAsync(TimeSpan.FromMilliseconds(_settings.MinimumRecordingMilliseconds));
            if (result is null) return; // accidental tap

            Debug.WriteLine($"Saved {result.Duration.TotalSeconds:0.0}s recording to {result.Path}");
            // Step 2: hand result.Path to Whisper.net here.
            _recorder.PruneOldRecordings(_settings.RecordingsToKeep);
        }
        catch (MmException ex)
        {
            _tray?.ShowMessage("Recording failed", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ChangeHotkey()
    {
        if (_hook is null) return;

        _hook.Enabled = false; // don't start recording while the user is pressing keys
        try
        {
            var window = new HotkeyWindow(_hook.Hotkey);
            if (window.ShowDialog() != true) return;

            _hook.SetHotkey(window.Hotkey);
            _settings.Hotkey = window.Hotkey.ToString();
            SettingsStore.Save(_settings);
            _tray?.SetHotkey(window.Hotkey.ToString());
        }
        finally
        {
            _hook.Enabled = true;
        }
    }

    private static void OpenRecordingsFolder()
    {
        System.IO.Directory.CreateDirectory(SettingsStore.RecordingsFolder);
        Process.Start(new ProcessStartInfo(SettingsStore.RecordingsFolder) { UseShellExecute = true });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        _recorder?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
