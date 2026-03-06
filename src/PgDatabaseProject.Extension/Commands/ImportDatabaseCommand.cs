using System;
using System.ComponentModel.Design;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace PgDatabaseProject.Extension.Commands;

internal sealed class ImportDatabaseCommand
{
    public const int CommandId = 0x0100;
    public static readonly Guid CommandSet = new("f2a1b3c4-d5e6-7890-abcd-123456789abc");

    private readonly AsyncPackage _package;

    private ImportDatabaseCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        _package = package;
        var menuCommandId = new CommandID(CommandSet, CommandId);
        var menuItem = new MenuCommand(Execute, menuCommandId);
        commandService.AddCommand(menuItem);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        if (commandService != null)
        {
            new ImportDatabaseCommand(package, commandService);
        }
    }

    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var projectPath = GetActiveProjectPath();
        if (projectPath == null)
        {
            VsShellUtilities.ShowMessageBox(
                _package,
                "No active project found. Please open a PostgreSQL Database Project first.",
                "Import PostgreSQL Database",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        var dialog = new Dialogs.ImportWizardDialog(projectPath);
        dialog.ShowDialog();
    }

    private string? GetActiveProjectPath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (!(Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsSolution)) is IVsSolution solution))
            return null;

        // Try to get the selected project in Solution Explorer
        if (!(Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsShellMonitorSelection)) is IVsMonitorSelection monitorSelection))
            return null;

        monitorSelection.GetCurrentSelection(out var hierarchyPtr, out _, out _, out _);
        if (hierarchyPtr == IntPtr.Zero)
            return null;

        var hierarchy = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
        System.Runtime.InteropServices.Marshal.Release(hierarchyPtr);

        if (hierarchy == null)
            return null;

        hierarchy.GetProperty((uint)Microsoft.VisualStudio.VSConstants.VSITEMID.Root,
            (int)__VSHPROPID.VSHPROPID_ProjectDir, out var projectDirObj);

        return projectDirObj as string;
    }
}
