using System;
using System.Security.Claims;
using AuthIssueTest.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.IO;
using Microsoft.OpenApi.Models;

namespace AuthIssueTest
{
	/// <summary>
	/// Startup class for configuring the server.
	/// </summary>
	public class Startup
	{
		#region Variables
		private IConfiguration _configuration;
		private IWebHostEnvironment _env;
		private readonly string _allowSpecificOrigins = "_allowSpecificOrigins";
		#endregion

		#region Constructors
		/// <summary>
		/// Creates a new instance of the class.
		/// </summary>
		/// <param name="config">An instance of IConfiguration.</param>
		/// <param name="env">The hosting environment.</param>
		public Startup(IConfiguration config, IWebHostEnvironment env)
		{
			_configuration = config;
			_env = env;
		}
		#endregion

		#region Methods
		/// <summary>
		/// This method gets called by the runtime. Use this method to add services to the container.
		/// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
		/// </summary>
		/// <param name="services">A collection of services.</param>
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddCors(options =>
			{
				options.AddPolicy(_allowSpecificOrigins,
					builder =>
					{
						builder.AllowAnyOrigin();
					}
				);
			});

			services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

			services.Configure<CookiePolicyOptions>(options =>
			{
				options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
				options.Secure = CookieSecurePolicy.Always;
				options.HandleSameSiteCookieCompatibility();
			});

			services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
					  .AddMicrosoftIdentityWebApp(_configuration, "AzureAd");

			// Uncomment the following block if the login procedure redirects you to https://localhost/signin-oidc
#if DEBUG
			// services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
			// {
			// 	var redirectHandler = options.Events.OnRedirectToIdentityProvider;
			// 	options.Events.OnRedirectToIdentityProvider = async ctx =>
			// 	{
			// 		await redirectHandler(ctx);
			// 		ctx.ProtocolMessage.RedirectUri = "https://<<the url provided by git>>.githubpreview.dev/signin-oidc";
			// 	};
			// });
#endif

			services.AddOptions();

			services.AddControllers(options => options.OutputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.StringOutputFormatter>())
					  .AddNewtonsoftJson(options =>
					  {
						  options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
					  });

			if (_env.IsProduction())
				services.Configure<MvcOptions>(options =>
				{
					options.Filters.Add(new RequireHttpsAttribute());
				});

			// -> Adds the antiforgery token to be used with angular requests.
			if (!services.Any(s => s.ServiceType.FullName == "Microsoft.AspNetCore.Mvc.ViewFeatures.Filters.AutoValidateAntiforgeryTokenAuthorizationFilter"))
				services.AddMvc();

			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new OpenApiInfo { Title = "AuthIssueTest", Version = "v1" });
			});

			services.AddAntiforgery(option => { option.HeaderName = "X-XSRF-TOKEN"; })
					 .Configure<MvcOptions>(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

			// <- Adds the antiforgery token to be used with angular requests.
		}

		/// <summary>
		/// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		/// </summary>
		/// <param name="app">The application builder.</param>
		/// <param name="loggerFactory">The logger factory.</param>
		/// <param name="provider">The service provider.</param>
		/// <param name="logger">The logger.</param>
		public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IServiceProvider provider, ILogger<Startup> logger)
		{
			if (_env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseHsts();
			}

			if (_env.IsProduction())
				app.UseHttpsRedirection();

			app.UseCors()
				.UseCookiePolicy()
				.UseRouting()
				.UseAuthentication()
				.UseAuthorization()
				.Use(async (ctx, next) =>
				{
					if (!(ctx?.User?.Identity?.IsAuthenticated ?? false))
					{
						var scheme = OpenIdConnectDefaults.AuthenticationScheme;
						var redirectUrl = ctx?.Request?.Path.Value?.ToLower() ?? "/";
						await ctx.ChallengeAsync(
						  scheme,
						  new AuthenticationProperties { RedirectUri = redirectUrl }
						);
					}
					else
						await next();
				})
				.UseMiddleware<AntiforgeryHandler>("XSRF-TOKEN")
				.Use(async (context, next) =>
				{
					var path = context.Request?.Path.Value?.ToLower() ?? "/";
					if (path.Contains("/admin") && !context.User.HasClaim(ClaimTypes.Role, "SomeAdminRole"))
					{
						context.Response.StatusCode = StatusCodes.Status401Unauthorized;
						return;
					}
					await next();
				})
				// #if DEBUG
				.UseSwagger()
				.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthIssueTest v1"))
				// #endif
				.Use(async (ctx, next) =>
				{
					ctx.Response.Headers.Add("Content-Security-Policy-Report-Only", "default-src 'self';");
					await next(ctx!);
				})
				.Use(async (context, next) =>
				{
					if (context.Response.Headers.Any(e => e.Key.ToLower() == "cache-control"))
					{
						context.Response.Headers.Remove("cache-control");
					}
					context.Response.Headers.Append("cache-control", "no-cache, no-store, must-revalidate");
					await next();
				})
				.UseEndpoints(endpoints =>
				{
					endpoints.MapControllers();
					endpoints.MapControllerRoute(
						name: "Api with action",
						pattern: "api/{controller}/{action}/{id?}"
					);
					endpoints.MapControllerRoute(
						name: "DefaultApi",
						pattern: "api/{controller}/{id?}"
					);
				})
				.Use(async (context, next) =>
				{
					await next();

					// If a path like /start was not found, assume it's an angular routing path and proceed with / as the request path
					// so tha Angular client would handle the URL
					if (context.Response.StatusCode == 404 && !Path.HasExtension(context.Request.Path.Value))
					{
						context.Request.Path = "/";
						context.Response.StatusCode = StatusCodes.Status200OK;
						await next();
					}
				})
				.UseDefaultFiles()
				.UseStaticFiles();

			if (_env.IsDevelopment())
			{
				app.UseSpa(spa =>
				{
					spa.Options.SourcePath = "../AuthIssueTest.Client";
					spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
				});
			}
		}

		#endregion
	}
}