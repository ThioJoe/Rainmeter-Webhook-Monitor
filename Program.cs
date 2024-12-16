//using static System.Net.Mime.MediaTypeNames;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using System.Runtime.InteropServices;

#nullable enable
#pragma warning disable IDE0028 // Simplify collection initialization. Some places it's clearer to use new() instead of []

namespace RainmeterWebhookMonitor
{
    public partial class Program
    {
        // Get version number from assembly
        static readonly System.Version versionFull = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        public static readonly string versionString = $"{versionFull.Major}.{versionFull.Minor}.{versionFull.Build}";

        // ------------------------ Constants and globals ------------------------

        // File names
        const string appConfigJsonStem = "appsettings";
        static readonly string appConfigTemplateJsonName = $"{appConfigJsonStem}_template.json";
        public static readonly string templateConfigResource = $"RainmeterWebhookMonitor.Assets.{appConfigTemplateJsonName}";
        public static readonly string appConfigJsonName = $"{appConfigJsonStem}.json";

        // App settings names in the json file
        const string commandDelay_SettingName = "Delay_Between_Multiple_Commands_ms";
        const int defaultCommandDelay = 5;
        const string debugMode_SettingName = "DebugMode";

        // Webhook setting names in the json file
        const string port_SettingName = "Port";
        const string URLPath_SettingName = "URL_Path";

        // Section names in the json file
        public const string webhookSettings_SectionName = "WebhookSettings";
        public const string rainmeterSettings_SectionName = "RainmeterSettings";
        public const string applicationSettings_SectionName = "ApplicationSettings";

        // Settings names in the json file
        public const string rainmeterPath_SettingName = "RainmeterPath";
        public const string commandsList_SettingName = "Commands";
        public const string bangCommand_SettingName = "BangCommand";
        public const string measureName_SettingName = "MeasureName";
        public const string skinConfigName_SettingName = "SkinConfigName";
        public const string webhookParameterToUseAsValue_SettingName = "WebhookParameterToUseAsValue";
        public const string optionName_SettingName = "OptionName";

        // Set up global settings
        static IConfigurationSection? rainmeterSettings;
        static List<IConfigurationSection>? commandsList;
        static IConfigurationSection? appSettings;
        static IConfigurationSection? webhookSettings;
        static string? rainmeterPath = null;
        static int commandDelay = defaultCommandDelay;
        static string? webhookURL = null;
        static bool showSystemTrayIcon = false;
        static string webhookURLPath = "/rainmeter";

        // Debug related
        public static bool debugMode = false;
        

        // Declare SystemTray object at class level so it doesn't get garbage collected
        private static SystemTray? systemTray;

        // Import the AllocConsole function from kernel32.dll
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        // ----------------------------- MAIN -----------------------------
        [STAThread] // Set the application to use STA threading model
        public static void Main(string[] args)
        {
            // Set the current working directory to the directory of the executable
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Trace.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
            ProcessLaunchArgs(args);

            // Check if the json file exists, if not, create it from the embedded resource
            if (!File.Exists(appConfigJsonName))
            {
                WriteTemplateJsonFile_FromEmbeddedResource(templateConfigResource);
                Trace.WriteLine($"Created {appConfigJsonName} template file from embedded resource.");
            }

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            ConfigureWebApp(builder);
            WebApplication app = builder.Build();

            // Load the rest of the settings from the json file
            LoadConfigFile(app);

            if (debugMode)
                Logging.EnableDebugConsoleFileLogging();

            // Run the app with or without a system tray icon depending on the settings
            if (showSystemTrayIcon)
            {
                // Get hwnd of a main window to use for system tray item
                IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle; // Probably zero but try anyway
                systemTray = new SystemTray(hwnd);

                ConfigureEndpoints(app);
                app.Run();
            }
            else
            {
                // If no system tray icon, just run the web app directly
                ConfigureEndpoints(app);
                app.Run();
            }
        }
        // -------------------------- End of Main -------------------------------




        // --------------------- Web App Configuration -------------------

        static void ConfigureWebApp(WebApplicationBuilder builder)
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Configuration.AddJsonFile(appConfigJsonName, optional: false, reloadOnChange: true);
        }

        static IResult LogProblemToConsoleAndDebug(string message)
        {
            Trace.WriteLine(message);
            return Results.Text(message);
        }

        static IResult LogSuccessToConsoleAndDebug(string message)
        {
            Trace.WriteLine(message);
            return Results.Text(message);
        }

