using System.Windows;
using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class QuadroDeCargasView
{
    private readonly QuadroDeCargasViewModel _viewModel;

    public QuadroDeCargasView(QuadroDeCargasViewModel viewModel)
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
