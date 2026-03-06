using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using PgDatabaseProject.Extension.Commands;
using Task = System.Threading.Tasks.Task;

namespace PgDatabaseProject.Extension;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("PostgreSQL Database Project", "Manage PostgreSQL database schemas with SQL scripts", "1.0")]
[Guid(PackageGuidString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideBindingPath]
[ProvideTextMateGrammarDirectory("PgDatabaseProject", "Grammars")]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class PgDatabasePackage : AsyncPackage
{
    public const string PackageGuidString = "d4e85a01-7b5c-4f2a-9c1d-3e6f8a0b2c4d";
    public const string PgsqlUIContextGuid = "c8d9e0f1-a2b3-4567-8901-23456789abcd";

    // Must hold references to prevent COM event sinks from being GC'd
    private Events? _dteEvents;
    private WindowEvents? _windowEvents;

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await ImportDatabaseCommand.InitializeAsync(this);
        await ConnectDatabaseCommand.InitializeAsync(this);
        await ExecuteScriptCommand.InitializeAsync(this);

        TrackActiveDocument();
    }

    private void TrackActiveDocument()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (GetService(typeof(DTE)) is not DTE dte) return;
        _dteEvents = dte.Events;
        _windowEvents = _dteEvents.WindowEvents;
        _windowEvents.WindowActivated += OnWindowActivated;
    }

    private void OnWindowActivated(Window gotFocus, Window lostFocus)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var isSqlFile = false;
        try
        {
            var doc = gotFocus?.Document;
            if (doc != null)
            {
                var ext = Path.GetExtension(doc.FullName);
                isSqlFile = string.Equals(ext, ".pgsql", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(ext, ".sql", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Window may not have a document (e.g. tool windows)
        }

        SetPgsqlUIContext(isSqlFile);
    }

    private void SetPgsqlUIContext(bool active)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (GetService(typeof(SVsShellMonitorSelection)) is IVsMonitorSelection monitorSelection)
        {
            var contextGuid = new Guid(PgsqlUIContextGuid);
            monitorSelection.GetCmdUIContextCookie(ref contextGuid, out var cookie);
            monitorSelection.SetCmdUIContext(cookie, active ? 1 : 0);
        }
    }
}
