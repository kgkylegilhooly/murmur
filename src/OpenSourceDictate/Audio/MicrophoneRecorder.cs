using System.IO;
using NAudio.Wave;

namespace OpenSourceDictate.Audio;

/// <summary>
/// Records the microphone straight to a 16 kHz, 16-bit mono WAV file,
/// which is the format Whisper expects, so no resampling is needed later.
/// </summary>
public sealed class MicrophoneRecorder : IDisposable
{
    public static readonly WaveFormat WhisperFormat = new(rate: 16000, bits: 16, channels: 1);

    private readonly string _folder;
    private readonly object _writerLock = new();
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _path;
    private DateTime _startedAt;
    private TimeSpan _minimumDuration;
    private TaskCompletionSource<RecordingResult?>? _stopped;

    public MicrophoneRecorder(string folder)
    {
        _folder = folder;
    }

    public bool IsRecording => _waveIn is not null;

    /// <param name="deviceNumber">WinMM device index, or -1 for the default microphone.</param>
    public void Start(int deviceNumber)
    {
        if (IsRecording) return;

        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.wav");

        var waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = WhisperFormat,
            BufferMilliseconds = 50,
        };
        waveIn.DataAvailable += OnDataAvailable;
        waveIn.RecordingStopped += OnRecordingStopped;

        _writer = new WaveFileWriter(_path, WhisperFormat);
        _waveIn = waveIn;
        _stopped = new TaskCompletionSource<RecordingResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _startedAt = DateTime.UtcNow;

        try
        {
            waveIn.StartRecording();
        }
        catch
        {
            Cleanup(deleteFile: true);
            throw;
        }
    }

    /// <summary>
    /// Stops recording and completes once the WAV file is fully written.
    /// Returns null if nothing was recording or the clip was shorter than <paramref name="minimumDuration"/>.
    /// </summary>
    public Task<RecordingResult?> StopAsync(TimeSpan minimumDuration)
    {
        if (_waveIn is null || _stopped is null) return Task.FromResult<RecordingResult?>(null);

        _minimumDuration = minimumDuration;
        var task = _stopped.Task;
        _waveIn.StopRecording(); // RecordingStopped fires once the last buffer is flushed
        return task;
    }

    // Raised on NAudio's capture thread.
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_writerLock)
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        var stopped = _stopped;
        var path = _path;
        var duration = _writer?.TotalTime ?? TimeSpan.Zero;
        var tooShort = duration < _minimumDuration || DateTime.UtcNow - _startedAt < _minimumDuration;

        Cleanup(deleteFile: e.Exception is not null || tooShort);

        if (e.Exception is not null)
            stopped?.TrySetException(e.Exception);
        else
            stopped?.TrySetResult(tooShort || path is null ? null : new RecordingResult(path, duration));
    }

    private void Cleanup(bool deleteFile)
    {
        lock (_writerLock)
        {
            _writer?.Dispose();
            _writer = null;
        }

        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (deleteFile && _path is not null)
        {
            try { File.Delete(_path); } catch (IOException) { }
        }

        _path = null;
    }

    /// <summary>Deletes all but the newest <paramref name="keep"/> recordings.</summary>
    public void PruneOldRecordings(int keep)
    {
        if (!Directory.Exists(_folder)) return;

        foreach (var file in new DirectoryInfo(_folder).GetFiles("*.wav")
                     .OrderByDescending(f => f.CreationTimeUtc)
                     .Skip(Math.Max(keep, 0)))
        {
            try { file.Delete(); } catch (IOException) { }
        }
    }

    public void Dispose() => Cleanup(deleteFile: true);
}

public sealed record RecordingResult(string Path, TimeSpan Duration);
