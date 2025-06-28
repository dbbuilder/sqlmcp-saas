using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace SqlMcpPoc.ConsoleApp
{
    /// <summary>
    /// Main entry point for the SQLMCP.net Proof of Concept console application
    /// Handles command line parsing, dependency injection setup, and application orchestration
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Application entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code (0 for success, non-zero for failure)</returns>
        public static async Task<int> Main(string[] args)
        {
            // Configure early logging for startup
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                Log.Information("SQLMCP.net Proof of Concept starting...");
                Log.Information("Version: {Version}", typeof(Program).Assembly.GetName().Version);
                Log.Information("Container ID: {ContainerId}", Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName);

                if (args.Length == 0)
                {
                    Console.WriteLine("SQLMCP.net POC - Natural Language to SQL Translation");
                    Console.WriteLine("Usage: SqlMcpPoc.ConsoleApp.exe \"your natural language query\"");
                    Console.WriteLine("Example: SqlMcpPoc.ConsoleApp.exe \"Show me all customers from California\"");
                    return 0;
                }

                var query = string.Join(" ", args);
                Console.WriteLine($"Processing query: {query}");
                Console.WriteLine("Note: This is a proof of concept. Full implementation coming soon!");

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "SQLMCP.net Proof of Concept terminated unexpectedly: {ErrorMessage}", ex.Message);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
