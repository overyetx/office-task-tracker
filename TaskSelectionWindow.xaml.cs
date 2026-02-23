using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using OfficeTaskTracker.Models;
using OfficeTaskTracker.Services;
using OfficeTaskTracker.Helpers;
using System;

namespace OfficeTaskTracker;

public class SelectableTask : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private bool _isSelected = true;
    private int _price;

    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

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

public partial class TaskSelectionWindow : Window
{
    private readonly ObservableCollection<SelectableTask> _items = new();
    private readonly TaskSession _session;

    public TaskSelectionWindow(List<OcrTaskResult> ocrTasks, TaskSession session)
    {
        InitializeComponent();
        _session = session;

        foreach (var task in ocrTasks)
        {
            _items.Add(new SelectableTask 
            { 
                Text = task.Name, 
                IsSelected = true,
                Price = task.Price
            });
        }

        ContractInput.Text = session.ContractName;
        OfficeInput.Text = session.OfficeName;

        int firstPrice = ocrTasks.FirstOrDefault(t => t.Price > 0)?.Price ?? 12000;
        GlobalPriceTextBox.Text = firstPrice.ToString();

        TasksListView.ItemsSource = _items;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeHelper.ApplyDarkMode(this);
    }

    // Constructor for editing an existing session
    public TaskSelectionWindow(TaskSession session, bool isEditMode)
    {
        InitializeComponent();
        _session = session;

        foreach (var task in session.Tasks)
        {
            _items.Add(new SelectableTask 
            { 
                Text = task.Name, 
                IsSelected = true,
                Price = task.Price
            });
        }

        ContractInput.Text = session.ContractName;
        OfficeInput.Text = session.OfficeName;

        if (session.Tasks.Count > 0)
        {
            int firstPrice = session.Tasks.First().Price;
            GlobalPriceTextBox.Text = firstPrice.ToString();
        }

        SubtitleText.Text = "Edit contract and task details";
        TasksListView.ItemsSource = _items;
    }

    // Overload for manual mode (no OCR results)
    public TaskSelectionWindow(TaskSession session) : this(new List<OcrTaskResult>(), session)
    {
        SubtitleText.Text = "Enter task names manually";
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        var text = NewTaskTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            _items.Add(new SelectableTask { Text = text, IsSelected = true, Price = 0 });
            NewTaskTextBox.Text = string.Empty;
            NewTaskTextBox.Focus();
        }
    }

    private void NewTaskTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddTask_Click(sender, e);
        }
    }

    private void RemoveTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is SelectableTask item)
        {
            _items.Remove(item);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
            item.IsSelected = true;
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
            item.IsSelected = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = _items.Where(i => i.IsSelected && !string.IsNullOrWhiteSpace(i.Text)).ToList();

        if (selectedItems.Count == 0)
        {
            CustomMessageBox.Show("You must select at least one task.", "Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int globalPrice = 0;
        if (!string.IsNullOrWhiteSpace(GlobalPriceTextBox.Text) && 
            int.TryParse(GlobalPriceTextBox.Text.Replace(",", "").Replace(".", "").Trim(), out int parsedPrice) &&
            parsedPrice > 0)
        {
            globalPrice = parsedPrice;
        }

        // Try to preserve completion status if name matches
        var statusMap = _session.Tasks.ToDictionary(t => t.Name, t => t.IsCompleted);

        _session.Tasks = selectedItems.Select(i => new TaskItem
        {
            Name = i.Text.Trim(),
            IsCompleted = statusMap.TryGetValue(i.Text.Trim(), out bool wasCompleted) ? wasCompleted : false,
            Price = globalPrice > 0 ? globalPrice : i.Price
        }).ToList();

        _session.ContractName = ContractInput.Text.Trim();
        _session.OfficeName = OfficeInput.Text.Trim();

        DialogResult = true;
        Close();
    }

    #region Custom TitleBar
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    #endregion

}