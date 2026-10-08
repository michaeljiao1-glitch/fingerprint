using SampleApplication.Models;
using SampleApplication.Services;
using SampleApplication.UranusCoreAPIServiceReference;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SampleApplication
{
    /// <summary>
    /// Enroll, edit and remove patients. Enrolling and removing fingerprints requires an
    /// operator login, which the Bayometric BFS service prompts for itself.
    /// </summary>
    public partial class PatientsWindow : Window
    {
        private bool busy;

        public PatientsWindow()
        {
            InitializeComponent();
            RefreshPatients();
        }

        private async void btEnroll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PatientDialog(new Patient()) { Owner = this, Title = "Enroll New Patient" };
            if (dialog.ShowDialog() != true) return;
            Patient patient = dialog.Patient;

            BiometricResult result;
            SetBusy("Waiting for operator login and the patient's fingerprint...");
            try
            {
                result = await Task.Run(() => App.Biometrics.Enroll());
            }
            catch (Exception ex)
            {
                ShowError("Could not enroll fingerprint: " + ex.Message);
                return;
            }
            finally
            {
                SetBusy(null);
            }

            if (result.Code == BFSClientReturnErrorCode.ALREADY_ENROLLED)
            {
                Patient existing = App.Store.FindPatientByBiometricId(result.PersonId);
                ShowError(existing != null
                    ? "This fingerprint is already enrolled for " + existing.FullName + "."
                    : "This fingerprint is already enrolled in the reader database (biometric ID " + result.PersonId + ").");
                return;
            }

            if (!result.IsSuccess || String.IsNullOrEmpty(result.PersonId))
            {
                ShowError("Could not enroll fingerprint: " + BiometricService.Describe(result.Code, result.Message));
                return;
            }

            patient.Id = Guid.NewGuid().ToString();
            patient.BiometricId = result.PersonId;
            patient.EnrolledAt = DateTime.Now;
            App.Store.Data.Patients.Add(patient);

            if (TrySave())
            {
                MessageBox.Show(this, patient.FullName + " has been enrolled.", Title);
            }
            RefreshPatients(patient);
        }

        private void btEdit_Click(object sender, RoutedEventArgs e)
        {
            EditSelected();
        }

        private void dgPatients_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSelected();
        }

        private void EditSelected()
        {
            var selected = dgPatients.SelectedItem as Patient;
            if (selected == null) return;

            var dialog = new PatientDialog(selected) { Owner = this, Title = "Edit Patient" };
            if (dialog.ShowDialog() != true) return;

            TrySave();
            RefreshPatients(selected);
        }

        private async void btRemove_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgPatients.SelectedItem as Patient;
            if (selected == null) return;

            if (MessageBox.Show(this,
                    "Remove " + selected.FullName + " and delete their fingerprint?\n\nPast visits are kept.",
                    Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (selected.HasFingerprint)
            {
                BiometricResult result;
                SetBusy("Waiting for operator login...");
                try
                {
                    result = await Task.Run(() => App.Biometrics.Remove(selected.BiometricId));
                }
                catch (Exception ex)
                {
                    ShowError("Could not delete fingerprint: " + ex.Message);
                    return;
                }
                finally
                {
                    SetBusy(null);
                }

                // PERSON_ID_NOT_FOUND means the reader database no longer has it, which is what we want.
                if (!result.IsSuccess && result.Code != BFSClientReturnErrorCode.PERSON_ID_NOT_FOUND)
                {
                    ShowError("Could not delete fingerprint: " + BiometricService.Describe(result.Code, result.Message));
                    return;
                }
            }

            App.Store.Data.Patients.Remove(selected);
            TrySave();
            RefreshPatients();
        }

        private void btClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void tbFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshPatients(dgPatients.SelectedItem as Patient);
        }

        private void RefreshPatients(Patient select = null)
        {
            string filter = tbFilter.Text.Trim();
            var patients = App.Store.Data.Patients
                .Where(p => filter.Length == 0
                    || (p.FullName ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || (p.Phone ?? "").Contains(filter)
                    || (p.CybermedPatientId ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToList();

            dgPatients.ItemsSource = patients;
            if (select != null && patients.Contains(select))
            {
                dgPatients.SelectedItem = select;
                dgPatients.ScrollIntoView(select);
            }
        }

        private bool TrySave()
        {
            try
            {
                App.Store.Save();
                return true;
            }
            catch (Exception ex)
            {
                ShowError("Could not save patient data: " + ex.Message);
                return false;
            }
        }

        private void SetBusy(string message)
        {
            busy = message != null;
            tbBusy.Text = message ?? "";
            btEnroll.IsEnabled = btEdit.IsEnabled = btRemove.IsEnabled = !busy;
            dgPatients.IsEnabled = !busy;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Closing mid-enrollment would lose the result of the fingerprint capture.
            e.Cancel = busy;
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
