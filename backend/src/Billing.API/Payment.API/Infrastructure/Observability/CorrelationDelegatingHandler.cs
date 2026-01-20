using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Payment.API.Infrastructure.Observability
{
    public class CorrelationDelegatingHandler : DelegatingHandler
    {
        private readonly ICorrelationContextAccessor _accessor;

        public CorrelationDelegatingHandler(ICorrelationContextAccessor accessor)
        {
            _accessor = accessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var correlationId = _accessor.CorrelationId;
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                request.Headers.Remove(CorrelationIdMiddleware.HeaderName);
                request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
