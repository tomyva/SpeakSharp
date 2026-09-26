using SpeakSharp.ViewModels;

namespace SpeakSharp;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        // Android can deliver a final Entry focus callback while the activity is
        // being torn down. Release focus while MAUI's service provider is alive.
        ApiKeyEntry.Unfocus();
        base.OnDisappearing();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        // Saving may open the OS notification-permission UI. Do not leave the
        // password Entry focused while Android moves our activity to the back.
        ApiKeyEntry.Unfocus();
        await _viewModel.SaveAsync();
    }
}
