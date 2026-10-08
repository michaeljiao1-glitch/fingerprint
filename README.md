# Patient Check-In (fingerprint)

A small WPF desktop app for the front desk. Patients scan a finger when they
arrive and again when they leave; the app records arrival and departure times
and makes sure a matching visit exists in Cybermed.

Built on the Bayometric BFS ("Uranus Core") .NET sample that this repo started
from. The BFS service does the fingerprint capture, matching and template
storage; this app keeps the patient details and visit times.

## Requirements

- Windows with .NET Framework 4.8
- Visual Studio 2019 or later (open `SampleApplication.csproj`)
- Bayometric BFS installed and running, with a reader connected. The app talks
  to it at `http://localhost:22963/Bayometric/BFSAPI/soap` (see `App.config`).

## How it works

**Check-in / check-out** (main window)

1. Press **Scan Fingerprint**. The BFS reader window appears and captures the finger.
2. The match is looked up in the local patient list.
   - No open visit today → a new visit starts (**checked in**), and the app asks
     Cybermed for today's visit, creating one if it doesn't exist.
   - Open visit today → the visit ends (**checked out**) and the duration is shown.
   - The same patient scanned again within `DuplicateScanWindowMinutes` is ignored,
     so a double tap doesn't immediately check them out.
3. The grid lists the day's visits. Pick another date to see history.
   **Check Out Selected** closes a visit for someone who left without scanning;
   **Retry Cybermed Sync** retries a visit whose Cybermed call failed.

**Patients** (Manage Patients...)

- **Enroll New Patient**: enter name, date of birth, phone and Cybermed patient
  ID, then BFS asks for an operator login (first time per app run) and the
  patient's finger. Fingerprints already enrolled are rejected.
- **Edit Details**: change demographics without rescanning.
- **Remove Patient**: deletes the fingerprint from BFS and the patient record.
  Past visits are kept.

## Data

Patients and visits are saved to `%ProgramData%\PatientCheckIn\clinic-data.xml`
(the previous version is kept as `clinic-data.xml.bak`). Change the folder with
`DataDirectory` in `App.config`. The file holds patient identifiers, so keep
the folder access-restricted and include it in backups.

## Cybermed (placeholder)

`Services/CybermedClient.cs` defines `ICybermedClient` with two calls:
`FindVisit(patient, day)` and `CreateVisit(patient, arrivedAt)`.
`CybermedService.EnsureVisit` calls `FindVisit` and only creates a visit when
none exists.

The current `PlaceholderCybermedClient` makes no network calls. It hands out
`PLACEHOLDER-xxxxxxxx` visit IDs and remembers them in memory. To connect the
real API, implement `ICybermedClient` and swap it in `App.xaml.cs`. The
`Cybermed.BaseUrl` and `Cybermed.ApiKey` settings in `App.config` are already
passed through. A failed call never blocks check-in; the error is shown in the
visit's Cybermed Status column and can be retried.

## Settings (`App.config` → `appSettings`)

| Key | Default | Meaning |
| --- | --- | --- |
| `DataDirectory` | `%ProgramData%\PatientCheckIn` | Where data is stored |
| `ScanTimeoutSeconds` | `30` | How long the reader waits for a finger |
| `DuplicateScanWindowMinutes` | `2` | Repeat scans within this window are ignored |
| `Cybermed.Enabled` | `true` | Turn the Cybermed sync off |
| `Cybermed.BaseUrl` / `Cybermed.ApiKey` | | Passed to the Cybermed client |
