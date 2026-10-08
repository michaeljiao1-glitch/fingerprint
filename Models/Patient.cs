using System;
using System.Runtime.Serialization;

namespace SampleApplication.Models
{
    /// <summary>
    /// A patient known to this station. The fingerprint template itself lives in the
    /// Bayometric BFS service; we only keep the BFS person ID that points to it.
    /// </summary>
    [DataContract(Namespace = "")]
    public class Patient
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string BiometricId { get; set; }
        [DataMember] public string FirstName { get; set; }
        [DataMember] public string LastName { get; set; }
        [DataMember] public DateTime? DateOfBirth { get; set; }
        [DataMember] public string Phone { get; set; }

        /// <summary>The patient's identifier in Cybermed, used when looking up / creating visits.</summary>
        [DataMember] public string CybermedPatientId { get; set; }

        [DataMember] public DateTime EnrolledAt { get; set; }

        public string FullName
        {
            get { return (LastName + ", " + FirstName).Trim(' ', ','); }
        }

        public string DateOfBirthText
        {
            get { return DateOfBirth.HasValue ? DateOfBirth.Value.ToString("yyyy-MM-dd") : ""; }
        }

        public bool HasFingerprint
        {
            get { return !String.IsNullOrEmpty(BiometricId); }
        }
    }
}
