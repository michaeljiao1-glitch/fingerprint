using SampleApplication.UranusCoreAPIServiceReference;
using System;
using System.ServiceModel;

namespace SampleApplication.Services
{
    public class BiometricResult
    {
        public BFSClientReturnErrorCode Code { get; set; }
        public string Message { get; set; }

        /// <summary>The BFS person ID that was matched, enrolled or removed.</summary>
        public string PersonId { get; set; }

        public bool IsSuccess
        {
            get { return Code == BFSClientReturnErrorCode.SUCCESS || Code == BFSClientReturnErrorCode.HIT_CONFIRMED; }
        }
    }

    /// <summary>
    /// Wraps the Bayometric BFS (Uranus Core) SOAP service that drives the fingerprint reader.
    /// All methods block until the BFS service answers (the reader GUI is shown by BFS itself),
    /// so call them from a background thread.
    /// </summary>
    public class BiometricService
    {
        private readonly int timeoutSeconds;
        private string sessionKey;

        public BiometricService(int timeoutSeconds)
        {
            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>Captures a fingerprint and searches all enrolled persons for a match.</summary>
        public BiometricResult Identify()
        {
            // Same as the sample's Search(), but with our own timeout.
            BFSResponse r = Call(c => c.SearchEx(null, null, true, timeoutSeconds));
            return new BiometricResult
            {
                Code = r.ResponseCode,
                Message = r.ReturnMessage,
                PersonId = r.PersonFoundID,
            };
        }

        public void CancelIdentify()
        {
            Call(c => c.CancelSearch(null));
        }

        /// <summary>
        /// Captures and stores a new fingerprint. Requires an operator session, which BFS
        /// prompts for (via its own GUI) the first time.
        /// </summary>
        public BiometricResult Enroll()
        {
            BFSResponse r = WithSession(key => Call(c => c.RegisterPerson(key, null)));
            return new BiometricResult
            {
                Code = r.ResponseCode,
                Message = r.ReturnMessage,
                // On ALREADY_ENROLLED, BFS reports the existing person in PersonFoundID.
                PersonId = r.ResponseCode == BFSClientReturnErrorCode.ALREADY_ENROLLED && !String.IsNullOrEmpty(r.PersonFoundID)
                    ? r.PersonFoundID
                    : r.PersonID,
            };
        }

        public BiometricResult Remove(string personId)
        {
            BFSResponse r = WithSession(key => Call(c => c.UnregisterPerson(key, personId)));
            return new BiometricResult { Code = r.ResponseCode, Message = r.ReturnMessage, PersonId = r.PersonID };
        }

        public void EndSession()
        {
            if (sessionKey == null) return;
            var key = sessionKey;
            sessionKey = null;
            Call(c => c.EndSession(key));
        }

        private BFSResponse WithSession(Func<string, BFSResponse> operation)
        {
            EnsureSession();
            BFSResponse r = operation(sessionKey);

            if (IsSessionProblem(r.ResponseCode))
            {
                // Session timed out on the BFS side; ask the operator to log in again once.
                sessionKey = null;
                EnsureSession();
                r = operation(sessionKey);
            }
            return r;
        }

        private void EnsureSession()
        {
            if (sessionKey != null) return;

            BFSAuthResponse auth = Call(c => c.CreateSession(new AuthRequestInfo { ShowGUI = true, Timeout = timeoutSeconds }));
            if (auth.ResponseCode != BFSClientReturnErrorCode.SUCCESS)
            {
                throw new BiometricException("Operator login failed: " + Describe(auth.ResponseCode, auth.ReturnMessage));
            }
            sessionKey = auth.SessionKey;
        }

        private static bool IsSessionProblem(BFSClientReturnErrorCode code)
        {
            return code == BFSClientReturnErrorCode.INVALID_SESSION
                || code == BFSClientReturnErrorCode.SESSION_EXPIRED
                || code == BFSClientReturnErrorCode.REQUIRES_AUTHENTICATION;
        }

        /// <summary>
        /// Uses a fresh WCF client per call so a faulted channel never poisons later calls.
        /// </summary>
        private static T Call<T>(Func<UranusCoreClient, T> operation)
        {
            var client = new UranusCoreClient();
            try
            {
                T result = operation(client);
                client.Close();
                return result;
            }
            catch (EndpointNotFoundException ex)
            {
                client.Abort();
                throw new BiometricException(
                    "Cannot reach the Bayometric BFS service. Make sure it is installed and running.", ex);
            }
            catch
            {
                client.Abort();
                throw;
            }
        }

        public static string Describe(BFSClientReturnErrorCode code, string message)
        {
            switch (code)
            {
                case BFSClientReturnErrorCode.CANCELED_BY_USER: return "Scan was cancelled.";
                case BFSClientReturnErrorCode.CAPTURE_TIMEOUT: return "No finger was detected before the timeout.";
                case BFSClientReturnErrorCode.BAD_IMAGE_QUALITY: return "Poor fingerprint quality. Clean the reader and try again.";
                case BFSClientReturnErrorCode.NO_READER_CONNECTED: return "No fingerprint reader is connected.";
                case BFSClientReturnErrorCode.SERVICE_BUSY: return "The fingerprint service is busy. Try again in a moment.";
                case BFSClientReturnErrorCode.NO_HIT: return "Fingerprint not recognised.";
                case BFSClientReturnErrorCode.ALREADY_ENROLLED: return "This fingerprint is already enrolled.";
                default:
                    return String.IsNullOrEmpty(message) ? code.ToString() : message + " (" + code + ")";
            }
        }
    }

    public class BiometricException : Exception
    {
        public BiometricException(string message) : base(message) { }
        public BiometricException(string message, Exception inner) : base(message, inner) { }
    }
}
