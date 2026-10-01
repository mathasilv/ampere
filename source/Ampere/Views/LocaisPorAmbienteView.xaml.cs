using System.Windows;
using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class LocaisPorAmbienteView
{
    private readonly LocaisPorAmbienteViewModel _viewModel;

    public LocaisPorAmbienteView(LocaisPorAmbienteViewModel viewModel)
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
