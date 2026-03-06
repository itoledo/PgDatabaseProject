using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using PgDatabaseProject.Extension.Dialogs;
using PgDatabaseProject.Extension.Services;
using Task = System.Threading.Tasks.Task;

namespace PgDatabaseProject.Extension.Commands;

internal sealed class ConnectDatabaseCommand
{
    public const int CommandId = 0x0200;
    public static readonly Guid CommandSet = new("f2a1b3c4-d5e6-7890-abcd-123456789abc");

    private readonly AsyncPackage _package;

    private ConnectDatabaseCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        _package = package;
        var menuCommandId = new CommandID(CommandSet, CommandId);
        var menuItem = new OleMenuCommand(Execute, menuCommandId);
        menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
        commandService.AddCommand(menuItem);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        if (commandService != null)
            new ConnectDatabaseCommand(package, commandService);
    }

    private void OnBeforeQueryStatus(object sender, EventArgs e)
    {
        if (sender is OleMenuCommand cmd)
        {
            var mgr = ConnectionManager.Instance;
            cmd.Text = mgr.IsConnected
                ? $"Connected: {mgr.DatabaseName}"
                : "Connect to PostgreSQL";
        }
    }

    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (ConnectionManager.Instance.IsConnected)
        {
            var result = System.Windows.MessageBox.Show(
                $"Connected to '{ConnectionManager.Instance.DatabaseName}'.\n\nDisconnect?",
                "PostgreSQL Connection",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                ConnectionManager.Instance.Disconnect();
            }
            return;
        }

        var dialog = new ConnectDialog();
        if (dialog.ShowDialog() == true && dialog.ResultConnectionString != null)
        {
            ConnectionManager.Instance.SetConnection(
                dialog.ResultConnectionString,
                dialog.ResultDatabaseName ?? "");
        }
    }
}
