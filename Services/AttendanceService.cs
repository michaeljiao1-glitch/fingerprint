using SampleApplication.Models;
using System;
using System.Linq;

namespace SampleApplication.Services
{
    public enum ScanAction
    {
        CheckedIn,
        CheckedOut,

        /// <summary>The same patient scanned again within the duplicate window; nothing changed.</summary>
        Ignored,
    }

    public class ScanOutcome
    {
        public ScanAction Action { get; set; }
        public Visit Visit { get; set; }
    }

    /// <summary>
    /// Decides whether a successful fingerprint match is an arrival or a departure.
    /// The first scan of the day opens a visit, the next scan closes it, and a scan
    /// after that opens a new visit (e.g. the patient came back later the same day).
    /// </summary>
    public class AttendanceService
    {
        private readonly DataStore store;
        private readonly TimeSpan duplicateWindow;

        public AttendanceService(DataStore store, TimeSpan duplicateWindow)
        {
            this.store = store;
            this.duplicateWindow = duplicateWindow;
        }

        public ScanOutcome RecordScan(Patient patient, DateTime now)
        {
            var todays = store.Data.Visits
                .Where(v => v.PatientId == patient.Id && v.ArrivedAt.Date == now.Date)
                .OrderByDescending(v => v.ArrivedAt)
                .ToList();

            var open = todays.FirstOrDefault(v => v.IsOpen);
            if (open != null)
            {
                // Guard against a nervous double-scan right after checking in.
                if (now - open.ArrivedAt < duplicateWindow)
                {
                    return new ScanOutcome { Action = ScanAction.Ignored, Visit = open };
                }

                open.LeftAt = now;
                store.Save();
                return new ScanOutcome { Action = ScanAction.CheckedOut, Visit = open };
            }

            var last = todays.FirstOrDefault();
            if (last != null && last.LeftAt.HasValue && now - last.LeftAt.Value < duplicateWindow)
            {
                return new ScanOutcome { Action = ScanAction.Ignored, Visit = last };
            }

            var visit = new Visit
            {
                Id = Guid.NewGuid().ToString(),
                PatientId = patient.Id,
                PatientName = patient.FullName,
                ArrivedAt = now,
                CybermedStatus = "Pending",
            };
            store.Data.Visits.Add(visit);
            store.Save();
            return new ScanOutcome { Action = ScanAction.CheckedIn, Visit = visit };
        }

        /// <summary>Manual check-out for patients who leave without scanning.</summary>
        public void CheckOut(Visit visit, DateTime now)
        {
            if (!visit.IsOpen) return;
            visit.LeftAt = now;
            store.Save();
        }
    }
}
