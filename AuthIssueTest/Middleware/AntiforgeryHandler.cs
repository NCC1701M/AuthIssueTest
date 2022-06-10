using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AuthIssueTest.Middleware
{
	internal class AntiforgeryHandler
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<AntiforgeryHandler> _logger;
		private readonly IAntiforgery _forgery;
		private readonly string _cookieName;

		/// <summary>
		/// Creates a new instance of this class.
		/// </summary>
		/// <param name="next">Dependency-Injection for the <see cref="RequestDelegate" />.</param>
		/// <param name="logger">Dependency-Injection for the <see cref="ILogger{AntiforgeryHandler}" />.</param>
		/// <param name="forgery">Dependency-Injection for the <see cref="IAntiforgery" />.</param>
		/// <param name="cookieName">The optional name of the cookie.</param>
		public AntiforgeryHandler(RequestDelegate next, ILogger<AntiforgeryHandler> logger, IAntiforgery forgery, string cookieName)
		{
			_next = next;
			_logger = logger;
			_forgery = forgery;
			_cookieName = cookieName;
		}

		/// <summary>
		/// Handles the request and adds the antiforgery token.
		/// </summary>
		/// <param name="ctx">Der aktuelle <see cref="HttpContext" />.</param>
		/// <returns></returns>
		public async Task Invoke(HttpContext ctx)
		{
			var tokens = _forgery.GetAndStoreTokens(ctx);
			ctx.Response.Cookies.Append(_cookieName, tokens.RequestToken, new CookieOptions { HttpOnly = false, Path = "/", SameSite = SameSiteMode.Strict });
			await _next(ctx);
		}
	}
}