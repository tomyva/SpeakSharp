using System.Text.Json;
using SpeakSharp.Models;
#if ANDROID || IOS || MACCATALYST
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
#endif

namespace SpeakSharp.Services;

public sealed class ReminderService
{
    private const string PreferenceKey = "practice_reminders";

    public IReadOnlyList<ReminderTime> Load()
    {
        var raw = Preferences.Default.Get(PreferenceKey, string.Empty);
        return string.IsNullOrWhiteSpace(raw)
            ? [new ReminderTime(9, 0), new ReminderTime(14, 0), new ReminderTime(19, 0)]
            : JsonSerializer.Deserialize<List<ReminderTime>>(raw) ?? [];
    }

    public async Task SaveAndScheduleAsync(IReadOnlyList<ReminderTime> reminders)
    {
        Preferences.Default.Set(PreferenceKey, JsonSerializer.Serialize(reminders));
#if ANDROID || IOS || MACCATALYST
        var center = LocalNotificationCenter.Current;
        await center.RequestNotificationPermission();
        for (var id = 8000; id < 8010; id++) center.Cancel(id);
        for (var index = 0; index < reminders.Count; index++)
        {
            var reminder = reminders[index];
            var next = DateTime.Today.Add(reminder.Time);
            if (next <= DateTime.Now) next = next.AddDays(1);
            var request = new NotificationRequest
            {
                NotificationId = 8000 + index,
                Title = "Your voice deserves practice",
                Description = "A focused SpeakSharp round takes less than a minute.",
                ReturningData = "practice",
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = next,
                    RepeatType = NotificationRepeat.Daily,
                },
            };
            await center.Show(request);
        }
#else
        await Task.CompletedTask;
#endif
    }
}
