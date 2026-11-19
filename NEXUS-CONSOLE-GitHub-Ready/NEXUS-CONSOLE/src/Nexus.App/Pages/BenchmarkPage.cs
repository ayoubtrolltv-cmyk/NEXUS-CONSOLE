using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexus.Core.Config;
using Nexus.Core.Input;
using Nexus.Core.Logging;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Services;

namespace Nexus.Shell.Pages;

/// <summary>
/// Banc d'essai : anime N cartes (cache bitmap, déplacement GPU) et mesure FPS, RAM, CPU.
/// Les cartes hors de la zone visible restent animées et comptées : c'est volontairement un pire cas.
/// Les valeurs sont celles du processus sur la machine qui exécute l'application.
/// </summary>
public sealed class BenchmarkPage : UserControl, INavPage
{
    private static readonly int[] Counts = { 24, 48, 96, 192 };
    private static readonly Brush CardBrush = MakeBrush();

    private readonly TextBlock _stats = Ui.Text("", 17);
    private readonly TextBlock _status = Ui.Text("", 14, dim: true);
    private readonly WrapPanel _field = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TextBlock _runLabel = null!, _countLabel = null!;

    private long _frames;
    private bool _running, _shown;
    private int _countIdx = 1;
    private double _fps, _fpsSum, _cpuPct, _wsMb, _privMb;
    private int _fpsN;
    private DateTime _lastTick;
    private TimeSpan _lastCpu;

    public string Title => "Performance";

    private readonly IShell _shell;

