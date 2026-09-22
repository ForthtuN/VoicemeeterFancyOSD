using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VoicemeeterOsdProgram.Types;

namespace VoicemeeterOsdProgram.UiControls.OSD.Strip;

/// <summary>
/// Interaction logic for FaderContainer.xaml
/// </summary>
public partial class FaderContainer : ContentControl, IOsdAnimatedElement
{
    private const double ValueSmoothingDurationMs = 70.0;
    private static readonly TimeSpan HighlightHoldTime = TimeSpan.FromMilliseconds(320);

    private readonly DoubleAnimation m_highlightInAnim = new()
    {
        To = 0.88,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        Duration = new Duration(TimeSpan.FromMilliseconds(90)),
        FillBehavior = FillBehavior.HoldEnd
    };
    private readonly DoubleAnimation m_scaleInAnim = new()
    {
        To = 1.08,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        Duration = new Duration(TimeSpan.FromMilliseconds(90)),
        FillBehavior = FillBehavior.HoldEnd
    };
    private readonly DoubleAnimation m_highlightOutAnim = new()
    {
        To = 0.0,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        Duration = new Duration(TimeSpan.FromMilliseconds(260)),
        FillBehavior = FillBehavior.Stop
    };
    private readonly DoubleAnimation m_scaleOutAnim = new()
    {
        To = 1.0,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        Duration = new Duration(TimeSpan.FromMilliseconds(260)),
        FillBehavior = FillBehavior.Stop
    };
    private readonly DispatcherTimer m_highlightHoldTimer = new(DispatcherPriority.Render)
    {
        Interval = HighlightHoldTime
    };

    private bool m_highlightActive;
    private bool m_highlightFadingOut;
    private bool m_valueRenderingSubscribed;
    private double m_valueAnimationStart;
    private double m_valueAnimationTarget;
    private long m_valueAnimationStartTimestamp;

    public FaderContainer()
    {
        InitializeComponent();
        m_highlightHoldTimer.Tick += OnHighlightHoldElapsed;
        m_highlightOutAnim.Completed += OnHighlightFadeOutComplete;
        Unloaded += OnUnloaded;
    }

    public Func<bool> IsAnimationsEnabled { get; set; } = () => true;

    private void OnFaderMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not Slider slider) return;

        double val = 3;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            val = 1;
        }
        else if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            val = 0.1;
        }
        if (e.Delta < 0) val *= -1;

        slider.Value += val;
    }

    private void OnFaderMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider) return;

        slider.Value = 0;
    }

    public void Highlight()
    {
        if (!IsAnimationsEnabled())
        {
            ResetHighlight();
            return;
        }

        if (HighlightWrap.RenderTransform is not ScaleTransform t)
        {
            HighlightWrap.RenderTransform = t = new ScaleTransform(1, 1, 0.5d, 0.5d);
        }

        if (!m_highlightActive || m_highlightFadingOut)
        {
            m_highlightInAnim.From = HighlightWrap.Opacity;
            m_scaleInAnim.From = t.ScaleX;
            m_highlightActive = true;
            m_highlightFadingOut = false;
            HighlightWrap.BeginAnimation(Border.OpacityProperty, m_highlightInAnim, HandoffBehavior.SnapshotAndReplace);
            t.BeginAnimation(ScaleTransform.ScaleXProperty, m_scaleInAnim, HandoffBehavior.SnapshotAndReplace);
        }

        m_highlightHoldTimer.Stop();
        m_highlightHoldTimer.Start();
    }

    internal void SetDisplayedValue(double value, bool animate)
    {
        if (!animate || !IsAnimationsEnabled() || !IsVisible)
        {
            StopValueAnimation();
            SetFaderValueWithoutWrite(value);
            m_valueAnimationTarget = value;
            return;
        }

        if (m_valueRenderingSubscribed && Math.Abs(m_valueAnimationTarget - value) < 0.0001)
        {
            return;
        }

        double current = Fader.Value;
        if (Math.Abs(current - value) < 0.0001)
        {
            StopValueAnimation();
            m_valueAnimationTarget = value;
            return;
        }

        m_valueAnimationStart = current;
        m_valueAnimationTarget = value;
        m_valueAnimationStartTimestamp = Stopwatch.GetTimestamp();

        if (!m_valueRenderingSubscribed)
        {
            CompositionTarget.Rendering += OnValueRendering;
            m_valueRenderingSubscribed = true;
        }
    }

    internal void StopValueAnimation()
    {
        if (!m_valueRenderingSubscribed) return;

        CompositionTarget.Rendering -= OnValueRendering;
        m_valueRenderingSubscribed = false;
    }

    private void OnValueRendering(object sender, EventArgs e)
    {
        double elapsedMs = Stopwatch.GetElapsedTime(m_valueAnimationStartTimestamp).TotalMilliseconds;
        double progress = Math.Clamp(elapsedMs / ValueSmoothingDurationMs, 0.0, 1.0);
        double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);
        double value = m_valueAnimationStart + ((m_valueAnimationTarget - m_valueAnimationStart) * easedProgress);

        SetFaderValueWithoutWrite(value);

        if (progress >= 1.0)
        {
            StopValueAnimation();
            SetFaderValueWithoutWrite(m_valueAnimationTarget);
        }
    }

    private void SetFaderValueWithoutWrite(double value)
    {
        Fader.isCustomFlag = true;
        try
        {
            Fader.Value = value;
        }
        finally
        {
            Fader.isCustomFlag = false;
        }
    }

    private void OnHighlightHoldElapsed(object sender, EventArgs e)
    {
        m_highlightHoldTimer.Stop();
        if (!m_highlightActive) return;

        m_highlightFadingOut = true;
        if (HighlightWrap.RenderTransform is not ScaleTransform t) return;

        m_highlightOutAnim.From = HighlightWrap.Opacity;
        m_scaleOutAnim.From = t.ScaleX;
        HighlightWrap.BeginAnimation(Border.OpacityProperty, m_highlightOutAnim, HandoffBehavior.SnapshotAndReplace);
        t.BeginAnimation(ScaleTransform.ScaleXProperty, m_scaleOutAnim, HandoffBehavior.SnapshotAndReplace);
    }

    private void OnHighlightFadeOutComplete(object sender, EventArgs e)
    {
        ResetHighlight();
    }

    private void ResetHighlight()
    {
        m_highlightHoldTimer.Stop();
        m_highlightActive = false;
        m_highlightFadingOut = false;
        HighlightWrap.BeginAnimation(Border.OpacityProperty, null);
        HighlightWrap.Opacity = 0.0;

        if (HighlightWrap.RenderTransform is ScaleTransform t)
        {
            t.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            t.ScaleX = 1.0;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopValueAnimation();
        ResetHighlight();
    }
}
