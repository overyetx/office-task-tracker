using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace OfficeTaskTracker.Services;

public static class ScreenshotService
{
    // ─── Win32 Imports ───────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// Finds the RAGE Multiplayer (GTA5) window by process name,
    /// then uses EnumWindows to get the actual HWND matching that PID.
    /// Same approach as the Go robotgo+EnumWindows method.
    /// </summary>
    public static IntPtr FindGameWindow()
    {
        // Step 1: Find GTA5 process ID
        uint targetPid = 0;
        var processes = Process.GetProcessesByName("GTA5");
        if (processes.Length == 0)
        {
            // Try alternative process names
            processes = Process.GetProcessesByName("GTAV");
            if (processes.Length == 0)
            {
                processes = Process.GetProcessesByName("ragemp_v");
                if (processes.Length == 0)
                {
                    Debug.WriteLine("GTA5/RAGE process not found");
                    return IntPtr.Zero;
                }
            }
        }

        targetPid = (uint)processes[0].Id;
        Debug.WriteLine($"Found GTA5 PID: {targetPid}");

        // Step 2: EnumWindows to find the HWND matching this PID
        IntPtr resultHwnd = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);

            if (IsWindowVisible(hWnd))
            {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();

                Debug.WriteLine($"MY_PID: {targetPid}, Window: 0x{hWnd:X}, PID: {pid}, Title: {title}");

                if (pid == targetPid)
                {
                    resultHwnd = hWnd;
                    Debug.WriteLine($"Found HWND: 0x{hWnd:X}");
                    return false; // Stop enumeration
                }
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        Debug.WriteLine($"Result HWND: 0x{resultHwnd:X}");
        return resultHwnd;
    }

    /// <summary>
    /// Finds the game window and forcefully brings it to foreground.
    /// </summary>
    public static IntPtr FocusGameWindow()
    {
        var hWnd = FindGameWindow();
        if (hWnd == IntPtr.Zero) return IntPtr.Zero;

        ForceSetForegroundWindow(hWnd);
        return hWnd;
    }

    /// <summary>
    /// Forces a window to the foreground using the AttachThreadInput trick.
    /// Bypasses Windows' restriction that only the foreground process
    /// can call SetForegroundWindow.
    /// </summary>
    private static void ForceSetForegroundWindow(IntPtr hWnd)
    {
        if (!IsWindow(hWnd)) return;

        // If minimized, restore first
        if (IsIconic(hWnd))
            ShowWindow(hWnd, SW_RESTORE);

        // Get thread IDs
        var currentThreadId = GetCurrentThreadId();
        var foregroundHwnd = GetForegroundWindow();
        var foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
        var targetThreadId = GetWindowThreadProcessId(hWnd, out _);

        // Simulate pressing Alt key to allow SetForegroundWindow from background
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);

        // Attach our thread to the foreground thread
        if (currentThreadId != foregroundThreadId)
            AttachThreadInput(currentThreadId, foregroundThreadId, true);

        // Attach our thread to the target thread
        if (currentThreadId != targetThreadId)
            AttachThreadInput(currentThreadId, targetThreadId, true);

        // Now we can set foreground
        SetForegroundWindow(hWnd);
        BringWindowToTop(hWnd);
        ShowWindow(hWnd, SW_SHOW);

        // Release Alt key
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        // Detach threads
        if (currentThreadId != foregroundThreadId)
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        if (currentThreadId != targetThreadId)
            AttachThreadInput(currentThreadId, targetThreadId, false);
    }

    /// <summary>
    /// Captures the entire primary screen.
    /// </summary>
    public static Bitmap CaptureFullScreen()
    {
        int screenWidth = (int)SystemParameters.VirtualScreenWidth;
        int screenHeight = (int)SystemParameters.VirtualScreenHeight;
        int screenLeft = (int)SystemParameters.VirtualScreenLeft;
        int screenTop = (int)SystemParameters.VirtualScreenTop;

        var bitmap = new Bitmap(screenWidth, screenHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(screenLeft, screenTop, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
        }
        return bitmap;
    }

    public static Bitmap CropBitmap(Bitmap source, System.Drawing.Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return source;

        var cropped = new Bitmap(rect.Width, rect.Height);
        using (var graphics = Graphics.FromImage(cropped))
        {
            graphics.DrawImage(source,
                new System.Drawing.Rectangle(0, 0, rect.Width, rect.Height),
                rect,
                GraphicsUnit.Pixel);
        }
        return cropped;
    }

    public static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }
}