    public BenchmarkPage(IShell shell)
    {
        _shell = shell;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var head = new StackPanel();
        head.Children.Add(Ui.Text("Performance", 36, weight: FontWeights.Bold));
        _stats.Margin = new Thickness(0, 12, 0, 4);
        head.Children.Add(_stats);
        head.Children.Add(_status);
        root.Children.Add(head);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 8) };
        var run = Ui.Card("Animation", "Arrêtée", null, out _runLabel);
        run.Click += (_, _) => Toggle();
        var count = Ui.Card("Nombre de cartes", Counts[_countIdx].ToString(), null, out _countLabel);
        count.Click += (_, _) =>
        {
            _countIdx = (_countIdx + 1) % Counts.Length;
            _countLabel.Text = Counts[_countIdx].ToString();
            if (_running) Build();
        };
        var save = Ui.Card("Enregistrer la mesure", "Ajoute une ligne à benchmark.csv", Save, out _);
        buttons.Children.Add(run);
        buttons.Children.Add(count);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        var clip = new Border { ClipToBounds = true, Child = _field };
        Grid.SetRow(clip, 2);
        root.Children.Add(clip);

        Content = root;
        _timer.Tick += OnTick;
        Render();
    }

    private static Brush MakeBrush()
    {
        var b = new LinearGradientBrush(Color.FromRgb(0x2B, 0x3A, 0x67), Color.FromRgb(0x0E, 0x13, 0x22), 90);
        b.Freeze();
        return b;
    }

    // ---- Cycle de vie : rien ne tourne quand la page n'est pas affichée ----
    public void OnShown()
    {
        _shown = true;
        CompositionTarget.Rendering += OnRendering;
        _lastTick = DateTime.UtcNow;
        _lastCpu = CurrentCpu();
        _timer.Start();
    }

    public void OnHidden()
    {
        _shown = false;
        CompositionTarget.Rendering -= OnRendering;
        _timer.Stop();
        Stop();
    }

    public bool HandleAction(NavAction action) => false;

    private void OnRendering(object? sender, EventArgs e) => _frames++;

    private static TimeSpan CurrentCpu()
    {
        using var p = Process.GetCurrentProcess();
        return p.TotalProcessorTime;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_shown) return;
        var now = DateTime.UtcNow;
        var dt = Math.Max((now - _lastTick).TotalSeconds, 0.001);
        _lastTick = now;
        _fps = _frames / dt;
        _frames = 0;

        using var p = Process.GetCurrentProcess();
        var cpu = p.TotalProcessorTime;
        _cpuPct = (cpu - _lastCpu).TotalSeconds / dt / Environment.ProcessorCount * 100;
        _lastCpu = cpu;
        _wsMb = p.WorkingSet64 / 1048576.0;
        _privMb = p.PrivateMemorySize64 / 1048576.0;

        if (_running) { _fpsSum += _fps; _fpsN++; }
        Render();
    }

    private void Render()
    {
        var avg = _fpsN > 0 ? _fpsSum / _fpsN : 0;
        var dpi = VisualTreeHelper.GetDpi(this);
        var w = Window.GetWindow(this);
        var size = w is null ? "?" : $"{w.ActualWidth * dpi.DpiScaleX:0}×{w.ActualHeight * dpi.DpiScaleY:0} px ({dpi.DpiScaleX * 100:0} %)";
        _stats.Text =
            $"Fluidité : {_fps:0} FPS (moyenne depuis le démarrage de l'animation : {avg:0})\n" +
            $"Mémoire : {_wsMb:0} Mo (working set) · {_privMb:0} Mo (privée)\n" +
            $"CPU du processus : {_cpuPct:0.0} %\n" +
            $"Affichage de l'interface : {StartupInfo.ReadyMs:0} ms après le lancement\n" +
            $"Fenêtre : {size} · cartes : {Counts[_countIdx]}";
        if (_status.Text.Length == 0) _status.Text = "À l'arrêt, WPF ne redessine rien : 0 FPS est normal.";
    }

    // ---- Animation de test ----
    private void Toggle()
    {
        if (_running) Stop(); else Start();
    }

    private void Start()
    {
        _running = true;
        _fpsSum = 0; _fpsN = 0;
        _runLabel.Text = "En cours";
        Build();
    }

    private void Stop()
    {
        if (!_running && _field.Children.Count == 0) return;
        _running = false;
        _runLabel.Text = "Arrêtée";
        foreach (Border b in _field.Children)
            ((TranslateTransform)b.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null);
        _field.Children.Clear();
    }

    private void Build()
    {
        foreach (Border old in _field.Children)
            ((TranslateTransform)old.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null);
        _field.Children.Clear();

        for (var i = 0; i < Counts[_countIdx]; i++)
        {
            var tr = new TranslateTransform();
            var card = new Border
            {
                Width = 150, Height = 210, Margin = new Thickness(8),
                CornerRadius = new CornerRadius(14), Background = CardBrush,
                RenderTransform = tr, CacheMode = new BitmapCache()
            };
            tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(900 + (i % 7) * 120))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
            _field.Children.Add(card);
        }
    }

    // ---- Enregistrement des mesures ----
    private void Save()
    {
        try
        {
            var path = Path.Combine(AppPaths.Logs, "benchmark.csv");
            var dpi = VisualTreeHelper.GetDpi(this);
            var w = Window.GetWindow(this);
            var avg = _fpsN > 0 ? _fpsSum / _fpsN : 0;
            var inv = CultureInfo.InvariantCulture;
            var s = _shell.Settings.Current;

            var header = "horodatage,fenetre_px,dpi_pct,cartes,animation_active,fps_moyen,ram_ws_mo,ram_privee_mo,cpu_pct,demarrage_ms,niveau_animations,mode_tv";
            var line = string.Join(',',
                DateTime.Now.ToString("s", inv),
                $"{(w?.ActualWidth ?? 0) * dpi.DpiScaleX:0}x{(w?.ActualHeight ?? 0) * dpi.DpiScaleY:0}",
                (dpi.DpiScaleX * 100).ToString("0", inv),
                Counts[_countIdx].ToString(inv),
                _running ? "1" : "0",
                avg.ToString("0.0", inv), _wsMb.ToString("0", inv), _privMb.ToString("0", inv),
                _cpuPct.ToString("0.0", inv), StartupInfo.ReadyMs.ToString("0", inv),
                s.AnimationLevel.ToString(inv), s.TvMode ? "1" : "0");

            if (!File.Exists(path)) File.AppendAllText(path, header + Environment.NewLine);
            File.AppendAllText(path, line + Environment.NewLine);
            _status.Text = $"Mesure enregistrée dans {path}";
            Log.Info("Mesure de performance enregistrée : " + line);
        }
        catch (Exception ex)
        {
            Log.Error("Enregistrement de la mesure impossible.", ex);
            _status.Text = "Impossible d'enregistrer la mesure (voir les journaux).";
        }
    }
}
