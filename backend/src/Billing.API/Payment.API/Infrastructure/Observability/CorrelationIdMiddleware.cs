using System.Diagnostics;

namespace Payment.API.Infrastructure.Observability
{
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "x-correlation-id";

        private readonly RequestDelegate _next;
        private readonly ObservabilityMetrics _metrics;

        public CorrelationIdMiddleware(RequestDelegate next, ObservabilityMetrics metrics)
        {
            _next = next;
            _metrics = metrics;
        }

        public async Task InvokeAsync(HttpContext context, ICorrelationContextAccessor accessor)
        {
            var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
            var correlationId = Guid.TryParse(incoming, out _) ? incoming! : Guid.NewGuid().ToString();

            accessor.CorrelationId = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            var sw = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                _metrics.RecordApiRequestLatency(sw.Elapsed);
            }
        }
    }
}
