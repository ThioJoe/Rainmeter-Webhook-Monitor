using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;

#nullable enable
#pragma warning disable IDE0028 // Simplify collection initialization. Some places it's clearer to use new() instead of []

namespace RainmeterWebhookMonitor
{
    public partial class Program
    {
        // ------------------------ Constants and globals ------------------------

        // File names
        const string appConfigJsonStem = "appsettings";
        static readonly string appConfigTemplateJsonName = $"{appConfigJsonStem}_template.json";
        static readonly string appConfigJsonName = $"{appConfigJsonStem}.json";
        const string debugLogFileName = "RainmeterWebhookMonitor_DebugLog.txt";

        // App settings names in the json file
        const string commandDelay_SettingName = "Delay_Between_Multiple_Commands_ms";
        const int defaultCommandDelay = 5;

        // Section names in the json file
        const string webhookSettings_SectionName = "WebhookSettings";
        const string rainmeterSettings_SectionName = "RainmeterSettings";
        const string applicationSettings_SectionName = "ApplicationSettings";
        const string webhookURIPath = "/rainmeter";

        // Settings names in the json file
        const string rainmeterPath_SettingName = "RainmeterPath";
        const string commandsList_SettingName = "Commands";
        const string bangCommand_SettingName = "BangCommand";
        const string measureName_SettingName = "MeasureName";
        const string skinConfigName_SettingName = "SkinConfigName";
        const string webhookParameterToUseAsValue_SettingName = "WebhookParameterToUseAsValue";
        const string optionName_SettingName = "OptionName";

        // Set up global settings
        static IConfigurationSection? rainmeterSettings;
        static List<IConfigurationSection>? commandsList;
        static IConfigurationSection? appSettings;
        static string? rainmeterPath = null;
        static int commandDelay = defaultCommandDelay;
        static string? webhookURL = null;
        static bool showSystemTrayIcon = false;

        // ----------------------------- MAIN -----------------------------
        [STAThread] // Set the application to use STA threading model
        public static void Main(string[] args)
        {
            // Set the current working directory to the directory of the executable
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Debug.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
            ProcessLaunchArgs(args);

            // Check if the json file exists, if not, create it from the embedded resource
            if (!File.Exists(appConfigJsonName))
            {
                WriteTemplateJsonFile_FromEmbeddedResource($"RainmeterWebhookMonitor.Assets.{appConfigTemplateJsonName}");
                Console.WriteLine($"Created {appConfigJsonName} template file from embedded resource.");
            }

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            ConfigureWebApp(builder);
            WebApplication app = builder.Build();

            // Load the rest of the settings from the json file
            LoadConfigFile(app);

            // Get hwnd to use for system tray icon if we can
            IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle; // Probably zero but try anyway
            SysytemTray sysytemTray = new();
            IntPtr trayHwnd = sysytemTray.InitializeNotifyIcon(hwnd); // Will return a new hidden window handle if hwnd is zero

            ConfigureEndpoints(app);
            app.Run();

            

            //// Run the app with or without a system tray icon depending on the settings
            //if (showSystemTrayIcon)
            //{
            //    Application.EnableVisualStyles();
            //    Application.SetCompatibleTextRenderingDefault(false);
            //    // Create a custom application context to handle the system tray icon separately
            //    var customContext = new CustomApplicationContext(app);
            //    Application.Run(customContext);
            //}
            //else
            //{
            //    // If no system tray icon, just run the web app directly
            //    ConfigureEndpoints(app);
            //    app.Run();
            //}
        }
        // -------------------------- End of Main -------------------------------


        // -------------------- Windows related methods -------------------

        ////CustomApplicationContext class to handle the system tray icon
        //public class CustomApplicationContext : ApplicationContext, IDisposable
        //{
        //    private readonly NotifyIcon notifyIcon;
        //    private readonly WebApplication webApp;
        //    private readonly CancellationTokenSource cancellationTokenSource;
        //    private bool _disposed = false;

