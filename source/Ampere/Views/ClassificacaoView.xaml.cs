using System.Windows;
using Ampere.ViewModels;

namespace Ampere.Views;

public sealed partial class ClassificacaoView
{
    private readonly ClassificacaoViewModel _viewModel;

    public ClassificacaoView(ClassificacaoViewModel viewModel)
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
