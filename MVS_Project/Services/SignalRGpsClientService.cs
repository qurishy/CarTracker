using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using MVS_Project.Data;
using MVS_Project.HUBS;
using MVS_Project.Models;

namespace MVS_Project.Services
{
    /// <summary>
    /// Service that connects to the external SignalR GPS API and processes incoming data
    /// This acts as a SignalR client to your GPS API and a data processor for your MVC app
    /// </summary>
    public class SignalRGpsClientService : IGpsDataService, IDisposable
    {
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<TrackingHub> _hubContext;
        private readonly ILogger<SignalRGpsClientService> _logger;

        private HubConnection? _connection;
        private bool _disposed = false;

        // Events for position updates
        public event EventHandler<CarPosition>? CarPositionUpdated;
        public event EventHandler<List<CarPosition>>? MultiplePositionsUpdated;

        public SignalRGpsClientService(
            IConfiguration config,
            IServiceProvider serviceProvider,
            IHubContext<TrackingHub> hubContext,
            ILogger<SignalRGpsClientService> logger)
        {
            _config = config;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Start real-time updates by connecting to the external SignalR GPS API
        /// </summary>
        public async Task StartRealtimeUpdatesAsync()
        {
            try
            {
                // Get the SignalR GPS API URL from configuration
                var gpsApiUrl = _config["GpsSignalRApi:Url"] ?? "https://localhost:5159/gps";

                _logger.LogInformation("Connecting to GPS SignalR API at {Url}", gpsApiUrl);

                // Build connection to the external GPS SignalR API
                _connection = new HubConnectionBuilder()
                    .WithUrl(gpsApiUrl)
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                    .Build();

                // Set up event handlers for incoming data
                SetupSignalREventHandlers();

                // Start the connection
                await _connection.StartAsync();

                _logger.LogInformation("Successfully connected to GPS SignalR API");

                // Subscribe to track all cars from Afghanistan
                await _connection.InvokeAsync("TrackAllCars");

                _logger.LogInformation("Subscribed to track all cars in Afghanistan");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start real-time GPS updates");
                throw;
            }
        }

        /// <summary>
        /// Set up event handlers for incoming SignalR data from GPS API
        /// </summary>
        private void SetupSignalREventHandlers()
        {
            if (_connection == null) return;

            // Handle connection established
            _connection.On<string>("Connected", (message) =>
            {
                _logger.LogInformation("GPS API says: {Message}", message);
            });

            // Handle single car position updates
            _connection.On<CarPosition>("CarPosition", async (position) =>
            {
                _logger.LogDebug("Received car position: Car {CarId} at {Lat}, {Lng}",
                    position.CarId, position.Latitude, position.Longitude);

                await ProcessSingleCarPosition(position);
            });

            // Handle multiple car positions (when tracking all cars)
            _connection.On<List<CarPosition>>("AllCarPositions", async (positions) =>
            {
                _logger.LogDebug("Received {Count} car positions", positions.Count);
                await ProcessMultipleCarPositions(positions);
            });

            // Handle errors from GPS API
            _connection.On<string>("Error", (errorMessage) =>
            {
                _logger.LogError("GPS API Error: {Error}", errorMessage);
            });

            // Handle reconnection
            _connection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation("Reconnected to GPS API with connection ID: {ConnectionId}", connectionId);
                // Re-subscribe to all cars after reconnection
                return _connection.InvokeAsync("TrackAllCars");
            };
        }

        /// <summary>
        /// Process a single car position update
        /// 1. Save to database
        /// 2. Broadcast to frontend via SignalR
        /// </summary>
        /// <param name="position">Car position data</param>
        private async Task ProcessSingleCarPosition(CarPosition position)
        {
            try
            {
                // Save to database
                await SavePositionToDatabase(position);

                // Broadcast to frontend clients
                await _hubContext.Clients
             .Group($"Car_{position.CarId}")
             .SendAsync("CarPositionUpdate", position);

                await _hubContext.Clients
                    .Group("AllCars")
                    .SendAsync("CarPositionUpdate", position);
                // Raise event for other services
                CarPositionUpdated?.Invoke(this, position);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing car position for car {CarId}", position.CarId);
            }
        }

