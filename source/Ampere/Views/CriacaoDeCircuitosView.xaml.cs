using System.Windows;
using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class CriacaoDeCircuitosView
{
    private readonly CriacaoDeCircuitosViewModel _viewModel;

    public CriacaoDeCircuitosView(CriacaoDeCircuitosViewModel viewModel)
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
