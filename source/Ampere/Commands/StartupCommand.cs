using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using Ampere.ViewModels;
using Ampere.Views;

namespace Ampere.Commands;

/// <summary>
///     External command entry point.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class StartupCommand : ExternalCommand
{
    public override void Execute()
    {
        var viewModel = new AmpereViewModel();
        var view = new AmpereView(viewModel);
        view.ShowDialog();
    }
}