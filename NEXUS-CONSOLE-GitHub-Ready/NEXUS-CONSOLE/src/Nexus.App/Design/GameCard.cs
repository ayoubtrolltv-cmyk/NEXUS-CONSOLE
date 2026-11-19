using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexus.Core.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell.Design;

/// <summary>Carte de jeu : pochette si elle existe, sinon icône, sinon initiales sur un dégradé propre au jeu.</summary>
public static class GameCard
{
    public static Brush Gradient(Color c)
    {
        var dark = Color.FromRgb((byte)(c.R * 0.4), (byte)(c.G * 0.4), (byte)(c.B * 0.4));
        var b = new LinearGradientBrush(c, dark, 60);
        b.Freeze();
        return b;
    }

    public static Button Create(Game g, double width, Action onClick, Action<Game>? onFocus = null)
    {
        var height = Math.Round(width * 1.4);
        var color = GameVisuals.ColorFor(g.Name);
        var root = new Grid { Width = width, Height = height, Opacity = g.Installed ? 1.0 : 0.45 };
        var radius = new CornerRadius(14);

        var cover = ImageCache.Load(g.CoverPath, (int)width * 2);
        if (cover is not null)
        {
            root.Children.Add(new Border { CornerRadius = radius, Background = new ImageBrush(cover) { Stretch = Stretch.UniformToFill } });
        }
        else
        {
            root.Children.Add(new Border { CornerRadius = radius, Background = Gradient(color) });
            var icon = ImageCache.Load(g.IconPath, 128);
            if (icon is not null)
            {
                root.Children.Add(new Image
                {
                    Source = icon, Width = width * 0.42, Height = width * 0.42,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, height * 0.12)
                });
            }
            else
            {
                root.Children.Add(new TextBlock
                {
                    Text = GameVisuals.Initials(g.Name), FontSize = width * 0.28, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White, Opacity = 0.85,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, height * 0.12)
                });
            }
        }

        var scrimBrush = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(0xD0, 0, 0, 0), 90);
        scrimBrush.Freeze();
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = g.Name, FontSize = Math.Max(13, width * 0.085), FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = width * 0.23
        });
        text.Children.Add(new TextBlock
        {
            Text = GameVisuals.PlatformLabel(g.Platform) + (g.Installed ? "" : " · non installé"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(0xB8, 255, 255, 255)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        root.Children.Add(new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(0, 0, 14, 14),
            Padding = new Thickness(12, 28, 12, 12), Background = scrimBrush, Child = text
        });

        if (g.Favorite)
        {
            var star = new TextBlock { Text = "★", FontSize = 18, Margin = new Thickness(0, 8, 10, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            star.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            root.Children.Add(star);
        }

        var button = new Button
        {
            Content = root,
            Style = (Style)Application.Current.FindResource("GameTile"),
            Margin = new Thickness(8)
        };
        button.Click += (_, _) => onClick();
        if (onFocus is not null) button.GotKeyboardFocus += (_, _) => onFocus(g);
        return button;
    }
}
