# Murmur

Free, local push-to-talk dictation for Windows. Hold a key, talk, let go, and your words are typed into whatever text field has focus. Speech recognition runs entirely on your PC (Whisper), so no audio leaves your machine.

> **Status: early development.** Right now the app records your voice while the hotkey is held and saves it as a WAV file. Transcription and typing are the next steps.

## Roadmap

1. ✅ Tray app with a configurable push-to-talk hotkey that records the microphone (16 kHz mono WAV)
2. ⬜ Local transcription with [Whisper.net](https://github.com/sandrohanea/whisper.net)
3. ⬜ Type the text into the focused window (SendInput, with clipboard paste as a fallback)
4. ⬜ Settings window (hotkey, model, microphone), recording indicator, installer

## Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

## Build and run

```powershell
git clone https://github.com/kgkylegilhooly/murmur.git
cd murmur
dotnet run --project src/Murmur
```

A microphone icon appears in the system tray. Hold **Right Ctrl** (the default) while you speak; the icon turns red while recording. Right-click the icon to change the hotkey or open the recordings folder.

## Where things live

| What | Where |
| --- | --- |
| Settings | `%AppData%\Murmur\settings.json` |
| Recordings (last 20 kept) | `%LocalAppData%\Murmur\Recordings` |

`settings.json` options:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Hotkey` | `RightCtrl` | Key to hold, e.g. `F9`, `CapsLock`, `Ctrl+Shift+Space`. Names follow WPF's `System.Windows.Input.Key`. |
| `MicrophoneDeviceNumber` | `-1` | Input device index; `-1` is the Windows default microphone. |
| `MinimumRecordingMilliseconds` | `300` | Shorter presses are treated as accidental taps and discarded. |
| `RecordingsToKeep` | `20` | How many recent WAV files to keep. |

## How it works

- **Hotkey:** a low-level keyboard hook (`WH_KEYBOARD_LL`) watches for the key going down and up. `RegisterHotKey` can't be used for push-to-talk because it only reports the press. A non-modifier hotkey (like F9) is swallowed so the focused app doesn't also receive it; modifier hotkeys (like Right Ctrl) pass through. Injected keystrokes are ignored, so text the app types later can't trigger it.
- **Audio:** [NAudio](https://github.com/naudio/NAudio) records straight to 16 kHz, 16-bit mono PCM, the format Whisper expects.

## License

[MIT](LICENSE)
