using System.Windows;

namespace PgDatabaseProject.Extension.Dialogs;

public partial class ImportWizardDialog : Window
{
    public ImportWizardDialog(string projectPath)
    {
        InitializeComponent();
        var viewModel = new ImportWizardViewModel(projectPath, () => Close());
        DataContext = viewModel;

        // Wire up PasswordBox (can't bind directly in WPF)
        PasswordBox.PasswordChanged += (s, e) => viewModel.Password = PasswordBox.Password;
    }
}
