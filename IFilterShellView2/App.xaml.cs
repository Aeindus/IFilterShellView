using IFilterShellView.Native;
using System.Threading;
using System.Windows;

namespace IFilterShellView
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex IFilterViewMutex = null;
        private static string IFilterViewAppGuid = "IFilterShellView-{fb45b5c0-c99e-4a79-bd10-bee33c83b3b8}";


        protected override void OnStartup(StartupEventArgs e)
        {
            IFilterViewMutex = new Mutex(true, IFilterViewAppGuid, out bool NewInstanceOfMutex);

            if (!NewInstanceOfMutex)
            {
                Current.Shutdown();
            }

            NativeWin32.SetProcessDPIAware();
            base.OnStartup(e);

            // Just instantiate the window class but don't show it.
            MainWindow AppMainWindow = new MainWindow();
            AppMainWindow.Show();
            AppMainWindow.UpdateWindowPositionToFixedPin();
            Thread.Sleep(400);
            AppMainWindow.Hide();

            // Don't show the window. Not yet
            // The callbacks and event monitors have been set by now.
            // It is up to the user what will happen from now on.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            IFilterViewMutex?.Close();
            base.OnExit(e);
        }
    }
}
