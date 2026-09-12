using System;
using System.Collections.Generic;

namespace TopmostApp.Interop;

internal interface IWndProcObject
{
}

public class WndProcHookManager
{
    private Dictionary<uint, WndProc> hooks = new();

    private List<IWndProcHookHandler> hookHandlers = new();

    private static Dictionary<IWndProcObject, WndProcHookManager> hookManagers = new();

    internal static WndProcHookManager RegisterForIWndProcObject(IWndProcObject wndProcObject)
    {
        if (wndProcObject == null)
            throw new ArgumentNullException(nameof(wndProcObject));

        WndProcHookManager hookManager = new();

        hookManagers.Add(wndProcObject, hookManager);

        return hookManager;
    }

    internal static void EnsureRegisteredForIWndProcObject(IWndProcObject wndProcObject, WndProcHookManager hookManager)
    {
        if (wndProcObject == null)
            throw new ArgumentNullException(nameof(wndProcObject));
        if (hookManager == null)
            throw new ArgumentNullException(nameof(hookManager));

        hookManagers[wndProcObject] = hookManager;
    }

    internal static WndProcHookManager GetForIWndProcObject(IWndProcObject wndProcObject)
    {
        if (wndProcObject == null)
            throw new ArgumentNullException(nameof(wndProcObject));

        if (hookManagers.TryGetValue(wndProcObject, out var hookManager))
        {
            return hookManager;
        }

        return null;
    }

    internal static void UnregisterForIWndProcObject(IWndProcObject wndProcObject)
    {
        if (wndProcObject == null) return;
        hookManagers.Remove(wndProcObject);
    }

    public static WndProcHookManager GetForBandWindow(BandWindow bandWindow)
    {
        return GetForIWndProcObject(bandWindow);
    }

    public void RegisterHookHandler(IWndProcHookHandler hookHandler)
    {
        if (hookHandler == null)
            throw new ArgumentNullException(nameof(hookHandler));

        hookHandlers.Add(hookHandler);
    }

    public void RegisterHookHandlerForMessage(uint msg, IWndProcHookHandler hookHandler)
    {
        if (hookHandler == null)
            throw new ArgumentNullException(nameof(hookHandler));

        hooks.Add(msg, hookHandler.OnWndProc);
    }

    public void RegisterCallbackForMessage(uint msg, WndProc callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        hooks.Add(msg, callback);
    }

    internal void OnHwndCreated(IntPtr hWnd)
    {
        hooks.Clear();
        foreach (var hookHandler in hookHandlers)
        {
            uint msg = hookHandler.OnHwndCreated(hWnd, out bool register);
            if (register)
            {
                RegisterHookHandlerForMessage(msg, hookHandler);
            }
        }
    }

    internal void OnHwndDestroyed()
    {
        hooks.Clear();
    }

    internal IntPtr TryHandleWindowMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = false;

        if (hooks.TryGetValue(msg, out var hook))
        {
            handled = true;
            return hook(hWnd, msg, wParam, lParam);
        }

        return IntPtr.Zero;
    }
}

public interface IWndProcHookHandler
{
    public uint OnHwndCreated(IntPtr hWnd, out bool register);

    public IntPtr OnWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
