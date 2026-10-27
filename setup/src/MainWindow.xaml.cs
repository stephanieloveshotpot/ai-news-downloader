using System;
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
            Title = isSetup ? "AI Pulse Setup" : "Remove AI Pulse";
            VersionText.Text = "Version " + Installer.Version;
            DirBox.Text = o.Dir;
            foreach (var p in new FrameworkElement[] { WelcomePage, ProgressPage, DonePage, ErrorPage, RemovePage })
                p.RenderTransform = new TranslateTransform();

            Card.SizeChanged += (s, e) => Card.Clip = new RectangleGeometry(new Rect(e.NewSize), 24, 24);
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

        // Drifting aurora, floating logo, the pulse line drawing itself, glowing.
        void StartAmbient()
        {
            Drift(B1, -120, -170, -40, -110, 9000);
            Drift(B2, 360, 180, 260, 120, 11000);
            Drift(B3, 420, -120, 340, -60, 8000);

            var floatAnim = new DoubleAnimation(0, -6, TimeSpan.FromMilliseconds(2600)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            LogoFloat.BeginAnimation(TranslateTransform.YProperty, floatAnim);

            var glow = new DoubleAnimation(0.45, 0.9, TimeSpan.FromMilliseconds(1600)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            LogoGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, glow);

            var draw = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            draw.KeyFrames.Add(new LinearDoubleKeyFrame(34, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            draw.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1100)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            draw.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000))));
            draw.KeyFrames.Add(new EasingDoubleKeyFrame(-34, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2800)), new CubicEase { EasingMode = EasingMode.EaseIn }));
            PulseLine.BeginAnimation(Shape.StrokeDashOffsetProperty, draw);

            var shimmer = new DoubleAnimation(-100, 620, TimeSpan.FromMilliseconds(1500)) { RepeatBehavior = RepeatBehavior.Forever };
            ShimmerX.BeginAnimation(TranslateTransform.XProperty, shimmer);
        }

        static void Drift(UIElement el, double x1, double y1, double x2, double y2, int ms)
        {
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            el.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(x1, x2, TimeSpan.FromMilliseconds(ms)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
            el.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(y1, y2, TimeSpan.FromMilliseconds(ms * 1.3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
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
            var to = open ? 560.0 : 440.0;
            var top = Top - (to - ActualHeight) / 2;
            BeginAnimation(HeightProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            BeginAnimation(TopProperty, new DoubleAnimation(top, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            if (open) Animate(OptionsPanel, OpacityProperty, 0, 1, 350);
        }

        void Browse()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Choose where to install AI Pulse", SelectedPath = DirBox.Text, ShowNewFolderButton = true })
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
            ProgressTitle.Text = opts.Update ? "Updating AI Pulse" : "Installing AI Pulse";
            StatusText.Text = opts.Update ? "Installing version " + Installer.Version + "…" : "Getting ready…";
            SetProgress(0, null, instant: true);
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
                ShowDone(opts.Update ? "Updated to " + Installer.Version : "AI Pulse is ready", opts.Update ? "Reopening AI Pulse…" : "Opening it now. Set it up in a few clicks.");
                if (!opts.Preview) Installer.LaunchApp(o, fresh: !opts.Update);
                busy = false;
                if (opts.SnapFile == null) { await Task.Delay(opts.Update ? 1200 : 1800); FadeAndClose(); }
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
            ProgressTitle.Text = "Removing AI Pulse";
            SetProgress(0, "Getting ready…", instant: true);
            Show(ProgressPage);
            var deleteData = DeleteDataBox.IsChecked == true;
            try
            {
                if (opts.Preview) await FakeWork(1);
                else await Task.Run(() => Installer.Uninstall(deleteData, Report));
                await Task.Delay(250);
                ShowDone("AI Pulse was removed", deleteData ? "Your settings were deleted too. Thanks for trying it." : "Your settings are kept in case you come back.");
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
            f = Math.Max(0, Math.Min(1, f));
            var track = Math.Max(1, ((FrameworkElement)BarFill.Parent).ActualWidth);
            var to = track * f;
            if (instant) { BarFill.BeginAnimation(WidthProperty, null); BarFill.Width = to; }
            else BarFill.BeginAnimation(WidthProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            PercentText.Text = Math.Round(f * 100) + "%";
            if (status != null) StatusText.Text = status;
        }

        void ShowDone(string title, string text)
        {
            DoneTitle.Text = title;
            DoneText.Text = text;
            Show(DonePage);
            CheckRing.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(600)) { BeginTime = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            CheckTick.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(350)) { BeginTime = TimeSpan.FromMilliseconds(650), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            AnimateScale(LogoScale, 1, 1.08, 260, new SineEase(), 0, autoReverse: true);
        }

        void ShowError(string message)
        {
            ErrorText.Text = message;
            Show(ErrorPage);
        }

        static string Friendly(Exception ex)
        {
            if (ex is UnauthorizedAccessException || ex is IOException)
                return "A file is in use or can't be written. Close AI Pulse if it's open and try again.\n(" + ex.Message + ")";
            return ex.Message;
        }

        async Task FakeWork(double upTo)
        {
            string[] files = { "AI Pulse.exe", "resources.pak", "app.asar", "aipulse-scraper.exe", "icudtl.dat", "locales" };
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
