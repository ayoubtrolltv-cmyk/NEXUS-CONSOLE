using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>Section pas encore développée : le dit franchement, sans faux contenu.</summary>
public sealed class PlaceholderPage : UserControl, INavPage
{
    public string Title { get; }

    public PlaceholderPage(string title, string plannedFor)
    {
        Title = title;
        var sp = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(Ui.Text(title, 44, weight: FontWeights.Bold));
        var d = Ui.Text($"Cette section n'est pas encore développée. Prévue : {plannedFor}.", 18, dim: true);
        d.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(d);
        Content = sp;
    }

    public bool HandleAction(NavAction action) => false;
    public void OnShown() { }
    public void OnHidden() { }
}
