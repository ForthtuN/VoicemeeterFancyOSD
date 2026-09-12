using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace AtgDev.Utils;

public class AutostartManager
{
    private string m_shortcutPath;
    private bool m_isEnabled = false;

    public string ProgramName { get; init; }
    public string ProgramPath { get; init; }

    public string IconLocation { get; set; }

    public bool IsEnabled 
    { 
        get => m_isEnabled;
        set
        {
            if (m_isEnabled == value) return;

            TryToggle(value);
        }
    }

    static public bool IsOsSupported => OperatingSystem.IsWindows();

    public bool TryEnable() => TryToggle(true);

    public bool TryDisable() => TryToggle(false);

    public bool TryToggle(bool isEnabled)
    {
        try
        {
            Toggle(isEnabled);
            return true;
        }
        catch { }
        return false;
    }

    public void Enable() => Toggle(true);

    public void Disable() => Toggle(false);

    public void Toggle(bool isEnabled)
    {
        if (OperatingSystem.IsWindows())
        {
            WinToggle(isEnabled);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
        m_isEnabled = isEnabled;
    }

    private void WinToggle(bool isEnabled)
    {
        if (isEnabled)
        {
            WinEnable();
        }
        else
        {
            WinDisable();
        }
    }

    private void WinEnable()
    {
        const string AppdataVarName = "APPDATA";
        const string StartupPathTail = @"Microsoft\Windows\Start Menu\Programs\Startup";
        string AppdataRoam = Environment.GetEnvironmentVariable(AppdataVarName);
        if (string.IsNullOrEmpty(AppdataRoam))
        {
            throw new Exception($"{AppdataVarName} environment variable not found");
        }

        string startupPath = Path.Combine(AppdataRoam, StartupPathTail);
        if (!Directory.Exists(startupPath))
        {
            throw new DirectoryNotFoundException($"Startup folder not found: {startupPath}");
        }

        if (string.IsNullOrEmpty(ProgramName))
        {
            throw new ArgumentException($"{nameof(ProgramName)} need to be defined");
        }
        if (string.IsNullOrEmpty(ProgramPath))
        {
            throw new ArgumentException($"{nameof(ProgramPath)} need to be defined");
        }

        string shortcutPath = Path.Combine(startupPath, ProgramName + ".lnk");

        WindowsShortcut.Create(
            shortcutPath,
            ProgramPath,
            Path.GetDirectoryName(ProgramPath),
            string.IsNullOrEmpty(IconLocation) ? ProgramPath : IconLocation);

        m_shortcutPath = shortcutPath;
    }

    private void WinDisable()
    {
        if (string.IsNullOrEmpty(m_shortcutPath)) return;

        System.IO.File.Delete(m_shortcutPath);
        m_shortcutPath = null;
    }
}
internal static class WindowsShortcut
{
    internal static void Create(string shortcutPath, string targetPath, string workingDirectory, string iconLocation)
    {
        object shellLinkObject = new ShellLink();
        try
        {
            IShellLinkW shellLink = (IShellLinkW)shellLinkObject;
            shellLink.SetPath(targetPath);
            shellLink.SetWorkingDirectory(workingDirectory ?? string.Empty);
            shellLink.SetIconLocation(iconLocation, 0);

            ((IPersistFile)shellLinkObject).Save(shortcutPath, true);
        }
        finally
        {
            if (Marshal.IsComObject(shellLinkObject))
            {
                Marshal.FinalReleaseComObject(shellLinkObject);
            }
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
