// Services/RealGpsService.cs
using MVS_Project.Data;
using MVS_Project.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MVS_Project.Services
{
    public class RealGpsService : IGpsDataService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RealGpsService> _logger;

        public RealGpsService(
            HttpClient httpClient,
            IConfiguration config,
            IServiceProvider serviceProvider,
            ILogger<RealGpsService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<IEnumerable<CarPosition>> GetLatestPositionsAsync(string countryCode)
        {
            try
            {
                var baseUrl = _config["GpsApi:BaseUrl"] ?? "http://localhost:5159";
                var endpoint = $"{baseUrl}/api/gps/positions/{countryCode}";

                _logger.LogInformation("Fetching GPS positions from {Endpoint}", endpoint);

                var response = await _httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var positions = JsonSerializer.Deserialize<List<CarPosition>>(
                        jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (positions != null && positions.Any())
                    {
                        await SavePositionsToDatabase(positions);
                        return positions;
                    }
                }
                else
                {
                    _logger.LogWarning("GPS API returned status code: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GPS positions for country: {CountryCode}", countryCode);
            }

            return Enumerable.Empty<CarPosition>();
        }

        public async Task<CarPosition?> GetCarPositionAsync(int carId, string countryCode)
        {
            try
            {
                var baseUrl = _config["GpsApi:BaseUrl"] ?? "http://localhost:5159";
                var endpoint = $"{baseUrl}/api/gps/car/{carId}/position?countryCode={countryCode}";

                _logger.LogInformation("Fetching position for car {CarId} from {Endpoint}", carId, endpoint);

                var response = await _httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var position = JsonSerializer.Deserialize<CarPosition>(
                        jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (position != null)
                    {
                        await SavePositionsToDatabase(new[] { position });
                        return position;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching position for car {CarId} in country {CountryCode}", carId, countryCode);
            }

            return null;
        }

        private async Task SavePositionsToDatabase(IEnumerable<CarPosition> positions)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var carIds = positions.Select(p => p.CarId).Distinct().ToList();
                var existingCars = await dbContext.Car
                    .Where(c => carIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);

                foreach (var position in positions)
                {
                    if (!existingCars.TryGetValue(position.CarId, out var car))
                    {
                        car = new Cars
                        {
                            LicensePlate = $"AF-{position.CarId:D4}",
                            Make = "Unknown",
                            Model = "Unknown",
                            LastTracked = position.Timestamp
                        };
                        dbContext.Car.Add(car);
                        existingCars[position.CarId] = car;
                    }
                    else
                    {
                        car.LastTracked = position.Timestamp;
                    }

                    dbContext.LocationHistory.Add(new LocationHistory
                    {
                        CarId = position.CarId,
                        Latitude = position.Latitude,
                        Longitude = position.Longitude,
                        Timestamp = position.Timestamp
                    });
                }

                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Saved {Count} GPS positions to database", positions.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving GPS positions to database");
            }
        }
    }
}