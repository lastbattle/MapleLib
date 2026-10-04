using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MapleLib.Helpers
{
    public static class ErrorLogger
    {
        private static readonly object _lock = new object();
        private static readonly object _saveLock = new object();
        private static readonly List<Error> _errorList = new List<Error>();

        public static void Log(ErrorLevel level, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentNullException(nameof(message), "Error message cannot be null or empty.");

            lock (_lock)
                _errorList.Add(new Error(level, message, DateTime.UtcNow));
        }

        /// <summary>
        /// Returns the numbers of errors currently in the pending queue
        /// </summary>
        /// <returns></returns>
        public static int NumberOfErrorsPresent()
        {
            lock (_lock)
                return _errorList.Count;
        }

        /// <summary>
        /// Errors present currently in the pending queue
        /// </summary>
        /// <returns></returns>
        public static bool ErrorsPresent()
        {
            lock (_lock)
                return _errorList.Count != 0;
        }

        /// <summary>
        /// Clears all errors currently in the pending queue
        /// </summary>
        public static void ClearErrors()
        {
            lock (_lock)
                _errorList.Clear();
        }

        /// <summary>
        /// Logs all pending errors in the queue to file, grouped by error level, and clears the queue
        /// </summary>
        /// <param name="filename">The path to the log file</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or empty</exception>
        /// <exception cref="IOException">Thrown when there's an error writing to the file</exception>
        public static void SaveToFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentNullException(nameof(filename), "Filename cannot be null or empty.");

            lock (_saveLock)
            {
                List<Error> errorsCopy;
                lock (_lock)
                {
                    if (_errorList.Count == 0)
                        return;
                    errorsCopy = new List<Error>(_errorList);
                }

                var groupedErrors = errorsCopy
                    .GroupBy(e => e.Level)
                    .OrderBy(g => g.Key);

                var sb = new StringBuilder();
                sb.AppendLine($"----- Start of the error log. [{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] -----");

                foreach (var errorGroup in groupedErrors)
                {
                    sb.AppendLine();
                    sb.AppendLine($"=== {errorGroup.Key} Errors ===");

                    foreach (var error in errorGroup.OrderBy(e => e.Timestamp))
                    {
                        sb.AppendLine($"[{error.Timestamp:HH:mm:ss.fff}] : {error.Message}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"----- End of the error log. [{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] -----");
                sb.AppendLine();

                // Use FileShare.ReadWrite to allow other processes to read the file while we're writing.
                using (var sw = new StreamWriter(File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    sw.Write(sb.ToString());
                }

                // Remove only the entries that were successfully persisted. Errors
                // logged while the file was being written remain pending.
                lock (_lock)
                {
                    RemovePersistedErrors(_errorList, errorsCopy);
                }
            }
        }

        internal static void RemovePersistedErrors(List<Error> pending, IReadOnlyList<Error> persisted)
        {
            if (persisted.Count == 0 || pending.Count == 0)
                return;

            // Log appends while the file is written, so the normal case is a
            // persisted prefix. Remove it in one operation after confirming the
            // references; the fallback preserves identity-based removal if a
            // concurrent clear/repopulation changed the list meanwhile.
            if (pending.Count >= persisted.Count)
            {
                bool isPrefix = true;
                for (int i = 0; i < persisted.Count; i++)
                {
                    if (!ReferenceEquals(pending[i], persisted[i]))
                    {
                        isPrefix = false;
                        break;
                    }
                }

                if (isPrefix)
                {
                    pending.RemoveRange(0, persisted.Count);
                    return;
                }
            }

            var persistedSet = new HashSet<Error>(persisted);
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < pending.Count; readIndex++)
            {
                Error error = pending[readIndex];
                if (!persistedSet.Contains(error))
                    pending[writeIndex++] = error;
            }

            if (writeIndex < pending.Count)
                pending.RemoveRange(writeIndex, pending.Count - writeIndex);
        }

        /// <summary>
        /// Gets a snapshot of current errors grouped by error level
        /// </summary>
        /// <returns>Dictionary with error levels and their corresponding error messages</returns>
        public static Dictionary<ErrorLevel, List<Error>> GetErrorSnapshot()
        {
            lock (_lock)
            {
                var snapshot = new Dictionary<ErrorLevel, List<Error>>();
                foreach (Error error in _errorList)
                {
                    if (!snapshot.TryGetValue(error.Level, out List<Error> group))
                    {
                        group = new List<Error>();
                        snapshot.Add(error.Level, group);
                    }
                    group.Add(error);
                }
                return snapshot;
            }
        }
    }

    public class Error
    {
        public ErrorLevel Level { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        internal Error(ErrorLevel level, string message, DateTime timestamp)
        {
            Level = level;
            Message = message;
            Timestamp = timestamp;
        }

        public override string ToString()
        {
            return $"[{Level}] [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] : {Message}";
        }
    }

    public enum ErrorLevel
    {
        Info,
        MissingFeature,
        IncorrectStructure,
        Critical,
        Crash
    }
}
