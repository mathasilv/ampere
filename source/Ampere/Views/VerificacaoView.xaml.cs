using System.Windows;
using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class VerificacaoView
{
    private readonly VerificacaoViewModel _viewModel;

    public VerificacaoView(VerificacaoViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Selecionar(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Selecionar()) DialogResult = true;
    }
}
