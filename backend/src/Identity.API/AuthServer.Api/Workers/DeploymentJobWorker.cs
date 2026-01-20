using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;

namespace AuthServer.Api.Workers;

public sealed class DeploymentJobWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeploymentJobWorker> _logger;

    public DeploymentJobWorker(IServiceProvider serviceProvider, ILogger<DeploymentJobWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deployment job worker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IDeploymentJobRepository>();
                var deployment = scope.ServiceProvider.GetRequiredService<IDeploymentService>();

                var next = await jobs.ClaimNextQueuedAsync(stoppingToken);
                if (next is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Processing deployment job {JobId} for app {AppId}", next.Id, next.AppId);
                await deployment.ProcessJobAsync(next.Id!, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in deployment job worker loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Deployment job worker stopping");
    }
}