        /// <summary>
        /// Process multiple car position updates
        /// </summary>
        /// <param name="positions">List of car positions</param>
        private async Task ProcessMultipleCarPositions(List<CarPosition> positions)
        {
            try
            {
                // Save all positions to database
                await SavePositionsToDatabase(positions);

                // Broadcast to frontend clients
                // Replace broadcast call with direct client messaging
                var tasks = new List<Task>();
                foreach (var pos in positions)
                {
                    tasks.Add(_hubContext.Clients
                        .Group($"Car_{pos.CarId}")
                        .SendAsync("CarPositionUpdate", pos));
                }

                tasks.Add(_hubContext.Clients
                    .Group("AllCars")
                    .SendAsync("MultipleCarPositions", positions));

                await Task.WhenAll(tasks);

                // Raise event for other services
                MultiplePositionsUpdated?.Invoke(this, positions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multiple car positions");
            }
        }

        /// <summary>
        /// Save a single car position to the database
        /// </summary>
        /// <param name="position">Car position to save</param>
        private async Task SavePositionToDatabase(CarPosition position)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                // Check if car exists
                var car = await dbContext.Car.FindAsync(position.CarId);

                if (car == null)
                {
                    // Create new car without setting Id explicitly
                    car = new Cars
                    {
                        // Remove Id assignment here
                        LicensePlate = $"AF-{position.CarId:D4}",
                        Make = "Unknown",
                        Model = "Unknown",
                        LastTracked = position.Timestamp
                    };
                    dbContext.Car.Add(car);
                }
                else
                {
                    car.LastTracked = position.Timestamp;
                }

                // Add location history
                dbContext.LocationHistory.Add(new LocationHistory
                {
                    CarId = position.CarId,
                    Latitude = position.Latitude,
                    Longitude = position.Longitude,
                    Timestamp = position.Timestamp
                });

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving car position to database for car {CarId}", position.CarId);
            }
        }

        /// <summary>
        /// Save multiple car positions to the database efficiently
        /// </summary>
        /// <param name="positions">List of positions to save</param>
        private async Task SavePositionsToDatabase(List<CarPosition> positions)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var carIds = positions.Select(p => p.CarId).Distinct().ToList();
                var existingCars = await dbContext.Car
                    .Where(c => carIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);

                var newCars = new List<Cars>();
                var locationHistoryEntries = new List<LocationHistory>();

                foreach (var position in positions)
                {
                    // Handle car creation
                    if (!existingCars.TryGetValue(position.CarId, out var car))
                    {
                        car = new Cars
                        {
                            // No Id assignment
                            LicensePlate = $"AF-{position.CarId:D4}",
                            Make = "Unknown",
                            Model = "Unknown",
                            LastTracked = position.Timestamp
                        };
                        newCars.Add(car);
                        //existingCars[position.CarId] = car;
                    }
                    else
                    {
                        car.LastTracked = position.Timestamp;


                        // Add location history
                        locationHistoryEntries.Add(new LocationHistory
                        {
                            CarId = car.Id,
                            Latitude = position.Latitude,
                            Longitude = position.Longitude,
                            Timestamp = position.Timestamp
                        });
                    }
                }

                // Add new cars and save changes
                if (newCars.Any())
                {
                    await dbContext.Car.AddRangeAsync(newCars);
                    await dbContext.SaveChangesAsync(); // Save new cars first
 
                    foreach (var car in newCars)
                {
                    var position = positions.First(p => $"AF-{p.CarId:D4}" == car.LicensePlate);

                    locationHistoryEntries.Add(new LocationHistory
                    {
                        CarId = car.Id,
                        Latitude = position.Latitude,
                        Longitude = position.Longitude,
                        Timestamp = position.Timestamp
                    });
                }


                }

                // Add location history and save
                dbContext.LocationHistory.AddRange(locationHistoryEntries);
                await dbContext.SaveChangesAsync();



            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving multiple car positions to database");
            }
        }

        /// <summary>
        /// Get latest positions from database (for REST API compatibility)
        /// </summary>
        public async Task<IEnumerable<CarPosition>> GetLatestPositionsAsync(string countryCode)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                // Get the latest position for each car
                var latestPositions = await dbContext.LocationHistory
                    .GroupBy(lh => lh.CarId)
                    .Select(g => g.OrderByDescending(lh => lh.Timestamp).First())
                    .Select(lh => new CarPosition(lh.CarId, lh.Latitude, lh.Longitude)
                    {
                        Timestamp = lh.Timestamp
                    })
                    .ToListAsync();

                return latestPositions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest positions from database");
                return Enumerable.Empty<CarPosition>();
            }
        }

        /// <summary>
        /// Get specific car position from database
        /// </summary>
        public async Task<CarPosition?> GetCarPositionAsync(int carId, string countryCode)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var latestPosition = await dbContext.LocationHistory
                    .Where(lh => lh.CarId == carId)
                    .OrderByDescending(lh => lh.Timestamp)
                    .FirstOrDefaultAsync();

                if (latestPosition != null)
                {
                    return new CarPosition(latestPosition.CarId, latestPosition.Latitude, latestPosition.Longitude)
                    {
                        Timestamp = latestPosition.Timestamp
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting car position from database for car {CarId}", carId);
            }

            return null;
        }

        /// <summary>
        /// Stop real-time updates and disconnect from GPS API
        /// </summary>
        public async Task StopRealtimeUpdatesAsync()
        {
            if (_connection != null)
            {
                try
                {
                    // Untrack from GPS API
                    await _connection.InvokeAsync("Untrack");

                    // Stop connection
                    await _connection.StopAsync();
                    _logger.LogInformation("Disconnected from GPS SignalR API");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping real-time updates");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.DisposeAsync();
                _disposed = true;
            }
        }
    }
}
