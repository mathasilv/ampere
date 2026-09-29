using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class AmpereView
{
    public AmpereView(AmpereViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}