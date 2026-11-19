using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexus.Core.Audio;
using Nexus.Core.Input;
using Nexus.Core.Logging;
using Nexus.Core.Models;
using Nexus.Data;
using Nexus.Detection;
using Nexus.Input;
using Nexus.Launching;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Pages;
using Nexus.Shell.Services;

namespace Nexus.Shell;

public partial class MainWindow : Window, IShell
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private readonly NavigationService _nav;
    private readonly (string Label, Func<UserControl> Create)[] _sections;
    private readonly List<Button> _tabButtons = new();
    private readonly DispatcherTimer _clock = new();
    private readonly DispatcherTimer _scanTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly GameLauncher _launcher = new();

    private int _index = -1;
    private bool _fullscreen, _firstRender = true;
    private bool _launching, _gameDetected;
    private CancellationTokenSource? _launchCts;
    private WindowState _stateBeforeGame = WindowState.Normal;

    public SettingsService Settings { get; }
    public GameRepository? Games => Settings.Games;
    public GameScanner? Scanner { get; }
    public ControllerManager Pad { get; } = new();
    public NavigationService Nav => _nav;

    public MainWindow(SettingsService settings)
    {
        InitializeComponent();
        Settings = settings;
        _nav = new NavigationService(Host);

        if (Games is not null)
            Scanner = new GameScanner(Games) { IconProvider = GameVisuals.ExtractIcon };

        _sections = new (string, Func<UserControl>)[]
        {
            ("Accueil", () => new HomePage(this)),
            ("Bibliothèque", () => new LibraryPage(this)),
            ("Découvrir", () => new StorePage(this, discover: true)),
            ("Boutique", () => new StorePage(this, discover: false)),
            ("Cloud Gaming", () => new CloudPage(this, appsOnly: false)),
            ("Applications", () => new CloudPage(this, appsOnly: true)),
            ("Performance", () => new BenchmarkPage(this)),
            ("Paramètres", () => new SettingsPage(this)),
        };

        for (var i = 0; i < _sections.Length; i++)
        {
            var index = i;
            var b = new Button { Content = _sections[i].Label, Style = (Style)FindResource("NavTab") };
            b.Click += (_, _) => SelectSection(index);
            _tabButtons.Add(b);
            Tabs.Children.Add(b);
        }

        Settings.Changed += OnSettingsChanged;
        Settings.ProfileChanged += OnProfileChanged;
        PreviewKeyDown += OnKey;
        ContentRendered += OnFirstRender;
        SizeChanged += (_, _) => UpdateScale();
        Activated += (_, _) => Pad.SetActive(true);
        Deactivated += (_, _) => Pad.SetActive(false);
        Closed += OnClosed;

        // Manettes : les événements arrivent d'un thread d'arrière-plan et sont renvoyés vers l'interface.
        Pad.Profile = PadProfile.FromJson(Settings.Current.PadProfileJson);
        Pad.ActionRaised += a => Dispatcher.InvokeAsync(() => Perform(a));
        Pad.ConnectionChanged += () => Dispatcher.InvokeAsync(UpdatePadStatus);
        UpdatePadStatus();

        _scanTimer.Tick += (_, _) => _ = Scanner?.ScanAsync();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastText.BeginAnimation(OpacityProperty, new DoubleAnimation(0, Motion.D(300))); };

        SetNetwork(NetworkInterface.GetIsNetworkAvailable());
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        StartClock();

        UpdateScale();
        UpdateProfile();
        SelectSection(0);
        if (!Settings.Current.FirstRunDone) _nav.Reset(new FirstRunPage(this));   // premier lancement : assistant
        if (Settings.Current.StartFullscreen) SetFullscreen(true);
    }

    public void GoToSection(string label)
    {
        var i = Array.FindIndex(_sections, s => s.Label == label);
        if (i >= 0) SelectSection(i);
    }

    private void SelectSection(int i)
    {
        _index = i;
        for (var k = 0; k < _tabButtons.Count; k++) _tabButtons[k].Tag = k == i ? "on" : null;
        _nav.Reset(_sections[i].Create());
    }

    // ---- Responsive : l'interface est dessinée pour 1600×900 puis mise à l'échelle (0,8× à 2×) ----
    private void UpdateScale()
    {
        var auto = ActualWidth > 0 && ActualHeight > 0
            ? Math.Clamp(Math.Min(ActualWidth / 1600.0, ActualHeight / 900.0), 0.8, 2.0)
            : 1.0;
        var baseScale = Settings.Current.UiScalePercent == 0 ? auto : Settings.Current.UiScalePercent / 100.0;
        var scale = baseScale * (Settings.Current.TvMode ? 1.25 : 1.0);
        UiScale.ScaleX = scale;
        UiScale.ScaleY = scale;
    }

    private void OnProfileChanged()
    {
        UpdateProfile();
        Pad.Profile = PadProfile.FromJson(Settings.Current.PadProfileJson);   // chaque profil a sa configuration de manette
        UpdatePadStatus();
    }

    private void OnSettingsChanged()
    {
        UpdateScale();
        if (!Settings.Current.AmbientEnabled) SetAmbient(null);
        ApplyScanTimer();
    }

    private void ApplyScanTimer()
    {
        var minutes = Settings.Current.ScanMinutes;
        _scanTimer.Stop();
        if (minutes <= 0 || _firstRender) return;   // avant le premier affichage, OnFirstRender s'en charge
        _scanTimer.Interval = TimeSpan.FromMinutes(minutes);
        _scanTimer.Start();
    }

    private void UpdateProfile()
    {
        var name = Settings.CurrentProfile?.Name ?? "Joueur";
        ProfileName.Text = name;
        AvatarText.Text = GameVisuals.Initials(name);
        Avatar.Background = new SolidColorBrush(GameVisuals.ColorFor(name));
        AvatarText.Foreground = Brushes.White;
    }

    public void Toast(string text) => ShowToast(text);

    public void SetAmbient(Color? color)
    {
        var on = color is not null && Settings.Current.AmbientEnabled;
        var top = on ? Color.FromArgb(0x55, color!.Value.R, color.Value.G, color.Value.B) : Color.FromArgb(0, 0, 0, 0);
        var bottom = Color.FromArgb(0, top.R, top.G, top.B);
        AmbTop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(top, Motion.D(450)));
        AmbBottom.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(bottom, Motion.D(450)));
    }

    // ---- Entrées : clavier et manette convergent vers Perform(NavAction) ----
    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.F11) { SetFullscreen(!_fullscreen); e.Handled = true; return; }

        NavAction? action = key switch
        {
            Key.Up => NavAction.Up,
            Key.Down => NavAction.Down,
            Key.Left => NavAction.Left,
            Key.Right => NavAction.Right,
            Key.Enter or Key.Space => NavAction.Accept,
            Key.Escape or Key.Back => NavAction.Back,
            Key.PageUp => NavAction.TabPrev,
            Key.PageDown => NavAction.TabNext,
            _ => null
        };
        if (action is { } a) { Perform(a); e.Handled = true; }
    }

    private void Perform(NavAction action)
    {
        if (!IsActive) return;
        if (_launching)
        {
            // Pendant l'attente du jeu, seul « Retour » compte : il annule le lancement.
            if (action == NavAction.Back && !_gameDetected) _launchCts?.Cancel();
            return;
        }
        switch (action)
        {
            case NavAction.TabPrev: if (!_nav.PageHandles(action)) SwitchSection(-1); break;
            case NavAction.TabNext: if (!_nav.PageHandles(action)) SwitchSection(1); break;
            case NavAction.Menu: _nav.PageHandles(action); break;   // menu contextuel général : Phase 9
            default: _nav.Dispatch(action); break;
        }
    }

    private void SwitchSection(int delta)
    {
        SoundService.Play(SoundId.Select);
        SelectSection((_index + delta + _sections.Length) % _sections.Length);
    }

    // ---- Manettes : état et indications à l'écran ----
    public void ApplyPadProfile(PadProfile profile)
    {
        Pad.Profile = profile;
        Settings.Update(c => c.PadProfileJson = profile.ToJson());
        UpdatePadStatus();
    }

    private void UpdatePadStatus()
    {
        var info = Pad.Primary;
        PadText.Text = info is null ? "○ Aucune manette" : $"● {info.Name}";
        if (info is null) { HintText.Visibility = Visibility.Collapsed; return; }

        string G(NavAction a) => Glyphs.Name(info.Kind, Pad.Profile.ButtonFor(a));
        HintText.Text = $"{G(NavAction.Accept)} Valider    {G(NavAction.Back)} Retour    {G(NavAction.TabPrev)} / {G(NavAction.TabNext)} Sections";
        HintText.Visibility = Visibility.Visible;
    }

    // ---- Lancement d'un jeu et retour automatique ----
    public async Task LaunchAsync(Game game)
    {
        if (_launching) return;
        if (!game.Installed) { ShowToast("Ce jeu n'est pas installé."); return; }

        _launching = true;
        _gameDetected = false;
        _launchCts = new CancellationTokenSource();
        var info = Pad.Primary;
        var backName = info is null ? "Échap" : $"{Glyphs.Name(info.Kind, Pad.Profile.ButtonFor(NavAction.Back))} ou Échap";
        Overlay.Visibility = Visibility.Visible;
        OverlayTitle.Text = $"Lancement de {game.Name}…";
        OverlayHint.Text = $"{backName} pour annuler";

        try
        {
            var result = await _launcher.RunAsync(game, () => Dispatcher.Invoke(OnGameDetected), _launchCts.Token);
            switch (result.Outcome)
            {
                case SessionOutcome.Ended:
                    Games?.RecordSession(game.Id, result.StartedUtc, (long)result.Played.TotalSeconds);
                    var played = result.Played.TotalMinutes >= 1 ? $"{(int)result.Played.TotalMinutes} min" : $"{(int)result.Played.TotalSeconds} s";
                    ShowToast(result.Played.TotalSeconds < 20
                        ? $"« {game.Name} » s'est fermé très vite ({played}) : échec de démarrage ou plantage ? Voir les journaux."
                        : $"Session terminée : {played} sur « {game.Name} ».");
                    _ = Scanner?.ScanAsync();
                    break;
                case SessionOutcome.NeverStarted:
                    ShowToast($"« {game.Name} » n'a pas démarré. Vérifie son lanceur (mise à jour, connexion requise…).");
                    break;
                case SessionOutcome.Cancelled:
                    ShowToast("Lancement annulé.");
                    break;
                case SessionOutcome.Untracked:
                    ShowToast("Jeu lancé, mais NEXUS ne peut pas le suivre : reviens ici avec Alt+Tab.");
                    break;
            }
        }
        catch (LaunchException ex) { ShowToast(ex.Message); }
        catch (Exception ex)
        {
            Log.Error("Erreur pendant la session de jeu.", ex);
            ShowToast("Un problème est survenu pendant le lancement (détails dans les journaux).");
        }
        finally
        {
            _launching = false;
            _launchCts?.Dispose();
            _launchCts = null;
            Overlay.Visibility = Visibility.Collapsed;
            if (_gameDetected) RestoreAfterGame();
            _gameDetected = false;
        }
    }

    private void OnGameDetected()
    {
        _gameDetected = true;
        _stateBeforeGame = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
        SoundService.Play(SoundId.Launch);
        Pad.Suspend();                       // plus aucune lecture de manette pendant le jeu
        Overlay.Visibility = Visibility.Collapsed;
        WindowState = WindowState.Minimized; // ni résolution, ni plein écran du jeu modifiés
    }

    private void RestoreAfterGame()
    {
        WindowState = _stateBeforeGame;
        Activate();
        WindowHandoff.BringToFront(new WindowInteropHelper(this).Handle);
        Pad.Resume();
        SoundService.Play(SoundId.Return);
    }

    private void ShowToast(string text)
    {
        ToastText.Text = text;
        ToastText.BeginAnimation(OpacityProperty, new DoubleAnimation(1, Motion.D(200)));
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // ---- Fenêtre ----
    private void SetFullscreen(bool on)
    {
        _fullscreen = on;
        WindowState = WindowState.Normal;
        if (on)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;   // nécessaire pour couvrir aussi la barre des tâches
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
        }
    }

    // ---- Barre supérieure : heure, date, réseau ----
    private void StartClock()
    {
        _clock.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clock.Start();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm", Fr);
        var date = now.ToString("dddd d MMMM", Fr);
        DateText.Text = char.ToUpper(date[0], Fr) + date[1..];
        _clock.Interval = TimeSpan.FromSeconds(61 - now.Second);   // un seul réveil par minute
    }

    private void OnNetworkChanged(object? s, NetworkAvailabilityEventArgs e) =>
        Dispatcher.InvokeAsync(() => SetNetwork(e.IsAvailable));

    // Indique la présence d'une connexion réseau, pas la joignabilité d'Internet.
    private void SetNetwork(bool available) => NetText.Text = available ? "● Réseau connecté" : "○ Pas de réseau";

    private void OnFirstRender(object? sender, EventArgs e)
    {
        if (!_firstRender) return;
        _firstRender = false;
        using var p = Process.GetCurrentProcess();
        StartupInfo.ReadyMs = (DateTime.Now - p.StartTime).TotalMilliseconds;
        Log.Info($"Interface affichée {StartupInfo.ReadyMs:0} ms après le lancement du processus");

        _ = Scanner?.ScanAsync();   // détection initiale en arrière-plan
        ApplyScanTimer();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        Settings.Changed -= OnSettingsChanged;
        Settings.ProfileChanged -= OnProfileChanged;
        _clock.Stop();
        _scanTimer.Stop();
        _toastTimer.Stop();
        _launchCts?.Cancel();
        Pad.Dispose();
        _nav.Clear();
    }
}
