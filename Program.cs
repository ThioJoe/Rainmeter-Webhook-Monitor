using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace FluxWebhookMonitor
{
    public class Program
    {
        [STAThread] // Set the application to use STA threading model
        public static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var builder = WebApplication.CreateBuilder(args);
            ConfigureWebApp(builder);

            var app = builder.Build();
            ConfigureEndpoints(app);

            var thread = new Thread(() =>
            {
                app.Run();
            });
            thread.Start();

            Application.Run(new SystrayApplicationContext());
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
                var settings = app.Configuration.GetSection("WebhookSettings");
                var outputFile = settings["OutputFileName"];
                var queryParam = settings["QueryParameter"];
                var paramValue = http.Request.Query[queryParam];

                if (!string.IsNullOrWhiteSpace(paramValue))
                {
                    try
                    {
                        File.WriteAllText(outputFile, paramValue);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error writing to file: {ex.Message}");
                        return Results.Problem("Failed to write to file.");
                    }
                    return Results.Ok($"Received {queryParam}: {paramValue}");
                }
                return Results.BadRequest($"{queryParam} parameter is missing");
            });
            string url = $"http://localhost:{app.Configuration["WebhookSettings:Port"]}";
            app.Urls.Add(url);
        }
    }
}
