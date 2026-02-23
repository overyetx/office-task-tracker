using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OfficeTaskTracker.Models;
using OfficeTaskTracker.Services;
using OfficeTaskTracker.Helpers;
using OfficeTaskTracker.ViewModels;
using System.Windows.Media;
using System.Reflection;

namespace OfficeTaskTracker;

public partial class MainWindow : Window
{
    private List<TaskSession> _allSessions = new();
    private ObservableCollection<TaskSession> _displayedSessions = new();
    private bool _isInitialized;

    public MainWindow()
    {
        InitializeComponent();
        InitializeAppMetadata();
        LoadData();
        InitializeFilterControls();
        UpdateStats();
    }

    private void InitializeAppMetadata()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Office Task Tracker";
        var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        this.Title = product;
        if (AppTitleText1 != null) AppTitleText1.Text = product;
        if (AppTitleText2 != null) AppTitleText2.Text = product;
        if (VersionText != null) VersionText.Text = $"{product} v{version}";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeHelper.ApplyDarkMode(this);
    }

    private void LoadData()
    {
        _allSessions = DataService.LoadSessions();
    }

    private void InitializeFilterControls()
    {
        _isInitialized = false;

        YearComboBox.Items.Clear();
        MonthComboBox.Items.Clear();
        DayComboBox.Items.Clear();

        // Years
        var years = _allSessions.Select(s => s.Date.Year.ToString()).Distinct().OrderByDescending(y => y).ToList();
        if (!years.Contains(DateTime.Now.Year.ToString())) years.Insert(0, DateTime.Now.Year.ToString());
        YearComboBox.Items.Add("All Years");
        foreach (var y in years) YearComboBox.Items.Add(y);

        // Months
        MonthComboBox.Items.Add("All Months");
        for (int i = 1; i <= 12; i++)
        {
            MonthComboBox.Items.Add(System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(i));
        }

        // Days
        DayComboBox.Items.Add("All Days");
        for (int i = 1; i <= 31; i++)
        {
            DayComboBox.Items.Add(i.ToString());
        }

        // Defaults: Current Year, Current Month, All Days (hierarchical)
        YearComboBox.SelectedItem = DateTime.Now.Year.ToString();
        MonthComboBox.SelectedIndex = DateTime.Now.Month;
        DayComboBox.SelectedIndex = 0;

        _isInitialized = true;
        ApplyFilter();
    }

    private void UpdateStats()
    {
        if (!_isInitialized || TotalSessionsText == null) return;

        var totalTasks = _allSessions.Sum(s => s.TotalTasks);
        var completedTasks = _allSessions.Sum(s => s.CompletedTasks);
        var taskEarnings = _allSessions.Sum(s => s.TaskEarnings);
        var unpaidBonus = _allSessions.Where(s => !s.IsBonusPaid).ToList();
        var paidBonus = _allSessions.Where(s => s.IsBonusPaid).ToList();
        var unpaidBonusAmount = unpaidBonus.Sum(s => s.BonusAmount);
        var paidBonusAmount = paidBonus.Sum(s => s.BonusAmount);

        TotalEarningsText.Text = $"${taskEarnings:N0}";
        TotalSessionsText.Text = $"{_allSessions.Count} contracts";

        UnpaidAmountText.Text = $"${unpaidBonusAmount:N0}";
        UnpaidText.Text = $"{unpaidBonus.Count} contracts";

        PaidAmountText.Text = $"${paidBonusAmount:N0}";
        PaidText.Text = $"{paidBonus.Count} contracts";

        TotalTasksText.Text = completedTasks.ToString();

        CompletedTasksText.Text = totalTasks.ToString();
        var percent = totalTasks > 0 ? (int)((double)completedTasks / totalTasks * 100) : 0;
        CompletedPercentText.Text = $"{percent}%";

        StatusText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
    }

    private void ApplyFilter()
    {
        if (!_isInitialized || SessionsGrid == null) return;

        var filtered = _allSessions.AsEnumerable();

        // Apply payment filter
        if (FilterUnpaid?.IsChecked == true)
            filtered = filtered.Where(s => !s.IsBonusPaid);
        else if (FilterPaid?.IsChecked == true)
            filtered = filtered.Where(s => s.IsBonusPaid);

        // Apply hierarchical date filter
        if (YearComboBox != null && YearComboBox.SelectedIndex > 0)
        {
            if (int.TryParse(YearComboBox.SelectedItem.ToString(), out int year))
            {
                int month = MonthComboBox.SelectedIndex; // 0 = All, 1 = Jan
                int day = DayComboBox.SelectedIndex;     // 0 = All, 1 = 1st

                if (day > 0 && month > 0)
                {
                    filtered = filtered.Where(s => s.Date.Year == year && s.Date.Month == month && s.Date.Day == day);
                }
                else if (month > 0)
                {
                    filtered = filtered.Where(s => s.Date.Year == year && s.Date.Month == month);
                }
                else
                {
                    filtered = filtered.Where(s => s.Date.Year == year);
                }
            }
        }

        // Sort by date descending
        filtered = filtered.OrderByDescending(s => s.Date);

        _displayedSessions = new ObservableCollection<TaskSession>(filtered);
        SessionsGrid.ItemsSource = _displayedSessions;
    }

    private async void TakeScreenshot_Click(object sender, RoutedEventArgs e)
    {
        var previousState = WindowState;
        Bitmap? screenshot = null;

        try
        {
            ScreenshotBtn.IsEnabled = false;

            // Minimize our window first
            WindowState = WindowState.Minimized;
            await Task.Delay(300);

            // Find GTA5/RAGE process and focus its window (like Go's robotgo+EnumWindows)
            var gameHwnd = ScreenshotService.FocusGameWindow();

            if (gameHwnd == IntPtr.Zero)
            {
                // Game not found
                CustomMessageBox.Show("GTA5 / RAGE Multiplayer process not found.\nMake sure the game is running.\n\nFull screenshot will be taken anyway.",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Wait for window to fully come to foreground
            await Task.Delay(800);

            // Capture full screen
            screenshot = ScreenshotService.CaptureFullScreen();

            // Restore our window
            WindowState = previousState;
            Activate();

            // OCR — extract tasks with prices
            var ocrResult = await OcrService.ExtractTasksFromBitmap(screenshot);

            if (ocrResult.Tasks.Count == 0)
            {
                CustomMessageBox.Show("Could not extract tasks from screenshot.\nMake sure the RAGE Multiplayer window is open and on the task screen.", 
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create a new session
            var session = new TaskSession
            {
                Date = DateTime.Now,
                ContractName = ocrResult.ContractName,
                OfficeName = ocrResult.OfficeName
            };

            // Save screenshot
            DataService.SaveScreenshot(screenshot, session.Id);

            // Show task selection dialog
            var taskSelectionWindow = new TaskSelectionWindow(ocrResult.Tasks, session);
            taskSelectionWindow.Owner = this;

            if (taskSelectionWindow.ShowDialog() == true)
            {
                _allSessions.Add(session);
                DataService.SaveSessions(_allSessions);
                ApplyFilter();
                UpdateStats();

                // Open the session detail directly after creating
                OpenSessionModal(session);
            }
        }
        catch (Exception ex)
        {
            WindowState = previousState;
            CustomMessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            screenshot?.Dispose();
            ScreenshotBtn.IsEnabled = true;
        }
    }

    private void ManualAdd_Click(object sender, RoutedEventArgs e)
    {
        var session = new TaskSession
        {
            Date = DateTime.Now
        };

        var taskSelectionWindow = new TaskSelectionWindow(session);
        taskSelectionWindow.Owner = this;

        if (taskSelectionWindow.ShowDialog() == true)
        {
            _allSessions.Add(session);
            DataService.SaveSessions(_allSessions);
            ApplyFilter();
            UpdateStats();

OpenSessionModal(session);
        }
    }

    private void MonthlyReport_Click(object sender, RoutedEventArgs e)
    {
        if (_allSessions.Count == 0)
        {
            CustomMessageBox.Show("No sessions found to report.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        GenerateReportModal(_allSessions);
        MonthlyReportModal.Visibility = Visibility.Visible;
    }

    private void GenerateReportModal(List<TaskSession> allSessions)
    {
        var reports = allSessions
            .GroupBy(s => new { s.Date.Year, s.Date.Month })
            .Select(g => new MonthlyReportItem
            {
                SortDate = new System.DateTime(g.Key.Year, g.Key.Month, 1),
                MonthName = $"{GetMonthName(g.Key.Month)} {g.Key.Year}",
                SessionCount = g.Count(),
                TotalTasks = g.Sum(s => s.TotalTasks),
                CompletedTasks = g.Sum(s => s.CompletedTasks),
                TaskEarnings = g.Sum(s => s.TaskEarnings),
                BonusEarnings = g.Where(s => s.IsBonusPaid).Sum(s => s.BonusAmount) 
            })
            .OrderByDescending(r => r.SortDate)
            .ToList();

        ReportGrid.ItemsSource = reports;
    }

    private string GetMonthName(int month)
    {
        return month switch
        {
            1 => "January", 2 => "February", 3 => "March", 4 => "April",
            5 => "May", 6 => "June", 7 => "July", 8 => "August",
            9 => "September", 10 => "October", 11 => "November", 12 => "December",
            _ => month.ToString()
        };
    }

    private void CloseReportModal_Click(object sender, RoutedEventArgs e)
    {
        MonthlyReportModal.Visibility = Visibility.Collapsed;
    }

    private void ReportModalDim_Click(object sender, MouseButtonEventArgs e)
    {
        MonthlyReportModal.Visibility = Visibility.Collapsed;
    }

    private void ExportBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = $"OfficeTaskBackup_{DateTime.Now:yyyyMMdd}.json",
                Title = "Export Backup"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_allSessions, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(saveFileDialog.FileName, json);
                CustomMessageBox.Show("Backup exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Import Backup"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var result = CustomMessageBox.Show(
                    "Importing will merge the backup with your existing contracts. Do you want to continue?",
                    "Import Backup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                    var imported = Newtonsoft.Json.JsonConvert.DeserializeObject<List<TaskSession>>(json);

                    if (imported != null && imported.Count > 0)
                    {
                        // Simple merge: add items that don't exist by ID (or just add all if preferred)
                        // For simplicity and safety, let's append all new items and regenerate IDs if necessary, 
                        // but usually these backups are full sets. Let's merge by ID.
                        var existingIds = _allSessions.Select(s => s.Id).ToHashSet();
                        int addedCount = 0;

                        foreach (var session in imported)
                        {
                            if (!existingIds.Contains(session.Id))
                            {
                                _allSessions.Add(session);
                                addedCount++;
                            }
                        }

                        if (addedCount > 0)
                        {
                            DataService.SaveSessions(_allSessions);
                            InitializeFilterControls(); // Refresh everything
                            UpdateStats();
                            CustomMessageBox.Show($"{addedCount} new contracts imported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            CustomMessageBox.Show("No new contracts found in the backup file.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SessionsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionsGrid.SelectedItem is TaskSession session)
        {
            OpenSessionModal(session);
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allSessions.Where(s => s.IsSelected).ToList();

        if (selected.Count == 0 && SessionsGrid.SelectedItem is TaskSession single)
        {
            selected.Add(single);
        }

        if (selected.Count == 0)
        {
            CustomMessageBox.Show("Please select the sessions you want to delete.", "Warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var message = selected.Count == 1 
            ? $"Are you sure you want to delete session #{selected[0].Id}?"
            : $"Are you sure you want to delete {selected.Count} sessions?";

        var result = CustomMessageBox.Show(
            message,
            "Delete Sessions",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            foreach (var s in selected) _allSessions.Remove(s);
            DataService.SaveSessions(_allSessions);
            ApplyFilter();
            UpdateStats();
        }
    }

    private void SelectAllSessions_Click(object sender, RoutedEventArgs e)
    {
        var isChecked = (sender as CheckBox)?.IsChecked ?? false;
        foreach (var session in _displayedSessions)
        {
            session.IsSelected = isChecked;
        }
    }

    private void RowDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TaskSession session)
        {
            var result = CustomMessageBox.Show(
                $"Are you sure you want to delete contract #{session.Id}?",
                "Delete Contract",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _allSessions.Remove(session);
                DataService.SaveSessions(_allSessions);
                ApplyFilter();
                UpdateStats();
            }
        }
    }

    private void RowPay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TaskSession session)
        {
            session.IsBonusPaid = !session.IsBonusPaid;
            DataService.SaveSessions(_allSessions);
            ApplyFilter();
            UpdateStats();
        }
    }

    private void MarkAllPaid_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allSessions.Where(s => s.IsSelected && !s.IsBonusPaid).ToList();
        
        // If nothing selected, fall back to all unpaid in current filter or simple logic
        if (selected.Count == 0)
        {
            selected = _allSessions.Where(s => !s.IsBonusPaid).ToList();
        }

        if (selected.Count == 0)
        {
            CustomMessageBox.Show("No unpaid bonuses found to process.", "Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var totalBonus = selected.Sum(s => s.BonusAmount);
        var result = CustomMessageBox.Show(
            $"Bonus payment will be made for {selected.Count} contracts.\nTotal bonus: ${totalBonus:N0}\n\nDo you want to mark as paid?",
            "Bulk Payment",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            foreach (var s in selected) s.IsBonusPaid = true;
            DataService.SaveSessions(_allSessions);
            ApplyFilter();
            UpdateStats();
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void RowEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TaskSession session)
        {
            var editWindow = new TaskSelectionWindow(session, true);
            editWindow.Owner = this;
            if (editWindow.ShowDialog() == true)
            {
                DataService.SaveSessions(_allSessions);
                ApplyFilter();
                UpdateStats();
                
                // If it was open in modal, refresh modal too
                if (_currentModalSession == session)
                {
                    OpenSessionModal(session);
                }
            }
        }
    }

    private void ClearDateFilter_Click(object sender, RoutedEventArgs e)
    {
        if (YearComboBox == null) return;
        _isInitialized = false;
        YearComboBox.SelectedIndex = 0;
        MonthComboBox.SelectedIndex = 0;
        DayComboBox.SelectedIndex = 0;
        _isInitialized = true;
        ApplyFilter();
    }

    #region TitleBar & Window state
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
        Close();
    }
    #endregion

    #region Modal Session Detail Handle
    private TaskSession? _currentModalSession;
    private List<TaskCardViewModel> _cardViewModels = new();

    private void OpenSessionModal(TaskSession session)
    {
        _currentModalSession = session;
        
        SessionIdText.Text = $"ID: #{session.Id}";
        SessionContractText.Text = string.IsNullOrWhiteSpace(session.ContractName) ? "CONTRACT: GENERAL" : $"CONTRACT: {session.ContractName}";
        SessionOfficeText.Text = string.IsNullOrWhiteSpace(session.OfficeName) ? "PLAYER: NOT SPECIFIED" : $"PLAYER: {session.OfficeName}";
        SessionDateText.Text = session.Date.ToString("dd MMMM yyyy, HH:mm", new System.Globalization.CultureInfo("en-US"));

        RefreshCards();
        UpdatePaymentButton();
        UpdateProgress();

        ModalDialog.Visibility = Visibility.Visible;
    }

    private void CloseModal_Click(object sender, RoutedEventArgs e)
    {
        CloseModal();
    }

    private void ModalDim_Click(object sender, MouseButtonEventArgs e)
    {
        CloseModal();
    }

    private void CloseModal()
    {
        ModalDialog.Visibility = Visibility.Collapsed;
        _currentModalSession = null;
        ApplyFilter();
        UpdateStats();
    }

    private void RefreshCards()
    {
        if (_currentModalSession == null) return;
        _cardViewModels = _currentModalSession.Tasks
            .Select((t, i) => new TaskCardViewModel(t, i + 1))
            .ToList();
        TaskCardsControl.ItemsSource = _cardViewModels;
    }

    private void UpdatePaymentButton()
    {
        if (_currentModalSession == null) return;
        if (_currentModalSession.IsBonusPaid)
        {
            PaymentToggleBtn.Content = "✓ BONUS PAID";
            PaymentToggleBtn.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D4AA"));
        }
        else
        {
            PaymentToggleBtn.Content = "BONUS UNPAID";
            PaymentToggleBtn.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6363"));
        }
    }

    private void UpdateProgress()
    {
        if (_currentModalSession == null) return;
        var completed = _currentModalSession.CompletedTasks;
        var total = _currentModalSession.TotalTasks;
        ProgressText.Text = $"{completed} / {total}";

        double percentage = total > 0 ? (double)completed / total : 0;
        
        // Use Grid Column for responsive progress bar
        ProgressColumn.Width = new GridLength(percentage, GridUnitType.Star);
        RemainingColumn.Width = new GridLength(1.0 - percentage, GridUnitType.Star);
    }

    private void PaymentToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_currentModalSession == null) return;
        _currentModalSession.IsBonusPaid = !_currentModalSession.IsBonusPaid;
        UpdatePaymentButton();
        AutoSave();
    }

    private void TaskCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is TaskCardViewModel vm)
        {
            vm.IsCompleted = !vm.IsCompleted;
            RefreshCards();
            UpdateProgress();
            AutoSave();
        }
    }

    private void AutoSave()
    {
        DataService.SaveSessions(_allSessions);
    }
    #endregion

}