        //    public CustomApplicationContext(WebApplication app)
        //    {
        //        webApp = app;
        //        cancellationTokenSource = new CancellationTokenSource();

        //        // Configure the web app (endpoints for the webhook, what to do upon receiving query, etc)
        //        ConfigureEndpoints(webApp);

        //        // Initialize NotifyIcon (System tray icon)
        //        notifyIcon = new NotifyIcon()
        //        {
        //            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
        //            Visible = true,
        //            ContextMenuStrip = new ContextMenuStrip()
        //        };

        //        // Add menu items
        //        notifyIcon.ContextMenuStrip.Items.Add("Open Config File", null, OpenConfigFile);
        //        notifyIcon.ContextMenuStrip.Items.Add("Reload Config", null, RestartApp);
        //        notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

        //        // Start web application in background
        //        Task.Run(async () =>
        //        {
        //            try
        //            {
        //                await webApp.RunAsync(cancellationTokenSource.Token);
        //            }
        //            catch (OperationCanceledException)
        //            {
        //                // Normal shutdown, no need to handle
        //            }
        //        });
        //    }

        //    // Restarts the application to reload the json file
        //    private void RestartApp(object? sender, EventArgs e)
        //    {
        //        // Cancel the current web application
        //        cancellationTokenSource.Cancel();
        //        // Dispose of the context (which includes cleanup)
        //        Dispose();
        //        // Restart the application
        //        Process.Start(Application.ExecutablePath);
        //    }

        //    private void OpenConfigFile(object? sender, EventArgs e)
        //    {
        //        string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), appConfigJsonName);
        //        try
        //        {
        //            Process.Start(new ProcessStartInfo(configFilePath) { UseShellExecute = true });
        //        }
        //        catch (Win32Exception ex)
        //        {
        //            // If there is no association, try opening with Notepad
        //            if (ex.NativeErrorCode == 1155) // ERROR_NO_ASSOCIATION
        //            {
        //                Process.Start(new ProcessStartInfo("notepad.exe", configFilePath) { UseShellExecute = true });
        //            }
        //            else
        //            {
        //                Console.WriteLine($"Error opening config file: {ex.Message}");
        //            }
        //        }
        //    }

        //    private void Exit(object? sender, EventArgs e)
        //    {
        //        // Hide tray icon immediately
        //        notifyIcon.Visible = false;

        //        // Cancel web application
        //        cancellationTokenSource.Cancel();

        //        // Dispose of the context (which includes cleanup)
        //        Dispose();

        //        // Exit the application context
        //        Application.Exit();
        //    }

        //    public new void Dispose()
        //    {
        //        Dispose(true);
        //        GC.SuppressFinalize(this);
        //    }

        //    protected override void Dispose(bool disposing)
        //    {
        //        if (!_disposed)
        //        {
        //            if (disposing)
        //            {
        //                // Dispose managed resources
        //                notifyIcon?.Dispose();
        //                cancellationTokenSource?.Dispose();
        //                webApp?.DisposeAsync().AsTask().Wait();
        //            }

        //            // Set disposed flag
        //            _disposed = true;
        //        }
        //        base.Dispose(disposing);
        //    }

        //    ~CustomApplicationContext()
        //    {
        //        Dispose(false);
        //    }
        //}

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
            Console.WriteLine(message);
            Debug.WriteLine(message);
            return Results.Problem(message);
        }

        static IResult LogSuccessToConsoleAndDebug(string message)
        {
            Console.WriteLine(message);
            Debug.WriteLine(message);
            return Results.Ok(message);
        }

