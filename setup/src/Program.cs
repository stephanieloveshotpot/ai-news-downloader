// AI Pulse setup and uninstaller (same code, two builds).
// The setup build carries the app (payload.zip) and the uninstaller as
// embedded resources; the uninstall build carries neither, which is how each
// one knows what it is.
//
//   AI-Pulse-Setup.exe                    install, then open AI Pulse's first-run setup
//   AI-Pulse-Setup.exe /update /relaunch  used by the in-app updater
//   AI-Pulse-Setup.exe /S [/relaunch]     silent
//   AI-Pulse-Setup.exe /D=C:\path         install somewhere else (last switch)
//   Uninstall AI Pulse.exe [/S]           remove (silent keeps settings)
//   /S --extract-only /D=C:\path          test: only unpack the app into a folder
//   --preview [--page welcome|options|progress|done|error|remove] [--snap out.png --snap-at ms]
//                                         look-only mode for design work: writes nothing
using System;
using System.Threading;
using System.Windows;

namespace AIPulseSetup
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var o = Options.Parse(args);
            var setup = Installer.HasPayload;

            if (o.Silent && !o.Preview)
            {
                try
                {
                    if (setup)
                    {
                        Installer.Install(o, (f, s) => { }, CancellationToken.None);
                        if (o.Relaunch && !o.ExtractOnly) Installer.LaunchApp(o, fresh: false);
                    }
                    else
                    {
                        Installer.Uninstall(false, (f, s) => { });
                        Installer.ScheduleSelfDelete();
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            return app.Run(new MainWindow(o, setup));
        }
    }
}
