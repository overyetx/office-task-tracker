using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OfficeTaskTracker.Helpers
{
    public static class ThemeHelper
    {
        [DllImport("DwmApi")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

        public static void ApplyDarkMode(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int[] trueValue = { 1 };
            
            // DWMWA_USE_IMMERSIVE_DARK_MODE builds > 18985 (Windows 11, recent Win 10)
            DwmSetWindowAttribute(hwnd, 20, trueValue, 4);

            // DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 (older Win 10 1809 - 1909)
            DwmSetWindowAttribute(hwnd, 19, trueValue, 4);
        }
    }
}
