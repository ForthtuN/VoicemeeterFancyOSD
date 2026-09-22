using AtgDev.Utils;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;
using TopmostApp.Interop;
using VoicemeeterOsdProgram.Helpers;
using VoicemeeterOsdProgram.Options;
using VoicemeeterOsdProgram.Types;
using WpfScreenHelper;

namespace VoicemeeterOsdProgram.UiControls.OSD;

public class OsdWindow : BandWindow
{
    public Logger Logger;

    private const int FadeInTimeMs = 140;
    private const int FadeOutTimeMs = 200;

    private Rect m_workingArea;
    private readonly DoubleAnimation m_fadeInAnim;
    private readonly DoubleAnimation m_fadeOutAnim;
    private bool m_isFadeOutRunning;

    public OsdWindow() : base()
    {
        m_fadeInAnim = new DoubleAnimation()
        {
            From = 0.0,
            To = 1.0,
            EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeInTimeMs)),
            FillBehavior = FillBehavior.Stop
        };

        var fadeOutAnim = new DoubleAnimation()
        {
            From = 1.0,
            To = 0.0,
            EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseIn },
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeOutTimeMs)),
            FillBehavior = FillBehavior.Stop
        };
        fadeOutAnim.Completed += OnFadeOutComplete;
        m_fadeOutAnim = fadeOutAnim;

        Loaded += (_, _) => UpdatePos();
        SizeChanged += (_, _) => UpdatePosAlign();
        Globals.Osd.screenProvider.MainScreenChanged += OnEventUpdatePos;
        InitAlignEvents();

        // triggered if any setting is changed including taskbar resize, display resolution
        SystemEvents.UserPreferenceChanged += OnUserPrefChanged;
        SystemEvents.DisplaySettingsChanged += OnDispSettChanged;

        Unloaded += OsdWindow_Unloaded;
    }

    private void InitAlignEvents()
    {
        var osdMainOpts = OptionsStorage.Osd;
        var osdAltOpts = OptionsStorage.AltOsdOptionsFullscreenApps;
        osdMainOpts.HorizontalAlignmentChanged += OnEventUpdatePosAlign;
        osdMainOpts.VerticalAlignmentChanged += OnEventUpdatePosAlign;
        osdAltOpts.HorizontalAlignmentChanged += OnEventUpdatePosAlign;
        osdAltOpts.VerticalAlignmentChanged += OnEventUpdatePosAlign;
    }

    public VertAlignment WorkingAreaVertAlignment => Globals.Osd.fullscreenAppsWatcher.IsDetected ?
        OptionsStorage.AltOsdOptionsFullscreenApps.VerticalAlignment :
        OptionsStorage.Osd.VerticalAlignment;

    public HorAlignment WorkingAreaHorAlignment => Globals.Osd.fullscreenAppsWatcher.IsDetected ?
        OptionsStorage.AltOsdOptionsFullscreenApps.HorizontalAlignment :
        OptionsStorage.Osd.HorizontalAlignment;

    public void HideAnimated(uint duration = FadeOutTimeMs)
    {
        if (duration > 0)
        {
            m_fadeOutAnim.From = Opacity;
            m_fadeOutAnim.Duration = new Duration(TimeSpan.FromMilliseconds(duration));
            m_isFadeOutRunning = true;
            BeginAnimation(OpacityProperty, m_fadeOutAnim);
        }
        else
        {
            Hide();
        }
    }

    public Screen OnWhatDisplay() => Screen.FromPoint(new Point(Left, Top));

    public new void Show()
    {
        bool wasVisible = Visibility == Visibility.Visible;
        if (!wasVisible || m_isFadeOutRunning)
        {
            CancelOpacityAnimation();
        }
        base.Show();

        if (!wasVisible)
        {
            BeginAnimation(OpacityProperty, m_fadeInAnim);
        }
    }

    private void UpdateWorkingArea()
    {
        m_workingArea = Globals.Osd.workingAreaProvider.GetWokringArea();

        var dpi = DpiHelper.GetDpiFromPoint(new Point(m_workingArea.X, m_workingArea.Y));
        m_workingArea.Width /= dpi.DpiScaleX;
        m_workingArea.Height /= dpi.DpiScaleY;
        UpdateContMaxSize();
    }

    private void UpdatePosAlign()
    {
        var area = m_workingArea;
        var h = ActualHeight;
        var w = ActualWidth;
        if ((area.Height == 0) || (area.Width == 0) || (h == 0) || (w == 0)) 
            return;

        var dpi = DpiHelper.GetDpiFromPoint(new Point(area.X, area.Y));
        var scaleX = 1 / dpi.DpiScaleX;
        var scaleY = 1 / dpi.DpiScaleY;

        _ = WorkingAreaHorAlignment switch
        {
            HorAlignment.Left => Left = area.X,
            HorAlignment.Center => Left = area.X + (area.Width - w) / 2 / scaleX,
            HorAlignment.Right => Left = area.X + (area.Width - w) / scaleX,
            _ => 0
        };
        _ = WorkingAreaVertAlignment switch
        {
            VertAlignment.Top => Top = area.Y,
            VertAlignment.Center => Top = area.Y + (area.Height - h) / 2 / scaleY,
            VertAlignment.Bottom => Top = area.Y + (area.Height - h) / scaleY,
            _ => 0
        };
    }

    private void UpdateContMaxSize()
    {
        if (Content is not OsdControl cont) return;

        cont.MaxW = m_workingArea.Width;
        cont.MaxH = m_workingArea.Height;
    }

    private void UpdatePos()
    {
        UpdateWorkingArea();
        UpdatePosAlign();
    }

    private void CancelOpacityAnimation()
    {
        m_fadeOutAnim.Completed -= OnFadeOutComplete;
        BeginAnimation(OpacityProperty, null);
        m_isFadeOutRunning = false;
        m_fadeOutAnim.Completed += OnFadeOutComplete;
    }
    private void OnDispSettChanged(object sender, EventArgs e)
    {
        Logger?.Log("System Display Settings changed, updating OSD");
        UpdatePos();
    }

    private void OnUserPrefChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Logger?.Log("System User Preferences changed, updating OSD");
        UpdatePos();
    }

    // do unsubcribing really works?
    private void OnEventUpdatePos<T>(object sender, T e) => UpdatePos();
    private void OnEventUpdatePosAlign<T>(object sender, T e) => UpdatePosAlign();

    private void OnFadeOutComplete(object sender, EventArgs e)
    {
        m_isFadeOutRunning = false;
        Hide();
    }

    private void OsdWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        Globals.Osd.screenProvider.MainScreenChanged -= OnEventUpdatePos;
        OptionsStorage.Osd.HorizontalAlignmentChanged -= OnEventUpdatePosAlign;
        OptionsStorage.Osd.VerticalAlignmentChanged -= OnEventUpdatePosAlign;
        OptionsStorage.AltOsdOptionsFullscreenApps.HorizontalAlignmentChanged -= OnEventUpdatePosAlign;
        OptionsStorage.AltOsdOptionsFullscreenApps.VerticalAlignmentChanged -= OnEventUpdatePosAlign;
        SystemEvents.UserPreferenceChanged -= OnUserPrefChanged;
        SystemEvents.DisplaySettingsChanged -= OnDispSettChanged;
    }
}
