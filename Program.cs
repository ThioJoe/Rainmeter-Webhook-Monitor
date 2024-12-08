using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using System.Collections.Generic;

#nullable enable

namespace RainmeterWebhookMonitor
{
    public class Program
    {
        const string appConfigJsonStem = "appsettings";
        static readonly string appConfigTemplateJsonName = $"{appConfigJsonStem}_template.json";
        static readonly string appConfigJsonName = $"{appConfigJsonStem}.json";

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


        // ----------------------------- MAIN -----------------------------
        [STAThread] // Set the application to use STA threading model
        public static void Main(string[] args)
        {
            ProcessLaunchArgs(args);

            Debug.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");

            // Check if the json file exists, if not, create it from the embedded resource
            if (!File.Exists(appConfigJsonName))
            {
                WriteTemplateJsonFile_FromEmbeddedResource($"RainmeterWebhookMonitor.Assets.{appConfigTemplateJsonName}");
                Console.WriteLine($"Created {appConfigJsonName} template file from embedded resource.");
            }

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            ConfigureWebApp(builder);

            WebApplication app = builder.Build();
            ConfigureEndpoints(app);

            Thread thread = new Thread(() =>
            {
                app.Run();
            });
            thread.Start();

        }
        // ----------------------------------------------------------------

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
                // Collect user settings from the json file
                IConfigurationSection rainmeterSettings = app.Configuration.GetSection(rainmeterSettings_SectionName);

                string? rainMeterPath = rainmeterSettings[rainmeterPath_SettingName];
                if (rainMeterPath == null || string.IsNullOrEmpty(rainMeterPath))
                    return LogProblemToConsoleAndDebug($"Error: Rainmeter path not found in {appConfigJsonName} file");

                //List<string> commandsList = rainmeterSettings.GetSection(commandsListSettingName).Get<List<string>>() ?? new List<string>();
                // Get each path within the section
                List<IConfigurationSection> commandsList = (List<IConfigurationSection>)rainmeterSettings.GetSection(commandsList_SettingName).GetChildren();

                if (commandsList.Count == 0)
                    return LogProblemToConsoleAndDebug($"Error: Command list is empty in {appConfigJsonName} file");

                string? queryParam;
                Dictionary<string, string> queryParamResults = [];
                List<IConfigurationSection> matchedCommandSets = [];

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
                    string? rainmeterCommandArgs = CreateRainmeterCommandArgs(rainMeterPath, commandSettings: commandSet, queryParamResults: queryParamResults, argsOnly: true);

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

                string? commandDelayString = app.Configuration[$"{applicationSettings_SectionName}:{commandDelay_SettingName}"];
                int commandDelay = commandDelayString != null ? int.Parse(commandDelayString) : defaultCommandDelay;

                // Send each command to Rainmeter with a delay between each one as set in the json file
                foreach (string rainmeterCommandArgs in rainmeterCommands)
                {
                    // Run the shell command to send the info/command to Rainmeter
                    // Run the shell command to send the info/command to Rainmeter
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = rainMeterPath,
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
            string url = $"http://localhost:{app.Configuration[$"{webhookSettings_SectionName}:Port"]}";
            app.Urls.Add(url);
        }

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
            if (!bangCommand.StartsWith("!"))
                bangCommand = "!" + bangCommand;

            // Construct the command string
            if (argsOnly)
                return $"{bangCommand} {measureName} {optionName} {value} {skinConfigName}";
            else
                return $"\"{rainmeterPath}\" {bangCommand} {measureName} {optionName} {value} {skinConfigName}";
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
                    if (arg.ToLower() == "-template" || arg.ToLower() == "/template")
                    {
                        WriteTemplateJsonFile_FromEmbeddedResource("RainmeterWebhookMonitor.appsettings.json");
                        Console.WriteLine($"Created {appConfigJsonName} template file from embedded resource.");
                    }
                }
            }
        }
    }
}
