using GPS_DataSender_Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace GPS_DataSender_Api.HUB
{
    /// <summary>
    /// Simple SignalR Hub for GPS tracking in Afghanistan
    /// Handles three main operations: Track single car, Track all cars, Untrack
    /// </summary>
    public class GpsHub : Hub
    {
        private readonly IGpsTrackingService _trackingService;
        private readonly ILogger<GpsHub> _logger;

        public GpsHub(IGpsTrackingService trackingService, ILogger<GpsHub> logger)
        {
            _trackingService = trackingService;
            _logger = logger;
        }

        /// <summary>
        /// Start tracking a specific car
        /// Client will receive continuous updates for this car only
        /// </summary>
        /// <param name="carId">ID of the car to track</param>
        public async Task TrackCar(int carId)
        {
            if (carId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "Invalid car ID");
                return;
            }

            // Register this client to track specific car
            await _trackingService.StartTrackingCarAsync(Context.ConnectionId, carId);

            _logger.LogInformation($"Client {Context.ConnectionId} started tracking car {carId}");

            // Send current position immediately
            var currentPosition = await _trackingService.GetCarPositionAsync(carId);
            if (currentPosition != null)
            {
                await Clients.Caller.SendAsync("CarPosition", currentPosition);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", $"Car {carId} not found");
            }
        }

        /// <summary>
        /// Start tracking all cars in Afghanistan
        /// Client will receive continuous updates for all cars
        /// </summary>
        public async Task TrackAllCars()
        {
            // Register this client to track all cars
            await _trackingService.StartTrackingAllCarsAsync(Context.ConnectionId);

            _logger.LogInformation($"Client {Context.ConnectionId} started tracking all cars");

            // Send current positions of all cars immediately
            var allPositions = await _trackingService.GetAllCarPositionsAsync();
            await Clients.Caller.SendAsync("AllCarPositions", allPositions);
        }

        /// <summary>
        /// Stop tracking (either specific car or all cars)
        /// Client will no longer receive updates
        /// </summary>
        public async Task Untrack()
        {
            await _trackingService.StopTrackingAsync(Context.ConnectionId);
            _logger.LogInformation($"Client {Context.ConnectionId} stopped tracking");
            await Clients.Caller.SendAsync("TrackingStopped", "You have stopped receiving location updates");
        }

        /// <summary>
        /// Get list of available car IDs (one-time request)
        /// </summary>
        public async Task GetAvailableCars()
        {
            var carIds = await _trackingService.GetAvailableCarIdsAsync();
            await Clients.Caller.SendAsync("AvailableCars", carIds);
        }

        /// <summary>
        /// Called when client disconnects - cleanup tracking
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _trackingService.StopTrackingAsync(Context.ConnectionId);
            _logger.LogInformation($"Client {Context.ConnectionId} disconnected and tracking cleaned up");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called when client connects
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client {Context.ConnectionId} connected");
            await Clients.Caller.SendAsync("Connected", "Welcome to Afghanistan GPS Tracking");
            await base.OnConnectedAsync();
        }
    }
}

