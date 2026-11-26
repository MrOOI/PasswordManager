using System.Windows.Controls;
using System.Windows.Input;

namespace PassManager.App.Views;

public partial class VaultView : UserControl
{
    public VaultView()
    {
        InitializeComponent();

        // Ctrl+F is the standard gesture for ApplicationCommands.Find; focusing is a view-only concern.
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }));

        Loaded += (_, _) => EntryList.Focus();
    }
}
