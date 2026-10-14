using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BlackThunder.UniEngine.Infrastructure
{
    public static partial class Log
    {
        public static bool LogWithStackTrace
        {
            get
            {
                lock (_safetyLock)
                    return _logWithStackTrace;
            }
            set
            {
                lock (_safetyLock)
                    _logWithStackTrace = value;
            }
        }

        public static string DebugFilePath
        {
            get
            {
                lock (_safetyLock)
                    return _debugFilePath;
            }
        }

        private static readonly object _safetyLock = new();
        private static readonly StringBuilder _buffer = new();
        private static readonly Encoding _fileEncoding = new UTF8Encoding(false);
        private static Action<string, LogLevel> _logger = WriteToConsole;
        private static bool _logWithStackTrace;
        private static string _debugFilePath = string.Empty;

        public static void Initialize(Action<string, LogLevel> logger = null)
        {
            lock (_safetyLock)
            {
                _logger = logger ?? WriteToConsole;
                _buffer.Clear();
                _debugFilePath = string.Empty;
            }
        }

        public static void AppendLine()
        {
            lock (_safetyLock)
                _buffer.AppendLine();
        }

        public static string Print()
        {
            lock (_safetyLock)
            {
                if (_buffer.Length == 0)
                    return string.Empty;

                string content = _buffer.ToString();
                string filePath = SaveLog(content);

                // Commit the saved path and clear only after the file closes successfully.
                _debugFilePath = filePath;
                _buffer.Clear();
                return content;
            }
        }

        public static void Reset()
        {
            lock (_safetyLock)
                _buffer.Clear();
        }

        private static string WriteMessage(string message, LogLevel logLevel)
        {
            Action<string, LogLevel> logger;
            lock (_safetyLock)
            {
                _buffer.Append('[').Append(logLevel).Append("] ").AppendLine(message);
                if (_logWithStackTrace)
                    _buffer.AppendLine(Environment.StackTrace);

                logger = _logger;
            }

            // External code may reenter the logger or throw.
            logger(message, logLevel);
            return message;
        }

        private static void WriteToConsole(string message, LogLevel logLevel) =>
            Console.WriteLine(message);

        private static string SaveLog(string content)
        {
            FileStream stream = CreateLogFile(Configuration.ProjectPath, out string filePath);
            try
            {
                using (stream)
                using (var writer = new StreamWriter(stream, _fileEncoding))
                    writer.Write(content);
            }
            catch
            {
                DeleteIncompleteFile(filePath);
                throw;
            }

            return filePath;
        }

        private static FileStream CreateLogFile(string directoryPath, out string filePath)
        {
            string fileName = "DebugLog-" + DateTime.Now.ToString("yyMMdd-HHmmss", CultureInfo.InvariantCulture);
            int number = 1;

            while (true)
            {
                string suffix = number == 1 ? string.Empty : "-" + number.ToString(CultureInfo.InvariantCulture);
                filePath = Path.Combine(directoryPath, fileName + suffix + ".txt");

                try
                {
                    return new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                }
                catch (IOException) when (File.Exists(filePath))
                {
                    number = checked(number + 1);
                }
            }
        }

        private static void DeleteIncompleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // Preserve the original write failure if cleanup cannot remove the partial file.
            }
        }
    }
}
