using System.Windows.Input;
using System.Diagnostics;
using SpeakSharp.Models;
using SpeakSharp.Services;

namespace SpeakSharp.ViewModels;

public sealed class PracticeViewModel : ObservableObject
{
    private readonly TopicService _topics;
    private readonly AudioPracticeService _audio;
    private readonly FillerAnalysisService _analyzer;
    private readonly OpenAiSpeechCoach _coach;
    private readonly SessionStore _store;
    private CancellationTokenSource? _timerCancellation;
    private DateTimeOffset _recordingStarted;
    private byte[]? _audioBytes;
    private string? _referenceAudioPath;
    private PracticeTopic _topic;
    private string _status = "Ready when you are";
    private string _timerText = "0:30";
    private string _transcript = string.Empty;
    private SpeechAnalysis? _analysis;
    private CoachingFeedback? _feedback;
    private bool _isRecording;
    private bool _isBusy;
    private bool _showTranscriptEditor;

    public PracticeViewModel(TopicService topics, AudioPracticeService audio, FillerAnalysisService analyzer, OpenAiSpeechCoach coach, SessionStore store)
    {
        _topics = topics;
        _audio = audio;
        _analyzer = analyzer;
        _coach = coach;
        _store = store;
        _topic = topics.Next();

        NewTopicCommand = new Command(NewTopic, () => !IsRecording && !IsBusy);
        RecordCommand = new Command(async () => await StartRecordingAsync(), () => !IsRecording && !IsBusy);
        StopCommand = new Command(async () => await StopRecordingAsync(), () => IsRecording);
        AnalyzeCommand = new Command(async () => await AnalyzeTranscriptAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(Transcript));
        UseSampleCommand = new Command(async () => await UseSampleAsync(), () => !IsBusy);
        PlayRecordingCommand = new Command(_audio.PlayRecording, () => _audio.HasRecording);
        PlayReferenceCommand = new Command(() => { if (_referenceAudioPath is not null) _audio.PlayFile(_referenceAudioPath); }, () => _referenceAudioPath is not null);
        RetryCommand = new Command(ResetAttempt, () => !IsRecording && !IsBusy);
    }

