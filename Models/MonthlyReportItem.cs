using System;

namespace OfficeTaskTracker.Models;

public class MonthlyReportItem
{
    public string MonthName { get; set; } = string.Empty;
    public DateTime SortDate { get; set; } 
    public int SessionCount { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int TaskEarnings { get; set; }
    public int BonusEarnings { get; set; }

    public int TotalEarnings => TaskEarnings + BonusEarnings;

    public string TaskCountText => $"{CompletedTasks}/{TotalTasks}";
    public string TaskEarningsText => $"${TaskEarnings:N0}";
    public string BonusEarningsText => $"${BonusEarnings:N0}";
    public string TotalEarningsText => $"${TotalEarnings:N0}";
}