        static void ConfigureEndpoints(WebApplication app)
        {
            app.MapPost(webhookURLPath, (HttpContext http) =>
            {
                string? queryParam = null;
                Dictionary<string, string> queryParamResults = [];
                List<IConfigurationSection> matchedCommandSets = [];
                // ------------------------------------------

                // Log the raw json request before anything else
                if (debugMode)
                    Logging.LogWebhookRequest(http, recognizedPath: true, webhookURLPath:webhookURLPath);

                if (commandsList == null)
                    return LogProblemToConsoleAndDebug("Commands list not found in json file. Skipping any processing of received request.");

                // See which commands specified by the user match any query parameters in the request, and store them
                foreach (IConfigurationSection commandSettings in commandsList)
                {
                    queryParam = commandSettings[webhookParameterToUseAsValue_SettingName];
                    if (queryParam == null || string.IsNullOrEmpty(queryParam))
                        return LogProblemToConsoleAndDebug($"Error: A query parameter to use was not found in a command section in {appConfigJsonName} file");

                    // Check if the query parameter is in the request
                    if (http.Request.Query.ContainsKey(queryParam) && http.Request.Query[queryParam].Count > 0)
                    {
                        string? paramValue = http.Request.Query[queryParam];
                        if (paramValue == null) // Empty string is ok but null is not
                            return LogProblemToConsoleAndDebug($"Error: Query parameter ({queryParam}) not found in the received webhook message. It wasn't simply an empty string result, it was apparently not included at all. Did you specified the right parameter in {appConfigJsonName}?");

                        queryParamResults[queryParam] = paramValue;
                        matchedCommandSets.Add(commandSettings);

                        // Output log with the query parameter and its value
                        Trace.WriteLine($"Matched Query parameter in received request: {queryParam}, Value: {queryParamResults[queryParam]}");
                    }
                }

                // Create list of commands to send to Rainmeter
                List<string> rainmeterCommands = new();

                int currentCommandIndex = 1;
                foreach (IConfigurationSection commandSet in matchedCommandSets)
                {
                    if (rainmeterPath == null) // Shouldn't be null but just in case to satisfy compiler
                        return LogProblemToConsoleAndDebug("Rainmeter path not found in json file.");

                    string? rainmeterCommandArgs = CreateRainmeterCommandArgs(rainmeterPath, commandSettings: commandSet, queryParamResults: queryParamResults, argsOnly: true);

                    // An empty string is ok, but null is a problem
                    if (rainmeterCommandArgs == null)
                        return LogProblemToConsoleAndDebug($"Failed to create Rainmeter command set from json file. Command set number: {currentCommandIndex}");
                    else
                    {
                        rainmeterCommands.Add(rainmeterCommandArgs);
                    }
                    currentCommandIndex++;
                }

                // If there are no commands to send, return a problem result
                if (rainmeterCommands.Count == 0)
                    return LogProblemToConsoleAndDebug("No commands to send to Rainmeter.");

                // Debug print the commands list to send to Rainmeter
                Trace.WriteLine($"Rainmeter commands:\n    {string.Join("\n    ", rainmeterCommands)}");

                // Send each command to Rainmeter with a delay between each one as set in the json file
                foreach (string rainmeterCommandArgs in rainmeterCommands)
                {
                    // Run the shell command to send the info/command to Rainmeter
                    // Run the shell command to send the info/command to Rainmeter
                    try
                    {
                        ProcessStartInfo psi = new()
                        {
                            FileName = rainmeterPath,
                            Arguments = rainmeterCommandArgs,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Trace.WriteLine($"Full command:  {psi.FileName} {psi.Arguments}");
                        using Process? process = Process.Start(psi);
                        // Optionally wait for the process to complete
                        // process?.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        return LogProblemToConsoleAndDebug($"Error sending command: {ex.Message}");
                    }

                    // Delay between each command
                    if (rainmeterCommands.Count > 1)
                    {
                        Thread.Sleep(commandDelay);
                    }
                }

                return LogSuccessToConsoleAndDebug($"Finished processing request.");

            });

            // A wildcard route to catch all other URL paths not matched
            if (debugMode)
            {
                app.Map("{*url}", (HttpContext http) =>
                {
                    // Log the request details
                    Logging.LogWebhookRequest(http, recognizedPath: false, webhookURLPath: webhookURLPath);
                    string nonMatchingPath = http.Request.Path;
                    return LogProblemToConsoleAndDebug($"Received request to webhook URL path that does not match that in the config: {nonMatchingPath}");
                });
            }
            
            // Add the webhook URL to the list of URLs to listen on
            if (webhookURL != null)
                app.Urls.Add(webhookURL);
            else
                LogProblemToConsoleAndDebug("Webhook URL path is null! Is it set in the config? It shouldn't have gotten to this point, this could be a bug.");
        }

        // ------------------------- Other Methods ------------------------

        static string? CreateRainmeterCommandArgs(string rainMeterPath, IConfigurationSection commandSettings, Dictionary<string, string> queryParamResults, bool argsOnly)
        {
            string? rainmeterPath = rainMeterPath;

            string? bangCommand = commandSettings[bangCommand_SettingName];
            string? measureName = commandSettings[measureName_SettingName];
            string? skinConfigName = commandSettings[skinConfigName_SettingName];
            string? webhookParameterToUseAsValue = commandSettings[webhookParameterToUseAsValue_SettingName];
            string? optionName = commandSettings[optionName_SettingName];

            string value = ""; // It's possible that the value is an empty string so allow that. Null will be used for problems.

            // Print error for required settings
            // Skin config name is technically optional but usually required
            if (string.IsNullOrWhiteSpace(rainmeterPath) || string.IsNullOrWhiteSpace(bangCommand) || string.IsNullOrWhiteSpace(measureName) || optionName == null)
            {
                List<string> missingSettings = new();

                if (string.IsNullOrWhiteSpace(rainmeterPath))
                    missingSettings.Add(nameof(rainmeterPath));
                if (string.IsNullOrWhiteSpace(bangCommand))
                    missingSettings.Add(nameof(bangCommand));
                if (string.IsNullOrWhiteSpace(measureName))
                    missingSettings.Add(nameof(measureName));
                if (optionName == null)
                    missingSettings.Add(nameof(optionName));

                Trace.WriteLine("Error: Required settings are missing.");
                Trace.WriteLine($"Missing settings: {string.Join(", ", missingSettings)}");
                return null;
            }

            // Find the value of the parameter to use as value
            if (webhookParameterToUseAsValue != null && queryParamResults.TryGetValue(webhookParameterToUseAsValue, out string? outValue))
                value = outValue;
            else
                Trace.WriteLine($"Warning: Parameter {webhookParameterToUseAsValue} not found in query parameters.");

            // ---- Further processing ------
            // Add exclamation to bang command if it doesn't have it
            if (!bangCommand.StartsWith('!'))
                bangCommand = "!" + bangCommand;

            // Construct the command string
            if (argsOnly)
                return $"{bangCommand} {measureName} {optionName} {value} {skinConfigName}";
            else
                return $"\"{rainmeterPath}\" {bangCommand} {measureName} {optionName} {value} {skinConfigName}";
        }



        static bool CheckJsonBoolSetting(IConfigurationSection section, string settingName, bool defaultValue)
        {
            string? setting = section[settingName];
            if (setting != null)
            {
                if (setting.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (setting.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;
                else
                {
                    Trace.WriteLine($"Error: {settingName} setting in json file is not valid. Must be 'true' or 'false'. Defaulting to {defaultValue}");
                    return defaultValue;
                }
            }
            else
            {
                Trace.WriteLine($"Warning: {settingName} setting not found in json file. Defaulting to {defaultValue}.");
                return defaultValue;
            }
        }

        static bool LoadConfigFile(WebApplication app)
        {
            // Get settings sections from the json file
            appSettings = app.Configuration.GetSection(applicationSettings_SectionName);
            rainmeterSettings = app.Configuration.GetSection(rainmeterSettings_SectionName);
            webhookSettings = app.Configuration.GetSection(webhookSettings_SectionName);

            // Enable system tray icon if specified in the json file
            showSystemTrayIcon = CheckJsonBoolSetting(section: appSettings, settingName: "ShowSystemTrayIcon", defaultValue: true);

            debugMode = CheckJsonBoolSetting(section: appSettings, settingName: debugMode_SettingName, defaultValue: false);

            // Get each path within the section
            commandsList = (List<IConfigurationSection>)rainmeterSettings.GetSection(commandsList_SettingName).GetChildren();

            rainmeterPath = rainmeterSettings[rainmeterPath_SettingName];
            if (rainmeterPath == null || String.IsNullOrEmpty(rainmeterPath))
            {
                Trace.WriteLine($"Error: Rainmeter path not found in {appConfigJsonName} file");
                return false;
            }

            if (commandsList == null || commandsList.Count == 0)
            {
                Trace.WriteLine($"Error: Command list is empty in {appConfigJsonName} file");
                return false;
            }

            if (webhookSettings == null)
            {
                Trace.WriteLine($"Error: Webhook settings not found in {appConfigJsonName} file");
                return false;
            }

            string? commandDelayString = app.Configuration[$"{applicationSettings_SectionName}:{commandDelay_SettingName}"];
            commandDelay = commandDelayString != null ? int.Parse(commandDelayString) : defaultCommandDelay;

            webhookURL = $"http://localhost:{app.Configuration[$"{webhookSettings_SectionName}:{port_SettingName}"]}";

            // Process the URL 'path' which is the part after the port number, like '/rainmeter'
            string? tempURLPath = webhookSettings[URLPath_SettingName];
            if (tempURLPath == null || string.IsNullOrEmpty(tempURLPath))
                Trace.WriteLine($"Error: Webhook URL path not found in {appConfigJsonName} file. Using \"/rainmeter\" as default.");
            else
            {
                // If there's a backslash, replace with a forward slash
                tempURLPath = tempURLPath.Replace('\\', '/');

                // If there's a slash  at the end, remove it
                if (tempURLPath.EndsWith('/'))
                    tempURLPath = tempURLPath.Substring(0, tempURLPath.Length - 1);

                // If there's no leading slash, add one
                if (!tempURLPath.StartsWith('/'))
                    tempURLPath = "/" + tempURLPath;

                webhookURLPath = tempURLPath;
            }

            return true;

        }

        public static string? WriteTemplateJsonFile_FromEmbeddedResource(string resourceName)
        {
            // Get the embedded resource
            using Stream? stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Trace.WriteLine("Error: Embedded resource not found.");
                return null;
            }

            // If the file name already exists, append a number to the end of the file name to avoid overwriting
            int fileNumber = 1;
            string newFileName = appConfigJsonName;
            while (File.Exists(newFileName))
            {
                fileNumber++;
                newFileName = $"{appConfigJsonStem}_{fileNumber}.json";
            }

            // Get current directory path of the exe
            string outputFilePath = Path.Combine(Directory.GetCurrentDirectory(), newFileName);

            try
            {
                // Write the embedded resource to a file
                using FileStream fileStream = new(outputFilePath, FileMode.Create);
                stream.CopyTo(fileStream);
                return outputFilePath;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error writing template file: {ex.Message}");
                return null;
            }
        }

        

        static void ProcessLaunchArgs(string[] args)
        {
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    if (arg.Equals("-template", StringComparison.OrdinalIgnoreCase) || arg.Equals("/template", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteTemplateJsonFile_FromEmbeddedResource(templateConfigResource);
                        Trace.WriteLine($"Created {appConfigJsonName} template file from embedded resource.");
                    }

                    if (arg.Equals("-debug", StringComparison.OrdinalIgnoreCase) || arg.Equals("/debug", StringComparison.OrdinalIgnoreCase))
                    {
                        // Set up a debug log file
                        Logging.EnableDebugConsoleFileLogging();

                        // Also enable console output via a console window
                        AllocConsole();

                        // Add a ConsoleTraceListener to redirect Trace output to the console
                        Trace.Listeners.Add(new ConsoleTraceListener());
                    }
                }
            }
        }

    } // ----------------------------- End of Program class -----------------------------

    public static class NativeMessageBox
    {
        // Import the MessageBox function from user32.dll
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        // MB_OK constant from WinUser.h
        private const uint MB_OK = 0x00000000;

        public static void ShowInfoMessage(string message, string title)
        {
            // Show message box with MB_OK style (just OK button)
            // First parameter is IntPtr.Zero for no parent window
            var result = MessageBox(IntPtr.Zero, message, title, MB_OK);
        }

        public static void ShowErrorMessage(string message, string title)
        {
            // Show message box with MB_ICONERROR style (error icon)
            // First parameter is IntPtr.Zero for no parent window
            const uint MB_ICONERROR = 0x00000010;
            var result = MessageBox(IntPtr.Zero, message, title, MB_OK | MB_ICONERROR);
        }

    } // --------------- End of NativeMessageBox class ---------------

}
