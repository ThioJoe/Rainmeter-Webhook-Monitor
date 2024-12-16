using Microsoft.Extensions.Primitives;
using System.Diagnostics;

namespace RainmeterWebhookMonitor
{
    public class Logging
    {
        private static bool debugConsoleFileLoggingAlreadyEnabled = false; // Prevents an extra trace listener from being added

        private static readonly string timestamp = DateTime.Now.ToString("MM-dd_HH-mm-ss");
        private static readonly string debugConsoleLogFileName = $"DebugConsoleLog_{timestamp}.txt";
        private static readonly string debugWebhookLogFileName = $"DebugWebhookLog_{timestamp}.txt";

        private static string debugFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "RainmeterWebhookMonitor_DebugLogs");
        private static readonly string debugConsoleLogFilePath = Path.Combine(debugFolderPath, debugConsoleLogFileName);
        private static readonly string debugWebhookLogFilePath = Path.Combine(debugFolderPath, debugWebhookLogFileName);

        public static void EnableDebugConsoleFileLogging()
        {
            if (!debugConsoleFileLoggingAlreadyEnabled)
            {
                // Set up a debug log file
                Trace.Listeners.Add(new TextWriterTraceListener(debugConsoleLogFilePath));
                Trace.AutoFlush = true;
                Trace.WriteLine($"Debug log file created at: {debugConsoleLogFilePath}");
                debugConsoleFileLoggingAlreadyEnabled = true;
            }
        }

        // Writes log entries for the raw json requests received, regardless of matching any commands
        public static void LogWebhookRequest(HttpContext http, bool recognizedPath, string webhookURLPath)
        {
            // Get time from the request
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string queryString = $"{http.Request.Method} {http.Request.Host}{http.Request.Path}{http.Request.QueryString}";
            string matchString = recognizedPath ? $"Matches path in config: {webhookURLPath}" : $"Does not match path in config: {webhookURLPath}";

            string t = "    "; // Indentation / tab
            string logEntry = "" +
                queryString + "\n" +
                $"{t}Time: " + time + "\n" +
                $"{t}Method: " + http.Request.Method + "\n" +
                $"{t}Host: " + http.Request.Host + "\n" +
                $"{t}Path: " + http.Request.Path + "\n" +
                $"{t}{t}{matchString}\n" +
                $"{t}Parameters: \n";

            foreach (KeyValuePair<string, StringValues> queryParam in http.Request.Query)
            {
                logEntry += $"{t}{t}{queryParam.Key}: {queryParam.Value}\n";
            }
            logEntry += "\n\n--------------------------------------------------------------------------------\n\n";

            // Create the folder if it doesn't exist. If it fails (return of false), don't write the log
            if (CheckAndCreateDebugFolder() == false) { return; }

            try
            {
                File.AppendAllText(debugWebhookLogFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error writing webhook request log: {ex.Message}");
            }
        }

        public static void WriteCrashLog(Exception ex)
        {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = "" +
                $"Crash at: {time}\n" +
                $"Exception: {ex.Message}\n" +
                $"Stack trace: {ex.StackTrace}\n\n";
            string crashLogPath = Path.Combine(debugFolderPath, $"CrashLog_{timestamp}.txt");

            // Create the folder if it doesn't exist. If it fails (return of false), don't write the log
            if (CheckAndCreateDebugFolder() == false) { return; }

            try { File.AppendAllText(crashLogPath, logEntry); }
            catch (Exception e) { Trace.WriteLine($"Error writing crash log: {e.Message}"); }
        }

        public static bool CheckAndCreateDebugFolder(string? folderPath = null)
        {
            string folderPathToUse;
            if (folderPath == null)
                folderPathToUse = debugFolderPath;
            else
                folderPathToUse = folderPath;

            if (!Directory.Exists(folderPathToUse))
            {
                try
                {
                    Directory.CreateDirectory(folderPathToUse);
                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Error creating debug folder: {ex.Message}");
                    return false;
                }
            }
            return true;
        }

        // Try to create the debug folder if it doesn't exist. If it fails, try to put it in the TEMP folder, and tell the user where it is
        public static bool TryCreateInitialDebugFolder()
        {
            if (!CheckAndCreateDebugFolder()) // If it fails to create the folder in the current directory
            {
                string testNewDebugFolderPath = Path.Combine(Path.GetTempPath(), "RainmeterWebhookMonitor_DebugLogs");
                if (CheckAndCreateDebugFolder(testNewDebugFolderPath) == true)
                {
                    debugFolderPath = testNewDebugFolderPath; // Update the debug folder path to the TEMP directory
                    Trace.WriteLine($"Debug logs will be saved in: {debugFolderPath}");
                }
                else
                {
                    Trace.WriteLine($"Error creating debug folder in TEMP directory. Debug logs will be saved in: {debugFolderPath}");
                    return false;
                }
            }

            return true;
        }

    }
}
