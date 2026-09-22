using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VoicemeeterOsdProgram.Types;

namespace VoicemeeterOsdProgram.UiControls.OSD.Strip;

/// <summary>
/// Interaction logic for LimiterContainer.xaml
/// </summary>
public partial class LimiterContainer : ContentControl, IOsdAnimatedElement
{
    private readonly DoubleAnimation m_highlightAnim = new()
    {
        From = 0.0,
        To = 0.82,
        AutoReverse = true,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        Duration = new Duration(TimeSpan.FromMilliseconds(140)),
        FillBehavior = FillBehavior.Stop
    };
    private readonly DoubleAnimation m_scaleAnim = new()
    {
        From = 0.94,
        To = 1.10,
        AutoReverse = true,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        Duration = new Duration(TimeSpan.FromMilliseconds(140)),
        FillBehavior = FillBehavior.Stop
    };

    public LimiterContainer()
    {
        InitializeComponent();
    }

    public Func<bool> IsAnimationsEnabled { get; set; } = () => true;

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider) return;

        slider.Value = 12;
    }

    public void Highlight()
    {
        if (!IsAnimationsEnabled()) return;

        if (HighlightWrap.RenderTransform is not ScaleTransform t)
        {
            HighlightWrap.RenderTransform = t = new ScaleTransform(1, 1, 0.5d, 0.5d);
        }

        HighlightWrap.BeginAnimation(Border.OpacityProperty, null);
        t.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        HighlightWrap.BeginAnimation(Border.OpacityProperty, m_highlightAnim);
        t.BeginAnimation(ScaleTransform.ScaleXProperty, m_scaleAnim);
    }
}
