using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace VoicemeeterOsdProgram.Helpers;

internal static class OsdDiagnostics
{
    private const uint GW_HWNDPREV = 3;
    private const uint GW_OWNER = 4;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int DWMWA_CLOAKED = 14;

    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static readonly string LogPath = Path.Combine(LogDirectory, "osd-diagnostic.log");
    private static StreamWriter Writer;

    static OsdDiagnostics()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            RotateIfTooLarge();
            var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            Writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            Write("SESSION", $"start process={Process.GetCurrentProcess().ProcessName} pid={Environment.ProcessId} os={Environment.OSVersion} framework={RuntimeInformation.FrameworkDescription} arch={RuntimeInformation.ProcessArchitecture}");
        }
        catch
        {
            Writer = null;
        }
    }

    internal static string FilePath => LogPath;

    internal static void Write(string stage, string details = "")
    {
        try
        {
            lock (Sync)
            {
                Writer?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{stage}] {details}");
            }
        }
        catch
        {
        }
    }

    internal static void WindowSnapshot(string stage, IntPtr outerHwnd, IntPtr sourceHwnd)
    {
        try
        {
            var foreground = GetForegroundWindow();
            Write(stage,
                $"outer={DescribeWindow(outerHwnd)} | source={DescribeWindow(sourceHwnd)} | foreground={DescribeIdentity(foreground)}");
        }
        catch (Exception ex)
        {
            Write(stage, $"snapshot-error={ex.GetType().Name}:{ex.Message}");
        }
    }

    private static string DescribeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "0";

        bool exists = IsWindow(hwnd);
        bool visible = exists && IsWindowVisible(hwnd);
        bool cloaked = false;
        if (exists)
        {
            int cloak = 0;
            cloaked = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, ref cloak, sizeof(int)) == 0 && cloak != 0;
        }

        GetWindowRect(hwnd, out var rect);
        var style = GetWindowLongPtrW(hwnd, GWL_STYLE).ToInt64();
        var exStyle = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
        var parent = GetParent(hwnd);
        var owner = GetWindow(hwnd, GW_OWNER);
        var above = GetWindow(hwnd, GW_HWNDPREV);

        string band = "?";
        try
        {
            if (GetWindowBand(hwnd, out uint zBand)) band = zBand.ToString();
        }
        catch
        {
        }

        return $"{DescribeIdentity(hwnd)} exists={exists} visible={visible} cloaked={cloaked} band={band} " +
               $"rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom} style=0x{style:X} ex=0x{exStyle:X} " +
               $"parent={FormatHandle(parent)} owner={FormatHandle(owner)} above={DescribeIdentity(above)}";
    }

    private static string DescribeIdentity(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "0";

        string processName = "?";
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0) processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
        }

        string title = "";
        try
        {
            int length = Math.Min(GetWindowTextLengthW(hwnd), 180);
            if (length > 0)
            {
                var sb = new StringBuilder(length + 1);
                _ = GetWindowTextW(hwnd, sb, sb.Capacity);
                title = sb.ToString().Replace('|', '/');
            }
        }
        catch
        {
        }

        return $"{FormatHandle(hwnd)}:{processName}:\"{title}\"";
    }

    private static string FormatHandle(IntPtr hwnd) => hwnd == IntPtr.Zero ? "0" : $"0x{hwnd.ToInt64():X}";

    private static void RotateIfTooLarge()
    {
        try
        {
            var info = new FileInfo(LogPath);
            if (!info.Exists || info.Length < 25 * 1024 * 1024) return;

            var oldPath = LogPath + ".old";
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(LogPath, oldPath);
        }
        catch
        {
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowBand(IntPtr hWnd, out uint band);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
