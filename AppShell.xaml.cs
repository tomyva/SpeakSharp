namespace SpeakSharp;

public partial class AppShell : Shell
{
	public AppShell(MainPage practicePage, ProgressPage progressPage, SettingsPage settingsPage)
	{
		InitializeComponent();
		Items.Add(new TabBar
		{
			Items =
			{
				new ShellContent { Title = "Practice", Route = "practice", Content = practicePage },
				new ShellContent { Title = "Progress", Route = "progress", Content = progressPage },
				new ShellContent { Title = "Settings", Route = "settings", Content = settingsPage },
			},
		});
	}
}
