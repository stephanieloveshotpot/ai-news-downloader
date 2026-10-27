// Everything the installer and uninstaller actually do to the PC.
// Per-user only: %LOCALAPPDATA%\Programs\AI Pulse, HKCU, the user's Start
// menu and desktop. Nothing here needs administrator rights.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace AIPulseSetup
{
    public sealed class Options
    {
        public bool Silent, Update, Relaunch, Preview;
        public bool ExtractOnly;   // test switch: unpack into Dir and nothing else
        public string Dir;
        public bool DesktopShortcut = true;
        public string SnapFile, SnapPage;
        public int SnapAt = 1500;

        public static Options Parse(string[] args)
        {
            var o = new Options { Dir = Installer.DefaultDir() };
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                var low = a.ToLowerInvariant();
                if (low == "/s" || low == "--silent") o.Silent = true;
                else if (low == "/update" || low == "--update") o.Update = true;
                else if (low == "/relaunch" || low == "--relaunch") o.Relaunch = true;
                else if (low == "--preview") o.Preview = true;
                else if (low == "--extract-only") o.ExtractOnly = true;
                else if (low.StartsWith("/d=")) o.Dir = string.Join(" ", args.Skip(i).ToArray()).Substring(3).Trim('"');   // NSIS-style: rest of the line
                else if (low == "--snap" && i + 1 < args.Length) o.SnapFile = args[++i];
                else if (low == "--snap-at" && i + 1 < args.Length) int.TryParse(args[++i], out o.SnapAt);
                else if (low == "--page" && i + 1 < args.Length) o.SnapPage = args[++i];
                if (low.StartsWith("/d=")) break;
            }
            return o;
        }
    }

    public static class Installer
    {
        public const string AppName = "AI Pulse";
        public const string AppExe = "AI Pulse.exe";
        public const string UninstallerExe = "Uninstall AI Pulse.exe";
        const string AppId = "AIPulse";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppId;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Publisher = "Shako";
        const string Url = "https://github.com/stephanieloveshotpot/ai-news-desk";

        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
                var plus = v.IndexOf('+');
                return plus > 0 ? v.Substring(0, plus) : v;
            }
        }

        public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ai-pulse");
        static string StartMenuDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);
        static string DesktopLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");

        public static string DefaultDir()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\" + AppId))
            {
                var saved = k?.GetValue("InstallDir") as string;
                if (!string.IsNullOrWhiteSpace(saved)) return saved;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
        }

        public static bool HasPayload => Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("payload.zip");
        public static bool IsInstalled => File.Exists(Path.Combine(DefaultDir(), AppExe));

        // ---------------- install ----------------

        // progress(fraction 0..1, status text)
        public static void Install(Options o, Action<double, string> progress, CancellationToken cancel)
        {
            progress(0.01, $"Closing {AppName}…");
            if (o.Update) Thread.Sleep(1200);           // the app is quitting by itself
            if (!o.ExtractOnly) StopApp();

            var dir = o.Dir;
            Directory.CreateDirectory(dir);
            var staging = Path.Combine(dir, ".staging");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            bool hadDesktopLink = File.Exists(DesktopLink);

            // 1. unpack into a staging folder, with real byte-level progress
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
            {
                long total = Math.Max(1, zip.Entries.Sum(e => e.Length)), done = 0;
                var buffer = new byte[1 << 20];
                var lastReport = DateTime.MinValue;
                foreach (var entry in zip.Entries)
                {
                    cancel.ThrowIfCancellationRequested();
                    var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                    if (!target.StartsWith(Path.GetFullPath(staging), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The package contains an unsafe path.");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var src = entry.Open())
                    using (var dst = File.Create(target))
                    {
                        int n;
                        while ((n = src.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            dst.Write(buffer, 0, n);
                            done += n;
                            if ((DateTime.UtcNow - lastReport).TotalMilliseconds > 60)
                            {
                                lastReport = DateTime.UtcNow;
                                progress(0.03 + 0.82 * done / total, "Unpacking " + entry.Name);
                            }
                        }
                    }
                }
            }

            // 2. swap the new files in
            progress(0.87, "Replacing the old version…");
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir).ToList())
            {
                if (string.Equals(entry, staging, StringComparison.OrdinalIgnoreCase)) continue;
                Retry(() => { if (Directory.Exists(entry)) Directory.Delete(entry, true); else File.Delete(entry); });
            }
            foreach (var entry in Directory.EnumerateFileSystemEntries(staging).ToList())
            {
                var to = Path.Combine(dir, Path.GetFileName(entry));
                Retry(() => { if (Directory.Exists(entry)) Directory.Move(entry, to); else File.Move(entry, to); });
            }
            Directory.Delete(staging, true);

            if (o.ExtractOnly) { progress(1, "Done"); return; }

            // 3. the uninstaller travels inside the setup
            using (var u = Assembly.GetExecutingAssembly().GetManifestResourceStream("uninstaller.exe"))
            using (var f = File.Create(Path.Combine(dir, UninstallerExe)))
                u.CopyTo(f);

            // 4. shortcuts
            progress(0.93, "Adding shortcuts…");
            var exe = Path.Combine(dir, AppExe);
            Directory.CreateDirectory(StartMenuDir);
            Shortcut(Path.Combine(StartMenuDir, AppName + ".lnk"), exe, dir);
            Shortcut(Path.Combine(StartMenuDir, "Uninstall " + AppName + ".lnk"), Path.Combine(dir, UninstallerExe), dir);
            if (o.Update ? hadDesktopLink : o.DesktopShortcut) Shortcut(DesktopLink, exe, dir);

            // 5. "Installed apps" entry
            progress(0.97, "Registering with Windows…");
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\" + AppId)) k.SetValue("InstallDir", dir);
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("DisplayIcon", exe);
                k.SetValue("Publisher", Publisher);
                k.SetValue("URLInfoAbout", Url);
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + Path.Combine(dir, UninstallerExe) + "\"");
                k.SetValue("QuietUninstallString", "\"" + Path.Combine(dir, UninstallerExe) + "\" /S");
                k.SetValue("EstimatedSize", (int)(DirSize(dir) / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            progress(1, "Done");
        }

        // After a fresh install AI Pulse opens its own first-run setup;
        // after an update it simply starts again.
        public static void LaunchApp(Options o, bool fresh)
        {
            var exe = Path.Combine(o.Dir, AppExe);
            if (!File.Exists(exe)) return;
            Process.Start(new ProcessStartInfo(exe, fresh ? "--setup" : "") { UseShellExecute = true, WorkingDirectory = o.Dir });
        }

        // ---------------- uninstall ----------------

        public static void Uninstall(bool deleteData, Action<double, string> progress)
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            progress(0.05, $"Closing {AppName}…");
            StopApp();

            progress(0.25, "Removing shortcuts and settings from Windows…");
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) k?.DeleteValue(AppName, false);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\" + AppId, false);
            if (File.Exists(DesktopLink)) File.Delete(DesktopLink);
            if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true);

            progress(0.45, "Removing the app…");
            var self = Process.GetCurrentProcess().MainModule.FileName;
            var entries = Directory.EnumerateFileSystemEntries(dir).ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (string.Equals(e, self, StringComparison.OrdinalIgnoreCase)) continue;
                try { if (Directory.Exists(e)) Directory.Delete(e, true); else File.Delete(e); } catch { /* cleaned up below */ }
                progress(0.45 + 0.4 * (i + 1) / entries.Count, "Removing the app…");
            }

            if (deleteData && Directory.Exists(DataDir))
            {
                progress(0.9, "Deleting your settings and bookmarks…");
                Retry(() => Directory.Delete(DataDir, true));
            }
            progress(1, "Done");
        }

        // The uninstaller can't delete itself while running: a hidden cmd
        // waits a moment and removes the folder.
        public static void ScheduleSelfDelete()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & rd /s /q \"{dir}\"")
            {
                CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
            });
        }

        // ---------------- helpers ----------------

        static void StopApp()
        {
            foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppExe)))
            {
                try { p.Kill(); p.WaitForExit(5000); } catch { /* already gone */ }
            }
            Thread.Sleep(300);
        }

        static void Retry(Action a)
        {
            for (int i = 0; ; i++)
            {
                try { a(); return; }
                catch (Exception) when (i < 8) { Thread.Sleep(250 * (i + 1)); }
            }
        }

        static long DirSize(string dir) =>
            new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

        static void Shortcut(string lnk, string target, string workDir)
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(type);
            try
            {
                dynamic sh = shell;
                dynamic link = sh.CreateShortcut(lnk);
                link.TargetPath = target;
                link.WorkingDirectory = workDir;
                link.IconLocation = target + ",0";
                link.Save();
                Marshal.FinalReleaseComObject(link);
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
    }
}
