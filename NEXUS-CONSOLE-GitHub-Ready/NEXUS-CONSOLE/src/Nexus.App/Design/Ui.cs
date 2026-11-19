using System.Windows;
using System.Windows.Controls;

namespace Nexus.Shell.Design;

/// <summary>Petites fabriques d'éléments pour garder les pages courtes et homogènes.</summary>
public static class Ui
{
    public static TextBlock Text(string text, double size, bool dim = false, FontWeight? weight = null)
    {
        var tb = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        if (weight is { } w) tb.FontWeight = w;
        if (dim) tb.SetResourceReference(TextBlock.ForegroundProperty, "TextDimBrush");
        return tb;
    }

    public static Button Card(string title, string subtitle, Action? onClick, out TextBlock subtitleText)
    {
        var sp = new StackPanel();
        sp.Children.Add(Text(title, 20, weight: FontWeights.SemiBold));
        subtitleText = Text(subtitle, 14, dim: true);
        subtitleText.Margin = new Thickness(0, 4, 0, 0);
        sp.Children.Add(subtitleText);

        var b = new Button
        {
            Content = sp,
            Style = (Style)Application.Current.FindResource("Card"),
            Margin = new Thickness(0, 0, 12, 12)
        };
        if (onClick is not null) b.Click += (_, _) => onClick();
        return b;
    }
}
