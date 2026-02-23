using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OfficeTaskTracker.Models;

public class TaskSession : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N")[..8].ToUpper();
    private DateTime _date = DateTime.Now;
    private List<TaskItem> _tasks = new();
    private bool _isBonusPaid;
    private string _contractName = string.Empty;
    private string _officeName = string.Empty;
    private bool _isSelected;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public DateTime Date
    {
        get => _date;
        set { _date = value; OnPropertyChanged(); }
    }

    public string ContractName
    {
        get => _contractName;
        set { _contractName = value; OnPropertyChanged(); }
    }

    public string OfficeName
    {
        get => _officeName;
        set { _officeName = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public List<TaskItem> Tasks
    {
        get => _tasks;
        set { _tasks = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalTasks)); OnPropertyChanged(nameof(CompletedTasks)); }
    }

    /// <summary>
    /// Whether the bonus for this session has been paid (tracked separately from task earnings)
    /// </summary>
    public bool IsBonusPaid
    {
        get => _isBonusPaid;
        set { _isBonusPaid = value; OnPropertyChanged(); }
    }

    // Keep JSON compat with old data
    public bool IsPaid
    {
        get => _isBonusPaid;
        set { _isBonusPaid = value; }
    }

    public int TotalTasks => Tasks.Count;
    public int CompletedTasks => Tasks.Count(t => t.IsCompleted);
    public string TaskCountText => $"{CompletedTasks}/{TotalTasks}";

    /// <summary>Task earnings = sum of completed task prices</summary>
    public int TaskEarnings => Tasks.Where(t => t.IsCompleted).Sum(t => t.Price);
    public string TaskEarningsText => $"${TaskEarnings:N0}";

    /// <summary>Bonus amount = Fixed $10,000 per completed task</summary>
    public int BonusAmount => CompletedTasks * 10000;
    public string BonusAmountText => $"${BonusAmount:N0}";

    /// <summary>Total potential = task earnings + bonus</summary>
    public int TotalAmount => TaskEarnings + BonusAmount;
    public string AmountText => $"${TaskEarnings:N0}";

    public string BonusStatusText => IsBonusPaid ? "Paid" : "Unpaid";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
