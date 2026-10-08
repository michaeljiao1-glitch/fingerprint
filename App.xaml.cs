using SampleApplication.Services;
using System;
using System.Windows;

namespace SampleApplication
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static AppSettings Settings { get; private set; }
        public static DataStore Store { get; private set; }
        public static BiometricService Biometrics { get; private set; }
        public static AttendanceService Attendance { get; private set; }
        public static CybermedService Cybermed { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Settings = AppSettings.Load();
            Store = new DataStore(Settings.DataDirectory);
            try
            {
                Store.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load patient data from " + Store.FilePath + ".\n\n" + ex.Message,
                    "Patient Check-In", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            Biometrics = new BiometricService(Settings.ScanTimeoutSeconds);
            Attendance = new AttendanceService(Store, Settings.DuplicateScanWindow);

            // Swap PlaceholderCybermedClient for the real implementation once the API is available.
            Cybermed = new CybermedService(new PlaceholderCybermedClient(Settings.CybermedBaseUrl, Settings.CybermedApiKey));

            new MainWindow().Show();
        }
    }
}
