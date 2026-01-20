using System.Threading;

namespace Payment.API.Infrastructure.Observability
{
    public interface ICorrelationContextAccessor
    {
        string CorrelationId { get; set; }
    }

    public class CorrelationContextAccessor : ICorrelationContextAccessor
    {
        private static readonly AsyncLocal<string?> Correlation = new AsyncLocal<string?>();

        public string CorrelationId
        {
            get => Correlation.Value ?? string.Empty;
            set => Correlation.Value = value;
        }
    }
}
