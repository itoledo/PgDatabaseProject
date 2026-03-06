using System.ComponentModel;

namespace PgDatabaseProject.Extension.Dialogs;

internal sealed class SelectableItem : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string Name { get; }
    public string Value { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public SelectableItem(string name, string? value = null)
    {
        Name = name;
        Value = value ?? name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
