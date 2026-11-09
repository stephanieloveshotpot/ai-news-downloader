using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AIPulseSetup
{
    // One window for installing, updating and uninstalling.
    // Every screen has the same shape: a header that says where you are, a
    // body that says plainly what will happen (or is happening), and a footer
    // with the buttons always in the same place. Motion is limited to the
    // progress bar and short cross-fades.
    public partial class MainWindow : Window
    {
        const string HelpMail = "quillwaveofficial@gmail.com";
        readonly Options opts;
        readonly bool isSetup;
        bool busy;
        FrameworkElement current;
        Action onPrimary, onSecondary;
        string[] steps = new string[0];
        int step;
        string[] notes;

        public MainWindow(Options o, bool setupMode)
        {
            opts = o;
            isSetup = setupMode;
            if (o.Preview && (o.SnapPage == "update" || o.SnapPage == "update-done")) o.Update = true;
            InitializeComponent();
            Title = isSetup ? "Quillwave Setup" : "Uninstall Quillwave";
            WindowTitle.Text = isSetup ? (o.Update ? "Quillwave Update" : "Quillwave Setup") : "Uninstall Quillwave";
            DirBox.Text = o.Dir;
            DirText.Text = o.Dir;
            notes = Installer.WhatsNew();
            if (notes.Length == 0 && o.Preview) notes = new[] { "Comment on your highlights and keep them with your notes.", "Save any assistant answer to the story's notes.", "Past chats: reopen earlier conversations with the assistant.", "Share cards come in Night, Paper and Aurora." };

            Card.SizeChanged += (s, e) => Card.Clip = new RectangleGeometry(new Rect(e.NewSize), 12, 12);
            TitleBar.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            MinBtn.Click += (s, e) => WindowState = WindowState.Minimized;
            CloseBtn.Click += (s, e) => { if (!busy) FadeAndClose(); };
            PrimaryBtn.Click += (s, e) => onPrimary?.Invoke();
            SecondaryBtn.Click += (s, e) => onSecondary?.Invoke();
            ChangeBtn.Click += (s, e) => ToggleDir();
            BrowseBtn.Click += (s, e) => Browse();
            HelpBtn.Click += (s, e) => OpenMail(isSetup ? "Quillwave setup problem" : "Why I uninstalled Quillwave");
            DeleteDataBox.Checked += (s, e) => PaintDataChoice();
            DeleteDataBox.Unchecked += (s, e) => PaintDataChoice();
            KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape && !busy) FadeAndClose(); };
            Loaded += OnLoaded;
        }

        // ---------------- start-up ----------------

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            Shell.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            var page = opts.SnapPage;
            if (!isSetup || page == "remove" || page == "remove-done") ShowRemove();
            else if (opts.Update && !opts.Preview) StartInstall();
            else ShowWelcome();

            if (page == "progress" || page == "done" || page == "update" || page == "update-done") StartInstall();
            if (page == "remove-done") StartRemove();
            if (page == "error") ShowError("A file Quillwave needs is in use. Close Quillwave if it's open, then try again.");
            if (opts.SnapFile != null)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(opts.SnapAt) };
                t.Tick += (s, a) => { t.Stop(); Snap(opts.SnapFile); Application.Current.Shutdown(); };
                t.Start();
            }
        }

        // ---------------- the screens ----------------

        void ShowWelcome()
        {
            var reinstall = Installer.IsInstalled && !opts.Preview;
            var mb = Mb(Installer.PayloadBytes);
            SetHeader(reinstall ? "Reinstall Quillwave" : "Install Quillwave",
                reinstall ? "Version " + Installer.Version + ". Your settings, notes and account stay as they are." : "Version " + Installer.Version + ". All your AI news in one sitting.", null);
            SizeTitle.Text = mb > 0 ? $"About {mb} MB, no admin rights needed" : "No admin rights needed";
            if (reinstall) { AfterTitle.Text = "Then Quillwave opens"; AfterText.Text = "Everything you had is still there."; }
            Foot("Installs for " + Environment.UserName + " only");
            Buttons("Install", StartInstall, "Cancel", FadeAndClose);
            Show(WelcomePage);
        }

        void ShowRemove()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var mb = Mb(Installer.InstalledBytes(dir));
            SetHeader("Uninstall Quillwave", "Version " + Installer.Version + (opts.Preview ? "" : " · " + dir), null);
            RemoveApp.Text = mb > 0 ? $"• The app ({mb} MB)" : "• The app and its files";
            Foot("Nothing is sent anywhere when you uninstall");
            Buttons("Uninstall", StartRemove, "Cancel", FadeAndClose, danger: true);
            PaintDataChoice();
            Show(RemovePage);
        }

        void PaintDataChoice()
        {
            var on = DeleteDataBox.IsChecked == true;
            StaysLocal.TextDecorations = on ? TextDecorations.Strikethrough : null;
            StaysLocal.Opacity = on ? 0.45 : 1;
        }

        async void StartInstall()
        {
            if (busy) return;
            busy = true;
            CloseBtn.IsEnabled = false;
            var update = opts.Update;
            if (update)
            {
                SetHeader("Updating to " + Installer.Version, "Quillwave closes for a moment and reopens by itself.", null);
                steps = new[] { "Close Quillwave", "Unpack the new version", "Swap in the new files", "Reopen Quillwave" };
            }
            else
            {
                SetHeader("Installing Quillwave", "This takes less than a minute.", null);
                steps = new[] { "Unpack the app", "Add shortcuts", "Register with Windows" };
            }
            ShowNotes(update);
            Foot(update ? "Your settings and notes are kept" : "Installs for " + Environment.UserName + " only");
            Buttons(null, null, null, null);
            SetProgress(0, "Getting ready…");
            Show(ProgressPage);

            var o = new Options { Dir = DirBox.Text.Trim(), DesktopShortcut = DesktopBox.IsChecked == true, Update = update, Relaunch = opts.Relaunch };
            try
            {
                if (opts.Preview)
                {
                    var hold = opts.SnapPage == "progress" || opts.SnapPage == "update";
                    await FakeWork(hold ? 0.62 : 1);
                    if (hold) return;
                }
                else await Task.Run(() => Installer.Install(o, Report, CancellationToken.None));
                busy = false;
                CloseBtn.IsEnabled = true;
                if (update)
                {
                    step = steps.Length;
                    RenderSteps();
                    if (!opts.Preview) Installer.LaunchApp(o, fresh: false);
                    SetHeader("Updated to " + Installer.Version, "Quillwave is reopening now.", true);
                    DoneText.Text = "You're on the newest version. Here's what changed:";
                    LaunchBox.Visibility = Visibility.Collapsed;
                    FillNotes(DoneNotesList, DoneNotesTitle);
                    DoneNotes.Visibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                    Buttons("Close", FadeAndClose, null, null);
                    Show(DonePage);
                    if (opts.SnapFile == null) { await Task.Delay(notes.Length > 0 ? 6000 : 1800); if (current == DonePage) FadeAndClose(); }
                }
                else
                {
                    SetHeader("Quillwave is installed", "Version " + Installer.Version, true);
                    DoneText.Text = "You'll find it in the Start menu" + (o.DesktopShortcut ? " and on your desktop" : "") + ". When it opens, a short setup picks your topics and asks you to accept the terms.";
                    LaunchBox.Visibility = Visibility.Visible;
                    DoneNotes.Visibility = Visibility.Collapsed;
                    Foot("");
                    Buttons("Finish", () => { if (LaunchBox.IsChecked == true && !opts.Preview) Installer.LaunchApp(o, fresh: true); FadeAndClose(); }, null, null);
                    Show(DonePage);
                }
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
            var deleteData = DeleteDataBox.IsChecked == true;
            SetHeader("Uninstalling Quillwave", "This only takes a few seconds.", null);
            steps = deleteData
                ? new[] { "Close Quillwave", "Remove shortcuts and Windows entries", "Remove the app", "Delete your data on this PC" }
                : new[] { "Close Quillwave", "Remove shortcuts and Windows entries", "Remove the app" };
            ShowNotes(false);
            Foot("");
            Buttons(null, null, null, null);
            SetProgress(0, "Getting ready…");
            Show(ProgressPage);
            try
            {
                if (opts.Preview) await FakeWork(1);
                else await Task.Run(() => Installer.Uninstall(deleteData, Report));
                busy = false;
                CloseBtn.IsEnabled = true;
                SetHeader("Quillwave is uninstalled", "Thanks for giving it a try.", true);
                DoneText.Text = deleteData
                    ? "Your data on this PC was deleted too. Your account and synced notes are still there if you ever come back."
                    : "Your settings, bookmarks and offline notes are still on this PC, so everything will be waiting if you reinstall.";
                LaunchBox.Visibility = Visibility.Collapsed;
                DoneNotes.Visibility = Visibility.Collapsed;
                Foot("Got a minute?");
                HelpBtn.Content = "Tell us why you left";
                HelpBtn.Visibility = Visibility.Visible;
                Buttons("Close", () => { if (!opts.Preview) Installer.ScheduleSelfDelete(); FadeAndClose(); }, null, null);
                Show(DonePage);
            }
            catch (Exception ex)
            {
                busy = false;
                CloseBtn.IsEnabled = true;
                ShowError(Friendly(ex));
            }
        }

        void ShowError(string message)
        {
            var what = !isSetup ? "uninstalling" : opts.Update ? "updating" : "installing";
            SetHeader("Couldn't finish " + what, "Quillwave Setup", false);
            ErrorText.Text = message;
            Foot("");
            HelpBtn.Content = "Get help";
            HelpBtn.Visibility = Visibility.Visible;
            Buttons("Try again", () => { HelpBtn.Visibility = Visibility.Collapsed; if (isSetup) StartInstall(); else StartRemove(); }, "Close", FadeAndClose);
            Show(ErrorPage);
        }

        // ---------------- pieces ----------------

        void SetHeader(string title, string sub, bool? ok)
        {
            HeadTitle.Text = title;
            HeadSub.Text = sub ?? "";
            if (ok == null) { Logo.Visibility = Visibility.Visible; StateBadge.Visibility = Visibility.Collapsed; return; }
            Logo.Visibility = Visibility.Collapsed;
            StateBadge.Visibility = Visibility.Visible;
            StateBadge.Background = (Brush)FindResource(ok.Value ? "Good" : "DangerBrush");
            StateIcon.Data = Geometry.Parse(ok.Value ? "M5,12 L10,17 L19,7" : "M12,5 L12,13 M12,18 L12,18.2");
        }

        void Foot(string text) { FootText.Text = text ?? ""; if (!string.IsNullOrEmpty(text) || current == null) HelpBtn.Visibility = Visibility.Collapsed; }

        void Buttons(string primary, Action p, string secondary, Action s, bool danger = false)
        {
            PrimaryBtn.Visibility = primary == null ? Visibility.Collapsed : Visibility.Visible;
            SecondaryBtn.Visibility = secondary == null ? Visibility.Collapsed : Visibility.Visible;
            if (primary != null) PrimaryBtn.Content = primary;
            if (secondary != null) SecondaryBtn.Content = secondary;
            PrimaryBtn.Style = (Style)FindResource(danger ? "Danger" : "Primary");
            onPrimary = p; onSecondary = s;
            if (primary != null) Dispatcher.BeginInvoke(new Action(() => PrimaryBtn.Focus()), DispatcherPriority.Input);
        }

        void ShowNotes(bool on)
        {
            var show = on && notes.Length > 0;
            NotesCol.Width = show ? new GridLength(1.05, GridUnitType.Star) : new GridLength(0);
            NotesPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show) FillNotes(NotesList, NotesTitle, brief: true);
        }

        // short: first sentence of each, for the narrow panel beside the progress
        void FillNotes(ItemsControl list, TextBlock title, bool brief = false)
        {
            title.Text = "WHAT'S NEW IN " + Installer.Version;
            list.Items.Clear();
            var max = brief ? 4 : 5;
            foreach (var raw in notes.Take(max))
            {
                var n = raw;
                if (brief) { var cut = n.IndexOfAny(new[] { '.', ':' }); if (cut > 20 && cut < n.Length - 1) n = n.Substring(0, cut) + "."; }
                var g = new Grid { Margin = new Thickness(0, 0, 0, 9) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 0, 0) });
                var t = new TextBlock { Text = n, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 18 };
                Grid.SetColumn(t, 1);
                g.Children.Add(t);
                list.Items.Add(g);
            }
            if (notes.Length > max)
                list.Items.Add(new TextBlock { Text = $"+{notes.Length - max} more in Settings → About", FontSize = 12, Foreground = (Brush)FindResource("Faint"), Margin = new Thickness(14, 2, 0, 0) });
        }

        // the checklist under the progress bar
        void RenderSteps()
        {
            StepList.Items.Clear();
            for (int i = 0; i < steps.Length; i++)
            {
                var done = i < step; var now = i == step;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 11) };
                var mark = new Grid { Width = 18, Height = 18, Margin = new Thickness(0, 0, 11, 0) };
                if (done)
                {
                    mark.Children.Add(new Ellipse { Fill = (Brush)FindResource("Good") });
                    mark.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M5,9.5 L8,12.5 L13,6.5"), Stroke = Brushes.White, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
                }
                else if (now)
                {
                    mark.Children.Add(new Ellipse { Stroke = (Brush)FindResource("Accent"), StrokeThickness = 1.6 });
                    mark.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = (Brush)FindResource("Accent") });
                }
                else mark.Children.Add(new Ellipse { Stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x4E, 0x5E)), StrokeThickness = 1.4 });
                row.Children.Add(mark);
                row.Children.Add(new TextBlock
                {
                    Text = steps[i], FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource(done || now ? "TextBright" : "Faint"),
                    FontWeight = now ? FontWeights.SemiBold : FontWeights.Normal,
                });
                StepList.Items.Add(row);
            }
        }

        void Show(FrameworkElement page)
        {
            var old = current;
            current = page;
            foreach (var p in new FrameworkElement[] { WelcomePage, ProgressPage, DonePage, ErrorPage, RemovePage })
                if (p != page) p.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
            if (old != page) page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }

        void ToggleDir()
        {
            var editing = DirBox.Visibility != Visibility.Visible;
            DirBox.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            BrowseBtn.Visibility = DirBox.Visibility;
            DirText.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
            ChangeBtn.Content = editing ? "Done" : "Change";
            if (editing) { DirBox.Focus(); DirBox.SelectAll(); }
            else DirText.Text = DirBox.Text.Trim();
        }

        void Browse()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Choose where to install Quillwave", SelectedPath = DirBox.Text, ShowNewFolderButton = true })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    DirBox.Text = dlg.SelectedPath.EndsWith(Installer.AppName, StringComparison.OrdinalIgnoreCase) ? dlg.SelectedPath : System.IO.Path.Combine(dlg.SelectedPath, Installer.AppName);
                    DirText.Text = DirBox.Text;
                }
            }
        }

        // Called from the worker thread.
        void Report(double fraction, string status) => Dispatcher.BeginInvoke(new Action(() => SetProgress(fraction, status)));

        void SetProgress(double f, string status)
        {
            f = Math.Max(0, Math.Min(1, f));
            PercentText.Text = Math.Round(f * 100) + "%";
            var full = Math.Max(0, ((FrameworkElement)BarFill.Parent).ActualWidth);
            BarFill.BeginAnimation(WidthProperty, new DoubleAnimation(full * f, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            if (!string.IsNullOrEmpty(status) && status != "Done") StatusText.Text = status;
            // which step we're on, from how far along it is
            int s;
            if (!isSetup) s = f < 0.25 ? 0 : f < 0.45 ? 1 : f < 0.9 || steps.Length < 4 ? 2 : 3;
            else if (opts.Update) s = f < 0.03 ? 0 : f < 0.86 ? 1 : 2;
            else s = f < 0.9 ? 0 : f < 0.96 ? 1 : 2;
            if (f >= 1 && (isSetup && !opts.Update || !isSetup)) s = steps.Length;
            if (s != step || StepList.Items.Count == 0) { step = s; RenderSteps(); }
        }

        static string Friendly(Exception ex)
        {
            if (ex is UnauthorizedAccessException || ex is IOException)
                return "A file Quillwave needs is in use or can't be written. Close Quillwave if it's open, then try again.\n\nDetails: " + ex.Message;
            return ex.Message;
        }

        static int Mb(long bytes) => bytes <= 0 ? 0 : (int)Math.Round(bytes / 1048576.0);

        static void OpenMail(string subject)
        {
            try { Process.Start(new ProcessStartInfo("mailto:" + HelpMail + "?subject=" + Uri.EscapeDataString(subject)) { UseShellExecute = true }); } catch { }
        }

        async Task FakeWork(double upTo)
        {
            string[] files = { "Quillwave.exe", "resources.pak", "app.asar", "quillwave-scanner.exe", "icudtl.dat" };
            for (int i = 0; i <= 100 * upTo; i += 2)
            {
                SetProgress(i / 100.0, i < 86 ? "Unpacking " + files[(i / 17) % files.Length] : i < 93 ? "Adding shortcuts…" : "Registering with Windows…");
                await Task.Delay(30);
            }
        }

        void FadeAndClose()
        {
            var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140));
            a.Completed += (s, e) => Close();
            Shell.BeginAnimation(OpacityProperty, a);
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
