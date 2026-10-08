using SampleApplication.Models;
using System;
using System.Windows;

namespace SampleApplication
{
    /// <summary>
    /// Edits a patient's demographic details. Changes are only applied to the
    /// patient when the user presses OK.
    /// </summary>
    public partial class PatientDialog : Window
    {
        public Patient Patient { get; private set; }

        public PatientDialog(Patient patient)
        {
            InitializeComponent();
            Patient = patient;

            tbFirstName.Text = patient.FirstName;
            tbLastName.Text = patient.LastName;
            dpDateOfBirth.SelectedDate = patient.DateOfBirth;
            tbPhone.Text = patient.Phone;
            tbCybermedId.Text = patient.CybermedPatientId;

            // The fingerprint hint only applies to new enrollments.
            if (patient.HasFingerprint) tbHint.Visibility = Visibility.Collapsed;

            Loaded += (s, e) => tbFirstName.Focus();
        }

        private void btOk_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(tbFirstName.Text) || String.IsNullOrWhiteSpace(tbLastName.Text))
            {
                MessageBox.Show(this, "First and last name are required.", Title);
                return;
            }

            Patient.FirstName = tbFirstName.Text.Trim();
            Patient.LastName = tbLastName.Text.Trim();
            Patient.DateOfBirth = dpDateOfBirth.SelectedDate;
            Patient.Phone = tbPhone.Text.Trim();
            Patient.CybermedPatientId = tbCybermedId.Text.Trim();

            DialogResult = true;
        }
    }
}
