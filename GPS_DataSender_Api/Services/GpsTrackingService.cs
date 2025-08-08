namespace GPS_DataSender_Api.Services
{
    using global::MVS_Project.Models;
    using GPS_DataSender_Api.HUB;
    using Microsoft.AspNetCore.SignalR;
    using GPS_DataSender_Api.HUB;

    using System.Collections.Concurrent;

    namespace MVS_Project.Services
    {
        /// <summary>
        /// GPS tracking service for Afghanistan
        /// Manages car positions and client subscriptions with continuous updates
        /// </summary>
        public class GpsTrackingService : IGpsTrackingService, IDisposable
        {
            // Afghanistan boundaries
            private const double MIN_LATITUDE = 29.3772;
            private const double MAX_LATITUDE = 38.4911;
            private const double MIN_LONGITUDE = 60.5042;
            private const double MAX_LONGITUDE = 74.9157;

            // Thread-safe collections for managing state
            private readonly ConcurrentDictionary<int, CarPosition> _carPositions;
            private readonly ConcurrentDictionary<string, int> _clientTrackingSpecificCar; // connectionId -> carId
            private readonly ConcurrentHashSet<string> _clientsTrackingAllCars; // connectionIds tracking all cars

            private readonly IHubContext<GpsHub> _hubContext;
            private readonly ILogger<GpsTrackingService> _logger;
            private Timer? _updateTimer;
            private bool _disposed = false;

            public GpsTrackingService(IHubContext<GpsHub> hubContext, ILogger<GpsTrackingService> logger)
            {
                _hubContext = hubContext;
                _logger = logger;
                _carPositions = new ConcurrentDictionary<int, CarPosition>();
                _clientTrackingSpecificCar = new ConcurrentDictionary<string, int>();
                _clientsTrackingAllCars = new ConcurrentHashSet<string>();

                InitializeAfghanistanCars();
            }

            /// <summary>
            /// Initialize sample cars in Afghanistan with random positions
            /// </summary>
            private void InitializeAfghanistanCars()
            {
                _logger.LogInformation("Initializing cars for Afghanistan");

                // Create 10 sample cars with random positions within Afghanistan
                for (int carId = 1; carId <= 10; carId++)
                {
                    var latitude = MIN_LATITUDE + (Random.Shared.NextDouble() * (MAX_LATITUDE - MIN_LATITUDE));
                    var longitude = MIN_LONGITUDE + (Random.Shared.NextDouble() * (MAX_LONGITUDE - MIN_LONGITUDE));

                    var position = new CarPosition(carId, latitude, longitude);
                    _carPositions[carId] = position;

                    _logger.LogDebug($"Initialized car {carId} at position {latitude:F6}, {longitude:F6}");
                }
            }

            /// <summary>
            /// Register a client to track a specific car
            /// </summary>
            public async Task StartTrackingCarAsync(string connectionId, int carId)
            {
                // Remove from all cars tracking if previously tracking all
                _clientsTrackingAllCars.TryRemove(connectionId);

                // Add to specific car tracking
                _clientTrackingSpecificCar[connectionId] = carId;

                _logger.LogInformation($"Client {connectionId} now tracking car {carId}");
                await Task.CompletedTask;
            }

            /// <summary>
            /// Register a client to track all cars
            /// </summary>
            public async Task StartTrackingAllCarsAsync(string connectionId)
            {
                // Remove from specific car tracking if previously tracking specific car
                _clientTrackingSpecificCar.TryRemove(connectionId, out _);

                // Add to all cars tracking
                _clientsTrackingAllCars.Add(connectionId);

                _logger.LogInformation($"Client {connectionId} now tracking all cars");
                await Task.CompletedTask;
            }

            /// <summary>
            /// Stop tracking for a client (remove from all tracking lists)
            /// </summary>
            public async Task StopTrackingAsync(string connectionId)
            {
                _clientTrackingSpecificCar.TryRemove(connectionId, out _);
                _clientsTrackingAllCars.TryRemove(connectionId);

                _logger.LogInformation($"Client {connectionId} stopped all tracking");
                await Task.CompletedTask;
            }

            /// <summary>
            /// Get current position of a specific car
            /// </summary>
            public async Task<CarPosition?> GetCarPositionAsync(int carId)
            {
                _carPositions.TryGetValue(carId, out var position);
                return await Task.FromResult(position);
            }

            /// <summary>
            /// Get current positions of all cars
            /// </summary>
            public async Task<IEnumerable<CarPosition>> GetAllCarPositionsAsync()
            {
                return await Task.FromResult(_carPositions.Values.ToList());
            }

            /// <summary>
            /// Get list of all available car IDs
            /// </summary>
            public async Task<IEnumerable<int>> GetAvailableCarIdsAsync()
            {
                return await Task.FromResult(_carPositions.Keys.ToList());
            }

            /// <summary>
            /// Start continuous position updates
            /// This runs in background and sends updates to all connected clients
            /// </summary>
            public async Task StartContinuousUpdatesAsync()
            {
                if (_updateTimer != null)
                    return;

                _logger.LogInformation("Starting continuous GPS updates for Afghanistan");

                // Update and broadcast every 2 seconds
                _updateTimer = new Timer(async _ => await UpdateAndBroadcastPositions(),
                    null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

                await Task.CompletedTask;
            }

            /// <summary>
            /// Stop continuous updates
            /// </summary>
            public async Task StopContinuousUpdatesAsync()
            {
                _updateTimer?.Dispose();
                _updateTimer = null;
                _logger.LogInformation("Stopped continuous GPS updates");
                await Task.CompletedTask;
            }

            /// <summary>
            /// Update all car positions and broadcast to appropriate clients
            /// This is the heart of the continuous update system
            /// </summary>
            private async Task UpdateAndBroadcastPositions()
            {
                try
                {
                    var updatedPositions = new List<CarPosition>();

                    // Update positions for all cars
                    foreach (var carId in _carPositions.Keys.ToList())
                    {
                        var currentPosition = _carPositions[carId];

                        // Simulate small movement (max 0.01 degrees ≈ ~1km)
                        var deltaLat = (Random.Shared.NextDouble() - 0.5) * 0.02; // -0.01 to +0.01
                        var deltaLng = (Random.Shared.NextDouble() - 0.5) * 0.02; // -0.01 to +0.01

                        var newLat = Math.Max(MIN_LATITUDE, Math.Min(MAX_LATITUDE, currentPosition.Latitude + deltaLat));
                        var newLng = Math.Max(MIN_LONGITUDE, Math.Min(MAX_LONGITUDE, currentPosition.Longitude + deltaLng));

                        var newPosition = new CarPosition(carId, newLat, newLng);
                        _carPositions[carId] = newPosition;
                        updatedPositions.Add(newPosition);
                    }

                    // Broadcast to clients tracking all cars
                    if (_clientsTrackingAllCars.Count > 0)
                    {
                        var allCarsClients = _clientsTrackingAllCars.ToList();
                        await _hubContext.Clients.Clients(allCarsClients)
                            .SendAsync("AllCarPositions", updatedPositions);
                    }

                    // Broadcast to clients tracking specific cars
                    foreach (var specificTracking in _clientTrackingSpecificCar.ToList())
                    {
                        var connectionId = specificTracking.Key;
                        var carId = specificTracking.Value;

                        var carPosition = updatedPositions.FirstOrDefault(p => p.CarId == carId);
                        if (carPosition != null)
                        {
                            await _hubContext.Clients.Client(connectionId)
                                .SendAsync("CarPosition", carPosition);
                        }
                    }

                    _logger.LogDebug($"Updated and broadcasted positions for {updatedPositions.Count} cars");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating and broadcasting positions");
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _updateTimer?.Dispose();
                    _disposed = true;
                    _logger.LogInformation("GpsTrackingService disposed");
                }
            }
        }
        /// <summary>
        /// Thread-safe HashSet for managing client connections
        /// </summary>
        public class ConcurrentHashSet<T> : IDisposable
        {
            private readonly HashSet<T> _hashSet = new HashSet<T>();
            private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

            public void Add(T item)
            {
                _lock.EnterWriteLock();
                try
                {
                    _hashSet.Add(item);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            public bool TryRemove(T item)
            {
                _lock.EnterWriteLock();
                try
                {
                    return _hashSet.Remove(item);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            public List<T> ToList()
            {
                _lock.EnterReadLock();
                try
                {
                    return new List<T>(_hashSet);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            public int Count
            {
                get
                {
                    _lock.EnterReadLock();
                    try
                    {
                        return _hashSet.Count;
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }
                }
            }

            public void Dispose()
            {
                _lock?.Dispose();
            }
        }
    }
}