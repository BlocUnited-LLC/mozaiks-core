

using AuthServer.Api.Shared;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AuthServer.Api.Middlewares
{
    public class CustomExceptionHandlerMiddleware : IMiddleware
    {
        private readonly ILogger<CustomExceptionHandlerMiddleware> _logger;

        public CustomExceptionHandlerMiddleware(ILogger<CustomExceptionHandlerMiddleware> logger)
        {
            _logger = logger;

        }
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
			try
			{
                string logInfo = $@"
                    Request started. Method: {context.Request.Method}, Path: {context.Request.Path}, QueryString: {context.Request.QueryString}";

                _logger.LogInformation(logInfo);

               
                await next(context);

                _logger.LogInformation("Request completed. Status Code: {StatusCode}", context.Response.StatusCode);
                
            }
			catch (Exception ex)
			{

                _logger.LogError(ex, "ERR: An unhandled exception occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }
        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                message = "An unexpected error occurred.",
                detail = exception.Message
            };

            return context.Response.WriteAsJsonAsync(response);
        }
    }
}
