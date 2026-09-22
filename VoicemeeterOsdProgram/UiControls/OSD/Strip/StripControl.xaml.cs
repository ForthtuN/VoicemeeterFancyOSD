using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VoicemeeterOsdProgram.Types;

namespace VoicemeeterOsdProgram.UiControls.OSD.Strip;

/// <summary>
/// Interaction logic for StripControl.xaml
/// </summary>
public partial class StripControl : UserControl, IOsdRootElement, IOsdAnimatedElement
{
    private bool m_hasChanges = false;
    private bool m_hasChildVis = false;

    private readonly DoubleAnimation m_highlightAnim = new()
    {
        From = 0.0,
        To = 0.58,
        AutoReverse = true,
        Duration = TimeSpan.FromMilliseconds(180),
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };
    private readonly DoubleAnimation m_scaleAnim = new()
    {
        From = 0.99,
        To = 1.015,
        AutoReverse = true,
        Duration = TimeSpan.FromMilliseconds(180),
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };

    public Func<bool> IsAnimationsEnabled { get; set; } = () => true;

    /// <summary>
    /// Resets itself when read
    /// </summary>
    public bool HasChangesFlag
    {
        get
        {
            if (m_hasChanges)
            {
                m_hasChanges = false;
                return true;
            }
            return false;
        }
        set => m_hasChanges = value;
    }

    /// <summary>
    /// Resets itself when read
    /// </summary>
    public bool HasAnyChildVisibleFlag
    {
        get
        {
            if (m_hasChildVis)
            {
                m_hasChildVis = false;
                return true;
            }
            return false;
        }
        set => m_hasChildVis = value;
    }

    public StripControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public static readonly DependencyProperty ShowHorizontalSeparatorAfterProperty = DependencyProperty.Register(
        "ShowHorizontalSeparatorAfter", typeof(bool), typeof(StripControl));
    public bool ShowHorizontalSeparatorAfter
    {
        get => (bool)GetValue(ShowHorizontalSeparatorAfterProperty);
        set
        {
            var thickness = BorderThickness;
            thickness.Right = value ? 1 : 0;
            BorderThickness = thickness;
            SetValue(ShowHorizontalSeparatorAfterProperty, value);
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        bool isVisible = Visibility == Visibility.Visible;
        var wasVisible = (bool)e.OldValue;
        if (!(isVisible && !wasVisible)) return;

        Highlight();
    }

    private void Highlight()
    {
        if (!IsAnimationsEnabled()) return;

        if (HighlightWrap.RenderTransform is not ScaleTransform t)
        {
            HighlightWrap.RenderTransform = t = new ScaleTransform(1, 1, 0.5d, 0.5d);
        }

        HighlightWrap.BeginAnimation(Border.OpacityProperty, null);
        t.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        HighlightWrap.BeginAnimation(Border.OpacityProperty, m_highlightAnim);
        t.BeginAnimation(ScaleTransform.ScaleXProperty, m_scaleAnim);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, m_scaleAnim);
    }
}
