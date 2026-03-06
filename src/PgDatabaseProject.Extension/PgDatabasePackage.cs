using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using PgDatabaseProject.Extension.Commands;
using Task = System.Threading.Tasks.Task;

namespace PgDatabaseProject.Extension;

/// <summary>
/// PostgreSQL Database Project package. Registers the .pgproj extension with Visual Studio
/// and provides the Import Database command.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("PostgreSQL Database Project", "Manage PostgreSQL database schemas with SQL scripts", "1.0")]
[Guid(PackageGuidString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideBindingPath]
[ProvideTextMateGrammarDirectory("PgDatabaseProject", "Grammars")]
public sealed class PgDatabasePackage : AsyncPackage
{
    public const string PackageGuidString = "d4e85a01-7b5c-4f2a-9c1d-3e6f8a0b2c4d";

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await ImportDatabaseCommand.InitializeAsync(this);
    }
}
