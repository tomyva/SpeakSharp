using SpeakSharp.ViewModels;

namespace SpeakSharp;

public partial class MainPage : ContentPage
{
    public MainPage(PracticeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
