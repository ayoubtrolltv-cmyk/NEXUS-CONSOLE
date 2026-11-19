using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Nexus.Shell.Services;
using Nexus.Store;

namespace Nexus.Shell.Design;

/// <summary>Carte de boutique : visuel téléchargé à la demande (cache disque), titre, prix et réduction.</summary>
public static class StoreCard
{
    public static Button Create(StoreItem item, StoreCache cache, double width, Action onClick, CancellationToken ct)
    {
        var imageHeight = Math.Round(width * 0.47);   // visuels Steam 460×215
        var root = new StackPanel { Width = width, Background = Brushes.Transparent };
        var color = GameVisuals.ColorFor(item.Name);

        var image = new Border { Height = imageHeight, CornerRadius = new CornerRadius(14, 14, 0, 0), Background = GameCard.Gradient(color) };
        root.Children.Add(image);
        _ = LoadImageAsync(image, cache, item.ImageUrl, (int)width * 2, ct);

        var text = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
        text.Children.Add(new TextBlock { Text = item.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var price = new TextBlock { Margin = new Thickness(0, 4, 0, 0), FontSize = 13 };
        if (item.DiscountPercent > 0)
        {
            price.Inlines.Add(new Run($"-{item.DiscountPercent} % ") { FontWeight = FontWeights.Bold });
            price.Inlines.Add(new Run(item.PriceText ?? ""));
            if (item.OriginalPriceText is not null)
                price.Inlines.Add(new Run("  " + item.OriginalPriceText) { TextDecorations = TextDecorations.Strikethrough, Foreground = Brushes.Gray });
        }
        else price.Text = item.PriceText ?? "Bientôt";
        price.SetResourceReference(TextBlock.ForegroundProperty, item.DiscountPercent > 0 ? "AccentBrush" : "TextDimBrush");
        text.Children.Add(price);
        root.Children.Add(text);

        var frame = new Border { CornerRadius = new CornerRadius(14), Child = root };
        frame.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");

        var button = new Button { Content = frame, Style = (Style)Application.Current.FindResource("GameTile"), Margin = new Thickness(8) };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static async Task LoadImageAsync(Border target, StoreCache cache, string? url, int decodeWidth, CancellationToken ct)
    {
        try
        {
            var path = await cache.GetImageAsync(url, ct);
            if (path is null || ct.IsCancellationRequested) return;
            var src = ImageCache.Load(path, decodeWidth);
            if (src is not null) target.Background = new ImageBrush(src) { Stretch = Stretch.UniformToFill };
        }
        catch (OperationCanceledException) { }
    }
}
