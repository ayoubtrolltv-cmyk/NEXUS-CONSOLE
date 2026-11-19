using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nexus.Shell.Design;

/// <summary>Durées d'animation pilotées par le réglage « Animations » et animation de focus.</summary>
public static class Motion
{
    /// <summary>0 = aucune, 1 = réduites (moitié du temps), 2 = complètes.</summary>
    public static int Level { get; set; } = 2;

    public static Duration D(int ms) => Level switch
    {
        0 => new Duration(TimeSpan.Zero),
        1 => new Duration(TimeSpan.FromMilliseconds(ms / 2.0)),
        _ => new Duration(TimeSpan.FromMilliseconds(ms))
    };

    public static readonly DependencyProperty FocusScaleProperty = DependencyProperty.RegisterAttached(
        "FocusScale", typeof(double), typeof(Motion), new PropertyMetadata(1.0, OnFocusScaleChanged));

    public static double GetFocusScale(DependencyObject o) => (double)o.GetValue(FocusScaleProperty);
    public static void SetFocusScale(DependencyObject o, double v) => o.SetValue(FocusScaleProperty, v);

    private static void OnFocusScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        el.RenderTransform = new ScaleTransform(1, 1);   // une instance par élément (nécessaire pour animer)
        el.GotKeyboardFocus -= OnGotFocus;
        el.LostKeyboardFocus -= OnLostFocus;
        el.GotKeyboardFocus += OnGotFocus;
        el.LostKeyboardFocus += OnLostFocus;
    }

    private static void OnGotFocus(object s, RoutedEventArgs e) => Animate((UIElement)s, GetFocusScale((DependencyObject)s));
    private static void OnLostFocus(object s, RoutedEventArgs e) => Animate((UIElement)s, 1.0);

    private static void Animate(UIElement el, double to)
    {
        if (el.RenderTransform is not ScaleTransform st) return;
        var a = new DoubleAnimation(to, D(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
