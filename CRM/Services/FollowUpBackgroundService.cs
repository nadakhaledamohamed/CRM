using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CRM.Services;

namespace CRM.BackgroundServices
{
    /// <summary>
    /// Background service that runs auto-closure process daily
    /// </summary>
    public class FollowUpBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FollowUpBackgroundService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(24); // Run daily

        public FollowUpBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<FollowUpBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAutoClosures();
                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    _logger.LogInformation("Follow-up background service is stopping.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing follow-up background service.");
                    // Continue running even if there's an error
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Retry after 30 minutes
                }
            }
        }

        private async Task ProcessAutoClosures()
        {
            _logger.LogInformation("Starting auto-closure process at {Time}", DateTime.Now);

            using var scope = _serviceProvider.CreateScope();
            var followUpService = scope.ServiceProvider.GetRequiredService<IFollowUpService>();

            try
            {
                await followUpService.ProcessAutoClosureAsync();
                _logger.LogInformation("Auto-closure process completed successfully at {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-closure process at {Time}", DateTime.Now);
                throw;
            }
        }
    }

    /// <summary>
    /// Extension methods for service registration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFollowUpServices(this IServiceCollection services)
        {
            // ✅ Register the correct implementation of IFollowUpService
            services.AddScoped<IFollowUpService, FollowUpAutomationService>();

            // ✅ Register the hosted background service
            services.AddHostedService<FollowUpBackgroundService>();

            return services;
        }
    }
}
