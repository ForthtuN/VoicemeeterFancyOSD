using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VoicemeeterOsdProgram.Types;

namespace VoicemeeterOsdProgram.UiControls.OSD.Strip;

/// <summary>
/// Interaction logic for ButtonContainer.xaml
/// </summary>
public partial class ButtonContainer : ContentControl, IOsdAnimatedElement
{
    private readonly Duration m_animDuration = new(TimeSpan.FromMilliseconds(140));
    private readonly DoubleAnimation m_highlightAnim = new()
    {
        From = 0.92,
        To = 1.06,
        AutoReverse = true,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };
    private readonly DoubleAnimation m_opacityAnim = new()
    {
        From = 0.0,
        To = 0.78,
        AutoReverse = true,
        EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };

    public ButtonContainer()
    {
        m_highlightAnim.Duration = m_animDuration;
        m_opacityAnim.Duration = m_animDuration;

        InitializeComponent();
    }

    public Func<bool> IsAnimationsEnabled { get; set; } = () => true;

    public void Highlight()
    {
        if (!IsAnimationsEnabled()) return;

        if (HighlightWrap.RenderTransform is not ScaleTransform t)
        {
            HighlightWrap.RenderTransform = t = new ScaleTransform(1, 1, 0.5d, 0.5d);
        }

        t.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        t.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        HighlightWrap.BeginAnimation(Border.OpacityProperty, null);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, m_highlightAnim);
        t.BeginAnimation(ScaleTransform.ScaleXProperty, m_highlightAnim);
        HighlightWrap.BeginAnimation(Border.OpacityProperty, m_opacityAnim);
    }
}
