using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AIPulseSetup
{
    public partial class MainWindow : Window
    {
        readonly Options opts;
        readonly bool isSetup;
        bool busy;
        FrameworkElement current;
        CancellationTokenSource cts;

        public MainWindow(Options o, bool setupMode)
        {
            opts = o;
            isSetup = setupMode;
            InitializeComponent();
            Title = isSetup ? "Quillwave Setup" : "Remove Quillwave";
            VersionText.Text = "Version " + Installer.Version;
            DirBox.Text = o.Dir;
            foreach (var p in new FrameworkElement[] { WelcomePage, ProgressPage, DonePage, ErrorPage, RemovePage })
                p.RenderTransform = new TranslateTransform();

            Card.SizeChanged += (s, e) => Card.Clip = new RectangleGeometry(new Rect(e.NewSize), 28, 28);
            TitleBar.MouseLeftButtonDown += (s, e) => DragMove();
            Card.MouseLeftButtonDown += (s, e) => { if (!(e.OriginalSource is TextBox)) { try { DragMove(); } catch { } } };
            MinBtn.Click += (s, e) => WindowState = WindowState.Minimized;
            CloseBtn.Click += (s, e) => { if (!busy) FadeAndClose(); };
            KeepBtn.Click += (s, e) => FadeAndClose();
            ErrorCloseBtn.Click += (s, e) => FadeAndClose();
            InstallBtn.Click += (s, e) => StartInstall();
            RetryBtn.Click += (s, e) => { if (isSetup) StartInstall(); else StartRemove(); };
            RemoveBtn.Click += (s, e) => StartRemove();
            OptionsBtn.Click += (s, e) => ToggleOptions();
            BrowseBtn.Click += (s, e) => Browse();
            Loaded += OnLoaded;
        }

        // ---------------- start-up ----------------

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            Animate(Shell, OpacityProperty, 0, 1, 380);
            AnimateScale(ShellScale, 0.94, 1, 520, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 });
            AnimateScale(LogoScale, 0.4, 1, 900, new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 5 }, 120);
            StartAmbient();

            var page = opts.SnapPage;
            if (!isSetup || page == "remove") { Footnote.Text = "Your settings stay unless you tick the box."; Show(RemovePage); }
            else if (opts.Update) StartInstall();
            else Show(WelcomePage);

            if (page == "options") ToggleOptions();
            if (page == "progress" || page == "done") StartInstall();
            if (page == "error") ShowError("The download was interrupted. Check your connection and try again.");
            if (opts.SnapFile != null)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(opts.SnapAt) };
                t.Tick += (s, a) => { t.Stop(); Snap(opts.SnapFile); Application.Current.Shutdown(); };
                t.Start();
            }
        }

        // Calm on purpose: the background and logo stay still, the only motion
        // is one very slow turn of the outer ring and the progress itself.
        void StartAmbient()
        {
            Spin(OrbitARot, 360, 90000);
            LogoGlow.Opacity = 0.55;
            clock.Start();
            CompositionTarget.Rendering += OnFrame;
            Closed += (s, e) => CompositionTarget.Rendering -= OnFrame;
        }

        static void Drift(UIElement el, double x1, double y1, double x2, double y2, int ms)
        {
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            el.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(x1, x2, TimeSpan.FromMilliseconds(ms)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
            el.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(y1, y2, TimeSpan.FromMilliseconds(ms * 1.3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
        }

        static void Spin(RotateTransform r, double by, int ms) =>
            r.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, by, TimeSpan.FromMilliseconds(ms)) { RepeatBehavior = RepeatBehavior.Forever });

        // ---------------- particles, the progress ring and the burst ----------------

        class Mote { public Shape El; public double X, Y, Vx, Vy, Life, Age, Phase, Base, Spin, Angle; }

        static readonly Random rng = new Random();
        static readonly Color[] Palette =
        {
            Color.FromRgb(0x3D, 0x5A, 0xFE), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xF0, 0x6F, 0xA4),
            Color.FromRgb(0xFF, 0x9A, 0x55), Color.FromRgb(0x22, 0xD3, 0xEE), Colors.White,
        };
        readonly List<Mote> stars = new List<Mote>();
        readonly List<Mote> sparks = new List<Mote>();
        readonly Stopwatch clock = new Stopwatch();
        double lastT, shown, target;

        const double W = 680, H = 460;                          // the card
        static readonly Point LogoCentre = new Point(W / 2, 32 + 85);

        void SeedStars()
        {
            for (int i = 0; i < 70; i++)
            {
                var size = 0.8 + rng.NextDouble() * 2.2;
                var c = rng.NextDouble() < 0.7 ? Colors.White : Palette[rng.Next(Palette.Length - 1)];
                var m = new Mote
                {
                    El = new Ellipse { Width = size, Height = size, Fill = new SolidColorBrush(c) },
                    X = rng.NextDouble() * W, Y = rng.NextDouble() * H,
                    Vx = (rng.NextDouble() - 0.5) * 4, Vy = -(3 + rng.NextDouble() * 9) * (size / 2),
                    Phase = rng.NextDouble() * Math.PI * 2, Base = 0.25 + rng.NextDouble() * 0.6, Spin = 0.8 + rng.NextDouble() * 2.2,
                };
                Canvas.SetLeft(m.El, m.X); Canvas.SetTop(m.El, m.Y);
                Stars.Children.Add(m.El);
                stars.Add(m);
            }
        }

        void OnFrame(object sender, EventArgs e)
        {
            var t = clock.Elapsed.TotalSeconds;
            var dt = Math.Min(0.05, t - lastT);
            lastT = t;

            foreach (var m in stars)
            {
                m.X += m.Vx * dt; m.Y += m.Vy * dt;
                if (m.Y < -4) { m.Y = H + 4; m.X = rng.NextDouble() * W; }
                if (m.X < -4) m.X = W + 4; else if (m.X > W + 4) m.X = -4;
                m.El.Opacity = m.Base * (0.55 + 0.45 * Math.Sin(t * m.Spin + m.Phase));
                Canvas.SetLeft(m.El, m.X); Canvas.SetTop(m.El, m.Y);
            }

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var m = sparks[i];
                m.Age += dt;
                if (m.Age >= m.Life) { Burst.Children.Remove(m.El); sparks.RemoveAt(i); continue; }
                m.Vy += 260 * dt; m.Vx *= 1 - 1.2 * dt; m.Vy *= 1 - 0.6 * dt;
                m.X += m.Vx * dt; m.Y += m.Vy * dt; m.Angle += m.Spin * dt;
                m.El.Opacity = 1 - Math.Pow(m.Age / m.Life, 2);
                ((RotateTransform)m.El.RenderTransform).Angle = m.Angle;
                Canvas.SetLeft(m.El, m.X); Canvas.SetTop(m.El, m.Y);
            }

            // ease the ring toward the real progress so it glides between updates
            if (Math.Abs(target - shown) > 0.0005)
            {
                shown += (target - shown) * Math.Min(1, dt * 9);
                if (Math.Abs(target - shown) <= 0.0005) shown = target;
                DrawArc(shown);
            }
        }

        void DrawArc(double f)
        {
            PercentText.Text = Math.Round(f * 100).ToString();
            const double r = 75, c = 85;
            if (f <= 0.001) { ProgressArc.Data = null; return; }
            if (f >= 0.999) { ProgressArc.Data = new EllipseGeometry(new Point(c, c), r, r); return; }
            var a = f * 2 * Math.PI;
            var fig = new PathFigure { StartPoint = new Point(c, c - r) };
            fig.Segments.Add(new ArcSegment(new Point(c + r * Math.Sin(a), c - r * Math.Cos(a)), new Size(r, r), 0, f > 0.5, SweepDirection.Clockwise, true));
            ProgressArc.Data = new PathGeometry(new[] { fig });
        }

        // Done: one soft ring of light from behind the logo.
        void Celebrate()
        {
            var sc = new ScaleTransform(1, 1);
            var wave = new Ellipse { Width = 100, Height = 100, StrokeThickness = 3, Stroke = (Brush)FindResource("Grad"), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = sc };
            Canvas.SetLeft(wave, LogoCentre.X - 50); Canvas.SetTop(wave, LogoCentre.Y - 50);
            Burst.Children.Add(wave);
            AnimateScale(sc, 1, 2.6, 1100, new CubicEase { EasingMode = EasingMode.EaseOut });
            var fade = new DoubleAnimation(0.6, 0, TimeSpan.FromMilliseconds(1100));
            fade.Completed += (s, e) => Burst.Children.Remove(wave);
            wave.BeginAnimation(OpacityProperty, fade);
        }

        // "What's inside" lines that rotate while it installs.
        static readonly string[] Features =
        {
            "Scanning nearly 100 sources, live",
            "Instant AI briefs on any story",
            "Key points in one click",
            "A For you feed that learns your topics",
            "Full articles, without the clutter",
            "Desktop alerts for the big stories",
        };
        DispatcherTimer ticker;
        int feature;

        void StartTicker(string only = null)
        {
            ticker?.Stop();
            feature = 0;
            FeatureText.Text = only ?? Features[0];
            if (only != null) return;
            ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
            ticker.Tick += (s, e) =>
            {
                var o = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                o.Completed += (a, b) =>
                {
                    feature = (feature + 1) % Features.Length;
                    FeatureText.Text = Features[feature];
                    FeatureText.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
                };
                FeatureText.BeginAnimation(OpacityProperty, o);
            };
            ticker.Start();
        }

        // ---------------- pages ----------------

        void Show(FrameworkElement page)
        {
            var old = current;
            current = page;
            if (old != null && old != page)
            {
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(170));
                fade.Completed += (s, e) => { if (current != old) old.Visibility = Visibility.Collapsed; };
                old.BeginAnimation(OpacityProperty, fade);
                ((TranslateTransform)old.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -10, TimeSpan.FromMilliseconds(170)));
            }
            page.Visibility = Visibility.Visible;
            var delay = old != null && old != page ? 150 : 0;
            var inFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(delay) };
            page.BeginAnimation(OpacityProperty, inFade);
            ((TranslateTransform)page.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        void ToggleOptions()
        {
            var open = OptionsPanel.Visibility != Visibility.Visible;
            OptionsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            OptionsBtn.Content = open ? "Hide options" : "Install options";
            // grow the window to make room for the panel, keeping it centred
            var to = open ? 646.0 : 500.0;
            var top = Top - (to - ActualHeight) / 2;
            BeginAnimation(HeightProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            BeginAnimation(TopProperty, new DoubleAnimation(top, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            if (open) Animate(OptionsPanel, OpacityProperty, 0, 1, 350);
        }

        void Browse()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Choose where to install Quillwave", SelectedPath = DirBox.Text, ShowNewFolderButton = true })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    DirBox.Text = dlg.SelectedPath.EndsWith(Installer.AppName, StringComparison.OrdinalIgnoreCase) ? dlg.SelectedPath : System.IO.Path.Combine(dlg.SelectedPath, Installer.AppName);
            }
        }

        // ---------------- install / remove ----------------

        async void StartInstall()
        {
            if (busy) return;
            busy = true;
            CloseBtn.IsEnabled = false;
            Footnote.Text = "";
            ProgressTitle.Text = opts.Update ? "Updating Quillwave" : "Installing Quillwave";
            StatusText.Text = opts.Update ? "Installing version " + Installer.Version + "…" : "Getting ready…";
            SetProgress(0, null, instant: true);
            ShowRing(true);
            StartTicker();
            Show(ProgressPage);

            var o = new Options { Dir = DirBox.Text.Trim(), DesktopShortcut = DesktopBox.IsChecked == true, Update = opts.Update, Relaunch = opts.Relaunch };
            cts = new CancellationTokenSource();
            try
            {
                if (opts.Preview)
                {
                    await FakeWork(opts.SnapPage == "progress" ? 0.64 : 1);
                    if (opts.SnapPage == "progress") return;   // hold here for the screenshot
                }
                else await Task.Run(() => Installer.Install(o, Report, cts.Token));
                await Task.Delay(250);
                ShowDone(opts.Update ? "Updated to " + Installer.Version : "Quillwave is ready", opts.Update ? "Reopening Quillwave…" : "Opening it now. Set it up in a few clicks.");
                if (!opts.Preview) Installer.LaunchApp(o, fresh: !opts.Update);
                busy = false;
                if (opts.SnapFile == null) { await Task.Delay(opts.Update ? 1400 : 2200); FadeAndClose(); }
            }
            catch (Exception ex)
            {
                busy = false;
                CloseBtn.IsEnabled = true;
                ShowError(Friendly(ex));
            }
        }

        async void StartRemove()
        {
            if (busy) return;
            busy = true;
            CloseBtn.IsEnabled = false;
            Footnote.Text = "";
            ProgressTitle.Text = "Removing Quillwave";
            SetProgress(0, "Getting ready…", instant: true);
            ShowRing(true);
            StartTicker("Cleaning up shortcuts and files");
            Show(ProgressPage);
            var deleteData = DeleteDataBox.IsChecked == true;
            try
            {
                if (opts.Preview) await FakeWork(1);
                else await Task.Run(() => Installer.Uninstall(deleteData, Report));
                await Task.Delay(250);
                ShowDone("Quillwave was removed", deleteData ? "Your settings were deleted too. Thanks for trying it." : "Your settings are kept in case you come back.", celebrate: false);
                busy = false;
                if (opts.SnapFile == null)
                {
                    await Task.Delay(1900);
                    if (!opts.Preview) Installer.ScheduleSelfDelete();
                    FadeAndClose();
                }
            }
            catch (Exception ex)
            {
                busy = false;
                CloseBtn.IsEnabled = true;
                ShowError(Friendly(ex));
            }
        }

        // Called from the worker thread.
        void Report(double fraction, string status) => Dispatcher.BeginInvoke(new Action(() => SetProgress(fraction, status)));

        void SetProgress(double f, string status, bool instant = false)
        {
            target = Math.Max(0, Math.Min(1, f));
            if (instant) { shown = target; DrawArc(shown); }
            if (status != null) StatusText.Text = status;
        }

        void ShowRing(bool on)
        {
            Animate(ArcTrack, OpacityProperty, ArcTrack.Opacity, on ? 1 : 0, 300);
            Animate(OrbitA, OpacityProperty, OrbitA.Opacity, on ? 0.25 : 0.55, 400);
        }

        void ShowDone(string title, string text, bool celebrate = true)
        {
            ticker?.Stop();
            target = 1;
            DoneTitle.Text = title;
            DoneText.Text = text;
            Show(DonePage);
            if (celebrate) Celebrate();
            ArcTrack.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(400) });
            AnimateScale(LogoScale, 1, 1.12, 280, new SineEase(), 0, autoReverse: true);
        }

        void ShowError(string message)
        {
            ticker?.Stop();
            ShowRing(false);
            target = 0;
            ErrorText.Text = message;
            Show(ErrorPage);
        }

        static string Friendly(Exception ex)
        {
            if (ex is UnauthorizedAccessException || ex is IOException)
                return "A file is in use or can't be written. Close Quillwave if it's open and try again.\n(" + ex.Message + ")";
            return ex.Message;
        }

        async Task FakeWork(double upTo)
        {
            string[] files = { "Quillwave.exe", "resources.pak", "app.asar", "aipulse-scraper.exe", "icudtl.dat", "locales" };
            for (int i = 0; i <= 100 * upTo; i += 2)
            {
                SetProgress(i / 100.0, i < 88 ? "Unpacking " + files[(i / 15) % files.Length] : i < 95 ? "Adding shortcuts…" : "Registering with Windows…");
                await Task.Delay(35);
            }
        }

        // ---------------- helpers ----------------

        void FadeAndClose()
        {
            var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            a.Completed += (s, e) => Close();
            Shell.BeginAnimation(OpacityProperty, a);
            AnimateScale(ShellScale, 1, 0.96, 220, new CubicEase { EasingMode = EasingMode.EaseIn });
        }

        static void Animate(UIElement el, DependencyProperty p, double from, double to, int ms) =>
            el.BeginAnimation(p, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

        static void AnimateScale(ScaleTransform s, double from, double to, int ms, IEasingFunction ease, int delay = 0, bool autoReverse = false)
        {
            var a = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(delay), AutoReverse = autoReverse };
            s.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        // --snap: render this window to a PNG (for screenshots without screen capture).
        void Snap(string file)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
            var h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
            var rtb = new RenderTargetBitmap(w, h, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            rtb.Render((Visual)Content);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var f = File.Create(file)) enc.Save(f);
        }
    }
}
