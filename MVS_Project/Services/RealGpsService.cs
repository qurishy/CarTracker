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

        public RealGpsService(HttpClient httpClient, IConfiguration config,
            IServiceProvider serviceProvider, ILogger<RealGpsService> logger)
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
                var baseUrl = _config["GpsApi:BaseUrl"] ?? "http://localhost:5159"; // Your GPS API URL
                var response = await _httpClient.GetAsync($"{baseUrl}/api/gps/positions/{countryCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var positions = JsonSerializer.Deserialize<List<CarPosition>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (positions != null && positions.Any())
                    {
                        // Save to database
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
                var response = await _httpClient.GetAsync($"{baseUrl}/api/gps/car/{carId}/position?countryCode={countryCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var position = JsonSerializer.Deserialize<CarPosition>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (position != null)
                    {
                        // Save single position to database
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
                foreach (var position in positions)
                {
                    // Check if car exists, if not create it
                    var car = await dbContext.Car.FindAsync(position.CarId);
                    if (car == null)
                    {
                        car = new Cars
                        {
                            //Id = position.CarId,
                            LicensePlate = $"AF-{position.CarId:D4}",
                            Make = "Unknown",
                            Model = "Unknown",
                            LastTracked = DateTime.UtcNow
                        };
                        dbContext.Car.Add(car);
                    }
                    else
                    {
                        car.LastTracked = DateTime.UtcNow;
                    }

                    // Add location history
                    var locationHistory = new LocationHistory
                    {
                        CarId = position.CarId,
                        Latitude = position.Latitude,
                        Longitude = position.Longitude,
                        Timestamp = position.Timestamp
                    };

                    dbContext.LocationHistory.Add(locationHistory);
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