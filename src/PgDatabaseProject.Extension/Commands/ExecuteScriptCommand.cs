using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Npgsql;
using PgDatabaseProject.Extension.Services;
using Task = System.Threading.Tasks.Task;

namespace PgDatabaseProject.Extension.Commands;

internal sealed class ExecuteScriptCommand
{
    public const int CommandId = 0x0300;
    public static readonly Guid CommandSet = new("f2a1b3c4-d5e6-7890-abcd-123456789abc");
    private static readonly Guid OutputPaneGuid = new("b1e8a9c0-d2f3-4567-890a-bcdef1234567");

    private readonly AsyncPackage _package;

    private ExecuteScriptCommand(AsyncPackage package, OleMenuCommandService commandService)
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
            new ExecuteScriptCommand(package, commandService);
    }

    private void OnBeforeQueryStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is OleMenuCommand cmd)
        {
            cmd.Enabled = ConnectionManager.Instance.IsConnected && IsActiveSqlDocument();
        }
    }

    private bool IsActiveSqlDocument()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
        var doc = dte?.ActiveDocument;
        if (doc == null) return false;
        var ext = Path.GetExtension(doc.FullName);
        return string.Equals(ext, ".pgsql", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".sql", StringComparison.OrdinalIgnoreCase);
    }

    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (!ConnectionManager.Instance.IsConnected)
        {
            VsShellUtilities.ShowMessageBox(_package,
                "Not connected to a database. Use the Connect button first.",
                "Execute Script", OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        var sql = GetActiveDocumentSql();
        if (string.IsNullOrWhiteSpace(sql))
        {
            VsShellUtilities.ShowMessageBox(_package,
                "No SQL text to execute.",
                "Execute Script", OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        var pane = GetOutputPane();
        WriteToPane(pane, $"-- Executing on {ConnectionManager.Instance.DatabaseName} --\n");
        pane.Activate();

        // Show the Output window
        var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
        dte?.Windows.Item(EnvDTE.Constants.vsWindowKindOutput)?.Activate();

        ExecuteSqlAsync(sql!, pane).Forget();
    }

    private string? GetActiveDocumentSql()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
        var doc = dte?.ActiveDocument;
        if (doc == null) return null;

        var textDoc = doc.Object("TextDocument") as TextDocument;
        if (textDoc == null) return null;

        // Use selection if available
        var selection = textDoc.Selection;
        if (selection != null && !string.IsNullOrWhiteSpace(selection.Text))
            return selection.Text;

        var startPoint = textDoc.StartPoint.CreateEditPoint();
        return startPoint.GetText(textDoc.EndPoint);
    }

    private async Task ExecuteSqlAsync(string sql, IVsOutputWindowPane pane)
    {
        var connectionString = ConnectionManager.Instance.ConnectionString!;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var output = await Task.Run(() => RunSql(connectionString, sql));

            stopwatch.Stop();
            WriteToPane(pane, output);
            WriteToPane(pane, $"\n-- Completed in {stopwatch.ElapsedMilliseconds} ms --\n\n");
        }
        catch (NpgsqlException ex)
        {
            stopwatch.Stop();
            WriteToPane(pane, $"\n-- ERROR --\n{ex.Message}\n");
            WriteToPane(pane, $"-- Failed after {stopwatch.ElapsedMilliseconds} ms --\n\n");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            WriteToPane(pane, $"\n-- ERROR --\n{ex.Message}\n\n");
        }
    }

    private static string RunSql(string connectionString, string sql)
    {
        var sb = new StringBuilder();

        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.CommandTimeout = 60;

        using var reader = cmd.ExecuteReader();
        var hasResultSet = false;

        do
        {
            if (reader.FieldCount > 0)
            {
                if (hasResultSet)
                    sb.AppendLine();
                hasResultSet = true;
                FormatResultSet(reader, sb);
            }
        } while (reader.NextResult());

        if (!hasResultSet)
        {
            var affected = reader.RecordsAffected;
            if (affected >= 0)
                sb.AppendLine($"({affected} rows affected)");
            else
                sb.AppendLine("Command executed successfully.");
        }

        return sb.ToString();
    }

    private static void FormatResultSet(NpgsqlDataReader reader, StringBuilder sb)
    {
        var columnCount = reader.FieldCount;
        var columns = new string[columnCount];
        var widths = new int[columnCount];
        var rows = new List<string[]>();

        for (var i = 0; i < columnCount; i++)
        {
            columns[i] = reader.GetName(i);
            widths[i] = columns[i].Length;
        }

        const int maxRows = 1000;
        const int maxColWidth = 50;

        while (reader.Read() && rows.Count < maxRows)
        {
            var row = new string[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                row[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "NULL";
                if (row[i].Length > widths[i])
                    widths[i] = Math.Min(row[i].Length, maxColWidth);
            }
            rows.Add(row);
        }

        // Header
        for (var i = 0; i < columnCount; i++)
        {
            if (i > 0) sb.Append(" | ");
            sb.Append(columns[i].PadRight(widths[i]));
        }
        sb.AppendLine();

        // Separator
        for (var i = 0; i < columnCount; i++)
        {
            if (i > 0) sb.Append("-+-");
            sb.Append(new string('-', widths[i]));
        }
        sb.AppendLine();

        // Rows
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                var val = row[i].Length > maxColWidth
                    ? row[i].Substring(0, maxColWidth - 3) + "..."
                    : row[i];
                sb.Append(val.PadRight(widths[i]));
            }
            sb.AppendLine();
        }

        var rowLabel = rows.Count == 1 ? "row" : "rows";
        sb.AppendLine($"({rows.Count} {rowLabel})");
    }

    private IVsOutputWindowPane GetOutputPane()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var outputWindow = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));
        var paneGuid = OutputPaneGuid;
        outputWindow.CreatePane(ref paneGuid, "PostgreSQL", 1, 1);
        outputWindow.GetPane(ref paneGuid, out var pane);
        return pane;
    }

#pragma warning disable VSTHRD010 // OutputStringThreadSafe is designed for cross-thread use
    private static void WriteToPane(IVsOutputWindowPane pane, string text)
    {
        pane.OutputStringThreadSafe(text);
    }
#pragma warning restore VSTHRD010
}
