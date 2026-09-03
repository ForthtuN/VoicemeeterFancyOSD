using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using VoicemeeterOsdProgram.Helpers;

namespace TopmostApp.Interop;

public partial class BandWindow
{
    private CancellationTokenSource m_diagnosticSnapshotCts;
    private bool m_diagnosticHookAdded;

    private void InitDiagnostics()
    {
        Loaded += (_, _) =>
        {
            TryAddDiagnosticHook();
            OsdDiagnostics.WindowSnapshot("WINDOW_LOADED", Handle, hwndSource?.Handle ?? IntPtr.Zero);
        };

        Shown += (_, _) => CaptureShownDiagnostics();
        DpiChanged += (_, _) => OsdDiagnostics.WindowSnapshot("DPI_CHANGED", Handle, hwndSource?.Handle ?? IntPtr.Zero);
    }

    private void TryAddDiagnosticHook()
    {
        if (m_diagnosticHookAdded || hwndSource is null) return;

        hwndSource.AddHook(DiagnosticHwndSourceHook);
        m_diagnosticHookAdded = true;
        OsdDiagnostics.Write("HOOK", $"HwndSource hook installed source=0x{hwndSource.Handle.ToInt64():X}");
    }

    private void CaptureShownDiagnostics()
    {
        TryAddDiagnosticHook();

        var outer = Handle;
        var source = hwndSource?.Handle ?? IntPtr.Zero;
        OsdDiagnostics.WindowSnapshot("SHOWN_NOW", outer, source);

        m_diagnosticSnapshotCts?.Cancel();
        m_diagnosticSnapshotCts?.Dispose();
        var cts = new CancellationTokenSource();
        m_diagnosticSnapshotCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100, cts.Token);
                OsdDiagnostics.WindowSnapshot("SHOWN_100MS", outer, source);
                await Task.Delay(900, cts.Token);
                OsdDiagnostics.WindowSnapshot("SHOWN_1000MS", outer, source);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private IntPtr DiagnosticHwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case 0x0018: // WM_SHOWWINDOW
                OsdDiagnostics.WindowSnapshot($"SRC_WM_SHOWWINDOW wParam={wParam.ToInt64()}", Handle, hwnd);
                break;
            case 0x0047: // WM_WINDOWPOSCHANGED
                OsdDiagnostics.WindowSnapshot("SRC_WM_WINDOWPOSCHANGED", Handle, hwnd);
                break;
            case 0x007D: // WM_STYLECHANGED
                OsdDiagnostics.WindowSnapshot("SRC_WM_STYLECHANGED", Handle, hwnd);
                break;
            case 0x007E: // WM_DISPLAYCHANGE
                OsdDiagnostics.WindowSnapshot("SRC_WM_DISPLAYCHANGE", Handle, hwnd);
                break;
            case 0x02E0: // WM_DPICHANGED
                OsdDiagnostics.WindowSnapshot("SRC_WM_DPICHANGED", Handle, hwnd);
                break;
            case 0x031E: // WM_DWMCOMPOSITIONCHANGED
                OsdDiagnostics.WindowSnapshot("SRC_WM_DWMCOMPOSITIONCHANGED", Handle, hwnd);
                break;
        }

        return IntPtr.Zero;
    }
}
