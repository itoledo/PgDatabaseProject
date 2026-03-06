using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Npgsql;
using PgDatabaseProject.Extension.Services;

namespace PgDatabaseProject.Extension.Dialogs;

internal partial class ConnectDialog : Window
{
    private readonly ObservableCollection<ConnectionEntry> _recentConnections = new();

    public string? ResultConnectionString { get; private set; }
    public string? ResultDatabaseName { get; private set; }

    public ConnectDialog()
    {
        InitializeComponent();

        RecentList.ItemsSource = _recentConnections;
        LoadRecentConnections();

        // Show "New Connection" tab if no recent connections
        if (_recentConnections.Count == 0)
            MainTabs.SelectedItem = NewConnectionTab;
    }

    private void LoadRecentConnections()
    {
        _recentConnections.Clear();
        foreach (var entry in ConnectionStore.Load())
            _recentConnections.Add(entry);

        EmptyMessage.Visibility = _recentConnections.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnConnect(object sender, RoutedEventArgs e)
    {
        if (MainTabs.SelectedItem == RecentTab)
        {
            // Connect using selected recent connection
            if (RecentList.SelectedItem is ConnectionEntry entry)
                ConnectWithEntry(entry);
            else
                StatusText.Text = "Select a connection from the list.";
        }
        else
        {
            // Connect using form fields
            ConnectWithEntry(BuildEntryFromFields());
        }
    }

    private void OnRecentDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentList.SelectedItem is ConnectionEntry entry)
            ConnectWithEntry(entry);
    }

    private void OnDeleteRecent(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ConnectionEntry entry)
        {
            ConnectionStore.Remove(entry);
            _recentConnections.Remove(entry);
            EmptyMessage.Visibility = _recentConnections.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void OnParseConnectionString(object sender, RoutedEventArgs e)
    {
        var text = ConnStringBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            StatusText.Text = "Paste a connection string first.";
            StatusText.Foreground = Brushes.Orange;
            return;
        }

        var entry = ConnectionEntry.ParseConnectionString(text);
        if (entry == null)
        {
            StatusText.Text = "Could not parse connection string. Expected format: Host=...;Port=...;Database=...";
            StatusText.Foreground = Brushes.Red;
            return;
        }

        PopulateFields(entry);
        StatusText.Text = "Connection string parsed. Review the fields and click Connect.";
        StatusText.Foreground = Brushes.Green;
    }

    private void PopulateFields(ConnectionEntry entry)
    {
        HostBox.Text = entry.Host;
        PortBox.Text = entry.Port.ToString();
        DatabaseBox.Text = entry.Database;
        UsernameBox.Text = entry.Username;
        PasswordBox.Password = entry.Password;

        // Select correct SSL mode
        var sslLower = entry.SslMode.ToLowerInvariant();
        for (int i = 0; i < SslModeBox.Items.Count; i++)
        {
            if (SslModeBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), entry.SslMode, StringComparison.OrdinalIgnoreCase))
            {
                SslModeBox.SelectedIndex = i;
                break;
            }
        }
    }

    private ConnectionEntry BuildEntryFromFields()
    {
        var sslMode = (SslModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Prefer";
        return new ConnectionEntry
        {
            Host = HostBox.Text.Trim(),
            Port = int.TryParse(PortBox.Text, out var p) ? p : 5432,
            Database = DatabaseBox.Text.Trim(),
            Username = UsernameBox.Text.Trim(),
            Password = PasswordBox.Password,
            SslMode = sslMode
        };
    }

    private void ConnectWithEntry(ConnectionEntry entry)
    {
        ConnectButton.IsEnabled = false;
        StatusText.Text = "Connecting...";
        StatusText.Foreground = Brushes.Gray;

        var connectionString = entry.ToConnectionString();
        var savedEntry = entry.Clone();

        _ = Task.Run(() =>
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT 1", conn);
            cmd.ExecuteScalar();
        }).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var msg = t.Exception?.InnerException?.Message ?? t.Exception?.Message ?? "Unknown error";
                StatusText.Text = $"Connection failed: {msg}";
                StatusText.Foreground = Brushes.Red;
                ConnectButton.IsEnabled = true;
            }
            else
            {
                // Save to recent connections
                ConnectionStore.AddOrUpdate(savedEntry);

                ResultConnectionString = connectionString;
                ResultDatabaseName = savedEntry.Database;
                DialogResult = true;
                Close();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
