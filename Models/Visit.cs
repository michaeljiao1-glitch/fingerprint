using System;
using System.Runtime.Serialization;

namespace SampleApplication.Models
{
    /// <summary>
    /// One arrival/departure pair for a patient.
    /// </summary>
    [DataContract(Namespace = "")]
    public class Visit
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string PatientId { get; set; }

        /// <summary>Snapshot of the name at check-in so history survives patient removal.</summary>
        [DataMember] public string PatientName { get; set; }

        [DataMember] public DateTime ArrivedAt { get; set; }
        [DataMember] public DateTime? LeftAt { get; set; }

        [DataMember] public string CybermedVisitId { get; set; }

        /// <summary>Human readable result of the last Cybermed sync attempt.</summary>
        [DataMember] public string CybermedStatus { get; set; }

        public bool IsOpen
        {
            get { return !LeftAt.HasValue; }
        }
    }
}
