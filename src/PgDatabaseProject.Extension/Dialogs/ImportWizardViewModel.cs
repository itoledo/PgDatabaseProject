using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Threading;
using PgDatabaseProject.Core.Models;
using PgDatabaseProject.SchemaExtractor;

namespace PgDatabaseProject.Extension.Dialogs;

internal sealed class ImportWizardViewModel : INotifyPropertyChanged
{
    private readonly string _projectPath;
    private readonly Action _closeAction;
    private int _currentStep = 1;
    private CancellationTokenSource? _cts;

    // Connection properties
    public string Host { get; set; } = "localhost";
    public string Port { get; set; } = "5432";
    public string Database { get; set; } = "";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "";
    public string SslMode { get; set; } = "Prefer";

    // Step 2 properties
    public ObservableCollection<SelectableItem> AvailableSchemas { get; } = new();
    public ObservableCollection<SelectableItem> AvailableObjectTypes { get; } = new();
    public bool IncludeDropIfExists { get; set; }
    public bool OverwriteExisting { get; set; } = true;

    // Step 3 properties
    public ObservableCollection<string> LogMessages { get; } = new();

    private string _connectionStatus = "";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { _connectionStatus = value; OnPropertyChanged(nameof(ConnectionStatus)); }
    }

    private Brush _connectionStatusColor = Brushes.Black;
    public Brush ConnectionStatusColor
    {
        get => _connectionStatusColor;
        set { _connectionStatusColor = value; OnPropertyChanged(nameof(ConnectionStatusColor)); }
    }

    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set { _progressValue = value; OnPropertyChanged(nameof(ProgressValue)); }
    }

    private string _progressText = "";
    public string ProgressText
    {
        get => _progressText;
        set { _progressText = value; OnPropertyChanged(nameof(ProgressText)); }
    }

    public string StepTitle => _currentStep switch
    {
        1 => "Step 1: Database Connection",
        2 => "Step 2: Select Objects to Import",
        3 => "Step 3: Import Progress",
        _ => ""
    };

    public Visibility IsStep1Visible => _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsStep2Visible => _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsStep3Visible => _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

    public string NextButtonText => _currentStep switch
    {
        1 => "Next >",
        2 => "Import",
        3 => "Close",
        _ => "Next >"
    };

    public ICommand TestConnectionCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }

    public ImportWizardViewModel(string projectPath, Action closeAction)
    {
        _projectPath = projectPath;
        _closeAction = closeAction;

        TestConnectionCommand = new RelayCommand(OnTestConnection);
        NextCommand = new RelayCommand(OnNext);
        BackCommand = new RelayCommand(OnBack, () => _currentStep == 2);

        // Initialize object types
        foreach (var type in Enum.GetValues(typeof(DatabaseObjectType)).Cast<DatabaseObjectType>())
        {
            AvailableObjectTypes.Add(new SelectableItem(type.ToString()));
        }
    }

    private ConnectionSettings BuildConnectionSettings() => new()
    {
        Host = Host,
        Port = int.TryParse(Port, out var p) ? p : 5432,
        Database = Database,
        Username = Username,
        Password = Password,
        SslMode = SslMode
    };

    private void OnTestConnection()
    {
        TestConnectionCoreAsync().Forget();
    }

    private async Task TestConnectionCoreAsync()
    {
        ConnectionStatus = "Testing connection...";
        ConnectionStatusColor = Brushes.Gray;

        try
        {
            using var extractor = new PgSchemaExtractor(BuildConnectionSettings());
            await extractor.TestConnectionAsync();
            ConnectionStatus = "Connection successful!";
            ConnectionStatusColor = Brushes.Green;
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Failed: {ex.Message}";
            ConnectionStatusColor = Brushes.Red;
        }
    }

    private void OnNext()
    {
        switch (_currentStep)
        {
            case 1:
                LoadSchemasAsync().Forget();
                break;
            case 2:
                RunImportAsync().Forget();
                break;
            case 3:
                _closeAction();
                break;
        }
    }

    private void OnBack()
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            NotifyStepChanged();
        }
    }

    private async Task LoadSchemasAsync()
    {
        try
        {
            ConnectionStatus = "Loading schemas...";
            ConnectionStatusColor = Brushes.Gray;

            using var extractor = new PgSchemaExtractor(BuildConnectionSettings());
            var schemas = await extractor.GetSchemasAsync();

            AvailableSchemas.Clear();
            foreach (var schema in schemas)
            {
                AvailableSchemas.Add(new SelectableItem(schema));
            }

            _currentStep = 2;
            NotifyStepChanged();
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Failed to load schemas: {ex.Message}";
            ConnectionStatusColor = Brushes.Red;
        }
    }

    private async Task RunImportAsync()
    {
        _currentStep = 3;
        NotifyStepChanged();

        _cts = new CancellationTokenSource();

        var selectedSchemas = AvailableSchemas
            .Where(s => s.IsSelected)
            .Select(s => s.Name)
            .ToList();

        var selectedTypes = AvailableObjectTypes
            .Where(t => t.IsSelected)
            .Select(t => (DatabaseObjectType)Enum.Parse(typeof(DatabaseObjectType), t.Name))
            .ToList();

        var options = new ImportOptions
        {
            Schemas = selectedSchemas,
            ObjectTypes = selectedTypes,
            IncludeDropIfExists = IncludeDropIfExists,
            OverwriteExisting = OverwriteExisting
        };

        var progress = new Progress<ImportProgress>(p =>
        {
            ProgressValue = p.TotalCount > 0 ? (p.ProcessedCount * 100) / p.TotalCount : 0;
            ProgressText = p.CurrentObject;

            var prefix = p.IsError ? "[ERROR] " : "[OK] ";
            LogMessages.Add($"{prefix}{p.CurrentObject}");

            if (p.ErrorMessage != null)
                LogMessages.Add($"       {p.ErrorMessage}");
        });

        try
        {
            LogMessages.Add($"Connecting to {Host}:{Port}/{Database}...");

            using var extractor = new PgSchemaExtractor(BuildConnectionSettings());
            var objects = await Task.Run(
                () => extractor.ExtractObjectsAsync(options, progress, _cts.Token));

            LogMessages.Add($"Extracted {objects.Count} objects.");
            LogMessages.Add($"Writing files to {_projectPath}...");

            var writer = new ScriptFileWriter();
            await Task.Run(() => writer.WriteAsync(_projectPath, objects, options, _cts.Token));

            ProgressValue = 100;
            ProgressText = "Import completed!";
            LogMessages.Add($"Done! {objects.Count} files written.");
        }
        catch (OperationCanceledException)
        {
            LogMessages.Add("Import cancelled by user.");
            ProgressText = "Cancelled.";
        }
        catch (Exception ex)
        {
            LogMessages.Add($"[ERROR] {ex.Message}");
            ProgressText = "Import failed.";
        }
    }

    private void NotifyStepChanged()
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(IsStep1Visible));
        OnPropertyChanged(nameof(IsStep2Visible));
        OnPropertyChanged(nameof(IsStep3Visible));
        OnPropertyChanged(nameof(NextButtonText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
