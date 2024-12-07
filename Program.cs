using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

#nullable enable

namespace FluxWebhookMonitor
{
    public class Program
    {
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
            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        }

        static void ConfigureEndpoints(WebApplication app)
        {
            app.MapPost("/mywebhook", (HttpContext http) =>
            {
                Dictionary<string,string> queryParamResults = new Dictionary<string, string>();

                IConfigurationSection webhookSettings = app.Configuration.GetSection("WebhookSettings");
                //string? outputFile = webhookSettings["OutputFileName"];
                string? queryParam = webhookSettings["QueryParameter"];
                string? paramValue = http.Request.Query[queryParam];

                // Doing this as a dictionary in case later we want to support more than one query parameter. In which case we can use a loop or something
                if (queryParam != null && paramValue != null)
                {
                    queryParamResults[queryParam] = paramValue; 
                }

                IConfigurationSection rainmeterSettings = app.Configuration.GetSection("RainmeterSettings");
                string rainmeterCommand = CreateRainmeterCommand(rainmeterSettings, queryParamResults);

                // An empty string is ok, but null is a problem
                if (rainmeterCommand == null)
                    return Results.Problem("Failed to create Rainmeter command.");

                // Debug print the command
                Debug.WriteLine($"Rainmeter command: {rainmeterCommand}");

                // Send the command to Rainmeter
                try
                {
                    // Send the command silently through shell, not through cmd window
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {rainmeterCommand}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending command: {ex.Message}");
                    return Results.Problem("Failed to write to file.");
                }

                return Results.Ok($"Received {queryParam}: {paramValue}");

            });
            string url = $"http://localhost:{app.Configuration["WebhookSettings:Port"]}";
            app.Urls.Add(url);
        }

        static string? CreateRainmeterCommand(IConfigurationSection rainmeterSettings, Dictionary<string, string> queryParamResults)
        {
            string? rainmeterPath = rainmeterSettings["RainmeterPath"];
            string? bangCommand = rainmeterSettings["BangCommand"];
            string? measureName = rainmeterSettings["MeasureName"];
            string? skinConfigName = rainmeterSettings["SkinConfigName"];
            string? parameterToUseAsValue = rainmeterSettings["ParameterToUseAsValue"];
            string? optionName = rainmeterSettings["OptionName"];

            string value = ""; // It's possible that the value is an empty string so allow that. Null will be used for problems.

            // Print error for required settings
            // Skin config name is technically optional but usually required
            if (string.IsNullOrWhiteSpace(rainmeterPath) || string.IsNullOrWhiteSpace(bangCommand) || string.IsNullOrWhiteSpace(measureName) || optionName == null)
            {
                List<string> missingSettings = new List<string>();

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
            if (parameterToUseAsValue != null && queryParamResults.ContainsKey(parameterToUseAsValue))
                value = queryParamResults[parameterToUseAsValue];
            else
                Debug.WriteLine($"Warning: Parameter {parameterToUseAsValue} not found in query parameters.");

            return $"\"{rainmeterPath}\" {bangCommand} {measureName} {optionName} {value} {skinConfigName}";
        }
    }
}
