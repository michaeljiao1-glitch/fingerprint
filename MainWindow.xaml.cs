using SampleApplication.Models;
using SampleApplication.Services;
using SampleApplication.UranusCoreAPIServiceReference;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SampleApplication
{
    /// <summary>
    /// Front-desk screen: scan a finger to check a patient in or out, and see the day's visits.
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Brush NeutralBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        private static readonly Brush InBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xF5, 0xDC));
        private static readonly Brush OutBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xE8, 0xF8));
        private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0xCC));
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xF9, 0xDA, 0xDA));

        private bool scanning;

        public MainWindow()
        {
            InitializeComponent();
            dpDay.SelectedDate = DateTime.Today;
            RefreshVisits();
        }

        private async void btScan_Click(object sender, RoutedEventArgs e)
        {
            SetScanning(true);
            ShowStatus("Scanning...", "Place the finger on the reader.", NeutralBrush);

            BiometricResult result;
            try
            {
                result = await Task.Run(() => App.Biometrics.Identify());
            }
            catch (Exception ex)
            {
                ShowStatus("Scanner error", ex.Message, ErrorBrush);
                return;
            }
            finally
            {
                SetScanning(false);
            }

            if (!result.IsSuccess || String.IsNullOrEmpty(result.PersonId))
            {
                var brush = result.Code == BFSClientReturnErrorCode.NO_HIT ? WarnBrush : ErrorBrush;
                var detail = BiometricService.Describe(result.Code, result.Message);
                if (result.Code == BFSClientReturnErrorCode.NO_HIT)
                {
                    detail += " If this is a new patient, enroll them under Manage Patients.";
                }
                ShowStatus("Not identified", detail, brush);
                return;
            }

            Patient patient = App.Store.FindPatientByBiometricId(result.PersonId);
            if (patient == null)
            {
                ShowStatus("Unknown fingerprint",
                    "The reader matched biometric ID " + result.PersonId + ", but no patient record uses it. " +
                    "Remove and re-enroll the fingerprint under Manage Patients.", WarnBrush);
                return;
            }

            ScanOutcome outcome;
            try
            {
                outcome = App.Attendance.RecordScan(patient, DateTime.Now);
            }
            catch (Exception ex)
            {
                ShowStatus("Could not save", ex.Message, ErrorBrush);
                return;
            }

            switch (outcome.Action)
            {
                case ScanAction.CheckedIn:
                    ShowStatus("Welcome, " + patient.FirstName,
                        patient.FullName + " checked in at " + outcome.Visit.ArrivedAt.ToString("t") + ".", InBrush);
                    break;
                case ScanAction.CheckedOut:
                    ShowStatus("Goodbye, " + patient.FirstName,
                        patient.FullName + " checked out at " + outcome.Visit.LeftAt.Value.ToString("t") +
                        " (" + FormatDuration(outcome.Visit) + ").", OutBrush);
                    break;
                default:
                    ShowStatus("Already recorded",
                        patient.FullName + " was just scanned. No change was made.", WarnBrush);
                    break;
            }

            dpDay.SelectedDate = DateTime.Today;
            RefreshVisits();

            if (outcome.Action == ScanAction.CheckedIn)
            {
                await SyncCybermedAsync(outcome.Visit, patient);
            }
        }

        private void btCancelScan_Click(object sender, RoutedEventArgs e)
        {
            btCancelScan.IsEnabled = false;
            Task.Run(() =>
            {
                try { App.Biometrics.CancelIdentify(); }
                catch { /* the pending search reports its own outcome */ }
            });
        }

        private void btPatients_Click(object sender, RoutedEventArgs e)
        {
            new PatientsWindow { Owner = this }.ShowDialog();
            RefreshVisits();
        }

        private void dpDay_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshVisits();
        }

        private void btCheckOut_Click(object sender, RoutedEventArgs e)
        {
            var row = dgVisits.SelectedItem as VisitRow;
            if (row == null || !row.Visit.IsOpen)
            {
                MessageBox.Show(this, "Select a visit that has not been checked out yet.", Title);
                return;
            }

            if (MessageBox.Show(this, "Check out " + row.PatientName + " now?", Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                App.Attendance.CheckOut(row.Visit, DateTime.Now);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            RefreshVisits();
        }

        private async void btRetrySync_Click(object sender, RoutedEventArgs e)
        {
            var row = dgVisits.SelectedItem as VisitRow;
            if (row == null || !String.IsNullOrEmpty(row.Visit.CybermedVisitId))
            {
                MessageBox.Show(this, "Select a visit that does not have a Cybermed visit yet.", Title);
                return;
            }

            Patient patient = App.Store.FindPatientById(row.Visit.PatientId);
            if (patient == null)
            {
                MessageBox.Show(this, "This patient has been removed, so the visit cannot be synced.", Title);
                return;
            }

            btRetrySync.IsEnabled = false;
            try
            {
                await SyncCybermedAsync(row.Visit, patient);
            }
            finally
            {
                btRetrySync.IsEnabled = true;
            }
        }

        /// <summary>
        /// Creates the Cybermed visit in the background. Failures are recorded on the visit
        /// (and can be retried) rather than blocking the check-in.
        /// </summary>
        private async Task SyncCybermedAsync(Visit visit, Patient patient)
        {
            if (!App.Settings.CybermedEnabled)
            {
                visit.CybermedStatus = "Disabled";
                SaveQuietly();
                RefreshVisits();
                return;
            }

            visit.CybermedStatus = "Syncing...";
            RefreshVisits();

            try
            {
                CybermedVisit cv = await Task.Run(() => App.Cybermed.EnsureVisit(patient, visit.ArrivedAt));
                visit.CybermedVisitId = cv.VisitId;
                visit.CybermedStatus = cv.Created ? "Visit created" : "Visit already existed";
            }
            catch (Exception ex)
            {
                visit.CybermedStatus = "Failed: " + ex.Message;
            }

            SaveQuietly();
            RefreshVisits();
        }

        private void SaveQuietly()
        {
            try
            {
                App.Store.Save();
            }
            catch (Exception ex)
            {
                ShowStatus("Could not save", ex.Message, ErrorBrush);
            }
        }

        private void RefreshVisits()
        {
            if (App.Store == null) return;

            DateTime day = dpDay.SelectedDate ?? DateTime.Today;
            var selectedId = dgVisits.SelectedItem is VisitRow ? ((VisitRow)dgVisits.SelectedItem).Visit.Id : null;

            var rows = App.Store.Data.Visits
                .Where(v => v.ArrivedAt.Date == day.Date)
                .OrderByDescending(v => v.ArrivedAt)
                .Select(v => new VisitRow(v))
                .ToList();

            dgVisits.ItemsSource = rows;
            dgVisits.SelectedItem = rows.FirstOrDefault(r => r.Visit.Id == selectedId);

            int present = rows.Count(r => r.Visit.IsOpen);
            tbSummary.Text = rows.Count + " visit(s), " + present + " still in clinic";
        }

        private void SetScanning(bool value)
        {
            scanning = value;
            btScan.IsEnabled = !value;
            btCancelScan.IsEnabled = value;
            btPatients.IsEnabled = !value;
        }

        private void ShowStatus(string title, string detail, Brush background)
        {
            tbStatusTitle.Text = title;
            tbStatusDetail.Text = detail;
            bdStatus.Background = background;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Release the reader and the operator session without hanging the shutdown.
            var cleanup = Task.Run(() =>
            {
                try
                {
                    if (scanning) App.Biometrics.CancelIdentify();
                    App.Biometrics.EndSession();
                }
                catch { /* best effort */ }
            });
            cleanup.Wait(TimeSpan.FromSeconds(3));
        }

        internal static string FormatDuration(Visit v)
        {
            if (!v.LeftAt.HasValue) return "";
            TimeSpan d = v.LeftAt.Value - v.ArrivedAt;
            return d.TotalHours >= 1
                ? String.Format("{0}h {1:00}m", (int)d.TotalHours, d.Minutes)
                : String.Format("{0}m", (int)d.TotalMinutes);
        }

        public class VisitRow
        {
            public VisitRow(Visit visit)
            {
                Visit = visit;
            }

            public Visit Visit { get; private set; }
            public string PatientName { get { return Visit.PatientName; } }
            public string Arrived { get { return Visit.ArrivedAt.ToString("t"); } }
            public string Left { get { return Visit.LeftAt.HasValue ? Visit.LeftAt.Value.ToString("t") : "In clinic"; } }
            public string Duration { get { return FormatDuration(Visit); } }
            public string CybermedVisitId { get { return Visit.CybermedVisitId; } }
            public string CybermedStatus { get { return Visit.CybermedStatus; } }
        }
    }
}
