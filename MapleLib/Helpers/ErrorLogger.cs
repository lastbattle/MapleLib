/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

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
            return _errorList.Count;
        }

        /// <summary>
        /// Errors present currently in the pending queue
        /// </summary>
        /// <returns></returns>
        public static bool ErrorsPresent()
        {
            return _errorList.Any();
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

            if (!ErrorsPresent())
                return;

            List<Error> errorsCopy;
            lock (_lock)
            {
                errorsCopy = new List<Error>(_errorList);
                ClearErrors();
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

            // Use FileShare.ReadWrite to allow other processes to read the file while we're writing
            using (var sw = new StreamWriter(File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
            {
                sw.Write(sb.ToString());
            }
        }

        /// <summary>
        /// Gets a snapshot of current errors grouped by error level
        /// </summary>
        /// <returns>Dictionary with error levels and their corresponding error messages</returns>
        public static Dictionary<ErrorLevel, List<Error>> GetErrorSnapshot()
        {
            lock (_lock)
            {
                return _errorList
                    .GroupBy(e => e.Level)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToList()
                    );
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
