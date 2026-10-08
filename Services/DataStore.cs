using SampleApplication.Models;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace SampleApplication.Services
{
    /// <summary>
    /// Persists patients and visits to a single XML file. Intended for a single
    /// check-in station; all access happens on the UI thread.
    /// </summary>
    public class DataStore
    {
        private readonly string filePath;
        private readonly DataContractSerializer serializer = new DataContractSerializer(typeof(ClinicData));

        public ClinicData Data { get; private set; }

        public DataStore(string directory)
        {
            Directory.CreateDirectory(directory);
            filePath = Path.Combine(directory, "clinic-data.xml");
        }

        public string FilePath
        {
            get { return filePath; }
        }

        public void Load()
        {
            if (!File.Exists(filePath))
            {
                Data = new ClinicData();
                return;
            }

            using (var stream = File.OpenRead(filePath))
            {
                Data = (ClinicData)serializer.ReadObject(stream);
            }

            // DataContractSerializer skips constructors, so missing lists come back null.
            if (Data.Patients == null) Data.Patients = new System.Collections.Generic.List<Patient>();
            if (Data.Visits == null) Data.Visits = new System.Collections.Generic.List<Visit>();
        }

        /// <summary>
        /// Writes to a temp file first and swaps it in, keeping the previous version as .bak,
        /// so a crash mid-write never leaves a truncated data file.
        /// </summary>
        public void Save()
        {
            var tempPath = filePath + ".tmp";
            using (var writer = XmlWriter.Create(tempPath, new XmlWriterSettings { Indent = true }))
            {
                serializer.WriteObject(writer, Data);
            }

            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, filePath + ".bak");
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }

        public Patient FindPatientByBiometricId(string biometricId)
        {
            if (String.IsNullOrEmpty(biometricId)) return null;
            return Data.Patients.FirstOrDefault(p =>
                String.Equals(p.BiometricId, biometricId, StringComparison.OrdinalIgnoreCase));
        }

        public Patient FindPatientById(string id)
        {
            return Data.Patients.FirstOrDefault(p => p.Id == id);
        }
    }
}
