using System.Windows;
using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class DimensionamentoView
{
    private readonly DimensionamentoViewModel _viewModel;

    public DimensionamentoView(DimensionamentoViewModel viewModel)
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