    public string TopicCategory => _topic.Category.ToUpperInvariant();
    public string TopicPrompt => _topic.Prompt;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string TimerText { get => _timerText; private set => SetProperty(ref _timerText, value); }
    public string Transcript { get => _transcript; set { if (SetProperty(ref _transcript, value)) ((Command)AnalyzeCommand).ChangeCanExecute(); } }
    public SpeechAnalysis? Analysis { get => _analysis; private set { if (SetProperty(ref _analysis, value)) Notify(nameof(HasResults)); } }
    public CoachingFeedback? Feedback { get => _feedback; private set { if (SetProperty(ref _feedback, value)) Notify(nameof(HasResults)); } }
    public bool HasResults => Analysis is not null && Feedback is not null;
    public bool ShowTranscriptEditor { get => _showTranscriptEditor; private set => SetProperty(ref _showTranscriptEditor, value); }
    public bool IsRecording { get => _isRecording; private set { if (SetProperty(ref _isRecording, value)) RefreshCommands(); } }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RefreshCommands(); } }
    public int DurationSeconds => Preferences.Default.Get("practice_duration", 30);

    public ICommand NewTopicCommand { get; }
    public ICommand RecordCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand UseSampleCommand { get; }
    public ICommand PlayRecordingCommand { get; }
    public ICommand PlayReferenceCommand { get; }
    public ICommand RetryCommand { get; }

    private async Task StartRecordingAsync()
    {
        try
        {
            ResetAttempt();
            await _audio.StartAsync();
            _recordingStarted = DateTimeOffset.Now;
            IsRecording = true;
            Status = "Recording — speak naturally";
            _timerCancellation = new CancellationTokenSource();
            _ = RunTimerAsync(DurationSeconds, _timerCancellation.Token);
        }
        catch (Exception exception) { await ShowErrorAsync(exception.Message); }
    }

    private async Task RunTimerAsync(int seconds, CancellationToken cancellationToken)
    {
        try
        {
            for (var remaining = seconds; remaining >= 0; remaining--)
            {
                TimerText = $"0:{remaining:00}";
                await Task.Delay(1000, cancellationToken);
            }
            await MainThread.InvokeOnMainThreadAsync(StopRecordingAsync);
        }
        catch (OperationCanceledException) { }
    }

    private async Task StopRecordingAsync()
    {
        if (!IsRecording) return;
        _timerCancellation?.Cancel();
        IsRecording = false;
        IsBusy = true;
        Status = "Preparing your recording…";
        try
        {
            _audioBytes = await _audio.StopAsync();
            ((Command)PlayRecordingCommand).ChangeCanExecute();
            if (await _coach.IsConfiguredAsync())
            {
                Status = "Transcribing every word and filler…";
                Transcript = await _coach.TranscribeAsync(_audioBytes);
                await AnalyzeTranscriptAsync();
            }
            else
            {
                Status = "Recording saved. Paste or type your transcript to analyze locally.";
                ShowTranscriptEditor = true;
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Transcription failed: {exception}");
            ShowTranscriptEditor = true;
            Status = $"Transcription failed: {exception.Message} Your recording is safe, and you can enter a transcript manually.";
            await ShowErrorAsync(exception.Message);
        }
        finally { IsBusy = false; }
    }

    private async Task AnalyzeTranscriptAsync()
    {
        if (string.IsNullOrWhiteSpace(Transcript)) return;
        IsBusy = true;
        ShowTranscriptEditor = true;
        Status = "Turning your attempt into practical feedback…";
        try
        {
            var elapsed = _recordingStarted == default ? TimeSpan.FromSeconds(DurationSeconds) : DateTimeOffset.Now - _recordingStarted;
            if (elapsed.TotalSeconds > DurationSeconds + 3) elapsed = TimeSpan.FromSeconds(DurationSeconds);
            Analysis = _analyzer.Analyze(Transcript, elapsed);
            var aiConfigured = await _coach.IsConfiguredAsync();
            Feedback = aiConfigured
                ? await _coach.CoachAsync(TopicPrompt, Transcript, Analysis)
                : CreateLocalFeedback(Transcript, Analysis);

            await _store.AddAsync(new PracticeSession
            {
                Topic = TopicPrompt,
                Transcript = Transcript,
                DurationSeconds = Math.Max(1, (int)Math.Round(elapsed.TotalSeconds)),
                Analysis = Analysis,
                Feedback = Feedback,
            });
            Status = aiConfigured ? "Your coaching report is ready" : "Local analysis ready — add an API key for deeper AI coaching";

            if (aiConfigured && !string.IsNullOrWhiteSpace(Feedback.ImprovedScript))
            {
                Status = "Creating your AI reference delivery…";
                _referenceAudioPath = await _coach.GenerateReferenceAudioAsync(Feedback.ImprovedScript);
                ((Command)PlayReferenceCommand).ChangeCanExecute();
                Status = "Coaching complete";
            }
        }
        catch (Exception exception) { await ShowErrorAsync(exception.Message); }
        finally { IsBusy = false; }
    }

    private async Task UseSampleAsync()
    {
        Transcript = "So, um, I think a small habit that improved my life is planning tomorrow before I finish work. It basically helps me focus, and, you know, I start the morning with a clear priority instead of reacting to everything.";
        _recordingStarted = DateTimeOffset.Now.AddSeconds(-30);
        ShowTranscriptEditor = true;
        await AnalyzeTranscriptAsync();
    }

    private static CoachingFeedback CreateLocalFeedback(string transcript, SpeechAnalysis analysis)
    {
        var pace = analysis.WordsPerMinute switch
        {
            < 80 => "Your pace looks measured; add a little more forward energy.",
            > 180 => "Your pace looks fast; slow down at the end of each idea.",
            _ => "Your estimated pace sits in a clear conversational range.",
        };
        var improvement = analysis.FillerCount == 0
            ? "No common verbal fillers were found in the transcript. Focus next on concise sentence endings."
            : $"Replace your {analysis.FillerCount} detected filler{(analysis.FillerCount == 1 ? "" : "s")} with a silent one-beat pause.";
        return new CoachingFeedback
        {
            OverallScore = analysis.Score,
            Headline = analysis.Score >= 85 ? "Clear and controlled" : analysis.Score >= 70 ? "Strong base, cleaner pauses next" : "Good rep — simplify and pause",
            Strength = pace,
            Improvement = improvement,
            NextDrill = "Repeat once. Speak in three parts: point, example, takeaway.",
            ImprovedScript = transcript,
        };
    }

    private void NewTopic()
    {
        _topic = _topics.Next();
        Notify(nameof(TopicCategory));
        Notify(nameof(TopicPrompt));
        ResetAttempt();
    }

    private void ResetAttempt()
    {
        _timerCancellation?.Cancel();
        Transcript = string.Empty;
        Analysis = null;
        Feedback = null;
        _audioBytes = null;
        _referenceAudioPath = null;
        ShowTranscriptEditor = false;
        TimerText = $"0:{DurationSeconds:00}";
        Status = "Ready when you are";
        ((Command)PlayReferenceCommand).ChangeCanExecute();
    }

    private void RefreshCommands()
    {
        ((Command)NewTopicCommand).ChangeCanExecute();
        ((Command)RecordCommand).ChangeCanExecute();
        ((Command)StopCommand).ChangeCanExecute();
        ((Command)AnalyzeCommand).ChangeCanExecute();
        ((Command)UseSampleCommand).ChangeCanExecute();
        ((Command)RetryCommand).ChangeCanExecute();
    }

    private static Task ShowErrorAsync(string message) =>
        Shell.Current?.DisplayAlertAsync("SpeakSharp", message, "OK") ?? Task.CompletedTask;
}
