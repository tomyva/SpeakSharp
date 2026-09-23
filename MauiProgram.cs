using Microsoft.Extensions.Logging;

using Plugin.Maui.Audio;
using SpeakSharp.Services;
using SpeakSharp.ViewModels;
#if ANDROID || IOS || MACCATALYST
using Plugin.LocalNotification;
#endif

namespace SpeakSharp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.AddAudio()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if ANDROID || IOS || MACCATALYST
		builder.UseLocalNotification();
#endif

		builder.Services.AddSingleton<TopicService>();
		builder.Services.AddSingleton<AudioPracticeService>();
		builder.Services.AddSingleton<FillerAnalysisService>();
		builder.Services.AddSingleton<SessionStore>();
		builder.Services.AddSingleton<ReminderService>();
		builder.Services.AddSingleton(new HttpClient
		{
			BaseAddress = new Uri("https://api.openai.com/v1/"),
			Timeout = TimeSpan.FromSeconds(90),
		});
		builder.Services.AddSingleton<OpenAiSpeechCoach>();
		builder.Services.AddSingleton<PracticeViewModel>();
		builder.Services.AddSingleton<ProgressViewModel>();
		builder.Services.AddSingleton<SettingsViewModel>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<ProgressPage>();
		builder.Services.AddSingleton<SettingsPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
