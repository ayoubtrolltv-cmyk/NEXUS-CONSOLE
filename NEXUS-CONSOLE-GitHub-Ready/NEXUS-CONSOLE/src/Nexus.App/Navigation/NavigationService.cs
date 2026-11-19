using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexus.Core.Audio;
using Nexus.Core.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Services;

namespace Nexus.Shell.Navigation;

/// <summary>Pile de pages + traduction des actions de navigation en déplacement de focus, avec bruitages.</summary>
public sealed class NavigationService
{
    private readonly ContentControl _host;
    private readonly Stack<UserControl> _stack = new();

    public NavigationService(ContentControl host) => _host = host;

    public void Reset(UserControl page)
    {
        Clear();
        Push(page);
    }

    public void Push(UserControl page)
    {
        if (_stack.Count > 0 && _stack.Peek() is INavPage previous) previous.OnHidden();
        _stack.Push(page);
        Show(page);
    }

    public bool Back()
    {
        if (_stack.Count <= 1) return false;
        Pop();
        Show(_stack.Peek());
        return true;
    }

    public void Clear()
    {
        while (_stack.Count > 0) Pop();
        _host.Content = null;
    }

    /// <summary>Laisse la page courante consommer l'action (retourne true si elle l'a fait).</summary>
    public bool PageHandles(NavAction action) => _stack.Count > 0 && _stack.Peek() is INavPage p && p.HandleAction(action);

    public void Dispatch(NavAction action)
    {
        if (_stack.Count > 0 && _stack.Peek() is INavPage p && p.HandleAction(action)) return;

        switch (action)
        {
            case NavAction.Back: if (Back()) SoundService.Play(SoundId.Back); break;
            case NavAction.Up: if (Move(FocusNavigationDirection.Up)) SoundService.Play(SoundId.Move); break;
            case NavAction.Down: if (Move(FocusNavigationDirection.Down)) SoundService.Play(SoundId.Move); break;
            case NavAction.Left: if (Move(FocusNavigationDirection.Left)) SoundService.Play(SoundId.Move); break;
            case NavAction.Right: if (Move(FocusNavigationDirection.Right)) SoundService.Play(SoundId.Move); break;
            case NavAction.Accept:
                if (Keyboard.FocusedElement is ButtonBase b)
                {
                    SoundService.Play(SoundId.Confirm);   // avant l'action : un son de test éventuel prend le relais
                    Invoke(b);
                }
                break;
        }
    }

    private void Pop()
    {
        var page = _stack.Pop();
        (page as INavPage)?.OnHidden();
        (page as IDisposable)?.Dispose();
    }

    private void Show(UserControl page)
    {
        _host.Content = page;
        if (Motion.Level == 0)
        {
            page.Opacity = 1;
            page.RenderTransform = Transform.Identity;
        }
        else
        {
            var slide = new TranslateTransform(0, 14);
            page.RenderTransform = slide;
            page.Opacity = 0;
            var fade = new DoubleAnimation(1, Motion.D(240));
            fade.Completed += (_, _) =>
            {
                page.BeginAnimation(UIElement.OpacityProperty, null);
                page.Opacity = 1;
                page.RenderTransform = Transform.Identity;
            };
            page.BeginAnimation(UIElement.OpacityProperty, fade);
            slide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, Motion.D(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        (page as INavPage)?.OnShown();
        page.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            new Action(() => page.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))));
    }

    private bool Move(FocusNavigationDirection direction)
    {
        if (Keyboard.FocusedElement is ButtonBase b) return b.MoveFocus(new TraversalRequest(direction));
        return _host.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private static void Invoke(ButtonBase b)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(b);
        (peer?.GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
    }
}
