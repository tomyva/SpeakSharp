using SpeakSharp.Models;
using SpeakSharp.Services;

namespace SpeakSharp.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ReminderService _reminders;
    private readonly OpenAiSpeechCoach _coach;
    private string _apiKey;
    private int _duration;
    private TimeSpan _morning;
    private TimeSpan _afternoon;
    private TimeSpan _evening;
    private string _status = string.Empty;

    public SettingsViewModel(ReminderService reminders, OpenAiSpeechCoach coach)
    {
        _reminders = reminders;
        _coach = coach;
        _apiKey = string.Empty;
        _duration = Preferences.Default.Get("practice_duration", 30);
        var values = reminders.Load();
        _morning = values.ElementAtOrDefault(0)?.Time ?? new TimeSpan(9, 0, 0);
        _afternoon = values.ElementAtOrDefault(1)?.Time ?? new TimeSpan(14, 0, 0);
        _evening = values.ElementAtOrDefault(2)?.Time ?? new TimeSpan(19, 0, 0);
    }

    public IReadOnlyList<int> Durations { get; } = [15, 30, 45, 60];
    public string ApiKey { get => _apiKey; set => SetProperty(ref _apiKey, value); }
    public int Duration { get => _duration; set => SetProperty(ref _duration, value); }
    public TimeSpan Morning { get => _morning; set => SetProperty(ref _morning, value); }
    public TimeSpan Afternoon { get => _afternoon; set => SetProperty(ref _afternoon, value); }
    public TimeSpan Evening { get => _evening; set => SetProperty(ref _evening, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public async Task LoadAsync() => ApiKey = await _coach.GetApiKeyAsync();

    public async Task SaveAsync()
    {
        await _coach.SetApiKeyAsync(ApiKey);
        Preferences.Default.Set("practice_duration", Duration);
        await _reminders.SaveAndScheduleAsync(
        [
            new ReminderTime(Morning.Hours, Morning.Minutes),
            new ReminderTime(Afternoon.Hours, Afternoon.Minutes),
            new ReminderTime(Evening.Hours, Evening.Minutes),
        ]);
#if WINDOWS
        Status = "Saved. Mobile builds schedule daily notifications; Windows keeps these times as your practice plan.";
#else
        Status = "Saved. Your daily coaching notifications are scheduled.";
#endif
    }
}
