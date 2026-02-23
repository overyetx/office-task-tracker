using System.IO;
using Newtonsoft.Json;
using OfficeTaskTracker.Models;

namespace OfficeTaskTracker.Services;

public static class DataService
{
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OfficeTaskTracker");

    private static readonly string DataFile = Path.Combine(DataDirectory, "sessions.json");
    private static readonly string ScreenshotsDirectory = Path.Combine(DataDirectory, "screenshots");

    static DataService()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ScreenshotsDirectory);
    }

    public static List<TaskSession> LoadSessions()
    {
        if (!File.Exists(DataFile))
            return new List<TaskSession>();

        try
        {
            var json = File.ReadAllText(DataFile);
            return JsonConvert.DeserializeObject<List<TaskSession>>(json) ?? new List<TaskSession>();
        }
        catch
        {
            return new List<TaskSession>();
        }
    }

    public static void SaveSessions(List<TaskSession> sessions)
    {
        var json = JsonConvert.SerializeObject(sessions, Formatting.Indented);
        File.WriteAllText(DataFile, json);
    }

    public static string SaveScreenshot(System.Drawing.Bitmap bitmap, string sessionId)
    {
        var fileName = $"screenshot_{sessionId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var filePath = Path.Combine(ScreenshotsDirectory, fileName);
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        return filePath;
    }

    public static string GetScreenshotsDirectory() => ScreenshotsDirectory;
}
