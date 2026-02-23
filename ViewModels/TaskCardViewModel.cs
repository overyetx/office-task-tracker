using System.ComponentModel;
using System.Runtime.CompilerServices;
using OfficeTaskTracker.Models;

namespace OfficeTaskTracker.ViewModels;

public class TaskCardViewModel : INotifyPropertyChanged
{
    private readonly TaskItem _task;

    public int Index { get; }
    public string Label => $"TASK {Index}";
    public string Name => _task.Name;
    public string PriceText => _task.PriceText;

    public bool IsCompleted
    {
        get => _task.IsCompleted;
        set
        {
            if (_task.IsCompleted != value)
            {
                _task.IsCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public TaskItem Task => _task;

    public TaskCardViewModel(TaskItem task, int index)
    {
        _task = task;
        Index = index;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
