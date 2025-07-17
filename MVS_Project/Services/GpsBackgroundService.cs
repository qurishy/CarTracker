using MVS_Project.Services;
using Microsoft.AspNetCore.SignalR;
using MVS_Project.HUBS;

namespace MVS_Project.Services
{
    public class GpsBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GpsBackgroundService> _logger;
        private readonly IConfiguration _config;

        public GpsBackgroundService(IServiceProvider serviceProvider,
            ILogger<GpsBackgroundService> logger, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = _config.GetValue<int>("GpsApi:UpdateIntervalSeconds", 30);
            var countryCode = _config.GetValue<string>("GpsApi:DefaultCountryCode", "AF");

            _logger.LogInformation("GPS Background Service started. Update interval: {Interval}s, Country: {Country}",
                intervalSeconds, countryCode);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var gpsService = scope.ServiceProvider.GetRequiredService<IGpsDataService>();
                    var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrackingHub>>();

                    var positions = await gpsService.GetLatestPositionsAsync(countryCode);

                    if (positions.Any())
                    {
                        // Broadcast updates via SignalR
                        foreach (var position in positions)
                        {
                            await hubContext.Clients.All.SendAsync("ReceiveCarUpdate",
                                position.CarId, position.Latitude, position.Longitude, stoppingToken);
                        }

                        _logger.LogInformation("Fetched and broadcasted {Count} GPS positions", positions.Count());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in GPS background service");
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }
    }
}
