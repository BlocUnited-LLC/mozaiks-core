using System.Net;
using Payment.API.Infrastructure.Observability;
using Stripe;

namespace Payment.API.Middlewares
{
    public class CustomExceptionHandlerMiddleware : IMiddleware
    {
        private readonly ILogger<CustomExceptionHandlerMiddleware> _logger;
        private readonly ICorrelationContextAccessor _correlation;
        private readonly IWebHostEnvironment _environment;

        public CustomExceptionHandlerMiddleware(ILogger<CustomExceptionHandlerMiddleware> logger, ICorrelationContextAccessor correlation, IWebHostEnvironment environment)
        {
            _logger = logger;
            _correlation = correlation;
            _environment = environment;
        }
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                string logInfo = $"\n                    Request started. Method: {context.Request.Method}, Path: {context.Request.Path}, QueryString: {context.Request.QueryString}";

                _logger.LogInformation(logInfo);

                await next(context);

                _logger.LogInformation("Request completed. Status Code: {StatusCode}", context.Response.StatusCode);

            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "ERR: An unhandled exception occurred. correlationId={CorrelationId}", _correlation.CorrelationId);
                await HandleExceptionAsync(context, ex);
            }
        }
        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var detail = GetSafeDetail(exception);
            var response = new
            {
                errorCode = "internal_error",
                message = "An unexpected error occurred.",
                detail,
                correlationId = _correlation.CorrelationId
            };

            return context.Response.WriteAsJsonAsync(response);
        }

        private string? GetSafeDetail(Exception exception)
        {
            if (!_environment.IsDevelopment())
            {
                return null;
            }

            if (exception is StripeException stripeException)
            {
                return stripeException.StripeError?.Code ?? "PaymentProviderError";
            }

            return exception.Message;
        }
    }
}
