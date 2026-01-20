using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Payment.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Payment.API.Infrastructure.Observability
{
    public class MetricBackfillHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ObservabilityMetrics _metrics;
        private readonly ILogger<MetricBackfillHostedService> _logger;

        public MetricBackfillHostedService(
            IServiceProvider serviceProvider,
            ObservabilityMetrics metrics,
            ILogger<MetricBackfillHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting Metric Backfill...");

                using var scope = _serviceProvider.CreateScope();
                var transactionService = scope.ServiceProvider.GetRequiredService<TransactionService>();
                var pendingRefunds = await transactionService.CountPendingAsync("Refund");
                var pendingSettlements = await transactionService.CountPendingAsync("Settlement");
                
                _metrics.RefillPendingRefundsGauge(pendingRefunds);
                _metrics.RefillPendingSettlementsGauge(pendingSettlements);

                _logger.LogInformation("Metric Backfill Completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metric backfill");
                // We don't want to crash the app startup, so we swallow the error but record it
                // _metrics.RecordWorkerError("Backfill"); // If we had a generic worker error metric
            }
            
            await Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
