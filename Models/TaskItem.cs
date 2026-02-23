using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OfficeTaskTracker.Models;

public class TaskItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isCompleted;
    private int _price;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set { _isCompleted = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Task price detected from OCR (e.g. 10000 for $10,000)
    /// </summary>
    public int Price
    {
        get => _price;
        set { _price = value; OnPropertyChanged(); OnPropertyChanged(nameof(PriceText)); }
    }

    public string PriceText => Price > 0 ? $"${Price:N0}" : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
