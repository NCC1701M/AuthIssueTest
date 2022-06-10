using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuthIssueTest
{
	/// <summary>
	/// Contains the main method of this application.
	/// </summary>
	public class Program
	{
		/// <summary>
		/// The main method of the application.
		/// </summary>
		/// <param name="args">The command line arguments.</param>
		public static void Main(string[] args)
		{
			BuildWebHost(args).Run();
		}

		/// <summary>
		/// Builds the web host of the application.
		/// </summary>
		/// <param name="args">The arguments for building the web host.</param>
		/// <returns></returns>
		public static IWebHost BuildWebHost(string[] args) =>
			new WebHostBuilder()
				.UseKestrel()
				.ConfigureAppConfiguration((hostingContext, config) =>
				{
					var env = hostingContext.HostingEnvironment;
					config.AddCommandLine(args)
							.SetBasePath(Directory.GetCurrentDirectory())
							.AddJsonFile("serversettings.json", true)
							.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
							.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
							.AddJsonFile($"appsettings.local.json", optional: true, reloadOnChange: true)
							.AddEnvironmentVariables();
				})
				.ConfigureLogging((hostingContext, logging) =>
				{
					if (hostingContext.HostingEnvironment.IsDevelopment())
					{
						logging.AddConsole();
						logging.AddDebug();
					}
				})
				.UseIISIntegration()
				.UseDefaultServiceProvider((ContextBoundObject, options) =>
				{
					options.ValidateScopes = ContextBoundObject.HostingEnvironment.IsDevelopment();
				})
				.UseContentRoot(Directory.GetCurrentDirectory())
				.UseStartup<Startup>()
				.Build();
	}
}