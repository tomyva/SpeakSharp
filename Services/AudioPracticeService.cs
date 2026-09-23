using Plugin.Maui.Audio;

namespace SpeakSharp.Services;

public sealed class AudioPracticeService(IAudioManager audioManager) : IDisposable
{
    private IAudioRecorder? _recorder;
    private IAudioPlayer? _player;
    private byte[]? _recording;

    public bool HasRecording => _recording is { Length: > 0 };

    public async Task StartAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted)
            throw new InvalidOperationException("Microphone permission is required to record a practice attempt.");

        _player?.Stop();
        _recorder = audioManager.CreateRecorder();
        if (!_recorder.CanRecordAudio)
            throw new InvalidOperationException("Audio recording is not available on this device.");

        await _recorder.StartAsync(new AudioRecorderOptions
        {
            Encoding = Plugin.Maui.Audio.Encoding.Wav,
            SampleRate = 16000,
            Channels = ChannelType.Mono,
            ThrowIfNotSupported = false,
        });
    }

    public async Task<byte[]> StopAsync()
    {
        if (_recorder is null || !_recorder.IsRecording)
            return _recording ?? [];

        var source = await _recorder.StopAsync();
        await using var stream = source.GetAudioStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        _recording = memory.ToArray();
        return _recording;
    }

    public void PlayRecording()
    {
        if (!HasRecording) return;
        Play(new MemoryStream(_recording!, writable: false));
    }

    public void PlayFile(string path)
    {
        if (File.Exists(path)) Play(File.OpenRead(path));
    }

    private void Play(Stream stream)
    {
        _player?.Stop();
        _player?.Dispose();
        _player = audioManager.CreatePlayer(stream);
        _player.Play();
    }

    public void Dispose()
    {
        _player?.Dispose();
    }
}
