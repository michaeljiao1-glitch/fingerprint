using SampleApplication.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SampleApplication.Services
{
    /// <summary>A visit as known by Cybermed.</summary>
    public class CybermedVisit
    {
        public string VisitId { get; set; }

        /// <summary>True when this call created the visit, false when it already existed.</summary>
        public bool Created { get; set; }
    }

    /// <summary>
    /// The Cybermed operations this app needs. Implement this against the real
    /// Cybermed API and swap it in at <see cref="App"/> start-up.
    /// </summary>
    public interface ICybermedClient
    {
        /// <summary>Returns the patient's visit on the given day, or null if none exists.</summary>
        CybermedVisit FindVisit(Patient patient, DateTime day);

        /// <summary>Creates a visit for the patient starting at <paramref name="arrivedAt"/>.</summary>
        CybermedVisit CreateVisit(Patient patient, DateTime arrivedAt);
    }

    /// <summary>
    /// Creates a Cybermed visit for an arrival unless one already exists for that day.
    /// Runs on a background thread; throws on API errors so the caller can record them.
    /// </summary>
    public class CybermedService
    {
        private readonly ICybermedClient client;

        public CybermedService(ICybermedClient client)
        {
            this.client = client;
        }

        public CybermedVisit EnsureVisit(Patient patient, DateTime arrivedAt)
        {
            CybermedVisit existing = client.FindVisit(patient, arrivedAt.Date);
            if (existing != null)
            {
                existing.Created = false;
                return existing;
            }

            CybermedVisit created = client.CreateVisit(patient, arrivedAt);
            created.Created = true;
            return created;
        }
    }

    /// <summary>
    /// PLACEHOLDER: does not talk to Cybermed. It remembers visits in memory so the
    /// "create if not exists" flow behaves realistically while the real API is pending.
    ///
    /// TODO(Cybermed): replace with an HTTP implementation, roughly:
    ///   FindVisit   -> GET  {BaseUrl}/patients/{CybermedPatientId}/visits?date=yyyy-MM-dd
    ///   CreateVisit -> POST {BaseUrl}/patients/{CybermedPatientId}/visits  { "arrivedAt": ... }
    /// authenticated with the configured API key. Patients without a CybermedPatientId
    /// may need a lookup by name + date of birth first.
    /// </summary>
    public class PlaceholderCybermedClient : ICybermedClient
    {
        private readonly string baseUrl;
        private readonly string apiKey;
        private readonly Dictionary<string, string> visitsByPatientDay = new Dictionary<string, string>();
        private readonly object sync = new object();

        public PlaceholderCybermedClient(string baseUrl, string apiKey)
        {
            this.baseUrl = baseUrl;
            this.apiKey = apiKey;
        }

        public CybermedVisit FindVisit(Patient patient, DateTime day)
        {
            Debug.WriteLine("[Cybermed placeholder] FindVisit {0} {1:yyyy-MM-dd} via {2} (api key set: {3})",
                PatientKey(patient), day, baseUrl, !String.IsNullOrEmpty(apiKey));
            lock (sync)
            {
                string visitId;
                return visitsByPatientDay.TryGetValue(Key(patient, day), out visitId)
                    ? new CybermedVisit { VisitId = visitId }
                    : null;
            }
        }

        public CybermedVisit CreateVisit(Patient patient, DateTime arrivedAt)
        {
            Debug.WriteLine("[Cybermed placeholder] CreateVisit {0} at {1:s} via {2}", PatientKey(patient), arrivedAt, baseUrl);
            var visitId = "PLACEHOLDER-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            lock (sync)
            {
                visitsByPatientDay[Key(patient, arrivedAt.Date)] = visitId;
            }
            return new CybermedVisit { VisitId = visitId };
        }

        private static string PatientKey(Patient patient)
        {
            return String.IsNullOrEmpty(patient.CybermedPatientId) ? patient.Id : patient.CybermedPatientId;
        }

        private static string Key(Patient patient, DateTime day)
        {
            return PatientKey(patient) + "|" + day.ToString("yyyy-MM-dd");
        }
    }
}
