using System.Collections.ObjectModel;
using SpeakSharp.Models;
using SpeakSharp.Services;

namespace SpeakSharp.ViewModels;

public sealed class ProgressViewModel(SessionStore store) : ObservableObject
{
    private string _summary = "Complete your first practice to start a streak.";
    public ObservableCollection<PracticeSession> Sessions { get; } = [];
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    public async Task LoadAsync()
    {
        var sessions = await store.LoadAsync();
        Sessions.Clear();
        foreach (var session in sessions) Sessions.Add(session);
        if (sessions.Count == 0) return;
        var recent = sessions.Take(7).ToArray();
        Summary = $"{sessions.Count} practice round{(sessions.Count == 1 ? "" : "s")} · {recent.Average(x => x.Feedback.OverallScore):0} average score · {recent.Sum(x => x.Analysis.FillerCount)} fillers in your last {recent.Length}";
    }
}
