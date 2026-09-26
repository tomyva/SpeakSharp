using Microsoft.Extensions.DependencyInjection;

namespace SpeakSharp;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Resolve the visual tree only after InitializeComponent has loaded the
		// application resource dictionaries used by each page's StaticResources.
		return new Window(_services.GetRequiredService<AppShell>());
	}
}
