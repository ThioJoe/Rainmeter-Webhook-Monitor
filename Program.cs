using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

#nullable enable

namespace RainmeterWebhookMonitor
{
    public class Program
    {
        const string appConfigJsonName = "appsettings.json";

        // Section names in the json file
        const string webhookSettingsSectionName = "WebhookSettings";
        const string rainmeterSettingsSectionName = "RainmeterSettings";

        // Settings names in the json file
        const string rainmeterPathSettingName = "RainmeterPath";
        const string bangCommandSettingName = "BangCommand";
        const string measureNameSettingName = "MeasureName";
        const string skinConfigNameSettingName = "SkinConfigName";
        const string webhookParameterToUseAsValueSettingName = "WebhookParameterToUseAsValue";
        const string optionNameSettingName = "OptionName";


        [STAThread] // Set the application to use STA threading model
        public static void Main(string[] args)
        {
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

        static void ConfigureWebApp(WebApplicationBuilder builder)
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Configuration.AddJsonFile(appConfigJsonName, optional: true, reloadOnChange: true);
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
            app.MapPost("/rainmeter", (HttpContext http) =>
            {
                // Collect user settings from the json file
                IConfigurationSection rainmeterSettings = app.Configuration.GetSection(rainmeterSettingsSectionName);

                string? rainMeterPath = rainmeterSettings[rainmeterPathSettingName];
                if (rainMeterPath == null || string.IsNullOrEmpty(rainMeterPath))
                    return LogProblemToConsoleAndDebug($"Error: Rainmeter path not found in {appConfigJsonName} file");


                string? queryParam = rainmeterSettings[webhookParameterToUseAsValueSettingName];
                if (queryParam == null || string.IsNullOrEmpty(queryParam))
                    return LogProblemToConsoleAndDebug($"Error: Query parameter not found in {appConfigJsonName} file");


                // Get the user-specified query parameter from the request
                string? paramValue = http.Request.Query[queryParam];
                if (paramValue == null) // Empty string is ok but null is not
                    return LogProblemToConsoleAndDebug($"Error: Query parameter ({queryParam}) not found in the received webhook message. It wasn't simply an empty string result, it was apparently not included at all. Did you specified the right parameter in {appConfigJsonName}?");

                // Plcae the query and its result value in the dictionary. This will allow multiple query parameters to be used in the future.
                Dictionary<string, string> queryParamResults = [];
                queryParamResults[queryParam] = paramValue;

                string? rainmeterCommandArgs = CreateRainmeterCommandArgs(rainmeterSettings: rainmeterSettings, queryParamResults: queryParamResults, argsOnly: true);

                // An empty string is ok, but null is a problem
                if (rainmeterCommandArgs == null)
                    return LogProblemToConsoleAndDebug("Failed to create Rainmeter command.");

                // Debug print the command
                Debug.WriteLine($"Rainmeter command: {rainmeterCommandArgs}");

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

                return LogSuccessToConsoleAndDebug($"Received {queryParam}: {paramValue}");

            });
            string url = $"http://localhost:{app.Configuration[$"{webhookSettingsSectionName}:Port"]}";
            app.Urls.Add(url);
        }

        static string? CreateRainmeterCommandArgs(IConfigurationSection rainmeterSettings, Dictionary<string, string> queryParamResults, bool argsOnly)
        {
            string? rainmeterPath = rainmeterSettings[rainmeterPathSettingName];
            string? bangCommand = rainmeterSettings[bangCommandSettingName];
            string? measureName = rainmeterSettings[measureNameSettingName];
            string? skinConfigName = rainmeterSettings[skinConfigNameSettingName];
            string? webhookParameterToUseAsValue = rainmeterSettings[webhookParameterToUseAsValueSettingName];
            string? optionName = rainmeterSettings[optionNameSettingName];

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

            if (argsOnly)
                return $"{bangCommand} {measureName} {optionName} {value} {skinConfigName}";
            else
                return $"\"{rainmeterPath}\" {bangCommand} {measureName} {optionName} {value} {skinConfigName}";
        }
    }
}
