using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SampleApplication.Models
{
    /// <summary>
    /// Root object persisted to disk by <see cref="SampleApplication.Services.DataStore"/>.
    /// </summary>
    [DataContract(Namespace = "")]
    public class ClinicData
    {
        [DataMember] public List<Patient> Patients { get; set; }
        [DataMember] public List<Visit> Visits { get; set; }

        public ClinicData()
        {
            Patients = new List<Patient>();
            Visits = new List<Visit>();
        }
    }
}
