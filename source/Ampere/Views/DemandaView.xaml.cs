using System.Windows;
using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class DemandaView
{
    private readonly DemandaViewModel _viewModel;

    public DemandaView(DemandaViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Confirmar(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Confirmar()) DialogResult = true;
    }
}
