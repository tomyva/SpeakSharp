namespace SpeakSharp.Models;

public sealed record PracticeTopic(string Category, string Prompt);

public sealed record FillerBreakdown(string Phrase, int Count);

public sealed class SpeechAnalysis
{
    public int WordCount { get; init; }
    public double WordsPerMinute { get; init; }
    public int FillerCount { get; init; }
    public double FillerRate { get; init; }
    public int Score { get; init; }
    public IReadOnlyList<FillerBreakdown> Fillers { get; init; } = [];
}

public sealed class CoachingFeedback
{
    public int OverallScore { get; init; }
    public string Headline { get; init; } = "Good practice";
    public string Strength { get; init; } = "You completed the exercise.";
    public string Improvement { get; init; } = "Pause briefly instead of filling silence.";
    public string NextDrill { get; init; } = "Try once more with one idea per sentence.";
    public string ImprovedScript { get; init; } = string.Empty;
}

public sealed class PracticeSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;
    public string Topic { get; init; } = string.Empty;
    public string Transcript { get; init; } = string.Empty;
    public int DurationSeconds { get; init; }
    public SpeechAnalysis Analysis { get; init; } = new();
    public CoachingFeedback Feedback { get; init; } = new();
}

public sealed record ReminderTime(int Hour, int Minute)
{
    public TimeSpan Time => new(Hour, Minute, 0);
    public override string ToString() => DateTime.Today.Add(Time).ToString("h:mm tt");
}