        static void ConfigureEndpoints(WebApplication app)
        {
            app.MapPost(webhookURIPath, (HttpContext http) =>
            {
                string? queryParam = null;
                Dictionary<string, string> queryParamResults = [];
                List<IConfigurationSection> matchedCommandSets = [];

                if (commandsList == null)
                    return LogProblemToConsoleAndDebug("Commands list not found in json file.");

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
                        Debug.WriteLine($"Matched Query parameter in received request: {queryParam}, Value: {queryParamResults[queryParam]}");
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
                Debug.WriteLine($"Rainmeter commands:\n{string.Join("\n\t", rainmeterCommands)}");

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
            
            if (webhookURL != null)
                app.Urls.Add(webhookURL);
            else
                LogProblemToConsoleAndDebug("Webhook URL not valid. This is probably a bug!");
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

                Console.WriteLine("Error: Required settings are missing.");
                Console.WriteLine($"Missing settings: {string.Join(", ", missingSettings)}");
                return null;
            }

            // Find the value of the parameter to use as value
            if (webhookParameterToUseAsValue != null && queryParamResults.TryGetValue(webhookParameterToUseAsValue, out string? outValue))
                value = outValue;
            else
                Debug.WriteLine($"Warning: Parameter {webhookParameterToUseAsValue} not found in query parameters.");

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

        static bool LoadConfigFile(WebApplication app)
        {
            // Get AppSettings section from the json file
            appSettings = app.Configuration.GetSection(applicationSettings_SectionName);

            // Enable system tray icon if specified in the json file
            string? systemTrayIconSetting = appSettings["ShowSystemTrayIcon"];
            if (systemTrayIconSetting != null)
            {
                if (systemTrayIconSetting.Equals("true", StringComparison.OrdinalIgnoreCase))
                    showSystemTrayIcon = true;
                else if (systemTrayIconSetting.Equals("false", StringComparison.OrdinalIgnoreCase))
                    showSystemTrayIcon = false;
                else
                {
                    // Default to true if the setting is not valid or not there
                    Console.WriteLine("Error: ShowSystemTrayIcon setting in json file is not valid. Must be 'true' or 'false'. Defaulting to true");
                    showSystemTrayIcon = true;
                }
            }
            else
            {
                Console.WriteLine("Warning: ShowSystemTrayIcon setting not found in json file. Defaulting to true.");
                showSystemTrayIcon = true; // Default to true if the setting is not there
            }
                
            // Collect user settings from the json file
            rainmeterSettings = app.Configuration.GetSection(rainmeterSettings_SectionName);
            // Get each path within the section
            commandsList = (List<IConfigurationSection>)rainmeterSettings.GetSection(commandsList_SettingName).GetChildren();

            rainmeterPath = rainmeterSettings[rainmeterPath_SettingName];
            if (rainmeterPath == null || string.IsNullOrEmpty(rainmeterPath))
            {
                Console.WriteLine($"Error: Rainmeter path not found in {appConfigJsonName} file");
                return false;
            }

            if (commandsList == null || commandsList.Count == 0)
            {
                Console.WriteLine($"Error: Command list is empty in {appConfigJsonName} file");
                return false;
            }

            string? commandDelayString = app.Configuration[$"{applicationSettings_SectionName}:{commandDelay_SettingName}"];
            commandDelay = commandDelayString != null ? int.Parse(commandDelayString) : defaultCommandDelay;

            webhookURL = $"http://localhost:{app.Configuration[$"{webhookSettings_SectionName}:Port"]}";

            return true;

        }

        static void WriteTemplateJsonFile_FromEmbeddedResource(string resourceName)
        {
            // Get the embedded resource
            using Stream? stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Console.WriteLine("Error: Embedded resource not found.");
                return;
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

            // Write the embedded resource to a file
            using FileStream fileStream = new(newFileName, FileMode.Create);
            stream.CopyTo(fileStream);
        }

        static void ProcessLaunchArgs(string[] args)
        {
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    if (arg.Equals("-template", StringComparison.OrdinalIgnoreCase) || arg.Equals("/template", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteTemplateJsonFile_FromEmbeddedResource("RainmeterWebhookMonitor.appsettings.json");
                        Console.WriteLine($"Created {appConfigJsonName} template file from embedded resource.");
                    }
                }
            }
        }
    }
}
