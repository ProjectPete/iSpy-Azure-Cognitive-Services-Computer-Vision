using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DigitalEyes.iSpy.DetectAnalyse.Helpers
{
    class Logger
    {
        const string LogFileName = "iSpyDetectAnalyse_Log.txt";
        const string LogFolderName = "iSpyDetectAnalyse";

        static string logFilePath = null;
        public static string LogFilePath
        {
            get
            {
                try
                {
                    if (logFilePath == null)
                    {
                        // Can also use CommonApplicationData, or MyDocuments
                        logFilePath = Path.Combine(
                            System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            LogFolderName);

                        //MessageBox.Show($"Logging to {logFilePath}");   // Uncomment to check AppData/Roaming folder

                        // Create directory if first time
                        if (!Directory.Exists(logFilePath))
                        {
                            Directory.CreateDirectory(logFilePath);
                        }

                        // Create file if first time
                        var logFile = Path.Combine(logFilePath, LogFileName);
                        if (!File.Exists(logFile))
                        {
                            using (StreamWriter sw = File.CreateText(logFile))
                            {
                                sw.WriteLine($"{DateTime.Now}: File created");
                            }
                        }

                    }

                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.ToString());
                    throw;
                }
                return logFilePath;
            }
        }

        public static bool LoggingEnabled = true;

        public static void LogError(Exception exc,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!LoggingEnabled)
                return;

            var logFile = Path.Combine(LogFilePath, LogFileName);
            using (StreamWriter sw = File.AppendText(logFile))
            {
                sw.WriteLine($"{DateTime.Now}: {memberName}: Line {sourceLineNumber}: " + exc.ToString());
            }
        }

        public static void LogMessage(string Message,
           [CallerMemberName] string memberName = "",
           [CallerFilePath] string sourceFilePath = "",
           [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!LoggingEnabled)
                return;

            var logFile = Path.Combine(LogFilePath, LogFileName);
            using (StreamWriter sw = File.AppendText(logFile))
            {
                sw.WriteLine($"{DateTime.Now}: {memberName}: Line {sourceLineNumber}: " + Message);
            }
        }
    }
}
