using Microsoft.AspNetCore.SignalR;
using MVS_Project.Data;
using MVS_Project.HUBS;
using MVS_Project.Models;

namespace MVS_Project.Services
{
    public class SimulatedGpsService : IGpsDataService, IHostedService
    {
        private readonly IServiceProvider _services;
        private Timer? _timer;
        private readonly string _countryCode = "AF"; // Configure per country

        public SimulatedGpsService(IServiceProvider services)
        {
            _services = services;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(UpdatePositions, null, 0, 5000); // Every 5s
            return Task.CompletedTask;
        }

        private async void UpdatePositions(object? state)
        {
            using var scope = _services.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrackingHub>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var positions = await GetLatestPositionsAsync(_countryCode);

            foreach (var pos in positions)
            {
                // Save to database
                dbContext.LocationHistory.Add(new LocationHistory
                {
                    CarId = pos.CarId,
                    Latitude = pos.Latitude,
                    Longitude = pos.Longitude,
                    Timestamp = DateTime.UtcNow
                });

                // Update car last tracked
                var car = await dbContext.Car.FindAsync(pos.CarId);
                if (car != null) car.LastTracked = DateTime.UtcNow;

                // Broadcast via SignalR
                await hubContext.Clients.All.SendAsync("ReceiveCarUpdate",
                    pos.CarId, pos.Latitude, pos.Longitude);
            }

            await dbContext.SaveChangesAsync();
        }

        public async Task<IEnumerable<CarPosition>> GetLatestPositionsAsync(string countryCode)
        {
            // Afghanistan bounding box coordinates
            const double minLat = 29.3772;
            const double maxLat = 38.4911;
            const double minLng = 60.5042;
            const double maxLng = 74.9157;

            return new List<CarPosition>
    {
        new(1,
            minLat + (Random.Shared.NextDouble() * (maxLat - minLat)),
            minLng + (Random.Shared.NextDouble() * (maxLng - minLng))),
        new(2,
            minLat + (Random.Shared.NextDouble() * (maxLat - minLat)),
            minLng + (Random.Shared.NextDouble() * (maxLng - minLng)))
    };

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            return Task.CompletedTask;
        }

    }
}
